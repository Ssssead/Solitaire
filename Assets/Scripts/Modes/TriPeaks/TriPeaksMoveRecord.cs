using System.Collections.Generic;

[System.Serializable]
public class TriPeaksMoveRecord
{
    public CardController MovedCard;
    public CardController PreviousWasteTop;
    public bool IsFromStock;

    // Откуда пришла карта (если IsFromStock == false)
    public TriPeaksTableauPile SourcePile;

    // Список слотов, карты в которых перевернулись (открылись) в результате этого хода
    public List<TriPeaksTableauPile> FlipList = new List<TriPeaksTableauPile>();

    public int PointsEarned;
    public int PreviousStreak;
}