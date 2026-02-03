using UnityEngine;

public class MenuLevelController : MonoBehaviour
{
    [Header("Global Level UI")]
    public XPProgressBar globalLevelBar;

    [Header("Game Progress Bars (Links)")]
    // Ссылки на 10 объектов в главном меню (маленькие карточки)
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

    [Header("Preview Bar (Optional)")]
    // Ссылка на "Большую" карточку в панели настроек (справа на скриншоте 2)
    public XPProgressBar bigPreviewBar;

    private void Start()
    {
        UpdateAllBars();
    }

    private void OnEnable()
    {
        UpdateAllBars();
    }

    /// <summary>
    /// Проходится по всем 10 играм и обновляет их полоски
    /// </summary>
    public void UpdateAllBars()
    {
        if (StatisticsManager.Instance == null) return;

        // 1. Глобальный уровень
        UpdateSingleBar(globalLevelBar, StatisticsManager.Instance.GetGlobalStats());

        // 2. Игры (строки должны совпадать с теми, что в StatisticsManager/SaveSystem)
        UpdateSingleBar(klondikeBar, StatisticsManager.Instance.GetGameGlobalStats("Klondike"));
        UpdateSingleBar(spiderBar, StatisticsManager.Instance.GetGameGlobalStats("Spider"));
        UpdateSingleBar(freeCellBar, StatisticsManager.Instance.GetGameGlobalStats("FreeCell"));
        UpdateSingleBar(pyramidBar, StatisticsManager.Instance.GetGameGlobalStats("Pyramid"));
        UpdateSingleBar(tripeaksBar, StatisticsManager.Instance.GetGameGlobalStats("Tripeaks"));
        UpdateSingleBar(yukonBar, StatisticsManager.Instance.GetGameGlobalStats("Yukon"));
        UpdateSingleBar(sultanBar, StatisticsManager.Instance.GetGameGlobalStats("Sultan"));
        UpdateSingleBar(octagonBar, StatisticsManager.Instance.GetGameGlobalStats("Octagon"));
        UpdateSingleBar(monteCarloBar, StatisticsManager.Instance.GetGameGlobalStats("MonteCarlo"));
        UpdateSingleBar(montanaBar, StatisticsManager.Instance.GetGameGlobalStats("Montana"));
    }

    /// <summary>
    /// Вызывается из MenuController, когда мы кликаем на игру, 
    /// чтобы обновить "Большую карточку" справа в настройках
    /// </summary>
    public void UpdatePreviewBar(GameType type)
    {
        if (bigPreviewBar == null) return;
        if (StatisticsManager.Instance == null) return;

        // Получаем статистику выбранной игры
        StatData data = StatisticsManager.Instance.GetGameGlobalStats(type.ToString());

        // 1. Обновляем данные (Level/XP)
        UpdateSingleBar(bigPreviewBar, data);

        // 2. ВАЖНО: Тут можно добавить логику смены Спрайта/Цвета для большой карточки, 
        // чтобы она визуально соответствовала выбранной игре.
        // bigPreviewBar.SetCustomSprite(...); // Если потребуется
    }

    private void UpdateSingleBar(XPProgressBar bar, StatData data)
    {
        if (bar == null) return;

        if (data != null)
        {
            int target = data.xpForNextLevel > 0 ? data.xpForNextLevel : 500;
            // Обновляем визуал (XPProgressBar сам выберет цвет/спрайт если вы настроили список спрайтов)
            bar.UpdateBar(data.currentLevel, data.currentXP, target);
        }
        else
        {
            // Если игрок еще не играл, показываем 1 уровень
            bar.UpdateBar(1, 0, 500);
        }
    }
}