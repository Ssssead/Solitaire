using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Reflection;
using System.Collections;

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
    [Header("Effects")]
    public SceneExitAnimator exitAnimator;

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
        if (exitAnimator == null) exitAnimator = GetComponent<SceneExitAnimator>() ?? FindObjectOfType<SceneExitAnimator>();
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

    /// <summary>
    /// Универсальный метод для открытия/закрытия панелей с анимацией
    /// </summary>
    private void TogglePanelAnimated(GameObject panel, bool show)
    {
        if (panel == null) return;

        // Если включаем - сначала активируем объект, потом анимация
        if (show)
        {
            panel.SetActive(true);
            StartCoroutine(AnimatePanelRoutine(panel, true));
        }
        else
        {
            // Если выключаем - сначала анимация, потом деактивация
            StartCoroutine(AnimatePanelRoutine(panel, false));
        }
    }

    private IEnumerator AnimatePanelRoutine(GameObject panel, bool show)
    {
        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt == null) yield break;

        float duration = 0.3f;
        float elapsed = 0f;

        // Получаем ширину экрана для вычисления позиции "за кадром слева"
        float screenWidth = typeOfCanvasOverlay(panel) ? Screen.width : 1920f; // Упрощенно
        float offScreenX = -screenWidth;

        Vector2 centerPos = new Vector2(0, 0); // Предполагаем, что центр панели - это (0,0)
        // ВАЖНО: Убедитесь, что в префабах панелей Anchors стоят по центру или Stretch, 
        // и позиция (0,0) действительно означает центр экрана.

        Vector2 startPos, endPos;

        if (show)
        {
            // Летим СЛЕВА -> В ЦЕНТР
            startPos = new Vector2(offScreenX, 0);
            endPos = centerPos;
            rt.anchoredPosition = startPos; // Мгновенно ставим влево перед полетом
        }
        else
        {
            // Летим ИЗ ЦЕНТРА -> ВЛЕВО
            startPos = rt.anchoredPosition;
            endPos = new Vector2(offScreenX, 0);
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            // Easing OutCubic (быстро начинается, плавно тормозит)
            t = 1f - Mathf.Pow(1f - t, 3);

            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rt.anchoredPosition = endPos;

        if (!show)
        {
            panel.SetActive(false);
        }
    }

    // Вспомогательный метод определения типа канваса (не критично, можно просто брать -2000)
    private bool typeOfCanvasOverlay(GameObject go) => true;

    // --- ЛОГИКА ВЫХОДА В МЕНЮ (С ПОДТВЕРЖДЕНИЕМ) ---

    // 1. Вызывается кнопкой "Menu" в интерфейсе (или "Home")
    public void OnMenuClicked()
    {
        int moves = (StatisticsManager.Instance != null) ? StatisticsManager.Instance.GetCurrentMoves() : 0;

        if (moves > 0 && exitConfirmationPanel != null)
        {
            // Вместо SetActive(true) используем анимацию
            TogglePanelAnimated(exitConfirmationPanel, true);
        }
        else
        {
            OnConfirmExitClicked();
        }
    }

    // 2. Вызывается кнопкой "ДА" в панели подтверждения выхода
    public void OnConfirmExitClicked()
    {
        // 1. Убираем панель подтверждения (уезжает влево)
        if (exitConfirmationPanel != null && exitConfirmationPanel.activeSelf)
        {
            TogglePanelAnimated(exitConfirmationPanel, false);
        }

        // 2. Логика с кэшом (сохраняем/сбрасываем)
        if (DealCacheSystem.Instance != null)
        {
            int moves = (StatisticsManager.Instance != null) ? StatisticsManager.Instance.GetCurrentMoves() : 0;
            if (moves > 0) DealCacheSystem.Instance.DiscardActiveDeal();
            else DealCacheSystem.Instance.ReturnActiveDealToQueue();
        }

        // 3. ЗАПУСК АНИМАЦИИ ВЫХОДА
        if (exitAnimator != null)
        {
            // Блокируем ввод, чтобы игрок не нажал ничего лишнего
            if (activeGameMode != null) activeGameMode.IsInputAllowed = false;

            exitAnimator.PlayExitSequence(() =>
            {
                // 4. Когда анимация закончилась - грузим сцену
                SceneManager.LoadScene("MenuScene");
            });
        }
        else
        {
            // Если аниматора нет - выходим мгновенно (Fallback)
            SceneManager.LoadScene("MenuScene");
        }
    }

    // 3. Вызывается кнопкой "НЕТ" в панели подтверждения выхода
    public void OnCancelExitClicked()
    {
        // Уезжает влево
        if (exitConfirmationPanel != null) TogglePanelAnimated(exitConfirmationPanel, false);
    }

    // --- ЛОГИКА НОВОЙ ИГРЫ (С ПОДТВЕРЖДЕНИЕМ) ---

    // 1. Вызывается кнопкой "New Game" (в панели поражения, победы или настроек)
    public void OnNewGameClicked()
    {
        bool isWinState = (winPanel != null && winPanel.activeSelf);
        if (isWinState)
        {
            OnConfirmNewGameClicked();
            return;
        }

        if (newGameConfirmationPanel != null)
        {
            // Выезжает слева
            TogglePanelAnimated(newGameConfirmationPanel, true);
        }
        else
        {
            OnConfirmNewGameClicked();
        }
    }

    // 2. Вызывается кнопкой "ДА" в панели подтверждения новой игры
    public void OnConfirmNewGameClicked()
    {
        // Закрываем все панели (анимацией или мгновенно? Для рестарта лучше мгновенно или быстро)
        if (newGameConfirmationPanel) TogglePanelAnimated(newGameConfirmationPanel, false);
        
        // Остальные панели лучше скрыть мгновенно, чтобы не мелькали при рестарте
        if (winPanel) winPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);

        if (undoManager != null) undoManager.ResetHistory();
        if (DealCacheSystem.Instance != null) DealCacheSystem.Instance.DiscardActiveDeal();

        if (activeGameMode != null)
        {
            activeGameMode.IsInputAllowed = true;
            activeGameMode.RestartGame();
        }
    }

    // 3. Вызывается кнопкой "НЕТ" в панели подтверждения новой игры
    public void OnCancelNewGameClicked()
    {
        if (newGameConfirmationPanel != null) TogglePanelAnimated(newGameConfirmationPanel, false);
    }

    // --------------------------------------------------------

    public void OnGameWon(int manualMoves = -1)
    {
        if (defeatPanel) defeatPanel.SetActive(false);

        // Win выезжает слева
        if (winPanel)
        {
            UpdateWinPanelStats(manualMoves); // Сначала обновляем данные
            TogglePanelAnimated(winPanel, true); // Потом показываем
        }

        if (activeGameMode != null) activeGameMode.IsInputAllowed = false;
    }

    private void UpdateWinPanelStats(int manualMoves)
    {
        // 1. Сложность
        if (winDifficultyText) winDifficultyText.text = GameSettings.CurrentDifficulty.ToString();

        // 2. Счет (через Reflection)
        if (activeGameMode != null)
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
                // Fallback для ScoreManager
                var smField = modeType.GetField("scoreManager");
                if (smField != null)
                {
                    var smObj = smField.GetValue(activeGameMode);
                    if (smObj != null)
                    {
                        var innerScoreProp = smObj.GetType().GetProperty("Score") ?? smObj.GetType().GetProperty("CurrentScore");
                        if (innerScoreProp != null)
                        {
                            finalScore = (int)innerScoreProp.GetValue(smObj);
                        }
                    }
                }
            }
            if (winScoreText) winScoreText.text = finalScore.ToString();
        }

        // 3. Статистика (Время, Ходы, XP и Анимация Бара)
        if (StatisticsManager.Instance != null)
        {
            // --- Ходы (приоритет manualMoves) ---
            int movesToShow = (manualMoves >= 0) ? manualMoves : StatisticsManager.Instance.GetCurrentMoves();
            if (winMovesText) winMovesText.text = movesToShow.ToString();

            // --- Время ---
            float duration = StatisticsManager.Instance.LastGameTime; // Или GetLastGameDurationFromHistory() если такой метод есть
            if (winTimeText) winTimeText.text = FormatTime(duration);

            // --- XP Текст ---
            if (winXPText) winXPText.text = $"+ {StatisticsManager.Instance.LastXPGained} XP";

            // --- АНИМАЦИЯ БАРА (Восстановленная логика) ---
            string gameName = activeGameMode != null ? activeGameMode.GameName : "Unknown";

            if (winLevelBar != null)
            {
                StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);

                if (data != null)
                {
                    int currentLevel = data.currentLevel;
                    int currentXP = data.currentXP;
                    // Если в StatData нет xpForNextLevel, используем 500 как заглушку (как в старом коде)
                    int targetXP = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;
                    int xpGained = StatisticsManager.Instance.LastXPGained;

                    // Вычисляем, сколько XP было ДО победы
                    int startXP = currentXP - xpGained;

                    if (startXP < 0)
                    {
                        // Если был Level Up (старт был на предыдущем уровне)
                        int oldLevel = currentLevel - 1;
                        if (oldLevel < 1) oldLevel = 1;

                        // Примерный расчет для старого уровня (если нет точных данных)
                        // В идеале StatisticsManager должен возвращать targetXP для любого уровня
                        int oldTarget = oldLevel * 500; // Упрощенная формула, замените на вашу
                        int oldXP = oldTarget + startXP; // startXP тут отрицательный, так что это остаток

                        // Запускаем сложную анимацию перехода уровня
                        winLevelBar.AnimateLevelUp(oldLevel, oldXP, oldTarget, currentLevel, currentXP, targetXP);
                    }
                    else
                    {
                        // Обычная анимация заполнения
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
            // Defeat выезжает слева
            TogglePanelAnimated(defeatPanel, true);
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
        // Если была активна - убираем влево, если нет - достаем слева
        TogglePanelAnimated(settingsPanel, !isActive);

        // Логику обновления бара оставляем, но вызываем её только если открываем
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
        if (statisticsPanel != null) TogglePanelAnimated(statisticsPanel, true);
    }
    public void OnCloseStatisticsClicked()
    {
        if (statisticsPanel != null) TogglePanelAnimated(statisticsPanel, false);
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