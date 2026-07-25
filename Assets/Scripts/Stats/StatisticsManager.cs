using UnityEngine;
using System.IO;
using YG;
using System.Collections.Generic;

public class StatisticsManager : MonoBehaviour
{
    public static StatisticsManager Instance;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public GameStatistics stats;
    private string filePath;

    // Таймер
    private float gameStartTime;
    private bool isTimerRunning = false;
    private bool hasTimerStarted = false;
    public bool IsNewScoreRecord { get; private set; }
    public bool IsNewTimeRecord { get; private set; }
    public bool IsNewMovesRecord { get; private set; }
    public float LastGameTime { get; private set; }
    public int LastXPGained { get; private set; }

    // Текущий контекст игры
    private string currentGameKey = "";
    private int currentMoves = 0;

    public bool IsUserPremium = false;
    public bool IsAdsDisabled = false;

    // События для UI (чтобы показать красивые анимации Level Up)
    public event System.Action<int> OnXPGained; // int = кол-во полученного опыта
    public event System.Action<string, int> OnLevelUp; // string = где апнули (Global/Klondike), int = новый уровень
    // --- СИСТЕМА БЕЗОПАСНОЙ ОТПРАВКИ В ЛИДЕРБОРДЫ (Обход лимита Яндекса) ---
    private Queue<System.Tuple<string, int>> leaderboardQueue = new Queue<System.Tuple<string, int>>();
    private Coroutine leaderboardCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {

        

        // Подписываемся на успешные покупки ВСЕГДА, так как этот менеджер живет на всех сценах
        YG2.onPurchaseSuccess += HandlePurchaseSuccess;
    }

    private void OnDisable()
    {

        

        YG2.onPurchaseSuccess -= HandlePurchaseSuccess;
    }

    private void Start()
    {
        // При старте игры автоматически просим Яндекс "консумировать" (обработать) все зависшие покупки
        // Если они есть, Яндекс вызовет событие onPurchaseSuccess, и мы выдадим награду
        if (YG2.isSDKEnabled)
        {
            YG2.ConsumePurchases(true);
        }
    }

    private void HandlePurchaseSuccess(string purchasedId)
    {
        Debug.Log($"[StatisticsManager] Выдача награды за покупку: {purchasedId}");

        // ---> ДОБАВЛЕНО: || purchasedId == "Premium2" <---
        if (purchasedId == "Premium" || purchasedId == "Premium2")
        {
            GrantPremium();
            DisableAds();
        }
        else if (purchasedId == "NoADS")
        {
            DisableAds();
        }
    }

    private void Initialize()
    {
        filePath = Path.Combine(Application.persistentDataPath, "solitaire_stats.json");


        // Загрузка в редакторе (через локальный файл JSON)
        LoadStatsLocal();

       
    }

    public void OnGameStarted(string gameName, Difficulty difficulty, string variant)
    {
        // 1. СБРОС ФЛАГОВ
        isTimerRunning = false;
        hasTimerStarted = false;
        LastGameTime = 0f;
        currentMoves = 0;

        LastXPGained = 0; // Сброс прошлого опыта
        IsNewScoreRecord = false; // Сброс рекордов
        IsNewTimeRecord = false;
        IsNewMovesRecord = false;

        // В режиме обучения обнуляем счетчики, но НЕ пишем +1 к запускам игры
        if (GameSettings.IsTutorialMode) return;

        // 2. ФОРМИРОВАНИЕ КЛЮЧЕЙ
        currentGameKey = $"{gameName}_{difficulty}_{variant}";
        string gameGlobalKey = $"{gameName}_Global";
        string appGlobalKey = "Global";

        // 3. ОБНОВЛЕНИЕ СЧЕТЧИКОВ ВО ВСЕХ КАТЕГОРИЯХ
        stats.GetData(currentGameKey).gamesStarted++;
        stats.GetData(gameGlobalKey).gamesStarted++;
        stats.GetData(appGlobalKey).gamesStarted++;
        SaveStats();
        Log($"Game Started: {currentGameKey}");
    }

