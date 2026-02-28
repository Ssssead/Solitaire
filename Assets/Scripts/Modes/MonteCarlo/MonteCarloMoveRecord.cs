using System.Collections.Generic;

public class MonteCarloMoveRecord
{
    public CardController Card1;
    public CardController Card2;
    public CardController[] PreviousBoardState;
    public List<CardController> CardsDealtFromStock = new List<CardController>();
}