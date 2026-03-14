using System.Collections.Generic;
using UnityEngine;

public class MontanaPileManager : PileManager
{
    private MontanaModeManager _mode;

    [Header("Montana Specific")]
    public Transform gridSlotsParent; // GridLayoutGroup на 56 €чеек (14 колонок)

    public List<MontanaSlot> Slots { get; private set; } = new List<MontanaSlot>();

    public void Initialize(MontanaModeManager m)
    {
        _mode = m;
    }

    public void CreatePiles()
    {
        Slots.Clear();

        // »нициализируем 56 €чеек сетки
        for (int i = 0; i < gridSlotsParent.childCount; i++)
        {
            if (i >= 56) break;

            int r = i % 4; // 4 - количество р€дов
            int c = i / 4;

            Transform slotTf = gridSlotsParent.GetChild(i);
            var slot = slotTf.GetComponent<MontanaSlot>() ?? slotTf.gameObject.AddComponent<MontanaSlot>();
            slot.Initialize(_mode, r, c);
            Slots.Add(slot);
        }
    }

    public MontanaSlot GetSlot(int row, int col)
    {
        // Ќова€ математика дл€ нумерации по столбцам (сверху-вниз)
        // 4 - это количество р€дов
        int index = col * 4 + row;

        if (index >= 0 && index < Slots.Count) return Slots[index];
        return null;
    }

    public override List<ICardContainer> GetAllContainers() => new List<ICardContainer>(Slots);

    public List<Transform> GetAllContainerTransforms()
    {
        var transforms = new List<Transform>();
        foreach (var s in Slots) transforms.Add(s.transform);
        return transforms;
    }

    public void ClearAllPiles()
    {
        foreach (var s in Slots) s.Clear();
    }
}