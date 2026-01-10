using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PyramidGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Pyramid;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        var deck = CreateDeck();
        Shuffle(deck);

        Deal deal = new Deal();
        deal.tableau = new List<List<CardInstance>>();
        deal.stock = new Stack<CardInstance>();

        int index = 0;
        // 28 карт в пирамиду
        for (int row = 0; row < 7; row++)
        {
            var rowList = new List<CardInstance>();
            for (int i = 0; i <= row; i++)
            {
                if (index < deck.Count)
                    rowList.Add(new CardInstance(deck[index++], true));
            }
            deal.tableau.Add(rowList);
        }

        // Остаток в сток. 
        // ВАЖНО: Ставим FaceUp = true, как вы просили, чтобы они были видны
        while (index < deck.Count)
        {
            deal.stock.Push(new CardInstance(deck[index++], true));
        }

        onComplete?.Invoke(deal, new DealMetrics());
        yield break;
    }

    private List<CardModel> CreateDeck()
    {
        var list = new List<CardModel>();
        for (int s = 0; s < 4; s++)
            for (int r = 1; r <= 13; r++) list.Add(new CardModel((Suit)s, r));
        return list;
    }

    private void Shuffle(List<CardModel> list)
    {
        var rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            var v = list[k]; list[k] = list[n]; list[n] = v;
        }
    }
}