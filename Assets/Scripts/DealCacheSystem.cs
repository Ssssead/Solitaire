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

    [Header("Starter Pack (Аварийный резерв)")]
    public DealDatabase database;

    private Queue<CacheKey> generationQueue = new Queue<CacheKey>();
    private bool isGenerating = false;

    private HashSet<GameType> dirtyTypes = new HashSet<GameType>();

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

#if UNITY_EDITOR
            LoadAllLocalCacheFiles();

            if (IsCacheEmpty())
            {
                Debug.Log("[DealCache] Local cache is empty. Loading Starter Pack...");
                LoadStarterPack();
            }

            IsReady = true;
            CheckBufferHealth();
#else
            GetData();
#endif
        }
        else Destroy(gameObject);
    }

#if !UNITY_EDITOR
    private void OnEnable() => YG2.onGetSDKData += GetData;
    private void OnDisable() => YG2.onGetSDKData -= GetData;
#endif

    private void OnApplicationQuit()
    {
        ReturnActiveDealToQueue();
        SaveData();
    }

    private void SaveData()
    {
#if UNITY_EDITOR
        SaveDirtyFilesSync();
#else
        SaveToCloud();
#endif
    }

    // --- API МЕТОДЫ И АВАРИЙНЫЙ РЕЗЕРВ ---
    public Deal GetDeal(GameType type, Difficulty diff, int param)
    {
        ReturnActiveDealToQueue();
        var key = new CacheKey(type, diff, param);

        // 1. ИЩЕМ В КЭШЕ
        if (dealCache.ContainsKey(key))
        {
            var queue = dealCache[key];
            while (queue.Count > 0)
            {
                Deal candidate = queue.Peek();
                if (IsDealValid(candidate))
                {
                    currentActiveDeal = queue.Dequeue();
                    currentActiveKey = key;
                    dealWasPlayed = false;

#if UNITY_EDITOR
                    dirtyTypes.Add(type);
#endif
                    SaveData();
                    CheckBufferHealth();

                    Debug.Log($"[DealCache] Served Deal for {type} {diff} (P:{param}). Remaining: {queue.Count}");
                    return currentActiveDeal;
                }
                else
                {
                    queue.Dequeue();
#if UNITY_EDITOR
                    dirtyTypes.Add(type);
#endif
                }
            }
        }

        // 2. АВАРИЙНЫЙ РЕЗЕРВ (Если кэш пуст или сломан)
        Debug.LogWarning($"[DealCache] Cache Empty for {type} {diff}! Pinging generator and using Emergency Reserve.");
        CheckBufferHealth(); // Заставляем генератор работать

        Deal emergencyDeal = GetEmergencyDeal(type, diff, param);
        if (emergencyDeal != null)
        {
            currentActiveDeal = emergencyDeal;
            currentActiveKey = key;
            dealWasPlayed = false;
            Debug.Log("[DealCache] Served EMERGENCY deal from Starter Pack!");
            return currentActiveDeal;
        }

        return null; // Совсем беда, пусть DeckManager запускает генератор в реальном времени
    }

    private Deal GetEmergencyDeal(GameType type, Difficulty diff, int param)
    {
        if (database == null || database.dealSets == null) return null;
        foreach (var set in database.dealSets)
        {
            if (set.gameType == type && set.difficulty == diff && set.param == param)
            {
                if (set.deals != null && set.deals.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, set.deals.Count);
                    return UnpackDeal(set.deals[randomIndex]);
                }
            }
        }
        return null;
    }

    public void DiscardActiveDeal()
    {
        currentActiveDeal = null;
        dealWasPlayed = false;
    }

    public void ReturnActiveDealToQueue()
    {
        if (currentActiveDeal != null && !dealWasPlayed)
        {
            if (!dealCache.ContainsKey(currentActiveKey)) dealCache[currentActiveKey] = new Queue<Deal>();
            dealCache[currentActiveKey].Enqueue(currentActiveDeal);

#if UNITY_EDITOR
            dirtyTypes.Add(currentActiveKey.GameType);
#endif
            SaveData();
        }
        currentActiveDeal = null;
    }

    public void MarkCurrentDealAsPlayed()
    {
        dealWasPlayed = true;
        currentActiveDeal = null;
        CheckBufferHealth();
    }

    // =========================================================
    //               ЛОГИКА БИЛДА (YANDEX CLOUD)
    // =========================================================
