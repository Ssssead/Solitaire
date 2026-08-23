using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
using System.Collections;

public class MenuController : MonoBehaviour
{
    public static MenuController Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // --- ВЫПОЛНЯЕТСЯ ДО ОТРИСОВКИ ПЕРВОГО КАДРА ---

        // 1. Определяем ориентацию сразу
        isPortrait = Screen.width < Screen.height;
        isSettingsMode = false;

        // 2. Прячем все панели настроек мгновенно
        if (landscapeSettings != null && landscapeSettings.panel) landscapeSettings.panel.SetActive(false);
        if (portraitSettings != null && portraitSettings.panel) portraitSettings.panel.SetActive(false);

        CloseAllOverlaysInstant();

        // 3. Расставляем верхние кнопки по якорям до появления на экране
        SnapButtonToTarget(leaderboardBtn);
        SnapButtonToTarget(statsBtn);
        foreach (var el in otherResponsiveElements)
        {
            SnapSimpleElementToTarget(el);
        }
    }

    

    [System.Serializable]
    public struct GameDefinition
    {
        public string name;
        public GameType type;
        public string sceneName;

        [Header("UI Options")]
        public bool showDifficulty;
        public bool showSuitSelector;
        public bool showRoundsSelector;

        [Space]
        public bool showDrawMode;
        public bool showYukonModes;
        public bool showMonteCarloModes;
        public bool showMontanaModes;
        public bool showNoOptionsPanel;
    }

    // ==========================================
    // НОВАЯ СТРУКТУРА ДЛЯ ПАНЕЛЕЙ НАСТРОЕК
    // ==========================================
    [System.Serializable]
    public class SettingsUIGroup
    {
        public GameObject panel;
        public SettingsPanelAnimator animator;

        [Header("Texts")]
        public TMP_Text xpPreviewText;

        [Header("Containers")]
        public GameObject difficultyContainer;
        public GameObject suitSelectionContainer;
        public GameObject roundsSelectionContainer;
        public GameObject drawModeContainer;
        public GameObject yukonModeContainer;
        public GameObject monteCarloModeContainer;
        public GameObject montanaModeContainer;
        public GameObject noOptionsContainer;

        [Header("Action Buttons")]
        public Button startButton;
        public Button tutorialButton;

        [Header("Option Buttons")]
        public Button draw1Button;
        public Button draw3Button;
        public Button yukonClassicButton;
        public Button yukonRussianButton;
        public Button monteCarlo8WaysButton;
        public Button monteCarlo4WaysButton;
        public Button montanaClassicButton;
        public Button montanaHardButton;

        [Header("Arrays")]
        public Button[] diffButtons;
        public Button[] suitButtons;
        public Button[] roundsButtons;
    }

    [Header("Game Definitions")]
    public List<GameDefinition> games;

    [Header("UI Panels")]
    public GameObject mainSelectionPanel;

    [Header("Game Settings Panels (Dual Orientation)")]
    public SettingsUIGroup landscapeSettings;
    public SettingsUIGroup portraitSettings;

    [System.Serializable]
    public class StatsUIGroup
    {
        public GameObject appBasicPanel;
        public GameObject appPremiumPanel;
        public GameObject gameBasicPanel;
        public GameObject gamePremiumPanel;
    }

    [Header("Stats Panels (Dual Orientation)")]
    public StatsUIGroup landscapeStats;
    public StatsUIGroup portraitStats;

    [Header("Controllers")]
    public MenuLevelController levelController;
    public CardAnimationController cardAnimator;
    public MenuExitController exitController;
    public DynamicLeaderboardController dynamicLeaderboard;

    [Header("Menu Overlays")]

    public GameObject landscapeLeaderboardPanel;
    public GameObject portraitLeaderboardPanel;
    public GameObject landscapeShopPanel;
    public GameObject portraitShopPanel;
    public GameObject landscapeDailyQuestsPanel;
    public GameObject portraitDailyQuestsPanel;

    [Header("XP Preview Loc Key")]
    public string xpPreviewLocKey = "xp_reward_preview";

    // --- ДИНАМИЧЕСКИЕ КНОПКИ ---
    [System.Serializable]
    public class ResponsiveButtonDef
    {
        public RectTransform button;
        [Header("Landscape Anchors")]
        public RectTransform landscapeStart;
        public RectTransform landscapeEnd;
        [Header("Portrait Anchors")]
        public RectTransform portraitStart;
        public RectTransform portraitEnd;

        public RectTransform GetTarget(bool isPortrait, bool isSettingsMode)
        {
            if (isPortrait) return isSettingsMode ? portraitEnd : portraitStart;
            return isSettingsMode ? landscapeEnd : landscapeStart;
        }
    }

    [System.Serializable]
    public class SimpleResponsiveDef
    {
        public RectTransform uiElement;
        [Header("Anchors")]
        public RectTransform landscapeAnchor;
        public RectTransform portraitAnchor;

        public RectTransform GetTarget(bool isPortrait)
        {
            return isPortrait ? portraitAnchor : landscapeAnchor;
        }
    }

    [Header("Dynamic Buttons (Responsive)")]
    public ResponsiveButtonDef leaderboardBtn;
    public ResponsiveButtonDef statsBtn;

    [Header("Other Responsive UI (Player Panel, Settings, etc.)")]
    public List<SimpleResponsiveDef> otherResponsiveElements;

    private Coroutine buttonsMoveCoroutine;
    private bool isSettingsMode = false;
    private bool isPortrait;

    [Header("Visual Settings (Background)")]
    public Color bgSelectedColor = new Color32(255, 176, 26, 255);
    public Color bgNormalColor = new Color32(154, 95, 64, 255);
    public Color bgDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Visual Settings (Text)")]
    public Color textSelectedColor = new Color32(36, 20, 12, 255);
    public Color textNormalColor = new Color32(192, 192, 192, 255);
    public Color buttonDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    private GameDefinition currentGame;

    [Header("Overlay Animations (Horizontal)")]
    public float landscapeOverlayDuration = 0.4f;
    public float portraitOverlayDuration = 0.3f; // Быстрее
    public float landscapeOverlayFlyDistanceX = 2500f;
    public float portraitOverlayFlyDistanceX = 3000f; // Дальше

    [System.Serializable]
    public class GlobalSettingsUIGroup
    {
        public GameObject panel;

        [Header("Audio Settings UI")]
        public Button soundOnButton;
        public Button soundOffButton;

        [Header("Language Settings UI")]
        public Button[] languageButtons;

        [Header("Click Mode Settings UI")]
        public Button singleClickButton;
        public Button doubleClickButton;
    }

    [Header("Global Settings Panels (Dual Orientation)")]
    public GlobalSettingsUIGroup landscapeGlobalSettings;
    public GlobalSettingsUIGroup portraitGlobalSettings;
    public string[] languageCodes = new string[] { "ru", "en", "tr", "es", "pt" };

    

    [Header("Button Animation Settings")]
    public float buttonAnimDuration = 0.15f;
    public Vector3 buttonActiveScale = new Vector3(1.05f, 1.05f, 1f);

    // ==========================================
    // СТРУКТУРА ДЛЯ ПАНЕЛЕЙ КАСТОМИЗАЦИИ
    // ==========================================
    [System.Serializable]
    public class CustomizationUIGroup
    {
        public GameObject backgroundSelectionPanel;
        public GameObject cardAppearancePanel;
        public GameObject cardBackSelectionPanel;
        public GameObject deckSelectionPanel;
    }

    [Header("Customization Panels (Dual Orientation)")]
    public CustomizationUIGroup landscapeCustomization;
    public CustomizationUIGroup portraitCustomization;

    [Header("Customization Animation Settings")]
    public float landscapeVerticalFlyDistance = 1500f;
    public float portraitVerticalFlyDistance = 3500f;

    private Dictionary<RectTransform, Coroutine> buttonScaleCoroutines = new Dictionary<RectTransform, Coroutine>();
    private Dictionary<GameObject, Vector2> panelInitialPositions = new Dictionary<GameObject, Vector2>();
    private Dictionary<GameObject, Coroutine> activePanelCoroutines = new Dictionary<GameObject, Coroutine>();

    private void Start()
    {
        // Оставляем в Start только включение главного меню и регистрацию позиций для анимаций
        if (mainSelectionPanel) mainSelectionPanel.SetActive(true);

        RegisterOverlay(landscapeGlobalSettings.panel);
        RegisterOverlay(portraitGlobalSettings.panel);

        RegisterOverlay(landscapeStats.appBasicPanel);
        RegisterOverlay(landscapeStats.appPremiumPanel);
        RegisterOverlay(landscapeStats.gameBasicPanel);
        RegisterOverlay(landscapeStats.gamePremiumPanel);

        RegisterOverlay(portraitStats.appBasicPanel);
        RegisterOverlay(portraitStats.appPremiumPanel);
        RegisterOverlay(portraitStats.gameBasicPanel);
        RegisterOverlay(portraitStats.gamePremiumPanel);

        RegisterOverlay(landscapeLeaderboardPanel);
        RegisterOverlay(portraitLeaderboardPanel);
        RegisterOverlay(landscapeShopPanel);
        RegisterOverlay(portraitShopPanel);
        RegisterOverlay(landscapeDailyQuestsPanel);
        RegisterOverlay(portraitDailyQuestsPanel);

        RegisterOverlay(landscapeCustomization.backgroundSelectionPanel);
        RegisterOverlay(landscapeCustomization.cardAppearancePanel);
        RegisterOverlay(landscapeCustomization.cardBackSelectionPanel);
        RegisterOverlay(landscapeCustomization.deckSelectionPanel);

        RegisterOverlay(portraitCustomization.backgroundSelectionPanel);
        RegisterOverlay(portraitCustomization.cardAppearancePanel);
        RegisterOverlay(portraitCustomization.cardBackSelectionPanel);
        RegisterOverlay(portraitCustomization.deckSelectionPanel);

        UpdateSoundButtonsVisuals(true);
        UpdateLanguageButtonsVisuals(true);
        UpdateClickModeButtonsVisuals(true);
    }

    private void SnapButtonToTarget(ResponsiveButtonDef btnDef)
    {
        if (btnDef.button == null) return;
        RectTransform target = btnDef.GetTarget(isPortrait, isSettingsMode);
        if (target != null)
        {
            btnDef.button.SetParent(target, false);
            SetAsStretchChild(btnDef.button);
        }
    }
    private void SnapSimpleElementToTarget(SimpleResponsiveDef def)
    {
        if (def.uiElement == null) return;
        RectTransform target = def.GetTarget(isPortrait);
        if (target != null)
        {
            def.uiElement.SetParent(target, false);
            SetAsStretchChild(def.uiElement);
        }
    }

    private void SetAsStretchChild(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private void SetAsFixedCenterAnchor(RectTransform rt, Vector2 currentAbsoluteSize)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = currentAbsoluteSize;
    }

    private void OnEnable() => LocalizationManager.OnLocalizationLoaded += OnLocalizationChanged;
    private void OnDisable() => LocalizationManager.OnLocalizationLoaded -= OnLocalizationChanged;

    private void Update()
    {
        bool checkPortrait = Screen.width < Screen.height;
        if (checkPortrait != isPortrait)
        {
            HandleOrientationChange(checkPortrait);
        }
    }

    // --- ЛОГИКА ПОВОРОТА ЭКРАНА ---
    private void HandleOrientationChange(bool newIsPortrait)
    {
        bool wasSettingsOpen = IsSettingsPanelOpen();
        bool wasGlobalSettingsOpen = IsGlobalSettingsOpen();

        // 1. Прячем старые панели
        if (wasSettingsOpen)
        {
            if (landscapeSettings.panel) landscapeSettings.panel.SetActive(false);
            if (portraitSettings.panel) portraitSettings.panel.SetActive(false);
        }
        if (wasGlobalSettingsOpen)
        {
            if (landscapeGlobalSettings.panel) landscapeGlobalSettings.panel.SetActive(false);
            if (portraitGlobalSettings.panel) portraitGlobalSettings.panel.SetActive(false);
        }
        bool wasBgOpen = IsPanelOpen(landscapeCustomization.backgroundSelectionPanel, portraitCustomization.backgroundSelectionPanel);
        bool wasAppOpen = IsPanelOpen(landscapeCustomization.cardAppearancePanel, portraitCustomization.cardAppearancePanel);
        bool wasBackOpen = IsPanelOpen(landscapeCustomization.cardBackSelectionPanel, portraitCustomization.cardBackSelectionPanel);
        bool wasDeckOpen = IsPanelOpen(landscapeCustomization.deckSelectionPanel, portraitCustomization.deckSelectionPanel);
        // ДОБАВЛЕНА ЭТА СТРОКА:
        bool wasShopOpen = IsPanelOpen(landscapeShopPanel, portraitShopPanel);
        bool wasLbOpen = IsPanelOpen(landscapeLeaderboardPanel, portraitLeaderboardPanel);
        bool wasAppBasicOpen = IsPanelOpen(landscapeStats.appBasicPanel, portraitStats.appBasicPanel);
        bool wasAppPremiumOpen = IsPanelOpen(landscapeStats.appPremiumPanel, portraitStats.appPremiumPanel);
        bool wasGameBasicOpen = IsPanelOpen(landscapeStats.gameBasicPanel, portraitStats.gameBasicPanel);
        bool wasGamePremiumOpen = IsPanelOpen(landscapeStats.gamePremiumPanel, portraitStats.gamePremiumPanel);
        bool wasQuestsOpen = IsPanelOpen(landscapeDailyQuestsPanel, portraitDailyQuestsPanel);
        isPortrait = newIsPortrait;
        MoveButtonsToTarget(isSettingsMode);
        SwapCustomizationPanel(landscapeCustomization.backgroundSelectionPanel, portraitCustomization.backgroundSelectionPanel, wasBgOpen);
        SwapCustomizationPanel(landscapeCustomization.cardAppearancePanel, portraitCustomization.cardAppearancePanel, wasAppOpen);
        SwapCustomizationPanel(landscapeCustomization.cardBackSelectionPanel, portraitCustomization.cardBackSelectionPanel, wasBackOpen);
        SwapCustomizationPanel(landscapeCustomization.deckSelectionPanel, portraitCustomization.deckSelectionPanel, wasDeckOpen);
        SwapStatsPanel(landscapeStats.appBasicPanel, portraitStats.appBasicPanel, wasAppBasicOpen, false);
        SwapStatsPanel(landscapeStats.appPremiumPanel, portraitStats.appPremiumPanel, wasAppPremiumOpen, false);
        SwapStatsPanel(landscapeStats.gameBasicPanel, portraitStats.gameBasicPanel, wasGameBasicOpen, true);
        SwapStatsPanel(landscapeStats.gamePremiumPanel, portraitStats.gamePremiumPanel, wasGamePremiumOpen, true);
        SwapCustomizationPanel(landscapeShopPanel, portraitShopPanel, wasShopOpen);
        SwapCustomizationPanel(landscapeLeaderboardPanel, portraitLeaderboardPanel, wasLbOpen);
        SwapCustomizationPanel(landscapeDailyQuestsPanel, portraitDailyQuestsPanel, wasQuestsOpen);
        // 2. Показываем новые
        if (wasSettingsOpen)
        {
            SetupSettingsPanel();
            UpdateXPPreview();
            SettingsUIGroup activeGroup = isPortrait ? portraitSettings : landscapeSettings;
            if (activeGroup.animator != null) activeGroup.animator.AnimateOpen();
            else if (activeGroup.panel != null) activeGroup.panel.SetActive(true);
        }

        if (wasGlobalSettingsOpen)
        {
            GameObject activeGlobalPanel = isPortrait ? portraitGlobalSettings.panel : landscapeGlobalSettings.panel;
            if (activeGlobalPanel) activeGlobalPanel.SetActive(true);

            // Если вы используете корутины анимаций оверлеев, фиксируем их якоря сразу, чтобы не сломался вылет
            RectTransform rt = activeGlobalPanel.GetComponent<RectTransform>();
            if (rt != null && panelInitialPositions.ContainsKey(activeGlobalPanel))
                rt.anchoredPosition = panelInitialPositions[activeGlobalPanel];
        }
    }
    private void SwapCustomizationPanel(GameObject landscapePanel, GameObject portraitPanel, bool wasOpen)
    {
        if (!wasOpen) return;
        if (landscapePanel) landscapePanel.SetActive(false);
        if (portraitPanel) portraitPanel.SetActive(false);

        GameObject activePanel = isPortrait ? portraitPanel : landscapePanel;
        if (activePanel)
        {
            activePanel.SetActive(true);
            RectTransform rt = activePanel.GetComponent<RectTransform>();
            if (rt != null && panelInitialPositions.ContainsKey(activePanel))
                rt.anchoredPosition = panelInitialPositions[activePanel];
        }
    }
    private void SwapStatsPanel(GameObject landscapePanel, GameObject portraitPanel, bool wasOpen, bool isGameSpecific)
    {
        if (!wasOpen) return;
        if (landscapePanel) landscapePanel.SetActive(false);
        if (portraitPanel) portraitPanel.SetActive(false);

        GameObject activePanel = isPortrait ? portraitPanel : landscapePanel;
        if (activePanel)
        {
            if (isGameSpecific)
            {
                // Игровой статистике нужно передать текущую игру, чтобы она отрисовала графики
                var premiumUI = activePanel.GetComponent<StatisticsUI>();
                if (premiumUI != null) premiumUI.ShowStatsForGame(currentGame.type);

                var basicUI = activePanel.GetComponent<BasicStatisticsUI>();
                if (basicUI != null) basicUI.ShowStatsForGame(currentGame.type);
            }
            else
            {
                // Глобальная статистика обновляется сама в OnEnable
                activePanel.SetActive(true);
            }

            RectTransform rt = activePanel.GetComponent<RectTransform>();
            if (rt != null && panelInitialPositions.ContainsKey(activePanel))
                rt.anchoredPosition = panelInitialPositions[activePanel];
        }
    }
    private bool IsPanelOpen(GameObject p1, GameObject p2)
    {
        return (p1 != null && p1.activeSelf) || (p2 != null && p2.activeSelf);
    }
    private bool IsSettingsPanelOpen()
    {
        return (landscapeSettings.panel != null && landscapeSettings.panel.activeSelf) ||
               (portraitSettings.panel != null && portraitSettings.panel.activeSelf);
    }

    private void OnLocalizationChanged()
    {
        UpdateLanguageButtonsVisuals(true);
    }

    private void RegisterOverlay(GameObject panel)
    {
        if (panel == null) return;
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt != null && !panelInitialPositions.ContainsKey(panel))
        {
            panelInitialPositions.Add(panel, rt.anchoredPosition);
        }
    }

    // ==========================================
    // ЛОГИКА ИНТЕРФЕЙСА (СИНХРОНИЗАЦИЯ 2 ПАНЕЛЕЙ)
    // ==========================================

    private void UpdateXPPreview()
    {
        string variant = GameSettings.GetCurrentVariantString(currentGame.type);
        int currentLvl = 1;
        if (StatisticsManager.Instance != null)
        {
            var data = StatisticsManager.Instance.GetGameGlobalStats(currentGame.type.ToString());
            if (data != null) currentLvl = data.currentLevel;
        }

        bool isPremium = StatisticsManager.Instance != null && StatisticsManager.Instance.IsUserPremium;
        int xpAmount = LevelingUtils.CalculateXP(currentGame.type, currentLvl, GameSettings.CurrentDifficulty, variant, isPremium);

        if (QuestManager.Instance != null && System.Enum.TryParse(currentGame.type.ToString(), out QuestCategory cat))
        {
            if (QuestManager.Instance.HasActiveXpBuff(cat)) xpAmount *= 2;
        }

        string coloredXP = $"<color=#FFC400>{xpAmount}</color>";
        string format = LocalizationManager.instance != null && LocalizationManager.instance.IsReady()
            ? LocalizationManager.instance.GetLocalizedValue(xpPreviewLocKey)
            : "{0} XP";

        if (string.IsNullOrEmpty(format)) format = "{0} XP";

        string finalText = "";
        try { finalText = string.Format(format, coloredXP); }
        catch { finalText = $"{coloredXP} XP"; }

        // Обновляем текст в обеих панелях
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            if (group.xpPreviewText != null) group.xpPreviewText.text = finalText;
        }

        if (levelController != null) levelController.ShowXPGainPreview(currentGame.type, xpAmount);
    }

    public void OnGameSelected(int gameIndex)
    {
        if (gameIndex < 0 || gameIndex >= games.Count) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Card_Select");
            AudioManager.Instance.PlaySoundWithAutoFade("Panel_Slide_In", 0.25f, 0.15f);
        }

        CloseAllOverlaysAnimated();

        currentGame = games[gameIndex];
        GameSettings.CurrentGameType = currentGame.type;

        // Включаем кнопки старта в обеих панелях
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            SetButtonState(group.startButton, true);
            if (group.startButton) group.startButton.interactable = true;
            if (group.tutorialButton) group.tutorialButton.interactable = true;
        }

        ResetGameSettingsDefault();
        SetupSettingsPanel();
        UpdateXPPreview();

        MoveButtonsToTarget(true);

        if (cardAnimator != null) cardAnimator.SelectCard(currentGame.type);

        SettingsUIGroup activeGroup = isPortrait ? portraitSettings : landscapeSettings;

        if (activeGroup.animator != null)
        {
            if (activeGroup.animator.IsOpen())
            {
                activeGroup.animator.AnimateSwitch(() =>
                {
                    SetupSettingsPanel();
                    if (levelController != null) levelController.UpdatePreviewBar(currentGame.type);
                    UpdateXPPreview();
                });
            }
            else
            {
                if (levelController != null) levelController.UpdatePreviewBar(currentGame.type);
                activeGroup.animator.AnimateOpen();
                UpdateXPPreview();
            }
        }
        else
        {
            if (activeGroup.panel) activeGroup.panel.SetActive(true);
            SetupSettingsPanel();
            if (levelController != null) levelController.UpdatePreviewBar(currentGame.type);
            UpdateXPPreview();
        }
    }

    private void ResetGameSettingsDefault()
    {
        GameSettings.CurrentDifficulty = Difficulty.Medium;
        GameSettings.RoundsCount = 1;
        GameSettings.KlondikeDrawCount = 1;
        GameSettings.SpiderSuitCount = 1;
        GameSettings.YukonRussian = false;
        GameSettings.MonteCarlo4Ways = false;
        GameSettings.MontanaHard = false;

        if (currentGame.type == GameType.Spider || currentGame.type == GameType.Octagon)
        {
            if (GameSettings.CurrentDifficulty != Difficulty.Medium)
                GameSettings.CurrentDifficulty = Difficulty.Medium;
        }
        else
        {
            foreach (var group in new[] { landscapeSettings, portraitSettings })
            {
                if (group.diffButtons != null)
                {
                    foreach (var btn in group.diffButtons) if (btn) btn.interactable = true;
                }
            }
            UpdateDifficultyVisuals();
        }
    }

    public void OnBackClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Back");
            AudioManager.Instance.PlaySoundWithAutoFade("Panel_Slide_Out", 0.25f, 0.15f);
        }

        SettingsUIGroup activeGroup = isPortrait ? portraitSettings : landscapeSettings;

        if (activeGroup.animator != null) activeGroup.animator.AnimateClose();
        else if (activeGroup.panel != null) activeGroup.panel.SetActive(false);

        if (cardAnimator != null) cardAnimator.ResetGrid();
        if (levelController != null) levelController.HideAllPreviews();

        MoveButtonsToTarget(false);
    }

    private void SetupSettingsPanel()
    {
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            SetContainerActive(group.difficultyContainer, currentGame.showDifficulty);
            SetContainerActive(group.suitSelectionContainer, currentGame.showSuitSelector);
            SetContainerActive(group.roundsSelectionContainer, currentGame.showRoundsSelector);
            SetContainerActive(group.drawModeContainer, currentGame.showDrawMode);
            SetContainerActive(group.yukonModeContainer, currentGame.showYukonModes);
            SetContainerActive(group.monteCarloModeContainer, currentGame.showMonteCarloModes);
            SetContainerActive(group.montanaModeContainer, currentGame.showMontanaModes);
            SetContainerActive(group.noOptionsContainer, currentGame.showNoOptionsPanel);
        }

        UpdateDifficultyVisuals();

        if (currentGame.showDrawMode) UpdateDrawModeVisuals();
        if (currentGame.showSuitSelector) UpdateSuitsVisuals(GameSettings.SpiderSuitCount);
        if (currentGame.showRoundsSelector) UpdateRoundsVisuals();
        if (currentGame.showYukonModes) UpdateYukonVisuals();
        if (currentGame.showMonteCarloModes) UpdateMonteCarloVisuals();
        if (currentGame.showMontanaModes) UpdateMontanaVisuals();

        if (currentGame.type == GameType.Spider) ValidateSpiderConstraints(GameSettings.SpiderSuitCount);
        else if (currentGame.type == GameType.Octagon) ValidateOctagonConstraints();
        else
        {
            foreach (var group in new[] { landscapeSettings, portraitSettings })
            {
                if (group.diffButtons != null)
                {
                    foreach (var btn in group.diffButtons) if (btn) btn.interactable = true;
                }
            }
            UpdateDifficultyVisuals();
        }
    }

    private void SetContainerActive(GameObject container, bool active)
    {
        if (container != null) container.SetActive(active);
    }

    public void OnDrawModeClicked(int count)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameSettings.KlondikeDrawCount = count;
        UpdateDrawModeVisuals();
        UpdateXPPreview();
    }

    private void UpdateDrawModeVisuals()
    {
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            SetButtonState(group.draw1Button, GameSettings.KlondikeDrawCount == 1);
            SetButtonState(group.draw3Button, GameSettings.KlondikeDrawCount == 3);
        }
    }

    public void OnYukonModeClicked(int mode)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameSettings.YukonRussian = (mode == 1);
        UpdateYukonVisuals();
        UpdateXPPreview();
    }

    private void UpdateYukonVisuals()
    {
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            SetButtonState(group.yukonClassicButton, !GameSettings.YukonRussian);
            SetButtonState(group.yukonRussianButton, GameSettings.YukonRussian);
        }
    }

    public void OnMonteCarloModeClicked(int mode)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameSettings.MonteCarlo4Ways = (mode == 1);
        UpdateMonteCarloVisuals();
        UpdateXPPreview();
    }

    private void UpdateMonteCarloVisuals()
    {
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            SetButtonState(group.monteCarlo8WaysButton, !GameSettings.MonteCarlo4Ways);
            SetButtonState(group.monteCarlo4WaysButton, GameSettings.MonteCarlo4Ways);
        }
    }

    public void OnMontanaModeClicked(int mode)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameSettings.MontanaHard = (mode == 1);
        UpdateMontanaVisuals();
        UpdateXPPreview();
    }

    private void UpdateMontanaVisuals()
    {
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            SetButtonState(group.montanaClassicButton, !GameSettings.MontanaHard);
            SetButtonState(group.montanaHardButton, GameSettings.MontanaHard);
        }
    }

    public void OnSuitClicked(int suitCount)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameSettings.SpiderSuitCount = suitCount;
        UpdateSuitsVisuals(suitCount);
        ValidateSpiderConstraints(suitCount);
        UpdateXPPreview();
    }

    private void UpdateSuitsVisuals(int count)
    {
        int selectedIndex = (count == 1) ? 0 : (count == 2 ? 1 : 2);

        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            if (group.suitButtons == null) continue;
            for (int i = 0; i < group.suitButtons.Length; i++)
            {
                SetButtonState(group.suitButtons[i], i == selectedIndex);
            }
        }
    }

    private void ValidateSpiderConstraints(int suitCount)
    {
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            if (group.diffButtons == null) continue;
            foreach (var btn in group.diffButtons) if (btn) btn.interactable = true;

            if (suitCount == 1)
            {
                if (group.diffButtons.Length > 2 && group.diffButtons[2]) group.diffButtons[2].interactable = false;
            }
            else if (suitCount == 4)
            {
                if (group.diffButtons.Length > 0 && group.diffButtons[0]) group.diffButtons[0].interactable = false;
            }
        }

        if (suitCount == 1 && GameSettings.CurrentDifficulty == Difficulty.Hard) SetDifficulty((int)Difficulty.Medium);
        else if (suitCount == 4 && GameSettings.CurrentDifficulty == Difficulty.Easy) SetDifficulty((int)Difficulty.Medium);

        UpdateDifficultyVisuals();
    }

    private void ValidateOctagonConstraints()
    {
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            if (group.diffButtons == null) continue;
            foreach (var btn in group.diffButtons) if (btn) btn.interactable = true;

            if (group.diffButtons.Length > 0 && group.diffButtons[0]) group.diffButtons[0].interactable = false;
            if (group.diffButtons.Length > 2 && group.diffButtons[2]) group.diffButtons[2].interactable = false;
        }

        if (GameSettings.CurrentDifficulty == Difficulty.Easy || GameSettings.CurrentDifficulty == Difficulty.Hard)
        {
            SetDifficulty((int)Difficulty.Medium);
        }
        else
        {
            UpdateDifficultyVisuals();
        }
    }

    public void OnRoundsClicked(int index)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameSettings.RoundsCount = index + 1;
        UpdateRoundsVisuals();
        UpdateXPPreview();
    }

    private void UpdateRoundsVisuals()
    {
        int currentIndex = GameSettings.RoundsCount - 1;
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            if (group.roundsButtons == null) continue;
            for (int i = 0; i < group.roundsButtons.Length; i++)
            {
                SetButtonState(group.roundsButtons[i], i == currentIndex);
            }
        }
    }

    public void OnDifficultyClicked(int diffIndex)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        SetDifficulty(diffIndex);
        UpdateXPPreview();
    }

    private void SetDifficulty(int index)
    {
        GameSettings.CurrentDifficulty = (Difficulty)index;
        UpdateDifficultyVisuals();
    }

    private void UpdateDifficultyVisuals()
    {
        int currentIndex = (int)GameSettings.CurrentDifficulty;
        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            if (group.diffButtons == null) continue;
            for (int i = 0; i < group.diffButtons.Length; i++)
            {
                if (group.diffButtons[i] == null) continue;
                if (!group.diffButtons[i].interactable)
                {
                    group.diffButtons[i].image.color = buttonDisabledColor;
                }
                else
                {
                    SetButtonState(group.diffButtons[i], i == currentIndex);
                }
            }
        }
    }

    private void SetButtonState(Button btn, bool isSelected)
    {
        if (btn == null) return;
        btn.image.color = isSelected ? bgSelectedColor : bgNormalColor;
        var tmpText = btn.GetComponentInChildren<TMP_Text>();
        if (tmpText != null) tmpText.color = isSelected ? textSelectedColor : textNormalColor;
        else
        {
            var legacyText = btn.GetComponentInChildren<Text>();
            if (legacyText != null) legacyText.color = isSelected ? textSelectedColor : textNormalColor;
        }
    }

    public void OnStartGameClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Game_Start");
        GameSettings.IsTutorialMode = false;

        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            SetButtonState(group.startButton, false);
            if (group.startButton) group.startButton.interactable = false;
            if (group.tutorialButton) group.tutorialButton.interactable = false;
        }

        if (DealCacheSystem.Instance != null) DealCacheSystem.Instance.ReturnActiveDealToQueue();

        if (string.IsNullOrEmpty(currentGame.sceneName)) return;

        if (exitController != null)
        {
            exitController.PlayExitAnimation(currentGame.type, () => SceneManager.LoadScene(currentGame.sceneName));
        }
        else SceneManager.LoadScene(currentGame.sceneName);
    }

    public void OnTutorialButtonClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Game_Start");
        GameSettings.IsTutorialMode = true;

        foreach (var group in new[] { landscapeSettings, portraitSettings })
        {
            SetButtonState(group.tutorialButton, false);
            if (group.startButton) group.startButton.interactable = false;
            if (group.tutorialButton) group.tutorialButton.interactable = false;
        }

        if (string.IsNullOrEmpty(currentGame.sceneName)) return;

        if (exitController != null)
        {
            exitController.PlayExitAnimation(currentGame.type, () => SceneManager.LoadScene(currentGame.sceneName));
        }
        else SceneManager.LoadScene(currentGame.sceneName);
    }

    // ==========================================
    // ОСТАЛЬНОЙ КОД (Корутины и прочее)
    // ==========================================
    private IEnumerator AnimateButtonsRoutine()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        float duration = 0.4f;
        float elapsed = 0f;

        List<(RectTransform ui, RectTransform target)> items = new List<(RectTransform, RectTransform)>();

        if (leaderboardBtn.button != null) items.Add((leaderboardBtn.button, leaderboardBtn.GetTarget(isPortrait, isSettingsMode)));
        if (statsBtn.button != null) items.Add((statsBtn.button, statsBtn.GetTarget(isPortrait, isSettingsMode)));

        foreach (var el in otherResponsiveElements)
        {
            if (el.uiElement != null) items.Add((el.uiElement, el.GetTarget(isPortrait)));
        }

        Dictionary<RectTransform, Vector3> startPositions = new Dictionary<RectTransform, Vector3>();
        Dictionary<RectTransform, Vector2> startSizes = new Dictionary<RectTransform, Vector2>();
        Dictionary<RectTransform, Vector3> startScales = new Dictionary<RectTransform, Vector3>();

        foreach (var item in items)
        {
            if (item.ui == null || item.target == null) continue;
            startPositions[item.ui] = item.ui.position;
            item.ui.SetParent(item.target, true);
            SetAsFixedCenterAnchor(item.ui, item.ui.rect.size);
            startSizes[item.ui] = item.ui.sizeDelta;
            startScales[item.ui] = item.ui.localScale;
            item.ui.position = startPositions[item.ui];
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            foreach (var item in items)
            {
                if (item.ui == null || item.target == null) continue;
                item.ui.position = Vector3.LerpUnclamped(startPositions[item.ui], item.target.position, smoothT);
                item.ui.sizeDelta = Vector2.LerpUnclamped(startSizes[item.ui], item.target.rect.size, smoothT);
                item.ui.localScale = Vector3.LerpUnclamped(startScales[item.ui], Vector3.one, smoothT);
            }
            yield return null;
        }

        foreach (var item in items)
        {
            if (item.ui != null && item.target != null) SetAsStretchChild(item.ui);
        }
    }

    private void MoveButtonsToTarget(bool toSettingsMode)
    {
        isSettingsMode = toSettingsMode;
        if (buttonsMoveCoroutine != null) StopCoroutine(buttonsMoveCoroutine);
        buttonsMoveCoroutine = StartCoroutine(AnimateButtonsRoutine());
    }

    // --- ОВЕРЛЕИ, НАСТРОЙКИ ЗВУКА И ПРОЧЕЕ ---
    // (Ниже расположены ваши неизмененные методы для настроек звука, языков, и анимации оверлеев)

    public void OnGlobalSettingsClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameObject activePanel = isPortrait ? portraitGlobalSettings.panel : landscapeGlobalSettings.panel;
        ToggleOverlay(activePanel, true);
    }
    private bool IsGlobalSettingsOpen()
    {
        return (landscapeGlobalSettings.panel != null && landscapeGlobalSettings.panel.activeSelf) ||
               (portraitGlobalSettings.panel != null && portraitGlobalSettings.panel.activeSelf);
    }

    public void OnLeaderboardClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        string lbName = IsSettingsPanelOpen() ? currentGame.type.ToString() : "Global";

        if (dynamicLeaderboard != null) dynamicLeaderboard.LoadLeaderboard(lbName);

        GameObject activePanel = isPortrait ? portraitLeaderboardPanel : landscapeLeaderboardPanel;
        ToggleOverlay(activePanel, true);
    }
    public void OnShopClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameObject activePanel = isPortrait ? portraitShopPanel : landscapeShopPanel;
        ToggleOverlay(activePanel, true);
    }
    public void OnDailyQuestsClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameObject activePanel = isPortrait ? portraitDailyQuestsPanel : landscapeDailyQuestsPanel;
        ToggleOverlay(activePanel, true);
    }

    public void OnStatisticsClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        bool isPremium = StatisticsManager.Instance != null && StatisticsManager.Instance.IsUserPremium;
        bool isGameSpecific = IsSettingsPanelOpen();

        StatsUIGroup activeStats = isPortrait ? portraitStats : landscapeStats;
        GameObject panelToOpen = isGameSpecific ? (isPremium ? activeStats.gamePremiumPanel : activeStats.gameBasicPanel) : (isPremium ? activeStats.appPremiumPanel : activeStats.appBasicPanel);

        if (panelToOpen != null)
        {
            ToggleOverlay(panelToOpen, true);
            if (isGameSpecific)
            {
                var premiumUI = panelToOpen.GetComponent<StatisticsUI>();
                if (premiumUI != null) premiumUI.ShowStatsForGame(currentGame.type);
                var basicUI = panelToOpen.GetComponent<BasicStatisticsUI>();
                if (basicUI != null) basicUI.ShowStatsForGame(currentGame.type);
            }
        }
    }

    public void OnCloseOverlayClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Back");
        CloseAllOverlaysAnimated();
        if (!IsSettingsPanelOpen() && mainSelectionPanel) mainSelectionPanel.SetActive(true);
    }

    private void ToggleOverlay(GameObject panel, bool show)
    {
        if (panel == null) return;

        if (show)
        {
            // Глобальные настройки
            if (landscapeGlobalSettings.panel && landscapeGlobalSettings.panel.activeSelf && landscapeGlobalSettings.panel != panel) ToggleOverlay(landscapeGlobalSettings.panel, false);
            if (portraitGlobalSettings.panel && portraitGlobalSettings.panel.activeSelf && portraitGlobalSettings.panel != panel) ToggleOverlay(portraitGlobalSettings.panel, false);

            // Статистика и прочее
            if (landscapeStats.appBasicPanel && landscapeStats.appBasicPanel.activeSelf && landscapeStats.appBasicPanel != panel) ToggleOverlay(landscapeStats.appBasicPanel, false);
            if (landscapeStats.appPremiumPanel && landscapeStats.appPremiumPanel.activeSelf && landscapeStats.appPremiumPanel != panel) ToggleOverlay(landscapeStats.appPremiumPanel, false);
            if (landscapeStats.gameBasicPanel && landscapeStats.gameBasicPanel.activeSelf && landscapeStats.gameBasicPanel != panel) ToggleOverlay(landscapeStats.gameBasicPanel, false);
            if (landscapeStats.gamePremiumPanel && landscapeStats.gamePremiumPanel.activeSelf && landscapeStats.gamePremiumPanel != panel) ToggleOverlay(landscapeStats.gamePremiumPanel, false);

            if (portraitStats.appBasicPanel && portraitStats.appBasicPanel.activeSelf && portraitStats.appBasicPanel != panel) ToggleOverlay(portraitStats.appBasicPanel, false);
            if (portraitStats.appPremiumPanel && portraitStats.appPremiumPanel.activeSelf && portraitStats.appPremiumPanel != panel) ToggleOverlay(portraitStats.appPremiumPanel, false);
            if (portraitStats.gameBasicPanel && portraitStats.gameBasicPanel.activeSelf && portraitStats.gameBasicPanel != panel) ToggleOverlay(portraitStats.gameBasicPanel, false);
            if (portraitStats.gamePremiumPanel && portraitStats.gamePremiumPanel.activeSelf && portraitStats.gamePremiumPanel != panel) ToggleOverlay(portraitStats.gamePremiumPanel, false);
            if (landscapeLeaderboardPanel && landscapeLeaderboardPanel.activeSelf && landscapeLeaderboardPanel != panel) ToggleOverlay(landscapeLeaderboardPanel, false);
            if (portraitLeaderboardPanel && portraitLeaderboardPanel.activeSelf && portraitLeaderboardPanel != panel) ToggleOverlay(portraitLeaderboardPanel, false);
            if (landscapeShopPanel && landscapeShopPanel.activeSelf && landscapeShopPanel != panel) ToggleOverlay(landscapeShopPanel, false);
            if (portraitShopPanel && portraitShopPanel.activeSelf && portraitShopPanel != panel) ToggleOverlay(portraitShopPanel, false);
            if (landscapeDailyQuestsPanel && landscapeDailyQuestsPanel.activeSelf && landscapeDailyQuestsPanel != panel) ToggleOverlay(landscapeDailyQuestsPanel, false);
            if (portraitDailyQuestsPanel && portraitDailyQuestsPanel.activeSelf && portraitDailyQuestsPanel != panel) ToggleOverlay(portraitDailyQuestsPanel, false);

            // Кастомизация (если используете ToggleOverlay для них)
            if (landscapeCustomization.cardAppearancePanel && landscapeCustomization.cardAppearancePanel.activeSelf && landscapeCustomization.cardAppearancePanel != panel) ToggleOverlay(landscapeCustomization.cardAppearancePanel, false);
            if (portraitCustomization.cardAppearancePanel && portraitCustomization.cardAppearancePanel.activeSelf && portraitCustomization.cardAppearancePanel != panel) ToggleOverlay(portraitCustomization.cardAppearancePanel, false);
        }

        if (activePanelCoroutines.ContainsKey(panel) && activePanelCoroutines[panel] != null)
        {
            StopCoroutine(activePanelCoroutines[panel]);
        }

        activePanelCoroutines[panel] = StartCoroutine(AnimateOverlayRoutine(panel, show));
    }

    private IEnumerator AnimateOverlayRoutine(GameObject panel, bool show)
    {
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt == null) yield break;

        // Выбираем скорость и дистанцию на лету
        float currentDuration = isPortrait ? portraitOverlayDuration : landscapeOverlayDuration;
        float currentDistance = isPortrait ? portraitOverlayFlyDistanceX : landscapeOverlayFlyDistanceX;

        if (AudioManager.Instance != null)
        {
            string soundToPlay = show ? "Panel_Slide_In" : "Panel_Slide_Out";
            float delay = currentDuration - 0.15f;
            if (delay < 0) delay = 0;
            AudioManager.Instance.PlaySoundWithAutoFade(soundToPlay, delay, 0.15f);
        }

        Vector2 centerPos = panelInitialPositions.ContainsKey(panel) ? panelInitialPositions[panel] : Vector2.zero;
        Vector2 leftPos = centerPos + new Vector2(-currentDistance, 0);  // Используем новую дистанцию
        Vector2 rightPos = centerPos + new Vector2(currentDistance, 0);

        if (show)
        {
            if (!panel.activeSelf) rt.anchoredPosition = leftPos;
            panel.SetActive(true);
        }

        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = show ? centerPos : rightPos;

        float elapsed = 0f;
        while (elapsed < currentDuration) // Используем новое время
        {
            elapsed += Time.deltaTime;
            float t = elapsed / currentDuration;
            float curveT = show ? (1f - Mathf.Pow(1f - t, 3)) : (t * t * t);

            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, curveT);
            yield return null;
        }

        rt.anchoredPosition = endPos;
        if (!show) panel.SetActive(false);
    }

    private void CloseAllOverlaysAnimated()
    {
        // Глобальные настройки
        if (landscapeGlobalSettings.panel && landscapeGlobalSettings.panel.activeSelf) ToggleOverlay(landscapeGlobalSettings.panel, false);
        if (portraitGlobalSettings.panel && portraitGlobalSettings.panel.activeSelf) ToggleOverlay(portraitGlobalSettings.panel, false);

        // Статистика и прочее
        if (landscapeStats.appBasicPanel && landscapeStats.appBasicPanel.activeSelf) ToggleOverlay(landscapeStats.appBasicPanel, false);
        if (landscapeStats.appPremiumPanel && landscapeStats.appPremiumPanel.activeSelf) ToggleOverlay(landscapeStats.appPremiumPanel, false);
        if (landscapeStats.gameBasicPanel && landscapeStats.gameBasicPanel.activeSelf) ToggleOverlay(landscapeStats.gameBasicPanel, false);
        if (landscapeStats.gamePremiumPanel && landscapeStats.gamePremiumPanel.activeSelf) ToggleOverlay(landscapeStats.gamePremiumPanel, false);

        if (portraitStats.appBasicPanel && portraitStats.appBasicPanel.activeSelf) ToggleOverlay(portraitStats.appBasicPanel, false);
        if (portraitStats.appPremiumPanel && portraitStats.appPremiumPanel.activeSelf) ToggleOverlay(portraitStats.appPremiumPanel, false);
        if (portraitStats.gameBasicPanel && portraitStats.gameBasicPanel.activeSelf) ToggleOverlay(portraitStats.gameBasicPanel, false);
        if (portraitStats.gamePremiumPanel && portraitStats.gamePremiumPanel.activeSelf) ToggleOverlay(portraitStats.gamePremiumPanel, false);
        if (landscapeLeaderboardPanel && landscapeLeaderboardPanel.activeSelf) ToggleOverlay(landscapeLeaderboardPanel, false);
        if (portraitLeaderboardPanel && portraitLeaderboardPanel.activeSelf) ToggleOverlay(portraitLeaderboardPanel, false);
        if (landscapeShopPanel && landscapeShopPanel.activeSelf) ToggleOverlay(landscapeShopPanel, false);
        if (portraitShopPanel && portraitShopPanel.activeSelf) ToggleOverlay(portraitShopPanel, false);
        if (landscapeDailyQuestsPanel && landscapeDailyQuestsPanel.activeSelf) ToggleOverlay(landscapeDailyQuestsPanel, false);
        if (portraitDailyQuestsPanel && portraitDailyQuestsPanel.activeSelf) ToggleOverlay(portraitDailyQuestsPanel, false);

        // Панели кастомизации (Левые/Вертикальные вылеты)
        if (landscapeCustomization.backgroundSelectionPanel && landscapeCustomization.backgroundSelectionPanel.activeSelf) ToggleOverlay(landscapeCustomization.backgroundSelectionPanel, false);
        if (portraitCustomization.backgroundSelectionPanel && portraitCustomization.backgroundSelectionPanel.activeSelf) ToggleOverlay(portraitCustomization.backgroundSelectionPanel, false);

        if (landscapeCustomization.cardAppearancePanel && landscapeCustomization.cardAppearancePanel.activeSelf) ToggleOverlay(landscapeCustomization.cardAppearancePanel, false);
        if (portraitCustomization.cardAppearancePanel && portraitCustomization.cardAppearancePanel.activeSelf) ToggleOverlay(portraitCustomization.cardAppearancePanel, false);

        // Панели кастомизации (Правые вылеты)
        if (landscapeCustomization.cardBackSelectionPanel && landscapeCustomization.cardBackSelectionPanel.activeSelf) ToggleOverlayRight(landscapeCustomization.cardBackSelectionPanel, false);
        if (portraitCustomization.cardBackSelectionPanel && portraitCustomization.cardBackSelectionPanel.activeSelf) ToggleOverlayRight(portraitCustomization.cardBackSelectionPanel, false);

        if (landscapeCustomization.deckSelectionPanel && landscapeCustomization.deckSelectionPanel.activeSelf) ToggleOverlayRight(landscapeCustomization.deckSelectionPanel, false);
        if (portraitCustomization.deckSelectionPanel && portraitCustomization.deckSelectionPanel.activeSelf) ToggleOverlayRight(portraitCustomization.deckSelectionPanel, false);
    }

    private void CloseAllOverlaysInstant()
    {
        // Глобальные настройки
        if (landscapeGlobalSettings.panel) landscapeGlobalSettings.panel.SetActive(false);
        if (portraitGlobalSettings.panel) portraitGlobalSettings.panel.SetActive(false);

        // Статистика и прочее
        if (landscapeStats.appBasicPanel) landscapeStats.appBasicPanel.SetActive(false);
        if (landscapeStats.appPremiumPanel) landscapeStats.appPremiumPanel.SetActive(false);
        if (landscapeStats.gameBasicPanel) landscapeStats.gameBasicPanel.SetActive(false);
        if (landscapeStats.gamePremiumPanel) landscapeStats.gamePremiumPanel.SetActive(false);

        if (portraitStats.appBasicPanel) portraitStats.appBasicPanel.SetActive(false);
        if (portraitStats.appPremiumPanel) portraitStats.appPremiumPanel.SetActive(false);
        if (portraitStats.gameBasicPanel) portraitStats.gameBasicPanel.SetActive(false);
        if (portraitStats.gamePremiumPanel) portraitStats.gamePremiumPanel.SetActive(false);
        if (landscapeLeaderboardPanel) landscapeLeaderboardPanel.SetActive(false);
        if (portraitLeaderboardPanel) portraitLeaderboardPanel.SetActive(false);
        if (landscapeShopPanel) landscapeShopPanel.SetActive(false);
        if (portraitShopPanel) portraitShopPanel.SetActive(false);
        if (landscapeDailyQuestsPanel) landscapeDailyQuestsPanel.SetActive(false);
        if (portraitDailyQuestsPanel) portraitDailyQuestsPanel.SetActive(false);

        // Панели кастомизации (Горизонтальные)
        if (landscapeCustomization.backgroundSelectionPanel) landscapeCustomization.backgroundSelectionPanel.SetActive(false);
        if (landscapeCustomization.cardAppearancePanel) landscapeCustomization.cardAppearancePanel.SetActive(false);
        if (landscapeCustomization.cardBackSelectionPanel) landscapeCustomization.cardBackSelectionPanel.SetActive(false);
        if (landscapeCustomization.deckSelectionPanel) landscapeCustomization.deckSelectionPanel.SetActive(false);

        // Панели кастомизации (Вертикальные)
        if (portraitCustomization.backgroundSelectionPanel) portraitCustomization.backgroundSelectionPanel.SetActive(false);
        if (portraitCustomization.cardAppearancePanel) portraitCustomization.cardAppearancePanel.SetActive(false);
        if (portraitCustomization.cardBackSelectionPanel) portraitCustomization.cardBackSelectionPanel.SetActive(false);
        if (portraitCustomization.deckSelectionPanel) portraitCustomization.deckSelectionPanel.SetActive(false);
    }

    public void OnSoundOnClicked() { if (AudioManager.Instance != null && AudioManager.Instance.isMuted) { AudioManager.Instance.SetMute(false); AudioManager.Instance.PlaySound("UI_Click"); UpdateSoundButtonsVisuals(false); } }
    public void OnSoundOffClicked() { if (AudioManager.Instance != null && !AudioManager.Instance.isMuted) { AudioManager.Instance.PlaySound("UI_Click"); AudioManager.Instance.SetMute(true); UpdateSoundButtonsVisuals(false); } }

    private void UpdateSoundButtonsVisuals(bool instant = false)
    {
        if (AudioManager.Instance == null) return;
        bool isMuted = AudioManager.Instance.isMuted;

        foreach (var group in new[] { landscapeGlobalSettings, portraitGlobalSettings })
        {
            SetButtonState(group.soundOnButton, !isMuted);
            SetButtonState(group.soundOffButton, isMuted);
            TintButtonIcon(group.soundOnButton, !isMuted);
            TintButtonIcon(group.soundOffButton, isMuted);

            if (group.soundOnButton != null) AnimateOrSetScale(group.soundOnButton.GetComponent<RectTransform>(), !isMuted ? buttonActiveScale : Vector3.one, instant);
            if (group.soundOffButton != null) AnimateOrSetScale(group.soundOffButton.GetComponent<RectTransform>(), isMuted ? buttonActiveScale : Vector3.one, instant);
        }
    }

    private void TintButtonIcon(Button btn, bool isSelected)
    {
        if (btn == null) return;
        foreach (var img in btn.GetComponentsInChildren<Image>())
        {
            if (img != btn.image) img.color = isSelected ? textSelectedColor : textNormalColor;
        }
    }

    public void OnLanguageButtonClicked(int index)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        if (index >= 0 && index < languageCodes.Length && LocalizationManager.instance != null) LocalizationManager.instance.SetLanguage(languageCodes[index]);
        UpdateLanguageButtonsVisuals(false);
    }

    private void UpdateLanguageButtonsVisuals(bool instant = false)
    {
        string currentLang = LocalizationManager.instance != null ? LocalizationManager.instance.CurrentLanguage : "en";

        foreach (var group in new[] { landscapeGlobalSettings, portraitGlobalSettings })
        {
            if (group.languageButtons == null) continue;
            for (int i = 0; i < group.languageButtons.Length; i++)
            {
                if (group.languageButtons[i] == null) continue;
                bool isSelected = (languageCodes[i] == currentLang);
                SetButtonState(group.languageButtons[i], isSelected);
                AnimateOrSetScale(group.languageButtons[i].GetComponent<RectTransform>(), isSelected ? buttonActiveScale : Vector3.one, instant);
            }
        }
    }

    public void OnClickModeButtonClicked(int mode)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameSettings.AutoMoveClickMode = mode;
        UpdateClickModeButtonsVisuals(false);
    }

    private void UpdateClickModeButtonsVisuals(bool instant = false)
    {
        int mode = GameSettings.AutoMoveClickMode;
        foreach (var group in new[] { landscapeGlobalSettings, portraitGlobalSettings })
        {
            if (group.singleClickButton != null) { SetButtonState(group.singleClickButton, mode == 0); AnimateOrSetScale(group.singleClickButton.GetComponent<RectTransform>(), mode == 0 ? buttonActiveScale : Vector3.one, instant); }
            if (group.doubleClickButton != null) { SetButtonState(group.doubleClickButton, mode == 1); AnimateOrSetScale(group.doubleClickButton.GetComponent<RectTransform>(), mode == 1 ? buttonActiveScale : Vector3.one, instant); }
        }
    }

    private void AnimateOrSetScale(RectTransform target, Vector3 targetScale, bool instant)
    {
        if (target == null) return;
        if (instant) { target.localScale = targetScale; return; }
        if (buttonScaleCoroutines.ContainsKey(target) && buttonScaleCoroutines[target] != null) StopCoroutine(buttonScaleCoroutines[target]);
        buttonScaleCoroutines[target] = StartCoroutine(ScaleButtonRoutine(target, targetScale));
    }

    private IEnumerator ScaleButtonRoutine(RectTransform target, Vector3 targetScale)
    {
        float elapsed = 0f;
        Vector3 startScale = target.localScale;
        while (elapsed < buttonAnimDuration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(startScale, targetScale, Mathf.SmoothStep(0f, 1f, elapsed / buttonAnimDuration));
            yield return null;
        }
        target.localScale = targetScale;
    }

    public void OpenBackgroundSelection()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameObject activeGlobalPanel = isPortrait ? portraitGlobalSettings.panel : landscapeGlobalSettings.panel;
        GameObject activeBgPanel = isPortrait ? portraitCustomization.backgroundSelectionPanel : landscapeCustomization.backgroundSelectionPanel;
        float flyDist = isPortrait ? portraitVerticalFlyDistance : landscapeVerticalFlyDistance;

        if (activeGlobalPanel && activeBgPanel)
            StartCoroutine(VerticalTransitionRoutine(activeGlobalPanel, activeBgPanel, true, flyDist));
    }
    public void CloseBackgroundSelection()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameObject activeGlobalPanel = isPortrait ? portraitGlobalSettings.panel : landscapeGlobalSettings.panel;
        GameObject activeBgPanel = isPortrait ? portraitCustomization.backgroundSelectionPanel : landscapeCustomization.backgroundSelectionPanel;
        float flyDist = isPortrait ? portraitVerticalFlyDistance : landscapeVerticalFlyDistance;

        if (activeBgPanel && activeGlobalPanel)
            StartCoroutine(VerticalTransitionRoutine(activeBgPanel, activeGlobalPanel, false, flyDist));
    }

    public void OpenCardAppearance()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameObject activeGlobalPanel = isPortrait ? portraitGlobalSettings.panel : landscapeGlobalSettings.panel;
        GameObject activeAppPanel = isPortrait ? portraitCustomization.cardAppearancePanel : landscapeCustomization.cardAppearancePanel;
        float flyDist = isPortrait ? portraitVerticalFlyDistance : landscapeVerticalFlyDistance;

        if (activeGlobalPanel && activeAppPanel)
            StartCoroutine(VerticalTransitionRoutine(activeGlobalPanel, activeAppPanel, true, flyDist));
    }

    public void CloseCardAppearance()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameObject activeGlobalPanel = isPortrait ? portraitGlobalSettings.panel : landscapeGlobalSettings.panel;
        GameObject activeAppPanel = isPortrait ? portraitCustomization.cardAppearancePanel : landscapeCustomization.cardAppearancePanel;
        float flyDist = isPortrait ? portraitVerticalFlyDistance : landscapeVerticalFlyDistance;

        if (activeAppPanel && activeGlobalPanel)
            StartCoroutine(VerticalTransitionRoutine(activeAppPanel, activeGlobalPanel, false, flyDist));
    }
    private IEnumerator VerticalTransitionRoutine(GameObject panelToHide, GameObject panelToShow, bool isOpeningBg, float flyDistance)
    {
        // Выбираем время анимации в зависимости от ориентации
        float currentDuration = isPortrait ? portraitOverlayDuration : landscapeOverlayDuration;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySoundWithAutoFade(isOpeningBg ? "Panel_Slide_In" : "Panel_Slide_Out", Mathf.Max(0, currentDuration - 0.15f), 0.15f);

        RectTransform hideRt = panelToHide.GetComponent<RectTransform>();
        RectTransform showRt = panelToShow.GetComponent<RectTransform>();

        Vector2 hideCenterPos = panelInitialPositions.ContainsKey(panelToHide) ? panelInitialPositions[panelToHide] : Vector2.zero;
        Vector2 showCenterPos = panelInitialPositions.ContainsKey(panelToShow) ? panelInitialPositions[panelToShow] : Vector2.zero;

        Vector2 hideTargetPos = hideCenterPos + new Vector2(0, isOpeningBg ? flyDistance : -flyDistance);
        Vector2 showStartPos = showCenterPos + new Vector2(0, isOpeningBg ? -flyDistance : flyDistance);

        panelToShow.SetActive(true);
        showRt.anchoredPosition = showStartPos;
        Vector2 hideStartPos = hideRt.anchoredPosition;

        float elapsed = 0f;
        while (elapsed < currentDuration) // Используем новое время
        {
            elapsed += Time.deltaTime;
            float t = elapsed / currentDuration;
            float curveT = isOpeningBg ? (1f - Mathf.Pow(1f - t, 3)) : (t * t * t);

            hideRt.anchoredPosition = Vector2.LerpUnclamped(hideStartPos, hideTargetPos, curveT);
            showRt.anchoredPosition = Vector2.LerpUnclamped(showStartPos, showCenterPos, curveT);
            yield return null;
        }

        hideRt.anchoredPosition = hideTargetPos;
        showRt.anchoredPosition = showCenterPos;
        panelToHide.SetActive(false);
    }

    public void OpenCardBackSelection()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        GameObject activePanel = isPortrait ? portraitCustomization.cardBackSelectionPanel : landscapeCustomization.cardBackSelectionPanel;
        ToggleOverlayRight(activePanel, true);
    }

    public void CloseCardBackSelection()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Back");
        GameObject activePanel = isPortrait ? portraitCustomization.cardBackSelectionPanel : landscapeCustomization.cardBackSelectionPanel;
        ToggleOverlayRight(activePanel, false);
    }

    public void OpenDeckSelection()
    {
        GameObject activePanel = isPortrait ? portraitCustomization.deckSelectionPanel : landscapeCustomization.deckSelectionPanel;
        ToggleOverlayRight(activePanel, true);
    }

    public void CloseDeckSelection()
    {
        GameObject activePanel = isPortrait ? portraitCustomization.deckSelectionPanel : landscapeCustomization.deckSelectionPanel;
        ToggleOverlayRight(activePanel, false);
    }

    private void ToggleOverlayRight(GameObject panel, bool show)
    {
        if (panel == null) return;
        if (activePanelCoroutines.ContainsKey(panel) && activePanelCoroutines[panel] != null) StopCoroutine(activePanelCoroutines[panel]);
        activePanelCoroutines[panel] = StartCoroutine(AnimateOverlayRightRoutine(panel, show));
    }

    private IEnumerator AnimateOverlayRightRoutine(GameObject panel, bool show)
    {
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt == null) yield break;

        // Выбираем скорость и дистанцию на лету
        float currentDuration = isPortrait ? portraitOverlayDuration : landscapeOverlayDuration;
        float currentDistance = isPortrait ? portraitOverlayFlyDistanceX : landscapeOverlayFlyDistanceX;

        if (AudioManager.Instance != null)
        {
            string soundToPlay = show ? "Panel_Slide_In" : "Panel_Slide_Out";
            float delay = currentDuration - 0.15f;
            AudioManager.Instance.PlaySoundWithAutoFade(soundToPlay, Mathf.Max(0, delay), 0.15f);
        }

        Vector2 centerPos = panelInitialPositions.ContainsKey(panel) ? panelInitialPositions[panel] : Vector2.zero;
        Vector2 rightPos = centerPos + new Vector2(currentDistance, 0); // Используем новую дистанцию

        if (show)
        {
            if (!panel.activeSelf) rt.anchoredPosition = rightPos;
            panel.SetActive(true);
        }

        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = show ? centerPos : rightPos;

        float elapsed = 0f;
        while (elapsed < currentDuration) // Используем новое время
        {
            elapsed += Time.deltaTime;
            float t = elapsed / currentDuration;
            float curveT = show ? (1f - Mathf.Pow(1f - t, 3)) : (t * t * t);

            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, curveT);
            yield return null;
        }

        rt.anchoredPosition = endPos;
        if (!show) panel.SetActive(false);
        activePanelCoroutines[panel] = null;
    }
}