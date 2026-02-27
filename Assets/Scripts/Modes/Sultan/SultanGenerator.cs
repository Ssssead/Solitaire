using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SultanGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Sultan;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal deal = new Deal();

        deal.tableau.Clear();
        deal.foundations.Clear();

        // 6 резервных слотов и 9 слотов для сетки F0-F8
        for (int i = 0; i < 6; i++) deal.tableau.Add(new List<CardInstance>());
        for (int i = 0; i < 9; i++) deal.foundations.Add(new List<CardModel>());

        List<CardModel> deck = new List<CardModel>();
        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                deck.Add(new CardModel(suit, rank));
                deck.Add(new CardModel(suit, rank)); // Две колоды
            }
        }

        System.Random rng = new System.Random();
        deck = deck.OrderBy(x => rng.Next()).ToList();

        // Вспомогательная функция для вытягивания нужной карты
        CardModel PullCard(Suit s, int r)
        {
            var c = deck.First(x => x.suit == s && x.rank == r);
            deck.Remove(c);
            return c;
        }

        // Задаем карты строго по вашей схеме F0 - F8
        deal.foundations[0].Add(PullCard(Suit.Diamonds, 13)); // F0: KD
        deal.foundations[1].Add(PullCard(Suit.Hearts, 1));  // F1: AH
        deal.foundations[2].Add(PullCard(Suit.Diamonds, 13)); // F2: KD
        deal.foundations[3].Add(PullCard(Suit.Clubs, 13));    // F3: KC
        deal.foundations[4].Add(PullCard(Suit.Hearts, 13));   // F4: KH (ЦЕНТР)
        deal.foundations[5].Add(PullCard(Suit.Clubs, 13));    // F5: KC
        deal.foundations[6].Add(PullCard(Suit.Spades, 13));   // F6: KS
        deal.foundations[7].Add(PullCard(Suit.Hearts, 13));   // F7: KH
        deal.foundations[8].Add(PullCard(Suit.Spades, 13));   // F8: KS

        // Раздаем 6 карт в резерв
        for (int i = 0; i < 6; i++)
        {
            CardModel c = deck.First();
            deck.RemoveAt(0);
            deal.tableau[i].Add(new CardInstance(c, true));
        }

        // Остаток (89 карт) идет в Stock
        foreach (var c in deck)
        {
            deal.stock.Push(new CardInstance(c, false));
        }

        var metrics = new DealMetrics { Solved = true };
        onComplete?.Invoke(deal, metrics);
        yield break;
    }
}