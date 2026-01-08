using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpiderLevelGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Spider;

    private class GenCard
    {
        public int Suit;
        public int Rank;
        public GenCard(int s, int r) { Suit = s; Rank = r; }

        public CardInstance ToCardInstance(bool faceUp)
        {
            return new CardInstance(new CardModel((Suit)Suit, Rank), faceUp);
        }

        // Для отладки
        public override string ToString() => $"{((Suit)Suit).ToString()[0]}{Rank}";
    }

    public override IEnumerator GenerateDeal(Difficulty difficulty, int suitsCount, Action<Deal, DealMetrics> onComplete)
    {
        // 1. Получаем коэффициент смещения на основе ваших критериев
        float bias = GetSameSuitBias(suitsCount, difficulty);

        // 2. Создаем и тасуем колоду с учетом смещения
        List<List<GenCard>> tableau;
        List<GenCard> stock;

        GenerateBiasedDeal(suitsCount, bias, out tableau, out stock);

        // 3. Формируем объект Deal для игры
        Deal finalDeal = new Deal();
        finalDeal.tableau = new List<List<CardInstance>>();
        finalDeal.stock = new Stack<CardInstance>();

        for (int i = 0; i < 10; i++)
        {
            List<CardInstance> colList = new List<CardInstance>();
            for (int k = 0; k < tableau[i].Count; k++)
            {
                // В Пауке обычно последняя карта открыта, остальные закрыты
                bool isTop = (k == tableau[i].Count - 1);
                colList.Add(tableau[i][k].ToCardInstance(isTop));
            }
            finalDeal.tableau.Add(colList);
        }

        foreach (var c in stock)
        {
            finalDeal.stock.Push(c.ToCardInstance(false));
        }

        Debug.Log($"[SpiderGenerator] Generated {suitsCount} Suits, {difficulty}. Bias: {bias}");

        yield return null;
        onComplete?.Invoke(finalDeal, new DealMetrics { Solved = true });
    }

    // --- НАСТРОЙКА КОЭФФИЦИЕНТОВ (Ваши критерии) ---
    private float GetSameSuitBias(int suits, Difficulty diff)
    {
        if (suits == 1)
        {
            // Easy: 0.8 (Sequences), Medium: 0.5 (Random)
            return (diff == Difficulty.Easy) ? 0.8f : 0.5f;
        }
        else if (suits == 2)
        {
            // Easy: 0.9 (Segregated)
            if (diff == Difficulty.Easy) return 0.9f;
            // Hard: 0.2 (Interleaved / Blocking)
            if (diff == Difficulty.Hard) return 0.2f;
            // Medium: 0.5 (Mixed)
            return 0.5f;
        }
        else // 4 Suits
        {
            // Hard: 0.05 (Chaos)
            if (diff == Difficulty.Hard) return 0.05f;
            // Medium: 0.4 (Fair) - Easy отключен, но на всякий случай вернем 0.4
            return 0.4f;
        }
    }

    // --- АЛГОРИТМ BIASED SHUFFLE ---
    private void GenerateBiasedDeal(int suitsCount, float bias, out List<List<GenCard>> tableau, out List<GenCard> stock)
    {
        // 1. Создаем колоду (104 карты)
        var deck = CreateDeck(suitsCount);

        // 2. Первичная полная перемешка (Честный рандом как база)
        Shuffle(deck);

        // 3. Применяем смещение (Bias) к картам, которые попадут на стол (Tableau)
        // В Пауке первые 54 карты идут на стол.
        // Раздача идет по рядам: 1-я карта в 1-ю колонку, 2-я во 2-ю... 11-я снова в 1-ю (поверх 1-й).
        // Значит, карта с индексом i лежит на карте с индексом (i - 10).

        int tableauSize = 54;

        for (int i = 10; i < tableauSize; i++)
        {
            int parentIndex = i - 10; // Индекс карты, на которую ляжет текущая
            GenCard parentCard = deck[parentIndex];

            float roll = Random.value;

            // Логика:
            // Если roll < bias -> Ищем "Идеальную пару" (Rank-1, Та же масть). Это создает последовательности.
            // Если roll > bias И сложность Hard (bias низкий) -> Ищем "Блокирующую пару" (Rank-1, Другая масть).

            bool forceGoodMatch = (roll < bias);

            // Для Харда 2 масти (bias 0.2): если не выпал GoodMatch, с вероятностью 80% попробуем сделать "Гадость" (BadMatch)
            // Но только если ранг позволяет (не Туз)
            bool forceBadMatch = !forceGoodMatch && (bias <= 0.25f) && (suitsCount > 1);

            if (forceGoodMatch && parentCard.Rank > 1)
            {
                // Ищем карту (Rank-1, Suit == Parent.Suit) в оставшейся части колоды (от i до конца)
                int swapTarget = FindCardIndex(deck, i, parentCard.Rank - 1, parentCard.Suit);
                if (swapTarget != -1)
                {
                    Swap(deck, i, swapTarget);
                }
            }
            else if (forceBadMatch && parentCard.Rank > 1)
            {
                // Ищем карту (Rank-1, Suit != Parent.Suit) чтобы заблокировать стопку
                // -1 в suit означает "любая другая масть"
                int swapTarget = FindCardIndex(deck, i, parentCard.Rank - 1, -1, parentCard.Suit);
                if (swapTarget != -1)
                {
                    Swap(deck, i, swapTarget);
                }
            }
            // Иначе оставляем карту как есть (Random)
        }

        // 4. Раскладываем обработанную колоду по спискам
        tableau = new List<List<GenCard>>();
        for (int i = 0; i < 10; i++) tableau.Add(new List<GenCard>());

        int cardIdx = 0;
        // 54 карты на стол
        // Ряды 0-4 (по 10 карт)
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 10; col++) tableau[col].Add(deck[cardIdx++]);
        }
        // Ряд 5 (4 карты)
        for (int col = 0; col < 4; col++) tableau[col].Add(deck[cardIdx++]);

        // Остальное в сток
        stock = new List<GenCard>();
        while (cardIdx < deck.Count)
        {
            stock.Add(deck[cardIdx++]);
        }

        // Сток в этом алгоритме мы не трогаем (оставляем рандомным), 
        // так как Bias влияет на стартовую ситуацию на столе.
    }

    // --- ПОМОЩНИКИ ---

    private int FindCardIndex(List<GenCard> deck, int startIndex, int targetRank, int targetSuit, int excludeSuit = -1)
    {
        // Ищем подходящую карту в диапазоне [startIndex, End]
        // Мы перемешиваем индексы поиска, чтобы не брать всегда первую попавшуюся (сохраняем энтропию)
        List<int> searchIndices = Enumerable.Range(startIndex, deck.Count - startIndex).ToList();
        Shuffle(searchIndices); // Локальная мешалка для поиска

        foreach (int idx in searchIndices)
        {
            GenCard c = deck[idx];
            if (c.Rank == targetRank)
            {
                // Если ищем конкретную масть
                if (targetSuit != -1 && c.Suit == targetSuit) return idx;

                // Если ищем ЛЮБУЮ масть, КРОМЕ excludeSuit (для блокировки)
                if (targetSuit == -1 && excludeSuit != -1 && c.Suit != excludeSuit) return idx;
            }
        }
        return -1;
    }

    private void Swap(List<GenCard> list, int a, int b)
    {
        GenCard temp = list[a];
        list[a] = list[b];
        list[b] = temp;
    }

    private List<GenCard> CreateDeck(int suitsCount)
    {
        List<GenCard> deck = new List<GenCard>();
        int[] suitsMap = GetSuitsMap(suitsCount);

        foreach (int s in suitsMap)
        {
            for (int r = 1; r <= 13; r++)
            {
                deck.Add(new GenCard(s, r));
            }
        }
        return deck;
    }

    private int[] GetSuitsMap(int count)
    {
        // 0=Clubs, 1=Diamonds, 2=Hearts, 3=Spades
        if (count == 1) return new int[] { 3, 3, 3, 3, 3, 3, 3, 3 }; // 8 Пик
        if (count == 2) return new int[] { 3, 2, 3, 2, 3, 2, 3, 2 }; // 4 Пики, 4 Черви
        return new int[] { 0, 1, 2, 3, 0, 1, 2, 3 }; // По 2 каждой
    }

    private void Shuffle<T>(List<T> list)
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
}