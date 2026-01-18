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
    public int maxMutationsPerCandidate = 5; // Если за 5 попыток не вышло - сброс и новый расклад
    public bool calibrationMode = true;      // Автозапуск при старте
    public Difficulty targetDifficulty;      // Целевая сложность для тестов
    [Header("Optimization")]
    [Range(1, 16)]
    public float frameBudgetMs = 8.0f; // Бюджет: 8ms = 120 FPS, 16ms = 60 FPS

    // Диапазоны Score (устанавливаются в коде)
    private int minScore, maxScore;

    // --- АВТОЗАПУСК (ДЛЯ ТЕСТОВ В РЕДАКТОРЕ) ---
    private void Start()
    {
        if (calibrationMode)
        {
            UnityEngine.Debug.Log($"[SmartGen] Starting Infinite Grinder for {targetDifficulty}...");
            // Запускаем бесконечный поиск одного идеального расклада
            StartCoroutine(GenerateDeal(targetDifficulty, 1, (deal, metrics) =>
            {
                UnityEngine.Debug.Log("<color=yellow>[SmartGen] DONE! Valid deal found and ready.</color>");
            }));
        }
    }

    // --- ГЛАВНАЯ КОРУТИНА ГЕНЕРАЦИИ ---
    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        SetupScoreRanges(difficulty);
        Deal validDeal = null;
        int totalAttempts = 0;
        bool found = false;

        // Таймер для мутаций
        Stopwatch frameWatch = new Stopwatch();

        while (!found)
        {
            totalAttempts++;

            // Если бюджет кадра кончился во время создания - ждем
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }

            // 1. Создаем базу (Быстро)
            Deal candidate = CreateSmartDeal(difficulty);

            // 2. Мутации
            for (int m = 0; m <= maxMutationsPerCandidate; m++)
            {
                // Запускаем АСИНХРОННЫЙ Солвер
                // Мы передаем ему "остаток" времени в кадре
                float timeLeft = frameBudgetMs - frameWatch.ElapsedMilliseconds;
                if (timeLeft <= 0) { yield return null; frameWatch.Restart(); timeLeft = frameBudgetMs; }

                KlondikeSolver.ExtendedSolverResult result = new KlondikeSolver.ExtendedSolverResult();

                // ЖДЕМ, ПОКА СОЛВЕР ЗАКОНЧИТ (ОН БУДЕТ ДЕЛАТЬ YIELD ВНУТРИ СЕБЯ)
                yield return StartCoroutine(KlondikeSolver.SolveAsync(candidate, param, frameBudgetMs, result));

                // Когда вернулись сюда - Солвер закончил работу
                // Проверяем бюджет, так как yield был в солвере
                frameWatch.Restart();

                if (result.IsSolved)
                {
                    int score = result.Score;
                    if (score >= minScore && score <= maxScore)
                    {
                        validDeal = candidate;
                        UnityEngine.Debug.Log($"<color=green>[Gen] FOUND! Diff:{difficulty} Score:{score} ({totalAttempts} attempts)</color>");
                        found = true;
                        break;
                    }

                    if (score < minScore) MutateSmart(candidate, true);
                    else if (score > maxScore) MutateSmart(candidate, false);
                }
                else
                {
                    MutateSmart(candidate, false);
                }
            }

            if (found) break;
            yield return null;
        }

        onComplete?.Invoke(validDeal, null);
    }

    // =========================================================================
    // 1. SMART CONSTRUCTION (АРХИТЕКТОР РАСКЛАДА)
    // =========================================================================
    private Deal CreateSmartDeal(Difficulty difficulty)
    {
        // 1. Создаем чистую колоду
        List<CardModel> deck = new List<CardModel>();
        foreach (Suit s in Enum.GetValues(typeof(Suit))) for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));

        // 2. Делим на фракции
        var aces = deck.Where(c => c.rank <= 2).ToList();    // A, 2 (Ключевые карты)
        var kings = deck.Where(c => c.rank >= 12).ToList();  // Q, K (Блокеры)
        var mids = deck.Where(c => c.rank >= 3 && c.rank <= 11).ToList(); // Остальное ("Мясо")

        System.Random rng = new System.Random();
        List<CardModel> constructedDeck = new List<CardModel>(new CardModel[52]); // Пустой массив для сборки
        bool[] filled = new bool[52];

        // Локальная функция для размещения карт в заданный диапазон индексов
        void PlaceCards(List<CardModel> source, int startIdx, int endIdx, int count)
        {
            Shuffle(source, rng);
            // Пытаемся разместить count карт
            for (int i = 0; i < count && source.Count > 0; i++)
            {
                var card = source[0];
                source.RemoveAt(0);

                // Ищем свободные слоты в диапазоне
                List<int> slots = new List<int>();
                for (int k = startIdx; k <= endIdx; k++) if (!filled[k]) slots.Add(k);

                if (slots.Count > 0)
                {
                    int slot = slots[rng.Next(slots.Count)];
                    constructedDeck[slot] = card;
                    filled[slot] = true;
                }
                else
                {
                    // Если места нет, возвращаем в пул "мяса"
                    mids.Add(card);
                }
            }
        }

        // --- ЛОГИКА СЛОЕВ ---
        // Индексы в плоском списке (0..51) при стандартной раздаче:
        // 0..6:   Самое дно 7 стопок (Bottom Layer) -> Влияет на AceDepth
        // 7..20:  Середина закрытых карт (Middle Layer)
        // 21..27: Верхние открытые карты стопок (Top Layer) -> Влияет на Blockers
        // 28..51: Колода (Stock)

        if (difficulty == Difficulty.Hard)
        {
            // HARD: 
            // 1. Закапываем Тузы и Двойки на дно (0-6)
            PlaceCards(aces, 0, 6, 4);

            // 2. Блокируем верх стопок Королями и Дамами (21-27)
            PlaceCards(kings, 21, 27, 4);

            // 3. Остаток Тузов/Королей смешиваем с мидом и заполняем остальное
            mids.AddRange(aces); aces.Clear();
            mids.AddRange(kings); kings.Clear();
            PlaceCards(mids, 0, 51, 52);
        }
        else if (difficulty == Difficulty.Medium)
        {
            // MEDIUM: Смешанный подход
            // Немного тузов вниз, немного королей вверх, но без фанатизма
            PlaceCards(aces, 0, 15, 2);
            PlaceCards(kings, 15, 30, 2);

            mids.AddRange(aces); aces.Clear();
            mids.AddRange(kings); kings.Clear();
            PlaceCards(mids, 0, 51, 52);
        }
        else // EASY
        {
            // EASY: 
            // 1. Короли на дно (0-10), чтобы не блокировали
            PlaceCards(kings, 0, 10, 6);

            // 2. Тузы наверх стопок и в начало стока (21-35) - быстрый старт
            PlaceCards(aces, 21, 35, 6);

            mids.AddRange(aces); aces.Clear();
            mids.AddRange(kings); kings.Clear();
            PlaceCards(mids, 0, 51, 52);
        }

        // Заполнение пропусков (если алгоритм пропустил слоты)
        for (int i = 0; i < 52; i++) if (!filled[i] && mids.Count > 0) { constructedDeck[i] = mids[0]; mids.RemoveAt(0); }

        // Собираем объект Deal
        Deal d = new Deal();
        RebuildDeal(d, constructedDeck);
        return d;
    }

    // =========================================================================
    // 2. SMART MUTATION (УМНАЯ МУТАЦИЯ)
    // =========================================================================
    private void MutateSmart(Deal d, bool makeHarder)
    {
        List<CardModel> flat = FlattenDeal(d);
        System.Random rng = new System.Random();

        if (makeHarder)
        {
            // УСЛОЖНИТЬ:
            // 1. Найти Туза (A, 2), который лежит слишком высоко (>20) и закопать его
            int easyAceIdx = flat.FindLastIndex(c => c.rank <= 2);

            if (easyAceIdx > 15)
            {
                // Меняем с дном (0-10)
                Swap(flat, easyAceIdx, rng.Next(0, 10));
            }
            else
            {
                // Если Тузы уже глубоко, найдем Короля внизу и поднимем наверх (создать пробку)
                int buriedKingIdx = flat.FindIndex(c => c.rank >= 12);
                if (buriedKingIdx != -1 && buriedKingIdx < 20)
                    Swap(flat, buriedKingIdx, rng.Next(21, 28)); // На верхушки
                else
                    Swap(flat, rng.Next(0, 52), rng.Next(0, 52)); // Рандом, если нет идей
            }
        }
        else // Make Easier
        {
            // УПРОСТИТЬ:
            // 1. Найти закопанного Туза (<20) и поднять наверх (>28)
            int buriedAceIdx = flat.FindIndex(c => c.rank <= 2);

            if (buriedAceIdx != -1 && buriedAceIdx < 20)
            {
                Swap(flat, buriedAceIdx, rng.Next(28, 52)); // В сток или наверх
            }
            else
            {
                // Или убрать блокирующего Короля с вершины (21-27) на дно
                bool kingMoved = false;
                for (int i = 21; i <= 27; i++)
                {
                    if (flat[i].rank >= 12) { Swap(flat, i, rng.Next(0, 10)); kingMoved = true; break; }
                }
                if (!kingMoved) Swap(flat, rng.Next(0, 52), rng.Next(0, 52));
            }
        }
        RebuildDeal(d, flat);
    }

    // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---

    private void SetupScoreRanges(Difficulty d)
    {
        switch (d)
        {
            // Новые диапазоны на основе формулы 2.0
            case Difficulty.Easy: minScore = 1000; maxScore = 5000; break;
            case Difficulty.Medium: minScore = 7500; maxScore = 13500; break;
            case Difficulty.Hard: minScore = 16000; maxScore = 999999; break;
        }
    }

    private void Swap(List<CardModel> list, int a, int b)
    {
        if (a < 0 || b < 0 || a >= list.Count || b >= list.Count) return;
        var temp = list[a]; list[a] = list[b]; list[b] = temp;
    }

    // Превращает Deal в плоский список (Табло слева направо + Сток)
    private List<CardModel> FlattenDeal(Deal d)
    {
        List<CardModel> list = new List<CardModel>();
        foreach (var pile in d.tableau) foreach (var c in pile) list.Add(c.Card);
        // В Deal.stock Push кладет наверх. Чтобы сохранить порядок списка, берем как есть.
        list.AddRange(d.stock.Select(x => x.Card));
        return list;
    }

    // Собирает Deal обратно
    private void RebuildDeal(Deal d, List<CardModel> cards)
    {
        foreach (var t in d.tableau) t.Clear();
        d.stock.Clear();
        int idx = 0;
        // Раздача Табло
        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                if (idx < cards.Count) d.tableau[i].Add(new CardInstance(cards[idx++], j == i));
            }
        }
        // Остаток в Сток
        while (idx < cards.Count) d.stock.Push(new CardInstance(cards[idx++], false));
    }

    private void Shuffle<T>(List<T> list, System.Random rng)
    {
        int n = list.Count;
        while (n > 1) { n--; int k = rng.Next(n + 1); T value = list[k]; list[k] = list[n]; list[n] = value; }
    }
}