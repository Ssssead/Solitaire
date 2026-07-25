using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

public class TriPeaksGenerator : BaseGenerator
{
    public override GameType GameType => GameType.TriPeaks;

    [Header("Settings")]
    public int maxMutationsPerCandidate = 15;
    public float frameBudgetMs = 10.0f;

    private System.Random _rng = new System.Random(Guid.NewGuid().GetHashCode());

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal validDeal = null;
        int totalAttempts = 0;
        bool found = false;
        Stopwatch frameWatch = new Stopwatch();

        // Сбор статистики лучшего промаха (fallback)
        Deal bestFallback = null;
        int minDeviation = int.MaxValue;

        while (!found)
        {
            totalAttempts++;
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }
            else if (!frameWatch.IsRunning) frameWatch.Start();

            Deal candidate = CreateSmartDeal(difficulty);

            for (int m = 0; m <= maxMutationsPerCandidate; m++)
            {
                if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }

                var res = TriPeaksEvaluator.Evaluate(candidate);

                if (res.IsSolvable)
                {
                    if (IsPerfectMatch(difficulty, res))
                    {
                        validDeal = candidate;
                        UnityEngine.Debug.Log($"<color=green>[Gen] FOUND {difficulty}!</color> States: {res.StatesToFirstWin} | SmartWin: {res.SmartWinRate}% | Kings: {res.KingsOnPeaks} | Att: {totalAttempts}");
                        found = true; break;
                    }

                    // Оценка отклонения для Fallback
                    int deviation = GetDeviation(difficulty, res);
                    if (deviation < minDeviation) { minDeviation = deviation; bestFallback = candidate; }

                    MutateBasedOnMetrics(candidate, difficulty, res);
                }
                else MutateBasedOnMetrics(candidate, difficulty, res); // Нерешаемо -> Упрощаем
            }

            if (found) break;
            if (totalAttempts > 200)
            {
                UnityEngine.Debug.LogWarning($"<color=orange>[Gen] Timeout! Emitting best fallback (Deviation: {minDeviation}).</color>");
                validDeal = bestFallback;
                break;
            }
        }

        onComplete?.Invoke(validDeal, new DealMetrics { Solved = true });
    }

    // --- МАГИЯ КЛАССИФИКАЦИИ (НА ОСНОВЕ ТВОЕГО АНАЛИЗА) ---
    private bool IsPerfectMatch(Difficulty d, TriPeaksEvaluator.EvalResult res)
    {
        switch (d)
        {
            case Difficulty.Easy:
                // Убрали проверку королей. Оставили винрейт, быстрый поиск и длинные комбо
                return res.SmartWinRate >= 65 && res.StatesToFirstWin <= 1000 && res.MaxChain >= 7;

            case Difficulty.Medium:
                return res.SmartWinRate >= 35 && res.SmartWinRate <= 60 && res.StatesToFirstWin > 1000 && res.StatesToFirstWin <= 8000;

            case Difficulty.Hard:
                // Убрали проверку королей. Немного снизили порог состояний до 8000 (12000 было слишком жестко для 200 попыток)
                return res.SmartWinRate <= 25 && res.StatesToFirstWin >= 8000 && res.MaxChain <= 6;

            default: return true;
        }
    }

    private int GetDeviation(Difficulty d, TriPeaksEvaluator.EvalResult res)
    {
        if (d == Difficulty.Easy) return Math.Abs(res.SmartWinRate - 75);
        if (d == Difficulty.Medium) return Math.Abs(res.SmartWinRate - 45);
        return Math.Abs(res.SmartWinRate - 15);
    }

    private void MutateBasedOnMetrics(Deal d, Difficulty diff, TriPeaksEvaluator.EvalResult res)
    {
        List<CardModel> flat = new List<CardModel>();
        foreach (var col in d.tableau) flat.Add(col[0].Card);
        var stockArr = d.stock.ToArray();
        Array.Reverse(stockArr); foreach (var card in stockArr) flat.Add(card.Card);

        bool needsHarder = false;
        if (diff == Difficulty.Hard && res.SmartWinRate > 25) needsHarder = true;
        if (diff == Difficulty.Easy && res.SmartWinRate < 60) needsHarder = false;

        if (!res.IsSolvable) needsHarder = false;

        if (needsHarder)
        {
            // НАСТОЯЩЕЕ УСЛОЖНЕНИЕ: 
            // Берем открытую карту из основания (18-27) и прячем ее либо на пики (0-8), либо в самый конец колоды (40-51)
            int easyIdx = _rng.Next(18, 28);
            int hardIdx = _rng.NextDouble() > 0.5f ? _rng.Next(0, 9) : _rng.Next(40, 52);
            Swap(flat, easyIdx, hardIdx);
        }
        else
        {
            // УПРОЩЕНИЕ:
            // Достаем застрявшую карту с пиков или из конца колоды и кладем в основание
            int stuckIdx = _rng.NextDouble() > 0.5f ? _rng.Next(0, 9) : _rng.Next(40, 52);
            int easyIdx = _rng.Next(18, 28);
            Swap(flat, stuckIdx, easyIdx);
        }

        // Пересобираем Deal
        d.tableau.Clear(); d.stock.Clear();
        int p = 0;
        for (int i = 0; i < 28; i++) d.tableau.Add(new List<CardInstance> { new CardInstance(flat[p++], i >= 18) });
        for (int i = p; i < 52; i++) d.stock.Push(new CardInstance(flat[i], false));
    }

    private Deal CreateSmartDeal(Difficulty difficulty)
    {
        List<CardModel> deck = new List<CardModel>();
        for (int s = 0; s < 4; s++) for (int r = 1; r <= 13; r++) deck.Add(new CardModel((Suit)s, r));
        int n = deck.Count; while (n > 1) { n--; int k = _rng.Next(n + 1); Swap(deck, k, n); }

        Deal deal = new Deal(); deal.tableau = new List<List<CardInstance>>(); deal.stock = new Stack<CardInstance>();
        int idx = 0;
        for (int i = 0; i < 28; i++) deal.tableau.Add(new List<CardInstance> { new CardInstance(deck[idx++], i >= 18) });
        for (int i = idx; i < 52; i++) deal.stock.Push(new CardInstance(deck[i], false));
        return deal;
    }

    private void Swap(List<CardModel> list, int a, int b) { var temp = list[a]; list[a] = list[b]; list[b] = temp; }
}