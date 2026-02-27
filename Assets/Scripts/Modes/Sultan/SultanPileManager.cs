using System.Collections.Generic;
using UnityEngine;

public class SultanPileManager : PileManager
{
    // --- ИСПРАВЛЕНИЕ: Переименовали mode в _mode, чтобы не конфликтовать с базовым классом ---
    private SultanModeManager _mode;

    [Header("Sultan Specific Parents")]
    public Transform centerSlotTransform;
    public Transform reserveSlotsParent;

    public SultanCenterPile CenterPile { get; private set; }
    public List<SultanReserveSlot> Reserves { get; private set; } = new List<SultanReserveSlot>();
    public new List<SultanFoundationPile> Foundations { get; private set; } = new List<SultanFoundationPile>();
    public new SultanStockPile StockPile { get; private set; }
    public new SultanWastePile WastePile { get; private set; }

    public void Initialize(SultanModeManager m)
    {
        _mode = m; // Используем новое имя
    }

    public void CreatePiles()
    {
        Reserves.Clear();
        Foundations.Clear();

        // 1. Центр
        if (centerSlotTransform != null)
        {
            CenterPile = centerSlotTransform.gameObject.GetComponent<SultanCenterPile>() ?? centerSlotTransform.gameObject.AddComponent<SultanCenterPile>();
            CenterPile.Initialize(_mode, centerSlotTransform as RectTransform);
        }

        // 2. Резервы
        if (reserveSlotsParent != null)
        {
            for (int i = 0; i < reserveSlotsParent.childCount; i++)
            {
                Transform slot = reserveSlotsParent.GetChild(i);
                var reserve = slot.GetComponent<SultanReserveSlot>() ?? slot.gameObject.AddComponent<SultanReserveSlot>();
                reserve.Initialize(_mode, slot as RectTransform);
                Reserves.Add(reserve);
            }
        }

        // 3. Дома 
        if (foundationSlotsParent != null)
        {
            for (int i = 0; i < foundationSlotsParent.childCount; i++)
            {
                Transform slot = foundationSlotsParent.GetChild(i);
                var foundation = slot.GetComponent<SultanFoundationPile>() ?? slot.gameObject.AddComponent<SultanFoundationPile>();
                foundation.Initialize(_mode, slot as RectTransform);
                Foundations.Add(foundation);
            }
        }

        // 4. Stock & Waste
        if (stockSlotTransform != null)
        {
            StockPile = stockSlotTransform.GetComponent<SultanStockPile>() ?? stockSlotTransform.gameObject.AddComponent<SultanStockPile>();
            StockPile.Initialize(_mode, stockSlotTransform as RectTransform);
        }

        if (wasteSlotTransform != null)
        {
            WastePile = wasteSlotTransform.GetComponent<SultanWastePile>() ?? wasteSlotTransform.gameObject.AddComponent<SultanWastePile>();
            WastePile.Initialize(_mode, wasteSlotTransform as RectTransform);
        }
    }

    public override List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();

        if (CenterPile != null) list.Add(CenterPile);
        if (Reserves != null) list.AddRange(Reserves);
        if (Foundations != null) list.AddRange(Foundations);
        if (StockPile != null) list.Add(StockPile);
        if (WastePile != null) list.Add(WastePile);

        return list;
    }

    public List<Transform> GetAllContainerTransforms()
    {
        var transforms = new List<Transform>();
        if (CenterPile != null) transforms.Add(CenterPile.transform);
        foreach (var r in Reserves) transforms.Add(r.transform);
        foreach (var f in Foundations) transforms.Add(f.transform);
        if (StockPile != null) transforms.Add(StockPile.transform);
        if (WastePile != null) transforms.Add(WastePile.transform);
        return transforms;
    }

    public void ClearAllPiles()
    {
        CenterPile?.Clear();
        foreach (var r in Reserves) r.Clear();
        foreach (var f in Foundations) f.Clear();
        StockPile?.Clear();
        WastePile?.Clear();
    }
}