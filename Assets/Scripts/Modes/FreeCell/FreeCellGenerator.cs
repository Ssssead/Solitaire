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
    public int maxMutationsPerCandidate = 15;
    public int maxTotalAttempts = 150;

    [Header("Optimization")]
    [Range(1, 30)]
    public float frameBudgetMs = 15.0f;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal validDeal = null;
        Deal bestCandidate = null;
        int bestDiffPenalty = int.MaxValue;

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

                GetStructuralMetrics(candidate, out int acesDepth, out int foundBlocks, out int readyToHome, out int safeMoves);
                int chaos = CalculateVisualChaosStatic(candidate);

                // --- 1. FAST REJECTION ---
                if (difficulty == Difficulty.Easy && (acesDepth > 10 || chaos > 48 || foundBlocks > 17 || readyToHome == 0))
                {
                    MutateDeal(candidate, false);
                    continue;
                }
                if (difficulty == Difficulty.Medium && (acesDepth < 6 || acesDepth > 14 || foundBlocks < 12 || foundBlocks > 22))
                {
                    MutateDeal(candidate, acesDepth < 10);
                    continue;
                }
                // Для Hard требуем, чтобы тузы были глубоко, но не "на самом дне" со старта, и чтобы были ложные пути
                if (difficulty == Difficulty.Hard && (acesDepth < 15 || foundBlocks < 12))
                {
                    MutateDeal(candidate, true);
                    continue;
                }

                // 2. ЗАПУСК ИИ-СОЛВЕРА
                FreeCellSolver.ExtendedSolverResult comfortResult = new FreeCellSolver.ExtendedSolverResult();
                yield return StartCoroutine(FreeCellSolver.SolveAsync(candidate, frameBudgetMs, 4, comfortResult));

                frameWatch.Restart();

                if (!comfortResult.IsSolved)
                {
                    MutateDeal(candidate, false);
                    continue;
                }

                int deadEnds = comfortResult.DeadEnds;

                // 3. ОЦЕНКА И ШТРАФЫ
                int targetDiffPenalty = 0;
                bool isMatch = false;

                if (difficulty == Difficulty.Easy)
                {
                    targetDiffPenalty = deadEnds +
                                        Math.Max(0, acesDepth - 8) * 5 +
                                        Math.Max(0, chaos - 42) * 2 +
                                        Math.Max(0, foundBlocks - 14) * 5 +
                                        (readyToHome == 0 ? 20 : 0);

                    isMatch = targetDiffPenalty <= 25;
                }
                else if (difficulty == Difficulty.Medium)
                {
                    targetDiffPenalty = (deadEnds < 30 ? (30 - deadEnds) : 0) + (deadEnds > 200 ? (deadEnds - 200) : 0) +
                                        Math.Abs(10 - acesDepth) * 3 +
                                        Math.Abs(45 - chaos) * 1 +
                                        Math.Abs(15 - foundBlocks) * 2;

                    isMatch = targetDiffPenalty <= 45;
                }
                else if (difficulty == Difficulty.Hard)
                {
                    // ИДЕАЛ: Огромный лабиринт (DeadEnds > 750), иллюзия выбора (SafeMoves >= 3)
                    // Штрафуем, если тупиков меньше 750 (Делим на 10, чтобы штраф был соразмерен остальным метрикам)
                    targetDiffPenalty = (deadEnds < 750 ? (750 - deadEnds) / 10 : 0) +
                                        (acesDepth < 18 ? (18 - acesDepth) * 2 : 0) +
                                        (foundBlocks < 14 ? (14 - foundBlocks) * 3 : 0) +
                                        (safeMoves < 3 ? (3 - safeMoves) * 5 : 0) +
                                        (readyToHome > 0 ? readyToHome * 15 : 0);

                    // Если генератор выдал 800 тупиков, штраф за них будет 0.
                    isMatch = targetDiffPenalty <= 25;
                }

                if (targetDiffPenalty < bestDiffPenalty)
                {
                    bestDiffPenalty = targetDiffPenalty;
                    bestCandidate = candidate.DeepClone();
                }

                // НАШЛИ ПОДХОДЯЩИЙ УРОВЕНЬ!
                if (isMatch)
                {
                    validDeal = candidate;
                    UnityEngine.Debug.Log($"<color=green>[FreeCell Gen] <b>FAST MATCH {difficulty}!</b> Penalty: {targetDiffPenalty} | DeadEnds:{deadEnds} | AcesDepth:{acesDepth} | Blocks:{foundBlocks} | SafeMoves:{safeMoves}</color>");
                    found = true;
                    break;
                }

                // 4. ДИНАМИЧЕСКАЯ МУТАЦИЯ
                bool needsMoreChaos;
                // В Харде топим тузы только если они лежат выше глубины 22. 
                // Иначе просто слегка перетасовываем, чтобы набить тупики!
                if (difficulty == Difficulty.Hard) needsMoreChaos = acesDepth < 22;
                else if (difficulty == Difficulty.Easy) needsMoreChaos = false;
                else needsMoreChaos = acesDepth < 10;

                MutateDeal(candidate, needsMoreChaos);
            }

            if (found) break;
        }

        if (!found)
        {
            if (bestCandidate != null)
            {
                validDeal = bestCandidate;
                UnityEngine.Debug.LogWarning($"<color=orange>[FreeCell Gen] Used fallback for {difficulty}. Penalty: {bestDiffPenalty}</color>");
            }
            else validDeal = CreateRandomDeal();
        }

        DealMetrics metrics = new DealMetrics();
        onComplete?.Invoke(validDeal, metrics);
    }

    private void GetStructuralMetrics(Deal d, out int acesDepth, out int foundBlocks, out int readyToHome, out int safeMoves)
    {
        acesDepth = 0; foundBlocks = 0; readyToHome = 0; safeMoves = 0;

        for (int col = 0; col < 8; col++)
        {
            if (d.tableau[col].Count == 0) continue;
            var topCard = d.tableau[col].Last().Card;
            if (topCard.rank == 1) readyToHome++;

            for (int j = 0; j < 8; j++)
            {
                if (col == j || d.tableau[j].Count == 0) continue;
                var target = d.tableau[j].Last().Card;
                bool cIsRed = (topCard.suit == Suit.Diamonds || topCard.suit == Suit.Hearts);
                bool targetIsRed = (target.suit == Suit.Diamonds || target.suit == Suit.Hearts);
                if (cIsRed != targetIsRed && topCard.rank == target.rank - 1) safeMoves++;
            }
        }

        for (int col = 0; col < 8; col++)
        {
            var pile = d.tableau[col];
            for (int j = 0; j < pile.Count; j++)
            {
                var c = pile[j].Card;
                int depthOverCard = (pile.Count - 1) - j;

                if (c.rank <= 2) acesDepth += depthOverCard;

                for (int under = 0; under < j; under++)
                {
                    var cardUnder = pile[under].Card;
                    if (c.suit == cardUnder.suit && c.rank > cardUnder.rank) foundBlocks++;
                }
            }
        }
    }

    private int CalculateVisualChaosStatic(Deal d)
    {
        int chaos = 0;
        for (int col = 0; col < 8; col++)
        {
            var pile = d.tableau[col];
            for (int j = 0; j < pile.Count; j++)
            {
                var c = pile[j].Card;
                int effectiveDepth = 0;

                for (int k = pile.Count - 1; k > j; k--)
                {
                    var cardAbove = pile[k].Card;
                    var cardBelow = pile[k - 1].Card;

                    bool topRed = (cardAbove.suit == Suit.Diamonds || cardAbove.suit == Suit.Hearts);
                    bool botRed = (cardBelow.suit == Suit.Diamonds || cardBelow.suit == Suit.Hearts);

                    if (topRed == botRed || cardAbove.rank != cardBelow.rank - 1) effectiveDepth++;
                    if (cardAbove.suit == c.suit && cardAbove.rank > c.rank) chaos++;

                    bool cRed = (c.suit == Suit.Diamonds || c.suit == Suit.Hearts);
                    if (topRed != cRed && cardAbove.rank == c.rank + 1) chaos++;
                }
                if (pile.Count - 1 > j) effectiveDepth++;
                if (c.rank == 1 || c.rank == 2) chaos += effectiveDepth;
            }
        }
        return chaos;
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
            int easyCardIdx = flat.FindLastIndex(c => c.rank <= 2);
            if (easyCardIdx > 20)
            {
                int bottomTarget = rng.Next(0, 8);
                var temp = flat[easyCardIdx]; flat[easyCardIdx] = flat[bottomTarget]; flat[bottomTarget] = temp;
            }
        }
        else
        {
            int buriedCardIdx = flat.FindIndex(c => c.rank <= 2);
            if (buriedCardIdx != -1 && buriedCardIdx < 30)
            {
                int topTarget = rng.Next(44, 52);
                var temp = flat[buriedCardIdx]; flat[buriedCardIdx] = flat[topTarget]; flat[topTarget] = temp;
            }
        }

        int r1 = rng.Next(0, 52);
        int r2 = rng.Next(0, 52);
        var tRandom = flat[r1]; flat[r1] = flat[r2]; flat[r2] = tRandom;

        foreach (var t in d.tableau) t.Clear();
        for (int i = 0; i < flat.Count; i++) d.tableau[i % 8].Add(new CardInstance(flat[i], true));
    }
}