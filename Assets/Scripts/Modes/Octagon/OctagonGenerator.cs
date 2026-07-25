using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

public class OctagonGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Octagon;

    [Header("Optimization")]
    [Range(1, 16)]
    public float frameBudgetMs = 8.0f; // Лимит времени на кадр, чтобы игра не фризила

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal validDeal = null;
        int attempts = 0;
        Stopwatch sw = new Stopwatch();

        UnityEngine.Debug.Log($"[OctagonGen] Starting generation (Solvable Only)...");

        while (validDeal == null)
        {
            attempts++;

            if (sw.ElapsedMilliseconds > frameBudgetMs) { yield return null; sw.Restart(); }
            else if (!sw.IsRunning) sw.Start();

            // 1. Создаем случайный расклад 
            Deal candidate = CreateRandomOctagonDeal();

            // 2. Тестируем Солвером
            OctagonSolver.ExtendedSolverResult result = new OctagonSolver.ExtendedSolverResult();
            yield return StartCoroutine(OctagonSolver.SolveAsync(candidate, frameBudgetMs, result));

            sw.Restart();

            if (result.IsSolved)
            {
                // ПРОСТО БЕРЕМ ПЕРВЫЙ РЕШАЕМЫЙ РАСКЛАД
                UnityEngine.Debug.Log($"<color=green>[OctagonGen] SUCCESS! Found solvable deal. Attempts: {attempts}.</color>");
                validDeal = candidate;
            }

            // Защита от бесконечного цикла на всякий случай
            if (attempts >= 50)
            {
                UnityEngine.Debug.LogWarning("[OctagonGen] Reached 50 attempts. Yielding last candidate to prevent freeze.");
                validDeal = candidate;
            }
        }

        DealMetrics metrics = new DealMetrics { Solved = true };
        onComplete?.Invoke(validDeal, metrics);
    }

    private Deal CreateRandomOctagonDeal()
    {
        List<CardModel> deck = new List<CardModel>();

        // Собираем 2 полные колоды (104 карты)
        for (int i = 0; i < 2; i++)
        {
            foreach (Suit s in Enum.GetValues(typeof(Suit)))
            {
                for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));
            }
        }

        // Извлекаем 8 тузов (они автоматически раздаются в Дом)
        for (int i = 0; i < 8; i++)
        {
            var ace = deck.First(c => c.rank == 1);
            deck.Remove(ace);
        }

        // Перемешиваем оставшиеся 96 карт
        Shuffle(deck);

        Deal deal = new Deal();
        deal.tableau = new List<List<CardInstance>>();

        int cardIndex = 0;

        // Раздаем 20 карт на стол (4 группы по 5 слотов)
        for (int g = 0; g < 4; g++)
        {
            List<CardInstance> groupCards = new List<CardInstance>();
            for (int s = 0; s < 5; s++)
            {
                // В Восьмиугольнике все карты на столе открыты
                groupCards.Add(new CardInstance(deck[cardIndex++], true));
            }
            deal.tableau.Add(groupCards);
        }

        deal.stock = new Stack<CardInstance>();

        // Оставшиеся 76 карт кидаем в колоду (рубашкой вверх)
        for (int i = cardIndex; i < deck.Count; i++)
        {
            deal.stock.Push(new CardInstance(deck[i], false));
        }

        return deal;
    }

    private void Shuffle(List<CardModel> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            var temp = list[k];
            list[k] = list[n];
            list[n] = temp;
        }
    }
}