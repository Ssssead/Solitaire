using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class GameUIController : MonoBehaviour
{
    [System.Serializable]
    public class GameUIGroup
    {
        [Header("Main Panels")]
        public GameObject winPanel;
        public GameObject defeatPanel;
        public GameObject basicStatisticsPanel;
        public GameObject premiumStatisticsPanel;
        public GameObject settingsPanel;
        public GameObject newGameSettingsPanel;
        public GameObject exitConfirmationPanel;
        public GameObject newGameConfirmationPanel;

        [Header("Win Panel Stats")]
        public TMP_Text winDifficultyText;
        public TMP_Text winScoreText;
        public TMP_Text winTimeText;
        public TMP_Text winMovesText;
        public TMP_Text winEarnedXPText;
        public RectTransform winCardRect;
        public XPProgressBar winLevelBar;
        public GameObject[] standardWinButtons;
        public GameObject tutorialBigMenuButton;

        [Header("Win Panel Buttons Config")]
        public GameObject winDifficultyContainer;
        public GameObject winDrawContainer;
        public GameObject winSuitContainer;
        public Button[] winDiffButtons;
        public Button[] winDrawButtons;

        [Header("Defeat Panel Ads UI")]
        public GameObject undoOneAdIcon;
        public GameObject undoAllAdIcon;
        public RectTransform undoOneTextRect;
        public RectTransform undoAllTextRect;

        [Header("Settings Containers")]
        public GameObject settingsDifficultyContainer;
        public GameObject settingsDrawContainer;
        public GameObject settingsSuitContainer;
        public GameObject settingsRoundsContainer;
        public GameObject settingsYukonContainer;
        public GameObject settingsMonteCarloContainer;
        public GameObject settingsMontanaContainer;

        [Header("Settings Buttons")]
        public Button[] settingsDiffButtons;
        public Button[] settingsDrawButtons;
        public Button[] settingsSuitButtons;
        public Button[] settingsRoundsButtons;
        public Button settingsYukonClassicBtn;
        public Button settingsYukonRussianBtn;
        public Button settingsMonteCarlo8Btn;
        public Button settingsMonteCarlo4Btn;
        public Button settingsMontanaClassicBtn;
        public Button settingsMontanaHardBtn;
        public TMP_Text xpPreviewText;

        [Header("New Game Settings Containers")]
        public GameObject newGameSettingsDiffContainer;
        public GameObject newGameSettingsDrawContainer;
        public GameObject newGameSettingsSuitContainer;
        public GameObject newGameSettingsRoundsContainer;
        public GameObject newGameSettingsYukonContainer;
        public GameObject newGameSettingsMonteCarloContainer;
        public GameObject newGameSettingsMontanaContainer;

        [Header("New Game Settings Buttons")]
        public Button[] newGameSettingsDiffButtons;
        public Button[] newGameSettingsDrawButtons;
        public Button[] newGameSettingsSuitButtons;
        public Button[] newGameSettingsRoundsButtons;
        public Button newGameSettingsYukonClassicBtn;
        public Button newGameSettingsYukonRussianBtn;
        public Button newGameSettingsMonteCarlo8Btn;
        public Button newGameSettingsMonteCarlo4Btn;
        public Button newGameSettingsMontanaClassicBtn;
        public Button newGameSettingsMontanaHardBtn;
        public TMP_Text newGameXPPreviewText;
    }
    // --- МОСТЫ ДЛЯ ОБРАТНОЙ СОВМЕСТИМОСТИ СО СТАРЫМИ РЕЖИМАМИ ---
    public GameObject winPanel => ActiveUI.winPanel;
    public GameObject defeatPanel => ActiveUI.defeatPanel;
    public GameObject basicStatisticsPanel => ActiveUI.basicStatisticsPanel;
    public GameObject premiumStatisticsPanel => ActiveUI.premiumStatisticsPanel;
    public GameObject settingsPanel => ActiveUI.settingsPanel;
    public GameObject newGameSettingsPanel => ActiveUI.newGameSettingsPanel;
    public GameObject exitConfirmationPanel => ActiveUI.exitConfirmationPanel;
    public GameObject newGameConfirmationPanel => ActiveUI.newGameConfirmationPanel;

    public TMP_Text winDifficultyText => ActiveUI.winDifficultyText;
    public TMP_Text winScoreText => ActiveUI.winScoreText;
    public TMP_Text winTimeText => ActiveUI.winTimeText;
    public TMP_Text winMovesText => ActiveUI.winMovesText;
    public TMP_Text winEarnedXPText => ActiveUI.winEarnedXPText;
    public RectTransform winCardRect => ActiveUI.winCardRect;
    public XPProgressBar winLevelBar => ActiveUI.winLevelBar;
    public TMP_Text xpPreviewText => ActiveUI.xpPreviewText;
    public TMP_Text newGameXPPreviewText => ActiveUI.newGameXPPreviewText;
    // -------------------------------------------------------------

    [Header("UI Groups (Dual Orientation)")]
    public GameUIGroup landscapeUI;
    public GameUIGroup portraitUI;

    // Свойство для получения активного UI в зависимости от ориентации
    private GameUIGroup ActiveUI => (GameLayoutManager.Instance != null && GameLayoutManager.Instance.IsPortrait) ? portraitUI : landscapeUI;

    [Header("Defeat Panel Config")]
    [Tooltip("Значение Right из инспектора, когда картинки НЕТ")]
    public float textPaddingNormal = 16.35f;

    [Tooltip("Значение Right из инспектора, когда картинка ЕСТЬ")]
    public float textPaddingWithAd = 55.51f;

    [Header("Win/Defeat Controls (Visuals)")]
    public Color btnSelectedColor = new Color32(255, 176, 26, 255); // Orange
    public Color btnNormalColor = new Color32(154, 95, 64, 255);    // Brown
    public Color textSelectedColor = new Color32(36, 20, 12, 255);
    public Color textNormalColor = new Color32(192, 192, 192, 255);
    public Color buttonDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Цвет заблокированной кнопки Паука

    public string xpPreviewLocKey = "xp_reward_preview"; // "You will get {0} XP"

    [Header("Other Bars & Notifications")]
    public XPProgressBar localLevelBar;
    public LevelUpNotification globalLevelUpPopup;

    [Header("References")]
    private ICardGameMode activeGameMode;
    public UndoManager undoManager;
    public SceneExitAnimator exitAnimator;

    private Dictionary<GameObject, Vector2> panelInitialPositions = new Dictionary<GameObject, Vector2>();
    private Coroutine winSequenceCoroutine;
    private Vector2 winCardDefaultPos;
    private bool lastIsPortrait;
    private void Start()
    {
        if (activeGameMode == null)
        {
            foreach (var obj in FindObjectsOfType<MonoBehaviour>())
            {
                if (obj is ICardGameMode mode) { activeGameMode = mode; break; }
            }
        }

        if (undoManager == null) undoManager = FindObjectOfType<UndoManager>();
        if (exitAnimator == null) exitAnimator = GetComponent<SceneExitAnimator>() ?? FindObjectOfType<SceneExitAnimator>();

        RegisterGroupPanels(landscapeUI);
        RegisterGroupPanels(portraitUI);

        if (landscapeUI.winCardRect != null) winCardDefaultPos = landscapeUI.winCardRect.anchoredPosition;

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnLevelUp += HandleLevelUp;

        // [NEW] Запоминаем начальную ориентацию при старте
        if (GameLayoutManager.Instance != null)
            lastIsPortrait = GameLayoutManager.Instance.IsPortrait;
    }

    private void Update()
    {
        // [NEW] Отслеживаем поворот экрана и меняем панели
        if (GameLayoutManager.Instance != null)
        {
            bool currentIsPortrait = GameLayoutManager.Instance.IsPortrait;
            if (currentIsPortrait != lastIsPortrait)
            {
                HandleOrientationChange(currentIsPortrait);
                lastIsPortrait = currentIsPortrait;
            }
        }

        if (ActiveUI.defeatPanel != null && ActiveUI.defeatPanel.activeSelf)
        {
            UpdateDefeatAdVisuals();
        }
    }

    private void RegisterGroupPanels(GameUIGroup ui)
    {
        RegisterAndHidePanel(ui.winPanel);
        RegisterAndHidePanel(ui.defeatPanel);
        RegisterAndHidePanel(ui.basicStatisticsPanel);
        RegisterAndHidePanel(ui.premiumStatisticsPanel);
        RegisterAndHidePanel(ui.settingsPanel);
        RegisterAndHidePanel(ui.newGameSettingsPanel);
        RegisterAndHidePanel(ui.exitConfirmationPanel);
        RegisterAndHidePanel(ui.newGameConfirmationPanel);
        if (ui.winCardRect != null) ui.winCardRect.gameObject.SetActive(false);
    }

    private void UpdateDefeatAdVisuals()
    {
        bool isRewardFree = AdManager.Instance != null && AdManager.Instance.IsRewardFree();
        float currentRightPadding = isRewardFree ? textPaddingNormal : textPaddingWithAd;

        ApplyAdVisuals(landscapeUI, isRewardFree, currentRightPadding);
        ApplyAdVisuals(portraitUI, isRewardFree, currentRightPadding);
    }

    private void ApplyAdVisuals(GameUIGroup ui, bool isRewardFree, float padding)
    {
        if (ui.undoOneAdIcon != null) ui.undoOneAdIcon.SetActive(!isRewardFree);
        if (ui.undoAllAdIcon != null) ui.undoAllAdIcon.SetActive(!isRewardFree);
        if (ui.undoOneTextRect != null) ui.undoOneTextRect.offsetMax = new Vector2(-padding, ui.undoOneTextRect.offsetMax.y);
        if (ui.undoAllTextRect != null) ui.undoAllTextRect.offsetMax = new Vector2(-padding, ui.undoAllTextRect.offsetMax.y);
    }

    private void OnEnable()
    {
        AdManager.OnRewardEarned += HandleRewardEarned;
    }

    private void OnDisable()
    {
        AdManager.OnRewardEarned -= HandleRewardEarned;
    }

    private void HandleRewardEarned(string rewardId)
    {
        if (rewardId == "undo_one") ExecuteUndoOne();
        else if (rewardId == "undo_all") ExecuteUndoAll();
    }

    private void RegisterAndHidePanel(GameObject panel)
    {
        if (panel == null) return;
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt != null && !panelInitialPositions.ContainsKey(panel))
        {
            panelInitialPositions.Add(panel, rt.anchoredPosition);
        }
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnLevelUp -= HandleLevelUp;
    }

    // --- WIN LOGIC ---

    public void OnGameWon(int manualMoves = -1)
    {
        if (ActiveUI.defeatPanel) ActiveUI.defeatPanel.SetActive(false);
        if (ActiveUI.settingsPanel) ActiveUI.settingsPanel.SetActive(false);
        if (activeGameMode != null) activeGameMode.IsInputAllowed = false;

        if (winSequenceCoroutine != null) StopCoroutine(winSequenceCoroutine);
        winSequenceCoroutine = StartCoroutine(WinSequenceRoutine(manualMoves));
    }

    private IEnumerator WinSequenceRoutine(int manualMoves)
    {
        yield return new WaitForSeconds(1.0f);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Win_Sound");

        GameUIGroup ui = ActiveUI;
        if (ui.winPanel != null)
        {
            UpdateWinPanelStats(manualMoves, landscapeUI);
            UpdateWinPanelStats(manualMoves, portraitUI);
            SetupControlPanel(landscapeUI);
            SetupControlPanel(portraitUI);

            if (ui.winCardRect != null)
            {
                Vector2 startPos = winCardDefaultPos + new Vector2(1500f, 0f);
                ui.winCardRect.anchoredPosition = startPos;
                var hover = ui.winCardRect.GetComponent<CardHoverEffect>();
                if (hover != null) hover.SetSelectedMode(false);
                ui.winCardRect.gameObject.SetActive(true);
            }

            TogglePanelAnimated(ui.winPanel, true);
            if (ui.winCardRect != null) StartCoroutine(AnimateCardEntrance(ui.winCardRect));
            StartCoroutine(AnimateXPBarDelayed(ui));
        }
    }

    // --- DEFEAT LOGIC ---

    public void OnGameLost()
    {
        GameUIGroup ui = ActiveUI;
        if (ui.winPanel != null && ui.winPanel.activeSelf) return;
        if (ui.settingsPanel != null && ui.settingsPanel.activeSelf) TogglePanelAnimated(ui.settingsPanel, false);

        if (ui.defeatPanel != null && !ui.defeatPanel.activeSelf)
        {
            UpdateXPPreviews(landscapeUI);
            UpdateXPPreviews(portraitUI);

            TogglePanelAnimated(ui.defeatPanel, true);
            if (activeGameMode != null) activeGameMode.IsInputAllowed = false;

            // --- ИСПРАВЛЕНИЕ: Вырываем карту из рук игрока и возвращаем на место ---
            var dragMgr = FindObjectOfType<DragManager>();
            if (dragMgr != null) dragMgr.ForceSnapBackLastDrop();
            // -----------------------------------------------------------------------

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Loss_Sound");
        }
    }

    // --- SETTINGS BUTTONS LOGIC (SYNC) ---

    public void OnSettingsClicked()
    {
        PlayClickSound();
        GameUIGroup ui = ActiveUI;
        if (ui.settingsPanel != null && !ui.settingsPanel.activeSelf)
        {
            SetupSettingsGroup(landscapeUI);
            SetupSettingsGroup(portraitUI);
            TogglePanelAnimated(ui.settingsPanel, true);
        }
    }

    public void OnCloseSettingsClicked()
    {
        PlayClickSound();
        if (ActiveUI.settingsPanel != null) TogglePanelAnimated(ActiveUI.settingsPanel, false);
    }

    private void SetupSettingsGroup(GameUIGroup ui)
    {
        if (activeGameMode == null) return;
        GameType type = activeGameMode.GameType;

        if (ui.settingsDrawContainer) ui.settingsDrawContainer.SetActive(type == GameType.Klondike);
        if (ui.settingsSuitContainer) ui.settingsSuitContainer.SetActive(type == GameType.Spider);
        if (ui.settingsRoundsContainer) ui.settingsRoundsContainer.SetActive(type == GameType.Pyramid || type == GameType.TriPeaks);
        if (ui.settingsYukonContainer) ui.settingsYukonContainer.SetActive(type == GameType.Yukon);
        if (ui.settingsMonteCarloContainer) ui.settingsMonteCarloContainer.SetActive(type == GameType.MonteCarlo);
        if (ui.settingsMontanaContainer) ui.settingsMontanaContainer.SetActive(type == GameType.Montana);
        if (ui.settingsDifficultyContainer) ui.settingsDifficultyContainer.SetActive(true);

        if (type == GameType.Spider) ValidateSpiderConstraints(GameSettings.SpiderSuitCount, ui.settingsDiffButtons);
        else EnableAllDifficulties(ui.settingsDiffButtons);

        UpdateSettingsVisuals();
    }

    private void UpdateSettingsVisuals()
    {
        ApplySettingsVisuals(landscapeUI);
        ApplySettingsVisuals(portraitUI);
        UpdateXPPreviews(landscapeUI);
        UpdateXPPreviews(portraitUI);
    }

    private void ApplySettingsVisuals(GameUIGroup ui)
    {
        int diffIndex = (int)GameSettings.CurrentDifficulty;
        if (ui.settingsDiffButtons != null)
        {
            for (int i = 0; i < ui.settingsDiffButtons.Length; i++)
            {
                if (ui.settingsDiffButtons[i] != null)
                {
                    if (!ui.settingsDiffButtons[i].interactable) ui.settingsDiffButtons[i].image.color = buttonDisabledColor;
                    else SetButtonState(ui.settingsDiffButtons[i], i == diffIndex);
                }
            }
        }

        if (ui.settingsDrawButtons != null && ui.settingsDrawButtons.Length >= 2)
        {
            SetButtonState(ui.settingsDrawButtons[0], GameSettings.KlondikeDrawCount == 1);
            SetButtonState(ui.settingsDrawButtons[1], GameSettings.KlondikeDrawCount == 3);
        }
        if (ui.settingsSuitButtons != null && ui.settingsSuitButtons.Length >= 3)
        {
            SetButtonState(ui.settingsSuitButtons[0], GameSettings.SpiderSuitCount == 1);
            SetButtonState(ui.settingsSuitButtons[1], GameSettings.SpiderSuitCount == 2);
            SetButtonState(ui.settingsSuitButtons[2], GameSettings.SpiderSuitCount == 4);
        }
        if (ui.settingsRoundsButtons != null && ui.settingsRoundsButtons.Length >= 3)
        {
            SetButtonState(ui.settingsRoundsButtons[0], GameSettings.RoundsCount == 1);
            SetButtonState(ui.settingsRoundsButtons[1], GameSettings.RoundsCount == 2);
            SetButtonState(ui.settingsRoundsButtons[2], GameSettings.RoundsCount == 3);
        }

        SetButtonState(ui.settingsYukonClassicBtn, !GameSettings.YukonRussian);
        SetButtonState(ui.settingsYukonRussianBtn, GameSettings.YukonRussian);
        SetButtonState(ui.settingsMonteCarlo8Btn, !GameSettings.MonteCarlo4Ways);
        SetButtonState(ui.settingsMonteCarlo4Btn, GameSettings.MonteCarlo4Ways);
        SetButtonState(ui.settingsMontanaClassicBtn, !GameSettings.MontanaHard);
        SetButtonState(ui.settingsMontanaHardBtn, GameSettings.MontanaHard);
    }

    public void OnSettingsDifficultyClicked(int diffIndex) { PlayClickSound(); GameSettings.CurrentDifficulty = (Difficulty)diffIndex; UpdateSettingsVisuals(); }
    public void OnSettingsDrawModeClicked(int drawCount) { PlayClickSound(); GameSettings.KlondikeDrawCount = drawCount; UpdateSettingsVisuals(); }
    public void OnSettingsSuitClicked(int count) { PlayClickSound(); GameSettings.SpiderSuitCount = count; ValidateSpiderConstraints(count, landscapeUI.settingsDiffButtons); ValidateSpiderConstraints(count, portraitUI.settingsDiffButtons); UpdateSettingsVisuals(); }
    public void OnSettingsRoundsClicked(int index) { PlayClickSound(); GameSettings.RoundsCount = index + 1; UpdateSettingsVisuals(); }
    public void OnSettingsYukonClicked(int mode) { PlayClickSound(); GameSettings.YukonRussian = (mode == 1); UpdateSettingsVisuals(); }
    public void OnSettingsMonteCarloClicked(int mode) { PlayClickSound(); GameSettings.MonteCarlo4Ways = (mode == 1); UpdateSettingsVisuals(); }
    public void OnSettingsMontanaClicked(int mode) { PlayClickSound(); GameSettings.MontanaHard = (mode == 1); UpdateSettingsVisuals(); }

    // --- ОБРАБОТЧИКИ КНОПОК ДЛЯ НАСТРОЕК НОВОЙ ИГРЫ ---
    public void OnNewGameSettingsSuitClicked(int count) { PlayClickSound(); GameSettings.SpiderSuitCount = count; ValidateSpiderConstraints(count, landscapeUI.newGameSettingsDiffButtons); ValidateSpiderConstraints(count, portraitUI.newGameSettingsDiffButtons); UpdateNewGameSettingsVisuals(); }
    public void OnNewGameSettingsRoundsClicked(int index) { PlayClickSound(); GameSettings.RoundsCount = index + 1; UpdateNewGameSettingsVisuals(); }
    public void OnNewGameSettingsYukonClicked(int mode) { PlayClickSound(); GameSettings.YukonRussian = (mode == 1); UpdateNewGameSettingsVisuals(); }
    public void OnNewGameSettingsMonteCarloClicked(int mode) { PlayClickSound(); GameSettings.MonteCarlo4Ways = (mode == 1); UpdateNewGameSettingsVisuals(); }
    public void OnNewGameSettingsMontanaClicked(int mode) { PlayClickSound(); GameSettings.MontanaHard = (mode == 1); UpdateNewGameSettingsVisuals(); }

    private void ValidateSpiderConstraints(int suitCount, Button[] diffBtns)
    {
        EnableAllDifficulties(diffBtns);

        if (suitCount == 1)
        {
            if (diffBtns != null && diffBtns.Length > 2 && diffBtns[2] != null) diffBtns[2].interactable = false;
            if (GameSettings.CurrentDifficulty == Difficulty.Hard) GameSettings.CurrentDifficulty = Difficulty.Medium;
        }
        else if (suitCount == 4)
        {
            if (diffBtns != null && diffBtns.Length > 0 && diffBtns[0] != null) diffBtns[0].interactable = false;
            if (GameSettings.CurrentDifficulty == Difficulty.Easy) GameSettings.CurrentDifficulty = Difficulty.Medium;
        }
    }

    private void EnableAllDifficulties(Button[] diffBtns)
    {
        if (diffBtns == null) return;
        foreach (var btn in diffBtns)
        {
            if (btn != null) btn.interactable = true;
        }
    }

    private void SetButtonState(Button btn, bool isSelected)
    {
        if (btn == null) return;
        btn.image.color = isSelected ? btnSelectedColor : btnNormalColor;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp) tmp.color = isSelected ? textSelectedColor : textNormalColor;
    }

    // --- КНОПКИ В ПАНЕЛИ ПОБЕДЫ ---

    private void SetupControlPanel(GameUIGroup ui)
    {
        if (activeGameMode == null) return;
        GameType type = activeGameMode.GameType;

        if (ui.winDrawContainer) ui.winDrawContainer.SetActive(type == GameType.Klondike);
        if (ui.winSuitContainer) ui.winSuitContainer.SetActive(type == GameType.Spider);
        if (ui.winDifficultyContainer) ui.winDifficultyContainer.SetActive(true);

        UpdateControlVisuals(ui);
    }

    private void UpdateControlVisuals(GameUIGroup ui)
    {
        int diffIndex = (int)GameSettings.CurrentDifficulty;
        if (ui.winDiffButtons != null)
        {
            for (int i = 0; i < ui.winDiffButtons.Length; i++)
            {
                if (ui.winDiffButtons[i] != null) SetButtonState(ui.winDiffButtons[i], i == diffIndex);
            }
        }

        if (ui.winDrawButtons != null && ui.winDrawButtons.Length >= 2)
        {
            SetButtonState(ui.winDrawButtons[0], GameSettings.KlondikeDrawCount == 1);
            SetButtonState(ui.winDrawButtons[1], GameSettings.KlondikeDrawCount == 3);
        }
    }
    private void UpdateControlVisuals()
    {
        UpdateControlVisualsForGroup(landscapeUI);
        UpdateControlVisualsForGroup(portraitUI);
    }
    private void UpdateControlVisualsForGroup(GameUIGroup ui)
    {
        int diffIndex = (int)GameSettings.CurrentDifficulty;
        if (ui.winDiffButtons != null)
        {
            for (int i = 0; i < ui.winDiffButtons.Length; i++)
            {
                if (ui.winDiffButtons[i] != null) SetButtonState(ui.winDiffButtons[i], i == diffIndex);
            }
        }
        if (ui.winDrawButtons != null && ui.winDrawButtons.Length >= 2)
        {
            SetButtonState(ui.winDrawButtons[0], GameSettings.KlondikeDrawCount == 1);
            SetButtonState(ui.winDrawButtons[1], GameSettings.KlondikeDrawCount == 3);
        }
    }
    private void UpdateWinPanelStats(int manualMoves)
    {
        UpdateWinPanelStats(manualMoves, landscapeUI);
        UpdateWinPanelStats(manualMoves, portraitUI);
    }
    private void UpdateXPPreviewText()
    {
        UpdateXPPreviews(landscapeUI);
        UpdateXPPreviews(portraitUI);
    }
    private void UpdateNewGameXPPreview()
    {
        UpdateXPPreviews(landscapeUI);
        UpdateXPPreviews(portraitUI);
    }
    public void OnWinDifficultyClicked(int diffIndex)
    {
        PlayClickSound();
        GameSettings.CurrentDifficulty = (Difficulty)diffIndex;
        UpdateControlVisuals(landscapeUI);
        UpdateControlVisuals(portraitUI);
        UpdateXPPreviews(landscapeUI);
        UpdateXPPreviews(portraitUI);
    }

    public void OnWinDrawModeClicked(int drawCount)
    {
        PlayClickSound();
        GameSettings.KlondikeDrawCount = drawCount;
        UpdateControlVisuals(landscapeUI);
        UpdateControlVisuals(portraitUI);
        UpdateXPPreviews(landscapeUI);
        UpdateXPPreviews(portraitUI);
    }

    // --- MENU AND EXIT LOGIC ---

    public void OnMenuClicked()
    {
        PlayClickSound();
        InGameQuestNotification.Instance?.ForceCloseAll();

        GameUIGroup ui = ActiveUI;
        if (ui.winPanel != null && ui.winPanel.activeSelf)
        {
            OnConfirmExitClicked();
            return;
        }

        bool needConfirmation = false;
        if (ui.defeatPanel != null && ui.defeatPanel.activeSelf) needConfirmation = true;
        else if (activeGameMode != null && activeGameMode.IsMatchInProgress() && !GameSettings.IsTutorialMode)
        {
            int moves = (StatisticsManager.Instance != null) ? StatisticsManager.Instance.GetCurrentMoves() : 0;
            if (moves > 0) needConfirmation = true;
        }

        activeGameMode?.Tutorial?.HidePanelToLeft();

        if (needConfirmation && ui.exitConfirmationPanel != null) TogglePanelAnimated(ui.exitConfirmationPanel, true);
        else OnConfirmExitClicked();
    }

    public void OnConfirmExitClicked()
    {
        PlayClickSound();
        activeGameMode?.Tutorial?.HidePanelToLeft();
        GameUIGroup ui = ActiveUI;

        if (ui.exitConfirmationPanel != null && ui.exitConfirmationPanel.activeSelf) TogglePanelAnimated(ui.exitConfirmationPanel, false);

        if (ui.winPanel != null && ui.winPanel.activeSelf) StartCoroutine(ExitMenuSequenceWithAnimation(ui));
        else if (ui.defeatPanel != null && ui.defeatPanel.activeSelf) StartCoroutine(DefeatExitSequenceWithAnimation(ui));
        else PerformSceneExit();
    }

    private IEnumerator DefeatExitSequenceWithAnimation(GameUIGroup ui)
    {
        if (ui.defeatPanel != null) TogglePanelAnimated(ui.defeatPanel, false);
        yield return new WaitForSeconds(0.3f);
        PerformSceneExit();
    }

    private IEnumerator ExitMenuSequenceWithAnimation(GameUIGroup ui)
    {
        // Скрываем панель настроек
        if (ui.settingsPanel != null && ui.settingsPanel.activeSelf) TogglePanelAnimated(ui.settingsPanel, false);

        // ---> ДОБАВЛЕНО: Скрываем обе панели статистики <---
        if (ui.basicStatisticsPanel != null && ui.basicStatisticsPanel.activeSelf) TogglePanelAnimated(ui.basicStatisticsPanel, false);
        if (ui.premiumStatisticsPanel != null && ui.premiumStatisticsPanel.activeSelf) TogglePanelAnimated(ui.premiumStatisticsPanel, false);

        if (ui.winCardRect != null && ui.winCardRect.gameObject.activeSelf) StartCoroutine(AnimateCardExit(ui.winCardRect));
        if (ui.winPanel != null) TogglePanelAnimated(ui.winPanel, false);

        yield return new WaitForSeconds(0.5f);
        PerformSceneExit();
    }

    private void AbortActiveGameAnimations()
    {
        if (activeGameMode is MonoBehaviour modeMb)
        {
            modeMb.StopAllCoroutines();
            var deck = modeMb.GetComponent("DeckManager") as MonoBehaviour;
            if (deck != null) deck.StopAllCoroutines();
            var anim = modeMb.GetComponent("AnimationService") as MonoBehaviour;
            if (anim != null) anim.StopAllCoroutines();
        }

        var allCards = FindObjectsOfType<CardController>();
        foreach (var card in allCards)
        {
            if (card != null) card.StopAllCoroutines();
        }
    }

    private void PerformSceneExit()
    {
        if (DealCacheSystem.Instance != null)
        {
            int moves = (StatisticsManager.Instance != null) ? StatisticsManager.Instance.GetCurrentMoves() : 0;
            if (moves > 0) DealCacheSystem.Instance.DiscardActiveDeal();
            else DealCacheSystem.Instance.ReturnActiveDealToQueue();
        }

        activeGameMode?.Tutorial?.HideHighlights();

        if (exitAnimator != null)
        {
            if (activeGameMode != null) activeGameMode.IsInputAllowed = false;
            AbortActiveGameAnimations();
            exitAnimator.PlayExitSequence(() => SceneManager.LoadScene("MenuScene"));
        }
        else
        {
            SceneManager.LoadScene("MenuScene");
        }
    }

    public void OnCancelExitClicked()
    {
        PlayClickSound();
        if (ActiveUI.exitConfirmationPanel != null) TogglePanelAnimated(ActiveUI.exitConfirmationPanel, false);
        activeGameMode?.Tutorial?.RestorePanelPosition();
    }

    // --- NEW GAME LOGIC ---

    public void OnNewGameClicked()
    {
        PlayClickSound();
        GameUIGroup ui = ActiveUI;
        if (ui.winPanel != null && ui.winPanel.activeSelf)
        {
            OnConfirmNewGameClicked();
            return;
        }
        if (ui.newGameConfirmationPanel != null) TogglePanelAnimated(ui.newGameConfirmationPanel, true);
        else OnConfirmNewGameClicked();
    }

    public void OnNewGameSettingsClicked()
    {
        PlayClickSound();
        GameUIGroup ui = ActiveUI;
        if (ui.newGameConfirmationPanel != null) TogglePanelAnimated(ui.newGameConfirmationPanel, false);
        if (ui.newGameSettingsPanel != null)
        {
            SetupNewGameGroup(landscapeUI);
            SetupNewGameGroup(portraitUI);
            TogglePanelAnimated(ui.newGameSettingsPanel, true);
        }
    }

    private void SetupNewGameGroup(GameUIGroup ui)
    {
        if (activeGameMode == null) return;
        GameType type = activeGameMode.GameType;

        if (ui.newGameSettingsDrawContainer) ui.newGameSettingsDrawContainer.SetActive(type == GameType.Klondike);
        if (ui.newGameSettingsSuitContainer) ui.newGameSettingsSuitContainer.SetActive(type == GameType.Spider);
        if (ui.newGameSettingsRoundsContainer) ui.newGameSettingsRoundsContainer.SetActive(type == GameType.Pyramid || type == GameType.TriPeaks);
        if (ui.newGameSettingsYukonContainer) ui.newGameSettingsYukonContainer.SetActive(type == GameType.Yukon);
        if (ui.newGameSettingsMonteCarloContainer) ui.newGameSettingsMonteCarloContainer.SetActive(type == GameType.MonteCarlo);
        if (ui.newGameSettingsMontanaContainer) ui.newGameSettingsMontanaContainer.SetActive(type == GameType.Montana);
        if (ui.newGameSettingsDiffContainer) ui.newGameSettingsDiffContainer.SetActive(true);

        if (type == GameType.Spider) ValidateSpiderConstraints(GameSettings.SpiderSuitCount, ui.newGameSettingsDiffButtons);
        else EnableAllDifficulties(ui.newGameSettingsDiffButtons);

        UpdateNewGameSettingsVisuals();
    }

    public void OnConfirmNewGameClicked()
    {
        PlayClickSound();
        GameSettings.IsTutorialMode = false;
        if (ActiveUI.newGameConfirmationPanel != null && ActiveUI.newGameConfirmationPanel.activeSelf)
        {
            TogglePanelAnimated(ActiveUI.newGameConfirmationPanel, false);
        }
        StartCoroutine(RestartSequenceRoutine());
    }

    private IEnumerator RestartSequenceRoutine()
    {
        if (winSequenceCoroutine != null) StopCoroutine(winSequenceCoroutine);
        GameUIGroup ui = ActiveUI;

        if (ui.settingsPanel != null && ui.settingsPanel.activeSelf) TogglePanelAnimated(ui.settingsPanel, false);
        if (ui.newGameSettingsPanel != null && ui.newGameSettingsPanel.activeSelf) TogglePanelAnimated(ui.newGameSettingsPanel, false);

        // ---> ДОБАВЛЕНО: Скрываем обе панели статистики <---
        if (ui.basicStatisticsPanel != null && ui.basicStatisticsPanel.activeSelf) TogglePanelAnimated(ui.basicStatisticsPanel, false);
        if (ui.premiumStatisticsPanel != null && ui.premiumStatisticsPanel.activeSelf) TogglePanelAnimated(ui.premiumStatisticsPanel, false);

        if ((ui.settingsPanel != null && ui.settingsPanel.activeSelf) || (ui.newGameSettingsPanel != null && ui.newGameSettingsPanel.activeSelf))
            yield return new WaitForSeconds(0.3f);

        if (ui.winPanel != null && ui.winPanel.activeSelf) yield return StartCoroutine(AnimateWinUIExit(ui));
        else if (ui.defeatPanel != null && ui.defeatPanel.activeSelf)
        {
            TogglePanelAnimated(ui.defeatPanel, false);
            yield return new WaitForSeconds(0.3f);
        }

        if (undoManager != null) undoManager.ResetHistory();
        if (DealCacheSystem.Instance != null) DealCacheSystem.Instance.DiscardActiveDeal();

        if (exitAnimator != null && activeGameMode != null)
        {
            activeGameMode.IsInputAllowed = false;
            AbortActiveGameAnimations();
            bool cardsFallen = false;
            exitAnimator.PlayRestartSequence(() => { cardsFallen = true; });
            while (!cardsFallen) yield return null;
            activeGameMode.RestartGame();
        }
        else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void OnNewGameStartClicked()
    {
        PlayClickSound();
        GameSettings.IsTutorialMode = false;
        if (ActiveUI.newGameSettingsPanel != null) TogglePanelAnimated(ActiveUI.newGameSettingsPanel, false);
        StartCoroutine(RestartSequenceRoutine());
    }

    public void OnCloseNewGameSettingsClicked()
    {
        PlayClickSound();
        if (ActiveUI.newGameSettingsPanel != null) TogglePanelAnimated(ActiveUI.newGameSettingsPanel, false);
    }

    private void UpdateNewGameSettingsVisuals()
    {
        ApplyNewGameSettingsVisuals(landscapeUI);
        ApplyNewGameSettingsVisuals(portraitUI);
        UpdateXPPreviews(landscapeUI);
        UpdateXPPreviews(portraitUI);
    }

    private void UpdateXPPreviews(GameUIGroup ui)
    {
        if (activeGameMode == null) return;
        string variant = GameSettings.GetCurrentVariantString(activeGameMode.GameType);
        int currentLvl = 1;
        bool isPremium = false;

        if (StatisticsManager.Instance != null)
        {
            var data = StatisticsManager.Instance.GetGameGlobalStats(activeGameMode.GameName);
            if (data != null) currentLvl = data.currentLevel;
            isPremium = StatisticsManager.Instance.IsUserPremium;
        }

        int xpAmount = LevelingUtils.CalculateXP(activeGameMode.GameType, currentLvl, GameSettings.CurrentDifficulty, variant, isPremium);
        if (QuestManager.Instance != null && System.Enum.TryParse(activeGameMode.GameType.ToString(), out QuestCategory cat))
        {
            if (QuestManager.Instance.HasActiveXpBuff(cat)) xpAmount *= 2;
        }

        string coloredXP = $"<color=#FFC400>{xpAmount}</color>";
        string format = "{0} XP";
        if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
        {
            string loc = LocalizationManager.instance.GetLocalizedValue(xpPreviewLocKey);
            if (!string.IsNullOrEmpty(loc)) format = loc;
        }

        if (ui.xpPreviewText != null) { try { ui.xpPreviewText.text = string.Format(format, coloredXP); } catch { ui.xpPreviewText.text = $"{coloredXP} XP"; } }
        if (ui.newGameXPPreviewText != null) { try { ui.newGameXPPreviewText.text = string.Format(format, coloredXP); } catch { ui.newGameXPPreviewText.text = $"{coloredXP} XP"; } }
    }

    private void ApplyNewGameSettingsVisuals(GameUIGroup ui)
    {
        int diffIndex = (int)GameSettings.CurrentDifficulty;
        if (ui.newGameSettingsDiffButtons != null)
        {
            for (int i = 0; i < ui.newGameSettingsDiffButtons.Length; i++)
            {
                if (ui.newGameSettingsDiffButtons[i] != null)
                {
                    if (!ui.newGameSettingsDiffButtons[i].interactable) ui.newGameSettingsDiffButtons[i].image.color = buttonDisabledColor;
                    else SetButtonState(ui.newGameSettingsDiffButtons[i], i == diffIndex);
                }
            }
        }
        if (ui.newGameSettingsDrawButtons != null && ui.newGameSettingsDrawButtons.Length >= 2)
        {
            SetButtonState(ui.newGameSettingsDrawButtons[0], GameSettings.KlondikeDrawCount == 1);
            SetButtonState(ui.newGameSettingsDrawButtons[1], GameSettings.KlondikeDrawCount == 3);
        }
        if (ui.newGameSettingsSuitButtons != null && ui.newGameSettingsSuitButtons.Length >= 3)
        {
            SetButtonState(ui.newGameSettingsSuitButtons[0], GameSettings.SpiderSuitCount == 1);
            SetButtonState(ui.newGameSettingsSuitButtons[1], GameSettings.SpiderSuitCount == 2);
            SetButtonState(ui.newGameSettingsSuitButtons[2], GameSettings.SpiderSuitCount == 4);
        }
        if (ui.newGameSettingsRoundsButtons != null && ui.newGameSettingsRoundsButtons.Length >= 3)
        {
            SetButtonState(ui.newGameSettingsRoundsButtons[0], GameSettings.RoundsCount == 1);
            SetButtonState(ui.newGameSettingsRoundsButtons[1], GameSettings.RoundsCount == 2);
            SetButtonState(ui.newGameSettingsRoundsButtons[2], GameSettings.RoundsCount == 3);
        }

        SetButtonState(ui.newGameSettingsYukonClassicBtn, !GameSettings.YukonRussian);
        SetButtonState(ui.newGameSettingsYukonRussianBtn, GameSettings.YukonRussian);
        SetButtonState(ui.newGameSettingsMonteCarlo8Btn, !GameSettings.MonteCarlo4Ways);
        SetButtonState(ui.newGameSettingsMonteCarlo4Btn, GameSettings.MonteCarlo4Ways);
        SetButtonState(ui.newGameSettingsMontanaClassicBtn, !GameSettings.MontanaHard);
        SetButtonState(ui.newGameSettingsMontanaHardBtn, GameSettings.MontanaHard);
    }

    public void OnNewGameSettingsDiffClicked(int diffIndex) { PlayClickSound(); GameSettings.CurrentDifficulty = (Difficulty)diffIndex; UpdateNewGameSettingsVisuals(); }
    public void OnNewGameSettingsDrawClicked(int drawCount) { PlayClickSound(); GameSettings.KlondikeDrawCount = drawCount; UpdateNewGameSettingsVisuals(); }

    private IEnumerator AnimateWinUIExit(GameUIGroup ui)
    {
        Coroutine cardAnim = null;
        if (ui.winCardRect != null && ui.winCardRect.gameObject.activeSelf) cardAnim = StartCoroutine(AnimateCardExit(ui.winCardRect));
        if (ui.winPanel != null) TogglePanelAnimated(ui.winPanel, false);
        if (cardAnim != null) yield return cardAnim;
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator AnimateCardExit(RectTransform card)
    {
        var hover = card.GetComponent<CardHoverEffect>();
        if (hover != null) hover.SetSelectedMode(false);

        Vector2 startPos = card.anchoredPosition;
        Vector2 targetPos = winCardDefaultPos + new Vector2(1500f, 0f);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Whoosh_Out");

        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            card.anchoredPosition = Vector2.Lerp(startPos, targetPos, t * t);
            yield return null;
        }

        card.anchoredPosition = targetPos;
        card.gameObject.SetActive(false);
    }

    public void OnCancelNewGameClicked()
    {
        PlayClickSound();
        if (ActiveUI.newGameConfirmationPanel != null) TogglePanelAnimated(ActiveUI.newGameConfirmationPanel, false);
    }

    private IEnumerator AnimateCardEntrance(RectTransform card)
    {
        Vector2 targetPos = winCardDefaultPos;
        Vector2 startPos = winCardDefaultPos + new Vector2(1500f, 0f);
        card.anchoredPosition = startPos;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Whoosh_Out");

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - (elapsed / duration), 3);
            card.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        card.anchoredPosition = targetPos;

        var hover = card.GetComponent<CardHoverEffect>();
        if (hover != null) hover.SetSelectedMode(true);
    }

    private void UpdateWinPanelStats(int manualMoves, GameUIGroup ui)
    {
        if (ui.winDifficultyText)
        {
            string diffKey = GameSettings.IsTutorialMode ? "tutorial" : $"Diff{GameSettings.CurrentDifficulty}";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
                ui.winDifficultyText.text = LocalizationManager.instance.GetLocalizedValue(diffKey);
            else ui.winDifficultyText.text = GameSettings.IsTutorialMode ? "Обучение" : GameSettings.CurrentDifficulty.ToString();
        }

        bool isNewScoreRecord = false, isNewMovesRecord = false, isNewTimeRecord = false;
        if (StatisticsManager.Instance != null)
        {
            isNewScoreRecord = StatisticsManager.Instance.IsNewScoreRecord;
            isNewMovesRecord = StatisticsManager.Instance.IsNewMovesRecord;
            isNewTimeRecord = StatisticsManager.Instance.IsNewTimeRecord;
        }

        if (activeGameMode != null && ui.winScoreText)
        {
            int finalScore = GetScoreFromGameMode();
            ui.winScoreText.text = GetRecordText(finalScore.ToString(), isNewScoreRecord);
        }

        if (StatisticsManager.Instance != null)
        {
            int movesToShow = (manualMoves >= 0) ? manualMoves : StatisticsManager.Instance.GetCurrentMoves();
            if (ui.winMovesText) ui.winMovesText.text = GetRecordText(movesToShow.ToString(), isNewMovesRecord);
            float duration = StatisticsManager.Instance.LastGameTime;
            if (ui.winTimeText) ui.winTimeText.text = GetRecordText(FormatTime(duration), isNewTimeRecord);
            if (ui.winEarnedXPText) ui.winEarnedXPText.text = $"<color=#FFC400>{StatisticsManager.Instance.LastXPGained}</color>";
        }

        if (ui.winLevelBar != null && StatisticsManager.Instance != null)
        {
            string gameName = activeGameMode != null ? activeGameMode.GameName : "Unknown";
            StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);
            if (data != null)
            {
                int startXP = data.currentXP - StatisticsManager.Instance.LastXPGained;
                if (startXP < 0)
                {
                    int oldLevel = Mathf.Max(1, data.currentLevel - 1);
                    ui.winLevelBar.UpdateBar(oldLevel, (oldLevel * 500) + startXP, oldLevel * 500);
                }
                else
                {
                    ui.winLevelBar.UpdateBar(data.currentLevel, startXP, data.xpForNextLevel > 0 ? data.xpForNextLevel : 500);
                }
            }
        }

        bool isTutorial = GameSettings.IsTutorialMode;
        if (ui.tutorialBigMenuButton != null) ui.tutorialBigMenuButton.SetActive(isTutorial);
        if (ui.standardWinButtons != null)
            foreach (var btnObj in ui.standardWinButtons) if (btnObj != null) btnObj.SetActive(!isTutorial);
    }

    private string GetRecordText(string baseValue, bool isRecord)
    {
        if (!isRecord) return baseValue;
        string recordWord = "Новый рекорд!";
        if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
        {
            string loc = LocalizationManager.instance.GetLocalizedValue("NewRecord");
            if (!string.IsNullOrEmpty(loc)) recordWord = loc;
        }
        return $"<color=#FFD700>{baseValue} {recordWord}</color>";
    }

    private int GetScoreFromGameMode()
    {
        int finalScore = 0;
        var modeType = activeGameMode.GetType();
        var scoreProp = modeType.GetProperty("CurrentScore");
        if (scoreProp != null) finalScore = (int)scoreProp.GetValue(activeGameMode);
        else
        {
            var smField = modeType.GetField("scoreManager");
            if (smField != null)
            {
                var smObj = smField.GetValue(activeGameMode);
                if (smObj != null)
                {
                    var innerScore = smObj.GetType().GetProperty("Score") ?? smObj.GetType().GetProperty("CurrentScore");
                    if (innerScore != null) finalScore = (int)innerScore.GetValue(smObj);
                }
            }
        }
        return finalScore;
    }

    private IEnumerator AnimateXPBarDelayed(GameUIGroup ui)
    {
        yield return new WaitForSeconds(0.6f);
        if (ui.winLevelBar != null && StatisticsManager.Instance != null)
        {
            string gameName = activeGameMode != null ? activeGameMode.GameName : "Unknown";
            StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);
            if (data != null)
            {
                int startXP = data.currentXP - StatisticsManager.Instance.LastXPGained;
                int targetXP = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;

                if (startXP < 0)
                {
                    int oldLevel = Mathf.Max(1, data.currentLevel - 1);
                    StartCoroutine(CardLevelUpSequence(data, oldLevel, (oldLevel * 500) + startXP, oldLevel * 500, data.currentXP, targetXP, ui));
                }
                else
                {
                    StartCoroutine(PlayDynamicXPSound(1.0f));
                    ui.winLevelBar.AnimateBar(data.currentLevel, startXP, data.currentXP, targetXP);
                }
            }
        }
    }

    private IEnumerator CardLevelUpSequence(StatData data, int oldLevel, int oldXPStart, int oldTarget, int currentXP, int targetXP, GameUIGroup ui)
    {
        StartCoroutine(PlayDynamicXPSound(1.5f));
        ui.winLevelBar.AnimateBar(oldLevel, oldXPStart, oldTarget, oldTarget);
        yield return new WaitForSeconds(1.5f);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Level_Up");
            AudioManager.Instance.PlaySound("Card_Whoosh_In");
        }

        Vector3 originalScale = ui.winCardRect.localScale;
        Vector3 targetScale = originalScale * 1.25f;

        float elapsed = 0f, animDuration = 1.5f, overshoot = 1.70158f;
        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / animDuration;
            float t_minus = t - 1f;
            float easeRotate = 1f + (overshoot + 1f) * Mathf.Pow(t_minus, 3) + overshoot * Mathf.Pow(t_minus, 2);
            float easeScale = 1f - Mathf.Pow(1f - t, 3);

            ui.winCardRect.localScale = Vector3.Lerp(originalScale, targetScale, easeScale);
            ui.winCardRect.localEulerAngles = new Vector3(0f, 0f, -360f * easeRotate);
            yield return null;
        }
        ui.winCardRect.localScale = targetScale;
        ui.winCardRect.localEulerAngles = Vector3.zero;

        yield return new WaitForSeconds(0.3f);
        ui.winLevelBar.AnimateBar(oldLevel, oldTarget, 0, oldTarget);
        yield return new WaitForSeconds(0.4f);

        ui.winLevelBar.UpdateBar(data.currentLevel, 0, targetXP);

        elapsed = 0f;
        while (elapsed < 0.6f)
        {
            elapsed += Time.unscaledDeltaTime;
            ui.winCardRect.localScale = Vector3.Lerp(targetScale, originalScale, Mathf.SmoothStep(0f, 1f, elapsed / 0.6f));
            yield return null;
        }
        ui.winCardRect.localScale = originalScale;

        if (currentXP > 0) StartCoroutine(PlayDynamicXPSound(0.6f));
        ui.winLevelBar.AnimateBar(data.currentLevel, 0, currentXP, targetXP);
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60F);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60F);
        return string.Format("{0}:{1:00}", minutes, seconds);
    }

    private void TogglePanelAnimated(GameObject panel, bool show)
    {
        if (panel == null) return;
        if (AudioManager.Instance != null)
        {
            if (show) AudioManager.Instance.PlaySound("Panel_Slide_In");
            else AudioManager.Instance.PlaySound("Panel_Slide_Out");
        }
        if (show)
        {
            panel.SetActive(true);
            StartCoroutine(AnimatePanelRoutine(panel, true));
        }
        else
        {
            StartCoroutine(AnimatePanelRoutine(panel, false));
        }
    }

    private IEnumerator AnimatePanelRoutine(GameObject panel, bool show)
    {
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (!rt) yield break;

        Vector2 targetPos = Vector2.zero;
        if (panelInitialPositions.ContainsKey(panel))
        {
            targetPos = panelInitialPositions[panel];
        }

        float offScreenX = -2500f;
        Vector2 offScreenPos = new Vector2(offScreenX, targetPos.y);

        Vector2 startPos = show ? offScreenPos : rt.anchoredPosition;
        Vector2 endPos = show ? targetPos : offScreenPos;

        if (show) rt.anchoredPosition = startPos;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, 1f - Mathf.Pow(1f - (elapsed / duration), 3));
            yield return null;
        }

        rt.anchoredPosition = endPos;
        if (!show) panel.SetActive(false);
    }

    private void HandleLevelUp(string context, int newLevel) { if (context == "Account" && globalLevelUpPopup) globalLevelUpPopup.ShowNotification(newLevel); }

    public void OnStatisticsClicked()
    {
        PlayClickSound();
        if (activeGameMode == null) return;

        bool isPremium = false;
        if (StatisticsManager.Instance != null)
        {
            isPremium = StatisticsManager.Instance.IsUserPremium;
        }

        GameUIGroup ui = ActiveUI;
        GameObject panelToShow = isPremium ? ui.premiumStatisticsPanel : ui.basicStatisticsPanel;

        if (panelToShow != null)
        {
            if (isPremium)
            {
                var statsUI = panelToShow.GetComponent<StatisticsUI>();
                if (statsUI != null) statsUI.ShowStatsForGame(activeGameMode.GameType);
            }
            else
            {
                var basicStatsUI = panelToShow.GetComponent<BasicStatisticsUI>();
                if (basicStatsUI != null) basicStatsUI.ShowStatsForGame(activeGameMode.GameType);
            }

            TogglePanelAnimated(panelToShow, true);
        }
    }

    public void OnCloseStatisticsClicked()
    {
        PlayClickSound();
        if (ActiveUI.basicStatisticsPanel != null && ActiveUI.basicStatisticsPanel.activeSelf) TogglePanelAnimated(ActiveUI.basicStatisticsPanel, false);
        if (ActiveUI.premiumStatisticsPanel != null && ActiveUI.premiumStatisticsPanel.activeSelf) TogglePanelAnimated(ActiveUI.premiumStatisticsPanel, false);
    }

    private void PlayClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Click");
    }

    private IEnumerator PlayDynamicXPSound(float duration, float delay = 0.05f)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (AudioManager.Instance == null) yield break;

        AudioSource source = AudioManager.Instance.PlaySound("XP_Gain");
        if (source == null) yield break;

        float elapsed = 0f;
        float baseVolume = source.volume;
        float basePitch = source.pitch;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            source.pitch = basePitch + (t * 0.4f);

            float speedFactor = 4f * t * (1f - t);
            source.volume = baseVolume * Mathf.Lerp(0.0f, 1.0f, speedFactor);

            yield return null;
        }

        float fadeOut = 0.2f;
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeOut)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(baseVolume * 0.3f, 0f, fadeElapsed / fadeOut);
            yield return null;
        }

        source.Stop();

        source.volume = baseVolume;
        source.pitch = basePitch;
    }
    // [NEW] Метод, который перекидывает видимость всех панелей при повороте
    private void HandleOrientationChange(bool isPortrait)
    {
        GameUIGroup oldUI = isPortrait ? landscapeUI : portraitUI;
        GameUIGroup newUI = isPortrait ? portraitUI : landscapeUI;

        SwapPanel(oldUI.winPanel, newUI.winPanel);
        SwapPanel(oldUI.defeatPanel, newUI.defeatPanel);
        SwapPanel(oldUI.basicStatisticsPanel, newUI.basicStatisticsPanel);
        SwapPanel(oldUI.premiumStatisticsPanel, newUI.premiumStatisticsPanel);
        SwapPanel(oldUI.settingsPanel, newUI.settingsPanel);
        SwapPanel(oldUI.newGameSettingsPanel, newUI.newGameSettingsPanel);
        SwapPanel(oldUI.exitConfirmationPanel, newUI.exitConfirmationPanel);
        SwapPanel(oldUI.newGameConfirmationPanel, newUI.newGameConfirmationPanel);

        // Синхронизируем карту победы, если она активна
        if (oldUI.winCardRect != null && newUI.winCardRect != null)
        {
            bool cardActive = oldUI.winCardRect.gameObject.activeSelf;

            // Выключаем эффект на старой карте перед её скрытием
            var oldHover = oldUI.winCardRect.GetComponent<CardHoverEffect>();
            if (oldHover != null) oldHover.SetSelectedMode(false);

            oldUI.winCardRect.gameObject.SetActive(false);

            if (cardActive)
            {
                newUI.winCardRect.gameObject.SetActive(true);
                newUI.winCardRect.anchoredPosition = winCardDefaultPos;

                // Включаем эффект левитации на новой карте
                var newHover = newUI.winCardRect.GetComponent<CardHoverEffect>();
                if (newHover != null) newHover.SetSelectedMode(true);
            }
        }
    }

    // [NEW] Вспомогательный метод: выключает старую панель и мгновенно ставит новую в центр
    private void SwapPanel(GameObject oldPanel, GameObject newPanel)
    {
        if (oldPanel != null && oldPanel.activeSelf)
        {
            oldPanel.SetActive(false);
            if (newPanel != null)
            {
                newPanel.SetActive(true);
                RectTransform rt = newPanel.GetComponent<RectTransform>();
                if (rt != null && panelInitialPositions.ContainsKey(newPanel))
                {
                    rt.anchoredPosition = panelInitialPositions[newPanel];
                }
            }
        }
    }
    public void OnUndoOneClicked()
    {
        PlayClickSound();
        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowRewarded("undo_one");
        }
        else
        {
            ExecuteUndoOne();
        }
    }

    public void OnUndoAllClicked()
    {
        PlayClickSound();
        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowRewarded("undo_all");
        }
        else
        {
            ExecuteUndoAll();
        }
    }

    private void ExecuteUndoOne()
    {
        if (ActiveUI.defeatPanel != null && ActiveUI.defeatPanel.activeSelf) TogglePanelAnimated(ActiveUI.defeatPanel, false);

        if (activeGameMode != null) activeGameMode.IsInputAllowed = true;

        if (undoManager != null && undoManager.undoButton != null && undoManager.undoButton.interactable) undoManager.undoButton.onClick.Invoke();
        else if (activeGameMode != null)
        {
            activeGameMode.OnUndoAction();
        }
    }

    private void ExecuteUndoAll()
    {
        if (ActiveUI.defeatPanel != null && ActiveUI.defeatPanel.activeSelf) TogglePanelAnimated(ActiveUI.defeatPanel, false);

        if (activeGameMode != null) activeGameMode.IsInputAllowed = true;

        if (undoManager != null && undoManager.undoAllButton != null && undoManager.undoAllButton.interactable) undoManager.undoAllButton.onClick.Invoke();
        else if (activeGameMode != null)
        {
            var type = activeGameMode.GetType();
            var method = type.GetMethod("OnUndoAllAction");
            if (method != null) method.Invoke(activeGameMode, null);
        }
    }

}