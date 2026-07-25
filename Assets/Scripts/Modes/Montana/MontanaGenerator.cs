using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics; // Обязательно для Stopwatch!

public class MontanaGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Montana;

    [Header("Optimization")]
    [Range(1, 16)]
    public float frameBudgetMs = 8.0f; // Бюджет времени на один кадр (8мс = стабильные 60+ FPS)

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal deal = new Deal();
        int maxReshuffles = (param == 1) ? 5 : 3;
        bool isHardEmpty = (param == 1);

        int masterSeed = 0;
        List<CardModel> bestDeck = null;
        int[] bestPlayerReqSuits = new int[4];
        List<int[]> bestTargets = new List<int[]>();
        List<int> bestGoldenSeeds = new List<int>();

        int attempts = 0;
        int maxAttempts = 3000; // Благодаря таймеру, можно безопасно проверять больше вариантов

        // --- СИСТЕМА БЮДЖЕТА ВРЕМЕНИ ИЗ КОСЫНКИ ---
        Stopwatch frameWatch = new Stopwatch();
        frameWatch.Start();

        while (attempts < maxAttempts)
        {
            attempts++;

            // Если вышли за рамки бюджета в этом кадре - ставим на паузу до следующего
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs)
            {
                yield return null;
                frameWatch.Restart();
            }

            masterSeed = UnityEngine.Random.Range(0, 7000000);
            System.Random rng = new System.Random(masterSeed);

            // 1. Создаем случайную стартовую доску
            List<CardModel> deck = new List<CardModel>();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                for (int rank = 1; rank <= 13; rank++) deck.Add(new CardModel(suit, rank));
            deck = deck.OrderBy(x => rng.Next()).ToList();

            int[] board = new int[56];
            int cardIndex = 0;
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 14; c++)
                {
                    int slot = c * 4 + r;
                    bool isEmpty = (!isHardEmpty && c == 0) || (isHardEmpty && c == 13);
                    if (!isEmpty && cardIndex < deck.Count)
                    {
                        CardModel card = deck[cardIndex++];
                        board[slot] = (int)card.suit * 13 + card.rank;
                    }
                }
            }

            // 2. Требования мастей для бота
            List<int> suits = new List<int> { 0, 1, 2, 3 };
            suits = suits.OrderBy(x => rng.Next()).ToList();
            int[] botSuits = suits.ToArray();

            // 3. Запускаем симуляцию игры (ПЕРЕДАЕМ ТУДА СЕКУНДОМЕР)
            List<int[]> outTargets;
            List<int> outGoldenSeeds;
            bool won = SimulateGame(board, botSuits, maxReshuffles, isHardEmpty, out outTargets, out outGoldenSeeds, frameWatch);

            // 4. Если бот победил - сохраняем
            if (won)
            {
                bestDeck = deck;
                bestTargets = outTargets;
                bestGoldenSeeds = outGoldenSeeds;

                for (int i = 0; i < 4; i++) bestPlayerReqSuits[i] = -1;
                if (difficulty == Difficulty.Hard) for (int i = 0; i < 4; i++) bestPlayerReqSuits[i] = botSuits[i];
                else if (difficulty == Difficulty.Medium) { bestPlayerReqSuits[0] = botSuits[0]; bestPlayerReqSuits[1] = botSuits[1]; }
                break;
            }
        }

        // --- FALLBACK: Если идеальный стол не найден, отдаем случайный ---
        if (bestDeck == null)
        {
            UnityEngine.Debug.LogWarning("[MontanaGenerator] Fast Solver didn't find perfect board. Using random fallback.");
            masterSeed = UnityEngine.Random.Range(0, 7000000);
            bestDeck = new List<CardModel>();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                for (int rank = 1; rank <= 13; rank++) bestDeck.Add(new CardModel(suit, rank));
            bestDeck = bestDeck.OrderBy(x => UnityEngine.Random.value).ToList();

            for (int i = 0; i < 4; i++) bestPlayerReqSuits[i] = -1;
            bestTargets = new List<int[]>();
            bestGoldenSeeds = new List<int>();
        }

        deal.tableau.Clear();
        for (int i = 0; i < 56; i++) deal.tableau.Add(new List<CardInstance>());

        int dIndex = 0;
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 14; c++)
            {
                int slot = c * 4 + r;
                bool isEmpty = (!isHardEmpty && c == 0) || (isHardEmpty && c == 13);
                if (!isEmpty && dIndex < bestDeck.Count) deal.tableau[slot].Add(new CardInstance(bestDeck[dIndex++], true));
            }
        }

        // Прячем решение
        MontanaPuzzleManager.EncodeSolution(masterSeed, bestPlayerReqSuits, bestTargets, bestGoldenSeeds, deal.stock);

        DealMetrics metrics = new DealMetrics { Solved = true, MoveEstimate = 50 };
        onComplete?.Invoke(deal, metrics);

        frameWatch.Stop();
        yield break;
    }

    private bool SimulateGame(int[] startBoard, int[] botSuits, int maxReshuffles, bool isHardEmpty, out List<int[]> outTargets, out List<int> outGoldenSeeds, Stopwatch frameWatch)
    {
        int[] board = new int[56];
        Array.Copy(startBoard, board, 56);
        outTargets = new List<int[]>();
        outGoldenSeeds = new List<int>();

        for (int reshuffle = 0; reshuffle <= maxReshuffles; reshuffle++)
        {
            bool moved = true;
            int safety = 0;

            while (moved && safety < 1000)
            {
                // Защита от зависаний ВНУТРИ симулятора
                if (frameWatch.ElapsedMilliseconds > frameBudgetMs) return false;

                safety++;
                moved = false;
                for (int i = 0; i < 56; i++)
                {
                    if (board[i] == 0)
                    {
                        int r = i % 4; int c = i / 4;
                        int nSuit = -1, nRank = -1;

                        if (c == 0) { nSuit = botSuits[r]; nRank = 1; }
                        else
                        {
                            int left = board[i - 4];
                            if (left != 0)
                            {
                                int lRank = (left - 1) % 13 + 1;
                                if (lRank < 13) { nSuit = (left - 1) / 13; nRank = lRank + 1; }
                            }
                        }

                        if (nRank != -1)
                        {
                            for (int j = 0; j < 56; j++)
                            {
                                if (board[j] != 0)
                                {
                                    int s = (board[j] - 1) / 13;
                                    int k = (board[j] - 1) % 13 + 1;
                                    if (s == nSuit && k == nRank)
                                    {
                                        board[i] = board[j]; board[j] = 0; moved = true; break;
                                    }
                                }
                            }
                        }
                    }
                    if (moved) break;
                }
            }

            int[] targets = new int[4];
            for (int s = 0; s < 4; s++)
            {
                int aceIdx = -1;
                for (int i = 0; i < 56; i++) if (board[i] == (s * 13 + 1)) { aceIdx = i; break; }

                if (aceIdx != -1 && aceIdx / 4 == 0)
                {
                    int r = aceIdx % 4; int len = 1;
                    for (int c = 1; c < 13; c++)
                    {
                        int card = board[c * 4 + r];
                        if (card != 0 && (card - 1) / 13 == s && (card - 1) % 13 + 1 == c + 1) len++;
                        else break;
                    }
                    targets[s] = len;
                }
                else
                {
                    targets[s] = 0;
                }
            }

            bool won = true;
            for (int s = 0; s < 4; s++) if (targets[s] < 13) won = false;

            if (won) return true;

            if (reshuffle > 0)
            {
                bool progressed = false;
                for (int s = 0; s < 4; s++) if (targets[s] > outTargets[reshuffle - 1][s]) progressed = true;
                if (!progressed) return false;
            }

            if (reshuffle >= maxReshuffles) return false;

            outTargets.Add(targets);

            List<int> unlocked = new List<int>();
            List<int> slots = new List<int>();

            for (int r = 0; r < 4; r++)
            {
                int validLength = 0;
                int first = board[r];
                if (first != 0 && (first - 1) % 13 + 1 == 1)
                {
                    int s = (first - 1) / 13; validLength = 1;
                    for (int c = 1; c < 13; c++)
                    {
                        int card = board[c * 4 + r];
                        if (card != 0 && (card - 1) / 13 == s && (card - 1) % 13 + 1 == c + 1) validLength++;
                        else break;
                    }
                }
                int gapCol = isHardEmpty ? 13 : validLength;
                for (int c = validLength; c < 14; c++)
                {
                    int idx = c * 4 + r;
                    if (c != gapCol) slots.Add(idx);
                    if (board[idx] != 0) unlocked.Add(board[idx]);
                }

                board[gapCol * 4 + r] = 0;
            }

            int goldenSeed = UnityEngine.Random.Range(0, 7000000);
            outGoldenSeeds.Add(goldenSeed);

            System.Random sRng = new System.Random(goldenSeed);
            Dictionary<int, int> weights = new Dictionary<int, int>();
            foreach (int c in unlocked) weights[c] = sRng.Next();
            unlocked = unlocked.OrderBy(c => weights[c]).ToList();

            int uIdx = 0;
            foreach (int s in slots) board[s] = (uIdx < unlocked.Count) ? unlocked[uIdx++] : 0;
        }
        return false;
    }
}