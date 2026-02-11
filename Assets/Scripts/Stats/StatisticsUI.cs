using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StatisticsUI : MonoBehaviour
{
    [Header("Header")]
    public TMP_Text headerText;
    public Button closeButton;

    [Header("Tabs (Global vs Diff)")]
    public Button globalTabButton;
    public TMP_Text globalTabLabel;
    public Button difficultiesTabButton;
    public TMP_Text difficultiesTabLabel;

    [Header("Visual Colors")]
    public Color activeTabColor = new Color32(255, 196, 0, 255);
    public Color inactiveTabColor = new Color32(101, 68, 45, 255);
    public Color activeTextColor = Color.black;
    public Color inactiveTextColor = Color.white;

    [Header("--- Containers References ---")]
    [Tooltip("Ссылка на общую панель, в которой лежат DiffContainer, DrawModeContainer и т.д.")]
    public GameObject filtersParentPanel; // <--- НОВОЕ ПОЛЕ: Общая панель фильтров

    [Header("Common: Difficulty")]
    public GameObject difficultyContainer;
    public Button[] difficultyButtons;     // 0: Easy, 1: Medium, 2: Hard

    [Header("Klondike: Draw Mode")]
    public GameObject drawModeContainer;
    public Button[] drawModeButtons;       // 0: Draw 1, 1: Draw 3

    [Header("Spider: Suit Mode")]
    public GameObject suitModeContainer;
    public Button[] suitModeButtons;       // 0: 1 Suit, 1: 2 Suits, 2: 4 Suits

    [Header("Pyramid/TriPeaks: Rounds")]
    public GameObject roundsContainer;
    public Button[] roundsButtons;         // 0: 1 Round, 1: 2 Rounds, 2: 3 Rounds

    [Header("Yukon/Montana/MonteCarlo: Game Mode")]
    public GameObject otherModesContainer;
    public Button[] otherModesButtons;

    [Header("Main Stats Display")]
    public TMP_Text gamesPlayedText;
    public TMP_Text winsText;

    [Header("Donut Chart")]
    public Image winRateCircle;
    public TMP_Text winRatePercentText;
    public TMP_Text winRateValueText;

    [Header("Performance")]
    public TMP_Text avgTimeText;
    public TMP_Text avgMovesText;

    [Header("Records")]
    public TMP_Text bestScoreText;
    public TMP_Text bestTimeText;
    public TMP_Text bestMovesText;

    [Header("Streaks")]
    public TMP_Text currentStreakText;
    public TMP_Text bestStreakText;

    [Header("History")]
    public Image[] historySlots;
    public Sprite winIcon;
    public Sprite lossIcon;
    public Sprite emptyIcon;

    // --- State ---
    private GameType currentGame;
    private bool isGlobalTab = true;
    private Difficulty currentDifficulty = Difficulty.Easy;
    private string currentVariantKey = "";

    private Button[] currentActiveVariantButtons;
    private List<string> currentActiveVariantKeys = new List<string>();

    private void Start()
    {
        if (globalTabButton) globalTabButton.onClick.AddListener(() => SetTab(true));
        if (difficultiesTabButton) difficultiesTabButton.onClick.AddListener(() => SetTab(false));
        if (closeButton) closeButton.onClick.AddListener(() => gameObject.SetActive(false));

        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            int index = i;
            if (difficultyButtons[i])
                difficultyButtons[i].onClick.AddListener(() => OnDifficultyClicked((Difficulty)index));
        }
    }

    public void ShowStatsForGame(GameType gameType)
    {
        currentGame = gameType;
        UpdateHeaderLocalisation();

        isGlobalTab = true;
        currentDifficulty = Difficulty.Easy;

        ConfigureGameVariants();
        RefreshWholeUI();
    }

    private void SetTab(bool isGlobal)
    {
        if (isGlobalTab == isGlobal) return;
        this.isGlobalTab = isGlobal;
        RefreshWholeUI();
    }

    private void OnDifficultyClicked(Difficulty diff)
    {
        currentDifficulty = diff;
        RefreshWholeUI();
    }

    private void OnVariantButtonClicked(int index)
    {
        if (index >= 0 && index < currentActiveVariantKeys.Count)
        {
            currentVariantKey = currentActiveVariantKeys[index];
            RefreshWholeUI();
        }
    }

    private void ConfigureGameVariants()
    {
        currentActiveVariantKeys.Clear();
        currentActiveVariantButtons = null;
        currentVariantKey = "Standard";

        switch (currentGame)
        {
            case GameType.Klondike:
                currentActiveVariantKeys.Add("Draw1");
                currentActiveVariantKeys.Add("Draw3");
                currentActiveVariantButtons = drawModeButtons;
                currentVariantKey = "Draw1";
                break;

            case GameType.Spider:
                currentActiveVariantKeys.Add("1Suit");
                currentActiveVariantKeys.Add("2Suits");
                currentActiveVariantKeys.Add("4Suits");
                currentActiveVariantButtons = suitModeButtons;
                currentVariantKey = "1Suit";
                break;

            case GameType.Pyramid:
            case GameType.TriPeaks:
                currentActiveVariantKeys.Add("1Rounds");
                currentActiveVariantKeys.Add("2Rounds");
                currentActiveVariantKeys.Add("3Rounds");
                currentActiveVariantButtons = roundsButtons;
                currentVariantKey = "1Rounds";
                break;

            case GameType.Yukon:
                currentActiveVariantKeys.Add("Classic");
                currentActiveVariantKeys.Add("Russian");
                currentActiveVariantButtons = otherModesButtons;
                currentVariantKey = "Classic";
                break;

            case GameType.MonteCarlo:
                currentActiveVariantKeys.Add("8Ways");
                currentActiveVariantKeys.Add("4Ways");
                currentActiveVariantButtons = otherModesButtons;
                currentVariantKey = "8Ways";
                break;

            case GameType.Montana:
                currentActiveVariantKeys.Add("Standard");
                currentActiveVariantKeys.Add("Hard");
                currentActiveVariantButtons = otherModesButtons;
                currentVariantKey = "Standard";
                break;

            default:
                currentActiveVariantButtons = null;
                break;
        }

        if (currentActiveVariantButtons != null)
        {
            for (int i = 0; i < currentActiveVariantButtons.Length; i++)
            {
                if (currentActiveVariantButtons[i] == null) continue;
                currentActiveVariantButtons[i].onClick.RemoveAllListeners();
                int idx = i;
                currentActiveVariantButtons[i].onClick.AddListener(() => OnVariantButtonClicked(idx));
            }
        }

        UpdateVariantLabels();
    }

    private void UpdateVariantLabels()
    {
        if (currentActiveVariantButtons != otherModesButtons) return;
        if (LocalizationManager.instance == null) return;

        for (int i = 0; i < currentActiveVariantButtons.Length; i++)
        {
            if (i >= currentActiveVariantKeys.Count) break;

            Button btn = currentActiveVariantButtons[i];
            if (btn == null) continue;

            TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
            if (label == null) continue;

            string variantKey = currentActiveVariantKeys[i];
            string localizationKey = variantKey;

            switch (variantKey)
            {
                case "Standard": localizationKey = "Classic"; break;
                case "Hard": localizationKey = "DiffHard"; break;
            }

            label.text = LocalizationManager.instance.GetLocalizedValue(localizationKey);
        }
    }

    private void RefreshWholeUI()
    {
        if (StatisticsManager.Instance == null) return;

        UpdateLayoutVisibility(); // Здесь логика появления новой панели
        UpdateTabVisuals();

        StatData dataToShow;
        if (isGlobalTab)
        {
            dataToShow = StatisticsManager.Instance.GetGameGlobalStats(currentGame.ToString());
        }
        else
        {
            dataToShow = StatisticsManager.Instance.GetStats(currentGame.ToString(), currentDifficulty, currentVariantKey);
            UpdateFilterButtonsVisuals();
        }

        FillData(dataToShow);
    }

    // --- ОБНОВЛЕННАЯ ЛОГИКА ВИДИМОСТИ ---
    private void UpdateLayoutVisibility()
    {
        if (isGlobalTab)
        {
            // 1. Если Global Stats — просто выключаем родительскую панель
            if (filtersParentPanel) filtersParentPanel.SetActive(false);

            // Внутренности можно не трогать, их все равно не видно
        }
        else
        {
            // 2. Если Difficulties Stats — включаем родительскую панель
            if (filtersParentPanel) filtersParentPanel.SetActive(true);

            // 3. Теперь настраиваем внутренности (какой именно контейнер показать)

            // Сначала всё выключаем внутри
            if (drawModeContainer) drawModeContainer.SetActive(false);
            if (suitModeContainer) suitModeContainer.SetActive(false);
            if (roundsContainer) roundsContainer.SetActive(false);
            if (otherModesContainer) otherModesContainer.SetActive(false);

            // Контейнер сложности нужен всегда в режиме Difficulties
            if (difficultyContainer) difficultyContainer.SetActive(true);

            // Включаем специфичный контейнер
            switch (currentGame)
            {
                case GameType.Klondike:
                    if (drawModeContainer) drawModeContainer.SetActive(true);
                    break;
                case GameType.Spider:
                    if (suitModeContainer) suitModeContainer.SetActive(true);
                    break;
                case GameType.Pyramid:
                case GameType.TriPeaks:
                    if (roundsContainer) roundsContainer.SetActive(true);
                    break;
                case GameType.Yukon:
                case GameType.Montana:
                case GameType.MonteCarlo:
                    if (otherModesContainer) otherModesContainer.SetActive(true);
                    break;
            }
        }
    }

    private void UpdateFilterButtonsVisuals()
    {
        if (currentActiveVariantButtons != null)
        {
            for (int i = 0; i < currentActiveVariantButtons.Length; i++)
            {
                if (i >= currentActiveVariantKeys.Count) break;
                bool isSelected = (currentActiveVariantKeys[i] == currentVariantKey);

                SetButtonVisualState(
                    currentActiveVariantButtons[i],
                    currentActiveVariantButtons[i].GetComponentInChildren<TMP_Text>(),
                    isSelected
                );
            }
        }

        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null) continue;
            bool isSelected = ((int)currentDifficulty == i);
            SetButtonVisualState(
                difficultyButtons[i],
                difficultyButtons[i].GetComponentInChildren<TMP_Text>(),
                isSelected
            );
        }
    }

    private void SetButtonVisualState(Button btn, TMP_Text label, bool isActive)
    {
        if (btn == null) return;
        Image bg = btn.GetComponent<Image>();
        if (bg) bg.color = isActive ? activeTabColor : inactiveTabColor;
        if (label) label.color = isActive ? activeTextColor : inactiveTextColor;
    }

    private void UpdateHeaderLocalisation()
    {
        if (headerText == null || LocalizationManager.instance == null) return;
        string key = GetStatsKey(currentGame);
        headerText.text = LocalizationManager.instance.GetLocalizedValue(key);
    }

    private string GetStatsKey(GameType type)
    {
        switch (type)
        {
            case GameType.Klondike: return "STATSKlondike";
            case GameType.Spider: return "STATSSpider";
            case GameType.FreeCell: return "STATSFreecell";
            case GameType.Pyramid: return "STATSPyramid";
            case GameType.TriPeaks: return "STATSTripeaks";
            case GameType.Octagon: return "STATSOctagon";
            case GameType.Montana: return "STATSMontana";
            case GameType.MonteCarlo: return "STATSMontecarlo";
            case GameType.Sultan: return "STATSSultan";
            case GameType.Yukon: return "STATSYukon";
            default: return "STATS" + type.ToString();
        }
    }

    private void UpdateTabVisuals()
    {
        SetButtonVisualState(globalTabButton, globalTabLabel, isGlobalTab);
        SetButtonVisualState(difficultiesTabButton, difficultiesTabLabel, !isGlobalTab);
    }

    private void FillData(StatData data)
    {
        if (data == null) data = new StatData();

        gamesPlayedText.text = data.gamesStarted.ToString();
        winsText.text = data.gamesWon.ToString();

        float winRate01 = (data.gamesStarted > 0) ? (float)data.gamesWon / data.gamesStarted : 0f;
        if (winRateCircle) winRateCircle.fillAmount = winRate01;
        if (winRatePercentText) winRatePercentText.text = $"{data.WinRate:F0}%";
        if (winRateValueText) winRateValueText.text = $"{data.WinRate:F0}%";

        avgTimeText.text = FormatTime(data.AvgTime);
        avgMovesText.text = $"{data.AvgMoves:F0}";

        bestScoreText.text = data.bestScore.ToString();
        bestTimeText.text = FormatTime(data.bestTime);
        bestMovesText.text = (data.fewestMoves == 0 || data.fewestMoves == int.MaxValue) ? "-" : data.fewestMoves.ToString();

        currentStreakText.text = data.currentStreak.ToString();
        bestStreakText.text = data.bestStreak.ToString();

        UpdateHistorySlots(data.history);
    }

    private void UpdateHistorySlots(List<GameHistoryEntry> history)
    {
        if (historySlots == null) return;
        int count = history.Count;
        for (int i = 0; i < historySlots.Length; i++)
        {
            int dataIndex = count - 1 - i;
            if (dataIndex >= 0)
            {
                var entry = history[dataIndex];
                historySlots[i].sprite = entry.won ? winIcon : lossIcon;
                historySlots[i].color = Color.white;
            }
            else
            {
                historySlots[i].sprite = emptyIcon;
                historySlots[i].color = (emptyIcon == null) ? Color.clear : new Color(1, 1, 1, 0.5f);
            }
        }
    }

    private string FormatTime(float s)
    {
        if (s <= 0 || s > 360000) return "-";
        int minutes = Mathf.FloorToInt(s / 60);
        int seconds = Mathf.FloorToInt(s % 60);
        if (minutes >= 60)
        {
            int hours = minutes / 60;
            minutes = minutes % 60;
            return $"{hours}h {minutes}m";
        }
        return $"{minutes}:{seconds:00}";
    }
}