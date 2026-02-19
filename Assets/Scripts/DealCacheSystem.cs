using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YG; // Пространство имен плагина

public class DealCacheSystem : MonoBehaviour
{
    public static DealCacheSystem Instance { get; private set; }

    [System.Serializable]
    public struct CacheKey
    {
        public GameType GameType;
        public Difficulty Difficulty;
        public int Param;
        public CacheKey(GameType type, Difficulty diff, int param) { GameType = type; Difficulty = diff; Param = param; }
        public override bool Equals(object obj) => obj is CacheKey k && GameType == k.GameType && Difficulty == k.Difficulty && Param == k.Param;
        public override int GetHashCode() => (GameType, Difficulty, Param).GetHashCode();
    }

    private struct CacheConfig
    {
        public Difficulty Diff;
        public int Param;
        public int TargetBufferSize;
    }

    private Dictionary<CacheKey, Queue<Deal>> dealCache = new Dictionary<CacheKey, Queue<Deal>>();
    private Dictionary<GameType, List<CacheConfig>> cacheRequirements = new Dictionary<GameType, List<CacheConfig>>();
    private Dictionary<GameType, BaseGenerator> generatorRegistry = new Dictionary<GameType, BaseGenerator>();

    [Header("Settings")]
    [SerializeField] private int defaultBufferSize = 10;

    [Header("Starter Pack")]
    public DealDatabase database;

    private Queue<CacheKey> generationQueue = new Queue<CacheKey>();
    private bool isGenerating = false;

    public bool IsReady { get; private set; } = false;

    private Deal currentActiveDeal = null;
    private CacheKey currentActiveKey;
    private bool dealWasPlayed = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeCacheConfigs();
            RegisterGenerators();
            
