using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public class SultanGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Sultan;

    [Header("Optimization Constraints")]
    public float frameBudgetMs = 8.0f;

    // --- СТРУКТУРЫ ДЛЯ СИМУЛЯТОРА ЖАДНОГО ИГРОКА (ДЛЯ EASY) ---
    private CardModel[] grStock = new CardModel[89];
    private CardModel[] grWaste = new CardModel[89];
    private CardModel[] grReserve = new CardModel[6];
    private int[] grTopRank = new int[9];
    private bool[] grDone = new bool[9];

    // --- СТРУКТУРЫ ДЛЯ DFS СОЛВЕРА (ДЛЯ MEDIUM / HARD) ---
    private const int MAX_DEPTH = 600;
    private CardModel[][] dfsStock = new CardModel[MAX_DEPTH][];
    private CardModel[][] dfsWaste = new CardModel[MAX_DEPTH][];
    private CardModel[][] dfsReserve = new CardModel[MAX_DEPTH][];
    private int[][] dfsTopRank = new int[MAX_DEPTH][];
    private bool[][] dfsDone = new bool[MAX_DEPTH][];
    private int[] dfsWasteCount = new int[MAX_DEPTH];
    private int[] dfsStockCount = new int[MAX_DEPTH];
    private int[] dfsPassesUsed = new int[MAX_DEPTH];
    private int[] dfsCardsToBuild = new int[MAX_DEPTH];

    private bool arraysInitialized = false;

    private readonly Suit[] reqSuits = {
        Suit.Diamonds, Suit.Hearts,   Suit.Diamonds,
        Suit.Clubs,    Suit.Hearts,   Suit.Clubs,
        Suit.Spades,   Suit.Hearts,   Suit.Spades
    };

    private void InitArrays()
    {
        if (arraysInitialized) return;
        for (int i = 0; i < MAX_DEPTH; i++)
        {
            dfsStock[i] = new CardModel[89];
            dfsWaste[i] = new CardModel[89];
            dfsReserve[i] = new CardModel[6];
            dfsTopRank[i] = new int[9];
            dfsDone[i] = new bool[9];
        }
        arraysInitialized = true;
    }

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        InitArrays();
        UnityEngine.Debug.Log($"<color=cyan>[SmartGen] Starting Perfect Natural Generator for {difficulty}...</color>");

        Stopwatch frameWatch = new Stopwatch();
        frameWatch.Start();

        List<CardModel> fullDeck = new List<CardModel>(104);
        foreach (Suit s in Enum.GetValues(typeof(Suit)))
        {
            for (int r = 1; r <= 13; r++)
            {
                fullDeck.Add(new CardModel(s, r));
                fullDeck.Add(new CardModel(s, r));
            }
        }

        CardModel[] foundationBases = new CardModel[9];
        foundationBases[0] = ExtractCard(fullDeck, Suit.Diamonds, 13);
        foundationBases[1] = ExtractCard(fullDeck, Suit.Hearts, 1);
        foundationBases[2] = ExtractCard(fullDeck, Suit.Diamonds, 13);
        foundationBases[3] = ExtractCard(fullDeck, Suit.Clubs, 13);
        foundationBases[4] = ExtractCard(fullDeck, Suit.Hearts, 13); // Center
        foundationBases[5] = ExtractCard(fullDeck, Suit.Clubs, 13);
        foundationBases[6] = ExtractCard(fullDeck, Suit.Spades, 13);
        foundationBases[7] = ExtractCard(fullDeck, Suit.Hearts, 13);
        foundationBases[8] = ExtractCard(fullDeck, Suit.Spades, 13);

        CardModel[] deckArray = fullDeck.ToArray();
        System.Random rng = new System.Random();

        bool found = false;
        int totalAttempts = 0;
        int finalMovesOrBacktracks = 0;

        CardModel[] bestDeck = new CardModel[95];
        int leastCardsRemaining = 95;

        while (!found && totalAttempts < 5000)
        {
            totalAttempts++;

            if (frameWatch.ElapsedMilliseconds > frameBudgetMs)
            {
                yield return null;
                frameWatch.Restart();
            }

            // 1. ИДЕАЛЬНО ЧЕСТНАЯ ТАСОВКА
            for (int i = 0; i < 95; i++) Swap(deckArray, i, i + rng.Next(95 - i));

            // 2. НЕЗАМЕТНЫЙ ГРАДИЕНТ
            ApplySoftGradient(deckArray, difficulty, rng);

            // 3. ПРОВЕРКА В ЗАВИСИМОСТИ ОТ СЛОЖНОСТИ
            if (difficulty == Difficulty.Easy)
            {
                // Запускаем симулятор жадного человека (0 отмен, максимальная жадность)
                int totalMoves, cardsRemaining;
                bool isSolved = TrySolveGreedy(deckArray, out totalMoves, out cardsRemaining);

                if (cardsRemaining < leastCardsRemaining)
                {
                    leastCardsRemaining = cardsRemaining;
                    Array.Copy(deckArray, bestDeck, 95);
                }

                if (isSolved && totalMoves >= 250 && totalMoves <= 350)
                {
                    found = true;
                    finalMovesOrBacktracks = totalMoves;
                }
            }
            else
            {
                // Для Medium/Hard используем старый DFS (игрок должен думать)
                SetupInitialStateDFS(deckArray);
                int backtracks = 0;
                int passesUsed = 0;

                int targetMax = difficulty == Difficulty.Medium ? 8 : 40;
                int targetMin = difficulty == Difficulty.Medium ? 2 : 10;

                bool isSolved = TrySolveDFS(0, ref backtracks, targetMax, ref passesUsed);

                if (isSolved && backtracks >= targetMin && backtracks <= targetMax && passesUsed <= 3)
                {
                    found = true;
                    finalMovesOrBacktracks = backtracks;
                }
            }
        }

        if (!found && difficulty == Difficulty.Easy)
        {
            UnityEngine.Debug.LogWarning($"<color=orange>[SmartGen] Timeout. Using closest Easy deal (Left: {leastCardsRemaining}).</color>");
            Array.Copy(bestDeck, deckArray, 95);
        }
        else
        {
            string statName = difficulty == Difficulty.Easy ? "Moves" : "Backtracks";
            UnityEngine.Debug.Log($"<color=green>[SmartGen] FOUND {difficulty}! {statName}: {finalMovesOrBacktracks}. Attempts: {totalAttempts}</color>");
        }

        // Формируем финальный Deal
        Deal deal = new Deal();
        for (int i = 0; i < 6; i++) deal.tableau.Add(new List<CardInstance>());
        for (int i = 0; i < 9; i++) deal.foundations.Add(new List<CardModel>());

        for (int i = 0; i < 9; i++) deal.foundations[i].Add(foundationBases[i]);
        for (int i = 0; i < 6; i++) deal.tableau[i].Add(new CardInstance(deckArray[i], true));
        for (int i = 94; i >= 6; i--) deal.stock.Push(new CardInstance(deckArray[i], false));

        DealMetrics metrics = new DealMetrics { Solved = found, MoveEstimate = finalMovesOrBacktracks };

        frameWatch.Stop();
        onComplete?.Invoke(deal, metrics);
    }

    // --- МЯГКИЙ ГРАДИЕНТ (Скрытая магия) ---
    private void ApplySoftGradient(CardModel[] deck, Difficulty diff, System.Random rng)
    {
        if (diff == Difficulty.Easy)
        {
            // Меняем местами случайные карты. Если левая тяжелее правой - меняем.
            // 60 итераций недостаточно, чтобы отсортировать массив, но достаточно, 
            // чтобы слегка сдвинуть вероятности в нужную сторону. Расклад будет казаться 100% случайным.
            for (int k = 0; k < 60; k++)
            {
                int a = rng.Next(0, 95);
                int b = rng.Next(0, 95);
                if (a == b) continue;
                int first = Math.Min(a, b);
                int second = Math.Max(a, b);

                if (deck[first].rank > deck[second].rank) Swap(deck, first, second);
            }
        }
        else if (diff == Difficulty.Hard)
        {
            // Обратный процесс - загоняем тяжелые карты в начало (создаем ловушки)
            for (int k = 0; k < 40; k++)
            {
                int a = rng.Next(0, 95);
                int b = rng.Next(0, 95);
                if (a == b) continue;
                int first = Math.Min(a, b);
                int second = Math.Max(a, b);

                if (deck[first].rank < deck[second].rank) Swap(deck, first, second);
            }
        }
    }

    // ==========================================
    // СИМУЛЯТОР ЖАДНОГО ЧЕЛОВЕКА (Для Easy)
    // ==========================================
    private bool TrySolveGreedy(CardModel[] deck, out int totalMoves, out int cardsRemaining)
    {
        totalMoves = 0;
        int stockCount = 89;
        int wasteCount = 0;
        int passesUsed = 1;
        int cardsToBuild = 95;

        for (int i = 0; i < 6; i++) grReserve[i] = deck[i];
        for (int i = 0; i < 89; i++) grStock[i] = deck[94 - i];

        grTopRank[0] = 13; grTopRank[1] = 1; grTopRank[2] = 13;
        grTopRank[3] = 13; grTopRank[4] = 13; grTopRank[5] = 13;
        grTopRank[6] = 13; grTopRank[7] = 13; grTopRank[8] = 13;

        for (int i = 0; i < 9; i++) grDone[i] = false;
        grDone[4] = true;

        while (cardsToBuild > 0)
        {
            bool actionTaken = false;

            // 1. Попытка из Waste (Сброса) в Дом
            if (wasteCount > 0)
            {
                CardModel wCard = grWaste[wasteCount - 1];
                for (int j = 0; j < 9; j++)
                {
                    if (grDone[j]) continue;
                    int reqRank = (grTopRank[j] % 13) + 1;
                    if (wCard.suit == reqSuits[j] && wCard.rank == reqRank)
                    {
                        grTopRank[j] = reqRank;
                        if (reqRank == 12) grDone[j] = true;
                        wasteCount--;
                        cardsToBuild--;
                        totalMoves++;
                        actionTaken = true;
                        break;
                    }
                }
            }
            if (actionTaken) continue;

            // 2. Попытка из Резерва в Дом
            for (int i = 0; i < 6; i++)
            {
                if (grReserve[i].rank == 0) continue;
                for (int j = 0; j < 9; j++)
                {
                    if (grDone[j]) continue;
                    int reqRank = (grTopRank[j] % 13) + 1;
                    if (grReserve[i].suit == reqSuits[j] && grReserve[i].rank == reqRank)
                    {
                        grTopRank[j] = reqRank;
                        if (reqRank == 12) grDone[j] = true;
                        grReserve[i].rank = 0;
                        cardsToBuild--;
                        totalMoves++;
                        actionTaken = true;
                        break;
                    }
                }
                if (actionTaken) break;
            }
            if (actionTaken) continue;

            // 3. Жадное заполнение пустого резерва (Сначала из Сброса, потом из Колоды)
            int emptyRes = -1;
            for (int i = 0; i < 6; i++) if (grReserve[i].rank == 0) { emptyRes = i; break; }

            if (emptyRes != -1)
            {
                if (wasteCount > 0)
                {
                    grReserve[emptyRes] = grWaste[wasteCount - 1];
                    wasteCount--;
                    totalMoves++;
                    actionTaken = true;
                }
                else if (stockCount > 0)
                {
                    grReserve[emptyRes] = grStock[stockCount - 1];
                    stockCount--;
                    totalMoves++;
                    actionTaken = true;
                }
            }
            if (actionTaken) continue; // Начинаем цикл заново, так как открылась новая карта!

            // 4. Движение колоды
            if (stockCount > 0)
            {
                grWaste[wasteCount] = grStock[stockCount - 1];
                wasteCount++;
                stockCount--;
                totalMoves++;
                actionTaken = true;
                continue;
            }

            // 5. Пересдача
            if (stockCount == 0 && wasteCount > 0)
            {
                // По правилам Султана доступно только 2 пересдачи (3 прохода)
                if (passesUsed >= 3) break;

                for (int i = 0; i < wasteCount; i++) grStock[i] = grWaste[wasteCount - 1 - i];
                stockCount = wasteCount;
                wasteCount = 0;
                passesUsed++;
                totalMoves++;
                actionTaken = true;
                continue;
            }

            // Тупик - ни одного действия не выполнено
            break;
        }

        cardsRemaining = cardsToBuild;
        return cardsToBuild == 0;
    }

    // ==========================================
    // УМНЫЙ СОЛВЕР (Для Medium / Hard)
    // ==========================================
    private bool TrySolveDFS(int d, ref int backtracks, int maxBacktracks, ref int finalPassesUsed)
    {
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < 6; i++)
            {
                if (dfsReserve[d][i].rank == 0) continue;
                for (int j = 0; j < 9; j++)
                {
                    if (dfsDone[d][j]) continue;
                    int reqRank = (dfsTopRank[d][j] % 13) + 1;
                    if (dfsReserve[d][i].suit == reqSuits[j] && dfsReserve[d][i].rank == reqRank)
                    {
                        dfsTopRank[d][j] = reqRank;
                        if (reqRank == 12) dfsDone[d][j] = true;
                        dfsReserve[d][i].rank = 0;
                        dfsCardsToBuild[d]--;
                        changed = true;
                        break;
                    }
                }
            }
            if (dfsWasteCount[d] > 0)
            {
                CardModel wCard = dfsWaste[d][dfsWasteCount[d] - 1];
                for (int j = 0; j < 9; j++)
                {
                    if (dfsDone[d][j]) continue;
                    int reqRank = (dfsTopRank[d][j] % 13) + 1;
                    if (wCard.suit == reqSuits[j] && wCard.rank == reqRank)
                    {
                        dfsTopRank[d][j] = reqRank;
                        if (reqRank == 12) dfsDone[d][j] = true;
                        dfsWasteCount[d]--;
                        dfsCardsToBuild[d]--;
                        changed = true;
                        break;
                    }
                }
            }
        } while (changed);

        if (dfsCardsToBuild[d] == 0)
        {
            finalPassesUsed = dfsPassesUsed[d];
            return true;
        }

        if (d >= MAX_DEPTH - 2) return false;

        int emptyRes = -1;
        for (int i = 0; i < 6; i++) if (dfsReserve[d][i].rank == 0) { emptyRes = i; break; }

        if (emptyRes != -1)
        {
            if (dfsWasteCount[d] > 0 || dfsStockCount[d] > 0)
            {
                CopyState(d, d + 1);
                if (dfsWasteCount[d + 1] > 0)
                {
                    dfsReserve[d + 1][emptyRes] = dfsWaste[d + 1][dfsWasteCount[d + 1] - 1];
                    dfsWasteCount[d + 1]--;
                }
                else
                {
                    dfsReserve[d + 1][emptyRes] = dfsStock[d + 1][dfsStockCount[d + 1] - 1];
                    dfsStockCount[d + 1]--;
                }

                if (TrySolveDFS(d + 1, ref backtracks, maxBacktracks, ref finalPassesUsed)) return true;

                backtracks++;
                if (backtracks > maxBacktracks) return false;
            }

            if (dfsStockCount[d] > 0)
            {
                CopyState(d, d + 1);
                dfsWaste[d + 1][dfsWasteCount[d + 1]] = dfsStock[d + 1][dfsStockCount[d + 1] - 1];
                dfsWasteCount[d + 1]++;
                dfsStockCount[d + 1]--;
                if (TrySolveDFS(d + 1, ref backtracks, maxBacktracks, ref finalPassesUsed)) return true;

                backtracks++;
                if (backtracks > maxBacktracks) return false;
            }
            else if (dfsWasteCount[d] > 0 && dfsPassesUsed[d] < 3)
            {
                CopyState(d, d + 1);
                for (int i = 0; i < dfsWasteCount[d + 1]; i++) dfsStock[d + 1][i] = dfsWaste[d + 1][dfsWasteCount[d + 1] - 1 - i];
                dfsStockCount[d + 1] = dfsWasteCount[d + 1];
                dfsWasteCount[d + 1] = 0;
                dfsPassesUsed[d + 1]++;
                if (TrySolveDFS(d + 1, ref backtracks, maxBacktracks, ref finalPassesUsed)) return true;

                backtracks++;
                if (backtracks > maxBacktracks) return false;
            }
        }
        else
        {
            if (dfsStockCount[d] > 0)
            {
                CopyState(d, d + 1);
                dfsWaste[d + 1][dfsWasteCount[d + 1]] = dfsStock[d + 1][dfsStockCount[d + 1] - 1];
                dfsWasteCount[d + 1]++;
                dfsStockCount[d + 1]--;
                return TrySolveDFS(d + 1, ref backtracks, maxBacktracks, ref finalPassesUsed);
            }
            else if (dfsWasteCount[d] > 0 && dfsPassesUsed[d] < 3)
            {
                CopyState(d, d + 1);
                for (int i = 0; i < dfsWasteCount[d + 1]; i++) dfsStock[d + 1][i] = dfsWaste[d + 1][dfsWasteCount[d + 1] - 1 - i];
                dfsStockCount[d + 1] = dfsWasteCount[d + 1];
                dfsWasteCount[d + 1] = 0;
                dfsPassesUsed[d + 1]++;
                return TrySolveDFS(d + 1, ref backtracks, maxBacktracks, ref finalPassesUsed);
            }
        }

        return false;
    }

    private void SetupInitialStateDFS(CardModel[] deck)
    {
        dfsStockCount[0] = 89; dfsWasteCount[0] = 0;
        for (int i = 0; i < 6; i++) dfsReserve[0][i] = deck[i];
        for (int i = 0; i < 89; i++) dfsStock[0][i] = deck[94 - i];

        dfsTopRank[0][0] = 13; dfsTopRank[0][1] = 1; dfsTopRank[0][2] = 13;
        dfsTopRank[0][3] = 13; dfsTopRank[0][4] = 13; dfsTopRank[0][5] = 13;
        dfsTopRank[0][6] = 13; dfsTopRank[0][7] = 13; dfsTopRank[0][8] = 13;

        for (int i = 0; i < 9; i++) dfsDone[0][i] = false;
        dfsDone[0][4] = true;

        dfsPassesUsed[0] = 1; dfsCardsToBuild[0] = 95;
    }

    private void CopyState(int from, int to)
    {
        if (dfsWasteCount[from] > 0) Array.Copy(dfsWaste[from], dfsWaste[to], dfsWasteCount[from]);
        if (dfsStockCount[from] > 0) Array.Copy(dfsStock[from], dfsStock[to], dfsStockCount[from]);
        Array.Copy(dfsReserve[from], dfsReserve[to], 6);
        Array.Copy(dfsTopRank[from], dfsTopRank[to], 9);
        Array.Copy(dfsDone[from], dfsDone[to], 9);

        dfsWasteCount[to] = dfsWasteCount[from];
        dfsStockCount[to] = dfsStockCount[from];
        dfsPassesUsed[to] = dfsPassesUsed[from];
        dfsCardsToBuild[to] = dfsCardsToBuild[from];
    }

    private CardModel ExtractCard(List<CardModel> deck, Suit suit, int rank)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i].suit == suit && deck[i].rank == rank)
            {
                CardModel c = deck[i]; deck.RemoveAt(i); return c;
            }
        }
        return new CardModel(suit, rank);
    }

    private void Swap(CardModel[] arr, int a, int b)
    {
        CardModel temp = arr[a]; arr[a] = arr[b]; arr[b] = temp;
    }
}