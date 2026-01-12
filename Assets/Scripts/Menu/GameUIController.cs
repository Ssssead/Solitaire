using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Reflection;

public class GameUIController : MonoBehaviour
{
    [Header("Main Panels")]
    public GameObject winPanel;
    public GameObject settingsPanel;
    public GameObject defeatPanel;
    public GameObject statisticsPanel;

    [Header("Confirmation Panels")] // --- НОВОЕ ---
    public GameObject exitConfirmationPanel;    // Панель "Выйти в меню?"
    public GameObject newGameConfirmationPanel; // Панель "Начать новую игру?"

    [Header("Win Panel Stats")]
    public TMP_Text winDifficultyText;
    public TMP_Text winScoreText;
    public TMP_Text winTimeText;
    public TMP_Text winMovesText;
    public TMP_Text winXPText;

    public XPProgressBar localLevelBar;
    public XPProgressBar winLevelBar;

    [Header("Notifications")]
    public LevelUpNotification globalLevelUpPopup;

    [Header("References")]
    private ICardGameMode activeGameMode;
    public UndoManager undoManager;

    private void Start()
    {
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

        // Скрываем все панели при старте
        if (winPanel) winPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);
        if (statisticsPanel) statisticsPanel.SetActive(false);

        // --- НОВОЕ: Скрываем панели подтверждения ---
        if (exitConfirmationPanel) exitConfirmationPanel.SetActive(false);
        if (newGameConfirmationPanel) newGameConfirmationPanel.SetActive(false);

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

    // --- ЛОГИКА ВЫХОДА В МЕНЮ (С ПОДТВЕРЖДЕНИЕМ) ---

    // 1. Вызывается кнопкой "Menu" в интерфейсе (или "Home")
    public void OnMenuClicked()
    {
        // 1. Проверяем, были ли сделаны ходы
        int moves = 0;
        if (StatisticsManager.Instance != null)
        {
            moves = StatisticsManager.Instance.GetCurrentMoves();
        }

        // 2. Если ходы БЫЛИ (> 0) и панель назначена -> Спрашиваем
        if (moves > 0 && exitConfirmationPanel != null)
        {
            exitConfirmationPanel.SetActive(true);
        }
        else
        {
            // 3. Если ходов 0 (или панели нет) -> Выходим сразу
            OnConfirmExitClicked();
        }
    }

    // 2. Вызывается кнопкой "ДА" в панели подтверждения выхода
    public void OnConfirmExitClicked()
    {
        if (DealCacheSystem.Instance != null) DealCacheSystem.Instance.ReturnActiveDealToQueue();
        SceneManager.LoadScene("MenuScene");
    }

    // 3. Вызывается кнопкой "НЕТ" в панели подтверждения выхода
    public void OnCancelExitClicked()
    {
        if (exitConfirmationPanel != null) exitConfirmationPanel.SetActive(false);
    }

    // --- ЛОГИКА НОВОЙ ИГРЫ (С ПОДТВЕРЖДЕНИЕМ) ---

    // 1. Вызывается кнопкой "New Game" (в панели поражения, победы или настроек)
    public void OnNewGameClicked()
    {
        // 1. Проверяем, выиграл ли игрок (открыта ли панель победы)
        bool isWinState = (winPanel != null && winPanel.activeSelf);

        // 2. Если мы ВЫИГРАЛИ -> Подтверждение НЕ нужно, начинаем сразу
        if (isWinState)
        {
            OnConfirmNewGameClicked();
            return;
        }

        // 3. Если мы ПРОИГРАЛИ (DefeatPanel) или нажали из МЕНЮ (Settings) -> Спрашиваем
        if (newGameConfirmationPanel != null)
        {
            newGameConfirmationPanel.SetActive(true);
        }
        else
        {
            // Если панели подтверждения нет, рестартим сразу
            OnConfirmNewGameClicked();
        }
    }

    // 2. Вызывается кнопкой "ДА" в панели подтверждения новой игры
    public void OnConfirmNewGameClicked()
    {
        // Закрываем панель подтверждения
        if (newGameConfirmationPanel) newGameConfirmationPanel.SetActive(false);

        // Закрываем остальные панели (Pobeda, Porazhenie, Settings)
        if (winPanel) winPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);

        // Перезапускаем игру
        if (activeGameMode != null)
        {
            activeGameMode.IsInputAllowed = true;
            activeGameMode.RestartGame();
        }
    }

    // 3. Вызывается кнопкой "НЕТ" в панели подтверждения новой игры
    public void OnCancelNewGameClicked()
    {
        if (newGameConfirmationPanel != null) newGameConfirmationPanel.SetActive(false);
    }

    // --------------------------------------------------------

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

        if (winScoreText && activeGameMode != null)
        {
            int finalScore = 0;
            var modeType = activeGameMode.GetType();

            var scoreProp = modeType.GetProperty("CurrentScore");
            if (scoreProp != null)
            {
                finalScore = (int)scoreProp.GetValue(activeGameMode);
            }
            else
            {
                var smField = modeType.GetField("scoreManager");
                if (smField != null)
                {
                    var smObj = smField.GetValue(activeGameMode);
                    if (smObj != null)
                    {
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

        if (StatisticsManager.Instance != null)
        {
            if (winMovesText) winMovesText.text = StatisticsManager.Instance.GetCurrentMoves().ToString();

            float duration = StatisticsManager.Instance.GetLastGameDurationFromHistory();
            if (winTimeText) winTimeText.text = FormatTime(duration);
            if (winXPText) winXPText.text = $"+ {StatisticsManager.Instance.LastXPGained} XP";

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

            if (winLevelBar != null)
            {
                StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);

                if (data != null)
                {
                    int currentLevel = data.currentLevel;
                    int currentXP = data.currentXP;
                    int targetXP = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;
                    int xpGained = StatisticsManager.Instance.LastXPGained;

                    int startXP = currentXP - xpGained;

                    if (startXP < 0)
                    {
                        int oldLevel = currentLevel - 1;
                        if (oldLevel < 1) oldLevel = 1;
                        int oldTarget = oldLevel * 500;
                        int oldXP = oldTarget + startXP;

                        winLevelBar.AnimateLevelUp(oldLevel, oldXP, oldTarget, currentLevel, currentXP, targetXP);
                    }
                    else
                    {
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
        if (statisticsPanel != null) statisticsPanel.SetActive(true);
    }

    public void OnCloseStatisticsClicked()
    {
        if (statisticsPanel != null) statisticsPanel.SetActive(false);
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