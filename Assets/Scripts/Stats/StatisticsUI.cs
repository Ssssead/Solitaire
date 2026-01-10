using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StatisticsUI : MonoBehaviour
{
    [Header("UI Header")]
    public TMP_Text headerText; // Текст заголовка (например "Klondike Stats")

    [Header("Global Stats References")]
    public StatBinder globalBinder; // Ссылки на верхнюю часть панели

    [Header("Difficulty Stats References")]
    // Это ссылки на ТЕКУЩИЕ видимые поля (Easy, Medium, Hard).
    // Скрипт будет менять в них текст в зависимости от выбранного режима.
    public DifficultyGroupBinder difficultyBinder;

    [Header("Variant Selector")]
    public GameObject selectorContainer; // Объект, содержащий кнопки и текст (чтобы скрыть для простых игр)
    public TMP_Text variantLabel;        // Текст "DrawMode: 1"
    public Button prevButton;            // Кнопка "<"
    public Button nextButton;            // Кнопка ">"

    [Header("Assets")]
    public Sprite winSprite;    // Зеленый
    public Sprite lossSprite;   // Красный
    public Sprite emptySprite;  // Серый

    // Внутреннее состояние
    private GameType currentGame;
    private int currentVariantIndex = 0;

    // Списки режимов для текущей игры
    // keys = то, что отправляем в StatisticsManager ("Draw1", "Draw3")
    // labels = то, что показываем игроку ("DrawMode: 1", "DrawMode: 3")
    private List<string> currentVariantKeys = new List<string>();
    private List<string> currentVariantLabels = new List<string>();

    private void Start()
    {
        // Подписываем кнопки
        if (prevButton) prevButton.onClick.AddListener(() => ChangeVariant(-1));
        if (nextButton) nextButton.onClick.AddListener(() => ChangeVariant(1));
    }

    /// <summary>
    /// Главный метод открытия статистики
    /// </summary>
    public void ShowStatsForGame(GameType game)
    {
        currentGame = game;
        currentVariantIndex = 0; // Сбрасываем на первый вариант

        SetupVariants(game);
        gameObject.SetActive(true);

        UpdateUI();
    }

    private void SetupVariants(GameType game)
    {
        currentVariantKeys.Clear();
        currentVariantLabels.Clear();

        switch (game)
        {
            case GameType.Klondike:
                // Настраиваем режимы Клондайка
                currentVariantKeys.Add("Draw1"); currentVariantLabels.Add("DrawMode: 1");
                currentVariantKeys.Add("Draw3"); currentVariantLabels.Add("DrawMode: 3");
                break;

            case GameType.Spider:
                // Настраиваем режимы Паука
                currentVariantKeys.Add("1Suit"); currentVariantLabels.Add("Suits: 1");
                currentVariantKeys.Add("2Suits"); currentVariantLabels.Add("Suits: 2");
                currentVariantKeys.Add("4Suits"); currentVariantLabels.Add("Suits: 4");
                break;
            case GameType.FreeCell:
                // Ключ "Standard" должен совпадать с тем, что вы передаете в OnGameStarted
                currentVariantKeys.Add("Standard"); currentVariantLabels.Add("Standard");
                break;
            default:
                // Для остальных игр (FreeCell и т.д.) только один стандартный режим
                currentVariantKeys.Add("Standard"); currentVariantLabels.Add("Standard");
                break;
        }

        // Если режим всего один (как в FreeCell), скрываем кнопки переключения
        if (selectorContainer != null)
        {
            selectorContainer.SetActive(currentVariantKeys.Count > 1);
        }
    }

    public void ChangeVariant(int direction)
    {
        if (currentVariantKeys.Count <= 1) return;

        currentVariantIndex += direction;

        // Зацикливаем переключение
        if (currentVariantIndex < 0) currentVariantIndex = currentVariantKeys.Count - 1;
        if (currentVariantIndex >= currentVariantKeys.Count) currentVariantIndex = 0;

        UpdateUI(); // Перерисовываем данные
    }

    public void UpdateUI()
    {
        if (StatisticsManager.Instance == null) return;

        if (headerText != null) headerText.text = $"{currentGame} Stats";

        // 1. Глобальная статистика игры (Верхняя панель) -> Тип GameGlobal
        StatData globalData = StatisticsManager.Instance.GetGameGlobalStats(currentGame.ToString());
        FillBinder(globalBinder, globalData, HistorySlotHover.SlotType.GameGlobal); // <--- Передаем тип
                                                                                    // 3. Выбор варианта (DrawMode)
        string currentKey = currentVariantKeys[currentVariantIndex];
        string currentLabel = currentVariantLabels[currentVariantIndex];

        // 2. Статистика по сложностям (Нижние панели) -> Тип Difficulty
        // Easy
        StatData easyData = StatisticsManager.Instance.GetStats(currentGame.ToString(), Difficulty.Easy, currentKey);
        FillBinder(difficultyBinder.easy, easyData, HistorySlotHover.SlotType.Difficulty); // <--- Передаем тип

        // Medium
        StatData mediumData = StatisticsManager.Instance.GetStats(currentGame.ToString(), Difficulty.Medium, currentKey);
        FillBinder(difficultyBinder.medium, mediumData, HistorySlotHover.SlotType.Difficulty); // <--- Передаем тип

        // Hard
        StatData hardData = StatisticsManager.Instance.GetStats(currentGame.ToString(), Difficulty.Hard, currentKey);
        FillBinder(difficultyBinder.hard, hardData, HistorySlotHover.SlotType.Difficulty); // <--- Передаем тип
    }

    private void FillBinder(StatBinder binder, StatData data, HistorySlotHover.SlotType slotType)
    {
        if (data == null) data = new StatData();

        SetText(binder.gamesPlayed, data.gamesStarted);
        SetText(binder.wins, data.gamesWon);
        SetText(binder.score, data.bestScore);
        SetText(binder.time, FormatTime(data.bestTime));
        SetText(binder.moves, data.fewestMoves == 0 ? "-" : data.fewestMoves.ToString());
        SetText(binder.winRate, $"{data.WinRate:F0}%");
        SetText(binder.avgTime, FormatTime(data.AvgTime));
        SetText(binder.avgMoves, $"{data.AvgMoves:F0}");
        SetText(binder.currentStreak, data.currentStreak);
        SetText(binder.bestStreak, data.bestStreak);

        // --- ИСТОРИЯ ---
        if (binder.historySlots != null && binder.historySlots.Length > 0)
        {
            for (int i = 0; i < binder.historySlots.Length; i++)
            {
                Image img = binder.historySlots[i];
                if (img == null) continue;

                // 1. Гарантируем наличие компонента (возвращаем AddComponent)
                HistorySlotHover hover = img.GetComponent<HistorySlotHover>();
                if (hover == null) hover = img.gameObject.AddComponent<HistorySlotHover>();

                if (i < data.history.Count)
                {
                    GameHistoryEntry entry = data.history[i];
                    bool won = entry.won;
                    img.sprite = won ? winSprite : lossSprite;
                    img.color = Color.white;

                    // 2. Передаем данные И ТИП СЛОТА
                    hover.Setup(entry, slotType);
                }
                else
                {
                    img.sprite = emptySprite;
                    img.color = (emptySprite == null) ? new Color(0, 0, 0, 0) : new Color(1, 1, 1, 0.5f);

                    // Очищаем
                    hover.Setup(null, slotType);
                }
            }
        }
    }

    // Хелпер для установки текста (чтобы не писать if null каждый раз)
    private void SetText(TMP_Text textParams, object value)
    {
        if (textParams != null) textParams.text = value.ToString();
    }

    private string FormatTime(float s)
    {
        if (s == 0 || s > 360000) return "-";
        return $"{(int)(s / 60)}:{(int)(s % 60):00}";
    }
}

// --- ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ДЛЯ ИНСПЕКТОРА ---

[System.Serializable]
public class StatBinder
{
    [Header("Basic")]
    public TMP_Text gamesPlayed;
    public TMP_Text wins;

    [Header("Records")]
    public TMP_Text score;
    public TMP_Text time;
    public TMP_Text moves;

    [Header("Performance")]
    public TMP_Text winRate;
    public TMP_Text avgTime;
    public TMP_Text avgMoves;

    [Header("Streaks")]
    public TMP_Text currentStreak;
    public TMP_Text bestStreak;

    [Header("History (Drag 10 Images here)")]
    public Image[] historySlots; // Массив уже созданных картинок
}

[System.Serializable]
public class DifficultyGroupBinder
{
    public StatBinder easy;
    public StatBinder medium;
    public StatBinder hard;
}