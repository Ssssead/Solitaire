using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class TriPeaksGenerator : BaseGenerator
{
    // Убедитесь, что в enum GameType (в файле BaseGenerator или Enums) есть TriPeaks.
    // Если нет - добавьте его туда вручную: TriPeaks = 3 (или следующий свободный индекс).
    public override GameType GameType => GameType.TriPeaks;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        // 1. Создаем полную колоду (52 карты)
        List<CardModel> fullDeck = new List<CardModel>();
        for (int s = 0; s < 4; s++)
        {
            for (int r = 1; r <= 13; r++)
            {
                fullDeck.Add(new CardModel((Suit)s, r));
            }
        }

        // 2. Тасуем (Fisher-Yates)
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
        deal.stock = new Stack<CardInstance>();

        // В TriPeaks 28 карт на столе. В вашей структуре Deal.tableau - это список списков (колонок).
        // Мы создадим 28 "колонок" по 1 карте, чтобы удобно мапить их на слоты TriPeaks.

        int cardIndex = 0;

        // --- Заполняем Табло (28 карт) ---
        for (int i = 0; i < 28; i++)
        {
            List<CardInstance> slotPile = new List<CardInstance>();
            CardModel model = fullDeck[cardIndex++];

            // ИСПРАВЛЕНИЕ: Конструктор CardInstance требует (CardModel, bool faceUp)
            // Изначально ставим false (закрыта), FaceUp будет управляться Менеджером на основе блокировок.
            CardInstance card = new CardInstance(model, false);

            slotPile.Add(card);
            deal.tableau.Add(slotPile);
        }

        // --- Заполняем Сток (24 карты) ---
        // Остаток карт кладем в Stack.
        for (int i = cardIndex; i < fullDeck.Count; i++)
        {
            // ИСПРАВЛЕНИЕ: Конструктор CardInstance
            CardInstance card = new CardInstance(fullDeck[i], false);
            deal.stock.Push(card);
        }

        // 4. Возвращаем результат
        onComplete?.Invoke(deal, new DealMetrics());
        yield break;
    }
}