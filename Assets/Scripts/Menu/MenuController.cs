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
        if (Instance == null)
        {
            Instance = this;
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
        public bool showSuitSelector;   // Spider
        public bool showRoundsSelector; // Pyramid/TriPeaks

        [Space]
        public bool showDrawMode;       // Klondike (вместо слайдера)
        public bool showYukonModes;     // Yukon
        public bool showMonteCarloModes;// Monte Carlo
        public bool showMontanaModes;   // Montana
        public bool showNoOptionsPanel;
    }

    [Header("Game Definitions")]
    public List<GameDefinition> games;

    [Header("UI Panels")]
    public GameObject mainSelectionPanel;
    public GameObject settingsPanel;
   

    public GameObject appBasicStatsPanel;    // 1. Общая базовая (для всего приложения)
    public GameObject appPremiumStatsPanel;  // 2. Общая премиум
    public GameObject gameBasicStatsPanel;   // 3. Игра базовая (для конкретного пасьянса)
    public GameObject gamePremiumStatsPanel; // 4. Игра премиум

    [Header("Controllers")]
    public MenuLevelController levelController;
    public CardAnimationController cardAnimator;
    public SettingsPanelAnimator settingsPanelAnimator;
    public MenuExitController exitController;
    public DynamicLeaderboardController dynamicLeaderboard;

    [Header("Menu Overlays (Restored)")]
    public GameObject globalSettingsPanel; // Глобальные настройки (Звук/Музыка)
    public GameObject leaderboardPanel;    // Лидерборд
    public GameObject shopPanel;           // Магазин
    public GameObject dailyQuestsPanel;    // Ежедневные задания

    [Header("UI Containers")]
    public GameObject difficultyContainer;
    public GameObject suitSelectionContainer;
    public GameObject roundsSelectionContainer;
    public GameObject drawModeContainer;        // Klondike
    public GameObject yukonModeContainer;       // Yukon
    public GameObject monteCarloModeContainer;  // Monte Carlo
    public GameObject montanaModeContainer;     // Montana
    public GameObject noOptionsContainer;

    [Header("XP Preview UI")] // <--- НОВОЕ
    [Tooltip("Текст внутри SettingsPanel, где написано 'Вы получите X опыта'")]
    public TMP_Text xpPreviewText;
    [Tooltip("Ключ локализации. Пример: 'xp_reward_preview'. В таблице должно быть 'You will get {0} XP'")]
    public string xpPreviewLocKey = "xp_reward_preview";

    [Header("Buttons: Klondike (Draw Mode)")]
    public Button draw1Button;
    public Button draw3Button;

    [Header("Buttons: Yukon")]
    public Button yukonClassicButton;
    public Button yukonRussianButton;

    [Header("Buttons: Monte Carlo")]
    public Button monteCarlo8WaysButton;
    public Button monteCarlo4WaysButton;

    [Header("Buttons: Montana")]
    public Button montanaClassicButton;
    public Button montanaHardButton;

    [Header("Buttons: Arrays")]
    public Button[] diffButtons;   // 0-Easy, 1-Medium, 2-Hard
    public Button[] suitButtons;   // 0-[1 suit], 1-[2 suits], 2-[4 suits]
    public Button[] roundsButtons; // 0-[1 round], 1-[2 rounds], 2-[3 rounds]

    [Header("Main Action Buttons")]
    public Button startButton; // <--- НОВАЯ ССЫЛКА НА КНОПКУ СТАРТ
    public Button tutorialButton;

    // --- НОВАЯ СЕКЦИЯ: ДВИЖЕНИЕ ПО МАРКЕРАМ ---
    [Header("Dynamic Buttons (Target Objects)")]
    public RectTransform leaderboardButton; // Сама кнопка
    public RectTransform statsButton;       // Сама кнопка

    [Header("Position Markers")]
    [Tooltip("Пустой объект, где кнопка Лидерборда стоит в ГЛАВНОМ МЕНЮ")]
    public Transform lbStartMarker;
    [Tooltip("Пустой объект, куда кнопка Лидерборда уезжает в НАСТРОЙКАХ")]
    public Transform lbEndMarker;

    [Tooltip("Пустой объект, где кнопка Статистики стоит в ГЛАВНОМ МЕНЮ")]
    public Transform statsStartMarker;
    [Tooltip("Пустой объект, куда кнопка Статистики уезжает в НАСТРОЙКАХ")]
    public Transform statsEndMarker;

    private Coroutine buttonsMoveCoroutine;

    [Header("Visual Settings (Background)")]
    // FFB01A (Orange)
    public Color bgSelectedColor = new Color32(255, 176, 26, 255);
    // 9A5F40 (Brown)
    public Color bgNormalColor = new Color32(154, 95, 64, 255);
    public Color bgDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Visual Settings (Text)")]
    // 24140C (Dark Brown/Black)
    public Color textSelectedColor = new Color32(36, 20, 12, 255);
    // C0C0C0 (Silver/Grey)
    public Color textNormalColor = new Color32(192, 192, 192, 255);
    public Color buttonDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    private GameDefinition currentGame;
    [Header("Overlay Animations")]
    public float overlayAnimDuration = 0.4f;
    public float overlayFlyDistanceX = 2500f; // Расстояние, на которое улетают панели за экран

    [Header("Audio Settings UI (Radio Buttons)")]
    public Button soundOnButton;
    public Button soundOffButton;

    [Header("Language Settings UI")]
    public Button[] languageButtons; // Массив из 5 кнопок
    public string[] languageCodes = new string[] { "ru", "en", "tr", "es", "pt" }; // Коды языков в том же порядке

    [Header("Button Animation Settings")]
    public float buttonAnimDuration = 0.15f; // Скорость изменения размера
    public Vector3 buttonActiveScale = new Vector3(1.05f, 1.05f, 1f); // Насколько увеличивается активная кнопка

    [Header("Background Selection UI")]
    public GameObject backgroundSelectionPanel; // Ваша новая нижняя панель
    public GameObject cardAppearancePanel;
    public float verticalFlyDistance = 1500f;

    [Header("Card Back Selection UI")]
    public GameObject cardBackSelectionPanel; // Ссылка на боковую панель рубашек
    [Header("Панели кастомизации")]
    public GameObject deckSelectionPanel;

    // Словарь для хранения активных анимаций кнопок (чтобы они не конфликтовали)
    private Dictionary<RectTransform, Coroutine> buttonScaleCoroutines = new Dictionary<RectTransform, Coroutine>();
    private Dictionary<GameObject, Vector2> panelInitialPositions = new Dictionary<GameObject, Vector2>();
    private Dictionary<GameObject, Coroutine> activePanelCoroutines = new Dictionary<GameObject, Coroutine>();

    private void Start()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (mainSelectionPanel) mainSelectionPanel.SetActive(true);

        // Регистрируем оверлеи, чтобы запомнить их идеальные позиции в центре экрана
        RegisterOverlay(globalSettingsPanel);
        RegisterOverlay(appBasicStatsPanel);     // <---
        RegisterOverlay(appPremiumStatsPanel);   // <---
        RegisterOverlay(gameBasicStatsPanel);    // <---
        RegisterOverlay(gamePremiumStatsPanel);
        RegisterOverlay(leaderboardPanel);
        RegisterOverlay(shopPanel);
        RegisterOverlay(dailyQuestsPanel);
        RegisterOverlay(backgroundSelectionPanel);

        // --- ДОБАВЬТЕ ЭТУ СТРОКУ ---
        RegisterOverlay(cardAppearancePanel);
        RegisterOverlay(cardBackSelectionPanel);
        // Прячем их без анимации при старте игры
        CloseAllOverlaysInstant();
        UpdateSoundButtonsVisuals(true);
        UpdateLanguageButtonsVisuals(true);
    }
    // Подписываемся на глобальное событие смены локализации
    private void OnEnable()
    {
        LocalizationManager.OnLocalizationLoaded += OnLocalizationChanged;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLocalizationLoaded -= OnLocalizationChanged;
    }

    private void OnLocalizationChanged()
    {
        // Как только язык загрузился, заставляем меню перекрасить кнопки.
        // Передаем true, чтобы кнопки переключились мгновенно, без анимации "растягивания"
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
    private void UpdateXPPreview()
    {
        // 1. Формируем строку варианта ИЗ ЕДИНОГО ЦЕНТРА
        string variant = GameSettings.GetCurrentVariantString(currentGame.type);

        // 2. Получаем текущий уровень игрока 
        int currentLvl = 1;
        if (StatisticsManager.Instance != null)
        {
            var data = StatisticsManager.Instance.GetGameGlobalStats(currentGame.type.ToString());
            if (data != null) currentLvl = data.currentLevel;
        }

        // 3. Считаем потенциальный опыт
        bool isPremium = StatisticsManager.Instance != null && StatisticsManager.Instance.IsUserPremium;

        int xpAmount = LevelingUtils.CalculateXP(
            currentGame.type,
            currentLvl,
            GameSettings.CurrentDifficulty,
            variant,
            isPremium
        );

        // ---> ИНТЕГРАЦИЯ БУСТЕРА Х2 <---
        if (QuestManager.Instance != null && System.Enum.TryParse(currentGame.type.ToString(), out QuestCategory cat))
        {
            if (QuestManager.Instance.HasActiveXpBuff(cat))
            {
                xpAmount *= 2;
            }
        }
        // ------------------------------

        // 4. Обновляем ТЕКСТ через LocalizationManager
        if (xpPreviewText != null)
        {
            // Формируем число с цветом #FFC400
            string coloredXP = $"<color=#FFC400>{xpAmount}</color>";

            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                // Получаем строку вида "Вы получите {0} опыта"
                string format = LocalizationManager.instance.GetLocalizedValue(xpPreviewLocKey);
                if (string.IsNullOrEmpty(format)) format = "{0} XP";

                try
                {
                    // Вставляем покрашенное число в строку
                    xpPreviewText.text = string.Format(format, coloredXP);
                }
                catch
                {
                    xpPreviewText.text = $"{coloredXP} XP";
                }
            }
            else
            {
                xpPreviewText.text = $"{coloredXP} XP";
            }
        }

        // 5. Обновляем БАР (визуальное заполнение)
        if (levelController != null)
        {
            levelController.ShowXPGainPreview(currentGame.type, xpAmount);
        }
    }
    // --- ЛОГИКА ПЕРЕХОДОВ ---

    public void OnGameSelected(int gameIndex)
    {
        if (gameIndex < 0 || gameIndex >= games.Count) return;

        // 1. Звук выбора самой карты (резкий шлепок/клик)
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Select");

        // 2. Звук выезда панели настроек (мягкое скольжение)
        if (AudioManager.Instance != null)
        {
            // Длительность анимации оверлеев у нас 0.4f. 
            // Мы проигрываем звук 0.25 сек и затухаем за 0.15 сек.
            AudioManager.Instance.PlaySoundWithAutoFade("Panel_Slide_In", 0.25f, 0.15f);
        }
        // --- НОВОЕ: Закрываем панели статистики и лидербордов при смене пасьянса ---
        CloseAllOverlaysAnimated();

        currentGame = games[gameIndex];
        GameSettings.CurrentGameType = currentGame.type;

        SetButtonState(startButton, true);
        ResetGameSettingsDefault();
        SetupSettingsPanel();
        UpdateXPPreview();

        // Запускаем анимацию К "EndMarker"
        MoveButtonsToTarget(true);

        if (cardAnimator != null) cardAnimator.SelectCard(currentGame.type);

        if (settingsPanelAnimator != null)
        {
            if (settingsPanelAnimator.IsOpen())
            {
                settingsPanelAnimator.AnimateSwitch(() =>
                {
                    SetupSettingsPanel();
                    if (levelController != null) levelController.UpdatePreviewBar(currentGame.type);
                    UpdateXPPreview();
                });
            }
            else
            {
                if (levelController != null) levelController.UpdatePreviewBar(currentGame.type);
                settingsPanelAnimator.AnimateOpen();
                UpdateXPPreview();
            }
        }
        else
        {
            settingsPanel.SetActive(true);
            SetupSettingsPanel();
            if (levelController != null) levelController.UpdatePreviewBar(currentGame.type);
            UpdateXPPreview();
        }
    }

    private void ResetGameSettingsDefault()
    {
        // Базовая сложность
        GameSettings.CurrentDifficulty = Difficulty.Medium;

        // Специфичные настройки
        GameSettings.RoundsCount = 1;
        GameSettings.KlondikeDrawCount = 1;
        GameSettings.SpiderSuitCount = 1;
        GameSettings.YukonRussian = false;
        GameSettings.MonteCarlo4Ways = false;
        GameSettings.MontanaHard = false;

        // Если это Паук, нужно убедиться, что сложность соответствует мастям
        if (currentGame.type == GameType.Spider)
        {
            if (GameSettings.CurrentDifficulty == Difficulty.Hard)
                GameSettings.CurrentDifficulty = Difficulty.Medium;
        }
        // --- ДЕЛАЕМ КАК У ПАУКА ---
        else if (currentGame.type == GameType.Octagon)
        {
            if (GameSettings.CurrentDifficulty != Difficulty.Medium)
                GameSettings.CurrentDifficulty = Difficulty.Medium;
        }
        else
        {
            // Для остальных игр разблокируем все сложности
            foreach (var btn in diffButtons) if (btn) btn.interactable = true;
            UpdateDifficultyVisuals();
        }
    }

    public void OnBackClicked()
    {
        // 1. Звук клика «Назад» (акцент на действии)
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Back");

        // 2. Звук улетающей панели (акцент на анимации)
        if (AudioManager.Instance != null)
        {
            // Используем тот же тайминг: 0.25 сек игры + 0.15 сек затухания
            AudioManager.Instance.PlaySoundWithAutoFade("Panel_Slide_Out", 0.25f, 0.15f);
        }
        if (settingsPanelAnimator != null) settingsPanelAnimator.AnimateClose();
        else settingsPanel.SetActive(false);

        if (cardAnimator != null) cardAnimator.ResetGrid();
        if (levelController != null) levelController.HideAllPreviews();

        // Запускаем анимацию обратно К "StartMarker"
        MoveButtonsToTarget(false);
    }

    private void MoveButtonsToTarget(bool toSettingsMode)
    {
        if (buttonsMoveCoroutine != null) StopCoroutine(buttonsMoveCoroutine);
        buttonsMoveCoroutine = StartCoroutine(AnimateButtonsRoutine(toSettingsMode));
    }

    private IEnumerator AnimateButtonsRoutine(bool toSettingsMode)
    {
        // Проверка ссылок, чтобы не было ошибок
        if (!leaderboardButton || !statsButton || !lbStartMarker || !lbEndMarker || !statsStartMarker || !statsEndMarker)
            yield break;

        float duration = 0.4f;
        float elapsed = 0f;

        // Откуда летим (текущая позиция, чтобы не дергалось, если анимация прервана)
        Vector3 startPosL = leaderboardButton.position;
        Vector3 startPosS = statsButton.position;

        // Куда летим
        Vector3 targetPosL = toSettingsMode ? lbEndMarker.position : lbStartMarker.position;
        Vector3 targetPosS = toSettingsMode ? statsEndMarker.position : statsStartMarker.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Используем .position (Global World Position) для точного совпадения с маркером
            leaderboardButton.position = Vector3.Lerp(startPosL, targetPosL, smoothT);
            statsButton.position = Vector3.Lerp(startPosS, targetPosS, smoothT);

            yield return null;
        }

        leaderboardButton.position = targetPosL;
        statsButton.position = targetPosS;
    }

    // --- OVERLAYS (Магазин, Лидерборд и т.д.) ---

    public void OnGlobalSettingsClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        ToggleOverlay(globalSettingsPanel, true);
    }
    public void OnLeaderboardClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        // 1. Понимаем контекст: открыта ли панель настроек конкретной игры?
        bool isGameSpecific = settingsPanel != null && settingsPanel.activeSelf;

        // 2. Формируем техническое имя лидерборда (оно полетит в Яндекс)
        string lbName = isGameSpecific ? currentGame.type.ToString() : "Global";

        // 3. Отправляем команду в наш контроллер
        if (dynamicLeaderboard != null)
        {
            dynamicLeaderboard.LoadLeaderboard(lbName);
        }

        ToggleOverlay(leaderboardPanel, true);
    }
    public void OnShopClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        ToggleOverlay(shopPanel, true);
    }
    public void OnDailyQuestsClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        ToggleOverlay(dailyQuestsPanel, true);
    }

    public void OnStatisticsClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        // 1. Узнаем статус игрока
        bool isPremium = StatisticsManager.Instance != null && StatisticsManager.Instance.IsUserPremium;

        // 2. Понимаем контекст: открыта ли панель конкретной игры?
        // (Если settingsPanel активна — значит игрок выбрал пасьянс)
        bool isGameSpecific = settingsPanel != null && settingsPanel.activeSelf;

        // 3. Выбираем нужную панель из 4-х
        GameObject panelToOpen = null;

        if (isGameSpecific)
        {
            panelToOpen = isPremium ? gamePremiumStatsPanel : gameBasicStatsPanel;
        }
        else
        {
            panelToOpen = isPremium ? appPremiumStatsPanel : appBasicStatsPanel;
        }

        // 4. Открываем и передаем данные
        if (panelToOpen != null)
        {
            ToggleOverlay(panelToOpen, true);

            // Если открыли статистику конкретной игры, подкидываем ей GameType
            if (isGameSpecific)
            {
                var premiumUI = panelToOpen.GetComponent<StatisticsUI>();
                if (premiumUI != null) premiumUI.ShowStatsForGame(currentGame.type);

                var basicUI = panelToOpen.GetComponent<BasicStatisticsUI>();
                if (basicUI != null) basicUI.ShowStatsForGame(currentGame.type);
            }
            else
            {
                // Если открыли ОБЩУЮ статистику приложения
                // (здесь позже вызовешь метод скрипта общей статистики, если он будет нужен)
                // Пример: panelToOpen.GetComponent<AppGlobalStatsUI>()?.ShowGlobalStats();
            }
        }
    }

    public void OnCloseOverlayClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Back");
        CloseAllOverlaysAnimated();

        if (!settingsPanel.activeSelf)
        {
            mainSelectionPanel.SetActive(true);
        }
    }

    private void ToggleOverlay(GameObject panel, bool show)
    {
        if (panel == null) return;

        // --- НОВОЕ: Эксклюзивное открытие ---
        // Если мы открываем панель, заставляем все остальные открытые панели закрыться
        if (show)
        {
            if (globalSettingsPanel && globalSettingsPanel.activeSelf && globalSettingsPanel != panel) ToggleOverlay(globalSettingsPanel, false);
            if (appBasicStatsPanel && appBasicStatsPanel.activeSelf && appBasicStatsPanel != panel) ToggleOverlay(appBasicStatsPanel, false);
            if (appPremiumStatsPanel && appPremiumStatsPanel.activeSelf && appPremiumStatsPanel != panel) ToggleOverlay(appPremiumStatsPanel, false);
            if (gameBasicStatsPanel && gameBasicStatsPanel.activeSelf && gameBasicStatsPanel != panel) ToggleOverlay(gameBasicStatsPanel, false);
            if (gamePremiumStatsPanel && gamePremiumStatsPanel.activeSelf && gamePremiumStatsPanel != panel) ToggleOverlay(gamePremiumStatsPanel, false);
            if (leaderboardPanel && leaderboardPanel.activeSelf && leaderboardPanel != panel) ToggleOverlay(leaderboardPanel, false);
            if (shopPanel && shopPanel.activeSelf && shopPanel != panel) ToggleOverlay(shopPanel, false);
            if (dailyQuestsPanel && dailyQuestsPanel.activeSelf && dailyQuestsPanel != panel) ToggleOverlay(dailyQuestsPanel, false);
        }

        // Если панель уже анимируется, прерываем старую анимацию для плавного реверса
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

        // <--- ИСПОЛЬЗУЕМ НОВЫЙ МЕТОД С ЗАТУХАНИЕМ --->
        if (AudioManager.Instance != null)
        {
            string soundToPlay = show ? "Panel_Slide_In" : "Panel_Slide_Out";

            // Начинаем заглушать звук за 0.15 сек до конца анимации
            float delay = overlayAnimDuration - 0.15f;
            if (delay < 0) delay = 0;

            AudioManager.Instance.PlaySoundWithAutoFade(soundToPlay, delay, 0.15f);
        }

        Vector2 centerPos = panelInitialPositions.ContainsKey(panel) ? panelInitialPositions[panel] : Vector2.zero;
        Vector2 leftPos = centerPos + new Vector2(-overlayFlyDistanceX, 0);  // Точка слева за экраном
        Vector2 rightPos = centerPos + new Vector2(overlayFlyDistanceX, 0); // Точка справа за экраном

        if (show)
        {
            // Если панель полностью закрыта, кидаем её влево, чтобы она вылетела оттуда
            if (!panel.activeSelf) rt.anchoredPosition = leftPos;
            panel.SetActive(true);
        }

        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = show ? centerPos : rightPos;

        float elapsed = 0f;
        while (elapsed < overlayAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / overlayAnimDuration;

            // Используем Cubic Ease-Out для вылета (быстро появляется) 
            // и Cubic Ease-In для улета (плавно начинает исчезать)
            float curveT = show ? (1f - Mathf.Pow(1f - t, 3)) : (t * t * t);

            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, curveT);
            yield return null;
        }

        rt.anchoredPosition = endPos;
        if (!show) panel.SetActive(false);
    }

    private void CloseAllOverlaysAnimated()
    {
        
        if (globalSettingsPanel && globalSettingsPanel.activeSelf) ToggleOverlay(globalSettingsPanel, false);
        if (appBasicStatsPanel && appBasicStatsPanel.activeSelf) ToggleOverlay(appBasicStatsPanel, false);
        if (appPremiumStatsPanel && appPremiumStatsPanel.activeSelf) ToggleOverlay(appPremiumStatsPanel, false);
        if (gameBasicStatsPanel && gameBasicStatsPanel.activeSelf) ToggleOverlay(gameBasicStatsPanel, false);
        if (gamePremiumStatsPanel && gamePremiumStatsPanel.activeSelf) ToggleOverlay(gamePremiumStatsPanel, false);
        if (leaderboardPanel && leaderboardPanel.activeSelf) ToggleOverlay(leaderboardPanel, false);
        if (shopPanel && shopPanel.activeSelf) ToggleOverlay(shopPanel, false);
        if (dailyQuestsPanel && dailyQuestsPanel.activeSelf) ToggleOverlay(dailyQuestsPanel, false);
        if (cardAppearancePanel && cardAppearancePanel.activeSelf) ToggleOverlay(cardAppearancePanel, false);
        if (cardBackSelectionPanel && cardBackSelectionPanel.activeSelf)
        {
            ToggleOverlayRight(cardBackSelectionPanel, false);
        }
        if (deckSelectionPanel.activeSelf) ToggleOverlayRight(deckSelectionPanel, false);
    }

    private void CloseAllOverlaysInstant()
    {
        if (globalSettingsPanel) globalSettingsPanel.SetActive(false);
        if (appBasicStatsPanel) appBasicStatsPanel.SetActive(false);
        if (appPremiumStatsPanel) appPremiumStatsPanel.SetActive(false);
        if (gameBasicStatsPanel) gameBasicStatsPanel.SetActive(false);
        if (gamePremiumStatsPanel) gamePremiumStatsPanel.SetActive(false);
        if (leaderboardPanel) leaderboardPanel.SetActive(false);
        if (shopPanel) shopPanel.SetActive(false);
        if (dailyQuestsPanel) dailyQuestsPanel.SetActive(false);
        if (cardAppearancePanel) cardAppearancePanel.SetActive(false);
        if (cardBackSelectionPanel) cardBackSelectionPanel.SetActive(false);
        if (deckSelectionPanel) deckSelectionPanel.SetActive(false);
    }

    // --- НАСТРОЙКА ПАНЕЛИ ---

    private void SetupSettingsPanel()
    {
        // 1. Включаем нужные контейнеры
        SetContainerActive(difficultyContainer, currentGame.showDifficulty);
        SetContainerActive(suitSelectionContainer, currentGame.showSuitSelector);
        SetContainerActive(roundsSelectionContainer, currentGame.showRoundsSelector);

        SetContainerActive(drawModeContainer, currentGame.showDrawMode);
        SetContainerActive(yukonModeContainer, currentGame.showYukonModes);
        SetContainerActive(monteCarloModeContainer, currentGame.showMonteCarloModes);
        SetContainerActive(montanaModeContainer, currentGame.showMontanaModes);
        SetContainerActive(noOptionsContainer, currentGame.showNoOptionsPanel);

        // 2. Обновляем визуал (цвета кнопок)
        UpdateDifficultyVisuals();

        if (currentGame.showDrawMode) UpdateDrawModeVisuals();
        if (currentGame.showSuitSelector) UpdateSuitsVisuals(GameSettings.SpiderSuitCount);
        if (currentGame.showRoundsSelector) UpdateRoundsVisuals();
        if (currentGame.showYukonModes) UpdateYukonVisuals();
        if (currentGame.showMonteCarloModes) UpdateMonteCarloVisuals();
        if (currentGame.showMontanaModes) UpdateMontanaVisuals();

        // --- КАК У ПАУКА: Блокировка сложности при открытии панели ---
        if (currentGame.type == GameType.Spider)
        {
            ValidateSpiderConstraints(GameSettings.SpiderSuitCount);
        }
        else if (currentGame.type == GameType.Octagon)
        {
            ValidateOctagonConstraints();
        }
        else
        {
            // Для остальных игр разблокируем все сложности
            foreach (var btn in diffButtons) if (btn) btn.interactable = true;
            UpdateDifficultyVisuals();
        }
    }

    private void SetContainerActive(GameObject container, bool active)
    {
        if (container != null) container.SetActive(active);
    }

    // -----------------------------------------------------------------------
    // KLONDIKE (Draw 1 / 3)
    // -----------------------------------------------------------------------
    public void OnDrawModeClicked(int count) // 1 или 3
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click"); // <--- ДОБАВИТЬ
        GameSettings.KlondikeDrawCount = count;
        UpdateDrawModeVisuals();
        UpdateXPPreview();
    }

    private void UpdateDrawModeVisuals()
    {
        SetButtonState(draw1Button, GameSettings.KlondikeDrawCount == 1);
        SetButtonState(draw3Button, GameSettings.KlondikeDrawCount == 3);
    }

    // -----------------------------------------------------------------------
    // YUKON (Classic / Russian)
    // -----------------------------------------------------------------------
    public void OnYukonModeClicked(int mode) // 0=Classic, 1=Russian
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click"); // <--- ДОБАВИТЬ
        GameSettings.YukonRussian = (mode == 1);
        UpdateYukonVisuals();
        UpdateXPPreview();
    }

    private void UpdateYukonVisuals()
    {
        SetButtonState(yukonClassicButton, !GameSettings.YukonRussian);
        SetButtonState(yukonRussianButton, GameSettings.YukonRussian);
    }

    // -----------------------------------------------------------------------
    // MONTE CARLO (8 Ways / 4 Ways)
    // -----------------------------------------------------------------------
    public void OnMonteCarloModeClicked(int mode) // 0=8Ways, 1=4Ways
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click"); // <--- ДОБАВИТЬ
        GameSettings.MonteCarlo4Ways = (mode == 1);
        UpdateMonteCarloVisuals();
        UpdateXPPreview();
    }

    private void UpdateMonteCarloVisuals()
    {
        SetButtonState(monteCarlo8WaysButton, !GameSettings.MonteCarlo4Ways);
        SetButtonState(monteCarlo4WaysButton, GameSettings.MonteCarlo4Ways);
    }

    // -----------------------------------------------------------------------
    // MONTANA (Classic / Hard)
    // -----------------------------------------------------------------------
    public void OnMontanaModeClicked(int mode) // 0=Classic, 1=Hard
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click"); // <--- ДОБАВИТЬ
        GameSettings.MontanaHard = (mode == 1);
        UpdateMontanaVisuals();
        UpdateXPPreview();
    }

    private void UpdateMontanaVisuals()
    {
        SetButtonState(montanaClassicButton, !GameSettings.MontanaHard);
        SetButtonState(montanaHardButton, GameSettings.MontanaHard);
    }

    // -----------------------------------------------------------------------
    // SPIDER & SUITS
    // -----------------------------------------------------------------------
    public void OnSuitClicked(int suitCount)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click"); // <--- ДОБАВИТЬ
        GameSettings.SpiderSuitCount = suitCount;
        UpdateSuitsVisuals(suitCount);
        ValidateSpiderConstraints(suitCount);
        UpdateXPPreview();
    }

    private void UpdateSuitsVisuals(int count)
    {
        // 1->index 0, 2->index 1, 4->index 2
        int selectedIndex = (count == 1) ? 0 : (count == 2 ? 1 : 2);

        for (int i = 0; i < suitButtons.Length; i++)
        {
            if (suitButtons[i] == null) continue;
            SetButtonState(suitButtons[i], i == selectedIndex);
        }
    }

    private void ValidateSpiderConstraints(int suitCount)
    {
        // Сначала включаем все кнопки сложности
        foreach (var btn in diffButtons) if (btn) btn.interactable = true;

        if (suitCount == 1)
        {
            // Для 1 масти блокируем Hard (индекс 2)
            if (diffButtons.Length > 2) diffButtons[2].interactable = false;

            // Если выбран Hard, переключаем на Medium
            if (GameSettings.CurrentDifficulty == Difficulty.Hard)
                SetDifficulty((int)Difficulty.Medium);
        }
        else if (suitCount == 4)
        {
            // Для 4 мастей блокируем Easy (индекс 0)
            if (diffButtons.Length > 0) diffButtons[0].interactable = false;

            // Если выбран Easy, переключаем на Medium
            if (GameSettings.CurrentDifficulty == Difficulty.Easy)
                SetDifficulty((int)Difficulty.Medium);
        }

        UpdateDifficultyVisuals();
    }
    private void ValidateOctagonConstraints()
    {
        // Сначала включаем все кнопки сложности (как у Паука)
        foreach (var btn in diffButtons) if (btn) btn.interactable = true;

        // Блокируем Easy (индекс 0) и Hard (индекс 2)
        if (diffButtons.Length > 0 && diffButtons[0] != null) diffButtons[0].interactable = false;
        if (diffButtons.Length > 2 && diffButtons[2] != null) diffButtons[2].interactable = false;

        // Если выбран Easy или Hard, переключаем на Medium (как у Паука)
        if (GameSettings.CurrentDifficulty == Difficulty.Easy || GameSettings.CurrentDifficulty == Difficulty.Hard)
        {
            SetDifficulty((int)Difficulty.Medium);
        }
        else
        {
            UpdateDifficultyVisuals();
        }
    }
    // -----------------------------------------------------------------------
    // ROUNDS
    // -----------------------------------------------------------------------
    public void OnRoundsClicked(int index) // 0, 1, 2
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click"); // <--- ДОБАВИТЬ
        GameSettings.RoundsCount = index + 1;
        UpdateRoundsVisuals();
        UpdateXPPreview();
    }

    private void UpdateRoundsVisuals()
    {
        int currentIndex = GameSettings.RoundsCount - 1;
        for (int i = 0; i < roundsButtons.Length; i++)
        {
            if (roundsButtons[i] == null) continue;
            SetButtonState(roundsButtons[i], i == currentIndex);
        }
    }

    // -----------------------------------------------------------------------
    // DIFFICULTY
    // -----------------------------------------------------------------------
    public void OnDifficultyClicked(int diffIndex)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click"); // <--- ДОБАВИТЬ
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
        for (int i = 0; i < diffButtons.Length; i++)
        {
            if (diffButtons[i] == null) continue;
            // Проверка на interactable нужна для логики Паука
            if (!diffButtons[i].interactable)
            {
                diffButtons[i].image.color = buttonDisabledColor;
            }
            else
            {
                SetButtonState(diffButtons[i], i == currentIndex);
            }
        }
    }

    // -----------------------------------------------------------------------
    // HELPER & START
    // -----------------------------------------------------------------------
    private void SetButtonState(Button btn, bool isSelected)
    {
        if (btn == null) return;

        // 1. Меняем фон
        btn.image.color = isSelected ? bgSelectedColor : bgNormalColor;

        // 2. Ищем текст внутри кнопки и меняем его цвет
        // Поддержка TextMeshPro
        var tmpText = btn.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.color = isSelected ? textSelectedColor : textNormalColor;
        }
        else
        {
            // Поддержка старого UI Text (на всякий случай)
            var legacyText = btn.GetComponentInChildren<Text>();
            if (legacyText != null)
            {
                legacyText.color = isSelected ? textSelectedColor : textNormalColor;
            }
        }
    }

    public void OnStartGameClicked()
    {
        // <--- ДОБАВИТЬ ЭТО: Торжественный клик старта --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Game_Start");
        // [FIX] Гарантируем, что при нажатии "Играть" туториал выключен
        GameSettings.IsTutorialMode = false;

        // 1. Визуальный эффект нажатия
        SetButtonState(startButton, false);

        // 2. Возврат раздачи в пул (если используется)
        if (DealCacheSystem.Instance != null) DealCacheSystem.Instance.ReturnActiveDealToQueue();

        if (string.IsNullOrEmpty(currentGame.sceneName))
        {
            Debug.LogError($"Scene name not set for this game: {currentGame.name}");
            return;
        }

        // 3. ЗАПУСК АНИМАЦИИ ВЫХОДА
        if (exitController != null)
        {
            startButton.interactable = false;
            if (tutorialButton != null) tutorialButton.interactable = false;

            exitController.PlayExitAnimation(currentGame.type, () =>
            {
                SceneManager.LoadScene(currentGame.sceneName);
            });
        }
        else
        {
            SceneManager.LoadScene(currentGame.sceneName);
        }
    }
    public void OnTutorialButtonClicked()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Game_Start");
        // [FIX] Включаем режим обучения
        GameSettings.IsTutorialMode = true;

        if (tutorialButton != null) SetButtonState(tutorialButton, false);

        if (string.IsNullOrEmpty(currentGame.sceneName)) return;

        if (exitController != null)
        {
            if (startButton != null) startButton.interactable = false;
            if (tutorialButton != null) tutorialButton.interactable = false;

            exitController.PlayExitAnimation(currentGame.type, () =>
            {
                SceneManager.LoadScene(currentGame.sceneName);
            });
        }
        else
        {
            SceneManager.LoadScene(currentGame.sceneName);
        }
    }
    public void OnSoundOnClicked()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.isMuted)
        {
            AudioManager.Instance.SetMute(false);
            AudioManager.Instance.PlaySound("UI_Click"); // Звук клика (т.к. звук только что включился)
            UpdateSoundButtonsVisuals(false);
        }
    }
    public void OnSoundOffClicked()
    {
        if (AudioManager.Instance != null && !AudioManager.Instance.isMuted)
        {
            // Сначала играем звук клика, а потом глушим систему
            AudioManager.Instance.PlaySound("UI_Click");
            AudioManager.Instance.SetMute(true);
            UpdateSoundButtonsVisuals(false);
        }
    }
    private void UpdateSoundButtonsVisuals(bool instant = false)
    {
        if (AudioManager.Instance == null) return;
        bool isMuted = AudioManager.Instance.isMuted;

        // 1. Меняем цвета (фон и текст вашим готовым методом)
        SetButtonState(soundOnButton, !isMuted);
        SetButtonState(soundOffButton, isMuted);

        // --- НОВОЕ: Меняем цвет иконки динамика ---
        TintButtonIcon(soundOnButton, !isMuted);
        TintButtonIcon(soundOffButton, isMuted);

        // 2. Меняем размеры (плавно или мгновенно при старте)
        if (soundOnButton != null)
        {
            Vector3 targetScale = !isMuted ? buttonActiveScale : Vector3.one;
            AnimateOrSetScale(soundOnButton.GetComponent<RectTransform>(), targetScale, instant);
        }

        if (soundOffButton != null)
        {
            Vector3 targetScale = isMuted ? buttonActiveScale : Vector3.one;
            AnimateOrSetScale(soundOffButton.GetComponent<RectTransform>(), targetScale, instant);
        }
    }

    // --- НОВЫЙ МЕТОД ---
    // Вспомогательный метод для покраски иконки (пропуская фон кнопки)
    private void TintButtonIcon(Button btn, bool isSelected)
    {
        if (btn == null) return;

        // Получаем все компоненты Image внутри кнопки (включая фон самой кнопки)
        Image[] images = btn.GetComponentsInChildren<Image>();

        foreach (var img in images)
        {
            // Если картинка НЕ является фоном самой кнопки, значит это наша иконка
            if (img != btn.image)
            {
                // Красим ее в цвета текста из ваших настроек
                img.color = isSelected ? textSelectedColor : textNormalColor;
            }
        }
    }
    public void OnLanguageButtonClicked(int index)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        if (index < 0 || index >= languageCodes.Length) return;

        string code = languageCodes[index];

        // Меняем язык через ваш существующий менеджер
        if (LocalizationManager.instance != null)
        {
            LocalizationManager.instance.SetLanguage(code);
        }

        // Запускаем обновление интерфейса (плавное изменение размера и цвета)
        UpdateLanguageButtonsVisuals(false);
    }

    private void UpdateLanguageButtonsVisuals(bool instant = false)
    {
        if (languageButtons == null || languageButtons.Length == 0) return;

        // Узнаем текущий язык из менеджера (если он еще не прогрузился, берем дефолтный "en")
        string currentLang = "en";
        if (LocalizationManager.instance != null)
        {
            currentLang = LocalizationManager.instance.CurrentLanguage;
        }

        for (int i = 0; i < languageButtons.Length; i++)
        {
            if (languageButtons[i] == null) continue;

            // Если код кнопки совпадает с текущим языком - она активна
            bool isSelected = (languageCodes[i] == currentLang);

            // 1. Меняем цвета (используем ваш готовый метод)
            SetButtonState(languageButtons[i], isSelected);

            // 2. Меняем размеры (плавно или мгновенно)
            Vector3 targetScale = isSelected ? buttonActiveScale : Vector3.one;
            AnimateOrSetScale(languageButtons[i].GetComponent<RectTransform>(), targetScale, instant);
        }
    }
    private void AnimateOrSetScale(RectTransform target, Vector3 targetScale, bool instant)
    {
        if (target == null) return;

        if (instant)
        {
            target.localScale = targetScale;
            return;
        }

        // Останавливаем старую анимацию для этой кнопки, если она была
        if (buttonScaleCoroutines.ContainsKey(target) && buttonScaleCoroutines[target] != null)
        {
            StopCoroutine(buttonScaleCoroutines[target]);
        }

        buttonScaleCoroutines[target] = StartCoroutine(ScaleButtonRoutine(target, targetScale));
    }

    private IEnumerator ScaleButtonRoutine(RectTransform target, Vector3 targetScale)
    {
        float elapsed = 0f;
        Vector3 startScale = target.localScale;

        while (elapsed < buttonAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / buttonAnimDuration;
            // Используем SmoothStep для приятной "мягкости" анимации
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            target.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            yield return null;
        }

        target.localScale = targetScale;
    }
    public void OpenBackgroundSelection()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        // Настройки улетают ВВЕРХ, а панель фонов выезжает СНИЗУ
        // Проверяем, что обе панели назначены в инспекторе
        if (globalSettingsPanel != null && backgroundSelectionPanel != null)
        {
            StartCoroutine(VerticalTransitionRoutine(globalSettingsPanel, backgroundSelectionPanel, true));
        }
    }
    public void CloseBackgroundSelection()
    {
        // ДОБАВЛЕНО: Звук клика при закрытии
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        // Панель фонов уезжает ВНИЗ, а настройки возвращаются СВЕРХУ
        if (backgroundSelectionPanel != null && globalSettingsPanel != null)
        {
            StartCoroutine(VerticalTransitionRoutine(backgroundSelectionPanel, globalSettingsPanel, false));
        }
    }
    // --- НОВЫЕ МЕТОДЫ ДЛЯ ПАНЕЛИ КАРТ ---
    public void OpenCardAppearance()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        // Настройки улетают вверх, панель карт выезжает снизу
        if (globalSettingsPanel != null && cardAppearancePanel != null)
        {
            StartCoroutine(VerticalTransitionRoutine(globalSettingsPanel, cardAppearancePanel, true));
        }
    }

    public void CloseCardAppearance()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        // Панель карт улетает вниз, настройки выезжают сверху
        if (cardAppearancePanel != null && globalSettingsPanel != null)
        {
            StartCoroutine(VerticalTransitionRoutine(cardAppearancePanel, globalSettingsPanel, false));
        }
    }
    private IEnumerator VerticalTransitionRoutine(GameObject panelToHide, GameObject panelToShow, bool isOpeningBg)
    {
        // 1. ЗВУК СКОЛЬЗЕНИЯ
        if (AudioManager.Instance != null)
        {
            string soundToPlay = isOpeningBg ? "Panel_Slide_In" : "Panel_Slide_Out";
            float delay = overlayAnimDuration - 0.15f;
            if (delay < 0) delay = 0;
            AudioManager.Instance.PlaySoundWithAutoFade(soundToPlay, delay, 0.15f);
        }

        RectTransform hideRt = panelToHide.GetComponent<RectTransform>();
        RectTransform showRt = panelToShow.GetComponent<RectTransform>();

        // Определяем идеальный центр (куда панели должны приходить)
        Vector2 hideCenterPos = panelInitialPositions.ContainsKey(panelToHide) ? panelInitialPositions[panelToHide] : Vector2.zero;
        Vector2 showCenterPos = panelInitialPositions.ContainsKey(panelToShow) ? panelInitialPositions[panelToShow] : Vector2.zero;

        // Определяем цели
        // Если открываем фоны (isOpeningBg=true): прячем настройки ВВЕРХ, показываем фоны снизу
        // Если закрываем фоны (isOpeningBg=false): прячем фоны ВНИЗ, показываем настройки сверху
        Vector2 hideTargetPos = hideCenterPos + new Vector2(0, isOpeningBg ? verticalFlyDistance : -verticalFlyDistance);
        Vector2 showStartPos = showCenterPos + new Vector2(0, isOpeningBg ? -verticalFlyDistance : verticalFlyDistance);

        // Подготовка показываемой панели
        panelToShow.SetActive(true);
        showRt.anchoredPosition = showStartPos;

        // Стартовые точки для Лерпа (откуда начинаем движение в данный момент)
        Vector2 hideStartPos = hideRt.anchoredPosition;

        float elapsed = 0f;
        while (elapsed < overlayAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / overlayAnimDuration;

            // Используем ту же кривую, что и в горизонтальных оверлеях
            float curveT = isOpeningBg ? (1f - Mathf.Pow(1f - t, 3)) : (t * t * t);

            hideRt.anchoredPosition = Vector2.LerpUnclamped(hideStartPos, hideTargetPos, curveT);
            showRt.anchoredPosition = Vector2.LerpUnclamped(showStartPos, showCenterPos, curveT);
            yield return null;
        }

        // Финальная фиксация
        hideRt.anchoredPosition = hideTargetPos;
        showRt.anchoredPosition = showCenterPos;
        panelToHide.SetActive(false);
    }
    public void OpenCardBackSelection()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        // Вызываем ToggleOverlay, но с флагом появления справа
        ToggleOverlayRight(cardBackSelectionPanel, true);
    }
    public void CloseCardBackSelection()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Back");
        ToggleOverlayRight(cardBackSelectionPanel, false);
    }

    // Специальная версия Toggle для правой панели
    private void ToggleOverlayRight(GameObject panel, bool show)
    {
        if (panel == null) return;

        if (activePanelCoroutines.ContainsKey(panel) && activePanelCoroutines[panel] != null)
        {
            StopCoroutine(activePanelCoroutines[panel]);
        }

        activePanelCoroutines[panel] = StartCoroutine(AnimateOverlayRightRoutine(panel, show));
    }

    private IEnumerator AnimateOverlayRightRoutine(GameObject panel, bool show)
    {
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt == null) yield break;

        // Звук скольжения
        if (AudioManager.Instance != null)
        {
            string soundToPlay = show ? "Panel_Slide_In" : "Panel_Slide_Out";
            float delay = overlayAnimDuration - 0.15f;
            AudioManager.Instance.PlaySoundWithAutoFade(soundToPlay, Mathf.Max(0, delay), 0.15f);
        }

        Vector2 centerPos = panelInitialPositions.ContainsKey(panel) ? panelInitialPositions[panel] : Vector2.zero;
        Vector2 rightPos = centerPos + new Vector2(overlayFlyDistanceX, 0); // Точка справа за экраном

        if (show)
        {
            // Если открываем — ставим панель СТРОГО СПРАВА перед началом анимации
            if (!panel.activeSelf) rt.anchoredPosition = rightPos;
            panel.SetActive(true);
        }

        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = show ? centerPos : rightPos;

        float elapsed = 0f;
        while (elapsed < overlayAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / overlayAnimDuration;

            // Используем Cubic Ease для плавности
            float curveT = show ? (1f - Mathf.Pow(1f - t, 3)) : (t * t * t);

            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, curveT);
            yield return null;
        }

        rt.anchoredPosition = endPos;
        if (!show) panel.SetActive(false);
        activePanelCoroutines[panel] = null;
    }
    public void OpenDeckSelection()
    {
        // Открываем оверлей справа
        ToggleOverlayRight(deckSelectionPanel, true);
    }

    // Вызывается кнопкой "Принять" или "Закрыть" на самой панели колод
    public void CloseDeckSelection()
    {
        // Закрываем оверлей
        ToggleOverlayRight(deckSelectionPanel, false);
    }
}