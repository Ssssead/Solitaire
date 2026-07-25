using UnityEngine;
using System.Linq;

public class GameQuestTracker : MonoBehaviour
{
    public static GameQuestTracker Instance { get; private set; }

    [Header("Match Temp Data")]
    private string currentGameName;
    private string currentDifficulty;
    private string currentVariant;

    private int undosUsed = 0;
    private int currentCombo = 0;
    private int maxFreeCellsOccupied = 0;
    private int stockDraws = 0;

    private bool isMatchActive = false;
    private float matchStartTime = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // ==========================================
    // 1. УПРАВЛЕНИЕ ЖИЗНЕННЫМ ЦИКЛОМ МАТЧА
    // ==========================================

    public void StartMatch(string gameName, Difficulty difficulty, string variant)
    {
        currentGameName = gameName;
        currentDifficulty = difficulty.ToString();
        currentVariant = variant;

        undosUsed = 0;
        currentCombo = 0;
        maxFreeCellsOccupied = 0;
        stockDraws = 0;
        matchStartTime = Time.realtimeSinceStartup;
        isMatchActive = true;

        // Сбрасываем прогресс ТОЛЬКО для тех заданий, которые строго выполняются "за одну игру"
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetQuestProgressByAction(QuestActionType.RemovePyramidPair); // Идеальная зачистка
            QuestManager.Instance.ResetQuestProgressByAction(QuestActionType.SpecificRanksInFoundation); // Первые шаги

            // Сброс ComboPairsWithoutDraw и ComboCardsWithoutDraw отсюда тоже УДАЛЕН.
        }
    }

    public void ReportMatchEnd(GameHistoryEntry history, int remainingStock = 0, int remainingShuffles = 0)
    {
        if (!isMatchActive) return;
        isMatchActive = false;

        // 1. Накопительное время (Отправляем 1 раз за игру все накопленные секунды)
        float totalTime = Time.realtimeSinceStartup - matchStartTime;
        SendEvent(QuestActionType.AccumulatePlayTime, Mathf.CeilToInt(totalTime));

        // Отправляем базовый квест завершения матча
        SendEvent(QuestActionType.CompleteMatch, 1);
        SendEvent(QuestActionType.CompleteMatchesWithAtLeastOneWin, 1);

        // 2. POST-MATCH ПРОВЕРКИ (Используем ваш GameHistoryEntry)
        if (history.won)
        {
            SendEvent(QuestActionType.WinGame, 1);
            SendEvent(QuestActionType.WinDifferentGames, 1, history.gameName);
            SendEvent(QuestActionType.WinStreak, 1);

            // Проверка времени
            SendEvent(QuestActionType.PlayTimeUnder, Mathf.CeilToInt(history.time));

            // Проверка ходов
            SendEvent(QuestActionType.WinWithMoreThanXMoves, history.moves);

            // Проверка отмен ходов (Undo)
            SendEvent(QuestActionType.WinWithMaxUndos, undosUsed);
            if (undosUsed == 0) SendEvent(QuestActionType.WinWithoutUndo, 1);

            // Специфичные проверки
            if (currentGameName == "FreeCell") SendEvent(QuestActionType.WinWithMaxFreeCells, maxFreeCellsOccupied);
            SendEvent(QuestActionType.WinWithRemainingStock, remainingStock);
            SendEvent(QuestActionType.WinWithMaxStockDraws, stockDraws);
            if (currentGameName == "FreeCell")
            {
                // ---> ЛОГ 3: Смотрим, что отправляется в QuestManager <---
                Debug.Log($"<color=green>[GameQuestTracker]</color> Матч выигран! Отправляем в квест максимум ячеек: {maxFreeCellsOccupied}");
                SendEvent(QuestActionType.WinWithMaxFreeCells, maxFreeCellsOccupied);
            }
            if (currentGameName == "Montana") SendEvent(QuestActionType.WinWithRemainingShuffles, remainingShuffles);
        }
    }

    // ==========================================
    // 2. REAL-TIME СОБЫТИЯ (Вызываются из механик)
    // ==========================================

    public void RecordMove() { SendEvent(QuestActionType.MakeMoves, 1); }
    public void RecordUndoUsed()
    {
        undosUsed++;
        ResetCombo(); // Undo сбрасывает комбо
    }
    public void RecordScore(int points) { SendEvent(QuestActionType.ScorePoints, points); }

    public void RecordCardToFoundation(int cardRank)
    {
        SendEvent(QuestActionType.MoveCardsToFoundation, 1);
        SendEvent(QuestActionType.MoveSpecificRanks, 1, cardRank.ToString());

        if (cardRank == 13) // Король
            SendEvent(QuestActionType.CompleteFoundationPile, 1);

        if (cardRank == 1 || cardRank == 2) // Туз или Двойка
            SendEvent(QuestActionType.SpecificRanksInFoundation, 1, cardRank.ToString());
    }

    // ---> ДОБАВИТЬ ЭТОТ МЕТОД <---
    public void RecordCardRemovedFromFoundation(int cardRank)
    {
        SendEvent(QuestActionType.MoveCardsToFoundation, -1);
        SendEvent(QuestActionType.MoveSpecificRanks, -1, cardRank.ToString());

        if (cardRank == 13) // Убрали Короля - стопка больше не собрана
            SendEvent(QuestActionType.CompleteFoundationPile, -1);

        if (cardRank == 1 || cardRank == 2)
            SendEvent(QuestActionType.SpecificRanksInFoundation, -1, cardRank.ToString());
    }

    public void RecordFreeCellOccupied(int currentOccupied)
    {
        SendEvent(QuestActionType.MoveToFreeCell, 1);

        if (currentOccupied > maxFreeCellsOccupied)
        {
            maxFreeCellsOccupied = currentOccupied;
            // ---> ЛОГ 2: Фиксируем момент, когда трекер обновил максимум <---
            Debug.Log($"<color=orange>[GameQuestTracker]</color> НОВЫЙ МАКСИМУМ занятых ячеек: {maxFreeCellsOccupied}");
        }
    }

    public void RecordStockDraw()
    {
        stockDraws++;
        ResetCombo();
        SendEvent(QuestActionType.FlipStock, 1);
    }

    public void IncrementCombo(QuestActionType comboType)
    {
        currentCombo++;
        SendEvent(comboType, currentCombo); // Отправляем текущее комбо как значение
    }

    public void ResetCombo()
    {
        currentCombo = 0;

        // Мы УДАЛИЛИ отсюда вызовы QuestManager.Instance.ResetQuestProgressByAction,
        // потому что теперь комбо-задания запоминают максимальный (лучший) результат!
    }

    // Универсальная отправка в глобальный менеджер квестов
    public void SendEvent(QuestActionType action, int value = 1, string customEventId = "")
    {
        // ---> БЛОКИРОВКА НА УРОВНЕ СЦЕНЫ: Не отслеживаем действия в туториале <---
        if (GameSettings.IsTutorialMode) return;

        if (QuestManager.Instance == null) return;

        System.Enum.TryParse(currentGameName, out QuestCategory category);
        System.Enum.TryParse(currentDifficulty, out TargetMatchDifficulty matchDiff);

        QuestEventContext context = new QuestEventContext
        {
            category = category,
            actionType = action,
            value = value,
            matchDifficulty = matchDiff,
            variant = currentVariant,
            eventId = customEventId
        };

        QuestManager.Instance.ReportEvent(context);
    }
}