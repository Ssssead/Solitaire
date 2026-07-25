using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
/*
public class YukonTelemetryTracker : MonoBehaviour
{
    public static YukonTelemetryTracker Instance;

    [Serializable]
    public class PlayerMoveLog
    {
        public int MoveIndex;
        public float ThinkTime;
        public string ActionType;
        public string CardDetails;
        public string FromTo;
    }

    [Serializable]
    public class CombinedGameLog
    {
        public string DealId;
        public string Variant;
        public string Difficulty;
        public string Timestamp;

        public int SolverTotalMoves;
        public int SolverBacktracks;
        public int InversionDepthSum;
        public List<string> InitialDealLayout = new List<string>();
        public List<string> SolverPath = new List<string>();

        public float PlayerTotalTime;
        public int PlayerTotalMoves;
        public int PlayerUndos;
        public int PlayerUndoAlls;
        public List<PlayerMoveLog> PlayerPath = new List<PlayerMoveLog>();

    }

    // --- НОВЫЙ КЛАСС-ОБЕРТКА ДЛЯ МАССИВА ЛОГОВ ---
    [Serializable]
    public class LogCollection
    {
        public List<CombinedGameLog> sessions = new List<CombinedGameLog>();
    }

    private CombinedGameLog currentLog;
    private float lastActionTime;
    private int moveCounter = 0;
    private int undoCounter = 0;
    private int undoAllCounter = 0;
    private bool isTracking = false;

    private void Awake() { if (Instance == null) Instance = this; }

    public void InitializeSession(string dealId, string variant, string diff, YukonSolver.ExtendedSolverResult solverResult, Deal initialDeal)
    {
        currentLog = new CombinedGameLog
        {
            DealId = dealId,
            Variant = variant,
            Difficulty = diff,
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            SolverTotalMoves = solverResult.Moves,
            SolverBacktracks = solverResult.Backtracks,
            InversionDepthSum = solverResult.InversionDepthSum,
            SolverPath = new List<string>(solverResult.SolutionPath)
        };

        // --- ГЕНЕРИРУЕМ СЖАТЫЙ РАСКЛАД ---
        for (int i = 0; i < 7; i++)
        {
            string colStr = "";
            var pile = initialDeal.tableau[i];
            for (int j = 0; j < pile.Count; j++)
            {
                var c = pile[j];
                string suitChar = c.Card.suit.ToString().Substring(0, 1); // C, D, H или S
                string faceChar = c.FaceUp ? "U" : "D"; // Up или Down
                colStr += $"{c.Card.rank}{suitChar}{faceChar}";
                if (j < pile.Count - 1) colStr += "|";
            }
            currentLog.InitialDealLayout.Add(colStr);
        }
        // ----------------------------------

        lastActionTime = Time.realtimeSinceStartup;
        moveCounter = 0; undoCounter = 0; undoAllCounter = 0;
        isTracking = true;
        Debug.Log($"[Telemetry] Запись начата. Солвер решил это за {solverResult.Moves} ходов.");
    }
    public void RecordMove(string actionType, string cardDetails, string fromTo)
    {
        if (!isTracking) return;
        float thinkTime = Time.realtimeSinceStartup - lastActionTime;
        lastActionTime = Time.realtimeSinceStartup;

        currentLog.PlayerPath.Add(new PlayerMoveLog { MoveIndex = ++moveCounter, ThinkTime = (float)Math.Round(thinkTime, 2), ActionType = actionType, CardDetails = cardDetails, FromTo = fromTo });
    }

    public void RecordUndo()
    {
        if (!isTracking) return;
        float thinkTime = Time.realtimeSinceStartup - lastActionTime;
        lastActionTime = Time.realtimeSinceStartup;

        moveCounter++;
        undoCounter++;

        currentLog.PlayerPath.Add(new PlayerMoveLog { MoveIndex = moveCounter, ThinkTime = (float)Math.Round(thinkTime, 2), ActionType = "Undo", CardDetails = "Single Move Reverted", FromTo = "N/A" });
    }

    public void RecordUndoAll()
    {
        if (!isTracking) return;
        float thinkTime = Time.realtimeSinceStartup - lastActionTime;
        lastActionTime = Time.realtimeSinceStartup;

        moveCounter++;
        undoAllCounter++;

        currentLog.PlayerPath.Add(new PlayerMoveLog { MoveIndex = moveCounter, ThinkTime = (float)Math.Round(thinkTime, 2), ActionType = "UndoAll", CardDetails = "GLOBAL RESTART", FromTo = "N/A" });
    }

    public void FinishAndExport()
    {
        if (!isTracking) return;
        isTracking = false;

        currentLog.PlayerTotalMoves = moveCounter;
        currentLog.PlayerUndos = undoCounter;
        currentLog.PlayerUndoAlls = undoAllCounter;

        float totalTime = 0;
        foreach (var m in currentLog.PlayerPath) totalTime += m.ThinkTime;
        currentLog.PlayerTotalTime = (float)Math.Round(totalTime, 2);

        // Формируем путь к файлу (теперь без времени в названии)
        string path = Path.Combine(Application.dataPath, "TelemetryLogs");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        // Название файла: "DeepResearch_Classic_Easy.json"
        string filename = $"DeepResearch_{currentLog.Variant}_{currentLog.Difficulty}.json";
        string fullPath = Path.Combine(path, filename);

        LogCollection collection = new LogCollection();

        // Если файл уже существует, читаем его и достаем старые логи
        if (File.Exists(fullPath))
        {
            try
            {
                string existingJson = File.ReadAllText(fullPath);
                collection = JsonUtility.FromJson<LogCollection>(existingJson);
                // Защита на случай, если файл был поврежден
                if (collection == null) collection = new LogCollection();
                if (collection.sessions == null) collection.sessions = new List<CombinedGameLog>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Telemetry] Ошибка чтения лога: {e.Message}. Создаем новый.");
            }
        }

        // Добавляем текущую игру в коллекцию
        collection.sessions.Add(currentLog);

        // Сохраняем обновленный файл
        File.WriteAllText(fullPath, JsonUtility.ToJson(collection, true));

        Debug.Log($"<color=cyan>[Telemetry] Игра добавлена в {filename}. Всего пройдено: {collection.sessions.Count}/10</color>");
    }
}*/