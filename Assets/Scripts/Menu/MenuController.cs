using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

public class MenuController : MonoBehaviour
{
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
    public GameObject statisticsPanel;

    [Header("Controllers")]
    public MenuLevelController levelController;
    public CardAnimationController cardAnimator;
    public SettingsPanelAnimator settingsPanelAnimator;
    public MenuExitController exitController;

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

    private void Start()
    {
        // Инициализация при старте
        if (settingsPanel) settingsPanel.SetActive(false);
        if (mainSelectionPanel) mainSelectionPanel.SetActive(true);
        if (statisticsPanel) statisticsPanel.SetActive(false);

        // Скрываем оверлеи
        CloseAllOverlays();
       
    }
    private void UpdateXPPreview()
    {
        // 1. Формируем строку варианта
        string variant = "";
        switch (currentGame.type)
        {
            case GameType.Klondike: variant = GameSettings.KlondikeDrawCount == 3 ? "draw3" : "draw1"; break;
            case GameType.Spider:
                if (GameSettings.SpiderSuitCount == 2) variant = "2suit";
                else if (GameSettings.SpiderSuitCount == 4) variant = "4suit";
                else variant = "1suit";
                break;
            case GameType.Pyramid:
            case GameType.TriPeaks:
                variant = GameSettings.RoundsCount.ToString(); // "1", "2", "3"
                break;
            case GameType.Yukon: variant = GameSettings.YukonRussian ? "russian" : "classic"; break;
            case GameType.MonteCarlo: variant = GameSettings.MonteCarlo4Ways ? "4ways" : "8ways"; break;
            case GameType.Montana: variant = GameSettings.MontanaHard ? "hard" : "classic"; break;
        }

        // 2. Получаем текущий уровень игрока (нужен для бонуса мастерства)
        int currentLvl = 1;
        if (StatisticsManager.Instance != null)
        {
            var data = StatisticsManager.Instance.GetGameGlobalStats(currentGame.type.ToString());
            if (data != null) currentLvl = data.currentLevel;
        }

        // 3. Считаем потенциальный опыт
        // (Предполагаем, что LevelingUtils у вас есть и настроен как мы делали ранее)
        int xpAmount = LevelingUtils.CalculateXP(
            currentGame.type,
            currentLvl,
            GameSettings.CurrentDifficulty,
            variant,
            false // isPremium - пока ставим false, или берите из профиля
        );

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

        currentGame = games[gameIndex];
        GameSettings.CurrentGameType = currentGame.type;
        SetButtonState(startButton, true);
        ResetGameSettingsDefault();
        UpdateXPPreview();

        if (cardAnimator != null) cardAnimator.SelectCard(currentGame.type);

        if (settingsPanelAnimator != null)
        {
            if (settingsPanelAnimator.IsOpen())
            {
                settingsPanelAnimator.AnimateSwitch(() =>
                {
                    SetupSettingsPanel();
                    if (levelController != null) levelController.UpdatePreviewBar(currentGame.type);
                    UpdateXPPreview(); // <--- ОБНОВЛЯЕМ XP ПРИ ПЕРЕКЛЮЧЕНИИ
                });
            }
            else
            {
                SetupSettingsPanel();
                if (levelController != null) levelController.UpdatePreviewBar(currentGame.type);
                settingsPanelAnimator.AnimateOpen();
                UpdateXPPreview(); // <--- ОБНОВЛЯЕМ XP ПРИ ОТКРЫТИИ
            }
        }
        else
        {
            settingsPanel.SetActive(true);
            SetupSettingsPanel();
            if (levelController != null) levelController.UpdatePreviewBar(currentGame.type);
            UpdateXPPreview(); // <--- ОБНОВЛЯЕМ XP
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
        // (например, 1 масть обычно не играется на Hard)
        if (currentGame.type == GameType.Spider)
        {
            // Для 1 масти убеждаемся, что не стоит Hard (как в ValidateSpiderConstraints)
            if (GameSettings.CurrentDifficulty == Difficulty.Hard)
                GameSettings.CurrentDifficulty = Difficulty.Medium;
        }
    }

    public void OnBackClicked()
    {
        // 1. Анимация закрытия панели
        if (settingsPanelAnimator != null) settingsPanelAnimator.AnimateClose();
        else settingsPanel.SetActive(false);

        // 2. Возврат карт
        if (cardAnimator != null) cardAnimator.ResetGrid();

        // 3. НОВОЕ: Прячем превью XP (сдувание полоски)
        if (levelController != null) levelController.HideAllPreviews();
    }

    // --- OVERLAYS (Магазин, Лидерборд и т.д.) ---

    public void OnGlobalSettingsClicked() => OpenOverlay(globalSettingsPanel);
    public void OnLeaderboardClicked() => OpenOverlay(leaderboardPanel);
    public void OnShopClicked() => OpenOverlay(shopPanel);
    public void OnDailyQuestsClicked() => OpenOverlay(dailyQuestsPanel);

    public void OnCloseOverlayClicked()
    {
        CloseAllOverlays();
        // Если панель настроек закрыта, значит мы в главном меню -> показываем его
        if (!settingsPanel.activeSelf)
        {
            mainSelectionPanel.SetActive(true);
        }
    }

    private void OpenOverlay(GameObject panelToOpen)
    {
        if (panelToOpen == null) return;
        panelToOpen.SetActive(true);
    }

    private void CloseAllOverlays()
    {
        if (globalSettingsPanel) globalSettingsPanel.SetActive(false);
        if (statisticsPanel) statisticsPanel.SetActive(false);
        if (leaderboardPanel) leaderboardPanel.SetActive(false);
        if (shopPanel) shopPanel.SetActive(false);
        if (dailyQuestsPanel) dailyQuestsPanel.SetActive(false);
    }

    public void OnStatisticsClicked()
    {
        if (statisticsPanel != null)
        {
            statisticsPanel.SetActive(true);
            // Если у вас есть скрипт StatisticsUI, обновляем его
            /* var ui = statisticsPanel.GetComponent<StatisticsUI>();
            if (ui != null) ui.ShowStatsForGame(currentGame.type);
            */
        }
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

        // Специфичная логика для Паука (блокировка сложности)
        if (currentGame.type == GameType.Spider)
        {
            ValidateSpiderConstraints(GameSettings.SpiderSuitCount);
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

    // -----------------------------------------------------------------------
    // ROUNDS
    // -----------------------------------------------------------------------
    public void OnRoundsClicked(int index) // 0, 1, 2
    {
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
            // Блокируем кнопку старт от повторных нажатий
            startButton.interactable = false;

            // Запускаем анимацию, передавая лямбду с загрузкой сцены как callback
            exitController.PlayExitAnimation(currentGame.type, () =>
            {
                SceneManager.LoadScene(currentGame.sceneName);
            });
        }
        else
        {
            // Если контроллера нет - грузим сразу (fallback)
            SceneManager.LoadScene(currentGame.sceneName);
        }
    }
}