    public void StartTimerIfNotStarted()
    {
        if (!hasTimerStarted)
        {
            hasTimerStarted = true;
            isTimerRunning = true;
            gameStartTime = Time.time;
            Log("Timer Started");
        }
    }

    public void RegisterMove()
    {
        currentMoves++;
        StartTimerIfNotStarted();
    }

    public void OnGameWon(int finalScore)
    {
        if (!hasTimerStarted) return;

        isTimerRunning = false;
        hasTimerStarted = false;
        float duration = Time.time - gameStartTime;
        LastGameTime = duration;

        // Если это обучение, просто выходим. Никакого опыта и сохранений.
        if (GameSettings.IsTutorialMode)
        {
            LastXPGained = 0; // Опыт на экране победы будет 0
            currentMoves = 0;
            return;
        }

        // Разбираем ключи
        string[] keyParts = currentGameKey.Split('_');
        string gameName = keyParts[0];
        string difficultyStr = keyParts[1];
        string variantStr = keyParts[2];

        // 1. Парсим Enum сложности и Типа игры
        Difficulty diffEnum = (Difficulty)System.Enum.Parse(typeof(Difficulty), difficultyStr);

        GameType gType;
        try
        {
            gType = (GameType)System.Enum.Parse(typeof(GameType), gameName);
        }
        catch
        {
            gType = GameType.Klondike; // Фолбэк на случай ошибки
        }

        string gameGlobalKey = $"{gameName}_Global"; 
        string appGlobalKey = "Global";              

        // Получаем текущие данные ДО их обновления, чтобы сравнить с новым результатом
        StatData modeData = stats.GetData(currentGameKey);

        // Проверяем, были ли победы ранее (чтобы не писать "Новый рекорд" при самой первой игре в режиме)
        bool hasPreviousWins = modeData.gamesWon > 0;

        // Устанавливаем флаги для GameUIController
        IsNewScoreRecord = hasPreviousWins && (finalScore > modeData.bestScore);
        IsNewTimeRecord = hasPreviousWins && (modeData.bestTime == 0 || duration < modeData.bestTime);
        IsNewMovesRecord = hasPreviousWins && (modeData.fewestMoves == 0 || currentMoves < modeData.fewestMoves);

        // 2. Получаем текущие данные игры, чтобы узнать УРОВЕНЬ
        StatData gameData = stats.GetData(gameGlobalKey);
        int currentLevel = (gameData != null) ? gameData.currentLevel : 1;

        // 3. РАСЧЕТ ОПЫТА 
        int xpGained = LevelingUtils.CalculateXP(gType, currentLevel, diffEnum, variantStr, IsUserPremium);

        // ПРИМЕНЕНИЕ БИЛЕТОВ НА Х2 ОПЫТ (И СПИСАНИЕ ЗАРЯДА)
        if (QuestManager.Instance != null && System.Enum.TryParse(gameName, out QuestCategory cat))
        {
            if (QuestManager.Instance.ConsumeXpBuffForGame(cat))
            {
                xpGained *= 2;
                Debug.Log($"[XP System] Сработал билет X2! Опыт удвоен до {xpGained}");
            }
        }

        LastXPGained = xpGained;

        Debug.Log($"[XP System] Gained {xpGained} XP. (Diff: {diffEnum}, Var: {variantStr})");

        // 4. СОХРАНЕНИЕ СТАТИСТИКИ И ОПЫТА
        stats.UpdateData(currentGameKey, true, duration, currentMoves, finalScore, difficultyStr, gameName, variantStr);
        stats.UpdateData(gameGlobalKey, true, duration, currentMoves, finalScore, difficultyStr, gameName, variantStr);
        stats.UpdateData(appGlobalKey, true, duration, currentMoves, finalScore, difficultyStr, gameName, variantStr);

        // Начисление локального опыта
        StatData localStats = stats.GetData(gameGlobalKey);
        if (localStats.currentLevel == 1 && localStats.xpForNextLevel == 0) localStats.xpForNextLevel = 500;

        bool localLevelUp = localStats.AddExperience(xpGained, isGlobal: false);
        if (localLevelUp)
        {
            Debug.Log($"[Level Up] {gameName} Level is now {localStats.currentLevel}!");
            OnLevelUp?.Invoke(gameName, localStats.currentLevel);
        }

        // Начисление глобального опыта
        StatData globalStats = stats.GetData(appGlobalKey);
        if (globalStats.currentLevel == 1 && globalStats.xpForNextLevel == 0) globalStats.xpForNextLevel = 2000;

        bool globalLevelUp = globalStats.AddExperience(xpGained, isGlobal: true);
        if (globalLevelUp)
        {
            Debug.Log($"[Level Up] GLOBAL Rank is now {globalStats.currentLevel}!");
            OnLevelUp?.Invoke("Account", globalStats.currentLevel);
        }

        OnXPGained?.Invoke(xpGained);
        SaveStats();
        var historyList = stats.GetData(currentGameKey)?.history;
        if (historyList != null && historyList.Count > 0)
        {
            var lastEntry = historyList[historyList.Count - 1];
            GameQuestTracker.Instance?.ReportMatchEnd(lastEntry, 0, 0);
        }


       


        currentMoves = 0;
    }

