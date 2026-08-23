using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

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
    public Color inactiveTabColor = new Color32(154, 95, 64, 255);
    public Color disabledButtonColor = new Color32(101, 68, 45, 255);
    public Color activeTextColor = Color.black;
    public Color inactiveTextColor = Color.white;

    [Header("--- Filters Panels ---")]
    public GameObject complexFiltersPanel;
    public GameObject simpleFiltersPanel;

    [Header("Animations")]
    public float panelAnimDuration = 0.3f;
    public float panelFlyOffsetY = 300f;

    [Header("Complex Panel: Difficulty")]
    public GameObject complexDifficultyContainer;
    public Button[] complexDifficultyButtons; // 0: Easy, 1: Medium, 2: Hard

    [Header("Simple Panel: Difficulty")]
    public GameObject simpleDifficultyContainer;
    public Button[] simpleDifficultyButtons;  // 0: Easy, 1: Medium, 2: Hard

    [Header("Game Specific Modes (Complex)")]
    public GameObject drawModeContainer;
    public Button[] drawModeButtons;

    public GameObject suitModeContainer;
    public Button[] suitModeButtons;

    public GameObject roundsContainer;
    public Button[] roundsButtons;

    public GameObject yukonModeContainer;
    public Button[] yukonModeButtons;

    public GameObject monteCarloModeContainer;
    public Button[] monteCarloModeButtons;

    public GameObject montanaModeContainer;
    public Button[] montanaModeButtons;

    [Header("Main Stats Display")]
    public TMP_Text gamesPlayedText;
    public TMP_Text winsText;

    [Header("Donut Chart")]
    public GameObject coloredChartParts;
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
    private static GameType currentGame;
    private static bool isGlobalTab = true;
    private static Difficulty currentDifficulty = Difficulty.Easy;
    private static string currentVariantKey = "";

    private Button[] currentActiveVariantButtons;
    private List<string> currentActiveVariantKeys = new List<string>();

    // --- Animation State ---
    private Coroutine complexAnimCoroutine;
    private Coroutine simpleAnimCoroutine;
    private Vector2 complexStartPos;
    private Vector2 simpleStartPos;
    private bool isComplexVisible = false;
    private bool isSimpleVisible = false;
    private bool positionsInitialized = false;

    private void Start()
    {
        if (globalTabButton) globalTabButton.onClick.AddListener(() => SetTab(true));
        if (difficultiesTabButton) difficultiesTabButton.onClick.AddListener(() => SetTab(false));
        if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);

        for (int i = 0; i < complexDifficultyButtons.Length; i++)
        {
            int index = i;
            if (complexDifficultyButtons[i])
                complexDifficultyButtons[i].onClick.AddListener(() => OnDifficultyClicked((Difficulty)index));
        }

        for (int i = 0; i < simpleDifficultyButtons.Length; i++)
        {
            int index = i;
            if (simpleDifficultyButtons[i])
                simpleDifficultyButtons[i].onClick.AddListener(() => OnDifficultyClicked((Difficulty)index));
        }
    }
    private void OnEnable()
    {
        InitPanelPositions();
        UpdateHeaderLocalisation();

        isComplexVisible = false;
        isSimpleVisible = false;

        if (complexFiltersPanel)
        {
            complexFiltersPanel.SetActive(false);
            complexFiltersPanel.GetComponent<RectTransform>().anchoredPosition = complexStartPos + new Vector2(0, panelFlyOffsetY);
        }
        if (simpleFiltersPanel)
        {
            simpleFiltersPanel.SetActive(false);
            simpleFiltersPanel.GetComponent<RectTransform>().anchoredPosition = simpleStartPos + new Vector2(0, panelFlyOffsetY);
        }

        ConfigureGameVariants();

        // Для StatisticsUI:
        RefreshWholeUI();

        // ВНИМАНИЕ! Для BasicStatisticsUI замените строку выше на:
        // RefreshUI();
    }
    private void OnDisable()
    {
        // 1. Останавливаем запуски корутин анимации
        if (complexAnimCoroutine != null) StopCoroutine(complexAnimCoroutine);
        if (simpleAnimCoroutine != null) StopCoroutine(simpleAnimCoroutine);

        // 2. Сбрасываем флаги видимости
        isComplexVisible = false;
        isSimpleVisible = false;

        // 3. Жестко прячем обе боковые панели фильтров
        if (complexFiltersPanel != null)
        {
            complexFiltersPanel.SetActive(false);
            if (positionsInitialized)
            {
                complexFiltersPanel.GetComponent<RectTransform>().anchoredPosition = complexStartPos + new Vector2(0, panelFlyOffsetY);
            }
        }

        if (simpleFiltersPanel != null)
        {
            simpleFiltersPanel.SetActive(false);
            if (positionsInitialized)
            {
                simpleFiltersPanel.GetComponent<RectTransform>().anchoredPosition = simpleStartPos + new Vector2(0, panelFlyOffsetY);
            }
        }
    }
    private void InitPanelPositions()
    {
        if (positionsInitialized) return;
        if (complexFiltersPanel != null) complexStartPos = complexFiltersPanel.GetComponent<RectTransform>().anchoredPosition;
        if (simpleFiltersPanel != null) simpleStartPos = simpleFiltersPanel.GetComponent<RectTransform>().anchoredPosition;
        positionsInitialized = true;
    }

    public void ShowStatsForGame(GameType gameType)
    {
        // Только сбрасываем параметры перед новым открытием статистики
        currentGame = gameType;
        isGlobalTab = true;
        currentDifficulty = Difficulty.Easy;
        currentVariantKey = "";

        // Включение объекта теперь контролирует GameUIController,
        // поэтому gameObject.SetActive(true) отсюда убрали!
    }

    private void SetTab(bool isGlobal)
    {
        if (isGlobalTab == isGlobal) return;
        PlayClickSound();

        // Убрали "this."
        isGlobalTab = isGlobal;

        RefreshWholeUI();
    }

    private void OnDifficultyClicked(Difficulty diff)
    {
        PlayClickSound();
        currentDifficulty = diff;
        RefreshWholeUI();
    }

    private void OnVariantButtonClicked(int index)
    {
        PlayClickSound();
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
        string fallbackVariant = "Standard";

        switch (currentGame)
        {
            case GameType.Klondike:
                currentActiveVariantKeys.Add("Draw1");
                currentActiveVariantKeys.Add("Draw3");
                currentActiveVariantButtons = drawModeButtons;
                fallbackVariant = "Draw1";
                break;
            case GameType.Spider:
                currentActiveVariantKeys.Add("1Suit");
                currentActiveVariantKeys.Add("2Suits");
                currentActiveVariantKeys.Add("4Suits");
                currentActiveVariantButtons = suitModeButtons;
                fallbackVariant = "1Suit";
                break;
            case GameType.Pyramid:
            case GameType.TriPeaks:
                currentActiveVariantKeys.Add("1Rounds");
                currentActiveVariantKeys.Add("2Rounds");
                currentActiveVariantKeys.Add("3Rounds");
                currentActiveVariantButtons = roundsButtons;
                fallbackVariant = "1Rounds";
                break;
            case GameType.Yukon:
                currentActiveVariantKeys.Add("Classic");
                currentActiveVariantKeys.Add("Russian");
                currentActiveVariantButtons = yukonModeButtons;
                fallbackVariant = "Classic";
                break;
            case GameType.MonteCarlo:
                currentActiveVariantKeys.Add("8Ways");
                currentActiveVariantKeys.Add("4Ways");
                currentActiveVariantButtons = monteCarloModeButtons;
                fallbackVariant = "8Ways";
                break;
            case GameType.Montana:
                currentActiveVariantKeys.Add("Standard");
                currentActiveVariantKeys.Add("Hard");
                currentActiveVariantButtons = montanaModeButtons;
                fallbackVariant = "Standard";
                break;
            default:
                currentActiveVariantButtons = null;
                break;
        }

        // [ФИКС] Защита от сброса при повороте экрана
        if (string.IsNullOrEmpty(currentVariantKey) || !currentActiveVariantKeys.Contains(currentVariantKey))
        {
            currentVariantKey = fallbackVariant;
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
        if (currentActiveVariantButtons != yukonModeButtons &&
            currentActiveVariantButtons != monteCarloModeButtons &&
            currentActiveVariantButtons != montanaModeButtons) return;

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

        ValidateConstraints();

        UpdateLayoutVisibility();
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

    private void ValidateConstraints()
    {
        SetDifficultyInteractable(0, true);
        SetDifficultyInteractable(1, true);
        SetDifficultyInteractable(2, true);

        if (currentGame == GameType.Spider && !isGlobalTab)
        {
            if (currentVariantKey == "1Suit")
            {
                SetDifficultyInteractable(2, false);
                if (currentDifficulty == Difficulty.Hard) currentDifficulty = Difficulty.Medium;
            }
            else if (currentVariantKey == "4Suits")
            {
                SetDifficultyInteractable(0, false);
                if (currentDifficulty == Difficulty.Easy) currentDifficulty = Difficulty.Medium;
            }
        }
    }

    private void SetDifficultyInteractable(int index, bool interactable)
    {
        if (complexDifficultyButtons != null && index < complexDifficultyButtons.Length && complexDifficultyButtons[index] != null)
        {
            complexDifficultyButtons[index].interactable = interactable;
            ColorBlock cb = complexDifficultyButtons[index].colors;
            cb.disabledColor = Color.white;
            complexDifficultyButtons[index].colors = cb;
        }

        if (simpleDifficultyButtons != null && index < simpleDifficultyButtons.Length && simpleDifficultyButtons[index] != null)
        {
            simpleDifficultyButtons[index].interactable = interactable;
            ColorBlock cb = simpleDifficultyButtons[index].colors;
            cb.disabledColor = Color.white;
            simpleDifficultyButtons[index].colors = cb;
        }
    }

    private void UpdateLayoutVisibility()
    {
        bool useComplex = false;
        bool useSimple = false;

        if (!isGlobalTab)
        {
            switch (currentGame)
            {
                case GameType.Klondike:
                case GameType.Spider:
                case GameType.Pyramid:
                case GameType.TriPeaks:
                case GameType.Yukon:
                case GameType.Montana:
                case GameType.MonteCarlo:
                    useComplex = true;
                    break;
                case GameType.FreeCell:
                case GameType.Sultan:
                case GameType.Octagon:
                default:
                    useSimple = true;
                    break;
            }
        }

        if (isComplexVisible != useComplex)
        {
            isComplexVisible = useComplex;
            ToggleFilterPanel(complexFiltersPanel, useComplex, ref complexAnimCoroutine, complexStartPos);
        }

        if (isSimpleVisible != useSimple)
        {
            isSimpleVisible = useSimple;
            ToggleFilterPanel(simpleFiltersPanel, useSimple, ref simpleAnimCoroutine, simpleStartPos);
        }

        if (useComplex)
        {
            if (complexDifficultyContainer) complexDifficultyContainer.SetActive(true);
            if (drawModeContainer) drawModeContainer.SetActive(currentGame == GameType.Klondike);
            if (suitModeContainer) suitModeContainer.SetActive(currentGame == GameType.Spider);
            if (roundsContainer) roundsContainer.SetActive(currentGame == GameType.Pyramid || currentGame == GameType.TriPeaks);
            if (yukonModeContainer) yukonModeContainer.SetActive(currentGame == GameType.Yukon);
            if (monteCarloModeContainer) monteCarloModeContainer.SetActive(currentGame == GameType.MonteCarlo);
            if (montanaModeContainer) montanaModeContainer.SetActive(currentGame == GameType.Montana);
        }

        if (useSimple)
        {
            if (simpleDifficultyContainer) simpleDifficultyContainer.SetActive(true);
        }
    }

    private void ToggleFilterPanel(GameObject panel, bool show, ref Coroutine currentCoroutine, Vector2 defaultPos)
    {
        if (panel == null) return;
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(AnimateFilterPanelRoutine(panel, show, defaultPos));
    }

    private IEnumerator AnimateFilterPanelRoutine(GameObject panel, bool show, Vector2 defaultPos)
    {
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 hiddenPos = defaultPos + new Vector2(0, panelFlyOffsetY);

        if (show && !panel.activeSelf)
        {
            rt.anchoredPosition = hiddenPos;
            panel.SetActive(true);
        }

        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = show ? defaultPos : hiddenPos;

        float elapsed = 0f;
        while (elapsed < panelAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / panelAnimDuration;
            float curveT = show ? (1f - Mathf.Pow(1f - t, 3)) : (t * t * t);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, curveT);
            yield return null;
        }

        rt.anchoredPosition = endPos;
        if (!show) panel.SetActive(false);
    }

    private void HideFilters()
    {
        if (isComplexVisible)
        {
            isComplexVisible = false;
            ToggleFilterPanel(complexFiltersPanel, false, ref complexAnimCoroutine, complexStartPos);
        }
        if (isSimpleVisible)
        {
            isSimpleVisible = false;
            ToggleFilterPanel(simpleFiltersPanel, false, ref simpleAnimCoroutine, simpleStartPos);
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

        for (int i = 0; i < complexDifficultyButtons.Length; i++)
        {
            if (complexDifficultyButtons[i] == null) continue;
            bool isSelected = ((int)currentDifficulty == i);
            SetButtonVisualState(
                complexDifficultyButtons[i],
                complexDifficultyButtons[i].GetComponentInChildren<TMP_Text>(),
                isSelected
            );
        }

        for (int i = 0; i < simpleDifficultyButtons.Length; i++)
        {
            if (simpleDifficultyButtons[i] == null) continue;
            bool isSelected = ((int)currentDifficulty == i);
            SetButtonVisualState(
                simpleDifficultyButtons[i],
                simpleDifficultyButtons[i].GetComponentInChildren<TMP_Text>(),
                isSelected
            );
        }
    }

    private void SetButtonVisualState(Button btn, TMP_Text label, bool isActive)
    {
        if (btn == null) return;
        Image bg = btn.GetComponent<Image>();

        if (bg)
        {
            if (!btn.interactable) bg.color = disabledButtonColor;
            else bg.color = isActive ? activeTabColor : inactiveTabColor;
        }

        if (label)
        {
            if (!btn.interactable) label.color = new Color(inactiveTextColor.r, inactiveTextColor.g, inactiveTextColor.b, 0.5f);
            else label.color = isActive ? activeTextColor : inactiveTextColor;
        }
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

        if (data.gamesStarted == 0)
        {
            if (coloredChartParts != null) coloredChartParts.SetActive(false);
            if (winRatePercentText) winRatePercentText.text = "0%";
            if (winRateValueText) winRateValueText.text = "0%";
        }
        else
        {
            if (coloredChartParts != null) coloredChartParts.SetActive(true);
            if (winRateCircle) winRateCircle.fillAmount = winRate01;
            if (winRatePercentText) winRatePercentText.text = $"{data.WinRate:F0}%";
            if (winRateValueText) winRateValueText.text = $"{data.WinRate:F0}%";
        }

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

            HistorySlotHover hover = historySlots[i].GetComponent<HistorySlotHover>();
            if (hover == null) hover = historySlots[i].gameObject.AddComponent<HistorySlotHover>();

            if (dataIndex >= 0)
            {
                var entry = history[dataIndex];
                historySlots[i].sprite = entry.won ? winIcon : lossIcon;
                historySlots[i].color = Color.white;

                hover.Setup(entry, HistorySlotHover.SlotType.GameGlobal);
            }
            else
            {
                historySlots[i].sprite = emptyIcon;
                historySlots[i].color = (emptyIcon == null) ? Color.clear : new Color(1, 1, 1, 0.5f);

                hover.Setup(null, HistorySlotHover.SlotType.Difficulty);
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

    private void OnCloseClicked()
    {
        HideFilters();
        if (MenuController.Instance != null)
        {
            MenuController.Instance.OnCloseOverlayClicked();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    private void PlayClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Click");
    }
}