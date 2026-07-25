using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BasicAppGlobalStatsUI : MonoBehaviour
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

    [Header("XP Stats")]
    public TMP_Text totalXpGlobalText;
    public TMP_Text totalXpGamesText;

    [Header("Premium Paywall")]
    public Button purchasePremiumButton;

    private void Start()
    {
        if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
        if (purchasePremiumButton) purchasePremiumButton.onClick.AddListener(OnPurchasePremiumClicked);
    }

    private void OnEnable()
    {
        RefreshGlobalUI();
    }

    public void RefreshGlobalUI()
    {
        if (StatisticsManager.Instance == null) return;

        StatData globalData = StatisticsManager.Instance.GetGlobalStats();
        if (globalData == null) globalData = new StatData();

        FillBasicAndDetailedStats(globalData);
        FillXPStats(globalData);
    }

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

    private void FillXPStats(StatData globalData)
    {
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

    private void OnPurchasePremiumClicked()
    {
        // 1. Сначала закрываем текущую панель статистики
        if (MenuController.Instance != null)
        {
            MenuController.Instance.OnCloseOverlayClicked();
        }
        else
        {
            gameObject.SetActive(false);
        }

        // 2. Открываем магазин
        if (MenuController.Instance != null)
        {
            // Убрали мгновенное скрытие gameObject.SetActive(false)! 
            // MenuController сам плавно закроет панель
            MenuController.Instance.OnShopClicked();
        }
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