#if !UNITY_EDITOR
    public void GetData()
    {
        if (IsReady && dealCache.Count > 0) return;

        string cloudJson = YG2.saves.dealCacheJson;
        if (string.IsNullOrEmpty(cloudJson)) LoadStarterPack();
        else DeserializeAndUnpack(cloudJson);

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
                CompressedEntry entry = new CompressedEntry { gType = (int)kvp.Key.GameType, diff = (int)kvp.Key.Difficulty, param = kvp.Key.Param, deals = new List<string>() };
                foreach (var deal in kvp.Value) if (IsDealValid(deal)) entry.deals.Add(DealSerializer.Serialize(deal));
                wrapper.entries.Add(entry);
            }

            YG2.saves.dealCacheJson = JsonUtility.ToJson(wrapper);
            YG2.SaveProgress();
        }
        catch (Exception e) { Debug.LogError($"[DealCache] Save Failed: {e.Message}"); }
    }

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
#endif

    // =========================================================
    //               ЛОГИКА РЕДАКТОРА (LOCAL FILES)
    // =========================================================
#if UNITY_EDITOR
    private string GetFilePathForGame(GameType type) => Path.Combine(Application.persistentDataPath, $"Deals_{type}.json");

    private void LoadAllLocalCacheFiles()
    {
        dealCache.Clear();
        foreach (GameType type in System.Enum.GetValues(typeof(GameType)))
        {
            string path = GetFilePathForGame(type);
            if (File.Exists(path)) LoadFileIntoCache(path);
        }
    }

    private void LoadFileIntoCache(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            SaveDataWrapper wrapper = JsonUtility.FromJson<SaveDataWrapper>(json);
            if (wrapper == null || wrapper.queues == null) return;

            foreach (var qData in wrapper.queues)
            {
                var key = new CacheKey(qData.type, qData.diff, qData.param);
                if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();

                foreach (var sDeal in qData.deals)
                {
                    Deal d = UnpackDeal(sDeal);
                    if (IsDealValid(d)) dealCache[key].Enqueue(d);
                }
            }
        }
        catch (Exception e) { Debug.LogWarning($"[DealCache] Failed to load {Path.GetFileName(path)}: {e.Message}"); }
    }

    private void SaveDirtyFilesSync()
    {
        if (dirtyTypes.Count == 0) return;

        foreach (var type in dirtyTypes)
        {
            // ПРЕДОХРАНИТЕЛЬ: Не перезаписываем файл, если память пуста
            int totalDealsCount = dealCache.Where(kvp => kvp.Key.GameType == type).Sum(kvp => kvp.Value.Count);
            string path = GetFilePathForGame(type);

            if (totalDealsCount == 0 && File.Exists(path))
            {
                Debug.Log($"[DealCache] Save for {type} skipped: memory is empty. Keeping old file as backup.");
                continue;
            }

            var entries = dealCache.Where(kvp => kvp.Key.GameType == type).ToList();
            SaveDataWrapper wrapper = new SaveDataWrapper();
            wrapper.queues = new List<QueueSaveData>();

            foreach (var kvp in entries)
            {
                QueueSaveData qData = new QueueSaveData { type = kvp.Key.GameType, diff = kvp.Key.Difficulty, param = kvp.Key.Param, deals = new List<SerializedDeal>() };
                foreach (var deal in kvp.Value) if (IsDealValid(deal)) qData.deals.Add(PackDeal(deal));
                wrapper.queues.Add(qData);
            }

            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(wrapper, true));
                Debug.Log($"[DealCache] Saved local file: {Path.GetFileName(path)}");
            }
            catch (Exception e) { Debug.LogError($"[DealCache] Failed to save {type}: {e.Message}"); }
        }
        dirtyTypes.Clear();
    }

    private SerializedDeal PackDeal(Deal d)
    {
        SerializedDeal sd = new SerializedDeal();
        if (d.tableau != null)
        {
            foreach (var pile in d.tableau)
            {
                var sPile = new SerializedPile();
                if (pile != null) foreach (var card in pile) sPile.cards.Add(new SerializedCard(card));
                sd.tableau.Add(sPile);
            }
        }
        if (d.stock != null) foreach (var card in d.stock) sd.stock.Add(new SerializedCard(card));
        return sd;
    }

    [ContextMenu("Open Local Cache Folder")]
    private void OpenLocalCacheFolder()
    {
        UnityEditor.EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
#endif

    // =========================================================
    //               ОБЩИЕ МЕТОДЫ И ГЕНЕРАТОРЫ
    // =========================================================
    private void LoadStarterPack()
    {
        bool dataFound = false;

        if (database != null && database.dealSets != null)
        {
            foreach (var set in database.dealSets)
            {
                var key = new CacheKey(set.gameType, set.difficulty, set.param);
                if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();
                foreach (var sDeal in set.deals)
                {
                    Deal d = UnpackDeal(sDeal);
                    if (IsDealValid(d)) dealCache[key].Enqueue(d);
                }
            }
            if (database.dealSets.Count > 0) dataFound = true;
        }

        TextAsset[] files = Resources.LoadAll<TextAsset>("InitialDeals");
        if (files.Length > 0)
        {
            foreach (var file in files)
            {
                try
                {
                    SaveDataWrapper wrapper = JsonUtility.FromJson<SaveDataWrapper>(file.text);
                    if (wrapper != null && wrapper.queues != null)
                    {
                        foreach (var qData in wrapper.queues)
                        {
                            var key = new CacheKey(qData.type, qData.diff, qData.param);
                            if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();
                            foreach (var sDeal in qData.deals)
                            {
                                Deal d = UnpackDeal(sDeal);
                                if (IsDealValid(d)) dealCache[key].Enqueue(d);
                            }
                        }
                        dataFound = true;
                    }
                }
                catch { }
            }
        }

        if (dataFound) SaveData();
    }

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

#if UNITY_EDITOR
            dirtyTypes.Add(key.GameType);
#endif
            SaveData();
        }
        else
        {
            // ЗАЩИТА ОТ ОСТАНОВКИ: генератор не справился, пробуем еще раз чуть позже
            yield return new WaitForSeconds(0.5f);
            generationQueue.Enqueue(key);
        }

        isGenerating = false;
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
                // Считаем сколько задач УЖЕ в очереди, чтобы не спамить
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

    private bool IsCacheEmpty()
    {
        foreach (var kvp in dealCache) if (kvp.Value.Count > 0) return false;
        return true;
    }

    // ИСПРАВЛЕННАЯ РАСПАКОВКА: Гарантируем инициализацию всех списков
    private Deal UnpackDeal(SerializedDeal sDeal)
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
                if (sPile.cards != null)
                {
                    foreach (var c in sPile.cards) pile.Add(c.ToRuntime());
                }
                d.tableau.Add(pile);
            }
        }

        if (sDeal.stock != null)
        {
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
        ConfigureStandardGame(GameType.Montana, new int[] { 0, 1 }, baseBuffer);
    }

    private void ConfigureStandardGame(GameType type, int[] paramsList, int buffer)
    {
        List<CacheConfig> configs = new List<CacheConfig>();
        foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
            foreach (int p in paramsList) configs.Add(new CacheConfig { Diff = d, Param = p, TargetBufferSize = buffer });
        cacheRequirements[type] = configs;
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
    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues; }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }
}