    public void OnGameAbandoned()
    {
        if (!hasTimerStarted) return;

        float duration = Time.time - gameStartTime;
        LastGameTime = duration;

        isTimerRunning = false;
        hasTimerStarted = false;

        if (GameSettings.IsTutorialMode)
        {
            currentMoves = 0;
            return;
        }

        string[] keyParts = currentGameKey.Split('_');
        string gameName = keyParts[0];
        string difficultyStr = keyParts[1];
        string variantStr = keyParts[2];

        string gameGlobalKey = $"{gameName}_Global";
        string appGlobalKey = "Global";

        stats.UpdateData(currentGameKey, false, duration, currentMoves, 0, difficultyStr, gameName, variantStr);
        stats.UpdateData(gameGlobalKey, false, duration, currentMoves, 0, difficultyStr, gameName, variantStr);
        stats.UpdateData(appGlobalKey, false, duration, currentMoves, 0, difficultyStr, gameName, variantStr);

        SaveStats();

        var historyList = stats.GetData(currentGameKey)?.history;
        if (historyList != null && historyList.Count > 0)
        {
            var lastEntry = historyList[historyList.Count - 1];
            GameQuestTracker.Instance?.ReportMatchEnd(lastEntry, 0, 0);
        }

        currentMoves = 0;
    }

    // --- SAVE / LOAD ---

    private void SaveStats()
    {
        string json = JsonUtility.ToJson(stats, true);
        File.WriteAllText(filePath, json);
        Log("Stats saved locally.");

        GlobalSaveManager.Instance?.MarkAsDirty();
    }

