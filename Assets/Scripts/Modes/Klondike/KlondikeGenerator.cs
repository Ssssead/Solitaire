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
    private const float GLOBAL_TIMEOUT_SEC = 2.0f; // 2 секунды достаточно для поиска

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal validDeal = null;
        Deal bestFallback = null;
        int bestFallbackScore = -99999;

        int attempts = 0;
        bool done = false;

        Stopwatch frameTimer = new Stopwatch();
        float totalTimeElapsed = 0f;

        // --- КРИТЕРИИ ---
        int minDecisions = 0;
        int minMoves = 0;

        bool requireGreedyWin = false;
        bool requireGreedyLoss = false;

        switch (difficulty)
        {
            case Difficulty.Easy:
                // Easy: Жадный бот должен выигрывать.
                // Расклад должен выглядеть случайно, но быть проходимым "в лоб".
                minDecisions = 0;
                requireGreedyWin = true;
                break;

            case Difficulty.Medium:
                // Medium: Жадный бот может проиграть, но не обязательно.
                // Главное - длина игры 60+ ходов.
                minDecisions = 1;
                minMoves = 60;
                break;

            case Difficulty.Hard:
                // Hard: Жадный бот ОБЯЗАН проиграть.
                // Длина 90+, и хотя бы 4 момента выбора.
                minDecisions = 4; // Снизил с 6 до 4, чтобы убрать Timeout
                minMoves = 90;
                requireGreedyLoss = true;
                break;
        }

        while (!done)
        {
            frameTimer.Restart();

            while (frameTimer.Elapsed.TotalMilliseconds < FRAME_BUDGET_MS)
            {
                attempts++;

                // ИСПОЛЬЗУЕМ ЗОНАЛЬНУЮ ГЕНЕРАЦИЮ (Естественный вид)
                Deal candidate = CreateConstructedDeal(difficulty);

                // 1. Жадный бот (Быстрый тест)
                bool greedyWin = KlondikeSolver.IsSolvableByGreedy(candidate, param);

                // Строгие фильтры
                if (requireGreedyWin && !greedyWin) continue; // Easy обязан быть простым

                // Для Hard: Если жадный выиграл -> это мусор, даже в Fallback не берем
                if (requireGreedyLoss && greedyWin) continue;

                // 2. Полный Солвер
                var result = KlondikeSolver.SolveExtended(candidate, param);

                if (result.IsSolved)
                {
                    int decisions = result.Decisions;
                    int moves = result.Moves;
                    int traps = result.Traps;

                    bool okDecisions = decisions >= minDecisions;
                    bool okMoves = moves >= minMoves;

                    // --- ОЧКИ КАЧЕСТВА ---
                    int score = 0;

                    if (difficulty == Difficulty.Hard)
                    {
                        // Для Харда ценим запутанность
                        score += decisions * 100;
                        score += moves;
                        score += traps * 10;
                    }
                    else if (difficulty == Difficulty.Medium)
                    {
                        // Для Медиума ищем ~80 ходов
                        score += 1000 - Math.Abs(moves - 80) * 10;
                        if (!greedyWin) score += 200; // Бонус за сложность
                    }
                    else // Easy
                    {
                        // Для Изи: чем меньше ходов и решений, тем лучше
                        score += 1000 - moves;
                        score += 500 - decisions * 100;
                    }

                    // Сохраняем лучший
                    if (bestFallback == null || score > bestFallbackScore)
                    {
                        bestFallback = candidate;
                        bestFallbackScore = score;
                    }

                    // ИДЕАЛ
                    if (okDecisions && okMoves)
                    {
                        validDeal = candidate;
                        UnityEngine.Debug.Log($"<color=green>[Gen] PERFECT {difficulty}! Decis:{decisions} Moves:{moves} Traps:{traps}. Att:{attempts}</color>");
                        done = true;
                        break;
                    }
                }
            }

            if (!done)
            {
                totalTimeElapsed += Time.unscaledDeltaTime;

                // Если таймаут или слишком много попыток
                if (totalTimeElapsed > GLOBAL_TIMEOUT_SEC)
                {
                    done = true;

                    if (bestFallback != null)
                    {
                        validDeal = bestFallback;
                        // Предупреждение, но Fallback теперь качественный (лучший по Score)
                        UnityEngine.Debug.LogWarning($"[Gen] Timeout. Fallback used. Score: {bestFallbackScore}.");
                    }
                    else
                    {
                        // Если вообще ничего не нашли (редкость для Easy/Medium)
                        validDeal = CreateConstructedDeal(Difficulty.Easy);
                        UnityEngine.Debug.LogError("[Gen] FAIL. No solvable deals found. Returning unchecked Easy.");
                    }
                }
                yield return null;
            }
        }

        onComplete?.Invoke(validDeal, null);
    }

    // --- ЗОНАЛЬНЫЙ КОНСТРУКТОР (ЕСТЕСТВЕННЫЙ ВИД) ---
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

        // Остальные карты делим на группы для более умного распределения
        var lowMids = deck.Where(c => c.rank >= 3 && c.rank <= 7).ToList();
        var highMids = deck.Where(c => c.rank >= 8 && c.rank <= 12).ToList();
        var allOthers = new List<CardModel>(); allOthers.AddRange(lowMids); allOthers.AddRange(highMids);

        // ВАЖНО: Тщательно мешаем группы, чтобы не было "цепочек" как в прошлом варианте
        Shuffle(aces, rng); Shuffle(twos, rng); Shuffle(kings, rng);
        Shuffle(lowMids, rng); Shuffle(highMids, rng); Shuffle(allOthers, rng);

        if (difficulty == Difficulty.Easy)
        {
            // EASY: Максимальный рандом, но без глупостей.
            // Тузы и Двойки в доступной зоне (Сток или верх стопок)
            PlaceCardsInZone(finalLayout, occupied, aces, 20, 51, rng);
            PlaceCardsInZone(finalLayout, occupied, twos, 20, 51, rng);

            // Короли внизу (0-15), чтобы не блокировали
            PlaceCardsInZone(finalLayout, occupied, kings, 0, 15, rng, true);

            // Остальное рандомно
            PlaceCardsInZone(finalLayout, occupied, allOthers, 0, 51, rng);
        }
        else if (difficulty == Difficulty.Hard)
        {
            // HARD: "Блокировка"
            // 1. Тузы на самое дно (0-4)
            PlaceCardsInZone(finalLayout, occupied, aces, 0, 4, rng);

            // 2. Короли СТРОГО на верхушках (21-27) или в начале стока
            // Это создает пробки.
            PlaceCardsInZone(finalLayout, occupied, kings, 21, 30, rng);

            // 3. Низкие карты (3-5), нужные для старта, прячем в середину (10-20)
            // Чтобы до них добраться, нужно снять Королей.
            PlaceCardsInZone(finalLayout, occupied, lowMids, 10, 25, rng, true);

            // 4. Двойки тоже глубоко
            PlaceCardsInZone(finalLayout, occupied, twos, 5, 15, rng);

            // Остальное (8-Q) заполняет пустоты
            PlaceCardsInZone(finalLayout, occupied, highMids, 0, 51, rng);
        }
        else // Medium
        {
            // MEDIUM: Смешанно
            var hardAces = aces.Take(2).ToList();
            var easyAces = aces.Skip(2).ToList();

            PlaceCardsInZone(finalLayout, occupied, hardAces, 0, 10, rng);
            PlaceCardsInZone(finalLayout, occupied, easyAces, 20, 51, rng);

            // Короли где угодно, но избегая дна (0-5), чтобы расклад не был совсем уж простым
            PlaceCardsInZone(finalLayout, occupied, kings, 5, 51, rng);

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