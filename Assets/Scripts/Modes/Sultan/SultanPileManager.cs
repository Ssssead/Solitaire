using System.Collections.Generic;
using UnityEngine;

public class SultanPileManager : PileManager
{
    private SultanModeManager _mode;

    public SultanCenterPile CenterPile { get; private set; }
    public List<SultanReserveSlot> Reserves { get; private set; } = new List<SultanReserveSlot>();
    public new List<SultanFoundationPile> Foundations { get; private set; } = new List<SultanFoundationPile>();
    public new SultanStockPile StockPile { get; private set; }
    public new SultanWastePile WastePile { get; private set; }

    public void Initialize(SultanModeManager m)
    {
        _mode = m;
    }

    public void CreatePiles()
    {
        Reserves.Clear();
        Foundations.Clear();

        // 1. Ищем Центр
        CenterPile = FindObjectOfType<SultanCenterPile>();
        if (CenterPile != null) CenterPile.Initialize(_mode, CenterPile.transform as RectTransform);

        // 2. Ищем Резервы (глобальный поиск спасает от потери слотов при повороте)
        var allReserves = FindObjectsOfType<SultanReserveSlot>();
        var sortedReserves = new List<SultanReserveSlot>(allReserves);
        sortedReserves.Sort((a, b) => a.name.CompareTo(b.name));
        foreach (var r in sortedReserves)
        {
            r.Initialize(_mode, r.transform as RectTransform);
            Reserves.Add(r);
        }

        // 3. Ищем Дома
        var allFoundations = FindObjectsOfType<SultanFoundationPile>();
        var sortedFounds = new List<SultanFoundationPile>(allFoundations);
        sortedFounds.Sort((a, b) => a.name.CompareTo(b.name));
        foreach (var f in sortedFounds)
        {
            f.Initialize(_mode, f.transform as RectTransform);
            Foundations.Add(f);
        }

        // 4. Stock & Waste
        StockPile = FindObjectOfType<SultanStockPile>();
        if (StockPile != null) StockPile.Initialize(_mode, StockPile.transform as RectTransform);

        WastePile = FindObjectOfType<SultanWastePile>();
        if (WastePile != null) WastePile.Initialize(_mode, WastePile.transform as RectTransform);
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

    public void ClearAllPiles()
    {
        CenterPile?.Clear();
        foreach (var r in Reserves) r.Clear();
        foreach (var f in Foundations) f.Clear();
        StockPile?.Clear();
        WastePile?.Clear();
    }
}