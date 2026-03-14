using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MontanaGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Montana; // Выберите правильный индекс в вашем enum

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal deal = new Deal();
        deal.tableau.Clear();
        deal.stock.Clear();

        // 56 слотов сетки (4 ряда x 14 колонок)
        for (int i = 0; i < 56; i++) deal.tableau.Add(new List<CardInstance>());

        List<CardModel> deck = new List<CardModel>();
        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            for (int rank = 1; rank <= 13; rank++) deck.Add(new CardModel(suit, rank));
        }

        System.Random rng = new System.Random();
        deck = deck.OrderBy(x => rng.Next()).ToList();

        int cardIndex = 0;
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 14; col++)
            {
                int slotIndex = col * 4 + row;

                // param == 0 (Classic): пустые слоты слева (Col == 0)
                // param == 1 (Hard): пустые слоты справа (Col == 13)
                bool isEmptySlot = (param == 0 && col == 0) || (param == 1 && col == 13);

                if (!isEmptySlot && cardIndex < deck.Count)
                {
                    deal.tableau[slotIndex].Add(new CardInstance(deck[cardIndex], true));
                    cardIndex++;
                }
            }
        }

        onComplete?.Invoke(deal, null);
        yield break;
    }
}