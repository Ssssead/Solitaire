using UnityEngine;
using System.IO;
using YG;

public class StatisticsManager : MonoBehaviour
{
    public static StatisticsManager Instance;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private GameStatistics stats;
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

    // События для UI (чтобы показать красивые анимации Level Up)
    public event System.Action<int> OnXPGained; // int = кол-во полученного опыта
    public event System.Action<string, int> OnLevelUp; // string = где апнули (Global/Klondike), int = новый уровень

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

#if !UNITY_EDITOR
    private void OnEnable() => YG2.onGetSDKData += LoadStatsCloud;
    private void OnDisable() => YG2.onGetSDKData -= LoadStatsCloud;
#endif

    private void Initialize()
    {
        filePath = Path.Combine(Application.persistentDataPath, "solitaire_stats.json");

#if UNITY_EDITOR
        // Загрузка в редакторе (через локальный файл JSON)
        LoadStatsLocal();
#else
        // Загрузка в билде (через Яндекс Игры)
        if (YG2.isSDKEnabled) 
        {
            LoadStatsCloud();
        }
#endif
    }

    public void OnGameStarted(string gameName, Difficulty difficulty, string variant)
    {
        // 1. СБРОС ФЛАГОВ
        isTimerRunning = false;
        hasTimerStarted = false;
        LastGameTime = 0f;
        currentMoves = 0;

        LastXPGained = 0; // [FIX] Сброс прошлого опыта
        IsNewScoreRecord = false; // [FIX] Сброс рекордов
        IsNewTimeRecord = false;
        IsNewMovesRecord = false;

        // [FIX] В режиме обучения обнуляем счетчики, но НЕ пишем +1 к запускам игры
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

        // [FIX] Если это обучение, просто выходим. Никакого опыта и сохранений.
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

        string gameGlobalKey = $"{gameName}_Global"; // Klondike_Global
        string appGlobalKey = "Global";              // Global

        // --- ДОБАВЛЕНО: ПРОВЕРКА РЕКОРДОВ ---
        // Получаем текущие данные ДО их обновления, чтобы сравнить с новым результатом
        StatData modeData = stats.GetData(currentGameKey);

        // Проверяем, были ли победы ранее (чтобы не писать "Новый рекорд" при самой первой игре в режиме)
        bool hasPreviousWins = modeData.gamesWon > 0;

        // Устанавливаем флаги для GameUIController
        IsNewScoreRecord = hasPreviousWins && (finalScore > modeData.bestScore);
        IsNewTimeRecord = hasPreviousWins && (modeData.bestTime == 0 || duration < modeData.bestTime);
        IsNewMovesRecord = hasPreviousWins && (modeData.fewestMoves == 0 || currentMoves < modeData.fewestMoves);
        // ------------------------------------

        // 2. Получаем текущие данные игры, чтобы узнать УРОВЕНЬ
        StatData gameData = stats.GetData(gameGlobalKey);
        int currentLevel = (gameData != null) ? gameData.currentLevel : 1;

        // 3. РАСЧЕТ ОПЫТА 
        int xpGained = LevelingUtils.CalculateXP(gType, currentLevel, diffEnum, variantStr, IsUserPremium);

        LastXPGained = xpGained;

        Debug.Log($"[XP System] Gained {xpGained} XP. (Diff: {diffEnum}, Var: {variantStr})");

        // 4. СОХРАНЕНИЕ СТАТИСТИКИ И ОПЫТА
        stats.UpdateData(currentGameKey, true, duration, currentMoves, finalScore, difficultyStr, gameName, variantStr);
        stats.UpdateData(gameGlobalKey, true, duration, currentMoves, finalScore, difficultyStr, gameName, variantStr);
        stats.UpdateData(appGlobalKey, true, duration, currentMoves, finalScore, difficultyStr, gameName, variantStr);

        // Б. Начисление опыта
        StatData localStats = stats.GetData(gameGlobalKey);
        if (localStats.currentLevel == 1 && localStats.xpForNextLevel == 0) localStats.xpForNextLevel = 500;

        bool localLevelUp = localStats.AddExperience(xpGained, isGlobal: false);
        if (localLevelUp)
        {
            Debug.Log($"[Level Up] {gameName} Level is now {localStats.currentLevel}!");
            OnLevelUp?.Invoke(gameName, localStats.currentLevel);
        }

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
        currentMoves = 0;
    }

