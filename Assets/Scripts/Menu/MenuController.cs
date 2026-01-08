using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MenuController : MonoBehaviour
{
    [System.Serializable]
    public struct GameDefinition
    {
        public string name;
        public GameType type;
        public string sceneName;

        [Header("UI Options")]
        public bool showDrawModeSlider;
        public bool showSuitSelector;
        public bool showDifficulty;
    }

    [Header("Game Definitions")]
    public List<GameDefinition> games;

    [Header("UI Panels")]
    public GameObject mainSelectionPanel;
    public GameObject settingsPanel;
    public GameObject statisticsPanel;

    [Header("Level Controller")]
    public MenuLevelController levelController;

    [Header("Settings UI Elements")]
    public GameObject drawModeContainer;
    public GameObject difficultyContainer;
    public GameObject suitSelectionContainer;

    [Header("Draw Mode Toggle (Visuals)")]
    public RectTransform toggleHandle;
    public Image toggleBackground;
    public Color toggleOnColor = new Color(0.2f, 0.8f, 0.2f);
    public Color toggleOffColor = new Color(0.3f, 0.3f, 0.3f);
    public float handleXOff = -20f;
    public float handleXOn = 20f;

    [Header("Difficulty Buttons (Visuals)")]
    public Button[] diffButtons; // Порядок: 0-Easy, 1-Medium, 2-Hard
    public Color diffSelectedColor = Color.yellow;
    public Color diffNormalColor = Color.white;
    public Color diffDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Цвет заблокированной кнопки

    [Header("Suit Buttons (Spider)")]
    public Button[] suitButtons; // Порядок: 0-[1 масть], 1-[2 масти], 2-[4 масти]

    private GameDefinition currentGame;
    private bool isDrawThree = false;

    private void Start()
    {
        if (statisticsPanel != null) statisticsPanel.SetActive(false);
        ShowMainPanel();
    }

    // --- Логика переходов ---

    public void ShowMainPanel()
    {
        mainSelectionPanel.SetActive(true);
        settingsPanel.SetActive(false);
        if (statisticsPanel != null) statisticsPanel.SetActive(false);
    }

    public void OnGameSelected(int gameIndex)
    {
        if (gameIndex < 0 || gameIndex >= games.Count) return;

        currentGame = games[gameIndex];
        GameSettings.CurrentGameType = currentGame.type;
        string gameName = currentGame.type.ToString();

        // 1. Сначала включаем панель
        mainSelectionPanel.SetActive(false);
        settingsPanel.SetActive(true);

        // 2. Настраиваем UI под игру
        SetupSettingsPanel();

        // 3. Обновляем бар
        if (levelController != null)
        {
            levelController.UpdateLocalBar(gameName);
        }
    }

    // --- СТАТИСТИКА ---
    public void OnStatisticsClicked()
    {
        if (statisticsPanel != null)
        {
            statisticsPanel.SetActive(true);
            var ui = statisticsPanel.GetComponent<StatisticsUI>();
            if (ui != null)
            {
                ui.ShowStatsForGame(currentGame.type);
            }
        }
    }

    public void OnCloseStatisticsClicked()
    {
        if (statisticsPanel != null) statisticsPanel.SetActive(false);
    }

    // ------------------------------------

    private void SetupSettingsPanel()
    {
        if (drawModeContainer) drawModeContainer.SetActive(currentGame.showDrawModeSlider);
        if (suitSelectionContainer) suitSelectionContainer.SetActive(currentGame.showSuitSelector);
        if (difficultyContainer) difficultyContainer.SetActive(currentGame.showDifficulty);

        // Сбрасываем сложность на Medium при каждом входе (безопасный вариант)
        SetDifficulty((int)Difficulty.Medium);
        SetDrawMode(false);

        // --- ИЗМЕНЕНИЕ: Дефолт для Паука ---
        if (currentGame.type == GameType.Spider)
        {
            // По умолчанию выбираем 1 масть. Это также запустит логику блокировки Hard.
            OnSuitClicked(1);
        }
        else
        {
            // Для других игр разблокируем все сложности
            foreach (var btn in diffButtons) btn.interactable = true;
            UpdateDifficultyVisuals();
        }
    }

    // --- Обработчики UI настроек ---

    // --- РЕЖИМ КОСЫНКИ (DRAW 3) ---
    public void OnDrawModeToggleClicked()
    {
        bool newState = !(GameSettings.KlondikeDrawCount == 3);
        SetDrawMode(newState);
    }

    private void SetDrawMode(bool drawThree)
    {
        isDrawThree = drawThree;
        float targetX = isDrawThree ? handleXOn : handleXOff;
        if (toggleHandle) toggleHandle.anchoredPosition = new Vector2(targetX, toggleHandle.anchoredPosition.y);
        if (toggleBackground) toggleBackground.color = isDrawThree ? toggleOnColor : toggleOffColor;
        GameSettings.KlondikeDrawCount = isDrawThree ? 3 : 1;
    }

    // --- РЕЖИМ ПАУКА (SUITS) ---
    public void OnSuitClicked(int suitCount)
    {
        GameSettings.SpiderSuitCount = suitCount;

        UpdateSuitVisuals(suitCount);

        // --- ИЗМЕНЕНИЕ: Валидация сложностей ---
        ValidateSpiderConstraints(suitCount);
    }

    private void UpdateSuitVisuals(int count)
    {
        // Индексы кнопок: 0 -> 1 масть, 1 -> 2 масти, 2 -> 4 масти
        int selectedIndex = -1;
        if (count == 1) selectedIndex = 0;
        else if (count == 2) selectedIndex = 1;
        else if (count == 4) selectedIndex = 2;

        for (int i = 0; i < suitButtons.Length; i++)
        {
            if (suitButtons[i] == null) continue;
            suitButtons[i].image.color = (i == selectedIndex) ? diffSelectedColor : diffNormalColor;
        }
    }

    // --- ИЗМЕНЕНИЕ: Логика блокировки кнопок ---
    private void ValidateSpiderConstraints(int suitCount)
    {
        // Сначала разблокируем всё
        foreach (var btn in diffButtons) btn.interactable = true;

        if (suitCount == 1)
        {
            // 1 масть: Блокируем HARD (индекс 2)
            if (diffButtons.Length > 2) diffButtons[2].interactable = false;

            // Если был выбран Hard, переключаем на Medium
            if (GameSettings.CurrentDifficulty == Difficulty.Hard)
            {
                SetDifficulty((int)Difficulty.Medium);
            }
        }
        else if (suitCount == 4)
        {
            // 4 масти: Блокируем EASY (индекс 0)
            if (diffButtons.Length > 0) diffButtons[0].interactable = false;

            // Если был выбран Easy, переключаем на Medium
            if (GameSettings.CurrentDifficulty == Difficulty.Easy)
            {
                SetDifficulty((int)Difficulty.Medium);
            }
        }

        // Обновляем цвета кнопок сложности с учетом блокировки
        UpdateDifficultyVisuals();
    }

    public void OnDifficultyClicked(int diffIndex)
    {
        SetDifficulty(diffIndex);
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

            if (!diffButtons[i].interactable)
            {
                // Если кнопка заблокирована - серый цвет
                diffButtons[i].image.color = diffDisabledColor;
            }
            else
            {
                // Если доступна - проверяем, выбрана ли она
                diffButtons[i].image.color = (i == currentIndex) ? diffSelectedColor : diffNormalColor;
            }
        }
    }

    public void OnStartGameClicked()
    {
        if (DealCacheSystem.Instance != null) DealCacheSystem.Instance.ReturnActiveDealToQueue();

        if (!string.IsNullOrEmpty(currentGame.sceneName))
        {
            SceneManager.LoadScene(currentGame.sceneName);
        }
        else
        {
            Debug.LogError("Scene name not set for this game!");
        }
    }

    public void OnBackClicked()
    {
        ShowMainPanel();
    }
}