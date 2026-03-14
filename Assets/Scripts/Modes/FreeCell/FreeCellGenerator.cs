using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

public class FreeCellGenerator : BaseGenerator
{
    public override GameType GameType => GameType.FreeCell;

    [Header("Generation Strategy")]
    public int maxMutationsPerCandidate = 10;

    // ”¬≈Ћ»„≈Ќќ ƒќ 50! “еперь генератор не сдастс€, пока не найдет »ƒ≈јЋ№Ќџ… уровень
    public int maxTotalAttempts = 50;

    [Header("Optimization")]
    [Range(1, 30)]
    public float frameBudgetMs = 15.0f;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal validDeal = null;
        Deal bestCandidate = null;
        int bestDiff = int.MaxValue;

        int totalAttempts = 0;
        bool found = false;

        Stopwatch frameWatch = new Stopwatch();

        while (!found && totalAttempts < maxTotalAttempts)
        {
            totalAttempts++;

            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }
            else if (!frameWatch.IsRunning) frameWatch.Start();

            Deal candidate = CreateRandomDeal();

            for (int m = 0; m <= maxMutationsPerCandidate; m++)
            {
                if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }

                // 1. »щем MFR (ћинимальные €чейки)
                int mfr = 5;
                FreeCellSolver.ExtendedSolverResult bestResult = null;

                for (int allowedFC = 0; allowedFC <= 4; allowedFC++)
                {
                    FreeCellSolver.ExtendedSolverResult result = new FreeCellSolver.ExtendedSolverResult();
                    yield return StartCoroutine(FreeCellSolver.SolveAsync(candidate, frameBudgetMs, allowedFC, result));

                    if (result.IsSolved)
                    {
                        mfr = allowedFC;
                        bestResult = result;
                        break;
                    }
                }

                // 2. ѕровер€ем на 4 €чейках комфортную длину пути и тупики
                if (bestResult != null && mfr <= 4)
                {
                    FreeCellSolver.ExtendedSolverResult comfortResult = new FreeCellSolver.ExtendedSolverResult();
                    yield return StartCoroutine(FreeCellSolver.SolveAsync(candidate, frameBudgetMs, 4, comfortResult));

                    frameWatch.Restart();

                    int deadEnds = comfortResult.DeadEnds;
                    int moves = comfortResult.Moves; // ƒлина идеального машинного пути (человек сделает на 20-30 ходов больше)

                    // --- »ƒ≈јЋ№Ќџ≈ ‘»Ћ№“–џ (ќтполировано по вашим плейтестам) ---
                    bool isMatch = false;
                    int targetDiffPenalty = 0;

                    if (difficulty == Difficulty.Easy)
                    {
                        // Easy: »деальный путь до 85 ходов (человек сделает ~100-110), почти нет тупиков, остаютс€ 2 €чейки
                        isMatch = (moves <= 85 && deadEnds <= 50 && mfr <= 2);
                        targetDiffPenalty = (moves > 85 ? moves - 85 : 0) + (deadEnds > 50 ? deadEnds - 50 : 0);
                    }
                    else if (difficulty == Difficulty.Medium)
                    {
                        // Medium: Ѕаланс
                        isMatch = (moves > 70 && moves <= 110 && deadEnds > 50 && deadEnds <= 400 && mfr <= 3);
                        targetDiffPenalty = Math.Abs(200 - deadEnds);
                    }
                    else if (difficulty == Difficulty.Hard)
                    {
                        // Hard: ќгромное количество тупиков »Ћ» жестка€ нехватка €чеек
                        isMatch = (deadEnds > 500 || (mfr >= 3 && deadEnds > 250));
                        targetDiffPenalty = deadEnds < 500 ? 500 - deadEnds : 0;
                    }

                    if (targetDiffPenalty < bestDiff)
                    {
                        bestDiff = targetDiffPenalty;
                        bestCandidate = candidate.DeepClone();
                    }

                    if (isMatch)
                    {
                        validDeal = candidate;
                        UnityEngine.Debug.Log($"<color=green>[FreeCell Gen] FOUND {difficulty}! MFR:{mfr} | DeadEnds:{deadEnds} | IdealMoves:{moves} (Attempt {totalAttempts})</color>");
                        found = true;
                        break;
                    }

                    // ћутируем в нужную сторону
                    MutateDeal(candidate, difficulty == Difficulty.Hard || difficulty == Difficulty.Medium);
                }
                else
                {
                    MutateDeal(candidate, false);
                }
            }

            if (found) break;
            yield return null;
        }

        if (!found)
        {
            if (bestCandidate != null)
            {
                validDeal = bestCandidate;
                UnityEngine.Debug.LogWarning($"<color=yellow>[FreeCell Gen] Used fallback after {maxTotalAttempts} attempts. Penalty: {bestDiff}</color>");
            }
            else validDeal = CreateRandomDeal();
        }

        DealMetrics metrics = new DealMetrics();
        onComplete?.Invoke(validDeal, metrics);
    }

    private Deal CreateRandomDeal()
    {
        List<CardModel> deck = new List<CardModel>();
        foreach (Suit s in Enum.GetValues(typeof(Suit)))
            for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));

        System.Random rng = new System.Random();
        int n = deck.Count;
        while (n > 1) { n--; int k = rng.Next(n + 1); var val = deck[k]; deck[k] = deck[n]; deck[n] = val; }

        Deal d = new Deal();
        d.tableau = new List<List<CardInstance>>();
        for (int i = 0; i < 8; i++) d.tableau.Add(new List<CardInstance>());
        d.stock = new Stack<CardInstance>();
        d.waste = new List<CardInstance>();
        d.foundations = new List<List<CardModel>>();
        for (int i = 0; i < 4; i++) d.foundations.Add(new List<CardModel>());

        for (int i = 0; i < deck.Count; i++) d.tableau[i % 8].Add(new CardInstance(deck[i], true));

        return d;
    }

    private void MutateDeal(Deal d, bool makeHarder)
    {
        List<CardModel> flat = new List<CardModel>();
        foreach (var pile in d.tableau) foreach (var c in pile) flat.Add(c.Card);

        System.Random rng = new System.Random();

        if (makeHarder)
        {
            // ”сложн€ем: пр€чем “узы
            int easyCardIdx = flat.FindLastIndex(c => c.rank <= 2);
            if (easyCardIdx > 20)
            {
                int bottomTarget = rng.Next(0, 8);
                var temp = flat[easyCardIdx]; flat[easyCardIdx] = flat[bottomTarget]; flat[bottomTarget] = temp;
            }
            else { int r1 = rng.Next(0, 52); int r2 = rng.Next(0, 52); var temp = flat[r1]; flat[r1] = flat[r2]; flat[r2] = temp; }
        }
        else
        {
            // ”прощаем: достаем “узы
            int buriedCardIdx = flat.FindIndex(c => c.rank <= 2);
            if (buriedCardIdx != -1 && buriedCardIdx < 20)
            {
                int topTarget = rng.Next(44, 52);
                var temp = flat[buriedCardIdx]; flat[buriedCardIdx] = flat[topTarget]; flat[topTarget] = temp;
            }
            else { int r1 = rng.Next(0, 52); int r2 = rng.Next(0, 52); var temp = flat[r1]; flat[r1] = flat[r2]; flat[r2] = temp; }
        }

        foreach (var t in d.tableau) t.Clear();
        for (int i = 0; i < flat.Count; i++) d.tableau[i % 8].Add(new CardInstance(flat[i], true));
    }
}