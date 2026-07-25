using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;


public class GameUIController : MonoBehaviour
{
    [Header("Main Panels")]
    public GameObject winPanel;
    public GameObject defeatPanel;
    public GameObject basicStatisticsPanel;   // <--- ИЗМЕНЕНО: Базовая статистика
    public GameObject premiumStatisticsPanel;
    public GameObject settingsPanel;

    [Header("Confirmation Panels")]
    public GameObject exitConfirmationPanel;
    public GameObject newGameConfirmationPanel;

    [Header("Win Panel Stats")]
    public TMP_Text winDifficultyText;
    public TMP_Text winScoreText;
    public TMP_Text winTimeText;
    public TMP_Text winMovesText;
    public TMP_Text winXPText; // "You will get {0} XP"
    public TMP_Text winEarnedXPText;
    [Header("Win Panel Animation")]
    public RectTransform winCardRect;
    public XPProgressBar winLevelBar;
    private Vector2 winCardDefaultPos;
    [Header("Defeat Panel Ads UI")]
    [Tooltip("Перетащи сюда объекты иконок рекламы (телевизоры) с кнопок отмены")]
    public GameObject undoOneAdIcon;
    public GameObject undoAllAdIcon;
    [Header("Text RectTransforms for Ads")]
    [Header("Text RectTransforms for Ads")]
    [Tooltip("Перетащи сюда сами объекты текста с кнопок отмены")]
    public RectTransform undoOneTextRect;
    public RectTransform undoAllTextRect;

    [Tooltip("Значение Right из инспектора, когда картинки НЕТ")]
    public float textPaddingNormal = 16.35f;

    [Tooltip("Значение Right из инспектора, когда картинка ЕСТЬ")]
    public float textPaddingWithAd = 55.51f;
    [Header("Other Bars")]
    public XPProgressBar localLevelBar;

    [Header("Notifications")]
    public LevelUpNotification globalLevelUpPopup;

    // --- NEW BLOCK: SETTINGS CONTROLS INSIDE WIN/DEFEAT ---
    [Header("Win/Defeat Controls (Visuals)")]
    public Color btnSelectedColor = new Color32(255, 176, 26, 255); // Orange
    public Color btnNormalColor = new Color32(154, 95, 64, 255);    // Brown
    public Color textSelectedColor = new Color32(36, 20, 12, 255);
    public Color textNormalColor = new Color32(192, 192, 192, 255);

    [Header("Win/Defeat Buttons References")]
    // Containers to hide irrelevant settings (e.g., Draw Mode in Spider)
    public GameObject winDifficultyContainer;
    public GameObject winDrawContainer;     // Klondike
    public GameObject winSuitContainer;     // Spider (if added)

    // The buttons themselves
    public Button[] winDiffButtons;   // 0-Easy, 1-Medium, 2-Hard
    public Button[] winDrawButtons;   // 0-(Draw1), 1-(Draw3)
    // public Button[] winSuitButtons; // For Spider if needed

    [Header("XP Preview Config")]
    public TMP_Text xpPreviewText;
    public string xpPreviewLocKey = "xp_reward_preview"; // "You will get {0} XP"
    private Dictionary<GameObject, Vector2> panelInitialPositions = new Dictionary<GameObject, Vector2>();
    [Header("References")]
    private ICardGameMode activeGameMode;
    public UndoManager undoManager;
    [Header("Effects")]
    public SceneExitAnimator exitAnimator;
    [Header("Colors Extension")]
    public Color buttonDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Цвет заблокированной кнопки Паука
    [Header("Settings Containers")]
    // Контейнеры внутри SettingsPanel
    public GameObject settingsDifficultyContainer;
    public GameObject settingsDrawContainer;     // Klondike
    public GameObject settingsSuitContainer;
    public GameObject settingsRoundsContainer;
    public GameObject settingsYukonContainer;
    public GameObject settingsMonteCarloContainer;
    public GameObject settingsMontanaContainer;
    [Header("New Game Settings Containers")]
    public GameObject newGameSettingsDiffContainer;
    public GameObject newGameSettingsDrawContainer;
    public GameObject newGameSettingsSuitContainer;
    public GameObject newGameSettingsRoundsContainer;
    public GameObject newGameSettingsYukonContainer;
    public GameObject newGameSettingsMonteCarloContainer;
    public GameObject newGameSettingsMontanaContainer;

    [Header("Settings Buttons")]
    public Button[] settingsDiffButtons;   // 0-Easy, 1-Medium, 2-Hard
    public Button[] settingsDrawButtons;   // 0-(Draw1), 1-(Draw3)
    public Button[] settingsSuitButtons;
    public Button[] settingsRoundsButtons;
    public Button settingsYukonClassicBtn;
    public Button settingsYukonRussianBtn;
    public Button settingsMonteCarlo8Btn;
    public Button settingsMonteCarlo4Btn;
    public Button settingsMontanaClassicBtn;
    public Button settingsMontanaHardBtn;

    // --- NEW GAME SETTINGS PANEL (SEPARATE PANEL) ---
    [Header("New Game Settings Panel")]
    public GameObject newGameSettingsPanel;

   

    [Header("New Game Settings Buttons")]
    public Button[] newGameSettingsDiffButtons; // 0-Easy, 1-Medium, 2-Hard
    public Button[] newGameSettingsDrawButtons; // 0-Draw1, 1-Draw3
    public Button[] newGameSettingsSuitButtons;
    public Button[] newGameSettingsRoundsButtons;
    public Button newGameSettingsYukonClassicBtn;
    public Button newGameSettingsYukonRussianBtn;
    public Button newGameSettingsMonteCarlo8Btn;
    public Button newGameSettingsMonteCarlo4Btn;
    public Button newGameSettingsMontanaClassicBtn;
    public Button newGameSettingsMontanaHardBtn;
    [Header("New Game XP Preview")]
    public TMP_Text newGameXPPreviewText;
    private Coroutine winSequenceCoroutine;
    [Header("Win Panel Tutorial UI")]
    [Tooltip("Поместите сюда 4 обычные кнопки: Новая игра, В меню(мелкая), Статистика, Настройки")]
    public GameObject[] standardWinButtons;
    [Tooltip("Поместите сюда вашу новую БОЛЬШУЮ кнопку В меню")]
    public GameObject tutorialBigMenuButton;
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

        // Регистрируем все панели, включая Settings
        RegisterAndHidePanel(winPanel);
        RegisterAndHidePanel(defeatPanel);
        RegisterAndHidePanel(basicStatisticsPanel);   // <--- РЕГИСТРИРУЕМ БАЗОВУЮ
        RegisterAndHidePanel(premiumStatisticsPanel);
        RegisterAndHidePanel(settingsPanel); // [ВАЖНО] Панель настроек должна быть здесь
        RegisterAndHidePanel(newGameSettingsPanel);
        RegisterAndHidePanel(exitConfirmationPanel);
        RegisterAndHidePanel(newGameConfirmationPanel);


