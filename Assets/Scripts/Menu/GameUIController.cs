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
    public GameObject statisticsPanel;
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
    
    [Header("Settings Containers")]
    // Контейнеры внутри SettingsPanel
    public GameObject settingsDifficultyContainer;
    public GameObject settingsDrawContainer;     // Klondike
    public GameObject settingsSuitContainer;     // Spider (резерв)

    [Header("Settings Buttons")]
    public Button[] settingsDiffButtons;   // 0-Easy, 1-Medium, 2-Hard
    public Button[] settingsDrawButtons;   // 0-(Draw1), 1-(Draw3)
    // public Button[] settingsSuitButtons;

    // --- NEW GAME SETTINGS PANEL (SEPARATE PANEL) ---
    [Header("New Game Settings Panel")]
    public GameObject newGameSettingsPanel;

    [Header("New Game Settings Containers")]
    public GameObject newGameSettingsDiffContainer;
    public GameObject newGameSettingsDrawContainer;

    [Header("New Game Settings Buttons")]
    public Button[] newGameSettingsDiffButtons; // 0-Easy, 1-Medium, 2-Hard
    public Button[] newGameSettingsDrawButtons; // 0-Draw1, 1-Draw3

    [Header("New Game XP Preview")]
    public TMP_Text newGameXPPreviewText;
    private Coroutine winSequenceCoroutine;
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
        RegisterAndHidePanel(statisticsPanel);
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
        }
    }
    // --- SETTINGS BUTTONS LOGIC (SYNC) ---

    public void OnSettingsClicked()
    {
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
        if (settingsPanel != null)
        {
            TogglePanelAnimated(settingsPanel, false);
        }
    }

    private void SetupSettingsPanel()
    {
        if (activeGameMode == null) return;

        GameType type = activeGameMode.GameType;

        // 1. Включаем контейнеры в зависимости от типа игры
        // Используем новые переменные settings...Container

        bool isKlondike = (type == GameType.Klondike);
        // bool isSpider = (type == GameType.Spider);

        if (settingsDrawContainer) settingsDrawContainer.SetActive(isKlondike);
        // if (settingsSuitContainer) settingsSuitContainer.SetActive(isSpider);

        if (settingsDifficultyContainer) settingsDifficultyContainer.SetActive(true);

        // 2. Обновляем цвета кнопок
        UpdateSettingsVisuals();

        // 3. Обновляем текст предпросмотра XP (если он есть в панели настроек)
        UpdateXPPreviewText();
    }
    private void UpdateSettingsVisuals()
    {
        // Difficulty
        int diffIndex = (int)GameSettings.CurrentDifficulty;
        for (int i = 0; i < settingsDiffButtons.Length; i++)
        {
            if (settingsDiffButtons[i] != null)
                SetButtonState(settingsDiffButtons[i], i == diffIndex);
        }

        // Klondike Draw
        if (settingsDrawButtons.Length >= 2)
        {
            SetButtonState(settingsDrawButtons[0], GameSettings.KlondikeDrawCount == 1);
            SetButtonState(settingsDrawButtons[1], GameSettings.KlondikeDrawCount == 3);
        }
    }
    public void OnSettingsDifficultyClicked(int diffIndex)
    {
        GameSettings.CurrentDifficulty = (Difficulty)diffIndex;
        UpdateSettingsVisuals();
        UpdateXPPreviewText();
    }

    public void OnSettingsDrawModeClicked(int drawCount)
    {
        GameSettings.KlondikeDrawCount = drawCount;
        UpdateSettingsVisuals();
        UpdateXPPreviewText();
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
        GameSettings.CurrentDifficulty = (Difficulty)diffIndex;
        UpdateControlVisuals();
        UpdateXPPreviewText();
    }

    public void OnWinDrawModeClicked(int drawCount)
    {
        GameSettings.KlondikeDrawCount = drawCount;
        UpdateControlVisuals();
        UpdateXPPreviewText();
    }

    private void UpdateXPPreviewText()
    {
        if (winXPText == null || activeGameMode == null) return;

        string variant = "";
        if (activeGameMode.GameType == GameType.Klondike)
            variant = GameSettings.KlondikeDrawCount == 3 ? "draw3" : "draw1";

        int currentLvl = 1;
        if (StatisticsManager.Instance != null)
        {
            var data = StatisticsManager.Instance.GetGameGlobalStats(activeGameMode.GameName);
            if (data != null) currentLvl = data.currentLevel;
        }

        int xpAmount = LevelingUtils.CalculateXP(
            activeGameMode.GameType,
            currentLvl,
            GameSettings.CurrentDifficulty,
            variant,
            false
        );

        string coloredXP = $"<color=#FFC400>{xpAmount}</color>";
        if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
        {
            string format = LocalizationManager.instance.GetLocalizedValue(xpPreviewLocKey);
            if (string.IsNullOrEmpty(format)) format = "{0} XP";
            try { winXPText.text = string.Format(format, coloredXP); }
            catch { winXPText.text = $"{coloredXP} XP"; }
        }
        else
        {
            winXPText.text = $"XP: {coloredXP}";
        }
    }

    // --- MENU AND EXIT LOGIC (CORRECTED) ---

    public void OnMenuClicked()
    {
        if ((winPanel != null && winPanel.activeSelf) || (defeatPanel != null && defeatPanel.activeSelf))
        {
            OnConfirmExitClicked();
            return;
        }

        // [FIX] Универсальная проверка "Идет ли игра" через интерфейс
        bool needConfirmation = false;
        if (activeGameMode != null && activeGameMode.IsMatchInProgress())
        {
            // Если игра реально идет (флаг started = true), проверяем ходы
            int moves = (StatisticsManager.Instance != null) ? StatisticsManager.Instance.GetCurrentMoves() : 0;
            if (moves > 0) needConfirmation = true;
        }

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
        // Скрываем панель подтверждения (если была)
        if (exitConfirmationPanel != null && exitConfirmationPanel.activeSelf)
            TogglePanelAnimated(exitConfirmationPanel, false);

        // --- НОВАЯ ЛОГИКА: Если открыта панель победы, сначала анимируем уход ---
        if (winPanel != null && winPanel.activeSelf)
        {
            StartCoroutine(ExitMenuSequenceWithAnimation());
        }
        else
        {
            // Стандартный выход (без анимации UI победы)
            PerformSceneExit();
        }
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

        // Анимация шторки (SceneExitAnimator) и загрузка сцены
        if (exitAnimator != null)
        {
            if (activeGameMode != null) activeGameMode.IsInputAllowed = false;

            // [FIX] Принудительно прерываем начальную раздачу перед выходом
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
        if (exitConfirmationPanel != null) TogglePanelAnimated(exitConfirmationPanel, false);
    }

    // --- NEW GAME LOGIC (CORRECTED) ---

    public void OnNewGameClicked()
    {
        // Убираем разделение логики. 
        // Неважно, победа сейчас или пауза — мы всегда запускаем один и тот же процесс подтверждения.

        // Если панель подтверждения уже назначена и мы НЕ на экране победы/поражения, показываем её.
        bool isWinState = (winPanel != null && winPanel.activeSelf);
        bool isDefeatState = (defeatPanel != null && defeatPanel.activeSelf);

        if (!isWinState && !isDefeatState && newGameConfirmationPanel != null)
        {
            TogglePanelAnimated(newGameConfirmationPanel, true);
        }
        else
        {
            // Если мы уже выиграли/проиграли, подтверждение не нужно — сразу рестарт.
            OnConfirmNewGameClicked();
        }
    }

    public void OnNewGameSettingsClicked()
    {
        // 1. Скрываем панель подтверждения новой игры
        if (newGameConfirmationPanel != null)
        {
            TogglePanelAnimated(newGameConfirmationPanel, false);
        }

        // 2. Открываем панель настроек
        // Этот метод (OnSettingsClicked) уже содержит проверку на повторное открытие 
        // и инициализацию кнопок (SetupSettingsPanel)
        OnSettingsClicked();
    }
    public void OnConfirmNewGameClicked()
    {
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
    // New helper method to distinguish between simple restart and scene reload
    public void OnNewGameSettingsOptionClicked()
    {
        // Скрываем подтверждение
        if (newGameConfirmationPanel != null) TogglePanelAnimated(newGameConfirmationPanel, false);

        // Открываем специальную панель настроек для новой игры
        if (newGameSettingsPanel != null)
        {
            SetupNewGameSettingsPanel();
            TogglePanelAnimated(newGameSettingsPanel, true);
        }
    }

    // 2. Вызывается кнопкой "НАЧАТЬ" (Start) в панели настроек новой игры
    public void OnNewGameStartClicked()
    {
        GameSettings.IsTutorialMode = false; // [NEW] Сбрасываем туториал

        if (newGameSettingsPanel != null) TogglePanelAnimated(newGameSettingsPanel, false);
        StartCoroutine(RestartSequenceRoutine());
    }

    // 3. Вызывается кнопкой "Закрыть/Крестик" в панели настроек новой игры (Отмена)
    public void OnCloseNewGameSettingsClicked()
    {
        if (newGameSettingsPanel != null) TogglePanelAnimated(newGameSettingsPanel, false);
    }

    // --- НАСТРОЙКА ВИЗУАЛА НОВОЙ ПАНЕЛИ ---

    private void SetupNewGameSettingsPanel()
    {
        if (activeGameMode == null) return;
        GameType type = activeGameMode.GameType;
        bool isKlondike = (type == GameType.Klondike);

        // Включаем нужные контейнеры
        if (newGameSettingsDrawContainer) newGameSettingsDrawContainer.SetActive(isKlondike);
        if (newGameSettingsDiffContainer) newGameSettingsDiffContainer.SetActive(true);

        // Обновляем цвета кнопок и текст
        UpdateNewGameSettingsVisuals();
        UpdateNewGameXPPreview();
    }

    private void UpdateNewGameSettingsVisuals()
    {
        // Сложность
        int diffIndex = (int)GameSettings.CurrentDifficulty;
        for (int i = 0; i < newGameSettingsDiffButtons.Length; i++)
        {
            if (newGameSettingsDiffButtons[i] != null)
                SetButtonState(newGameSettingsDiffButtons[i], i == diffIndex);
        }

        // Режим раздачи (Klondike)
        if (newGameSettingsDrawButtons.Length >= 2)
        {
            SetButtonState(newGameSettingsDrawButtons[0], GameSettings.KlondikeDrawCount == 1);
            SetButtonState(newGameSettingsDrawButtons[1], GameSettings.KlondikeDrawCount == 3);
        }
    }

    private void UpdateNewGameXPPreview()
    {
        if (newGameXPPreviewText == null || activeGameMode == null) return;

        string variant = (activeGameMode.GameType == GameType.Klondike && GameSettings.KlondikeDrawCount == 3) ? "draw3" : "draw1";

        int currentLvl = 1;
        if (StatisticsManager.Instance != null)
        {
            var data = StatisticsManager.Instance.GetGameGlobalStats(activeGameMode.GameName);
            if (data != null) currentLvl = data.currentLevel;
        }

        int xpAmount = LevelingUtils.CalculateXP(activeGameMode.GameType, currentLvl, GameSettings.CurrentDifficulty, variant, false);
        string coloredXP = $"<color=#FFC400>{xpAmount}</color>";

        // Используем тот же ключ локализации или формат
        if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
        {
            string format = LocalizationManager.instance.GetLocalizedValue(xpPreviewLocKey); // "You will get {0} XP"
            if (string.IsNullOrEmpty(format)) format = "{0} XP";
            try { newGameXPPreviewText.text = string.Format(format, coloredXP); }
            catch { newGameXPPreviewText.text = $"{coloredXP} XP"; }
        }
        else
        {
            newGameXPPreviewText.text = $"XP: {coloredXP}";
        }
    }

    // --- ОБРАБОТЧИКИ КЛИКОВ ДЛЯ НОВОЙ ПАНЕЛИ ---
    // Назначьте эти методы кнопкам внутри NewGameSettingsPanel

    public void OnNewGameSettingsDiffClicked(int diffIndex)
    {
        GameSettings.CurrentDifficulty = (Difficulty)diffIndex;
        UpdateNewGameSettingsVisuals();
        UpdateNewGameXPPreview();
    }

    public void OnNewGameSettingsDrawClicked(int drawCount)
    {
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
        if (newGameConfirmationPanel != null) TogglePanelAnimated(newGameConfirmationPanel, false);
    }


    // --- VISUALS & ANIMATIONS (UNCHANGED LOGIC) ---

    private IEnumerator AnimateCardEntrance(RectTransform card)
    {
        Vector2 targetPos = winCardDefaultPos;
        Vector2 startPos = winCardDefaultPos + new Vector2(1500f, 0f);

        // На всякий случай еще раз ставим позицию (дублирование не повредит)
        card.anchoredPosition = startPos;

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
            switch (GameSettings.CurrentDifficulty)
            {
                case Difficulty.Easy: diffKey = "DiffEasy"; break;
                case Difficulty.Medium: diffKey = "DiffMedium"; break;
                case Difficulty.Hard: diffKey = "DiffHard"; break;
            }
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
                winDifficultyText.text = LocalizationManager.instance.GetLocalizedValue(diffKey);
            else
                winDifficultyText.text = GameSettings.CurrentDifficulty.ToString();
        }

        // --- ЧТЕНИЕ ФЛАГОВ РЕКОРДОВ ИЗ STATISTICS MANAGER ---
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
            // Форматируем текст с проверкой на рекорд
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
                winEarnedXPText.text = $"<color=#FFC400>{earned}</color> опыта";
            }
        }

        // 5. Визуал карты 
        if (winLevelBar != null && StatisticsManager.Instance != null)
        {
            string gameName = activeGameMode != null ? activeGameMode.GameName : "Unknown";
            StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);
            if (data != null)
            {
                int displayLevel = data.currentLevel;
                int xpGained = StatisticsManager.Instance.LastXPGained;
                if (data.currentXP - xpGained < 0) displayLevel = Mathf.Max(1, displayLevel - 1);

                winLevelBar.UpdateBar(displayLevel, 0, 100);
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

                if (startXP < 0) // Level Up
                {
                    int oldLevel = Mathf.Max(1, data.currentLevel - 1);
                    int oldTarget = oldLevel * 500;
                    int oldXPStart = oldTarget + startXP;
                    winLevelBar.AnimateLevelUp(oldLevel, oldXPStart, oldTarget, data.currentLevel, currentXP, targetXP);
                }
                else
                {
                    winLevelBar.AnimateBar(data.currentLevel, startXP, currentXP, targetXP);
                }
            }
        }
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
    public void OnStatisticsClicked() { if (statisticsPanel) TogglePanelAnimated(statisticsPanel, true); }
    public void OnCloseStatisticsClicked() { if (statisticsPanel) TogglePanelAnimated(statisticsPanel, false); }
    public void OnUndoOneClicked() { if (defeatPanel) defeatPanel.SetActive(false); if (activeGameMode != null) activeGameMode.IsInputAllowed = true; if (undoManager && undoManager.undoButton.interactable) undoManager.undoButton.onClick.Invoke(); }
    public void OnUndoAllClicked() { if (defeatPanel) defeatPanel.SetActive(false); if (activeGameMode != null) activeGameMode.IsInputAllowed = true; if (undoManager && undoManager.undoAllButton.interactable) undoManager.undoAllButton.onClick.Invoke(); }
}