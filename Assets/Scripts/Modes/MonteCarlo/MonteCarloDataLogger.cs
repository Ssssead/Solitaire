using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// --- СТРУКТУРЫ ДЛЯ JSON ЛОГОВ ---
[Serializable]
public class MonteCarloMoveLog
{
    public string type;
    public int i1;
    public int i2;
    public string cards;
}

[Serializable]
public class MonteCarloSessionLog
{
    public string timestamp;
    public string generatorDifficulty;
    public string actualDifficulty;
    public string variant;
    public float timeSeconds;
    public int totalMoves;
    public string initialDeck;
    public string result;
    public List<MonteCarloMoveLog> moves = new List<MonteCarloMoveLog>();
}

[Serializable]
public class SessionDatabase
{
    public List<MonteCarloSessionLog> sessions = new List<MonteCarloSessionLog>();
}

public class MonteCarloDataLogger : MonoBehaviour
{
    public static MonteCarloDataLogger Instance;

    private MonteCarloSessionLog currentSession;
    private bool currentVariantIs8Ways;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartSession(string genDifficulty, bool is8Ways, Deal deal)
    {
        currentVariantIs8Ways = is8Ways;

        currentSession = new MonteCarloSessionLog
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            generatorDifficulty = genDifficulty,
            variant = is8Ways ? "8Ways" : "4Ways",
            initialDeck = BuildDeckString(deal)
        };
        Debug.Log("[DataLogger] Запись JSON сессии начата.");
    }

    public void LogMatch(int idx1, int idx2, CardModel c1, CardModel c2)
    {
        if (currentSession == null) return;
        currentSession.moves.Add(new MonteCarloMoveLog
        {
            type = "Match",
            i1 = idx1,
            i2 = idx2,
            cards = $"{GetShortCard(c1)} {GetShortCard(c2)}"
        });
    }

    public void LogUndo()
    {
        if (currentSession == null) return;
        currentSession.moves.Add(new MonteCarloMoveLog { type = "Undo", i1 = -1, i2 = -1, cards = "" });
    }

    public void LogUndoAll()
    {
        if (currentSession == null) return;
        currentSession.moves.Add(new MonteCarloMoveLog { type = "UndoAll", i1 = -1, i2 = -1, cards = "" });
    }

    public void LogDefeatPanel()
    {
        if (currentSession == null) return;
        currentSession.moves.Add(new MonteCarloMoveLog { type = "GameLostPanel", i1 = -1, i2 = -1, cards = "" });
        Debug.Log("[DataLogger] Событие: Игрок увидел панель поражения.");
    }

    // ИСПРАВЛЕНИЕ: Теперь метод принимает точное игровое время (gameTimer)
    public void EndSession(string result, float gameTime)
    {
        if (currentSession == null || currentSession.moves.Count == 0) return;

        int moveCount = currentSession.moves.Count;

        currentSession.result = result;
        currentSession.timeSeconds = gameTime;
        currentSession.totalMoves = moveCount;

        string actualDiff = EvaluateHumanPerformance(moveCount, gameTime);
        currentSession.actualDifficulty = actualDiff;

        string variantStr = currentVariantIs8Ways ? "8Ways" : "4Ways";
        string fileName = $"MonteCarloLogs_{actualDiff}_{variantStr}.json";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        SaveToDatabase(path, currentSession);

        currentSession = null;
    }

    private string EvaluateHumanPerformance(int moves, float time)
    {
        // 1. Идеальная или почти идеальная игра (до 30 ходов). 
        // Это точно Easy, даже если игрок думал над ходами до 3 минут.
        if (moves <= 30 && time <= 180f) return "easy";

        // 2. Стандартный Easy (есть пара отмен, до 40 ходов, уложился в 2 минуты)
        if (moves <= 40 && time <= 120f) return "easy";

        // 3. Откровенный Hard (куча отмен и блужданий, больше 80 ходов ИЛИ дольше 4 минут)
        if (moves >= 80 || time >= 240f) return "hard";

        // 4. Все остальное (рабочие отмены, среднее время) -> MEDIUM
        return "medium";
    }

    private void SaveToDatabase(string path, MonteCarloSessionLog newSession)
    {
        SessionDatabase db = new SessionDatabase();

        try
        {
            if (File.Exists(path))
            {
                string jsonIn = File.ReadAllText(path);
                db = JsonUtility.FromJson<SessionDatabase>(jsonIn) ?? new SessionDatabase();
            }

            db.sessions.Add(newSession);
            string jsonOut = JsonUtility.ToJson(db, true);
            File.WriteAllText(path, jsonOut);

            Debug.Log($"[DataLogger] Игра ({newSession.timeSeconds:F0}с, {newSession.totalMoves} ходов) сохранена в: {Path.GetFileName(path)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataLogger] Ошибка обновления базы данных {path}: {e.Message}");
        }
    }

    private string BuildDeckString(Deal deal)
    {
        string deckStr = "";
        List<CardModel> initCards = new List<CardModel>();
        for (int i = 0; i < 25; i++) initCards.Add(deal.tableau[i][0].Card);

        var stockArr = deal.stock.ToArray();
        for (int i = stockArr.Length - 1; i >= 0; i--) initCards.Add(stockArr[i].Card);

        for (int i = 0; i < initCards.Count; i++)
        {
            deckStr += GetShortCard(initCards[i]);
            if (i < initCards.Count - 1) deckStr += " ";
        }
        return deckStr;
    }

    private string GetShortCard(CardModel card)
    {
        string rankStr = card.rank.ToString();
        if (card.rank == 1) rankStr = "A";
        else if (card.rank == 11) rankStr = "J";
        else if (card.rank == 12) rankStr = "Q";
        else if (card.rank == 13) rankStr = "K";

        string suitStr = "";
        switch (card.suit)
        {
            case Suit.Spades: suitStr = "S"; break;
            case Suit.Hearts: suitStr = "H"; break;
            case Suit.Clubs: suitStr = "C"; break;
            case Suit.Diamonds: suitStr = "D"; break;
        }
        return rankStr + suitStr;
    }
}