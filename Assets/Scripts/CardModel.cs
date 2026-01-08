// CardModel.cs
using System;

[Serializable]
public struct CardModel
{
    public Suit suit;
    public int rank; // 1..13, где 1 = Ace, 11 = Jack, 12 = Queen, 13 = King

    public CardModel(Suit suit, int rank)
    {
        this.suit = suit;
        this.rank = rank;
    }

    public override string ToString() => $"{suit}-{rank}";
}

public enum Suit { Clubs = 0, Diamonds = 1, Hearts = 2, Spades = 3 }