        if (winCardRect != null)
        {
            winCardDefaultPos = winCardRect.anchoredPosition;
            winCardRect.gameObject.SetActive(false);
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnLevelUp += HandleLevelUp;
    }
    private void Update()
    {
        // Если панель поражения открыта, проверяем статус бесплатной рекламы
        if (defeatPanel != null && defeatPanel.activeSelf)
        {
            UpdateDefeatAdVisuals();
        }
    }

    private void UpdateDefeatAdVisuals()
    {
        // Проверяем, существует ли AdManager и активен ли льготный период
        bool isRewardFree = AdManager.Instance != null && AdManager.Instance.IsRewardFree();

        // 1. Включаем/выключаем иконки
        if (undoOneAdIcon != null) undoOneAdIcon.SetActive(!isRewardFree);
        if (undoAllAdIcon != null) undoAllAdIcon.SetActive(!isRewardFree);

        // 2. Определяем нужный отступ. 
        // Если бесплатно (иконки нет) -> отступ обычный (16.35)
        // Если платно (иконка есть) -> отступ большой (55.51)
        float currentRightPadding = isRewardFree ? textPaddingNormal : textPaddingWithAd;

        // 3. Применяем отступ к текстам. В коде значение Right — это отрицательный offsetMax.x!
        if (undoOneTextRect != null)
        {
            undoOneTextRect.offsetMax = new Vector2(-currentRightPadding, undoOneTextRect.offsetMax.y);
        }

        if (undoAllTextRect != null)
        {
            undoAllTextRect.offsetMax = new Vector2(-currentRightPadding, undoAllTextRect.offsetMax.y);
        }
    }

    private void OnEnable()
    {
        // Слушаем сообщение от AdManager об успешном просмотре
        AdManager.OnRewardEarned += HandleRewardEarned;
    }

    private void OnDisable()
    {
        AdManager.OnRewardEarned -= HandleRewardEarned;
    }