            // Вызываем получение данных сразу при старте (как в примере SaverTest)
            GetData(); 
        }
        else Destroy(gameObject);
    }

    // --- ПОДПИСКА НА СОБЫТИЯ YG2 ---
    private void OnEnable() => YG2.onGetSDKData += GetData;
    private void OnDisable() => YG2.onGetSDKData -= GetData;

    private void OnApplicationQuit()
    {
        ReturnActiveDealToQueue();
        SaveToCloud();
    }

    // --- ЛОГИКА ДАННЫХ ---

    // Метод, который вызывается плагином при получении данных
    public void GetData()
    {
        if (IsReady && dealCache.Count > 0) return; // Уже загружены

        // Читаем строку из сохранений плагина
        // ВАЖНО: Убедитесь, что добавили public string dealCacheJson; в SavesYG2.cs
        string cloudJson = YG2.saves.dealCacheJson;

        if (string.IsNullOrEmpty(cloudJson))
        {
            Debug.Log("[DealCache] Cloud empty/Init. Loading Starter Pack...");
            LoadStarterPack();
            // Не сохраняем сразу, чтобы не спамить запросами при старте. 
            // Сохраним при первом изменении кэша.
        }
        else
        {
            Debug.Log("[DealCache] Found cloud data. Unpacking...");
            DeserializeAndUnpack(cloudJson);
        }

        IsReady = true;
        CheckBufferHealth();
    }

    private void SaveToCloud()
    {
        try
        {
            CompressedWrapper wrapper = new CompressedWrapper();
            wrapper.entries = new List<CompressedEntry>();

            foreach (var kvp in dealCache)
            {
                if (kvp.Value.Count == 0) continue;

                CompressedEntry entry = new CompressedEntry
                {
                    gType = (int)kvp.Key.GameType,
                    diff = (int)kvp.Key.Difficulty,
                    param = kvp.Key.Param,
                    deals = new List<string>()
                };

                foreach (var deal in kvp.Value)
                {
                    if (IsDealValid(deal)) entry.deals.Add(DealSerializer.Serialize(deal));
                }
                wrapper.entries.Add(entry);
            }

            string json = JsonUtility.ToJson(wrapper);

            // ЗАПИСЫВАЕМ В ПЛАГИН
            YG2.saves.dealCacheJson = json;
            YG2.SaveProgress(); // Отправляем на сервер

            // Debug.Log($"[DealCache] Saved. Size: {json.Length / 1024f:F2} KB");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DealCache] Save Failed: {e.Message}");
        }
    }

    // --- API МЕТОДЫ ---

    public Deal GetDeal(GameType type, Difficulty diff, int param)
    {
        ReturnActiveDealToQueue();
        var key = new CacheKey(type, diff, param);

        if (dealCache.ContainsKey(key) && dealCache[key].Count > 0)
        {
            currentActiveDeal = dealCache[key].Dequeue();
            currentActiveKey = key;
            dealWasPlayed = false;

            SaveToCloud(); // Сохраняем изменение (расклад забран)
            CheckBufferHealth();

            Debug.Log($"[DealCache] Served Deal: {type} {diff}. Left: {dealCache[key].Count}");
            return currentActiveDeal;
        }

        Debug.LogWarning($"[DealCache] Cache Empty for {type} {diff}!");
        CheckBufferHealth();
        return null;
    }

    // Метод для GameUIController
    public void DiscardActiveDeal()
    {
        currentActiveDeal = null;
        dealWasPlayed = false;
        // Не возвращаем в очередь -> он удаляется
    }

    public void ReturnActiveDealToQueue()
    {
        if (currentActiveDeal != null && !dealWasPlayed)
        {
            if (!dealCache.ContainsKey(currentActiveKey)) dealCache[currentActiveKey] = new Queue<Deal>();
            dealCache[currentActiveKey].Enqueue(currentActiveDeal);
            SaveToCloud();
        }
        currentActiveDeal = null;
    }

    // --- ВНУТРЕННЯЯ ЛОГИКА ---

    private void DeserializeAndUnpack(string json)
    {
        dealCache.Clear();
        try
        {
            CompressedWrapper wrapper = JsonUtility.FromJson<CompressedWrapper>(json);
            if (wrapper != null && wrapper.entries != null)
            {
                foreach (var entry in wrapper.entries)
                {
                    var key = new CacheKey((GameType)entry.gType, (Difficulty)entry.diff, entry.param);
                    var queue = new Queue<Deal>();
                    foreach (string sDeal in entry.deals)
                    {
                        Deal d = DealSerializer.Deserialize(sDeal);
                        if (IsDealValid(d)) queue.Enqueue(d);
                    }
                    dealCache[key] = queue;
                }
            }
        }
        catch { LoadStarterPack(); }
    }

    private void LoadStarterPack()
    {
        bool dataFound = false;

        // 1. Пробуем загрузить из ScriptableObject (если назначен)
        if (database != null && database.dealSets != null)
        {
            foreach (var set in database.dealSets)
            {
                var key = new CacheKey(set.gameType, set.difficulty, set.param);
                if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();

                foreach (var sDeal in set.deals)
                {
                    Deal d = UnpackLegacyDeal(sDeal);
                    if (IsDealValid(d)) dealCache[key].Enqueue(d);
                }
            }
            if (database.dealSets.Count > 0) dataFound = true;
        }

        // 2. ДОБАВЛЕНО: Загрузка JSON файлов из папки Resources/InitialDeals
        TextAsset[] files = Resources.LoadAll<TextAsset>("InitialDeals");
        if (files.Length > 0)
        {
            Debug.Log($"[DealCache] Found {files.Length} files in Resources. Parsing...");
            foreach (var file in files)
            {
                try
                {
                    // Пытаемся распарсить файл сохранения
                    SaveDataWrapper wrapper = JsonUtility.FromJson<SaveDataWrapper>(file.text);

                    if (wrapper != null && wrapper.queues != null)
                    {
                        foreach (var qData in wrapper.queues)
                        {
                            var key = new CacheKey(qData.type, qData.diff, qData.param);
                            if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();

                            foreach (var sDeal in qData.deals)
                            {
                                Deal d = UnpackLegacyDeal(sDeal);
                                if (IsDealValid(d)) dealCache[key].Enqueue(d);
                            }
                        }
                        dataFound = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DealCache] Skipped file {file.name}: {e.Message}");
                }
            }
        }

        if (dataFound)
        {
            // Сразу сохраняем загруженное в облако, чтобы в следующий раз грузить оттуда быстрее
            SaveToCloud();
            Debug.Log("[DealCache] Starter Pack Loaded & Synced.");
        }
        else
        {
            Debug.LogError("[DealCache] CRITICAL: No deals found in Database OR Resources/InitialDeals!");
        }
    }
    private Deal UnpackDeal(SerializedDeal sDeal)
    {
        // Это копия UnpackLegacyDeal, но с понятным именем, чтобы не путаться
        return UnpackLegacyDeal(sDeal);
    }
    [ContextMenu("DEBUG: WIPE CLOUD CACHE")]
    public void ClearCloudCache()
    {
        // 1. Очищаем локальную память
        dealCache.Clear();
        generationQueue.Clear();

        // 2. Очищаем переменную сохранения в плагине
        YG2.saves.dealCacheJson = "";

        // 3. Отправляем пустоту в облако (перезаписываем старые данные)
        YG2.SaveProgress();

        // 4. Сбрасываем флаг готовности, чтобы при следующем обращении система загрузила StarterPack
        IsReady = false;

        Debug.LogError(">>> CLOUD CACHE HAS BEEN WIPED! <<<");
        Debug.Log("Please restart the game or call GetData() to reload from StarterPack.");
    }
    // --- ГЕНЕРАЦИЯ ---

    private void Update()
    {
        if (!isGenerating && generationQueue.Count > 0)
        {
            var key = generationQueue.Dequeue();
            StartCoroutine(GenerateInBackground(key));
        }
    }

    private IEnumerator GenerateInBackground(CacheKey key)
    {
        if (!generatorRegistry.ContainsKey(key.GameType)) yield break;

        isGenerating = true;
        BaseGenerator generator = generatorRegistry[key.GameType];
        Deal generatedDeal = null;
        bool done = false;

        yield return StartCoroutine(generator.GenerateDeal(key.Difficulty, key.Param, (deal, metrics) =>
        {
            generatedDeal = deal;
            done = true;
        }));

        while (!done) yield return null;

        if (generatedDeal != null && IsDealValid(generatedDeal))
        {
            if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();
            dealCache[key].Enqueue(generatedDeal);
            SaveToCloud();
        }
        isGenerating = false;
        yield return null;
    }

    private void CheckBufferHealth()
    {
        foreach (var kvp in cacheRequirements)
        {
            GameType gType = kvp.Key;
            if (!generatorRegistry.ContainsKey(gType)) continue;

            foreach (var config in kvp.Value)
            {
                var key = new CacheKey(gType, config.Diff, config.Param);
                if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();

                int currentCount = dealCache[key].Count;
                int queuedCount = generationQueue.Count(k => k.Equals(key));
                int needed = config.TargetBufferSize - (currentCount + queuedCount);

                for (int i = 0; i < needed; i++) generationQueue.Enqueue(key);
            }
        }
    }

    private bool IsDealValid(Deal deal)
    {
        if (deal == null) return false;
        int count = 0;
        if (deal.stock != null) count += deal.stock.Count;
        if (deal.tableau != null) foreach (var pile in deal.tableau) if (pile != null) count += pile.Count;
        return count > 10;
    }

    private Deal UnpackLegacyDeal(SerializedDeal sDeal)
    {
        Deal d = new Deal();
        d.tableau = new List<List<CardInstance>>();
        d.foundations = new List<List<CardModel>>();
        d.waste = new List<CardInstance>();
        d.stock = new Stack<CardInstance>();

        for (int i = 0; i < 8; i++) d.foundations.Add(new List<CardModel>());

        if (sDeal.tableau != null)
        {
            foreach (var sPile in sDeal.tableau)
            {
                var pile = new List<CardInstance>();
                foreach (var c in sPile.cards) pile.Add(c.ToRuntime());
                d.tableau.Add(pile);
            }
        }
        if (sDeal.stock != null)
        {
            // Внимание: сохраняем порядок стека
            for (int i = sDeal.stock.Count - 1; i >= 0; i--)
                d.stock.Push(sDeal.stock[i].ToRuntime());
        }
        return d;
    }

    private void InitializeCacheConfigs()
    {
        int baseBuffer = defaultBufferSize;
        ConfigureStandardGame(GameType.Klondike, new int[] { 1, 3 }, baseBuffer);
        
        List<CacheConfig> spiderConfigs = new List<CacheConfig>();
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Easy, Param = 1, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 1, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Easy, Param = 2, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 2, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Hard, Param = 2, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 4, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Hard, Param = 4, TargetBufferSize = baseBuffer });
        cacheRequirements[GameType.Spider] = spiderConfigs;

        ConfigureStandardGame(GameType.FreeCell, new int[] { 0 }, baseBuffer);
        ConfigureProgressiveGame(GameType.Pyramid, baseBuffer);
        ConfigureProgressiveGame(GameType.TriPeaks, baseBuffer);
        ConfigureStandardGame(GameType.Yukon, new int[] { 0, 1 }, baseBuffer);
        ConfigureStandardGame(GameType.MonteCarlo, new int[] { 0, 1 }, baseBuffer);
        ConfigureStandardGame(GameType.Sultan, new int[] { 0 }, baseBuffer);
        ConfigureStandardGame(GameType.Octagon, new int[] { 0 }, baseBuffer);
        ConfigureStandardGame(GameType.Montana, new int[] { 0 }, baseBuffer);
    }

    private void ConfigureStandardGame(GameType type, int[] paramsList, int buffer)
    {
        List<CacheConfig> configs = new List<CacheConfig>();
        foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
            foreach (int p in paramsList) configs.Add(new CacheConfig { Diff = d, Param = p, TargetBufferSize = buffer });
        cacheRequirements[type] = configs;
    }
    public void MarkCurrentDealAsPlayed()
    {
        dealWasPlayed = true;
        currentActiveDeal = null; // Забываем про него, он сыгран
        CheckBufferHealth();
    }
    private void ConfigureProgressiveGame(GameType type, int baseBuffer)
    {
        List<CacheConfig> configs = new List<CacheConfig>();
        foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
        {
            configs.Add(new CacheConfig { Diff = d, Param = 1, TargetBufferSize = baseBuffer });
            configs.Add(new CacheConfig { Diff = d, Param = 2, TargetBufferSize = baseBuffer * 2 });
            configs.Add(new CacheConfig { Diff = d, Param = 3, TargetBufferSize = baseBuffer * 3 });
        }
        cacheRequirements[type] = configs;
    }

    private void RegisterGenerators()
    {
        foreach (var gen in GetComponentsInChildren<BaseGenerator>())
            if (!generatorRegistry.ContainsKey(gen.GameType)) generatorRegistry.Add(gen.GameType, gen);
    }

    [Serializable] private class CompressedWrapper { public List<CompressedEntry> entries; }
    [Serializable] private class CompressedEntry { public int gType; public int diff; public int param; public List<string> deals; }
    // --- КЛАССЫ ДЛЯ ЧТЕНИЯ STARTER PACK (JSON ФАЙЛОВ) ---
    [Serializable]
    private class SaveDataWrapper
    {
        public List<QueueSaveData> queues;
    }

    [Serializable]
    private class QueueSaveData
    {
        public GameType type;
        public Difficulty diff;
        public int param;
        public List<SerializedDeal> deals;
    }
}