using UnityEngine;

public class MenuLevelController : MonoBehaviour
{
    [Header("Global Level UI (Main Menu)")]
    public XPProgressBar globalLevelBar;

    [Header("Local Level UI (Settings Panel)")]
    public XPProgressBar localLevelBar;

    private void Start()
    {
        UpdateGlobalBar();
    }

    // Этот метод вызывается, когда включается объект (если скрипт висит на активном объекте)
    private void OnEnable()
    {
        UpdateGlobalBar();
    }

    public void UpdateGlobalBar()
    {
        if (StatisticsManager.Instance == null) return;

        if (globalLevelBar == null)
        {
            Debug.LogWarning("[MenuLevelController] Global Level Bar is not assigned!");
            return;
        }

        StatData data = StatisticsManager.Instance.GetGlobalStats();
        if (data != null)
        {
            int target = data.xpForNextLevel > 0 ? data.xpForNextLevel : 2000;
            globalLevelBar.UpdateBar(data.currentLevel, data.currentXP, target);
        }
        else
        {
            globalLevelBar.UpdateBar(1, 0, 2000);
        }
    }

    public void UpdateLocalBar(string gameName)
    {
        Debug.Log($"[MenuLevelController] Update Request for: {gameName}");

        if (localLevelBar == null)
        {
            Debug.LogError("[MenuLevelController] ERROR: 'Local Level Bar' is not assigned in Inspector!");
            return;
        }

        if (StatisticsManager.Instance == null)
        {
            Debug.LogError("[MenuLevelController] StatisticsManager missing!");
            return;
        }

        // Прямой запрос данных
        StatData data = StatisticsManager.Instance.GetGameGlobalStats(gameName);

        if (data != null)
        {
            Debug.Log($"[MenuLevelController] SUCCESS. Found data for {gameName}. Level: {data.currentLevel}, XP: {data.currentXP}/{data.xpForNextLevel}");

            // Обновляем визуальную часть
            int target = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;
            localLevelBar.UpdateBar(data.currentLevel, data.currentXP, target);
        }
        else
        {
            Debug.LogWarning($"[MenuLevelController] WARNING. No data found for key '{gameName}_Global'. Showing default 1.");
            localLevelBar.UpdateBar(1, 0, 500);
        }
    }
}