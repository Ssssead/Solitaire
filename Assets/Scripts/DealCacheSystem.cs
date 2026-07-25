using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;


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

    [Header("Optimization")]
    [Tooltip("Задержка между сохранениями в секундах (защита от спама диска)")]
    [SerializeField] private float saveCooldown = 5f;
    private float lastSaveTime = 0f;
    private bool isDirty = false; // Флаг: есть ли несохраненные изменения

    [Header("Starter Pack (Аварийный резерв)")]
    public DealDatabase database;

    private Queue<CacheKey> generationQueue = new Queue<CacheKey>();
    private bool isGenerating = false;

    private HashSet<GameType> dirtyTypes = new HashSet<GameType>();
    private HashSet<GameType> quarantinedTypes = new HashSet<GameType>();

    public bool IsReady { get; private set; } = false;

    private Deal currentActiveDeal = null;
    private CacheKey currentActiveKey;
    private bool dealWasPlayed = false;
    private bool cloudDataReceived = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeCacheConfigs();
            RegisterGenerators();

            LoadAllLocalCacheFiles();

            if (IsCacheEmpty())
            {
                Debug.Log("[DealCache] Local cache is empty. Loading Starter Pack...");
                LoadStarterPack();
            }

            IsReady = true;
            CheckBufferHealth();

            
        }
        else Destroy(gameObject);
    }


    private void OnApplicationQuit()
    {
        ReturnActiveDealToQueue();
        // Принудительно сохраняем при выходе, если есть изменения
        if (isDirty) SaveData();
    }

    // [NEW] Умный метод отметки изменений (вместо мгновенного сохранения)
    private void MarkAsDirty(GameType type)
    {
        dirtyTypes.Add(type);
        isDirty = true;
    }

    private void SaveData()
    {
        if (!isDirty && dirtyTypes.Count == 0) return;

        isDirty = false;
        lastSaveTime = Time.realtimeSinceStartup;

        SaveDirtyFilesSync();

        if (GlobalSaveManager.Instance != null)
            GlobalSaveManager.Instance.MarkAsDirty();
        else
            isDirty = true; // не теряем флаг, попробуем сохранить позже из Update()
    }
    public string GetCloudData()
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

            string json = JsonUtility.ToJson(wrapper);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DealCache] Save Preparation Failed: {e.Message}");
            return "";
        }
    }
    // --- API МЕТОДЫ И АВАРИЙНЫЙ РЕЗЕРВ ---
    public Deal GetDeal(GameType type, Difficulty diff, int param)
    {
        ReturnActiveDealToQueue();
        var key = new CacheKey(type, diff, param);

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

                    MarkAsDirty(type); // Просто помечаем, сохранит Update()
                    CheckBufferHealth();

                    Debug.Log($"[DealCache] Served Deal for {type} {diff} (P:{param}). Remaining: {queue.Count}");
                    return currentActiveDeal;
                }
                else
                {
                    queue.Dequeue();
                    MarkAsDirty(type);
                }
            }
        }

        Debug.LogWarning($"[DealCache] Cache Empty for {type} {diff}! Pinging generator and using Emergency Reserve.");
        CheckBufferHealth();

        Deal emergencyDeal = GetEmergencyDeal(type, diff, param);
        if (emergencyDeal != null)
        {
            currentActiveDeal = emergencyDeal;
            currentActiveKey = key;
            dealWasPlayed = false;
            Debug.Log("[DealCache] Served EMERGENCY deal from Starter Pack!");
            return currentActiveDeal;
        }

        return null;
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

            MarkAsDirty(currentActiveKey.GameType);
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

    public void LoadFromCloud(string cloudJson)
    {
        cloudDataReceived = true;
        if (!string.IsNullOrEmpty(cloudJson))
        {
            DeserializeAndUnpack(cloudJson);
        }
        IsReady = true;
        CheckBufferHealth();
    }

   

    private void DeserializeAndUnpack(string data)
    {
        dealCache.Clear();
        try
        {
            string json = data;

            // ---> ИЗМЕНЕНИЕ 2: Читаем из Base64 (если это он) <---
            if (!string.IsNullOrEmpty(data) && !data.StartsWith("{"))
            {
                byte[] bytes = Convert.FromBase64String(data);
                json = System.Text.Encoding.UTF8.GetString(bytes);
            }

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


    // =========================================================
    //               ЛОГИКА РЕДАКТОРА (LOCAL FILES)
    // =========================================================

    private string GetFilePathForGame(GameType type) => Path.Combine(Application.persistentDataPath, $"Deals_{type}.json");

    private void LoadAllLocalCacheFiles()
    {
        dealCache.Clear();
        foreach (GameType type in System.Enum.GetValues(typeof(GameType)))
        {
            string path = GetFilePathForGame(type);
            if (File.Exists(path)) LoadFileIntoCache(path, type);
        }
    }

    private void LoadFileIntoCache(string path, GameType type)
    {
        try
        {
            string json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"[DealCache] File {type} is empty (0 bytes)! Quarantining to prevent overwrite.");
                quarantinedTypes.Add(type);
                return;
            }

            CompressedWrapper wrapper = JsonUtility.FromJson<CompressedWrapper>(json);
            if (wrapper == null || wrapper.entries == null)
            {
                Debug.LogWarning($"[DealCache] JSON parse failed for {type}. Quarantining.");
                quarantinedTypes.Add(type);
                return;
            }

            foreach (var qData in wrapper.entries)
            {
                var key = new CacheKey((GameType)qData.gType, (Difficulty)qData.diff, qData.param);
                if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();

                foreach (var sDeal in qData.deals)
                {
                    Deal d = DealSerializer.Deserialize(sDeal);
                    if (IsDealValid(d)) dealCache[key].Enqueue(d);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DealCache] Failed to load {type}: {e.Message}. Quarantining.");
            quarantinedTypes.Add(type);
        }
    }

    private void SaveDirtyFilesSync()
    {
        if (dirtyTypes.Count == 0) return;

        foreach (var type in dirtyTypes)
        {
            if (quarantinedTypes.Contains(type)) continue;

            int totalDealsCount = dealCache.Where(kvp => kvp.Key.GameType == type).Sum(kvp => kvp.Value.Count);
            string path = GetFilePathForGame(type);

            if (totalDealsCount == 0 && File.Exists(path)) continue;

            var entries = dealCache.Where(kvp => kvp.Key.GameType == type).ToList();

            // Используем CompressedWrapper вместо SaveDataWrapper
            CompressedWrapper wrapper = new CompressedWrapper();
            wrapper.entries = new List<CompressedEntry>();

            foreach (var kvp in entries)
            {
                CompressedEntry qData = new CompressedEntry
                {
                    gType = (int)kvp.Key.GameType,
                    diff = (int)kvp.Key.Difficulty,
                    param = kvp.Key.Param,
                    deals = new List<string>()
                };
                foreach (var deal in kvp.Value)
                {
                    // Сжимаем в Base64
                    if (IsDealValid(deal)) qData.deals.Add(DealSerializer.Serialize(deal));
                }
                wrapper.entries.Add(qData);
            }

            try
            {
                // Убираем true (pretty print), чтобы сэкономить еще больше места
                string json = JsonUtility.ToJson(wrapper);
                string tempPath = path + ".tmp";

                File.WriteAllText(tempPath, json);

                if (File.Exists(path)) File.Replace(tempPath, path, null);
                else File.Move(tempPath, path);

                Debug.Log($"[DealCache] SAFE Saved COMPRESSED file: {Path.GetFileName(path)} ({totalDealsCount} deals)");
            }
            catch (Exception e) { Debug.LogError($"[DealCache] Failed to safe-save {type}: {e.Message}"); }
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

    // ---> ИЗМЕНЕНИЕ 3: Исправлена ошибка билда CS0234 <---
#if UNITY_EDITOR
    [ContextMenu("Open Local Cache Folder")]
    private void OpenLocalCacheFolder() { UnityEditor.EditorUtility.RevealInFinder(Application.persistentDataPath); }
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
                    CompressedWrapper wrapper = JsonUtility.FromJson<CompressedWrapper>(file.text);
                    if (wrapper != null && wrapper.entries != null)
                    {
                        foreach (var qData in wrapper.entries)
                        {
                            var key = new CacheKey((GameType)qData.gType, (Difficulty)qData.diff, qData.param);
                            if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();

                            foreach (var sDeal in qData.deals)
                            {
                                Deal d = DealSerializer.Deserialize(sDeal);
                                if (IsDealValid(d)) dealCache[key].Enqueue(d);
                            }
                        }
                        dataFound = true;
                    }
                }
                catch { }
            }
        }

        if (dataFound)
        {
            isDirty = true;
            SaveData();
        }
    }

    private void Update()
    {

        // [NEW] ЗАЩИТА ОТ ДОМЕННОЙ ПЕРЕЗАГРУЗКИ (Hot-Reload)
        // Если Instance стал null, значит Unity только что перекомпилировала скрипты
        // и стерла всю оперативную память (dealCache).
        if (Instance == null)
        {
            Debug.LogWarning("[DealCache] Domain Reload detected! Restoring memory from disk to prevent data loss...");
            Instance = this;

            // Сбрасываем зависшие статусы (Unity убила старую корутину при перезагрузке)
            isGenerating = false;
            isDirty = false;
            generationQueue.Clear();
            dirtyTypes.Clear();

            // Заново читаем наши большие файлы с диска в оперативную память
            LoadAllLocalCacheFiles();
            CheckBufferHealth();

            return; // Пропускаем этот кадр, даем системе прийти в себя
        }


        // --- ВАШ СТАРЫЙ КОД НИЖЕ ---

        // 1. Фоமைப்பு генерация
        if (!isGenerating && generationQueue.Count > 0)
        {
            var key = generationQueue.Dequeue();
            StartCoroutine(GenerateInBackground(key));
        }

        // 2. Автосохранение (Пакетная запись)
        if (isDirty && Time.realtimeSinceStartup - lastSaveTime > saveCooldown)
        {
            SaveData();
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

            // --- БАГФИКС: ЖЕСТКИЙ ЛИМИТ КЭША (Hard Cap) ---
            int targetLimit = GetTargetBufferLimit(key);

            // Если в кэше оказалось больше раскладов, чем нужно (например, 11 вместо 10),
            // мы просто удаляем самый старый (тот, что лежит на дне очереди).
            while (dealCache[key].Count > targetLimit)
            {
                dealCache[key].Dequeue();
                Debug.Log($"[DealCache] Dropped oldest deal to respect buffer limit ({targetLimit}) for {key.GameType} {key.Difficulty}");
            }

            MarkAsDirty(key.GameType); // Вместо SaveData() помечаем как "грязный"
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
            generationQueue.Enqueue(key); // Если генерация провалилась, пробуем еще раз
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
                int queuedCount = generationQueue.Count(k => k.Equals(key));

                // Проверяем, сколько нам не хватает до идеала
                int needed = config.TargetBufferSize - (currentCount + queuedCount);

                // Добавляем задания в очередь только если есть дефицит
                for (int i = 0; i < needed; i++)
                {
                    generationQueue.Enqueue(key);
                }

                // --- БАГФИКС (На всякий случай): Если кэш раздут прямо при запуске, чистим его ---
                while (dealCache[key].Count > config.TargetBufferSize)
                {
                    dealCache[key].Dequeue();
                    MarkAsDirty(gType);
                }
            }
        }
    }
    private int GetTargetBufferLimit(CacheKey key)
    {
        if (cacheRequirements.ContainsKey(key.GameType))
        {
            var config = cacheRequirements[key.GameType].FirstOrDefault(c => c.Diff == key.Difficulty && c.Param == key.Param);
            // Возвращаем лимит (обычно 10). Если по какой-то причине не нашли, возвращаем defaultBufferSize
            return config.TargetBufferSize > 0 ? config.TargetBufferSize : defaultBufferSize;
        }
        return defaultBufferSize;
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

    // ВАЖНО: Я вернул абсолютно безопасную версию распаковки, чтобы ничего не крашилось
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
        List<CacheConfig> octagonConfigs = new List<CacheConfig>();
        octagonConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 0, TargetBufferSize = baseBuffer });
        cacheRequirements[GameType.Octagon] = octagonConfigs;
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
#if UNITY_EDITOR
    [ContextMenu("Compress Resources Files")]
    private void CompressOldResourcesFiles()
    {
        TextAsset[] files = Resources.LoadAll<TextAsset>("InitialDeals");
        foreach (var file in files)
        {
            // Читаем по СТАРОМУ формату
            SaveDataWrapper oldWrapper = JsonUtility.FromJson<SaveDataWrapper>(file.text);
            if (oldWrapper != null && oldWrapper.queues != null)
            {
                // Перекладываем в НОВЫЙ формат
                CompressedWrapper newWrapper = new CompressedWrapper();
                newWrapper.entries = new List<CompressedEntry>();

                foreach (var qData in oldWrapper.queues)
                {
                    CompressedEntry newEntry = new CompressedEntry
                    {
                        gType = (int)qData.type,
                        diff = (int)qData.diff,
                        param = qData.param,
                        deals = new List<string>()
                    };

                    foreach (var sDeal in qData.deals)
                    {
                        // Распаковываем старым методом и сразу запаковываем новым
                        Deal d = UnpackDeal(sDeal);
                        newEntry.deals.Add(DealSerializer.Serialize(d));
                    }
                    newWrapper.entries.Add(newEntry);
                }

                // Сохраняем рядом новый сжатый файл
                string newJson = JsonUtility.ToJson(newWrapper);
                string savePath = Application.dataPath + "/Resources/InitialDeals/" + file.name + "_Compressed.json";

                // Убедитесь, что папка существует или измените путь под вашу структуру
                System.IO.File.WriteAllText(savePath, newJson);
                Debug.Log($"Конвертирован файл {file.name}. Размер стал: {newJson.Length} байт.");
            }
        }
        UnityEditor.AssetDatabase.Refresh();
    }
#endif
    [Serializable] private class CompressedWrapper { public List<CompressedEntry> entries; }
    [Serializable] private class CompressedEntry { public int gType; public int diff; public int param; public List<string> deals; }
    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues; }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }
}