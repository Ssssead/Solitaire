using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

public class YukonGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Yukon;

    [Header("Generation Strategy")]
    public bool calibrationMode = true;

    [Header("Optimization")]
    [Range(1, 50)]
    public float frameBudgetMs = 32.0f;

    private delegate void HardTrap(Deal d, System.Random rng, int variantParam);

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        // АБСОЛЮТНО НЕЗАВИСИМЫЕ МАРШРУТЫ
        if (param == 0) // CLASSIC
        {
            if (difficulty == Difficulty.Easy) yield return StartCoroutine(GenerateEasyClassicDeal(onComplete, param));
            else if (difficulty == Difficulty.Medium) yield return StartCoroutine(GenerateMediumClassicDeal(onComplete, param));
            else yield return StartCoroutine(GenerateHardClassicDeal(onComplete, param));
        }
        else // RUSSIAN (Строгая масть)
        {
            if (difficulty == Difficulty.Easy) yield return StartCoroutine(GenerateEasyRussianDeal(onComplete, param));
            else if (difficulty == Difficulty.Medium) yield return StartCoroutine(GenerateMediumRussianDeal(onComplete, param));
            else yield return StartCoroutine(GenerateHardRussianDeal(onComplete, param));
        }
    }

    // ==============================================================================================
    // БЛОК 1: КЛАССИЧЕСКИЙ ЮКОН (Чередование цветов)
    // ==============================================================================================

    private IEnumerator GenerateEasyClassicDeal(Action<Deal, DealMetrics> onComplete, int param)
    {
        if (calibrationMode) UnityEngine.Debug.Log("[YukonGen] Запуск поиска натурального Easy Classic...");

        Deal validDeal = null;
        int totalAttempts = 0;
        bool found = false;
        DealMetrics metrics = new DealMetrics();
        Stopwatch frameWatch = new Stopwatch();

        System.Random rng = new System.Random();

        while (!found)
        {
            totalAttempts++;
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }
            else if (!frameWatch.IsRunning) frameWatch.Start();

            // 1. Создаем натуральный случайный расклад (с мягкой балансировкой)
            Deal candidate = CreateNaturalBalancedDeal(rng);

            // 2. Симулятор "Слепого Игрока" (Монте-Карло)
            float winRate = 0f;
            yield return StartCoroutine(YukonMonteCarlo.EvaluateWinRateAsync(candidate, param, 40, frameBudgetMs, frameWatch, result => winRate = result));
            frameWatch.Restart();

            // 3. Фильтр сложности: Если случайные ходы побеждают хотя бы в 55% случаев — это отличный легкий уровень!
            if (winRate >= 0.55f)
            {
                // 4. Запускаем Умного Солвера только для того, чтобы получить красивую цифру минимальных ходов
                YukonSolver.ExtendedSolverResult mathResult = new YukonSolver.ExtendedSolverResult();
                yield return StartCoroutine(YukonSolver.SolveAsync(candidate, param, frameBudgetMs, mathResult, 5000));

                if (mathResult.IsSolved)
                {
                    validDeal = candidate;
                    metrics.Solved = true;
                    metrics.MoveEstimate = mathResult.SolutionMoves;
                    metrics.HiddenCards = 21;
                    found = true;

                    if (calibrationMode) UnityEngine.Debug.Log($"<color=green>[EASY CLASSIC FOUND] WinRate слепого бота: {winRate * 100}%. Тупики AI: {mathResult.Backtracks}. Попыток генерации: {totalAttempts}</color>");
                }
            }

            if (calibrationMode && totalAttempts % 15 == 0)
                UnityEngine.Debug.Log($"[YukonGen] Поиск Easy... Попыток: {totalAttempts}. Последний WinRate: {Math.Round(winRate * 100, 1)}%");
        }

        onComplete?.Invoke(validDeal, metrics);
    }
    private Deal CreateEasyClassicBase(System.Random rng)
    {
        var deck = GetShuffledDeck(rng);
        List<CardModel> faceDownPool = deck.Take(21).ToList();
        List<CardModel> faceUpPool = deck.Skip(21).ToList();

        // ЖЕСТКИЙ КОНТРОЛЬ СТАРТА: Гарантируем, что начало игры будет плавным
        BalancePool(faceDownPool, faceUpPool, c => c.rank == 1, 1); // Максимум 1 Туз в закрытых
        BalancePool(faceDownPool, faceUpPool, c => c.rank == 2, 2); // Максимум 2 Двойки
        BalancePool(faceDownPool, faceUpPool, c => c.rank == 13, 1); // Максимум 1 Король

        ShuffleList(faceDownPool, rng);
        ShuffleList(faceUpPool, rng);

        Deal d = new Deal(); int downIdx = 0, upIdx = 0;
        for (int col = 0; col < 7; col++)
        {
            d.tableau.Add(new List<CardInstance>());
            int downCount = col; int upCount = (col == 0) ? 1 : 5;
            for (int r = 0; r < downCount; r++) d.tableau[col].Add(new CardInstance(faceDownPool[downIdx++], false));
            for (int r = 0; r < upCount; r++) d.tableau[col].Add(new CardInstance(faceUpPool[upIdx++], true));
        }
        return d;
    }
    private IEnumerator GenerateMediumClassicDeal(Action<Deal, DealMetrics> onComplete, int param)
    {
        if (calibrationMode) UnityEngine.Debug.Log("[YukonGen] Запуск поиска Medium Classic...");

        Deal validDeal = null;
        int totalAttempts = 0;
        bool found = false;
        DealMetrics metrics = new DealMetrics();
        Stopwatch frameWatch = new Stopwatch();
        System.Random rng = new System.Random();

        List<HardTrap> allTraps = new List<HardTrap>
        {
            Trap_DeepBuriedAnchor,
            Trap_IllusionOfConsolidation,
            Trap_RoyalBlockade,
            Trap_MonochromeJam,
            Trap_Ouroboros,
            Trap_PoisonedTail,
            Trap_DeadEndHook,
            Trap_ScatteredFamily,
            Trap_TopHeavyBoard,
            Trap_FalseKing
        };

        while (!found)
        {
            totalAttempts++;
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }
            else if (!frameWatch.IsRunning) frameWatch.Start();

            // 1. Создаем сбалансированную базу (чтобы старт не был глухим, как в Hard)
            Deal candidate = CreateNaturalBalancedDeal(rng);

            // 2. Внедряем 1 или 2 случайные ловушки (создаем умеренную когнитивную вязкость)
            int numTraps = rng.Next(1, 3);
            var selectedTraps = allTraps.OrderBy(x => rng.Next()).Take(numTraps).ToList();
            foreach (var trap in selectedTraps)
            {
                trap(candidate, rng, param);
            }

            // 3. Монте-Карло: Слепой бот должен иногда выигрывать, но чаще проигрывать
            float winRate = 0f;
            yield return StartCoroutine(YukonMonteCarlo.EvaluateWinRateAsync(candidate, param, 30, frameBudgetMs, frameWatch, result => winRate = result));
            frameWatch.Restart();

            // WinRate от 5% до 45% (примерно 2-13 случайных побед из 30)
            if (winRate >= 0.05f && winRate <= 0.45f)
            {
                // 4. Умный Солвер: Убеждаемся, что алгоритм должен немного подумать
                YukonSolver.ExtendedSolverResult mathResult = new YukonSolver.ExtendedSolverResult();
                yield return StartCoroutine(YukonSolver.SolveAsync(candidate, param, frameBudgetMs, mathResult, 15000));

                if (mathResult.IsSolved)
                {
                    // Требуем от 20 до 900 тупиков (чтобы отсеять случайные слишком легкие или слишком сложные расклады)
                    if (mathResult.Backtracks >= 20 && mathResult.Backtracks <= 900)
                    {
                        validDeal = candidate;
                        metrics.Solved = true;
                        metrics.MoveEstimate = mathResult.SolutionMoves;
                        metrics.HiddenCards = 21;
                        found = true;
                        if (calibrationMode) UnityEngine.Debug.Log($"<color=yellow>[MEDIUM CLASSIC FOUND] Идеальный баланс. WinRate: {Math.Round(winRate * 100, 1)}%. Тупиков AI: {mathResult.Backtracks}. Ловушек: {numTraps}. Попыток: {totalAttempts}</color>");
                    }
                }
            }

            if (calibrationMode && totalAttempts % 15 == 0)
                UnityEngine.Debug.Log($"[YukonGen] Поиск Medium... Попыток: {totalAttempts}. Последний WinRate: {Math.Round(winRate * 100, 1)}%");
        }

        onComplete?.Invoke(validDeal, metrics);
    }

    private IEnumerator GenerateHardClassicDeal(Action<Deal, DealMetrics> onComplete, int param)
    {
        if (calibrationMode) UnityEngine.Debug.Log("[YukonGen] Запуск поиска Hard Classic (Ужесточенный режим)...");

        Deal validDeal = null;
        int totalAttempts = 0;
        bool found = false;
        DealMetrics metrics = new DealMetrics();
        Stopwatch frameWatch = new Stopwatch();
        System.Random rng = new System.Random();

        List<HardTrap> allTraps = new List<HardTrap>
        {
            Trap_DeepBuriedAnchor,
            Trap_IllusionOfConsolidation,
            Trap_RoyalBlockade,
            Trap_MonochromeJam,
            Trap_Ouroboros,
            Trap_PoisonedTail,
            Trap_DeadEndHook,
            Trap_ScatteredFamily,
            Trap_TopHeavyBoard,
            Trap_FalseKing
        };

        while (!found)
        {
            totalAttempts++;
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); }
            else if (!frameWatch.IsRunning) frameWatch.Start();

            // 1. Натуральный рандом
            Deal candidate = CreateCompletelyRandomDeal(rng);

            // 2. УЖЕСТОЧЕНИЕ: Внедряем 4 ловушки вместо 3-х
            var selectedTraps = allTraps.OrderBy(x => rng.Next()).Take(4).ToList();
            foreach (var trap in selectedTraps)
            {
                trap(candidate, rng, param);
            }

            // 3. Монте-Карло: Слепой бот всё еще должен страдать (WinRate <= 4%)
            float winRate = 0f;
            yield return StartCoroutine(YukonMonteCarlo.EvaluateWinRateAsync(candidate, param, 25, frameBudgetMs, frameWatch, result => winRate = result));
            frameWatch.Restart();

            if (winRate <= 0.04f)
            {
                // 4. Умный Солвер: Расклад должен быть решаемым, но очень "вязким"
                YukonSolver.ExtendedSolverResult mathResult = new YukonSolver.ExtendedSolverResult();
                yield return StartCoroutine(YukonSolver.SolveAsync(candidate, param, frameBudgetMs, mathResult, 30000));

                if (mathResult.IsSolved)
                {
                    // УЖЕСТОЧЕНИЕ: Требуем больше 1000 тупиков ИИ и минимум 105 ходов идеального пути!
                    if (mathResult.Backtracks > 1000 && mathResult.SolutionMoves > 105)
                    {
                        validDeal = candidate;
                        metrics.Solved = true;
                        metrics.MoveEstimate = mathResult.SolutionMoves;
                        metrics.HiddenCards = 21;
                        found = true;
                        if (calibrationMode) UnityEngine.Debug.Log($"<color=red>[HARD CLASSIC FOUND] Идеальная жесть. WinRate: {winRate * 100}%. Тупиков AI: {mathResult.Backtracks}. Идеальных ходов: {mathResult.SolutionMoves}. Попыток: {totalAttempts}</color>");
                    }
                }
            }

            if (calibrationMode && totalAttempts % 10 == 0)
                UnityEngine.Debug.Log($"[YukonGen] Поиск Hard... Попыток: {totalAttempts} (Фильтр >1000 тупиков отсеивает слабаков)");
        }

        onComplete?.Invoke(validDeal, metrics);
    }

    // ==============================================================================================
    // БЛОК 2: РУССКИЙ ЮКОН (Строгая масть)
    // ==============================================================================================

    private IEnumerator GenerateEasyRussianDeal(Action<Deal, DealMetrics> onComplete, int param)
    {
        if (calibrationMode) UnityEngine.Debug.Log("[YukonGen] Запуск БЫСТРОГО поиска Easy Russian...");
        Deal validDeal = null; int totalAttempts = 0; bool found = false; DealMetrics metrics = new DealMetrics();
        Stopwatch frameWatch = new Stopwatch(); System.Random rng = new System.Random();

        while (!found)
        {
            totalAttempts++;
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); } else if (!frameWatch.IsRunning) frameWatch.Start();

            Deal candidate = CreateEasyRussianBase(rng);
            FixRussianDeadlocks(candidate, rng);

            YukonSolver.ExtendedSolverResult mathResult = new YukonSolver.ExtendedSolverResult();
            yield return StartCoroutine(YukonSolver.SolveAsync(candidate, param, frameBudgetMs, mathResult, 5000));
            frameWatch.Restart();

            if (mathResult.IsSolved)
            {
                if (mathResult.Backtracks <= 40 && mathResult.SolutionMoves <= 115)
                {
                    validDeal = candidate; metrics.Solved = true; metrics.MoveEstimate = mathResult.SolutionMoves; metrics.HiddenCards = 21; found = true;
                    if (calibrationMode) UnityEngine.Debug.Log($"<color=green>[EASY RUSSIAN FOUND] Тупиков AI: {mathResult.Backtracks}. Ходов: {mathResult.SolutionMoves}. Попыток: {totalAttempts}</color>");
                }
            }
        }
        onComplete?.Invoke(validDeal, metrics);
    }

    private IEnumerator GenerateMediumRussianDeal(Action<Deal, DealMetrics> onComplete, int param)
    {
        if (calibrationMode) UnityEngine.Debug.Log("[YukonGen] Поиск Medium Russian...");
        Deal validDeal = null; int totalAttempts = 0; bool found = false; DealMetrics metrics = new DealMetrics();
        Stopwatch frameWatch = new Stopwatch(); System.Random rng = new System.Random();

        List<HardTrap> russianTraps = new List<HardTrap> {
            RussianTrap_FoundationInversion,
            RussianTrap_KingStarvation,
            RussianTrap_DeepDeadZone
        };

        while (!found)
        {
            totalAttempts++;
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); } else if (!frameWatch.IsRunning) frameWatch.Start();

            // ИСПРАВЛЕНИЕ: Используем структурную базу вместо чистого рандома!
            // База гарантирует решаемость, а ловушки создадут сложность.
            Deal candidate = CreateEasyRussianBase(rng);
            FixRussianDeadlocks(candidate, rng);

            var selectedTraps = russianTraps.OrderBy(x => rng.Next()).Take(rng.Next(1, 3)).ToList();
            foreach (var trap in selectedTraps) trap(candidate, rng, param);

            YukonSolver.ExtendedSolverResult mathResult = new YukonSolver.ExtendedSolverResult();
            yield return StartCoroutine(YukonSolver.SolveAsync(candidate, param, frameBudgetMs, mathResult, 15000));
            frameWatch.Restart();

            // Чуть расширили окно тупиков (от 20 до 400), чтобы генератор не отбрасывал хорошие средние уровни
            if (mathResult.IsSolved && mathResult.Backtracks > 20 && mathResult.Backtracks <= 400)
            {
                validDeal = candidate; metrics.Solved = true; metrics.MoveEstimate = mathResult.SolutionMoves; metrics.HiddenCards = 21; found = true;
                if (calibrationMode) UnityEngine.Debug.Log($"<color=yellow>[MEDIUM RUSSIAN FOUND] Тупиков AI: {mathResult.Backtracks}. Ходов: {mathResult.SolutionMoves}. Попыток: {totalAttempts}</color>");
            }

            if (calibrationMode && totalAttempts % 50 == 0) UnityEngine.Debug.Log($"[YukonGen] Medium Russian: {totalAttempts} попыток...");
        }
        onComplete?.Invoke(validDeal, metrics);
    }

    private IEnumerator GenerateHardRussianDeal(Action<Deal, DealMetrics> onComplete, int param)
    {
        if (calibrationMode) UnityEngine.Debug.Log("[YukonGen] Поиск НАСТОЯЩЕГО Hard Russian...");
        Deal validDeal = null; int totalAttempts = 0; bool found = false; DealMetrics metrics = new DealMetrics();
        Stopwatch frameWatch = new Stopwatch(); System.Random rng = new System.Random();

        List<HardTrap> russianTraps = new List<HardTrap> {
            RussianTrap_FoundationInversion,
            RussianTrap_KingStarvation,
            RussianTrap_DeepDeadZone
        };

        while (!found)
        {
            totalAttempts++;
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs) { yield return null; frameWatch.Restart(); } else if (!frameWatch.IsRunning) frameWatch.Start();

            // ИСПРАВЛЕНИЕ: Используем структурную базу для Харда тоже!
            Deal candidate = CreateEasyRussianBase(rng);
            FixRussianDeadlocks(candidate, rng);

            // Накидываем все 3 жестокие топологические ловушки
            var selectedTraps = russianTraps.OrderBy(x => rng.Next()).Take(3).ToList();
            foreach (var trap in selectedTraps) trap(candidate, rng, param);

            YukonSolver.ExtendedSolverResult mathResult = new YukonSolver.ExtendedSolverResult();
            yield return StartCoroutine(YukonSolver.SolveAsync(candidate, param, frameBudgetMs, mathResult, 25000));
            frameWatch.Restart();

            if (mathResult.IsSolved)
            {
                if (mathResult.Backtracks >= 300 && mathResult.SolutionMoves > 115)
                {
                    validDeal = candidate; metrics.Solved = true; metrics.MoveEstimate = mathResult.SolutionMoves; metrics.HiddenCards = 21; found = true;
                    if (calibrationMode) UnityEngine.Debug.Log($"<color=red>[HARD RUSSIAN FOUND] Идеальная жесть. Тупиков AI: {mathResult.Backtracks}. Ходов: {mathResult.SolutionMoves}. Попыток: {totalAttempts}</color>");
                }
            }

            if (calibrationMode && totalAttempts % 50 == 0) UnityEngine.Debug.Log($"[YukonGen] Hard Russian: {totalAttempts} попыток... Ищем сложный, но проходимый лабиринт.");
        }
        onComplete?.Invoke(validDeal, metrics);
    }

    // ==============================================================================================
    // БИБЛИОТЕКИ ЛОВУШЕК И ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ (ОБЩИЕ)
    // ==============================================================================================
    private Deal CreateEasyRussianBase(System.Random rng)
    {
        var deck = GetShuffledDeck(rng);
        var highs = deck.Where(c => c.rank >= 7).ToList();
        var lows = deck.Where(c => c.rank < 7).ToList();

        List<CardModel> faceDownPool = new List<CardModel>();
        List<CardModel> faceUpPool = new List<CardModel>();

        int randomLowsToBury = rng.Next(2, 5);
        for (int i = 0; i < randomLowsToBury; i++) { faceDownPool.Add(lows[0]); lows.RemoveAt(0); }
        while (faceDownPool.Count < 21) { faceDownPool.Add(highs[0]); highs.RemoveAt(0); }

        faceUpPool.AddRange(lows);
        faceUpPool.AddRange(highs);

        ShuffleList(faceDownPool, rng);
        ShuffleList(faceUpPool, rng);

        Deal d = new Deal(); int downIdx = 0, upIdx = 0;
        for (int col = 0; col < 7; col++)
        {
            d.tableau.Add(new List<CardInstance>());
            int downCount = col; int upCount = (col == 0) ? 1 : 5;
            for (int r = 0; r < downCount; r++) d.tableau[col].Add(new CardInstance(faceDownPool[downIdx++], false));
            for (int r = 0; r < upCount; r++) d.tableau[col].Add(new CardInstance(faceUpPool[upIdx++], true));
        }
        return d;
    }

    private void FixRussianDeadlocks(Deal d, System.Random rng)
    {
        for (int pass = 0; pass < 10; pass++)
        {
            bool changed = false;
            for (int c = 0; c < 7; c++)
            {
                var col = d.tableau[c];
                for (int i = 0; i < col.Count - 1; i++)
                {
                    for (int j = i + 1; j < col.Count; j++)
                    {
                        var lower = col[i].Card; var upper = col[j].Card;
                        if (lower.suit == upper.suit && lower.rank > upper.rank)
                        {
                            int swapC = rng.Next(7);
                            while (swapC == c) swapC = rng.Next(7);
                            int swapR = rng.Next(d.tableau[swapC].Count);

                            var temp = col[j].Card;
                            col[j].Card = d.tableau[swapC][swapR].Card;
                            d.tableau[swapC][swapR].Card = temp;
                            changed = true;
                        }
                    }
                }
            }
            if (!changed) break;
        }
    }

    private Deal CreateNaturalBalancedDeal(System.Random rng)
    {
        var deck = GetShuffledDeck(rng);
        List<CardModel> faceDownPool = deck.Take(21).ToList();
        List<CardModel> faceUpPool = deck.Skip(21).ToList();

        BalancePool(faceDownPool, faceUpPool, c => c.rank == 1, 2);
        BalancePool(faceDownPool, faceUpPool, c => c.rank == 13, 1);

        ShuffleList(faceDownPool, rng);
        ShuffleList(faceUpPool, rng);

        Deal d = new Deal(); int downIdx = 0, upIdx = 0;
        for (int col = 0; col < 7; col++)
        {
            d.tableau.Add(new List<CardInstance>());
            int downCount = col; int upCount = (col == 0) ? 1 : 5;
            for (int r = 0; r < downCount; r++) d.tableau[col].Add(new CardInstance(faceDownPool[downIdx++], false));
            for (int r = 0; r < upCount; r++) d.tableau[col].Add(new CardInstance(faceUpPool[upIdx++], true));
        }
        return d;
    }

    private void BalancePool(List<CardModel> source, List<CardModel> dest, Func<CardModel, bool> condition, int maxAllowed)
    {
        var targets = source.Where(condition).ToList();
        while (targets.Count > maxAllowed)
        {
            var cardToMove = targets[0]; source.Remove(cardToMove); targets.RemoveAt(0);

            // ИСПРАВЛЕНИЕ: Безопасный поиск индекса, защищающий от NullReferenceException
            int replaceIdx = dest.FindIndex(c => c.rank >= 4 && c.rank <= 10);
            if (replaceIdx == -1) replaceIdx = 0; // Фолбэк, если карт от 4 до 10 не осталось

            var replacement = dest[replaceIdx];
            dest.RemoveAt(replaceIdx);
            source.Add(replacement);
            dest.Add(cardToMove);
        }
    }

    // ЛОВУШКИ КЛАССИКИ
    private void Trap_DeepBuriedAnchor(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 1, rng.Next(4, 7), rng.Next(0, 2)); }
    private void Trap_IllusionOfConsolidation(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 1, 4, 3); SwapCardTo(d, c => c.rank == 13, 4, 4); SwapCardTo(d, c => c.rank == 12, 4, 5); SwapCardTo(d, c => c.rank == 11, 4, 6); }
    private void Trap_RoyalBlockade(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 13, 3, rng.Next(0, 3)); SwapCardTo(d, c => c.rank == 13, 4, rng.Next(0, 4)); SwapCardTo(d, c => c.rank == 13, 5, rng.Next(0, 5)); }
    private void Trap_MonochromeJam(Deal d, System.Random rng, int param)
    {
        for (int i = 0; i < 5; i++)
        {
            var blackOpen = FindCard(d, c => (c.suit == Suit.Spades || c.suit == Suit.Clubs) && IsFaceUp(d, c));
            var redClosed = FindCard(d, c => (c.suit == Suit.Hearts || c.suit == Suit.Diamonds) && !IsFaceUp(d, c));
            if (blackOpen.c != -1 && redClosed.c != -1) DoSwap(d, blackOpen.c, blackOpen.r, redClosed.c, redClosed.r);
        }
    }
    private void Trap_Ouroboros(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 8 && c.suit == Suit.Spades, 5, 5); SwapCardTo(d, c => c.rank == 6 && c.suit == Suit.Hearts, 5, 8); SwapCardTo(d, c => c.rank == 7 && c.suit == Suit.Hearts, 4, 0); }
    private void Trap_PoisonedTail(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank <= 2, 6, 6); }
    private void Trap_DeadEndHook(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 13, 1, 1); SwapCardTo(d, c => c.rank == 13, 2, 2); }
    private void Trap_ScatteredFamily(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 4 && c.suit == Suit.Hearts, 4, 0); SwapCardTo(d, c => c.rank == 5 && c.suit == Suit.Hearts, 5, 0); SwapCardTo(d, c => c.rank == 6 && c.suit == Suit.Hearts, 6, 0); }
    private void Trap_TopHeavyBoard(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 3, 4, 4); SwapCardTo(d, c => c.rank == 12, 4, 8); SwapCardTo(d, c => c.rank == 4, 5, 5); SwapCardTo(d, c => c.rank == 11, 5, 9); }
    private void Trap_FalseKing(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 13, 0, 0); }

    // ЛОВУШКИ РУССКОГО ЮКОНА
    private void RussianTrap_FoundationInversion(Deal d, System.Random rng, int param) { Suit trapSuit = (Suit)rng.Next(4); SwapCardTo(d, c => c.rank == 3 && c.suit == trapSuit, 5, 2); SwapCardTo(d, c => c.rank == 11 && c.suit == trapSuit, 5, 3); }
    private void RussianTrap_KingStarvation(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 1, 3, 3); SwapCardTo(d, c => c.rank == 13, 3, 4); SwapCardTo(d, c => c.rank == 1, 4, 4); SwapCardTo(d, c => c.rank == 13, 4, 5); }
    private void RussianTrap_DeepDeadZone(Deal d, System.Random rng, int param) { SwapCardTo(d, c => c.rank == 1, 6, 0); SwapCardTo(d, c => c.rank == 2, 6, 1); SwapCardTo(d, c => c.rank == 1, 5, 0); }

    private void SwapCardTo(Deal d, Func<CardModel, bool> predicate, int targetCol, int targetRow)
    {
        var loc = FindCard(d, predicate);
        // ИСПРАВЛЕНИЕ: Логика ИЛИ (||), чтобы карта могла свапнуться, если она не находится точно в целевой ячейке
        if (loc.c != -1 && (loc.c != targetCol || loc.r != targetRow))
            DoSwap(d, loc.c, loc.r, targetCol, targetRow);
    }

    private (int c, int r) FindCard(Deal d, Func<CardModel, bool> predicate)
    {
        for (int c = 0; c < 7; c++)
        {
            for (int r = 0; r < d.tableau[c].Count; r++) if (predicate(d.tableau[c][r].Card)) return (c, r);
        }
        return (-1, -1);
    }

    private void DoSwap(Deal d, int c1, int r1, int c2, int r2)
    {
        var temp = d.tableau[c1][r1].Card; d.tableau[c1][r1].Card = d.tableau[c2][r2].Card; d.tableau[c2][r2].Card = temp;
    }

    private bool IsFaceUp(Deal d, CardModel c)
    {
        for (int col = 0; col < 7; col++)
        {
            int downCount = col;
            for (int r = 0; r < d.tableau[col].Count; r++) if (d.tableau[col][r].Card.Equals(c)) return r >= downCount;
        }
        return false;
    }

    private List<CardModel> GetShuffledDeck(System.Random rng)
    {
        List<CardModel> deck = new List<CardModel>();
        foreach (Suit s in Enum.GetValues(typeof(Suit))) for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));
        ShuffleList(deck, rng); return deck;
    }

    private void ShuffleList<T>(List<T> list, System.Random rng)
    {
        int n = list.Count;
        while (n > 1) { n--; int k = rng.Next(n + 1); T value = list[k]; list[k] = list[n]; list[n] = value; }
    }

    private Deal CreateCompletelyRandomDeal(System.Random rng)
    {
        var deck = GetShuffledDeck(rng); Deal d = new Deal(); int deckIdx = 0;
        for (int col = 0; col < 7; col++)
        {
            d.tableau.Add(new List<CardInstance>());
            int downCount = col; int upCount = (col == 0) ? 1 : 5;
            for (int r = 0; r < downCount; r++) d.tableau[col].Add(new CardInstance(deck[deckIdx++], false));
            for (int r = 0; r < upCount; r++) d.tableau[col].Add(new CardInstance(deck[deckIdx++], true));
        }
        return d;
    }
}