using System.Collections.Generic;
using UnityEngine;

public enum QuestCategory { General, Klondike, Spider, FreeCell, Pyramid, TriPeaks, Yukon, MonteCarlo, Montana, Sultan, Octagon }
public enum QuestDifficulty { Easy, Medium, Hard }
public enum QuestTriggerType { RealTime, PostMatch }
public enum QuestActionType
{
    MakeMoves,
    WinGame,
    MoveCardsToFoundation,
    FlipHiddenCards,
    PlayTimeUnder,
    WinWithoutUndo,
    ScorePoints,
    PlayDifferentGames,   
    MoveSpecificRanks,    
    FlipStock,            
    CompleteMatch,
    AccumulatePlayTime,
    WinDifferentGames,                // Выиграть в разные игры
    WinStreak,                        // Серия побед подряд
    WinWithMoreThanXMoves,            // Выиграть, сделав больше X ходов
    CompleteMatchesWithAtLeastOneWin,
    CompleteFoundationPile,      // Собрать полную стопку в доме (положить Короля)
    MoveFromWasteToFoundation,    // Перенести из сброса сразу в дом
    ClearTableauColumn,       // Очистить столбец на игровом столе (для "Уборки")
    MoveTableauToTableau,     // Переместить карту между столбцами (для "Сортировки")
    WinWithMaxUndos,
    MoveToFreeCell,               // Поместить карту в свободную ячейку
    MoveCardSequence,             // Переместить стопку карт (длиной не менее X)
    WinWithMaxFreeCells,          // Выиграть, заняв одновременно не более X свободных ячеек
    SpecificRanksInFoundation,
    RemovePyramidPair,          // Убрать любую пару (или одного Короля) в Пирамиде
    RemoveFromPyramidFigure,    // Убрать карту непосредственно из самой пирамиды
    RemovePairWithStock,        // Убрать пару, где хотя бы одна карта из колоды/сброса
    ComboPairsWithoutDraw,      // Комбо: убрать X пар подряд без переворота колоды
    WinWithRemainingStock,       // Выиграть, оставив в колоде не менее X карт
    RemoveBoardCard,              // Убрать карту с игрового поля (в сброс)
    ComboCardsWithoutDraw,        // Сделать комбо из X карт (одинаково работает и для Пирамиды, и для Трех вершин)
    ClearPeak,                    // Полностью очистить одну вершину (пик)
    WinWithMaxStockDraws,          // Выиграть, перевернув не более X карт из колоды
    RemoveMonteCarloPair,
    MoveFromReserve,
    FillMontanaGap,             // Успешно заполнить пустую ячейку (пробел)
    CompleteMontanaRow,         // Полностью собрать ряд от 2 до Короля
    ComboGapsWithoutShuffle,    // Серия заполнения пробелов без нажатия перетасовки
    WinWithRemainingShuffles,    // Выиграть партию, сохранив X перетасовок
    MoveFromCorner
}
public enum TargetMatchDifficulty
{
    Any,
    Easy,
    Medium,
    Hard
}

[CreateAssetMenu(fileName = "Quest_New", menuName = "Solitaire/Daily Quest Data")]
public class DailyQuestData : ScriptableObject
{
    [Header("Main Info")]
    public string questId;
    public string questName;
    [TextArea] public string descriptionTemplate;
    [Tooltip("Если снята, генератор не будет выдавать это задание игрокам (удобно для отключения временно неработающих квестов)")]
    public bool isAvailableInPool = true;
    [Header("Rules")]
    public QuestCategory category;
    public QuestDifficulty difficulty;       // Сложность задания (для XP)
    public QuestTriggerType triggerType;
    public QuestActionType actionType;

    [Header("Specific Conditions (Optional)")]
    [Tooltip("Для 'Упорный новичок'. Какую сложность матча нужно запустить?")]
    public TargetMatchDifficulty requiredMatchDifficulty = TargetMatchDifficulty.Any;
    [Tooltip("Например, 'Draw3' для партий 'по 3 карты'. Оставьте пустым, если не важно.")]
    public string requiredVariant = "";

    [Tooltip("Для 'Королевский сбор'. Ранги карт (1=Туз, 2=Двойка, 12=Дама, 13=Король)")]
    public List<int> targetRanks = new List<int>();

    [Header("Targets (Random Range)")]
    public int minTargetValue = 1;
    public int maxTargetValue = 1;
    [Tooltip("Дополнительное условие (например, 180 для 'быстрее 180 секунд' или 100 для 'больше 100 ходов')")]
    public int requiredConditionValue = 0;
    [Header("Rewards")]
    public int xpReward;

    private void OnValidate()
    {
        switch (difficulty)
        {
            case QuestDifficulty.Easy: xpReward = 200; break;
            case QuestDifficulty.Medium: xpReward = 400; break;
            case QuestDifficulty.Hard: xpReward = 700; break;
        }
        if (string.IsNullOrEmpty(questId)) questId = System.Guid.NewGuid().ToString();
        if (maxTargetValue < minTargetValue) maxTargetValue = minTargetValue;
    }
}