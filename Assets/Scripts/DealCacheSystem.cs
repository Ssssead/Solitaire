using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DealCacheSystem : MonoBehaviour
{
    public static DealCacheSystem Instance { get; private set; }

    [System.Serializable]
    public struct CacheKey
    {
        public GameType GameType;
        public Difficulty Difficulty;
        public int Param; // Klondike: DrawCount; Spider: SuitCount; Others: 0
        public CacheKey(GameType type, Difficulty diff, int param) { GameType = type; Difficulty = diff; Param = param; }
        public override bool Equals(object obj) => obj is CacheKey k && GameType == k.GameType && Difficulty == k.Difficulty && Param == k.Param;
        public override int GetHashCode() => (GameType, Difficulty, Param).GetHashCode();
    }

    // Хранит настройки: какие комбинации нужно кэшировать для каждой игры
    private Dictionary<GameType, List<CacheConfig>> cacheRequirements = new Dictionary<GameType, List<CacheConfig>>();
    private struct CacheConfig { public Difficulty Diff; public int Param; }

    private Dictionary<CacheKey, Queue<Deal>> dealCache = new Dictionary<CacheKey, Queue<Deal>>();
    private Dictionary<GameType, BaseGenerator> generatorRegistry = new Dictionary<GameType, BaseGenerator>();

    [Header("Settings")]
    [SerializeField] private int targetBufferSize = 3;

    [Header("Persistent Database (Starter Pack)")]
    public DealDatabase database;

    private Queue<CacheKey> generationQueue = new Queue<CacheKey>();
    private bool isGenerating = false;
    private string saveFilePath;
    private bool isDirty = false;

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
            saveFilePath = Path.Combine(Application.persistentDataPath, "deals_cache_v2.json");

            InitializeCacheConfigs(); // <--- Настраиваем требования к кэшу
            RegisterGenerators();

            if (!LoadState())
            {
                LoadDatabaseToRuntimeCache();
                isDirty = true;
                SaveStateSync();
            }
            CheckBufferHealth();
        }
        else Destroy(gameObject);
    }

    private void OnApplicationQuit() { ReturnActiveDealToQueue(); SaveStateSync(); }
    private void OnApplicationPause(bool pause) { if (pause) SaveStateSync(); }

    /// <summary>
    /// Здесь мы прописываем, какие режимы нужны для каждой игры.
    /// </summary>
    private void InitializeCacheConfigs()
    {
        // 1. KLONDIKE (Draw 1, Draw 3 для всех сложностей)
        List<CacheConfig> klondikeConfigs = new List<CacheConfig>();
        foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
        {
            klondikeConfigs.Add(new CacheConfig { Diff = d, Param = 1 });
            klondikeConfigs.Add(new CacheConfig { Diff = d, Param = 3 });
        }
        cacheRequirements[GameType.Klondike] = klondikeConfigs;

        // 2. SPIDER (Специфичные 7 режимов)
        List<CacheConfig> spiderConfigs = new List<CacheConfig>();
        // 1 Suit
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Easy, Param = 1 });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 1 });
        // 2 Suits
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Easy, Param = 2 });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 2 });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Hard, Param = 2 });
        // 4 Suits
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Medium, Param = 4 });
        spiderConfigs.Add(new CacheConfig { Diff = Difficulty.Hard, Param = 4 });

        cacheRequirements[GameType.Spider] = spiderConfigs;

        List<CacheConfig> freeCellConfigs = new List<CacheConfig>();
        foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
        {
            freeCellConfigs.Add(new CacheConfig { Diff = d, Param = 0 });
        }
        cacheRequirements[GameType.FreeCell] = freeCellConfigs;

        // 3. ОСТАЛЬНЫЕ (Пока стандартные Easy/Medium/Hard c Param 0)
        GameType[] otherGames = {
            GameType.Pyramid, GameType.TriPeaks,
            GameType.Sultan, GameType.Octagon, GameType.MulticoloredStar,
            GameType.Yukon, GameType.MonteCarlo
        };

        foreach (var game in otherGames)
        {
            List<CacheConfig> configs = new List<CacheConfig>();
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                configs.Add(new CacheConfig { Diff = d, Param = 0 });
            }
            cacheRequirements[game] = configs;
        }
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

    private void Update()
    {
        if (!isGenerating && generationQueue.Count > 0)
        {
            var key = generationQueue.Dequeue();
            StartCoroutine(GenerateInBackground(key));
        }
    }

    public Deal GetDeal(GameType type, Difficulty diff, int param)
    {
        ReturnActiveDealToQueue();

        var key = new CacheKey(type, diff, param);

        if (dealCache.ContainsKey(key))
        {
            while (dealCache[key].Count > 0)
            {
                Deal candidate = dealCache[key].Peek();
                if (IsDealValid(candidate))
                {
                    currentActiveDeal = dealCache[key].Dequeue();
                    currentActiveKey = key;
                    dealWasPlayed = false;
                    isDirty = true;
                    Debug.Log($"[DealCache] Served Deal for {type} {diff} (P:{param}). Buffer: {dealCache[key].Count}");
                    CheckBufferHealth();
                    return currentActiveDeal;
                }
                else
                {
                    Debug.LogWarning($"[DealCache] Corrupted deal found for {type}. Removing.");
                    dealCache[key].Dequeue();
                    isDirty = true;
                }
            }
        }

        Debug.LogWarning($"[DealCache] Buffer empty for {type} {diff} (P:{param})! Generating fallback.");
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
                isDirty = true;
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
        isDirty = true;
    }

    private bool IsDealValid(Deal deal)
    {
        if (deal == null) return false;
        if (deal.tableau == null && deal.stock == null) return false;

        int count = 0;
        if (deal.stock != null) count += deal.stock.Count;

        if (deal.tableau != null)
        {
            foreach (var pile in deal.tableau)
            {
                if (pile != null) count += pile.Count;
            }
        }
        return count > 10;
    }

    // --- ПРОВЕРКА БУФЕРА НА ОСНОВЕ CONFIGS ---
    private void CheckBufferHealth()
    {
        // Проходимся по всем играм, которые мы настроили в InitializeCacheConfigs
        foreach (var kvp in cacheRequirements)
        {
            GameType gType = kvp.Key;
            List<CacheConfig> requiredConfigs = kvp.Value;

            // Проверяем, есть ли генератор для этой игры
            bool hasGenerator = generatorRegistry.ContainsKey(gType);

            foreach (var config in requiredConfigs)
            {
                var key = new CacheKey(gType, config.Diff, config.Param);

                // Создаем очередь в памяти, даже если она пустая (занимаем место)
                if (!dealCache.ContainsKey(key)) dealCache[key] = new Queue<Deal>();

                // Если генератора нет, мы не можем пополнить кэш, но место зарезервировано
                if (!hasGenerator) continue;

                int count = dealCache[key].Count;
                int inQ = CountInGenerationQueue(key);
                int needed = targetBufferSize - (count + inQ);

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
        // Доп. проверка: если генератор удалили/отключили во время работы очереди
        if (!generatorRegistry.ContainsKey(key.GameType)) yield break;

        isGenerating = true;
        BaseGenerator generator = generatorRegistry[key.GameType];

        Deal generatedDeal = null;
        bool done = false;

        // Запускаем генерацию с защитой от ошибок
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
            isDirty = true;
            SaveStateSync();
        }
        else
        {
            Debug.LogError($"[DealCache] Generator failed for {key.GameType} {key.Difficulty}.");
        }
        isGenerating = false;
    }

    // --- JSON SAVE/LOAD ---

    [System.Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [System.Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }

    private void SaveStateSync()
    {
        if (!isDirty) return;

        SaveDataWrapper wrapper = new SaveDataWrapper();
        foreach (var kvp in dealCache)
        {
            // Сохраняем, только если есть реальные данные. 
            // Пустые очереди в файл писать не обязательно, они создадутся при старте в CheckBufferHealth.
            if (kvp.Value.Count == 0) continue;

            QueueSaveData qData = new QueueSaveData { type = kvp.Key.GameType, diff = kvp.Key.Difficulty, param = kvp.Key.Param, deals = new List<SerializedDeal>() };

            foreach (var deal in kvp.Value)
            {
                if (IsDealValid(deal)) qData.deals.Add(PackDeal(deal));
            }

            if (qData.deals.Count > 0) wrapper.queues.Add(qData);
        }

        string json = JsonUtility.ToJson(wrapper, true);

        try
        {
            File.WriteAllText(saveFilePath, json);
            isDirty = false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DealCache] Failed to save cache: {e.Message}");
        }
    }

    private bool LoadState()
    {
        if (!File.Exists(saveFilePath)) return false;
        try
        {
            string json = File.ReadAllText(saveFilePath);
            SaveDataWrapper wrapper = JsonUtility.FromJson<SaveDataWrapper>(json);
            if (wrapper == null || wrapper.queues == null) return false;

            dealCache.Clear();
            foreach (var qData in wrapper.queues)
            {
                var key = new CacheKey(qData.type, qData.diff, qData.param);
                var queue = new Queue<Deal>();

                foreach (var sDeal in qData.deals)
                {
                    Deal d = UnpackDeal(sDeal);
                    if (IsDealValid(d)) queue.Enqueue(d);
                    else isDirty = true;
                }
                dealCache[key] = queue;
            }
            return true;
        }
        catch { return false; }
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
        }
    }

    // --- CONVERTERS ---
    private Deal UnpackDeal(SerializedDeal sDeal)
    {
        Deal d = new Deal();
        for (int i = 0; i < 7; i++) d.tableau.Add(new List<CardInstance>()); // Default init
        // Если в сохраненке больше 7 столбцов (Spider имеет 10), расширяем
        if (sDeal.tableau.Count > d.tableau.Count)
        {
            while (d.tableau.Count < sDeal.tableau.Count) d.tableau.Add(new List<CardInstance>());
        }

        if (sDeal.tableau != null)
        {
            for (int i = 0; i < sDeal.tableau.Count; i++)
            {
                if (i >= d.tableau.Count) d.tableau.Add(new List<CardInstance>());
                foreach (var sCard in sDeal.tableau[i].cards) d.tableau[i].Add(sCard.ToRuntime());
            }
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