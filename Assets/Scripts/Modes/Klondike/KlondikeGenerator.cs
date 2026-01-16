using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

public class KlondikeRandomGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Klondike;

    private const float FRAME_BUDGET_MS = 5.0f;
    private const float GLOBAL_TIMEOUT_SEC = 2.5f;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal validDeal = null;

        // Хранилища для "почти подходящих" раскладов
        Deal bestFallback = null;
        int bestFallbackScore = -1000; // Начинаем с очень низкого порога

        int attempts = 0;
        bool done = false;

        Stopwatch frameTimer = new Stopwatch();
        float totalTimeElapsed = 0f;

        // --- НОВЫЕ СТРОГИЕ КРИТЕРИИ ---
        int minTraps = 0, maxTraps = 100;
        bool requireGreedyWin = false;
        bool requireGreedyLoss = false;
        int minSolutionMoves = 0;
        int maxSolutionMoves = 9999;

        switch (difficulty)
        {
            case Difficulty.Easy:
                minTraps = 0; maxTraps = 2; // Мало тупиков
                minSolutionMoves = 0; maxSolutionMoves = 130; // Игра не должна быть бесконечной
                requireGreedyWin = true;    // Должен решаться автопилотом
                break;

            case Difficulty.Medium:
                minTraps = 3; maxTraps = 25;
                requireGreedyLoss = true;
                minSolutionMoves = 50;      // Средняя длина
                break;

            case Difficulty.Hard:
                // УЖЕСТОЧЕНИЕ:
                // 1. Минимум 7 тупиков (раньше было 4)
                // 2. Минимум 90 ходов решения (раньше было 45). Это отсеет "быстрые" победы.
                minTraps = 30; maxTraps = 99;
                requireGreedyLoss = true;
                minSolutionMoves = 100;
                break;
        }

        while (!done)
        {
            frameTimer.Restart();

            while (frameTimer.Elapsed.TotalMilliseconds < FRAME_BUDGET_MS)
            {
                attempts++;

                // 1. Конструктор (Создает структуру)
                Deal candidate = CreateConstructedDeal(difficulty);

                // 2. Жадный бот (Быстрый фильтр)
                bool greedyWin = KlondikeSolver.IsSolvableByGreedy(candidate, param);

                bool failsGreedy = (requireGreedyWin && !greedyWin);
                bool failsAntiGreedy = (requireGreedyLoss && greedyWin);

                // ОПТИМИЗАЦИЯ: Если у нас уже есть хороший Fallback, пропускаем плохих кандидатов
                if (bestFallback != null && (failsGreedy || failsAntiGreedy)) continue;

                // 3. Полный Солвер (Тяжелый анализ)
                SolverResult result = KlondikeSolver.Solve(candidate, param);

                if (result.IsSolved)
                {
                    int traps = result.MovesCount;
                    int moves = result.StockPasses; // Длина решения (ходов)

                    bool matchesTraps = (traps >= minTraps && traps <= maxTraps);
                    bool matchesLength = (moves >= minSolutionMoves && moves <= maxSolutionMoves);

                    // --- СИСТЕМА ОЦЕНКИ (SCORING) ---
                    // Начисляем очки, чтобы при таймауте выбрать САМЫЙ сложный из найденных (для Hard)
                    // или САМЫЙ простой (для Easy).
                    int score = 0;

                    if (difficulty == Difficulty.Hard)
                    {
                        // Для Харда: чем больше тупиков и ходов, тем лучше
                        score += (traps * 10);
                        score += moves;
                        if (!failsAntiGreedy) score += 100; // Жадный проиграл - это база
                    }
                    else if (difficulty == Difficulty.Easy)
                    {
                        // Для Изи: чем меньше тупиков и ходов, тем лучше
                        score += (100 - traps * 10);
                        score += (200 - moves);
                        if (!failsGreedy) score += 500; // Жадный выиграл - это главное
                    }
                    else // Medium
                    {
                        // Для Медиума ищем баланс (ближе к цели)
                        score += 100 - Math.Abs(traps - 5) * 10;
                        score += 100 - Math.Abs(moves - 60);
                        if (!failsAntiGreedy) score += 100;
                    }

                    // Обновляем Fallback, если этот расклад лучше предыдущего
                    if (bestFallback == null || score > bestFallbackScore)
                    {
                        bestFallback = candidate;
                        bestFallbackScore = score;
                    }

                    // ИДЕАЛ: Полное совпадение всех параметров
                    if (matchesTraps && matchesLength && !failsGreedy && !failsAntiGreedy)
                    {
                        validDeal = candidate;
                        UnityEngine.Debug.Log($"<color=green>[Gen] PERFECT {difficulty}! Traps: {traps}, Moves: {moves}. Att: {attempts}</color>");
                        done = true;
                        break;
                    }
                }
            }

            if (!done)
            {
                totalTimeElapsed += Time.unscaledDeltaTime;
                if (totalTimeElapsed > GLOBAL_TIMEOUT_SEC)
                {
                    done = true;

                    if (bestFallback != null)
                    {
                        validDeal = bestFallback;
                        // Выводим Score, чтобы понимать качество
                        UnityEngine.Debug.LogWarning($"[Gen] Timeout. Fallback Score: {bestFallbackScore}. Attempts: {attempts}");
                    }
                    else
                    {
                        // Крайний случай
                        validDeal = CreateConstructedDeal(Difficulty.Easy);
                        UnityEngine.Debug.LogError($"[Gen] FAIL. No solvable deals. Returning unchecked.");
                    }
                }
                yield return null;
            }
        }

        onComplete?.Invoke(validDeal, null);
    }

    // --- УЛУЧШЕННЫЙ КОНСТРУКТОР ---
    private Deal CreateConstructedDeal(Difficulty difficulty)
    {
        List<CardModel> deck = new List<CardModel>();
        foreach (Suit s in Enum.GetValues(typeof(Suit))) for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));

        CardModel[] finalLayout = new CardModel[52];
        bool[] occupied = new bool[52];
        System.Random rng = new System.Random();

        var aces = deck.Where(c => c.rank == 1).ToList();
        var twos = deck.Where(c => c.rank == 2).ToList();
        var kings = deck.Where(c => c.rank == 13).ToList();

        // Разделяем "Середину" на две части для Харда
        var lowMids = deck.Where(c => c.rank >= 3 && c.rank <= 7).ToList();  // 3,4,5,6,7 (Нужны для постройки базы)
        var highMids = deck.Where(c => c.rank >= 8 && c.rank <= 12).ToList(); // 8,9,10,J,Q (Нужны для начала цепочек)

        // Объединяем их обратно для Easy/Medium, чтобы не ломать логику
        var allOthers = new List<CardModel>();
        allOthers.AddRange(lowMids);
        allOthers.AddRange(highMids);

        Shuffle(aces, rng); Shuffle(twos, rng); Shuffle(kings, rng);
        Shuffle(lowMids, rng); Shuffle(highMids, rng); Shuffle(allOthers, rng);

        if (difficulty == Difficulty.Easy)
        {
            // EASY: Всё доступно
            PlaceCardsInZone(finalLayout, occupied, aces, 26, 51, rng);
            PlaceCardsInZone(finalLayout, occupied, twos, 26, 51, rng);
            PlaceCardsInZone(finalLayout, occupied, kings, 0, 8, rng, true); // Короли пониже
            PlaceCardsInZone(finalLayout, occupied, allOthers, 0, 51, rng);
        }
        else if (difficulty == Difficulty.Hard)
        {
            // HARD: 
            // 1. Тузы на самое дно (0..6)
            PlaceCardsInZone(finalLayout, occupied, aces, 0, 6, rng);

            // 2. Двойки чуть выше (7..15)
            PlaceCardsInZone(finalLayout, occupied, twos, 7, 15, rng);

            // 3. Мелкие карты (3-7) закапываем в середину (10..25), 
            // чтобы до них было трудно добраться через Королей
            PlaceCardsInZone(finalLayout, occupied, lowMids, 10, 25, rng, true);

            // 4. Короли блокируют верхушки (21..27)
            PlaceCardsInZone(finalLayout, occupied, kings, 21, 27, rng, true);

            // 5. Остальное (8-Q) заполняет дыры
            PlaceCardsInZone(finalLayout, occupied, highMids, 0, 51, rng);
        }
        else // Medium
        {
            // MEDIUM: Смешанно
            var hardAces = aces.Take(2).ToList();
            var easyAces = aces.Skip(2).ToList();
            PlaceCardsInZone(finalLayout, occupied, hardAces, 0, 10, rng);
            PlaceCardsInZone(finalLayout, occupied, easyAces, 20, 51, rng);
            PlaceCardsInZone(finalLayout, occupied, kings, 0, 51, rng);
            PlaceCardsInZone(finalLayout, occupied, allOthers, 0, 51, rng);
        }

        return AssembleDeal(finalLayout);
    }

    private void PlaceCardsInZone(CardModel[] layout, bool[] occupied, List<CardModel> cards, int minIdx, int maxIdx, System.Random rng, bool overflowToStock = false)
    {
        foreach (var card in cards)
        {
            List<int> freeSlots = new List<int>();
            for (int i = minIdx; i <= maxIdx; i++) if (i < 52 && !occupied[i]) freeSlots.Add(i);
            if (freeSlots.Count > 0)
            {
                int slot = freeSlots[rng.Next(freeSlots.Count)]; layout[slot] = card; occupied[slot] = true;
            }
            else if (overflowToStock)
            {
                for (int i = 28; i < 52; i++) if (!occupied[i]) { layout[i] = card; occupied[i] = true; break; }
            }
            else
            {
                for (int i = 0; i < 52; i++) if (!occupied[i]) { layout[i] = card; occupied[i] = true; break; }
            }
        }
    }

    private Deal AssembleDeal(CardModel[] layout)
    {
        Deal d = new Deal(); int idx = 0;
        for (int i = 0; i < 7; i++) { for (int j = 0; j <= i; j++) { bool isTop = (j == i); if (idx < 52) d.tableau[i].Add(new CardInstance(layout[idx], isTop)); idx++; } }
        while (idx < 52) { d.stock.Push(new CardInstance(layout[idx], false)); idx++; }
        return d;
    }
    private void Shuffle<T>(List<T> list, System.Random rng) { int n = list.Count; while (n > 1) { n--; int k = rng.Next(n + 1); T value = list[k]; list[k] = list[n]; list[n] = value; } }
}