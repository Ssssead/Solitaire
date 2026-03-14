using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Diagnostics;

public class SpiderLevelGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Spider;

    [Header("Generation Strategy")]
    public bool calibrationMode = true;
    public Difficulty targetDifficulty;
    public int targetSuitsCount = 2;

    [Header("Optimization")]
    [Range(1, 16)]
    public float frameBudgetMs = 8.0f;
    public int maxMutations = 50000;

    private int targetMinScore;
    private int targetMaxScore;

    // Новые лимиты: Сколько состояний Солвер должен проверить, чтобы мы поверили в сложность
    private int targetMinStates;
    private int targetMaxStates;

    private readonly int[] topIndices = new int[] { 5, 11, 17, 23, 28, 33, 38, 43, 48, 53 };

    private void Start()
    {
        if (calibrationMode)
        {
            UnityEngine.Debug.Log($"[SpiderGen] Starting Evolution Mutator for {targetDifficulty} ({targetSuitsCount} Suits)...");
            StartCoroutine(GenerateDeal(targetDifficulty, targetSuitsCount, (deal, metrics) =>
            {
                UnityEngine.Debug.Log("<color=yellow>[SpiderGen] DONE! Targeted deal generated and verified.</color>");
            }));
        }
    }

    public override IEnumerator GenerateDeal(Difficulty difficulty, int suitsCount, Action<Deal, DealMetrics> onComplete)
    {
        SetTargets(difficulty, suitsCount);

        Deal validDeal = null;
        int totalAttempts = 0;
        int solverRejects = 0;
        bool found = false;

        Stopwatch frameWatch = Stopwatch.StartNew();

        while (!found)
        {
            totalAttempts++;
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }

            List<CardModel> evolvedDeck = MutateDeckToTarget(suitsCount);
            Deal candidate = BuildDealFromDeck(evolvedDeck);

            SpiderSolver.ExtendedSolverResult result = new SpiderSolver.ExtendedSolverResult();
            yield return StartCoroutine(SpiderSolver.SolveAsync(candidate, suitsCount, frameBudgetMs, result));
            frameWatch.Restart();

            if (result.IsSolved)
            {
                // ФИНАЛЬНАЯ ПРОВЕРКА СЛОЖНОСТИ СОЛВЕРОМ
                if (result.StatesVisited >= targetMinStates && result.StatesVisited <= targetMaxStates)
                {
                    validDeal = candidate;
                    int finalScore = CalculateFlatSDS(evolvedDeck, suitsCount);

                    UnityEngine.Debug.Log($"<color=green>[SpiderGen] FOUND! Diff:{difficulty}. SDS:{finalScore}. States:{result.StatesVisited}. Mutations:{totalAttempts}, Rejects:{solverRejects}</color>");

                    DealMetrics outMetrics = new DealMetrics
                    {
                        Solved = true,
                        MoveEstimate = result.Moves
                    };

                    onComplete?.Invoke(validDeal, outMetrics);
                    found = true;
                    break;
                }
                else
                {
                    // Расклад оказался слишком легким или слишком сложным для нужной категории
                    solverRejects++;
                }
            }

            if (totalAttempts % 5 == 0) yield return null;
        }
    }

    private void SetTargets(Difficulty diff, int suits)
    {
        if (suits == 1)
        {
            if (diff == Difficulty.Easy) { targetMinScore = 10; targetMaxScore = 30; targetMinStates = 0; targetMaxStates = 600; }
            else if (diff == Difficulty.Medium) { targetMinScore = 35; targetMaxScore = 45; targetMinStates = 600; targetMaxStates = 2500; }
            else { targetMinScore = 50; targetMaxScore = 200; targetMinStates = 2500; targetMaxStates = 1500000; }
        }
        else if (suits == 2)
        {
            if (diff == Difficulty.Easy) { targetMinScore = 30; targetMaxScore = 55; targetMinStates = 0; targetMaxStates = 4000; }
            else if (diff == Difficulty.Medium) { targetMinScore = 60; targetMaxScore = 80; targetMinStates = 4000; targetMaxStates = 20000; }
            else
            {
                // === ЖЕСТКИЙ HARD ДЛЯ 2 МАСТЕЙ ===
                // Увеличили стартовый хаос (95) и заставили солвер перебирать минимум 60 000 состояний!
                targetMinScore = 95; targetMaxScore = 200; targetMinStates = 60000; targetMaxStates = 1500000;
            }
        }
        else // 4 Suits
        {
            if (diff == Difficulty.Easy) { targetMinScore = 70; targetMaxScore = 95; targetMinStates = 0; targetMaxStates = 15000; }
            else if (diff == Difficulty.Medium) { targetMinScore = 96; targetMaxScore = 115; targetMinStates = 15000; targetMaxStates = 50000; }
            else { targetMinScore = 116; targetMaxScore = 300; targetMinStates = 50000; targetMaxStates = 1500000; }
        }
    }

    private List<CardModel> MutateDeckToTarget(int suitsCount)
    {
        List<CardModel> deck = new List<CardModel>();
        int[] suitsMap = GetSuitsMap(suitsCount);
        foreach (int s in suitsMap)
            for (int r = 1; r <= 13; r++) deck.Add(new CardModel((Suit)s, r));

        Shuffle(deck); // Естественная стартовая база

        int currentScore = CalculateFlatSDS(deck, suitsCount);

        for (int m = 0; m < maxMutations; m++)
        {
            if (currentScore >= targetMinScore && currentScore <= targetMaxScore)
                break;

            // Только 100% чистые случайные перестановки (никаких искусственных "склеек")
            int idxA = Random.Range(0, 104);
            int idxB = Random.Range(0, 104);

            var temp = deck[idxA];
            deck[idxA] = deck[idxB];
            deck[idxB] = temp;

            int newScore = CalculateFlatSDS(deck, suitsCount);

            int currentDist = GetDistanceToRange(currentScore, targetMinScore, targetMaxScore);
            int newDist = GetDistanceToRange(newScore, targetMinScore, targetMaxScore);

            if (newDist <= currentDist)
            {
                currentScore = newScore;
            }
            else
            {
                // Откат
                temp = deck[idxA];
                deck[idxA] = deck[idxB];
                deck[idxB] = temp;
            }
        }

        return deck;
    }

    private int GetDistanceToRange(int val, int min, int max)
    {
        if (val < min) return min - val;
        if (val > max) return val - max;
        return 0;
    }

    private int CalculateFlatSDS(List<CardModel> deck, int suitsCount)
    {
        int score = 0;
        if (suitsCount == 1) score += 15;
        else if (suitsCount == 2) score += 45;
        else if (suitsCount == 4) score += 80;

        int initialMoves = 0;
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                if (i == j) continue;
                if (deck[topIndices[j]].rank == deck[topIndices[i]].rank - 1) initialMoves++;
            }
        }
        score -= (initialMoves * 2);

        int kingsOnHidden = 0;
        int deepestKing = 0;
        int buriedAces = 0;
        int highBlockers = 0;
        int hiddenRankBreaks = 0;
        int unnaturalSuitClumps = 0; // Новая метрика: неестественные скопления одной масти

        int idx = 0;
        for (int i = 0; i < 10; i++)
        {
            int colSize = (i < 4) ? 6 : 5;
            int hiddenCount = colSize - 1;
            var topCard = deck[idx + hiddenCount];

            if (topCard.rank == 13) kingsOnHidden++;
            if (topCard.rank >= 11) highBlockers += (topCard.rank - 10) * hiddenCount * 50;

            int currentSuitClump = 1;

            for (int j = 0; j < hiddenCount; j++)
            {
                if (deck[idx + j].rank <= 2) buriedAces += (hiddenCount - j);

                // Проверка на неестественные скопления одной масти под рубашками (если мастей > 1)
                if (suitsCount > 1 && j > 0)
                {
                    if (deck[idx + j].suit == deck[idx + j - 1].suit) currentSuitClump++;
                    else currentSuitClump = 1;

                    // Если 4 закрытые карты подряд одной масти — это выглядит подозрительно искусственно
                    if (currentSuitClump >= 4) unnaturalSuitClumps++;
                }
            }

            for (int j = 0; j < hiddenCount - 1; j++)
            {
                var top = deck[idx + j];
                var bot = deck[idx + j + 1];
                if (bot.rank != top.rank - 1 || bot.suit != top.suit) hiddenRankBreaks++;
                if (top.rank == 13) deepestKing = Mathf.Max(deepestKing, hiddenCount - j);
            }
            if (hiddenCount > 0 && deck[idx + hiddenCount - 1].rank == 13) deepestKing = Mathf.Max(deepestKing, 1);

            idx += colSize;
        }

        // Рентген колоды работает ТОЛЬКО против "ультра-хардкора"
        // Если мы не наказываем за это в Easy/Medium, игра оставит естественные совпадения в Stock!
        if (targetMinScore >= 95) // Признак уровня Hard
        {
            int stockFreePairs = 0;
            for (int col = 0; col < 10; col++)
            {
                for (int deal = 0; deal < 4; deal++)
                {
                    int firstDealIdx = 103 - (deal * 10) - col;
                    int secondDealIdx = 103 - ((deal + 1) * 10) - col;

                    var c1 = deck[firstDealIdx];
                    var c2 = deck[secondDealIdx];

                    if (c2.rank == c1.rank - 1 && c2.suit == c1.suit) stockFreePairs++;
                }
            }
            score -= (stockFreePairs * 5);
        }

        score += (kingsOnHidden * 4);
        score += (deepestKing * 2);
        score += buriedAces;
        score += (highBlockers / 100);
        score += (hiddenRankBreaks / 2);
        score += (unnaturalSuitClumps * 10); // Штраф за искусственную сортировку

        return Mathf.Max(1, score);
    }

    private Deal BuildDealFromDeck(List<CardModel> deck)
    {
        Deal d = new Deal();
        d.tableau.Clear();
        for (int i = 0; i < 10; i++) d.tableau.Add(new List<CardInstance>());

        int idx = 0;
        for (int i = 0; i < 10; i++)
        {
            int colSize = (i < 4) ? 6 : 5;
            for (int k = 0; k < colSize; k++)
            {
                d.tableau[i].Add(new CardInstance(deck[idx++], k == colSize - 1));
            }
        }

        while (idx < 104) d.stock.Push(new CardInstance(deck[idx++], false));

        return d;
    }

    private int[] GetSuitsMap(int count)
    {
        if (count == 1) return new int[] { 3, 3, 3, 3, 3, 3, 3, 3 };
        if (count == 2) return new int[] { 3, 2, 3, 2, 3, 2, 3, 2 };
        return new int[] { 0, 1, 2, 3, 0, 1, 2, 3 };
    }

    private void Shuffle<T>(List<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}