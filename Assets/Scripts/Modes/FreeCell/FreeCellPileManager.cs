using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class FreeCellPileManager : PileManager
{
    [Header("FreeCell Specifics")]
    public Transform freeCellSlotsParent;
    [SerializeField] private List<FreeCellPile> freeCells = new List<FreeCellPile>();
    public IReadOnlyList<FreeCellPile> FreeCells => freeCells;

    // --- ВАЖНО: ИСПРАВЛЕННЫЙ МЕТОД ---
    // Теперь это работает, так как в PileManager мы добавили 'virtual'
    public override List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();

        // Собираем всё вручную, чтобы точно ничего не потерять
        if (Tableau != null) list.AddRange(Tableau);
        if (Foundations != null) list.AddRange(Foundations);
        if (freeCells != null) list.AddRange(freeCells);

        return list;
    }
    // ---------------------------------

    public void InitializeFreeCell(FreeCellModeManager modeManager)
    {
        // 1. Собираем FreeCells
        freeCells.Clear();
        if (freeCellSlotsParent != null)
        {
            freeCells.AddRange(freeCellSlotsParent.GetComponentsInChildren<FreeCellPile>());
        }

        // 2. Принудительно ищем и прописываем Tableau и Foundations
        // (Это решает проблему, если в Инспекторе списки пустые)

        var allTabs = FindObjectsOfType<FreeCellTableauPile>();
        // Сортируем по имени (Slot_0, Slot_1...), чтобы порядок был верным
        var sortedTabs = new List<TableauPile>(allTabs);
        sortedTabs.Sort((a, b) => (a as MonoBehaviour).name.CompareTo((b as MonoBehaviour).name));
        SetPrivateList("tableau", sortedTabs);

        var allFounds = FindObjectsOfType<FoundationPile>();
        SetPrivateList("foundations", new List<FoundationPile>(allFounds));

        // Инициализируем фундаменты, чтобы они знали про manager
        foreach (var f in allFounds) f.Initialize(null, null);
    }

    private void SetPrivateList<T>(string fieldName, List<T> list)
    {
        var field = typeof(PileManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) field.SetValue(this, list);
    }
}