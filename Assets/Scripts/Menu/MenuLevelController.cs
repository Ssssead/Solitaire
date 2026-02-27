using UnityEngine;

public class MenuLevelController : MonoBehaviour
{
    [Header("Global Level UI")]
    public XPProgressBar globalLevelBar;

    [Header("Game Progress Bars")]
    public XPProgressBar klondikeBar;
    public XPProgressBar spiderBar;
    public XPProgressBar freeCellBar;
    public XPProgressBar pyramidBar;
    public XPProgressBar tripeaksBar;
    public XPProgressBar yukonBar;
    public XPProgressBar sultanBar;
    public XPProgressBar octagonBar;
    public XPProgressBar monteCarloBar;
    public XPProgressBar montanaBar;

    // Ссылка на бар, который сейчас показывает превью (чтобы потом его спрятать)
    private XPProgressBar lastActivePreviewBar;

    private void Start() => UpdateAllBars();
    private void OnEnable() => UpdateAllBars();

    public void UpdateAllBars()
    {
        if (StatisticsManager.Instance == null) return;
        UpdateSingleBar(globalLevelBar, StatisticsManager.Instance.GetGlobalStats());
        UpdateSingleBar(klondikeBar, StatisticsManager.Instance.GetGameGlobalStats("Klondike"));
        UpdateSingleBar(spiderBar, StatisticsManager.Instance.GetGameGlobalStats("Spider"));
        UpdateSingleBar(freeCellBar, StatisticsManager.Instance.GetGameGlobalStats("FreeCell"));
        UpdateSingleBar(pyramidBar, StatisticsManager.Instance.GetGameGlobalStats("Pyramid"));
        UpdateSingleBar(tripeaksBar, StatisticsManager.Instance.GetGameGlobalStats("TriPeaks"));
        UpdateSingleBar(yukonBar, StatisticsManager.Instance.GetGameGlobalStats("Yukon"));
        UpdateSingleBar(sultanBar, StatisticsManager.Instance.GetGameGlobalStats("Sultan"));
        UpdateSingleBar(octagonBar, StatisticsManager.Instance.GetGameGlobalStats("Octagon"));
        UpdateSingleBar(monteCarloBar, StatisticsManager.Instance.GetGameGlobalStats("MonteCarlo"));
        UpdateSingleBar(montanaBar, StatisticsManager.Instance.GetGameGlobalStats("Montana"));
    }

    // --- ГЛАВНАЯ ЛОГИКА ---
    public void ShowXPGainPreview(GameType type, int potentialGain)
    {
        XPProgressBar targetBar = GetBarByType(type);
        if (targetBar == null || StatisticsManager.Instance == null) return;

        // 1. Если до этого была выбрана ДРУГАЯ игра, прячем её превью
        if (lastActivePreviewBar != null && lastActivePreviewBar != targetBar)
        {
            lastActivePreviewBar.HidePreviewXP();
        }

        // 2. Обновляем текущий активный бар
        lastActivePreviewBar = targetBar;

        // 3. Показываем превью на новом
        StatData data = StatisticsManager.Instance.GetGameGlobalStats(type.ToString());
        int currentXP = (data != null) ? data.currentXP : 0;
        int targetXP = (data != null && data.xpForNextLevel > 0) ? data.xpForNextLevel : 500;

        targetBar.ShowPreviewXP(currentXP, potentialGain, targetXP);
    }

    // Метод для принудительного скрытия всех превью (например, при нажатии Back)
    public void HideAllPreviews()
    {
        if (lastActivePreviewBar != null)
        {
            lastActivePreviewBar.HidePreviewXP();
            lastActivePreviewBar = null;
        }
    }

    private XPProgressBar GetBarByType(GameType type)
    {
        switch (type)
        {
            case GameType.Klondike: return klondikeBar;
            case GameType.Spider: return spiderBar;
            case GameType.FreeCell: return freeCellBar;
            case GameType.Pyramid: return pyramidBar;
            case GameType.TriPeaks: return tripeaksBar;
            case GameType.Octagon: return octagonBar;
            case GameType.Sultan: return sultanBar;
            case GameType.Montana: return montanaBar;
            case GameType.Yukon: return yukonBar;
            case GameType.MonteCarlo: return monteCarloBar;
            default: return null;
        }
    }
    public void UpdatePreviewBar(GameType type)
    {
        XPProgressBar targetBar = GetBarByType(type);
        if (targetBar == null) return;

        if (StatisticsManager.Instance == null) return;

        StatData data = StatisticsManager.Instance.GetGameGlobalStats(type.ToString());
        UpdateSingleBar(targetBar, data);
    }
    private void UpdateSingleBar(XPProgressBar bar, StatData data)
    {
        if (bar == null) return;
        if (data != null)
        {
            int target = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;
            bar.UpdateBar(data.currentLevel, data.currentXP, target);
        }
        else bar.UpdateBar(1, 0, 500);
    }
}