    public void OnGameAbandoned()
    {
        // Если игра даже не началась (не было ходов), не записываем статистику
        if (!hasTimerStarted) return;

        // 1. Рассчитываем реальное время игры
        float duration = Time.time - gameStartTime;
        LastGameTime = duration;

        isTimerRunning = false;
        hasTimerStarted = false;

        // [FIX] В режиме обучения не пишем поражения
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

        // 2. ПЕРЕДАЕМ duration И currentMoves ВМЕСТО 0
        stats.UpdateData(currentGameKey, false, duration, currentMoves, 0, difficultyStr, gameName, variantStr);
        stats.UpdateData(gameGlobalKey, false, duration, currentMoves, 0, difficultyStr, gameName, variantStr);
        stats.UpdateData(appGlobalKey, false, duration, currentMoves, 0, difficultyStr, gameName, variantStr);

        SaveStats();
        currentMoves = 0;
    }

    // --- SAVE / LOAD ---

    private void SaveStats()
    {
#if UNITY_EDITOR
        // Сохранение в редакторе (стандартный JSON)
        string json = JsonUtility.ToJson(stats, true);
        File.WriteAllText(filePath, json);
        Log("Stats saved locally.");
#else
        // Сохранение в билде (сжимаем в Base64 и пушим в плагин)
        try
        {
            YG2.saves.statsDataJson = StatsSerializer.Serialize(stats);
            YG2.SaveProgress();
            Log("Stats saved to cloud.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[StatsManager] Cloud Save Failed: {e.Message}");
        }
#endif
    }
#if UNITY_EDITOR
    private void LoadStatsLocal()
    {
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                stats = JsonUtility.FromJson<GameStatistics>(json);
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
#endif

#if !UNITY_EDITOR
    private void LoadStatsCloud()
    {
        string cloudJson = YG2.saves.statsDataJson;

        if (string.IsNullOrEmpty(cloudJson))
        {
            stats = new GameStatistics();
            Log("Cloud stats empty, creating new.");
        }
        else
        {
            try
            {
                StatsSerializer.Deserialize(cloudJson, out stats);
                stats.BuildLookup();
                Log("Cloud stats loaded successfully.");
            }
            catch (System.Exception e)
            {
                stats = new GameStatistics();
                Debug.LogError($"[StatsManager] Error loading cloud stats, creating new. {e.Message}");
            }
        }
    }
#endif
 
    // --- API для UI ---

    // Метод для получения статистики конкретного режима (Easy/Medium/Hard)
    public StatData GetStats(string gameName, Difficulty difficulty, string variant)
    {
        string key = $"{gameName}_{difficulty}_{variant}";
        return stats.GetData(key);
    }

    // 2. Для ОБЩЕЙ статистики всего приложения (оставьте как есть, пригодится для главного меню)
    public StatData GetGlobalStats()
    {
        return stats.GetData("Global");
    }

    // 3. --- НОВЫЙ МЕТОД ---
    // Для глобальной статистики КОНКРЕТНОГО РЕЖИМА (например, "Klondike_Global")
    public StatData GetGameGlobalStats(string gameName)
    {
        return stats.GetData($"{gameName}_Global");
    }

    private void Log(string msg)
    {
        if (showDebugLogs) Debug.Log($"[StatsManager] {msg}");
    }

    /// <summary>
    /// Возвращает текущее количество ходов.
    /// </summary>
    public int GetCurrentMoves()
    {
        return currentMoves;
    }

    public float GetLastGameDurationFromHistory()
    {
        // 1. Берем данные текущего режима
        var data = stats.GetData(currentGameKey);

        // 2. Если история есть - берем последнюю запись
        if (data != null && data.history.Count > 0)
        {
            // Последняя добавленная игра всегда в конце списка (или в начале, зависит от реализации, 
            // но в вашем коде history.Add добавляет в конец, а удаляет RemoveAt(0))
            return data.history[data.history.Count - 1].time;
        }

        return 0f;
    }
}