    // Этот метод сработает ТОЛЬКО если игрок досмотрел рекламу до конца
    // (Или если у него действует льготный бесплатный период / куплен Премиум)
    private void HandleRewardEarned(string rewardId)
    {
        if (rewardId == "undo_one")
        {
            ExecuteUndoOne();
        }
        else if (rewardId == "undo_all")
        {
            ExecuteUndoAll();
        }
    }
    private void RegisterAndHidePanel(GameObject panel)
    {
        if (panel == null) return;

        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt != null)
        {
            // Сохраняем координату, где панель стоит в редакторе
            if (!panelInitialPositions.ContainsKey(panel))
            {
                panelInitialPositions.Add(panel, rt.anchoredPosition);
            }
        }
        panel.SetActive(false);
    }
    private void OnDestroy()
    {
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnLevelUp -= HandleLevelUp;
        }
    }

    // --- WIN LOGIC ---

    public void OnGameWon(int manualMoves = -1)
    {
        if (defeatPanel) defeatPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (activeGameMode != null) activeGameMode.IsInputAllowed = false;
        
        // [FIX] Сохраняем ссылку на корутину, чтобы можно было её отменить
        if (winSequenceCoroutine != null) StopCoroutine(winSequenceCoroutine);
        winSequenceCoroutine = StartCoroutine(WinSequenceRoutine(manualMoves));
    }

    private IEnumerator WinSequenceRoutine(int manualMoves)
    {
        yield return new WaitForSeconds(1.0f);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Win_Sound");
        if (winPanel)
        {
            // [REMOVED] SetupControlPanel() - здесь больше нет кнопок

            // Только обновляем статистику и текст
            UpdateWinPanelStats(manualMoves);
            // XP Preview теперь в настройках, но если вы хотите показывать "Сколько дали", 
            // это делает UpdateWinPanelStats. 

            // 1. Подготовка карты
            if (winCardRect != null)
            {
                Vector2 startPos = winCardDefaultPos + new Vector2(1500f, 0f);
                winCardRect.anchoredPosition = startPos;
                var hover = winCardRect.GetComponent<CardHoverEffect>();
                if (hover != null) hover.SetSelectedMode(false);
                winCardRect.gameObject.SetActive(true);
            }

            // 2. Анимация панели
            TogglePanelAnimated(winPanel, true);

            // 3. Анимация карты
            if (winCardRect != null)
            {
                StartCoroutine(AnimateCardEntrance(winCardRect));
            }

            StartCoroutine(AnimateXPBarDelayed());
        }
    }


    // --- DEFEAT LOGIC ---

    public void OnGameLost()
    {
        if (winPanel != null && winPanel.activeSelf) return;

        // [FIX] Скрываем настройки, если они вдруг были открыты
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            TogglePanelAnimated(settingsPanel, false);
        }

        if (defeatPanel != null && !defeatPanel.activeSelf)
        {
            // [FIX] Больше не вызываем SetupControlPanel, так как кнопки настроек теперь в отдельной панели.
            // Только обновляем превью XP, если оно есть на панели поражения.
            UpdateXPPreviewText();

            TogglePanelAnimated(defeatPanel, true);
            if (activeGameMode != null) activeGameMode.IsInputAllowed = false;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Loss_Sound");
        }
    }
    // --- SETTINGS BUTTONS LOGIC (SYNC) ---

    public void OnSettingsClicked()
    {
        PlayClickSound();
        if (settingsPanel != null)
        {
            // [FIX] Если панель уже открыта (или находится в процессе анимации),
            // игнорируем нажатие. Это предотвращает баг "исчезла и снова выехала".
            if (settingsPanel.activeSelf) return;

            SetupSettingsPanel();
            TogglePanelAnimated(settingsPanel, true);
        }
    }
    public void OnCloseSettingsClicked()
    {
        PlayClickSound();
        if (settingsPanel != null)
        {
            TogglePanelAnimated(settingsPanel, false);
        }
    }

    private void SetupSettingsPanel()
    {
        if (activeGameMode == null) return;
        GameType type = activeGameMode.GameType;

        // Включаем нужные контейнеры
        if (settingsDrawContainer) settingsDrawContainer.SetActive(type == GameType.Klondike);
        if (settingsSuitContainer) settingsSuitContainer.SetActive(type == GameType.Spider);
        if (settingsRoundsContainer) settingsRoundsContainer.SetActive(type == GameType.Pyramid || type == GameType.TriPeaks);
        if (settingsYukonContainer) settingsYukonContainer.SetActive(type == GameType.Yukon);
        if (settingsMonteCarloContainer) settingsMonteCarloContainer.SetActive(type == GameType.MonteCarlo);
        if (settingsMontanaContainer) settingsMontanaContainer.SetActive(type == GameType.Montana);

        if (settingsDifficultyContainer) settingsDifficultyContainer.SetActive(true);

        // Ограничения для Паука
        if (type == GameType.Spider) ValidateSpiderConstraints(GameSettings.SpiderSuitCount, settingsDiffButtons);
        else EnableAllDifficulties(settingsDiffButtons);

        UpdateSettingsVisuals();
        UpdateXPPreviewText();
    }
    private void UpdateSettingsVisuals()
    {
        int diffIndex = (int)GameSettings.CurrentDifficulty;
        for (int i = 0; i < settingsDiffButtons.Length; i++)
        {
            if (settingsDiffButtons[i] != null)
            {
                if (!settingsDiffButtons[i].interactable) settingsDiffButtons[i].image.color = buttonDisabledColor;
                else SetButtonState(settingsDiffButtons[i], i == diffIndex);
            }
        }

        if (settingsDrawButtons != null && settingsDrawButtons.Length >= 2)
        {
            SetButtonState(settingsDrawButtons[0], GameSettings.KlondikeDrawCount == 1);
            SetButtonState(settingsDrawButtons[1], GameSettings.KlondikeDrawCount == 3);
        }
        if (settingsSuitButtons != null && settingsSuitButtons.Length >= 3)
        {
            SetButtonState(settingsSuitButtons[0], GameSettings.SpiderSuitCount == 1);
            SetButtonState(settingsSuitButtons[1], GameSettings.SpiderSuitCount == 2);
            SetButtonState(settingsSuitButtons[2], GameSettings.SpiderSuitCount == 4);
        }
        if (settingsRoundsButtons != null && settingsRoundsButtons.Length >= 3)
        {
            SetButtonState(settingsRoundsButtons[0], GameSettings.RoundsCount == 1);
            SetButtonState(settingsRoundsButtons[1], GameSettings.RoundsCount == 2);
            SetButtonState(settingsRoundsButtons[2], GameSettings.RoundsCount == 3);
        }

        SetButtonState(settingsYukonClassicBtn, !GameSettings.YukonRussian);
        SetButtonState(settingsYukonRussianBtn, GameSettings.YukonRussian);
        SetButtonState(settingsMonteCarlo8Btn, !GameSettings.MonteCarlo4Ways);
        SetButtonState(settingsMonteCarlo4Btn, GameSettings.MonteCarlo4Ways);
        SetButtonState(settingsMontanaClassicBtn, !GameSettings.MontanaHard);
        SetButtonState(settingsMontanaHardBtn, GameSettings.MontanaHard);
    }
    public void OnSettingsDifficultyClicked(int diffIndex)
    {
        if (settingsDiffButtons != null && diffIndex < settingsDiffButtons.Length && !settingsDiffButtons[diffIndex].interactable) return;
        PlayClickSound();
        GameSettings.CurrentDifficulty = (Difficulty)diffIndex;
        UpdateSettingsVisuals();
        UpdateXPPreviewText();
    }

    public void OnSettingsDrawModeClicked(int drawCount)
    {
        PlayClickSound();
        GameSettings.KlondikeDrawCount = drawCount;
        UpdateSettingsVisuals();
        UpdateXPPreviewText();
    }
    public void OnSettingsSuitClicked(int count) // Передаем 1, 2 или 4
    {
        PlayClickSound();
        GameSettings.SpiderSuitCount = count;
        ValidateSpiderConstraints(count, settingsDiffButtons);
        UpdateSettingsVisuals();
        UpdateXPPreviewText();
    }
    public void OnSettingsRoundsClicked(int index) // Передаем 0, 1 или 2 (будет 1, 2, 3 раунда)
    {
        PlayClickSound();
        GameSettings.RoundsCount = index + 1;
        UpdateSettingsVisuals();
        UpdateXPPreviewText();
    }
    public void OnSettingsYukonClicked(int mode) { PlayClickSound(); GameSettings.YukonRussian = (mode == 1); UpdateSettingsVisuals(); UpdateXPPreviewText(); }
    public void OnSettingsMonteCarloClicked(int mode) { PlayClickSound(); GameSettings.MonteCarlo4Ways = (mode == 1); UpdateSettingsVisuals(); UpdateXPPreviewText(); }
    public void OnSettingsMontanaClicked(int mode) { PlayClickSound(); GameSettings.MontanaHard = (mode == 1); UpdateSettingsVisuals(); UpdateXPPreviewText(); }

    // --- ДОБАВИТЬ: Обработчики кнопок для Настроек Новой Игры ---
    public void OnNewGameSettingsSuitClicked(int count) // Передаем 1, 2 или 4
    {
        PlayClickSound();
        GameSettings.SpiderSuitCount = count;
        ValidateSpiderConstraints(count, newGameSettingsDiffButtons);
        UpdateNewGameSettingsVisuals();
        UpdateNewGameXPPreview();
    }
    public void OnNewGameSettingsRoundsClicked(int index) // Передаем 0, 1 или 2
    {
        PlayClickSound();
        GameSettings.RoundsCount = index + 1;
        UpdateNewGameSettingsVisuals();
        UpdateNewGameXPPreview();
    }
    public void OnNewGameSettingsYukonClicked(int mode) { PlayClickSound(); GameSettings.YukonRussian = (mode == 1); UpdateNewGameSettingsVisuals(); UpdateNewGameXPPreview(); }
    public void OnNewGameSettingsMonteCarloClicked(int mode) { PlayClickSound(); GameSettings.MonteCarlo4Ways = (mode == 1); UpdateNewGameSettingsVisuals(); UpdateNewGameXPPreview(); }
    public void OnNewGameSettingsMontanaClicked(int mode) { PlayClickSound(); GameSettings.MontanaHard = (mode == 1); UpdateNewGameSettingsVisuals(); UpdateNewGameXPPreview(); }

    // --- ДОБАВИТЬ: Вспомогательные методы ограничений Паука ---
    private void ValidateSpiderConstraints(int suitCount, Button[] diffBtns)
    {
        EnableAllDifficulties(diffBtns);

        if (suitCount == 1)
        {
            if (diffBtns.Length > 2 && diffBtns[2] != null) diffBtns[2].interactable = false;
            if (GameSettings.CurrentDifficulty == Difficulty.Hard) GameSettings.CurrentDifficulty = Difficulty.Medium;
        }
        else if (suitCount == 4)
        {
            if (diffBtns.Length > 0 && diffBtns[0] != null) diffBtns[0].interactable = false;
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
    private void SetupControlPanel()
    {
        if (activeGameMode == null) return;

        GameType type = activeGameMode.GameType;

        // 1. Enable/Disable containers based on game type
        bool isKlondike = (type == GameType.Klondike);
        // bool isSpider = (type == GameType.Spider);

        if (winDrawContainer) winDrawContainer.SetActive(isKlondike);
        // if (winSuitContainer) winSuitContainer.SetActive(isSpider);
        if (winDifficultyContainer) winDifficultyContainer.SetActive(true);

        // 2. Color buttons based on GameSettings
        UpdateControlVisuals();
    }

    private void UpdateControlVisuals()
    {
        // Difficulty
        int diffIndex = (int)GameSettings.CurrentDifficulty; // 0, 1, 2
        for (int i = 0; i < winDiffButtons.Length; i++)
        {
            if (winDiffButtons[i] != null)
                SetButtonState(winDiffButtons[i], i == diffIndex);
        }

        // Klondike Draw
        if (winDrawButtons.Length >= 2)
        {
            SetButtonState(winDrawButtons[0], GameSettings.KlondikeDrawCount == 1);
            SetButtonState(winDrawButtons[1], GameSettings.KlondikeDrawCount == 3);
        }
    }

    private void SetButtonState(Button btn, bool isSelected)
    {
        if (btn == null) return;
        btn.image.color = isSelected ? btnSelectedColor : btnNormalColor;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp) tmp.color = isSelected ? textSelectedColor : textNormalColor;
    }

    // --- BUTTON CLICK HANDLERS (WIN/DEFEAT PANEL) ---

    public void OnWinDifficultyClicked(int diffIndex)
    {
        PlayClickSound();
        GameSettings.CurrentDifficulty = (Difficulty)diffIndex;
        UpdateControlVisuals();
        UpdateXPPreviewText();
    }

    public void OnWinDrawModeClicked(int drawCount)
    {
        PlayClickSound();
        GameSettings.KlondikeDrawCount = drawCount;
        UpdateControlVisuals();
        UpdateXPPreviewText();
    }

    private void UpdateXPPreviewText()
    {
        if (winXPText == null && xpPreviewText == null || activeGameMode == null) return;

        string variant = GameSettings.GetCurrentVariantString(activeGameMode.GameType);

        int currentLvl = 1;
        bool isPremium = false;

        // ---> ИСПРАВЛЕНИЕ УРОВНЯ И ПРЕМИУМА <---
        if (StatisticsManager.Instance != null)
        {
            var data = StatisticsManager.Instance.GetGameGlobalStats(activeGameMode.GameName);
            if (data != null) currentLvl = data.currentLevel;
            isPremium = StatisticsManager.Instance.IsUserPremium;
        }

        int xpAmount = LevelingUtils.CalculateXP(activeGameMode.GameType, currentLvl, GameSettings.CurrentDifficulty, variant, isPremium);

        // ---> ИНТЕГРАЦИЯ БУСТЕРА Х2 <---
        if (QuestManager.Instance != null && System.Enum.TryParse(activeGameMode.GameType.ToString(), out QuestCategory cat))
        {
            if (QuestManager.Instance.HasActiveXpBuff(cat))
            {
                xpAmount *= 2;
            }
        }
        // ------------------------------

        string coloredXP = $"<color=#FFC400>{xpAmount}</color>";

        string format = "{0} XP";
        if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
        {
            string loc = LocalizationManager.instance.GetLocalizedValue(xpPreviewLocKey);
            if (!string.IsNullOrEmpty(loc)) format = loc;
        }

        if (winXPText != null) { try { winXPText.text = string.Format(format, coloredXP); } catch { winXPText.text = $"{coloredXP} XP"; } }
        if (xpPreviewText != null) { try { xpPreviewText.text = string.Format(format, coloredXP); } catch { xpPreviewText.text = $"{coloredXP} XP"; } }
    }

    // --- MENU AND EXIT LOGIC (CORRECTED) ---

    public void OnMenuClicked()
    {
        PlayClickSound();

        // ---> ЗАСТАВЛЯЕМ ВСЕ ПАНЕЛИ ЗАДАНИЙ МГНОВЕННО УЛЕТЕТЬ <---
        InGameQuestNotification.Instance?.ForceCloseAll();

        // 1. Если мы на экране победы - выходим в меню мгновенно (и запускаем красивую анимацию улета)
        if (winPanel != null && winPanel.activeSelf)
        {
            OnConfirmExitClicked();
            return;
        }

        bool needConfirmation = false;

        // 2. Проверяем, нужно ли показать окно подтверждения выхода
        if (defeatPanel != null && defeatPanel.activeSelf)
        {
            // Игрок на экране поражения, но хочет выйти -> сдается
            needConfirmation = true;
        }
        else if (activeGameMode != null && activeGameMode.IsMatchInProgress() && !GameSettings.IsTutorialMode)
        {
            // Обычная игра (не туториал), и игрок уже сделал хотя бы один ход -> сдается
            int moves = (StatisticsManager.Instance != null) ? StatisticsManager.Instance.GetCurrentMoves() : 0;
            if (moves > 0) needConfirmation = true;
        }

        // Прячем текстовую панель туториала влево (если она была)
        activeGameMode?.Tutorial?.HidePanelToLeft();

        // Показываем панель подтверждения выхода, если требуется
        if (needConfirmation && exitConfirmationPanel != null)
        {
            TogglePanelAnimated(exitConfirmationPanel, true);
        }
        else
        {
            OnConfirmExitClicked();
        }
    }

    public void OnConfirmExitClicked()
    {
        PlayClickSound();

        // --- ФИКС: Железобетонно прячем панель туториала при любом выходе ---
        activeGameMode?.Tutorial?.HidePanelToLeft();

        // Скрываем панель подтверждения (если была)
        if (exitConfirmationPanel != null && exitConfirmationPanel.activeSelf)
            TogglePanelAnimated(exitConfirmationPanel, false);

        // --- НОВАЯ ЛОГИКА: Проверяем, какая панель открыта, и анимируем её улет ---
        if (winPanel != null && winPanel.activeSelf)
        {
            StartCoroutine(ExitMenuSequenceWithAnimation());
        }
        else if (defeatPanel != null && defeatPanel.activeSelf)
        {
            // [ИСПРАВЛЕНИЕ БАГА] Анимируем улет панели поражения
            StartCoroutine(DefeatExitSequenceWithAnimation());
        }
        else
        {
            // Стандартный выход
            PerformSceneExit();
        }
    }
    private IEnumerator DefeatExitSequenceWithAnimation()
    {
        // Скрываем панель с анимацией
        if (defeatPanel != null)
        {
            TogglePanelAnimated(defeatPanel, false);
        }

        // Ждем 0.3 секунды (время вашей анимации AnimatePanelRoutine)
        yield return new WaitForSeconds(0.3f);

        // Выходим в меню
        PerformSceneExit();
    }
    private IEnumerator ExitMenuSequenceWithAnimation()
    {
        // [FIX] Скрываем настройки, если они были открыты поверх победы
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            TogglePanelAnimated(settingsPanel, false);
        }

        // 1. Анимация улета карты
        if (winCardRect != null && winCardRect.gameObject.activeSelf)
        {
            StartCoroutine(AnimateCardExit(winCardRect));
        }

        // 2. Анимация улета панели
        if (winPanel != null)
        {
            TogglePanelAnimated(winPanel, false);
        }

        // 3. Ждем завершения анимаций
        yield return new WaitForSeconds(0.5f);

        // 4. Выходим в меню
        PerformSceneExit();
    }
    private void AbortActiveGameAnimations()
    {
        // 1. Останавливаем корутины на главном менеджере (например, KlondikeModeManager)
        if (activeGameMode is MonoBehaviour modeMb)
        {
            modeMb.StopAllCoroutines();

            // 2. Ищем и останавливаем раздачу в DeckManager
            var deck = modeMb.GetComponent("DeckManager") as MonoBehaviour;
            if (deck != null) deck.StopAllCoroutines();

            // 3. Ищем и глушим AnimationService (если он висит там же)
            var anim = modeMb.GetComponent("AnimationService") as MonoBehaviour;
            if (anim != null) anim.StopAllCoroutines();
        }

        // 4. Жестко останавливаем любые анимации на самих картах
        var allCards = FindObjectsOfType<CardController>();
        foreach (var card in allCards)
        {
            if (card != null) card.StopAllCoroutines();
        }
    }
    private void PerformSceneExit()
    {
        // Логика кэша
        if (DealCacheSystem.Instance != null)
        {
            int moves = (StatisticsManager.Instance != null) ? StatisticsManager.Instance.GetCurrentMoves() : 0;
            if (moves > 0) DealCacheSystem.Instance.DiscardActiveDeal();
            else DealCacheSystem.Instance.ReturnActiveDealToQueue();
        }

        // 3. Игрок реально выходит -> Отправляем хайлайты в космос и чистим туториал
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
        if (exitConfirmationPanel != null) TogglePanelAnimated(exitConfirmationPanel, false);

        // 2. Игрок нажал "Нет" -> Возвращаем текстовую панель туториала обратно
        activeGameMode?.Tutorial?.RestorePanelPosition();
    }

    // --- NEW GAME LOGIC (CORRECTED) ---

    public void OnNewGameClicked()
    {
        PlayClickSound();

        // 1. Если панель победы активна - игра УЖЕ выиграна.
        // Сразу начинаем новую игру без предупреждений о поражении.
        if (winPanel != null && winPanel.activeSelf)
        {
            OnConfirmNewGameClicked();
            return;
        }

        // 2. Если игра идет ИЛИ мы на экране поражения - нужно подтверждение.
        // Игрок еще может нажать Undo, поэтому предупреждаем его.
        if (newGameConfirmationPanel != null)
        {
            TogglePanelAnimated(newGameConfirmationPanel, true);
        }
        else
        {
            OnConfirmNewGameClicked();
        }
    }

    public void OnNewGameSettingsClicked()
    {
        PlayClickSound();

        // 1. Скрываем панель подтверждения новой игры
        if (newGameConfirmationPanel != null)
        {
            TogglePanelAnimated(newGameConfirmationPanel, false);
        }

        // 2. Открываем СПЕЦИАЛЬНУЮ панель настроек новой игры (NewGameSettingsPanel)
        if (newGameSettingsPanel != null)
        {
            SetupNewGameSettingsPanel();
            TogglePanelAnimated(newGameSettingsPanel, true);
        }
    }
    public void OnConfirmNewGameClicked()
    {
        PlayClickSound();
        GameSettings.IsTutorialMode = false; // [NEW] Сбрасываем туториал

        if (newGameConfirmationPanel != null && newGameConfirmationPanel.activeSelf)
        {
            TogglePanelAnimated(newGameConfirmationPanel, false);
        }
        StartCoroutine(RestartSequenceRoutine());
    }
    private IEnumerator RestartSequenceRoutine()
    {
        // [FIX] Глушим отложенную панель победы, чтобы она не вылезла во время новой раздачи
        if (winSequenceCoroutine != null) StopCoroutine(winSequenceCoroutine);

        // 1. Скрываем настройки
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            TogglePanelAnimated(settingsPanel, false);
        }
        if (newGameSettingsPanel != null && newGameSettingsPanel.activeSelf)
        {
            TogglePanelAnimated(newGameSettingsPanel, false);
        }

        if ((settingsPanel != null && settingsPanel.activeSelf) || (newGameSettingsPanel != null && newGameSettingsPanel.activeSelf))
        {
            yield return new WaitForSeconds(0.3f);
        }

        // 2. Скрываем UI победы/поражения
        if (winPanel != null && winPanel.activeSelf)
        {
            yield return StartCoroutine(AnimateWinUIExit());
        }
        else if (defeatPanel != null && defeatPanel.activeSelf)
        {
            TogglePanelAnimated(defeatPanel, false);
            yield return new WaitForSeconds(0.3f);
        }

        // 3. Сброс истории
        if (undoManager != null) undoManager.ResetHistory();
        if (DealCacheSystem.Instance != null) DealCacheSystem.Instance.DiscardActiveDeal();

        // 4. Запуск падения карт и рестарт
        if (exitAnimator != null && activeGameMode != null)
        {
            activeGameMode.IsInputAllowed = false;

            // Принудительно прерываем начальную раздачу перед рестартом
            AbortActiveGameAnimations();

            bool cardsFallen = false;
            exitAnimator.PlayRestartSequence(() => { cardsFallen = true; });

            while (!cardsFallen) yield return null;

            activeGameMode.RestartGame();
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
    
    // 2. Вызывается кнопкой "НАЧАТЬ" (Start) в панели настроек новой игры
    public void OnNewGameStartClicked()
    {
        PlayClickSound();
        GameSettings.IsTutorialMode = false; // [NEW] Сбрасываем туториал

        if (newGameSettingsPanel != null) TogglePanelAnimated(newGameSettingsPanel, false);
        StartCoroutine(RestartSequenceRoutine());
    }

    // 3. Вызывается кнопкой "Закрыть/Крестик" в панели настроек новой игры (Отмена)
    public void OnCloseNewGameSettingsClicked()
    {
        PlayClickSound();
        if (newGameSettingsPanel != null) TogglePanelAnimated(newGameSettingsPanel, false);
    }

    // --- НАСТРОЙКА ВИЗУАЛА НОВОЙ ПАНЕЛИ ---

    private void SetupNewGameSettingsPanel()
    {
        if (activeGameMode == null) return;
        GameType type = activeGameMode.GameType;

        if (newGameSettingsDrawContainer) newGameSettingsDrawContainer.SetActive(type == GameType.Klondike);
        if (newGameSettingsSuitContainer) newGameSettingsSuitContainer.SetActive(type == GameType.Spider);
        if (newGameSettingsRoundsContainer) newGameSettingsRoundsContainer.SetActive(type == GameType.Pyramid || type == GameType.TriPeaks);
        if (newGameSettingsYukonContainer) newGameSettingsYukonContainer.SetActive(type == GameType.Yukon);
        if (newGameSettingsMonteCarloContainer) newGameSettingsMonteCarloContainer.SetActive(type == GameType.MonteCarlo);
        if (newGameSettingsMontanaContainer) newGameSettingsMontanaContainer.SetActive(type == GameType.Montana);

        if (newGameSettingsDiffContainer) newGameSettingsDiffContainer.SetActive(true);

        // Ограничения для Паука
        if (type == GameType.Spider) ValidateSpiderConstraints(GameSettings.SpiderSuitCount, newGameSettingsDiffButtons);
        else EnableAllDifficulties(newGameSettingsDiffButtons);

        UpdateNewGameSettingsVisuals();
        UpdateNewGameXPPreview();
    }

    private void UpdateNewGameSettingsVisuals()
    {
        int diffIndex = (int)GameSettings.CurrentDifficulty;
        for (int i = 0; i < newGameSettingsDiffButtons.Length; i++)
        {
            if (newGameSettingsDiffButtons[i] != null)
            {
                if (!newGameSettingsDiffButtons[i].interactable) newGameSettingsDiffButtons[i].image.color = buttonDisabledColor;
                else SetButtonState(newGameSettingsDiffButtons[i], i == diffIndex);
            }
        }

        if (newGameSettingsDrawButtons != null && newGameSettingsDrawButtons.Length >= 2)
        {
            SetButtonState(newGameSettingsDrawButtons[0], GameSettings.KlondikeDrawCount == 1);
            SetButtonState(newGameSettingsDrawButtons[1], GameSettings.KlondikeDrawCount == 3);
        }
        if (newGameSettingsSuitButtons != null && newGameSettingsSuitButtons.Length >= 3)
        {
            SetButtonState(newGameSettingsSuitButtons[0], GameSettings.SpiderSuitCount == 1);
            SetButtonState(newGameSettingsSuitButtons[1], GameSettings.SpiderSuitCount == 2);
            SetButtonState(newGameSettingsSuitButtons[2], GameSettings.SpiderSuitCount == 4);
        }
        if (newGameSettingsRoundsButtons != null && newGameSettingsRoundsButtons.Length >= 3)
        {
            SetButtonState(newGameSettingsRoundsButtons[0], GameSettings.RoundsCount == 1);
            SetButtonState(newGameSettingsRoundsButtons[1], GameSettings.RoundsCount == 2);
            SetButtonState(newGameSettingsRoundsButtons[2], GameSettings.RoundsCount == 3);
        }

        SetButtonState(newGameSettingsYukonClassicBtn, !GameSettings.YukonRussian);
        SetButtonState(newGameSettingsYukonRussianBtn, GameSettings.YukonRussian);
        SetButtonState(newGameSettingsMonteCarlo8Btn, !GameSettings.MonteCarlo4Ways);
        SetButtonState(newGameSettingsMonteCarlo4Btn, GameSettings.MonteCarlo4Ways);
        SetButtonState(newGameSettingsMontanaClassicBtn, !GameSettings.MontanaHard);
        SetButtonState(newGameSettingsMontanaHardBtn, GameSettings.MontanaHard);
    }

    private void UpdateNewGameXPPreview()
    {
        if (newGameXPPreviewText == null || activeGameMode == null) return;

        string variant = GameSettings.GetCurrentVariantString(activeGameMode.GameType);

        int currentLvl = 1;
        bool isPremium = false;

        // ---> ИСПРАВЛЕНИЕ УРОВНЯ И ПРЕМИУМА <---
        if (StatisticsManager.Instance != null)
        {
            var data = StatisticsManager.Instance.GetGameGlobalStats(activeGameMode.GameName);
            if (data != null) currentLvl = data.currentLevel;
            isPremium = StatisticsManager.Instance.IsUserPremium;
        }

        int xpAmount = LevelingUtils.CalculateXP(activeGameMode.GameType, currentLvl, GameSettings.CurrentDifficulty, variant, isPremium);

        // ---> ИНТЕГРАЦИЯ БУСТЕРА Х2 <---
        if (QuestManager.Instance != null && System.Enum.TryParse(activeGameMode.GameType.ToString(), out QuestCategory cat))
        {
            if (QuestManager.Instance.HasActiveXpBuff(cat))
            {
                xpAmount *= 2;
            }
        }
        // ------------------------------

        string coloredXP = $"<color=#FFC400>{xpAmount}</color>";

        if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
        {
            string format = LocalizationManager.instance.GetLocalizedValue(xpPreviewLocKey);
            if (string.IsNullOrEmpty(format)) format = "{0} XP";
            try { newGameXPPreviewText.text = string.Format(format, coloredXP); }
            catch { newGameXPPreviewText.text = $"{coloredXP} XP"; }
        }
        else newGameXPPreviewText.text = $"XP: {coloredXP}";
    }

    // --- ОБРАБОТЧИКИ КЛИКОВ ДЛЯ НОВОЙ ПАНЕЛИ ---
    // Назначьте эти методы кнопкам внутри NewGameSettingsPanel

    public void OnNewGameSettingsDiffClicked(int diffIndex)
    {
        if (newGameSettingsDiffButtons != null && diffIndex < newGameSettingsDiffButtons.Length && !newGameSettingsDiffButtons[diffIndex].interactable) return;
        PlayClickSound();
        GameSettings.CurrentDifficulty = (Difficulty)diffIndex;
        UpdateNewGameSettingsVisuals();
        UpdateNewGameXPPreview();
    }

    public void OnNewGameSettingsDrawClicked(int drawCount)
    {
        PlayClickSound();
        GameSettings.KlondikeDrawCount = drawCount;
        UpdateNewGameSettingsVisuals();
        UpdateNewGameXPPreview();
    }
    private IEnumerator AnimateWinUIExit()
    {
        // 1. Сначала запускаем улет карты
        Coroutine cardAnim = null;
        if (winCardRect != null && winCardRect.gameObject.activeSelf)
        {
            cardAnim = StartCoroutine(AnimateCardExit(winCardRect));
        }

        // 2. Одновременно запускаем улет панели
        if (winPanel != null)
        {
            TogglePanelAnimated(winPanel, false);
        }

        // 3. Ждем завершения анимации карты (это самая долгая часть)
        if (cardAnim != null) yield return cardAnim;

        // Дополнительная небольшая пауза для надежности
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator AnimateCardExit(RectTransform card)
    {
        // Отключаем покачивание
        var hover = card.GetComponent<CardHoverEffect>();
        if (hover != null) hover.SetSelectedMode(false);

        Vector2 startPos = card.anchoredPosition;
        Vector2 targetPos = winCardDefaultPos + new Vector2(1500f, 0f); // Улетает вправо
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Card_Whoosh_Out");
        }
        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // EaseInBack (немного назад, потом рывок вперед)
            float t = elapsed / duration;
            // t = t * t * t; // Cubic

            card.anchoredPosition = Vector2.Lerp(startPos, targetPos, t * t);
            yield return null;
        }

        card.anchoredPosition = targetPos;
        card.gameObject.SetActive(false);
    }
    public void OnCancelNewGameClicked()
    {
        PlayClickSound();
        if (newGameConfirmationPanel != null) TogglePanelAnimated(newGameConfirmationPanel, false);
    }


    // --- VISUALS & ANIMATIONS (UNCHANGED LOGIC) ---

    private IEnumerator AnimateCardEntrance(RectTransform card)
    {
        Vector2 targetPos = winCardDefaultPos;
        Vector2 startPos = winCardDefaultPos + new Vector2(1500f, 0f);

        // На всякий случай еще раз ставим позицию (дублирование не повредит)
        card.anchoredPosition = startPos;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Card_Whoosh_Out");
        }
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // EaseOutCubic (быстро вылетает, плавно тормозит)
            float t = 1f - Mathf.Pow(1f - (elapsed / duration), 3);

            card.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        card.anchoredPosition = targetPos;

        // Включаем "вечное" покачивание только когда карта приехала
        var hover = card.GetComponent<CardHoverEffect>();
        if (hover != null) hover.SetSelectedMode(true);
    }

    // Анимация улета карты обратно вправо


    private void UpdateWinPanelStats(int manualMoves)
    {
        // 1. Сложность 
        if (winDifficultyText)
        {
            string diffKey = "DiffMedium";

            if (GameSettings.IsTutorialMode)
            {
                diffKey = "tutorial";
            }
            else
            {
                switch (GameSettings.CurrentDifficulty)
                {
                    case Difficulty.Easy: diffKey = "DiffEasy"; break;
                    case Difficulty.Medium: diffKey = "DiffMedium"; break;
                    case Difficulty.Hard: diffKey = "DiffHard"; break;
                }
            }

            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
                winDifficultyText.text = LocalizationManager.instance.GetLocalizedValue(diffKey);
            else
            {
                winDifficultyText.text = GameSettings.IsTutorialMode ? "Обучение" : GameSettings.CurrentDifficulty.ToString();
            }
        }

        bool isNewScoreRecord = false;
        bool isNewMovesRecord = false;
        bool isNewTimeRecord = false;

        if (StatisticsManager.Instance != null)
        {
            isNewScoreRecord = StatisticsManager.Instance.IsNewScoreRecord;
            isNewMovesRecord = StatisticsManager.Instance.IsNewMovesRecord;
            isNewTimeRecord = StatisticsManager.Instance.IsNewTimeRecord;
        }

        // 2. Счет
        if (activeGameMode != null && winScoreText)
        {
            int finalScore = GetScoreFromGameMode();
            winScoreText.text = GetRecordText(finalScore.ToString(), isNewScoreRecord);
        }

        // 3. Время и Ходы
        if (StatisticsManager.Instance != null)
        {
            int movesToShow = (manualMoves >= 0) ? manualMoves : StatisticsManager.Instance.GetCurrentMoves();
            winMovesText.text = GetRecordText(movesToShow.ToString(), isNewMovesRecord);

            float duration = StatisticsManager.Instance.LastGameTime;
            winTimeText.text = GetRecordText(FormatTime(duration), isNewTimeRecord);

            // 4. ОПЫТ
            if (winEarnedXPText)
            {
                int earned = StatisticsManager.Instance.LastXPGained;
                winEarnedXPText.text = $"<color=#FFC400>{earned}</color>";
            }
        }
        // 5. Визуал карты 
        if (winLevelBar != null && StatisticsManager.Instance != null)
        {
            string gameName = activeGameMode != null ? activeGameMode.GameName : "Unknown";
            StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);
            if (data != null)
            {
                int currentXP = data.currentXP;
                int xpGained = StatisticsManager.Instance.LastXPGained;
                int startXP = currentXP - xpGained;

                if (startXP < 0) // Если произошел Level Up
                {
                    int oldLevel = Mathf.Max(1, data.currentLevel - 1);
                    int oldTarget = oldLevel * 500;
                    int oldXPStart = oldTarget + startXP;
                    winLevelBar.UpdateBar(oldLevel, oldXPStart, oldTarget);
                }
                else // Обычное начисление опыта
                {
                    int targetXP = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;
                    winLevelBar.UpdateBar(data.currentLevel, startXP, targetXP);
                }
            }
        }
        // --- 6. ПЕРЕКЛЮЧЕНИЕ КНОПОК НАВИГАЦИИ (ОБЫЧНАЯ ИГРА vs ТУТОРИАЛ) ---
        bool isTutorial = GameSettings.IsTutorialMode;

        if (tutorialBigMenuButton != null)
        {
            tutorialBigMenuButton.SetActive(isTutorial);
        }

        if (standardWinButtons != null)
        {
            foreach (var btnObj in standardWinButtons)
            {
                if (btnObj != null) btnObj.SetActive(!isTutorial);
            }
        }
    }
    // --- НОВЫЙ МЕТОД ДЛЯ ФОРМАТИРОВАНИЯ ТЕКСТА РЕКОРДА ---
    private string GetRecordText(string baseValue, bool isRecord)
    {
        // Если это не рекорд, просто возвращаем дефолтное значение ("2500")
        if (!isRecord) return baseValue;

        // Дефолтное слово (на случай если LocalizationManager недоступен)
        string recordWord = "Новый рекорд!";

        // Достаем локализацию
        if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
        {
            string loc = LocalizationManager.instance.GetLocalizedValue("NewRecord");
            if (!string.IsNullOrEmpty(loc)) recordWord = loc;
        }

        // Возвращаем покрашенную в золото строку
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
   
    private IEnumerator AnimateXPBarDelayed()
    {
        yield return new WaitForSeconds(0.6f);
        if (winLevelBar != null && StatisticsManager.Instance != null)
        {
            string gameName = activeGameMode != null ? activeGameMode.GameName : "Unknown";
            StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);
            if (data != null)
            {
                int currentXP = data.currentXP;
                int xpGained = StatisticsManager.Instance.LastXPGained;
                int startXP = currentXP - xpGained;
                int targetXP = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;

                if (startXP < 0) // ВЕТКА ПОЛУЧЕНИЯ НОВОГО УРОВНЯ (Level Up)
                {
                    int oldLevel = Mathf.Max(1, data.currentLevel - 1);
                    int oldTarget = oldLevel * 500;
                    int oldXPStart = oldTarget + startXP;

                    // Передаем управление в нашу красивую анимацию
                    StartCoroutine(CardLevelUpSequence(data, oldLevel, oldXPStart, oldTarget, currentXP, targetXP));
                }
                else // ВЕТКА ОБЫЧНОГО ЗАПОЛНЕНИЯ (XP Gain)
                {
                    // <--- ДИНАМИЧЕСКИЙ ЗВУК ОПЫТА --->
                    // Предполагаем, что стандартная анимация бара длится около 1 секунды
                    StartCoroutine(PlayDynamicXPSound(1.0f));

                    winLevelBar.AnimateBar(data.currentLevel, startXP, currentXP, targetXP);
                }
            }
        }
    }
    private IEnumerator CardLevelUpSequence(StatData data, int oldLevel, int oldXPStart, int oldTarget, int currentXP, int targetXP)
    {
        // --- 1. ФАЗА ЗАПОЛНЕНИЯ ДО 100% ---
        // Запускаем динамический звук на 1.5 сек (время анимации бара)
        StartCoroutine(PlayDynamicXPSound(1.5f));

        winLevelBar.AnimateBar(oldLevel, oldXPStart, oldTarget, oldTarget);
        yield return new WaitForSeconds(1.5f);

        // --- 2. ФАЗА ТРАНСФОРМАЦИИ (Вращение 360) ---
        if (AudioManager.Instance != null)
        {
            // Звук магического повышения уровня
            AudioManager.Instance.PlaySound("Level_Up");
            // Звук резкого вращения/взмаха карты
            AudioManager.Instance.PlaySound("Card_Whoosh_In");
        }

        Vector3 originalScale = winCardRect.localScale;
        Vector3 targetScale = originalScale * 1.25f;

        float animDuration = 1.5f;
        float elapsed = 0f;
        float overshoot = 1.70158f;

        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / animDuration;

            float t_minus = t - 1f;
            float easeRotate = 1f + (overshoot + 1f) * Mathf.Pow(t_minus, 3) + overshoot * Mathf.Pow(t_minus, 2);
            float easeScale = 1f - Mathf.Pow(1f - t, 3);

            winCardRect.localScale = Vector3.Lerp(originalScale, targetScale, easeScale);
            winCardRect.localEulerAngles = new Vector3(0f, 0f, -360f * easeRotate);

            yield return null;
        }

        winCardRect.localScale = targetScale;
        winCardRect.localEulerAngles = Vector3.zero;

        yield return new WaitForSeconds(0.3f);

        // --- 5. ОБНУЛЕНИЕ ПОЛОСКИ ---
        winLevelBar.AnimateBar(oldLevel, oldTarget, 0, oldTarget);
        yield return new WaitForSeconds(0.4f);

        // 6. ОБНОВЛЯЕМ ЦИФРУ УРОВНЯ 
        winLevelBar.UpdateBar(data.currentLevel, 0, targetXP);

        // 7. ВОЗВРАТ РАЗМЕРА КАРТЫ
        float shrinkDuration = 0.6f;
        elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / shrinkDuration);
            winCardRect.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        winCardRect.localScale = originalScale;

        // --- 8. ФИНАЛЬНОЕ ЗАПОЛНЕНИЕ НОВЫМ ОПЫТОМ ---
        if (currentXP > 0)
        {
            // Запускаем динамический звук на 0.6 сек
            StartCoroutine(PlayDynamicXPSound(0.6f));
        }

        winLevelBar.AnimateBar(data.currentLevel, 0, currentXP, targetXP);
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
            if (show)
                AudioManager.Instance.PlaySound("Panel_Slide_In");
            else
                AudioManager.Instance.PlaySound("Panel_Slide_Out");
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

        // 1. Находим целевую позицию (ту, где панель была в редакторе)
        Vector2 targetPos = Vector2.zero;
        if (panelInitialPositions.ContainsKey(panel))
        {
            targetPos = panelInitialPositions[panel];
        }

        // 2. Рассчитываем стартовую позицию (за экраном слева, но с сохранением Y)
        // Если -2000 недостаточно, можно увеличить
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
            // EaseOutCubic
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

        // Выбираем панель в зависимости от премиума
        GameObject panelToShow = isPremium ? premiumStatisticsPanel : basicStatisticsPanel;

        if (panelToShow != null)
        {
            // Передаем данные в скрипт ДО начала анимации
            if (isPremium)
            {
                var statsUI = panelToShow.GetComponent<StatisticsUI>();
                if (statsUI != null) statsUI.ShowStatsForGame(activeGameMode.GameType);
            }
            else
            {
                // Вызываем показ в базовой панели (предполагается, что скрипт называется BasicStatisticsUI)
                var basicStatsUI = panelToShow.GetComponent<BasicStatisticsUI>();
                if (basicStatsUI != null) basicStatsUI.ShowStatsForGame(activeGameMode.GameType);
            }

            TogglePanelAnimated(panelToShow, true);
        }
    }
    public void OnCloseStatisticsClicked()
    {
        PlayClickSound();
        // Закрываем любую открытую статистику с анимацией улета
        if (basicStatisticsPanel != null && basicStatisticsPanel.activeSelf) TogglePanelAnimated(basicStatisticsPanel, false);
        if (premiumStatisticsPanel != null && premiumStatisticsPanel.activeSelf) TogglePanelAnimated(premiumStatisticsPanel, false);
    }
    private void PlayClickSound()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Click");
    }
    private IEnumerator PlayDynamicXPSound(float duration, float delay = 0.05f)
    {
        // 1. Ждем крошечную долю секунды, чтобы UI-анимация точно успела начать движение
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

            // 2. ИЗМЕНЕНО: Начинаем с 0.0f (полная тишина). 
            // Звук будет плавно "выплывать" из нуля вместе с разгоном жидкости
            source.volume = baseVolume * Mathf.Lerp(0.0f, 1.0f, speedFactor);

            yield return null;
        }

        // Плавное затухание
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
    public void OnUndoOneClicked()
    {
        PlayClickSound();
        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowRewarded("undo_one");
        }
        else
        {
            // Если AdManager нет на сцене (тест в редакторе), выполняем сразу
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
    // --- РЕАЛЬНАЯ ЛОГИКА ОТМЕНЫ ---
    // Вызывается автоматически из HandleRewardEarned после успешного просмотра
    private void ExecuteUndoOne()
    {
        // Красивый улет панели поражения
        if (defeatPanel != null && defeatPanel.activeSelf) TogglePanelAnimated(defeatPanel, false);

        if (activeGameMode != null) activeGameMode.IsInputAllowed = true;

        if (undoManager != null && undoManager.undoButton != null)
        {
            if (undoManager.undoButton.interactable) undoManager.undoButton.onClick.Invoke();
        }
        else if (activeGameMode != null)
        {
            activeGameMode.OnUndoAction();
        }
    }

    private void ExecuteUndoAll()
    {
        // Красивый улет панели поражения
        if (defeatPanel != null && defeatPanel.activeSelf) TogglePanelAnimated(defeatPanel, false);

        if (activeGameMode != null) activeGameMode.IsInputAllowed = true;

        if (undoManager != null && undoManager.undoAllButton != null)
        {
            if (undoManager.undoAllButton.interactable) undoManager.undoAllButton.onClick.Invoke();
        }
        else if (activeGameMode != null)
        {
            var type = activeGameMode.GetType();
            var method = type.GetMethod("OnUndoAllAction");
            if (method != null) method.Invoke(activeGameMode, null);
        }
    }
}