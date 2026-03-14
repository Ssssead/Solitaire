using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class PyramidGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Pyramid;

    [Header("Generation Strategy")]
    public int maxMutationsPerCandidate = 30;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal validDeal = null;

        // Запускаем весь процесс генерации и валидации в фоновом потоке!
        // Это полностью освобождает главный поток Unity от фризов.
        Task<Deal> generationTask = Task.Run(() => GenerateDealBackground(difficulty));

        // Ждем, пока фоновый поток закончит работу (FPS при этом не падает)
        while (!generationTask.IsCompleted)
        {
            yield return null;
        }

        if (generationTask.IsFaulted)
        {
            UnityEngine.Debug.LogError($"[Pyramid Gen] Error: {generationTask.Exception}");
        }
        else
        {
            validDeal = generationTask.Result;
        }

        var metrics = new DealMetrics { Solved = true, MoveEstimate = 0, StockPasses = 0 };
        onComplete?.Invoke(validDeal, metrics);
    }

    // Этот метод теперь работает вне главного потока
    private Deal GenerateDealBackground(Difficulty difficulty)
    {
        int totalAttempts = 0;

        // Создаем контекст ОДИН раз на всю генерацию (Экономия памяти и Garbage Collector)
        PyramidSolver.SolverContext solverContext = new PyramidSolver.SolverContext();
        PyramidSolver.SolverResult result = new PyramidSolver.SolverResult();

        while (true)
        {
            totalAttempts++;
            Deal candidate = CreateSmartDeal(difficulty);

            for (int m = 0; m <= maxMutationsPerCandidate; m++)
            {
                // Вызываем синхронный поиск (он использует закэшированную память)
                PyramidSolver.Solve(candidate, 2, solverContext, result);

                if (result.IsSolved)
                {
                    int states = result.StatesVisited;
                    int inv = result.Inversions;
                    bool match = false;
                    bool makeHarder = false;

                    switch (difficulty)
                    {
                        case Difficulty.Easy:
                            if (states <= 25000 && inv <= 11) match = true;
                            else if (states > 25000) makeHarder = false;
                            break;

                        case Difficulty.Medium:
                            if (states > 25000 && states <= 260000) match = true;
                            else makeHarder = states <= 25000;
                            break;

                        case Difficulty.Hard:
                            if (states > 260000 && inv >= 12) match = true;
                            else makeHarder = true;
                            break;
                    }

                    if (match)
                    {
                        // Debug.Log не потокобезопасен в Unity, поэтому логи выведем потом или используем потокобезопасные методы
                        return candidate; // Нашли! Возвращаем результат
                    }
                    else
                    {
                        MutateSafe(candidate, makeHarder);
                    }
                }
                else
                {
                    MutateSafe(candidate, false);
                }
            }
        }
    }

    private Deal CreateSmartDeal(Difficulty difficulty)
    {
        List<CardModel> deck = new List<CardModel>();
        foreach (Suit s in Enum.GetValues(typeof(Suit)))
            for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));

        System.Random rng = new System.Random();
        Shuffle(deck, rng);

        if (difficulty == Difficulty.Easy) MoveKingsToBottomOrStock(deck, rng);
        else if (difficulty == Difficulty.Medium) MoveKingsToMiddle(deck, rng);
        else if (difficulty == Difficulty.Hard)
        {
            MoveKingsToTop(deck, rng);
            CreateChoiceTraps(deck, rng, 2);
        }

        Deal d = new Deal();
        RebuildDeal(d, deck);
        return d;
    }

    private void MutateSafe(Deal d, bool makeHarder)
    {
        List<CardModel> flat = FlattenDeal(d);
        System.Random rng = new System.Random();

        List<int> validTableau = new List<int>();
        for (int i = 0; i < 28; i++)
            if (flat[i].rank != 13) validTableau.Add(i);

        List<int> validStock = new List<int>();
        for (int i = 28; i < 52; i++)
            if (flat[i].rank != 13) validStock.Add(i);

        if (validTableau.Count == 0 || validStock.Count == 0) return;

        if (makeHarder)
        {
            int stockIdx = validStock[rng.Next(validStock.Count)];
            var topCandidates = validTableau.Where(x => x <= 14).ToList();
            if (topCandidates.Count > 0)
            {
                int topIdx = topCandidates[rng.Next(topCandidates.Count)];
                Swap(flat, stockIdx, topIdx);
            }
        }
        else
        {
            var topCandidates = validTableau.Where(x => x <= 14).ToList();
            if (topCandidates.Count > 0)
            {
                int topIdx = topCandidates[rng.Next(topCandidates.Count)];
                int easierIdx = validStock[rng.Next(validStock.Count)];
                Swap(flat, topIdx, easierIdx);
            }
        }

        RebuildDeal(d, flat);
    }

    private void MoveKingsToBottomOrStock(List<CardModel> deck, System.Random rng)
    {
        List<int> easySlots = new List<int> { 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        Shuffle(easySlots, rng);
        int slotPtr = 0;
        for (int i = 0; i < deck.Count; i++)
            if (deck[i].rank == 13 && i < 21 && slotPtr < easySlots.Count) Swap(deck, i, easySlots[slotPtr++]);
    }

    private void MoveKingsToMiddle(List<CardModel> deck, System.Random rng)
    {
        List<int> midSlots = new List<int> { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
        Shuffle(midSlots, rng);
        int slotPtr = 0;
        for (int i = 0; i < deck.Count; i++)
            if (deck[i].rank == 13 && (i < 6 || i > 27) && slotPtr < midSlots.Count) Swap(deck, i, midSlots[slotPtr++]);
    }

    private void MoveKingsToTop(List<CardModel> deck, System.Random rng)
    {
        List<int> hardSlots = new List<int> { 0, 1, 2, 3, 4, 5 };
        Shuffle(hardSlots, rng);
        int slotPtr = 0;
        for (int i = 0; i < deck.Count; i++)
            if (deck[i].rank == 13 && i >= 6 && slotPtr < hardSlots.Count) Swap(deck, i, hardSlots[slotPtr++]);
    }

    private void CreateChoiceTraps(List<CardModel> deck, System.Random rng, int trapCount)
    {
        List<int> validParents = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        Shuffle(validParents, rng);

        int trapsCreated = 0;
        List<int> usedRanks = new List<int> { 13 };
        HashSet<int> lockedIndices = new HashSet<int>();

        for (int i = 0; i < validParents.Count && trapsCreated < trapCount; i++)
        {
            int parentIdx = validParents[i];
            int row = GetRow(parentIdx);
            int childIdx = parentIdx + row + 1 + (rng.NextDouble() > 0.5 ? 1 : 0);

            if (lockedIndices.Contains(parentIdx) || lockedIndices.Contains(childIdx)) continue;

            int rank1 = rng.Next(1, 13);
            if (usedRanks.Contains(rank1) || usedRanks.Contains(13 - rank1)) continue;

            int rank2 = 13 - rank1;
            usedRanks.Add(rank1); usedRanks.Add(rank2);

            List<int> baitSlots = new List<int> { 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 };
            baitSlots.RemoveAll(x => lockedIndices.Contains(x) || x == parentIdx || x == childIdx);
            if (baitSlots.Count < 2) continue;
            Shuffle(baitSlots, rng);

            lockedIndices.Add(parentIdx); lockedIndices.Add(childIdx);
            lockedIndices.Add(baitSlots[0]); lockedIndices.Add(baitSlots[1]);

            PlaceCardSafe(deck, parentIdx, rank1, lockedIndices);
            PlaceCardSafe(deck, childIdx, rank2, lockedIndices);
            PlaceCardSafe(deck, baitSlots[0], rank1, lockedIndices);
            PlaceCardSafe(deck, baitSlots[1], rank2, lockedIndices);

            trapsCreated++;
        }
    }

    private void PlaceCardSafe(List<CardModel> deck, int targetIdx, int targetRank, HashSet<int> locked)
    {
        if (deck[targetIdx].rank == targetRank) return;
        for (int i = 0; i < deck.Count; i++)
            if (deck[i].rank == targetRank && !locked.Contains(i)) { Swap(deck, targetIdx, i); return; }
        for (int i = 0; i < deck.Count; i++)
            if (deck[i].rank == targetRank && i != targetIdx) { Swap(deck, targetIdx, i); return; }
    }

    private int GetRow(int index)
    {
        if (index == 0) return 0;
        if (index <= 2) return 1;
        if (index <= 5) return 2;
        if (index <= 9) return 3;
        if (index <= 14) return 4;
        if (index <= 20) return 5;
        return 6;
    }

    private List<CardModel> FlattenDeal(Deal d)
    {
        List<CardModel> list = new List<CardModel>();
        foreach (var row in d.tableau) foreach (var c in row) list.Add(c.Card);
        var stockArr = d.stock.ToArray();
        for (int i = 0; i < stockArr.Length; i++) list.Add(stockArr[i].Card);
        return list;
    }

    private void RebuildDeal(Deal d, List<CardModel> cards)
    {
        d.tableau.Clear(); d.stock.Clear();
        int ptr = 0;
        for (int row = 0; row < 7; row++)
        {
            var rowList = new List<CardInstance>();
            for (int col = 0; col <= row; col++) rowList.Add(new CardInstance(cards[ptr++], true));
            d.tableau.Add(rowList);
        }
        for (int i = 51; i >= 28; i--) d.stock.Push(new CardInstance(cards[i], true));
    }

    private void Swap(List<CardModel> list, int a, int b) { var temp = list[a]; list[a] = list[b]; list[b] = temp; }
    private void Shuffle<T>(List<T> list, System.Random rng) { int n = list.Count; while (n > 1) { n--; int k = rng.Next(n + 1); T value = list[k]; list[k] = list[n]; list[n] = value; } }
}