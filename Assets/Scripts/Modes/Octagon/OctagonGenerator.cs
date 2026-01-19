using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OctagonGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Octagon;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        // 1. Создаем 2 колоды (используем CardModel с int rank)
        List<CardModel> deck = CreateDoubleDeck();

        // 2. Удаляем 8 Тузов (так как в Octagon они выкладываются на базы принудительно)
        RemoveCards(deck, 1); // 1 = Ace

        // 3. Тасуем
        Shuffle(deck);

        // 4. Формируем структуру Deal
        Deal deal = new Deal();

        // Инициализируем Tableau (4 стопки)
        for (int i = 0; i < 4; i++)
        {
            deal.tableau.Add(new List<CardInstance>());
        }

        // Раздаем по 5 карт в 4 стопки (Tableau)
        for (int i = 0; i < 4; i++)
        {
            for (int k = 0; k < 5; k++)
            {
                if (deck.Count > 0)
                {
                    CardModel model = deck[0];
                    deck.RemoveAt(0);

                    // Верхняя (последняя) карта открыта
                    bool faceUp = (k == 4);
                    deal.tableau[i].Add(new CardInstance(model, faceUp));
                }
            }
        }

        // Остаток кладем в Stock (Колоду)
        foreach (var model in deck)
        {
            // В колоде карты всегда рубашкой вверх (false)
            deal.stock.Push(new CardInstance(model, false));
        }

        // 5. Заполняем метрики
        DealMetrics metrics = new DealMetrics();
        // metrics.difficulty = difficulty; // УДАЛЕНО: Поля difficulty нет в DealMetrics
        metrics.Solved = true; // Пока считаем, что расклад решаем (для заглушки)

        onComplete?.Invoke(deal, metrics);
        yield break;
    }

    private List<CardModel> CreateDoubleDeck()
    {
        List<CardModel> list = new List<CardModel>();
        for (int d = 0; d < 2; d++)
        {
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (int r = 1; r <= 13; r++)
                {
                    list.Add(new CardModel(suit, r));
                }
            }
        }
        return list;
    }

    private void RemoveCards(List<CardModel> deck, int rankToRemove)
    {
        deck.RemoveAll(x => x.rank == rankToRemove);
    }

    private void Shuffle(List<CardModel> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}