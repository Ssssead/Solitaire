using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

public class KlondikeRandomGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Klondike;

    [Header("Generation Strategy")]
    public int maxMutationsPerCandidate = 5;
    public bool calibrationMode = true;
    public Difficulty targetDifficulty;

    [Header("Optimization")]
    [Range(1, 16)]
    public float frameBudgetMs = 8.0f; // Бюджет времени на кадр

    private int minScore, maxScore;

    // --- АВТОЗАПУСК (ДЛЯ ТЕСТОВ) ---
    private void Start()
    {
        if (calibrationMode)
        {
            // Для теста можно менять 1 на 3
            int testParam = 1;
            UnityEngine.Debug.Log($"[SmartGen] Starting Infinite Grinder for {targetDifficulty} (Draw {testParam})...");

            StartCoroutine(GenerateDeal(targetDifficulty, testParam, (deal, metrics) =>
            {
                UnityEngine.Debug.Log("<color=yellow>[SmartGen] DONE! Deal generated.</color>");
            }));
        }
    }

    // --- ГЛАВНАЯ КОРУТИНА ---
    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        // Настраиваем диапазоны в зависимости от режима (Draw 1 или 3)
        SetupScoreRanges(difficulty, param);

        Deal validDeal = null;
        int totalAttempts = 0;
        bool found = false;

        Stopwatch frameWatch = new Stopwatch();

        while (!found)
        {
            totalAttempts++;

            // Проверка времени перед созданием
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }
            else if (!frameWatch.IsRunning) frameWatch.Start();

            // 1. Создаем базу (Архитектурный подход)
            Deal candidate = CreateSmartDeal(difficulty);

            // 2. Мутации
            for (int m = 0; m <= maxMutationsPerCandidate; m++)
            {
                // Проверка времени перед тяжелым солвером
                if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }

                float timeLeft = frameBudgetMs - frameWatch.ElapsedMilliseconds;
                if (timeLeft <= 0) { yield return null; frameWatch.Restart(); timeLeft = frameBudgetMs; }

                KlondikeSolver.ExtendedSolverResult result = new KlondikeSolver.ExtendedSolverResult();

                // Запускаем Солвер (он учитывает param для правил перекладки)
                yield return StartCoroutine(KlondikeSolver.SolveAsync(candidate, param, frameBudgetMs, result));

                frameWatch.Restart();

                if (result.IsSolved)
                {
                    int score = result.Score;

                    // Попали в диапазон?
                    if (score >= minScore && score <= maxScore)
                    {
                        validDeal = candidate;
                        UnityEngine.Debug.Log($"<color=green>[Gen] FOUND! Diff:{difficulty} (Draw {param}) Score:{score}. Att:{totalAttempts} Mut:{m}</color>");
                        UnityEngine.Debug.Log($"Stats: Moves:{result.Moves} Recyc:{result.Recycles} Traps:{result.Traps} Breaks:{result.Breaks}");
                        found = true;
                        break;
                    }

                    // Если не попали - мутируем. Передаем param, чтобы знать специфику Draw 3.
                    if (score < minScore) MutateSmart(candidate, true, param);
                    else if (score > maxScore) MutateSmart(candidate, false, param);
                }
                else
                {
                    // Не решается -> упрощаем
                    MutateSmart(candidate, false, param);
                }
            }

            if (found) break;

            // Пауза каждые 5 попыток полного цикла, даже если бюджет есть (для GC и стабильности)
            if (totalAttempts % 5 == 0) yield return null;
        }

        onComplete?.Invoke(validDeal, null);
    }

    // --- НАСТРОЙКА ДИАПАЗОНОВ ---
    private void SetupScoreRanges(Difficulty d, int param)
    {
        if (param == 3)
        {
            // DRAW 3: Новые диапазоны на основе анализа
            // Очки выше, так как каждый Recycle теперь стоит 1500, а их много.
            switch (d)
            {
                case Difficulty.Easy: minScore = 2000; maxScore = 9000; break;
                case Difficulty.Medium: minScore = 10000; maxScore = 25000; break;
                case Difficulty.Hard: minScore = 30000; maxScore = int.MaxValue; break;
            }
        }
        else
        {
            // DRAW 1: Старые проверенные диапазоны (как в исходном файле)
            switch (d)
            {
                case Difficulty.Easy: minScore = 1000; maxScore = 5000; break;
                case Difficulty.Medium: minScore = 7500; maxScore = 13000; break;
                case Difficulty.Hard: minScore = 16000; maxScore = 999999; break;
            }
        }
    }

    // --- УМНАЯ МУТАЦИЯ (С ЛОГИКОЙ DRAW 3) ---
    private void MutateSmart(Deal d, bool makeHarder, int drawParam)
    {
        List<CardModel> flat = FlattenDeal(d);
        System.Random rng = new System.Random();

        // Специфика Draw 3:
        // Иногда проблема не в том, где лежат карты, а в "фазе" (индекс % 3).
        // Перестановка двух карт ВНУТРИ стока меняет доступность всего веера.
        // Шанс 40%, если режим Draw 3.
        bool useStockShuffle = (drawParam == 3 && rng.NextDouble() < 0.40);

        if (useStockShuffle)
        {
            // Сток начинается с 28-го индекса (0-27 это табло)
            int stockStart = 28;
            if (stockStart < flat.Count)
            {
                // Меняем две случайные карты внутри стока
                int idxA = rng.Next(stockStart, flat.Count);
                int idxB = rng.Next(stockStart, flat.Count);
                Swap(flat, idxA, idxB);

                // И еще раз для надежности
                idxA = rng.Next(stockStart, flat.Count);
                idxB = rng.Next(stockStart, flat.Count);
                Swap(flat, idxA, idxB);
            }
        }
        else if (makeHarder)
        {
            // Усложнить: Закопать туза или поднять короля
            int easyAceIdx = flat.FindLastIndex(c => c.rank <= 2);
            if (easyAceIdx > 15)
            {
                Swap(flat, easyAceIdx, rng.Next(0, 10)); // На дно
            }
            else
            {
                int kingIdx = flat.FindIndex(c => c.rank >= 12);
                if (kingIdx != -1 && kingIdx < 20) Swap(flat, kingIdx, rng.Next(21, 28)); // На верхушки
                else Swap(flat, rng.Next(0, 52), rng.Next(0, 52));
            }
        }
        else // makeEasier
        {
            // Упростить: Поднять туза или убрать короля с верхушки
            int buriedAceIdx = flat.FindIndex(c => c.rank <= 2);
            if (buriedAceIdx != -1 && buriedAceIdx < 20)
            {
                Swap(flat, buriedAceIdx, rng.Next(28, 52)); // В сток или наверх
            }
            else
            {
                bool kingMoved = false;
                // Ищем короля на вершинах стопок (примерно 21-27)
                for (int i = 21; i <= 27; i++)
                {
                    if (flat[i].rank >= 12) { Swap(flat, i, rng.Next(0, 10)); kingMoved = true; break; }
                }
                if (!kingMoved) Swap(flat, rng.Next(0, 52), rng.Next(0, 52));
            }
        }

        RebuildDeal(d, flat);
    }

    // --- АРХИТЕКТОР РАСКЛАДА (Без изменений) ---
    private Deal CreateSmartDeal(Difficulty difficulty)
    {
        List<CardModel> deck = new List<CardModel>();
        foreach (Suit s in Enum.GetValues(typeof(Suit))) for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));

        var aces = deck.Where(c => c.rank <= 2).ToList();
        var kings = deck.Where(c => c.rank >= 12).ToList();
        var mids = deck.Where(c => c.rank >= 3 && c.rank <= 11).ToList();

        System.Random rng = new System.Random();
        List<CardModel> constructedDeck = new List<CardModel>(new CardModel[52]);
        bool[] filled = new bool[52];

        void PlaceCards(List<CardModel> source, int startIdx, int endIdx, int count)
        {
            Shuffle(source, rng);
            for (int i = 0; i < count && source.Count > 0; i++)
            {
                var card = source[0]; source.RemoveAt(0);
                List<int> slots = new List<int>();
                for (int k = startIdx; k <= endIdx; k++) if (!filled[k]) slots.Add(k);

                if (slots.Count > 0)
                {
                    int slot = slots[rng.Next(slots.Count)];
                    constructedDeck[slot] = card; filled[slot] = true;
                }
                else { mids.Add(card); }
            }
        }

        if (difficulty == Difficulty.Hard)
        {
            PlaceCards(aces, 0, 6, 4);      // Тузы на дно
            PlaceCards(kings, 21, 27, 4);   // Короли наверх (блок)
            mids.AddRange(aces); aces.Clear();
            mids.AddRange(kings); kings.Clear();
            PlaceCards(mids, 0, 51, 52);
        }
        else if (difficulty == Difficulty.Medium)
        {
            PlaceCards(aces, 0, 15, 2);
            PlaceCards(kings, 15, 30, 2);
            mids.AddRange(aces); aces.Clear();
            mids.AddRange(kings); kings.Clear();
            PlaceCards(mids, 0, 51, 52);
        }
        else // EASY
        {
            PlaceCards(kings, 0, 10, 6);    // Короли на дно (не мешают)
            PlaceCards(aces, 21, 35, 6);    // Тузы доступнее
            mids.AddRange(aces); aces.Clear();
            mids.AddRange(kings); kings.Clear();
            PlaceCards(mids, 0, 51, 52);
        }

        for (int i = 0; i < 52; i++) if (!filled[i] && mids.Count > 0) { constructedDeck[i] = mids[0]; mids.RemoveAt(0); }

        Deal d = new Deal();
        RebuildDeal(d, constructedDeck);
        return d;
    }

    // --- ХЕЛПЕРЫ ---
    private void Swap(List<CardModel> list, int a, int b)
    {
        if (a < 0 || b < 0 || a >= list.Count || b >= list.Count) return;
        var temp = list[a]; list[a] = list[b]; list[b] = temp;
    }

    private List<CardModel> FlattenDeal(Deal d)
    {
        List<CardModel> list = new List<CardModel>();
        foreach (var pile in d.tableau) foreach (var c in pile) list.Add(c.Card);
        list.AddRange(d.stock.Select(x => x.Card));
        return list;
    }

    private void RebuildDeal(Deal d, List<CardModel> cards)
    {
        foreach (var t in d.tableau) t.Clear();
        d.stock.Clear();
        int idx = 0;
        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                if (idx < cards.Count) d.tableau[i].Add(new CardInstance(cards[idx++], j == i));
            }
        }
        while (idx < cards.Count) d.stock.Push(new CardInstance(cards[idx++], false));
    }

    private void Shuffle<T>(List<T> list, System.Random rng)
    {
        int n = list.Count;
        while (n > 1) { n--; int k = rng.Next(n + 1); T value = list[k]; list[k] = list[n]; list[n] = value; }
    }
}