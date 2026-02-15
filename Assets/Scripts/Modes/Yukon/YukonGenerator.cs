using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class YukonGenerator : BaseGenerator
{
    // Убедитесь, что GameType.Yukon существует в Enums.cs, иначе используйте приведение (GameType)X
    public override GameType GameType => GameType.Yukon;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        // 1. Создаем колоду (52 карты)
        List<CardModel> fullDeck = new List<CardModel>();
        for (int s = 0; s < 4; s++)
        {
            for (int r = 1; r <= 13; r++)
            {
                fullDeck.Add(new CardModel((Suit)s, r));
            }
        }

        // 2. Тасуем (алгоритм Фишера-Йейтса)
        int seed = System.DateTime.Now.Millisecond;
        UnityEngine.Random.InitState(seed);
        int n = fullDeck.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            CardModel value = fullDeck[k];
            fullDeck[k] = fullDeck[n];
            fullDeck[n] = value;
        }

        // 3. Формируем объект Deal
        Deal deal = new Deal();
        deal.tableau = new List<List<CardInstance>>();
        deal.stock = new Stack<CardInstance>(); // В Юконе сток пуст, все карты на столе

        int cardIndex = 0;

        // Расклад Юкона (7 колонок)
        // Колонка 0: 0 закрытых, 1 открытая (всего 1)
        // Колонка 1: 1 закрытая, 5 открытых (всего 6)
        // ...
        // Колонка 6: 6 закрытых, 5 открытых (всего 11)

        for (int i = 0; i < 7; i++)
        {
            List<CardInstance> column = new List<CardInstance>();

            // 1. Закрытые карты (Face Down)
            // Количество равно индексу колонки (0..6)
            int faceDownCount = i;
            for (int j = 0; j < faceDownCount; j++)
            {
                if (cardIndex >= fullDeck.Count) break;
                // CardInstance(CardModel model, bool isFaceUp)
                column.Add(new CardInstance(fullDeck[cardIndex++], false));
            }

            // 2. Открытые карты (Face Up)
            // Всегда 5 карт, кроме первой колонки (там всего 1 карта, значит faceUp = 1)
            int faceUpCount = (i == 0) ? 1 : 5;

            for (int j = 0; j < faceUpCount; j++)
            {
                if (cardIndex >= fullDeck.Count) break;
                column.Add(new CardInstance(fullDeck[cardIndex++], true));
            }

            deal.tableau.Add(column);
        }

        // Формируем метрики (заглушка)
        DealMetrics metrics = new DealMetrics();
        // ИСПРАВЛЕНО: Поле metrics.difficulty отсутствует в классе DealMetrics, поэтому мы его не заполняем.
        metrics.Solved = false;
        metrics.MoveEstimate = 0;

        // Возвращаем результат
        onComplete?.Invoke(deal, metrics);
        yield break;
    }
}