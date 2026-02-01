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
        public int Param; // DrawCount, SuitCount, RoundCount, Variant
        public CacheKey(GameType type, Difficulty diff, int param) { GameType = type; Difficulty = diff; Param = param; }
        public override bool Equals(object obj) => obj is CacheKey k && GameType == k.GameType && Difficulty == k.Difficulty && Param == k.Param;
        public override int GetHashCode() => (GameType, Difficulty, Param).GetHashCode();
    }

    // Расширенная конфигурация: теперь знаем, сколько именно раскладов хранить
    private struct CacheConfig
    {
        public Difficulty Diff;
        public int Param;
        public int TargetBufferSize; // Сколько раскладов держать в кэше (3, 6, 9...)
    }

    // Хранит настройки для каждого типа игры
    private Dictionary<GameType, List<CacheConfig>> cacheRequirements = new Dictionary<GameType, List<CacheConfig>>();

    // Само хранилище
    private Dictionary<CacheKey, Queue<Deal>> dealCache = new Dictionary<CacheKey, Queue<Deal>>();
    private Dictionary<GameType, BaseGenerator> generatorRegistry = new Dictionary<GameType, BaseGenerator>();

    [Header("Settings")]
    [Tooltip("Стандартный размер буфера, если не указано иное")]
    [SerializeField] private int defaultBufferSize = 10;

    [Header("Persistent Database (Starter Pack)")]
    public DealDatabase database;

    private Queue<CacheKey> generationQueue = new Queue<CacheKey>();
    private bool isGenerating = false;

    // Список игр, файлы которых нужно обновить/создать
    private HashSet<GameType> dirtyTypes = new HashSet<GameType>();

    // Active Deal State
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

            // 1. Пытаемся перенести старый общий файл в новые (если он есть)
            TryMigrateLegacyCache();

            // 2. Загружаем все существующие файлы режимов
            LoadAllCacheFiles();

            // 3. Если кэш пуст (первый запуск), грузим базу и помечаем файлы на создание
            if (IsCacheEmpty())
            {
                LoadDatabaseToRuntimeCache();
            }

            // ГАРАНТИРУЕМ СОЗДАНИЕ ФАЙЛОВ:
            // Пробегаем по всем настроенным играм. Если файла нет - добавляем в список на сохранение.
            foreach (var gameType in cacheRequirements.Keys)
            {
                if (!File.Exists(GetFilePathForGame(gameType)))
                {
                    dirtyTypes.Add(gameType);
                }
            }

            // Сохраняем (это создаст пустые JSON файлы для будущих режимов)
            SaveDirtyFilesSync();

            // Проверяем, нужно ли пополнить запасы (если генераторы есть)
            CheckBufferHealth();
        }
        else Destroy(gameObject);
    }

    private void OnApplicationQuit() { ReturnActiveDealToQueue(); SaveDirtyFilesSync(); }
    private void OnApplicationPause(bool pause) { if (pause) SaveDirtyFilesSync(); }

    /// <summary>
    /// Здесь прописана вся математика количества раскладов для каждого режима.
    /// </summary>
    private void InitializeCacheConfigs()
    {
        // Базовый размер буфера (количество игр в запасе)
        int baseBuffer = 10;

        // 1. KLONDIKE
        // 3 diff * 2 params * 10 buffer
        ConfigureStandardGame(GameType.Klondike, new int[] { 1, 3 }, baseBuffer);

        // 2. SPIDER
        List<CacheConfig> spiderConfigs = new List<CacheConfig>();
        // 1 Suit (Easy, Medium)
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Easy, Param = 1, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 1, TargetBufferSize = baseBuffer });
        // 2 Suits (Easy, Medium, Hard)
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Easy, Param = 2, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 2, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Hard, Param = 2, TargetBufferSize = baseBuffer });
        // 4 Suits (Medium, Hard)
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 4, TargetBufferSize = baseBuffer });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Hard, Param = 4, TargetBufferSize = baseBuffer });
        cacheRequirements[GameType.Spider] = spiderConfigs;

        // 3. FREECELL
        ConfigureStandardGame(GameType.FreeCell, new int[] { 0 }, baseBuffer);

        // 4. PYRAMID (Прогрессивный буфер: 10 игр)
        // 1 Round -> 10 deals
        // 2 Rounds -> 20 deals
        // 3 Rounds -> 30 deals
        ConfigureProgressiveGame(GameType.Pyramid, baseBuffer);

        // 5. TRIPEAKS
        ConfigureProgressiveGame(GameType.TriPeaks, baseBuffer);

        // 6. YUKON
        ConfigureStandardGame(GameType.Yukon, new int[] { 0, 1 }, baseBuffer);

        // 7. MONTE CARLO
        ConfigureStandardGame(GameType.MonteCarlo, new int[] { 0, 1 }, baseBuffer);

        // 8. ОСТАЛЬНЫЕ
        ConfigureStandardGame(GameType.Sultan, new int[] { 0 }, baseBuffer);
        ConfigureStandardGame(GameType.Octagon, new int[] { 0 }, baseBuffer);
        ConfigureStandardGame(GameType.Montana, new int[] { 0 }, baseBuffer);
    }

    // Помощник для стандартных игр (фиксированный буфер)
    private void ConfigureStandardGame(GameType type, int[] paramsList, int buffer)
    {
        List<CacheConfig> configs = new List<CacheConfig>();
        foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
        {
            foreach (int p in paramsList)
            {
                configs.Add(new CacheConfig { Diff = d, Param = p, TargetBufferSize = buffer });
            }
        }
        cacheRequirements[type] = configs;
    }

    // Помощник для Pyramid/TriPeaks (буфер растет с числом раундов)
    private void ConfigureProgressiveGame(GameType type, int baseBuffer)
    {
        List<CacheConfig> configs = new List<CacheConfig>();
        foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
        {
            // 1 Round -> храним 10 (на 10 игр)
            configs.Add(new CacheConfig { Diff = d, Param = 1, TargetBufferSize = baseBuffer * 1 });

            // 2 Rounds -> храним 20 (на 10 игр по 2 расклада)
            configs.Add(new CacheConfig { Diff = d, Param = 2, TargetBufferSize = baseBuffer * 2 });

            // 3 Rounds -> храним 30 (на 10 игр по 3 расклада)
            configs.Add(new CacheConfig { Diff = d, Param = 3, TargetBufferSize = baseBuffer * 3 });
        }
        cacheRequirements[type] = configs;
    }

    private void RegisterGenerators()
    {
        var generators = GetComponentsInChildren<BaseGenerator>();
        foreach (var gen in generators)
        {
            if (!generatorRegistry.ContainsKey(gen.GameType))
                generatorRegistry.Add(gen.GameType, gen);
        }
    }
    public void DiscardActiveDeal()
    {
        // Используем правильное имя переменной
        currentActiveDeal = null;
        dealWasPlayed = false;
    }
    private void Update()
    {
        if (!isGenerating && generationQueue.Count > 0)
        {
            var key = generationQueue.Dequeue();
            StartCoroutine(GenerateInBackground(key));
        }
    }

    // --- ФАЙЛОВАЯ СИСТЕМА ---
    private string GetFilePathForGame(GameType type) => Path.Combine(Application.persistentDataPath, $"Deals_{type}.json");
    private string GetLegacyFilePath() => Path.Combine(Application.persistentDataPath, "deals_cache_v2.json");

    // --- ОСНОВНОЙ API ---

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

                    dirtyTypes.Add(type); // Помечаем, что этот файл изменился

                    Debug.Log($"[DealCache] Served Deal for {type} {diff} (P:{param}). Remaining: {queue.Count}");
                    CheckBufferHealth();
                    return currentActiveDeal;
                }
                else
                {
                    Debug.LogWarning($"[DealCache] Found corrupted deal for {type}. Discarding.");
                    queue.Dequeue();
                    dirtyTypes.Add(type);
                }
            }
        }

        Debug.LogWarning($"[DealCache] Buffer empty for {type} {diff} (P:{param})! Generator needed.");
        return null;
    }

    public void ReturnActiveDealToQueue()
    {
        if (currentActiveDeal != null && !dealWasPlayed)
        {
            if (IsDealValid(currentActiveDeal))
            {
                if (!dealCache.ContainsKey(currentActiveKey)) dealCache[currentActiveKey] = new Queue<Deal>();
                dealCache[currentActiveKey].Enqueue(currentActiveDeal);
                dirtyTypes.Add(currentActiveKey.GameType);
            }
        }
        currentActiveDeal = null;
        dealWasPlayed = false;
    }

    public void MarkCurrentDealAsPlayed()
    {
        if (currentActiveDeal == null || dealWasPlayed) return;
        dealWasPlayed = true;
        CheckBufferHealth();
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

    // --- ПРОВЕРКА БУФЕРА (С УЧЕТОМ ЦЕЛЕВЫХ РАЗМЕРОВ) ---
    private void CheckBufferHealth()
    {
        foreach (var kvp in cacheRequirements)
        {
            GameType gType = kvp.Key;
            List<CacheConfig> requiredConfigs = kvp.Value;
            bool hasGenerator = generatorRegistry.ContainsKey(gType);

            foreach (var config in requiredConfigs)
            {
                var key = new CacheKey(gType, config.Diff, config.Param);

                // Создаем очередь, если её нет (чтобы она попала в файл сохранения даже пустой)
                if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();

                // Если генератора нет, мы не можем пополнить, но структура в памяти и файле будет создана
                if (!hasGenerator) continue;

                int count = dealCache[key].Count;
                int inQ = CountInGenerationQueue(key);

                // Используем TargetBufferSize из конфига (3, 6 или 9)
                int needed = config.TargetBufferSize - (count + inQ);

                for (int i = 0; i < needed; i++)
                {
                    generationQueue.Enqueue(key);
                }
            }
        }
    }

    private int CountInGenerationQueue(CacheKey key)
    {
        int count = 0;
        foreach (var k in generationQueue) if (k.Equals(key)) count++;
        return count;
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

            dirtyTypes.Add(key.GameType); // Сохраняем только файл этого режима
            SaveDirtyFilesSync();
        }
        isGenerating = false;
    }

    // --- СОХРАНЕНИЕ И ЗАГРУЗКА ---

    [System.Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [System.Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }

    /// <summary>
    /// Сохраняет только те файлы, которые изменились или должны быть созданы.
    /// </summary>
    private void SaveDirtyFilesSync()
    {
        if (dirtyTypes.Count == 0) return;

        // Группируем кэш по типу игры
        var groupedCache = dealCache.GroupBy(kvp => kvp.Key.GameType);

        // Также учитываем типы, которых может не быть в dealCache (если совсем пусто), но они есть в dirtyTypes
        foreach (var type in dirtyTypes)
        {
            // Находим данные для этого типа
            var entries = dealCache.Where(kvp => kvp.Key.GameType == type).ToList();

            SaveDataWrapper wrapper = new SaveDataWrapper();

            // Даже если entries пуст, мы создадим пустой wrapper, чтобы файл физически существовал
            foreach (var kvp in entries)
            {
                QueueSaveData qData = new QueueSaveData
                {
                    type = kvp.Key.GameType,
                    diff = kvp.Key.Difficulty,
                    param = kvp.Key.Param,
                    deals = new List<SerializedDeal>()
                };

                // Сохраняем расклады, если есть
                foreach (var deal in kvp.Value)
                {
                    if (IsDealValid(deal)) qData.deals.Add(PackDeal(deal));
                }

                // Добавляем конфигурацию очереди в файл, даже если она пуста
                wrapper.queues.Add(qData);
            }

            // Записываем файл
            string path = GetFilePathForGame(type);
            try
            {
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(path, json);
                Debug.Log($"[DealCache] Saved file: {Path.GetFileName(path)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DealCache] Failed to save {type}: {e.Message}");
            }
        }

        dirtyTypes.Clear();
    }

    private void LoadAllCacheFiles()
    {
        dealCache.Clear();

        // Проходим по всем известным типам игр из Enum
        foreach (GameType type in System.Enum.GetValues(typeof(GameType)))
        {
            string path = GetFilePathForGame(type);
            if (File.Exists(path))
            {
                LoadFileIntoCache(path);
            }
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
        catch (Exception e)
        {
            Debug.LogWarning($"[DealCache] Failed to load {Path.GetFileName(path)}: {e.Message}");
        }
    }

    private void TryMigrateLegacyCache()
    {
        string legacyPath = GetLegacyFilePath();
        if (!File.Exists(legacyPath)) return;

        Debug.Log("[DealCache] Migrating legacy cache file...");
        LoadFileIntoCache(legacyPath);

        // Помечаем всё что загрузили как грязное, чтобы оно пересохранилось в новые файлы
        foreach (var kvp in dealCache)
        {
            dirtyTypes.Add(kvp.Key.GameType);
        }

        SaveDirtyFilesSync();

        try { File.Delete(legacyPath); }
        catch { }
    }

    private void LoadDatabaseToRuntimeCache()
    {
        if (database == null) return;
        foreach (var set in database.dealSets)
        {
            var key = new CacheKey(set.gameType, set.difficulty, set.param);
            if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();
            foreach (var sDeal in set.deals)
            {
                Deal d = UnpackDeal(sDeal);
                if (IsDealValid(d)) dealCache[key].Enqueue(d);
            }
            dirtyTypes.Add(set.gameType); // Если загрузили из базы, сразу сохраним в файлы
        }
    }

    // --- CONVERTERS ---
    private Deal UnpackDeal(SerializedDeal sDeal)
    {
        Deal d = new Deal();
        for (int i = 0; i < 10; i++) d.tableau.Add(new List<CardInstance>());
        if (sDeal.tableau != null)
        {
            while (d.tableau.Count < sDeal.tableau.Count) d.tableau.Add(new List<CardInstance>());
            for (int i = 0; i < sDeal.tableau.Count; i++)
                foreach (var sCard in sDeal.tableau[i].cards) d.tableau[i].Add(sCard.ToRuntime());
        }
        if (sDeal.stock != null)
        {
            for (int i = sDeal.stock.Count - 1; i >= 0; i--) d.stock.Push(sDeal.stock[i].ToRuntime());
        }
        return d;
    }

    private SerializedDeal PackDeal(Deal d)
    {
        SerializedDeal sd = new SerializedDeal();
        foreach (var pile in d.tableau)
        {
            var sPile = new SerializedPile();
            if (pile != null) foreach (var card in pile) sPile.cards.Add(new SerializedCard(card));
            sd.tableau.Add(sPile);
        }
        if (d.stock != null)
            foreach (var card in d.stock) sd.stock.Add(new SerializedCard(card));
        return sd;
    }
}