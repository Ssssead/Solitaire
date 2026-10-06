using System.Collections.Generic;
using UnityEngine;

public class MontanaPileManager : PileManager
{
    private MontanaModeManager _mode;

    public List<MontanaSlot> Slots { get; private set; } = new List<MontanaSlot>();

    public void Initialize(MontanaModeManager m)
    {
        _mode = m;
    }

    public void CreatePiles()
    {
        Slots.Clear();

        // --- ИСПРАВЛЕНИЕ: Берем слоты напрямую из GameLayoutManager ---
        // Это гарантирует, что мы вешаем логику контейнера именно на тот объект, 
        // который прыгает между горизонтальным и вертикальным интерфейсом.
        if (GameLayoutManager.Instance != null && GameLayoutManager.Instance.slots.Count >= 56)
        {
            for (int i = 0; i < 56; i++)
            {
                int r = i % 4;
                int c = i / 4;

                Transform slotTf = GameLayoutManager.Instance.slots[i].targetSlot;
                if (slotTf != null)
                {
                    var slot = slotTf.GetComponent<MontanaSlot>();
                    if (slot == null) slot = slotTf.gameObject.AddComponent<MontanaSlot>();

                    slot.Initialize(_mode, r, c);
                    Slots.Add(slot);
                }
            }
        }
        else
        {
            Debug.LogError("[MontanaPileManager] ОШИБКА: GameLayoutManager не найден или в нем меньше 56 слотов!");
        }
    }

    public MontanaSlot GetSlot(int row, int col)
    {
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