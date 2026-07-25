using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[System.Serializable]
public struct GameIndicatorMapping
{
    public GameType gameType;
    public Sprite sprite;
}

[System.Serializable]
public class GameBarData
{
    public GameType gameType;
    public Image barImage;
    public Image indicatorImage;

    [HideInInspector] public int gamesCount;
    [HideInInspector] public float percentage;
    [HideInInspector] public Color originalBarColor;
    [HideInInspector] public Color targetBarColor;
}

public class AppGlobalStatsUI : MonoBehaviour
{
    [Header("Header")]
    public Button closeButton;

    [Header("Main Stats")]
    public TMP_Text gamesPlayedText;
    public TMP_Text winsText;
    public TMP_Text favoriteGameText;

    [Header("Detailed Stats")]
    public TMP_Text totalTimeText;
    public TMP_Text totalMovesText;
    public TMP_Text questsCompletedText;

    [Header("Level & XP")]
    public TMP_Text levelText;
    public TMP_Text currentLevelXpText;
    public TMP_Text totalXpGlobalText;
    public TMP_Text totalXpGamesText;

    [Header("Records (Cross-Game)")]
    public TMP_Text bestScoreText;
    public TMP_Text bestTimeText;
    public TMP_Text bestMovesText;

    [Header("Streaks (Win Streaks)")]
    public TMP_Text currentStreakText;
    public TMP_Text bestStreakText;

    // ---> НОВЫЙ БЛОК: СТАТИСТИКА ПО КВЕСТАМ (7 ЗНАЧЕНИЙ) <---
    [Header("Quest Statistics (7 Values)")]
    public TMP_Text questStreakCurrentText;       // Заданий подряд (текущий)
    public TMP_Text questStreakBestText;          // Заданий подряд (рекорд)
    public TMP_Text dayStreakCurrentText;         // Дней подряд (текущий)
    public TMP_Text dayStreakBestText;            // Дней подряд (рекорд)
    public TMP_Text perfectDaysTotalUiText;       // Всего идеальных дней (6/6)
    public TMP_Text perfectDaysStreakCurrentText; // Идеальных дней подряд (текущий)
    public TMP_Text perfectDaysStreakBestText;    // Идеальных дней подряд (рекорд)

    [Header("History (Last 10 Games)")]
    public Image[] historySlots;
    public Sprite winIcon;
    public Sprite lossIcon;
    public Sprite emptyIcon;
    public Image[] historyIndicators;
    public GameIndicatorMapping[] gameIndicators;

    [Header("--- Progress Bar Settings ---")]
    public float hoverTransitionSpeed = 12f;
    public GameBarData[] gameBarItems;

    [Header("Progress Bar Tooltip")]
    public GameObject barTooltipPanel;
    public TMP_Text barTooltipText;
    public Vector2 barTooltipOffset = new Vector2(15f, -15f);

    private void Start()
    {
        if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);

