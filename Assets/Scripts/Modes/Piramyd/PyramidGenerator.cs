using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PyramidGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Pyramid;

    private struct GenCard
    {
        public int Suit; // 0-3
        public int Rank; // 1-13
        public int ID;   // 0-51
    }

    private Dictionary<int, List<int>> coverMap;

    private void Awake()
    {
        InitializeCoverMap();
    }

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, System.Action<Deal, DealMetrics> onComplete)
    {
        // 1. Создаем колоду
        List<GenCard> deck = CreateFullDeck();

        // 2. Базовая перетасовка
        Shuffle(deck);

        yield return null;

        // 3. Применяем смещение (Bias) в зависимости от сложности
        ApplyDifficultyBias(deck, difficulty);

        // 4. Проверка и исправление "Мертвых замков" (Hard Locks)
        int attempts = 0;
        int maxFixCycles = 50;

        while (HasHardLocks(deck) && attempts < maxFixCycles)
        {
            FixLocks(deck);
            attempts++;
            if (attempts % 10 == 0) yield return null;
        }

        if (attempts >= maxFixCycles)
        {
            Debug.LogWarning($"[PyramidGenerator] Could not fully resolve locks after {attempts} attempts. Retrying...");
            yield return StartCoroutine(GenerateDeal(difficulty, param, onComplete));
            yield break;
        }

        // 5. Собираем объект Deal
        Deal finalDeal = BuildDealObject(deck);

        // 6. Возвращаем результат
        var metrics = new DealMetrics
        {
            Solved = true,
            MoveEstimate = 0
        };

        onComplete?.Invoke(finalDeal, metrics);
    }

    // --- 1. BIASING (НАСТРОЙКА СЛОЖНОСТИ) ---

    private void ApplyDifficultyBias(List<GenCard> deck, Difficulty difficulty)
    {
        if (difficulty == Difficulty.Easy)
        {
            // EASY: Короли внизу пирамиды или в начале стока
            var kingsIndices = GetIndicesByRank(deck, 13);
            List<int> easySlots = new List<int> { 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
            ShuffleList(easySlots);

            for (int i = 0; i < kingsIndices.Count; i++)
            {
                Swap(deck, kingsIndices[i], easySlots[i]);
            }

            // EASY: В начало стока подтягиваем пары к открытым картам
            int stockPtr = 28;
            for (int i = 21; i <= 27; i++)
            {
                if (stockPtr >= 36) break;

                GenCard tableCard = deck[i];
                if (tableCard.Rank == 13) continue;

                int neededRank = 13 - tableCard.Rank;
                int pairIdx = FindCardIndex(deck, neededRank, 36, 52);

                if (pairIdx != -1)
                {
                    Swap(deck, stockPtr, pairIdx);
                    stockPtr++;
                }
            }
        }
        else if (difficulty == Difficulty.Medium)
        {
            // MEDIUM: Не больше 1 короля на самой верхушке
            int kingsAtTop = 0;
            for (int i = 0; i <= 5; i++)
            {
                if (deck[i].Rank == 13) kingsAtTop++;
            }

            if (kingsAtTop > 1)
            {
                var kings = GetIndicesByRank(deck, 13).Where(idx => idx <= 5).ToList();
                for (int k = 1; k < kings.Count; k++)
                {
                    int swapTarget = Random.Range(30, 52);
                    Swap(deck, kings[k], swapTarget);
                }
            }
        }
        else if (difficulty == Difficulty.Hard)
        {
            // HARD: Короли на вершине или в конце стока
            var kingsIndices = GetIndicesByRank(deck, 13);
            List<int> hardSlots = new List<int> { 0, 1, 2, 3, 4, 5, 46, 47, 48, 49, 50, 51 };
            ShuffleList(hardSlots);

            for (int i = 0; i < kingsIndices.Count; i++)
            {
                Swap(deck, kingsIndices[i], hardSlots[i]);
            }

            // HARD: Анти-синергия в начале стока
            for (int s = 28; s <= 32; s++)
            {
                GenCard stockCard = deck[s];
                bool helpsPlayer = false;
                for (int t = 21; t <= 27; t++)
                {
                    if (deck[t].Rank + stockCard.Rank == 13)
                    {
                        helpsPlayer = true;
                        break;
                    }
                }

                if (helpsPlayer)
                {
                    int deepSlot = Random.Range(35, 52);
                    Swap(deck, s, deepSlot);
                }
            }
        }
    }

    // --- 2. VALIDATION & FIXING ---

    private bool HasHardLocks(List<GenCard> deck)
    {
        for (int i = 0; i < 21; i++)
        {
            GenCard blocker = deck[i];
            if (blocker.Rank == 13) continue;

            int targetRank = 13 - blocker.Rank;
            List<int> coveredIndices = GetCoveredIndicesRecursive(i);

            int pairsAvailableElsewhere = 0;

            for (int k = 0; k < 52; k++)
            {
                if (deck[k].Rank == targetRank)
                {
                    if (k >= 28) // В стоке
                    {
                        pairsAvailableElsewhere++;
                    }
                    else if (k > i && !IsCoveredBy(k, i)) // В другой ветке
                    {
                        pairsAvailableElsewhere++;
                    }
                }
            }

            if (pairsAvailableElsewhere == 0) return true;
        }
        return false;
    }

    private void FixLocks(List<GenCard> deck)
    {
        for (int i = 0; i < 21; i++)
        {
            GenCard blocker = deck[i];
            if (blocker.Rank == 13) continue;

            int targetRank = 13 - blocker.Rank;
            List<int> coveredIndices = GetCoveredIndicesRecursive(i);

            int pairsAvailable = 0;
            for (int k = 0; k < 52; k++)
            {
                if (deck[k].Rank == targetRank)
                {
                    if (!coveredIndices.Contains(k) && k >= 28) pairsAvailable++;
                }
            }

            if (pairsAvailable == 0)
            {
                int swapIdx = FindSafeSwapIndexInStock(deck, targetRank);
                Swap(deck, i, swapIdx);
                return;
            }
        }
    }

    private int FindSafeSwapIndexInStock(List<GenCard> deck, int rankToAvoid)
    {
        List<int> candidates = new List<int>();
        for (int i = 28; i < 52; i++)
        {
            if (deck[i].Rank != rankToAvoid && deck[i].Rank != 13)
                candidates.Add(i);
        }
        if (candidates.Count > 0) return candidates[Random.Range(0, candidates.Count)];
        return Random.Range(28, 52);
    }

    // --- DATA HELPERS ---

    private void InitializeCoverMap()
    {
        coverMap = new Dictionary<int, List<int>>();
        int currentRow = 0;
        int cardsInRow = 1;
        int cardIndex = 0;

        while (currentRow < 6)
        {
            for (int i = 0; i < cardsInRow; i++)
            {
                int leftChild = cardIndex + currentRow + 1;
                int rightChild = cardIndex + currentRow + 2;
                coverMap[cardIndex] = new List<int> { leftChild, rightChild };
                cardIndex++;
            }
            currentRow++;
            cardsInRow++;
        }
    }

    private List<int> GetCoveredIndicesRecursive(int index)
    {
        List<int> result = new List<int>();
        if (!coverMap.ContainsKey(index)) return result;

        Queue<int> queue = new Queue<int>();
        foreach (var child in coverMap[index]) queue.Enqueue(child);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (!result.Contains(current))
            {
                result.Add(current);
                if (coverMap.ContainsKey(current))
                {
                    foreach (var child in coverMap[current]) queue.Enqueue(child);
                }
            }
        }
        return result;
    }

    private bool IsCoveredBy(int child, int parent)
    {
        var covered = GetCoveredIndicesRecursive(parent);
        return covered.Contains(child);
    }

    // --- GENERIC HELPERS ---

    private List<GenCard> CreateFullDeck()
    {
        List<GenCard> d = new List<GenCard>();
        int id = 0;
        for (int s = 0; s < 4; s++)
        {
            for (int r = 1; r <= 13; r++)
            {
                d.Add(new GenCard { Suit = s, Rank = r, ID = id++ });
            }
        }
        return d;
    }

    private void Shuffle(List<GenCard> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            GenCard value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    private void Swap(List<GenCard> list, int a, int b)
    {
        GenCard temp = list[a];
        list[a] = list[b];
        list[b] = temp;
    }

    private List<int> GetIndicesByRank(List<GenCard> deck, int rank)
    {
        List<int> idxs = new List<int>();
        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i].Rank == rank) idxs.Add(i);
        }
        return idxs;
    }

    private int FindCardIndex(List<GenCard> deck, int rank, int startIdx, int endIdx)
    {
        for (int i = startIdx; i < endIdx; i++)
        {
            if (deck[i].Rank == rank) return i;
        }
        return -1;
    }

    // --- CONVERSION TO GAME OBJECTS ---

    private Deal BuildDealObject(List<GenCard> flatDeck)
    {
        Deal deal = new Deal();
        deal.stock = new Stack<CardInstance>();
        deal.tableau = new List<List<CardInstance>>();

        int ptr = 0;
        // Заполняем Пирамиду
        for (int row = 0; row < 7; row++)
        {
            List<CardInstance> rowList = new List<CardInstance>();
            for (int col = 0; col <= row; col++)
            {
                var gCard = flatDeck[ptr++];
                CardModel model = new CardModel((Suit)gCard.Suit, gCard.Rank);
                rowList.Add(new CardInstance(model, true));
            }
            deal.tableau.Add(rowList);
        }

        // Заполняем Сток
        for (int i = 51; i >= 28; i--)
        {
            var gCard = flatDeck[i];
            CardModel model = new CardModel((Suit)gCard.Suit, gCard.Rank);
            // --- ИЗМЕНЕНИЕ ЗДЕСЬ: FaceUp = true ---
            deal.stock.Push(new CardInstance(model, true));
        }

        return deal;
    }
}