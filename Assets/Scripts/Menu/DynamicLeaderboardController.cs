using UnityEngine;
using TMPro;
using YG; // Подключаем пространство имен плагина

[RequireComponent(typeof(LeaderboardYG))] // Требует, чтобы скрипт плагина висел на этом же объекте
public class DynamicLeaderboardController : MonoBehaviour
{
    [Header("UI Headers")]
    public TMP_Text titleText; // Сюда перетащите текст заголовка

    private LeaderboardYG ygLeaderboard;

    private void Awake()
    {
        // Получаем ссылку на стандартный скрипт плагина
        ygLeaderboard = GetComponent<LeaderboardYG>();
    }

    // Вызывается из MenuController при открытии панели
    public void LoadLeaderboard(string lbName)
    {
        // --- 1. ДИНАМИЧЕСКИЙ ЗАГОЛОВОК ---
        if (titleText != null)
        {
            // Берем чистые названия для перевода (Leaderboard_Global, Leaderboard_Klondike)
            string targetKey = $"Leaderboard_{lbName}";
            string locTitle = GetLocalizedValue(targetKey);

            titleText.text = string.IsNullOrEmpty(locTitle) ? $"Leaderboard: {lbName}" : locTitle;
        }

        // --- 2. ПЕРЕДАЕМ ИМЯ В ПЛАГИН И ОБНОВЛЯЕМ ---
        if (ygLeaderboard != null)
        {
            // ---> ИСПРАВЛЕНИЕ: Автоматически приклеиваем "LVL" к любому запросу <---
            ygLeaderboard.nameLB = lbName + "LVL";
            ygLeaderboard.UpdateLB();
        }
    }


    private string GetLocalizedValue(string key)
    {
        if (LocalizationManager.instance != null)
        {
            string loc = LocalizationManager.instance.GetLocalizedValue(key);
            if (!string.IsNullOrEmpty(loc) && loc != "Localized text not found") return loc;
        }
        return "";
    }
}