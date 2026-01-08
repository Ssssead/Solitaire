using System.Collections.Generic;
using UnityEngine;

public class SpiderPileManager : MonoBehaviour
{
    [Header("Piles")]
    // Исправлено: имена с большой буквы (PascalCase)
    public SpiderStockPile StockPile;
    public List<SpiderTableauPile> TableauPiles; // Было tableauPiles, стало TableauPiles
    public List<SpiderFoundationPile> FoundationPiles; // Было foundationPiles, стало FoundationPiles

    // Свойство для совместимости, если нужно
    public List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();
        if (StockPile) list.Add(StockPile);
        foreach (var t in TableauPiles) list.Add(t);
        foreach (var f in FoundationPiles) list.Add(f);
        return list;
    }

    public List<Transform> GetAllContainerTransforms()
    {
        List<Transform> list = new List<Transform>();
        if (StockPile) list.Add(StockPile.transform);
        foreach (var t in TableauPiles) list.Add(t.transform);
        foreach (var f in FoundationPiles) list.Add(f.transform);
        return list;
    }

    private void OnValidate()
    {
        if (TableauPiles == null || TableauPiles.Count != 10)
            Debug.LogWarning("Spider expects exactly 10 Tableau Piles.");
    }
}