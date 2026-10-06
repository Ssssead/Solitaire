using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class FreeCellPileManager : PileManager
{
    [Header("FreeCell Specifics")]
    public Transform freeCellSlotsParent; // Теперь используется только для обратной совместимости
    [SerializeField] private List<FreeCellPile> freeCells = new List<FreeCellPile>();
    public IReadOnlyList<FreeCellPile> FreeCells => freeCells;

    // --- ВАЖНО: ИСПРАВЛЕННЫЙ МЕТОД ---
    public override List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();

        // Собираем всё вручную, чтобы точно ничего не потерять
        if (Tableau != null) list.AddRange(Tableau);
        if (Foundations != null) list.AddRange(Foundations);
        if (freeCells != null) list.AddRange(freeCells);

        return list;
    }

    public void InitializeFreeCell(FreeCellModeManager modeManager)
    {
        // 1. ИСПРАВЛЕНИЕ: Ищем Свободные ячейки ГЛОБАЛЬНО, чтобы они не терялись при повороте экрана
        freeCells.Clear();
        var allFreeCells = FindObjectsOfType<FreeCellPile>();
        var sortedFC = new List<FreeCellPile>(allFreeCells);
        // Сортируем по имени (Slot0, Slot1...), чтобы они сохранили правильный порядок слева направо
        sortedFC.Sort((a, b) => a.gameObject.name.CompareTo(b.gameObject.name));
        freeCells.AddRange(sortedFC);

        // 2. Принудительно ищем и прописываем Tableau и Foundations
        var allTabs = FindObjectsOfType<FreeCellTableauPile>();
        var sortedTabs = new List<TableauPile>(allTabs);
        sortedTabs.Sort((a, b) => a.gameObject.name.CompareTo(b.gameObject.name));
        SetPrivateList("tableau", sortedTabs);

        var allFounds = FindObjectsOfType<FoundationPile>();
        var sortedFounds = new List<FoundationPile>(allFounds);
        sortedFounds.Sort((a, b) => a.gameObject.name.CompareTo(b.gameObject.name));
        SetPrivateList("foundations", sortedFounds);

        // Инициализируем фундаменты
        foreach (var f in sortedFounds) f.Initialize(null, null);
    }

    private void SetPrivateList<T>(string fieldName, List<T> list)
    {
        var field = typeof(PileManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) field.SetValue(this, list);
    }
}