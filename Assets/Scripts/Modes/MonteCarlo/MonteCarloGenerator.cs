using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonteCarloGenerator : BaseGenerator
{
    public override GameType GameType => GameType.MonteCarlo;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, System.Action<Deal, DealMetrics> onComplete)
    {
        // 1. Создаем и тасуем колоду
        List<CardModel> deck = CreateShuffledDeck();
        yield return null;

        Deal deal = new Deal();
        deal.tableau.Clear(); // Очищаем стандартные 7 стопок
        deal.stock.Clear();

        // 2. Раздаем первые 25 карт на сетку 5x5
        // Мы используем tableau как список из 25 слотов, в каждом максимум 1 карта
        for (int i = 0; i < 25; i++)
        {
            var slotList = new List<CardInstance>();
            slotList.Add(new CardInstance(deck[i], true)); // Карты открыты
            deal.tableau.Add(slotList);
        }

        // 3. Остальные карты идут в колоду (Stock)
        for (int i = 51; i >= 25; i--)
        {
            deal.stock.Push(new CardInstance(deck[i], true)); // В Монте-Карло сток обычно показывается лицом вверх при раздаче
        }

        DealMetrics metrics = new DealMetrics { Solved = true, MoveEstimate = 0 };
        onComplete?.Invoke(deal, metrics);
    }

    private List<CardModel> CreateShuffledDeck()
    {
        List<CardModel> d = new List<CardModel>(52);
        for (int s = 0; s < 4; s++)
        {
            for (int r = 1; r <= 13; r++)
            {
                d.Add(new CardModel((Suit)s, r));
            }
        }

        // Тасовка Фишера-Йетса
        for (int i = d.Count - 1; i > 0; i--)
        {
            int k = Random.Range(0, i + 1);
            var temp = d[i];
            d[i] = d[k];
            d[k] = temp;
        }
        return d;
    }
}