        InitializeHoverTriggers();
    }

    private void OnEnable()
    {
        RefreshGlobalUI();
    }

    private void Update()
    {
        // Плавное изменение цвета полосок
        float lerpStep = hoverTransitionSpeed * Time.deltaTime;
        foreach (var data in gameBarItems)
        {
            if (data.barImage != null)
            {
                data.barImage.color = Color.Lerp(data.barImage.color, data.targetBarColor, lerpStep);
            }
        }

        // Тултип следует за мышкой
        if (barTooltipPanel != null && barTooltipPanel.activeSelf)
        {
            Vector2 mousePos = Input.mousePosition;
            Vector2 finalPos = mousePos + barTooltipOffset;

            RectTransform rt = barTooltipPanel.GetComponent<RectTransform>();
            Canvas parentCanvas = GetComponentInParent<Canvas>();

            if (rt != null && parentCanvas != null)
            {
                float width = rt.rect.width * parentCanvas.scaleFactor;
                float height = rt.rect.height * parentCanvas.scaleFactor;

                if (finalPos.x + width > Screen.width) finalPos.x = mousePos.x - width - barTooltipOffset.x;
                if (finalPos.y - height < 0) finalPos.y = mousePos.y + height + Mathf.Abs(barTooltipOffset.y);
            }

            barTooltipPanel.transform.position = finalPos;
        }
    }

    public void RefreshGlobalUI()
    {
        if (StatisticsManager.Instance == null) return;

        StatData globalData = StatisticsManager.Instance.GetGlobalStats();
        if (globalData == null) globalData = new StatData();

        FillBasicAndDetailedStats(globalData);
        FillLevelAndXP(globalData);
        FillCrossGameRecords();
        FillStreaks(globalData);
        FillQuestStats(); // <--- ВЫЗОВ НОВОГО МЕТОДА
        UpdateHistorySlots(globalData.history);

        FillProgressBar();
    }

    // --- ЛОГИКА ПРОГРЕСС-БАРА И НАВЕДЕНИЯ ---

    private void InitializeHoverTriggers()
    {
        foreach (var data in gameBarItems)
        {
            if (data.barImage != null)
            {
                data.originalBarColor = data.barImage.color;
                data.targetBarColor = data.originalBarColor;
            }

            if (data.indicatorImage != null)
            {
                EventTrigger trigger = data.indicatorImage.gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = data.indicatorImage.gameObject.AddComponent<EventTrigger>();

                GameType typeToPass = data.gameType;

                EventTrigger.Entry entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entryEnter.callback.AddListener((eventData) => { OnBarHoverEnter(typeToPass); });
                trigger.triggers.Add(entryEnter);

                EventTrigger.Entry entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                entryExit.callback.AddListener((eventData) => { OnBarHoverExit(); });
                trigger.triggers.Add(entryExit);
            }
        }

        if (barTooltipPanel != null) barTooltipPanel.SetActive(false);
    }

    private void FillProgressBar()
    {
        foreach (var data in gameBarItems)
        {
            data.gamesCount = 0;
            data.percentage = 0f;
        }

        int totalGames = 0;
        var allEntries = StatisticsManager.Instance.GetAllEntriesRaw();

        if (allEntries != null)
        {
            foreach (var entry in allEntries)
            {
                if (entry.key.EndsWith("_Global") && entry.key != "Global")
                {
                    string gName = entry.key.Replace("_Global", "");
                    if (System.Enum.TryParse(gName, out GameType gType))
                    {
                        foreach (var data in gameBarItems)
                        {
                            if (data.gameType == gType)
                            {
                                data.gamesCount = entry.data.gamesStarted;
                                totalGames += data.gamesCount;
                                break;
                            }
                        }
                    }
                }
            }
        }

        float cumulativeFill = 0f;
        foreach (var data in gameBarItems)
        {
            if (totalGames > 0)
            {
                data.percentage = (float)data.gamesCount / totalGames;
                cumulativeFill += data.percentage;
                if (data.barImage != null) data.barImage.fillAmount = cumulativeFill;
            }
            else
            {
                data.percentage = 0f;
                if (data.barImage != null) data.barImage.fillAmount = 0f;
            }
        }
    }

    private void OnBarHoverEnter(GameType hoveredType)
    {
        foreach (var data in gameBarItems)
        {
            bool isCurrent = (data.gameType == hoveredType);

            Color dimmedBar = new Color(data.originalBarColor.r * 0.4f, data.originalBarColor.g * 0.4f, data.originalBarColor.b * 0.4f, data.originalBarColor.a);
            data.targetBarColor = isCurrent ? data.originalBarColor : dimmedBar;

            if (isCurrent)
            {
                ShowBarTooltip(data);
            }
        }
    }

    private void OnBarHoverExit()
    {
        foreach (var data in gameBarItems)
        {
            data.targetBarColor = data.originalBarColor;
        }

        if (barTooltipPanel != null) barTooltipPanel.SetActive(false);
    }

    private void ShowBarTooltip(GameBarData data)
    {
        if (barTooltipPanel == null || barTooltipText == null) return;

        string locName = GetLocalizedGameName(data.gameType.ToString());
        float pct = data.percentage * 100f;

        barTooltipText.text = $"{locName} {pct:F2}%";
        barTooltipPanel.SetActive(true);
    }

    // --- ОСТАЛЬНЫЕ МЕТОДЫ ---

    private void FillBasicAndDetailedStats(StatData data)
    {
        if (gamesPlayedText) gamesPlayedText.text = data.gamesStarted.ToString();
        if (winsText) winsText.text = data.gamesWon.ToString();

        if (favoriteGameText)
        {
            string favGame = FindFavoriteGame();
            favoriteGameText.text = favGame == "None" ? "-" : GetLocalizedGameName(favGame);
        }

        if (totalTimeText) totalTimeText.text = FormatTimeLocalized(data.totalTime);
        if (totalMovesText) totalMovesText.text = data.totalMoves.ToString("N0");
        if (questsCompletedText) questsCompletedText.text = data.questsCompleted.ToString();
    }

    private void FillStreaks(StatData data)
    {
        if (currentStreakText) currentStreakText.text = data.currentStreak.ToString();
        if (bestStreakText) bestStreakText.text = data.bestStreak.ToString();
    }

    // НОВЫЙ МЕТОД: Заполнение 7 параметров квестов
    private void FillQuestStats()
    {
        if (QuestManager.Instance == null || QuestManager.Instance.saveData == null) return;

        var qData = QuestManager.Instance.saveData;

        // 1. Заданий подряд
        if (questStreakCurrentText) questStreakCurrentText.text = qData.questsInARowCurrent.ToString();
        if (questStreakBestText) questStreakBestText.text = qData.questsInARowBest.ToString();

        // 2. Дней подряд
        if (dayStreakCurrentText) dayStreakCurrentText.text = qData.minorStreakCurrent.ToString();
        if (dayStreakBestText) dayStreakBestText.text = qData.minorStreakBest.ToString();

        // 3. Идеальные дни (6/6)
        if (perfectDaysTotalUiText) perfectDaysTotalUiText.text = qData.perfectDaysTotal.ToString();
        if (perfectDaysStreakCurrentText) perfectDaysStreakCurrentText.text = qData.perfectDaysStreakCurrent.ToString();
        if (perfectDaysStreakBestText) perfectDaysStreakBestText.text = qData.perfectDaysStreakBest.ToString();
    }

    private string FindFavoriteGame()
    {
        var allEntries = StatisticsManager.Instance.GetAllEntriesRaw();
        if (allEntries == null) return "None";

        Dictionary<string, int> gameCounts = new Dictionary<string, int>();
        foreach (var entry in allEntries)
        {
            if (entry.key.EndsWith("_Global") && entry.key != "Global")
            {
                string gameName = entry.key.Replace("_Global", "");
                if (!gameCounts.ContainsKey(gameName)) gameCounts[gameName] = 0;
                gameCounts[gameName] += entry.data.gamesStarted;
            }
        }

        if (gameCounts.Count == 0) return "None";

        string favorite = "None";
        int maxStarts = 0;
        foreach (var kvp in gameCounts)
        {
            if (kvp.Value > maxStarts)
            {
                maxStarts = kvp.Value;
                favorite = kvp.Key;
            }
        }

        return maxStarts > 0 ? favorite : "None";
    }

    private void FillLevelAndXP(StatData globalData)
    {
        if (levelText) levelText.text = globalData.currentLevel.ToString();
        if (currentLevelXpText) currentLevelXpText.text = $"{globalData.currentXP} / {globalData.xpForNextLevel}";

        int cumulativeGlobalXP = CalculateCumulativeXP(globalData, 2000);
        if (totalXpGlobalText) totalXpGlobalText.text = cumulativeGlobalXP.ToString("N0");

        int cumulativeGamesXP = 0;
        var allEntries = StatisticsManager.Instance.GetAllEntriesRaw();
        if (allEntries != null)
        {
            foreach (var entry in allEntries)
            {
                if (entry.key.EndsWith("_Global") && entry.key != "Global")
                {
                    cumulativeGamesXP += CalculateCumulativeXP(entry.data, 500);
                }
            }
        }
        if (totalXpGamesText) totalXpGamesText.text = cumulativeGamesXP.ToString("N0");
    }

    private int CalculateCumulativeXP(StatData data, int levelMultiplier)
    {
        if (data == null) return 0;
        int totalXP = 0;
        for (int i = 1; i < data.currentLevel; i++)
        {
            totalXP += i * levelMultiplier;
        }
        totalXP += data.currentXP;
        return totalXP;
    }

    private void FillCrossGameRecords()
    {
        int maxScore = 0; string maxScoreGame = "";
        float minTime = float.MaxValue; string minTimeGame = "";
        int minMoves = int.MaxValue; string minMovesGame = "";

        var allEntries = StatisticsManager.Instance.GetAllEntriesRaw();

        if (allEntries != null)
        {
            foreach (var entry in allEntries)
            {
                if (entry.key.EndsWith("_Global") && entry.key != "Global")
                {
                    string gName = entry.key.Replace("_Global", "");

                    if (entry.data.bestScore > maxScore) { maxScore = entry.data.bestScore; maxScoreGame = gName; }
                    if (entry.data.bestTime > 0 && entry.data.bestTime < minTime) { minTime = entry.data.bestTime; minTimeGame = gName; }
                    if (entry.data.fewestMoves > 0 && entry.data.fewestMoves < minMoves) { minMoves = entry.data.fewestMoves; minMovesGame = gName; }
                }
            }
        }

        SetRecordText(bestScoreText, maxScore == 0 ? "-" : maxScore.ToString("N0"), maxScoreGame);
        SetRecordText(bestTimeText, minTime == float.MaxValue ? "-" : FormatRecordTime(minTime), minTimeGame);
        SetRecordText(bestMovesText, minMoves == int.MaxValue ? "-" : minMoves.ToString(), minMovesGame);
    }

    private void SetRecordText(TMP_Text textElement, string value, string gameName)
    {
        if (textElement == null) return;

        if (string.IsNullOrEmpty(gameName) || value == "-")
        {
            textElement.text = "-";
            return;
        }

        string locGameName = GetLocalizedGameName(gameName);
        textElement.text = $"{value} <color=#A8D9E0>({locGameName})</color>";
    }

    private string GetLocalizedGameName(string gameName)
    {
        string locKey = gameName;
        if (locKey == "FreeCell") locKey = "Freecell";

        if (LocalizationManager.instance != null)
        {
            string locTry = LocalizationManager.instance.GetLocalizedValue(locKey);
            if (!string.IsNullOrEmpty(locTry) && locTry != locKey && locTry != "Localized text not found") return locTry;
        }
        return gameName;
    }

    private void UpdateHistorySlots(List<GameHistoryEntry> history)
    {
        if (historySlots == null) return;
        int count = history.Count;
        for (int i = 0; i < historySlots.Length; i++)
        {
            int dataIndex = count - 1 - i;

            HistorySlotHover hover = historySlots[i].GetComponent<HistorySlotHover>();
            if (hover == null) hover = historySlots[i].gameObject.AddComponent<HistorySlotHover>();

            if (dataIndex >= 0)
            {
                var entry = history[dataIndex];

                historySlots[i].sprite = entry.won ? winIcon : lossIcon;
                historySlots[i].color = Color.white;
                hover.Setup(entry, HistorySlotHover.SlotType.AppGlobal);

                if (historyIndicators != null && i < historyIndicators.Length && historyIndicators[i] != null)
                {
                    Sprite gameSprite = GetIndicatorSprite(entry.gameName);
                    if (gameSprite != null)
                    {
                        historyIndicators[i].gameObject.SetActive(true);
                        historyIndicators[i].sprite = gameSprite;
                    }
                    else
                    {
                        historyIndicators[i].gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                historySlots[i].sprite = emptyIcon;
                historySlots[i].color = (emptyIcon == null) ? Color.clear : new Color(1, 1, 1, 0.5f);
                hover.Setup(null, HistorySlotHover.SlotType.Difficulty);

                if (historyIndicators != null && i < historyIndicators.Length && historyIndicators[i] != null)
                {
                    historyIndicators[i].gameObject.SetActive(false);
                }
            }
        }
    }

    private Sprite GetIndicatorSprite(string gameName)
    {
        if (System.Enum.TryParse(gameName, out GameType type))
        {
            foreach (var mapping in gameIndicators)
            {
                if (mapping.gameType == type) return mapping.sprite;
            }
        }
        return null;
    }

    private string FormatTimeLocalized(float timeInSeconds)
    {
        if (timeInSeconds <= 0) return "-";

        int hours = Mathf.FloorToInt(timeInSeconds / 3600f);
        int minutes = Mathf.FloorToInt((timeInSeconds % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

        string lang = "en";
        if (LocalizationManager.instance != null && !string.IsNullOrEmpty(LocalizationManager.instance.CurrentLanguage))
        {
            lang = LocalizationManager.instance.CurrentLanguage.ToLower();
        }

        string hStr = "h", mStr = "m", sStr = "s";

        switch (lang)
        {
            case "ru": hStr = "ч"; mStr = "м"; sStr = "с"; break;
            case "tr": hStr = "sa"; mStr = "dk"; sStr = "sn"; break;
            case "es":
            case "pt":
            case "en":
            default: hStr = "h"; mStr = "m"; sStr = "s"; break;
        }

        if (hours > 0) return $"{hours}{hStr} {minutes}{mStr} {seconds}{sStr}";
        else if (minutes > 0) return $"{minutes}{mStr} {seconds}{sStr}";
        else return $"{seconds}{sStr}";
    }

    private string FormatRecordTime(float timeInSeconds)
    {
        if (timeInSeconds <= 0) return "-";

        int hours = Mathf.FloorToInt(timeInSeconds / 3600f);
        int minutes = Mathf.FloorToInt((timeInSeconds % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);

        if (hours > 0) return $"{hours}:{minutes:00}:{seconds:00}";
        else return $"{minutes}:{seconds:00}";
    }

    private void OnCloseClicked()
    {
        if (MenuController.Instance != null)
        {
            MenuController.Instance.OnCloseOverlayClicked();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}