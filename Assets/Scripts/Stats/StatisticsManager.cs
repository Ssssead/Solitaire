using UnityEngine;
using System.IO;

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
            Destroy(gameObject); // Уничтожаем дубликат, если вернулись в меню
        }
    }

    private void Initialize()
    {
        filePath = Path.Combine(Application.persistentDataPath, "solitaire_stats.json");
        LoadStats();
    }

    public void OnGameStarted(string gameName, Difficulty difficulty, string variant)
    {
        // 1. СБРОС ФЛАГОВ (чтобы не было дубликатов)
        isTimerRunning = false;
        hasTimerStarted = false;
        LastGameTime = 0f; // Сбрасываем при новой игре
        currentMoves = 0;

        // 2. ФОРМИРОВАНИЕ КЛЮЧЕЙ
        currentGameKey = $"{gameName}_{difficulty}_{variant}"; // Пример: Klondike_Easy_Draw1
        string gameGlobalKey = $"{gameName}_Global";           // Пример: Klondike_Global
        string appGlobalKey = "Global";                        // Общая статистика приложения

        // 3. ОБНОВЛЕНИЕ СЧЕТЧИКОВ (gamesStarted) ВО ВСЕХ КАТЕГОРИЯХ

        // А. Конкретный режим
        stats.GetData(currentGameKey).gamesStarted++;

        // Б. Глобальная статистика ЭТОЙ игры (чтобы работала верхняя панель)
        stats.GetData(gameGlobalKey).gamesStarted++;

        // В. Общая статистика приложения (на будущее)
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

        // Разбираем ключи
        string gameName = currentGameKey.Split('_')[0];
        string difficultyStr = currentGameKey.Split('_')[1];
        string variantStr = currentGameKey.Split('_')[2];

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

        // 2. Получаем текущие данные игры для расчетов (Уровень и Рекорды)
        StatData gameData = stats.GetData(gameGlobalKey);
        int currentLevel = (gameData != null) ? gameData.currentLevel : 1;

        // --- ПРОВЕРКА НА РЕКОРДЫ (ДО ТОГО КАК МЫ ИХ ОБНОВИЛИ) ---
        IsNewScoreRecord = false;
        IsNewTimeRecord = false;
        IsNewMovesRecord = false;

        StatData currentModeStats = stats.GetData(currentGameKey); // Берем стату конкретного режима (например Klondike_Hard_Draw3)
        if (currentModeStats != null)
        {
            // Если побед еще не было, или результат лучше предыдущего лучшего
            if (finalScore > currentModeStats.bestScore) IsNewScoreRecord = true;
            if (currentModeStats.bestTime == 0 || duration < currentModeStats.bestTime) IsNewTimeRecord = true;
            if (currentModeStats.fewestMoves == 0 || currentMoves < currentModeStats.fewestMoves) IsNewMovesRecord = true;

            // Защита от спама рекордов при счете 0 (если игра не на очки)
            if (finalScore == 0 && currentModeStats.bestScore == 0) IsNewScoreRecord = false;
        }
        // --------------------------------------------------------

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

        string[] keyParts = currentGameKey.Split('_');
        string gameName = keyParts[0];
        string difficultyStr = keyParts[1];
        string variantStr = keyParts[2];

        string gameGlobalKey = $"{gameName}_Global";
        string appGlobalKey = "Global";

        // 2. ПЕРЕДАЕМ duration И currentMoves ВМЕСТО 0
        // (Score при поражении оставляем 0)
        stats.UpdateData(currentGameKey, false, duration, currentMoves, 0, difficultyStr, gameName, variantStr);
        stats.UpdateData(gameGlobalKey, false, duration, currentMoves, 0, difficultyStr, gameName, variantStr);
        stats.UpdateData(appGlobalKey, false, duration, currentMoves, 0, difficultyStr, gameName, variantStr);

        SaveStats();

        isTimerRunning = false;
        hasTimerStarted = false;
        currentMoves = 0;
    }

    // --- SAVE / LOAD ---

    private void SaveStats()
    {
        string json = JsonUtility.ToJson(stats, true);
        File.WriteAllText(filePath, json);
    }

    private void LoadStats()
    {
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                stats = JsonUtility.FromJson<GameStatistics>(json);
                stats.BuildLookup();
                Log("Stats loaded successfully.");
            }
            catch
            {
                stats = new GameStatistics();
                Log("Error loading stats, creating new.");
            }
        }
        else
        {
            stats = new GameStatistics();
            Log("No stats file found, creating new.");
        }
    }

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