    private void LoadStatsLocal()
    {
        // Загружаем локальные флаги покупок
        IsUserPremium = PlayerPrefs.GetInt("IsPremiumSaved", 0) == 1;
        IsAdsDisabled = PlayerPrefs.GetInt("IsAdsDisabledSaved", 0) == 1;

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                stats = JsonUtility.FromJson<GameStatistics>(json);

                if (stats.MigrateOldKeys())
                {
                    Log("Migrated old keys. Saving changes...");
                    SaveStats(); 
                }

                stats.BuildLookup();
                Log("Local stats loaded successfully.");
            }
            catch
            {
                stats = new GameStatistics();
                Log("Error loading local stats, creating new.");
            }
        }
        else
        {
            stats = new GameStatistics();
            Log("No local stats file found, creating new.");
        }
    }



    public void LoadFromCloud(string cloudJson)
    {
        // IsUserPremium / IsAdsDisabled уже выставлены GlobalSaveManager-ом
        // ДО этого вызова - здесь их трогать не нужно.

        if (string.IsNullOrEmpty(cloudJson))
        {
            // Облако пустое (новый игрок ИЛИ преждевременный вызов до факт. загрузки) -
            // оставляем то, что уже подняла LoadStatsLocal().
            Log("Cloud stats empty, keeping local stats.");
            return;
        }

        try
        {
            StatsSerializer.Deserialize(cloudJson, out stats);

            if (stats.MigrateOldKeys())
            {
                Log("Migrated old keys after cloud load.");
                SaveStats();
            }

            stats.BuildLookup();
            Log("Cloud stats loaded successfully.");
           
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StatsManager] Error loading cloud stats: {e.Message}");
            // НЕ затираем stats - оставляем локальные данные как fallback
        }
    }
    public string GetCloudData()
    {
        return StatsSerializer.Serialize(stats);
    }


    public StatData GetStats(string gameName, Difficulty difficulty, string variant)
    {
        string key = $"{gameName}_{difficulty}_{variant}";
        return stats.GetData(key);
    }

    public StatData GetGlobalStats()
    {
        return stats.GetData("Global");
    }

    public StatData GetGameGlobalStats(string gameName)
    {
        return stats.GetData($"{gameName}_Global");
    }

    private void Log(string msg)
    {
        if (showDebugLogs) Debug.Log($"[StatsManager] {msg}");
    }

    public int GetCurrentMoves()
    {
        return currentMoves;
    }

    public void RegisterQuestCompleted(int currentQuestStreak, int currentDayStreak, int xpReward = 0)
    {
        StatData appGlobal = stats.GetData("Global");

        appGlobal.questsCompleted++;
        appGlobal.questStreak = currentQuestStreak;       
        appGlobal.questDayStreak = currentDayStreak;      

        if (xpReward > 0)
        {
            if (appGlobal.currentLevel == 1 && appGlobal.xpForNextLevel == 0)
                appGlobal.xpForNextLevel = 2000;

            if (IsUserPremium)
            {
                xpReward = Mathf.RoundToInt(xpReward * LevelingUtils.MULTIPLIER_PREMIUM);
            }

            bool leveledUp = appGlobal.AddExperience(xpReward, isGlobal: true);

            if (leveledUp)
            {
                OnLevelUp?.Invoke("Global", appGlobal.currentLevel);
            }
            OnXPGained?.Invoke(xpReward);
        }
        SaveStats();

        Log($"Квест выполнен! Всего: {appGlobal.questsCompleted}. Получено XP: {xpReward}");
    }

    public float GetLastGameDurationFromHistory()
    {
        var data = stats.GetData(currentGameKey);

        if (data != null && data.history.Count > 0)
        {
            return data.history[data.history.Count - 1].time;
        }

        return 0f;
    }
    
    public void GrantPremium()
    {
        IsUserPremium = true;
        PlayerPrefs.SetInt("IsPremiumSaved", 1);
        SaveStats(); // -> GlobalSaveManager.MarkAsDirty(), он сам подхватит IsUserPremium в YG2.saves.isPremium

        // ---> ПРЯЧЕМ STICKY БАННЕР СРАЗУ ПОСЛЕ ПОКУПКИ <---
        if (AdManager.Instance != null) AdManager.Instance.UpdateStickyAd();
    }

    public void DisableAds()
    {
        IsAdsDisabled = true;
        PlayerPrefs.SetInt("IsAdsDisabledSaved", 1);
        SaveStats();

        // ---> ПРЯЧЕМ STICKY БАННЕР СРАЗУ ПОСЛЕ ПОКУПКИ <---
        if (AdManager.Instance != null) AdManager.Instance.UpdateStickyAd();
    }

    public List<StatEntry> GetAllEntriesRaw()
    {
        if (stats == null) return new List<StatEntry>();
        return stats.entries;
    }

    public void ResetAllStatistics()
    {
        stats = new GameStatistics();
        currentMoves = 0;
        LastGameTime = 0f;
        LastXPGained = 0;
        SaveStats();
        Debug.Log("[StatisticsManager] Вся статистика полностью сброшена!");
    }

}