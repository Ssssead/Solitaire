using System.Collections.Generic;

[System.Serializable]
public class TriPeaksMoveRecord
{
    public CardController MovedCard;
    public CardController PreviousWasteTop;
    public bool IsFromStock;

    // ÈÑÏĞÀÂËÅÍÎ: TriPeaksTableauPile âìåñòî TriPeaksTableauSlot
    public List<TriPeaksTableauPile> FlipList = new List<TriPeaksTableauPile>();

    public int PointsEarned;
    public int PreviousStreak;
}