using UnityEngine;
using System.Collections.Generic;

public class TriPeaksPileManager : MonoBehaviour
{
    [Header("Piles")]
    public TriPeaksStockPile Stock;
    public TriPeaksWastePile Waste;
    public List<TriPeaksTableauPile> TableauPiles; // Назначить в инспекторе (28 шт)

    public void ClearAll()
    {
        Stock.Clear();
        Waste.Clear();
        foreach (var p in TableauPiles) p.Clear();
    }

    public TriPeaksTableauPile FindSlotWithCard(CardController card)
    {
        foreach (var p in TableauPiles)
        {
            if (p.CurrentCard == card) return p;
        }
        return null;
    }
}