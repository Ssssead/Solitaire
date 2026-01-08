// GameUIController.cs [UPDATED WITH REAL SCORE LOGIC]
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Reflection; // Нужно для рефлексии

public class GameUIController : MonoBehaviour
{
    [Header("Main Panels")]
    public GameObject winPanel;
    public GameObject settingsPanel;
    public GameObject defeatPanel;
    public GameObject statisticsPanel;

    [Header("Win Panel Stats")]
    // Ссылки на тексты внутри WinPanel (создайте их в Unity)
    public TMP_Text winDifficultyText;
    public TMP_Text winScoreText;
    public TMP_Text winTimeText;
    public TMP_Text winMovesText;
    public TMP_Text winXPText;

    public XPProgressBar localLevelBar;
    public XPProgressBar winLevelBar;   // Бар в панели победы (WinPanel)

    [Header("Notifications")]
    public LevelUpNotification globalLevelUpPopup;

    [Header("References")]
    // ВМЕСТО КОНКРЕТНОГО МЕНЕДЖЕРА ИСПОЛЬЗУЕМ ИНТЕРФЕЙС
    private ICardGameMode activeGameMode;
    public UndoManager undoManager;

    private void Start()
    {
        // 1. Ищем любой активный режим игры на сцене
        if (activeGameMode == null)
        {
            foreach (var obj in FindObjectsOfType<MonoBehaviour>())
            {
                if (obj is ICardGameMode mode)
                {
                    activeGameMode = mode;
                    break;
                }
            }
        }

        if (undoManager == null) undoManager = FindObjectOfType<UndoManager>();

        // Скрываем панели
        if (winPanel) winPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);
        if (statisticsPanel) statisticsPanel.SetActive(false);

        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnLevelUp += HandleLevelUp;
        }
    }

    private void OnDestroy()
    {
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnLevelUp -= HandleLevelUp;
        }
    }

    // --- МЕТОДЫ ПОБЕДЫ И ПОРАЖЕНИЯ ---

    public void OnGameWon()
    {
        if (defeatPanel) defeatPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(true);

        UpdateWinPanelStats();

        if (activeGameMode != null) activeGameMode.IsInputAllowed = false;
    }

    private void UpdateWinPanelStats()
    {
        if (winDifficultyText) winDifficultyText.text = GameSettings.CurrentDifficulty.ToString();

        // --- ОБНОВЛЕНИЕ: ПОЛУЧЕНИЕ СЧЕТА ---
        if (winScoreText && activeGameMode != null)
        {
            int finalScore = 0;
            var modeType = activeGameMode.GetType();

            // 1. Сначала ищем свойство CurrentScore (реализовано в SpiderModeManager)
            var scoreProp = modeType.GetProperty("CurrentScore");
            if (scoreProp != null)
            {
                finalScore = (int)scoreProp.GetValue(activeGameMode);
            }
            // 2. Если нет, ищем поле scoreManager (для Klondike, если интерфейс не менялся)
            else
            {
                var smField = modeType.GetField("scoreManager");
                if (smField != null)
                {
                    var smObj = smField.GetValue(activeGameMode);
                    if (smObj != null)
                    {
                        // Внутри ScoreManager ищем CurrentScore
                        var innerScoreProp = smObj.GetType().GetProperty("CurrentScore");
                        if (innerScoreProp != null)
                        {
                            finalScore = (int)innerScoreProp.GetValue(smObj);
                        }
                    }
                }
            }

            winScoreText.text = finalScore.ToString();
        }
        // -----------------------------------

        if (StatisticsManager.Instance != null)
        {
            if (winMovesText) winMovesText.text = StatisticsManager.Instance.GetCurrentMoves().ToString();

            float duration = StatisticsManager.Instance.GetLastGameDurationFromHistory();
            if (winTimeText) winTimeText.text = FormatTime(duration);
            if (winXPText) winXPText.text = $"+ {StatisticsManager.Instance.LastXPGained} XP";

            // --- УНИВЕРСАЛЬНОЕ ИМЯ ИГРЫ ---
            string gameName = activeGameMode != null ? activeGameMode.GameName : "Unknown";

            if (winLevelBar != null)
            {
                StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);
                if (data != null)
                {
                    int target = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;
                    winLevelBar.UpdateBar(data.currentLevel, data.currentXP, target);
                }
            }

            // --- АНИМАЦИЯ БАРА УРОВНЯ ---
            if (winLevelBar != null)
            {
                StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);

                if (data != null)
                {
                    int currentLevel = data.currentLevel;
                    int currentXP = data.currentXP;
                    int targetXP = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;
                    int xpGained = StatisticsManager.Instance.LastXPGained;

                    // Вычисляем XP до начисления
                    int startXP = currentXP - xpGained;

                    if (startXP < 0)
                    {
                        // Level Up
                        int oldLevel = currentLevel - 1;
                        if (oldLevel < 1) oldLevel = 1;
                        int oldTarget = oldLevel * 500;
                        int oldXP = oldTarget + startXP;

                        winLevelBar.AnimateLevelUp(oldLevel, oldXP, oldTarget, currentLevel, currentXP, targetXP);
                    }
                    else
                    {
                        // Normal
                        winLevelBar.AnimateBar(currentLevel, startXP, currentXP, targetXP);
                    }
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

    public void OnGameLost()
    {
        if (winPanel != null && winPanel.activeSelf) return;
        if (defeatPanel != null && !defeatPanel.activeSelf)
        {
            defeatPanel.SetActive(true);
            if (activeGameMode != null) activeGameMode.IsInputAllowed = false;
        }
    }

    // --- ОСТАЛЬНЫЕ МЕТОДЫ (Без изменений) ---

    public void OnMenuClicked()
    {
        if (DealCacheSystem.Instance != null) DealCacheSystem.Instance.ReturnActiveDealToQueue();
        SceneManager.LoadScene("MenuScene");
    }

    private void HandleLevelUp(string context, int newLevel)
    {
        if (context == "Account" && globalLevelUpPopup != null)
        {
            globalLevelUpPopup.ShowNotification(newLevel);
        }
    }

    public void OnSettingsClicked()
    {
        bool isActive = settingsPanel.activeSelf;
        settingsPanel.SetActive(!isActive);

        if (!isActive && localLevelBar != null && StatisticsManager.Instance != null)
        {
            string gameName = activeGameMode != null ? activeGameMode.GameName : "Klondike";
            StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);
            if (data != null)
            {
                int target = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;
                localLevelBar.UpdateBar(data.currentLevel, data.currentXP, target);
            }
        }
    }

    public void OnStatisticsClicked()
    {
        if (statisticsPanel != null)
        {
            statisticsPanel.SetActive(true);
        }
    }

    public void OnCloseStatisticsClicked()
    {
        if (statisticsPanel != null) statisticsPanel.SetActive(false);
    }

    public void OnNewGameClicked()
    {
        if (winPanel) winPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);

        if (activeGameMode != null)
        {
            activeGameMode.IsInputAllowed = true;
            activeGameMode.RestartGame();
        }
    }

    public void OnUndoOneClicked()
    {
        if (defeatPanel) defeatPanel.SetActive(false);
        if (activeGameMode != null) activeGameMode.IsInputAllowed = true;

        if (undoManager != null && undoManager.undoButton.interactable)
            undoManager.undoButton.onClick.Invoke();
    }

    public void OnUndoAllClicked()
    {
        if (defeatPanel) defeatPanel.SetActive(false);
        if (activeGameMode != null) activeGameMode.IsInputAllowed = true;

        if (undoManager != null && undoManager.undoAllButton.interactable)
            undoManager.undoAllButton.onClick.Invoke();
    }
}