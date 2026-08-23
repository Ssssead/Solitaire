using UnityEngine;
using TMPro;
using YG;

public class DynamicLeaderboardController : MonoBehaviour
{
    [Header("Landscape UI")]
    public LeaderboardYG landscapeLeaderboard;
    public TMP_Text landscapeTitleText;

    [Header("Portrait UI")]
    public LeaderboardYG portraitLeaderboard;
    public TMP_Text portraitTitleText;

    // Вызывается из MenuController при открытии панели
    public void LoadLeaderboard(string lbName)
    {
        // --- 1. ДИНАМИЧЕСКИЙ ЗАГОЛОВОК ---
        string targetKey = $"Leaderboard_{lbName}";
        string locTitle = GetLocalizedValue(targetKey);
        string finalTitle = string.IsNullOrEmpty(locTitle) ? $"Leaderboard: {lbName}" : locTitle;

        // Обновляем текст на обеих панелях
        if (landscapeTitleText != null) landscapeTitleText.text = finalTitle;
        if (portraitTitleText != null) portraitTitleText.text = finalTitle;

        // --- 2. ПЕРЕДАЕМ ИМЯ В ПЛАГИН И ОБНОВЛЯЕМ ---
        string finalLbName = lbName + "LVL";

        if (landscapeLeaderboard != null)
        {
            landscapeLeaderboard.nameLB = finalLbName;
            // Если панель сейчас активна, дергаем обновление сразу
            if (landscapeLeaderboard.gameObject.activeInHierarchy)
            {
                landscapeLeaderboard.UpdateLB();
            }
        }

        if (portraitLeaderboard != null)
        {
            portraitLeaderboard.nameLB = finalLbName;
            // Если панель сейчас активна, дергаем обновление сразу
            if (portraitLeaderboard.gameObject.activeInHierarchy)
            {
                portraitLeaderboard.UpdateLB();
            }
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