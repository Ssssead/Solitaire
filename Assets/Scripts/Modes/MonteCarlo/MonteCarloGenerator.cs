using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using Random = UnityEngine.Random;

public class MonteCarloGenerator : BaseGenerator
{
    public override GameType GameType => GameType.MonteCarlo;

    [Header("Generation Settings")]
    [Tooltip("Сколько секунд искать самый сложный уровень")]
    public float hardDifficultySearchTime = 20f;

    [Header("WebGL Optimization")]
    private const float FRAME_BUDGET = 0.012f;
    private const float GLOBAL_TIMEOUT = 30f; // Абсолютный лимит времени

    // Лимиты сложности
    private const int EASY_MAX_BACKTRACKS = 2;
    private const int MEDIUM_MIN_BACKTRACKS = 15;
    private const int MEDIUM_MAX_BACKTRACKS = 100;
    private const int MEDIUM_TARGET = 50; // Идеальный средний уровень
    private const int HARD_MIN_BACKTRACKS = 500;

    private const int MAX_DFS_ITERATIONS = 4000;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, System.Action<Deal, DealMetrics> onComplete)
    {
        bool is8Ways = param != 0;
        string variantStr = is8Ways ? "8Ways" : "4Ways";

        Debug.Log($"<color=#00FFFF>[MonteCarlo Gen]</color> Запуск майнинга: <b>{difficulty}</b> ({variantStr})...");

        byte[] bestDeck = null;
        byte[] fallbackDeck = null;

        int bestFallbackScore = difficulty == Difficulty.Easy ? int.MaxValue : -1;
        int attempts = 0;

        float globalStartTime = Time.realtimeSinceStartup;
        float frameStartTime = Time.realtimeSinceStartup;

        int finalBacktracks = 0;

        while (true)
        {
            attempts++;
            byte[] candidateDeck = GenerateNaturalReverseDeck(is8Ways);

            if (candidateDeck != null)
            {
                int backtracks = EvaluateHumanDifficulty(candidateDeck, is8Ways);

                if (difficulty == Difficulty.Easy)
                {
                    if (backtracks < bestFallbackScore) { bestFallbackScore = backtracks; fallbackDeck = candidateDeck; }
                    // Если нашли идеальный легкий уровень (0-2 ошибки) - берем сразу
                    if (backtracks <= EASY_MAX_BACKTRACKS) { bestDeck = candidateDeck; finalBacktracks = backtracks; break; }
                }
                else if (difficulty == Difficulty.Medium)
                {
                    // Ищем уровень максимально близкий к MEDIUM_TARGET
                    if (bestFallbackScore == -1 || Mathf.Abs(backtracks - MEDIUM_TARGET) < Mathf.Abs(bestFallbackScore - MEDIUM_TARGET))
                    {
                        bestFallbackScore = backtracks;
                        fallbackDeck = candidateDeck;
                    }
                    // Для Medium требуем ИДЕАЛЬНОЕ попадание, либо ждем окончания времени (чтобы выбрать лучший фоллбэк)
                    if (backtracks == MEDIUM_TARGET)
                    {
                        bestDeck = candidateDeck; finalBacktracks = backtracks; break;
                    }
                }
                else if (difficulty == Difficulty.Hard)
                {
                    if (backtracks > bestFallbackScore)
                    {
                        bestFallbackScore = backtracks;
                        fallbackDeck = candidateDeck;

                        // Логируем каждый новый рекорд сложности в реальном времени
                        if (backtracks >= HARD_MIN_BACKTRACKS)
                        {
                            Debug.Log($"<color=orange>[MonteCarlo Gen]</color> {difficulty}: Найден новый хардкор! Отмен: {backtracks}");
                        }
                    }

                    // Выходим досрочно только если нашли АБСОЛЮТНЫЙ максимум
                    if (backtracks >= MAX_DFS_ITERATIONS)
                    {
                        bestDeck = candidateDeck; finalBacktracks = backtracks;
                        break;
                    }
                }
            }

            // Управление временем и кадрами
            if (Time.realtimeSinceStartup - frameStartTime > FRAME_BUDGET)
            {
                float elapsedGlobal = Time.realtimeSinceStartup - globalStartTime;
                bool shouldExit = false;

                // Периодический отчет в консоль, чтобы показать, что процесс идет
                if (attempts % 100 == 0)
                {
                    Debug.Log($"[MonteCarlo Gen] {difficulty} в процессе... Попыток: {attempts}. Лучший результат: {bestFallbackScore}");
                }

                if (difficulty == Difficulty.Hard && elapsedGlobal > hardDifficultySearchTime) shouldExit = true;
                if (difficulty != Difficulty.Hard && elapsedGlobal > GLOBAL_TIMEOUT) shouldExit = true;

                if (shouldExit)
                {
                    if (bestDeck == null)
                    {
                        bestDeck = fallbackDeck;
                        finalBacktracks = bestFallbackScore;
                        Debug.Log($"<color=yellow>[MonteCarlo Gen]</color> Время вышло! Берем лучший фоллбэк с результатом: {finalBacktracks}");
                    }
                    break;
                }

                yield return null;
                frameStartTime = Time.realtimeSinceStartup;
            }
        }

        float generationTime = Time.realtimeSinceStartup - globalStartTime;

        Deal deal = new Deal();
        deal.tableau.Clear();
        deal.stock.Clear();

        string fullDeckLog = "";

        if (bestDeck != null)
        {
            List<CardModel> finalCards = DecodeDeck(bestDeck);

            for (int i = 0; i < finalCards.Count; i++)
            {
                fullDeckLog += GetShortCardString(finalCards[i]);
                if (i < finalCards.Count - 1) fullDeckLog += " ";
            }

            for (int i = 0; i < 25; i++)
            {
                var slotList = new List<CardInstance> { new CardInstance(finalCards[i], true) };
                deal.tableau.Add(slotList);
            }
            for (int i = 51; i >= 25; i--)
            {
                deal.stock.Push(new CardInstance(finalCards[i], true));
            }
        }

        Debug.Log($"<color=green><b>[MonteCarlo Gen] ГОТОВО!</b></color> Сложность: {difficulty} | Отмен: {finalBacktracks} | Попыток: {attempts} | Время: {generationTime:F2}с.");

        LogGenerationData(difficulty.ToString(), is8Ways, finalBacktracks, attempts, generationTime, fullDeckLog.Trim());

        DealMetrics metrics = new DealMetrics { Solved = true, MoveEstimate = 26 };
        onComplete?.Invoke(deal, metrics);
    }

    // --- КОНВЕРТЕР В КОРОТКИЙ ФОРМАТ ---
    private string GetShortCardString(CardModel card)
    {
        string rankStr = card.rank.ToString();
        if (card.rank == 1) rankStr = "A";
        else if (card.rank == 11) rankStr = "J";
        else if (card.rank == 12) rankStr = "Q";
        else if (card.rank == 13) rankStr = "K";

        string suitStr = "";
        switch (card.suit)
        {
            case Suit.Spades: suitStr = "S"; break;
            case Suit.Hearts: suitStr = "H"; break;
            case Suit.Clubs: suitStr = "C"; break;
            case Suit.Diamonds: suitStr = "D"; break;
        }

        return rankStr + suitStr;
    }

    // --- ЛОГИРОВАНИЕ ---
    private void LogGenerationData(string diff, bool is8Ways, int backtracks, int attempts, float time, string fullDeck)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "MonteCarloTrainingLog.csv");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "Date,Difficulty,Variant,BotBacktracks,Attempts,GenTimeSec,FullDeck\n");
            }

            string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{diff},{(is8Ways ? "8Ways" : "4Ways")},{backtracks},{attempts},{time:F2},{fullDeck}\n";
            File.AppendAllText(path, logLine);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MonteCarlo Log] Ошибка записи лога: {e.Message}");
        }
    }

    private byte[] GenerateNaturalReverseDeck(bool is8Ways)
    {
        List<byte[]> pairs = new List<byte[]>();
        for (int r = 0; r < 13; r++)
        {
            byte[] rankCards = new byte[] { (byte)(0 * 13 + r), (byte)(1 * 13 + r), (byte)(2 * 13 + r), (byte)(3 * 13 + r) };
            ShuffleArray(rankCards);
            pairs.Add(new byte[] { rankCards[0], rankCards[1] });
            pairs.Add(new byte[] { rankCards[2], rankCards[3] });
        }

        ShuffleList(pairs);
        List<byte> board = new List<byte>(52);

        for (int i = 0; i < 26; i++)
        {
            int maxIndex = Mathf.Min(24, board.Count + 1);
            List<Vector2Int> valid = new List<Vector2Int>(10);

            for (int p1 = 0; p1 <= maxIndex; p1++)
            {
                for (int p2 = p1 + 1; p2 <= maxIndex; p2++)
                {
                    if (IsAdjacentFast(p1, p2, is8Ways)) valid.Add(new Vector2Int(p1, p2));
                }
            }

            if (valid.Count == 0) return null;

            Vector2Int chosen = valid[Random.Range(0, valid.Count)];
            board.Insert(chosen.x, pairs[i][0]);
            board.Insert(chosen.y, pairs[i][1]);
        }

        return board.ToArray();
    }

    private int EvaluateHumanDifficulty(byte[] deck, bool is8Ways)
    {
        int backtracks = 0;
        byte[] initialBoard = new byte[52];
        System.Array.Copy(deck, initialBoard, 52);

        GreedyDFS(initialBoard, 25, 25, deck, is8Ways, ref backtracks);
        return backtracks;
    }

    private bool GreedyDFS(byte[] board, int boardCount, int stockIdx, byte[] fullDeck, bool is8Ways, ref int backtracks)
    {
        if (boardCount == 0) return true;
        if (backtracks >= MAX_DFS_ITERATIONS) return false;

        List<Vector2Int> validMoves = GetAllValidMatches(board, boardCount, is8Ways);

        if (validMoves.Count == 0)
        {
            backtracks++;
            return false;
        }

        validMoves.Sort((a, b) => GetHumanTemptationScore(b, board, boardCount, is8Ways).CompareTo(GetHumanTemptationScore(a, board, boardCount, is8Ways)));

        foreach (var move in validMoves)
        {
            byte[] nextBoard = new byte[52];
            System.Array.Copy(board, nextBoard, 52);
            int nextCount = boardCount;
            int nextStock = stockIdx;

            RemoveAt(nextBoard, ref nextCount, Mathf.Max(move.x, move.y));
            RemoveAt(nextBoard, ref nextCount, Mathf.Min(move.x, move.y));

            while (nextCount < 25 && nextStock < 52)
            {
                nextBoard[nextCount] = fullDeck[nextStock];
                nextCount++;
                nextStock++;
            }

            if (GreedyDFS(nextBoard, nextCount, nextStock, fullDeck, is8Ways, ref backtracks))
            {
                return true;
            }

            if (backtracks >= MAX_DFS_ITERATIONS) return false;
        }

        backtracks++;
        return false;
    }

    private List<Vector2Int> GetAllValidMatches(byte[] board, int boardCount, bool is8Ways)
    {
        List<Vector2Int> matches = new List<Vector2Int>();
        for (int i = 0; i < boardCount; i++)
        {
            for (int j = i + 1; j < boardCount; j++)
            {
                if (board[i] % 13 == board[j] % 13 && IsAdjacentFast(i, j, is8Ways))
                {
                    matches.Add(new Vector2Int(i, j));
                }
            }
        }
        return matches;
    }

    // НОВАЯ, ЧИСТАЯ КОГНИТИВНАЯ МОДЕЛЬ
    private int GetHumanTemptationScore(Vector2Int move, byte[] currentBoard, int boardCount, bool is8Ways)
    {
        int i = move.x;
        int j = move.y;
        int score = 0;

        // 1. Когнитивное искажение "Сверху-вниз"
        score += (25 - i) * 50;

        // 2. Визуальная иерархия паттернов
        int r1 = i / 5, c1 = i % 5;
        int r2 = j / 5, c2 = j % 5;
        int rd = Mathf.Abs(r1 - r2);
        int cd = Mathf.Abs(c1 - c2);

        if (rd == 0 && cd == 1) score += 300;       // Строгая горизонталь (максимально привлекательно)
        else if (rd == 1 && cd == 0) score += 200;  // Строгая вертикаль
        else score += 50;                           // Диагональ

        // 3. Иллюзия прогресса (Стяжка массива)
        int indexSpan = j - i;
        if (indexSpan > 1 && indexSpan <= 5)
        {
            score += (indexSpan * 15);
        }

        return score;
    }

    private void RemoveAt(byte[] array, ref int count, int index)
    {
        for (int i = index; i < count - 1; i++) array[i] = array[i + 1];
        count--;
    }

    private bool IsAdjacentFast(int p1, int p2, bool is8Ways)
    {
        int r1 = p1 / 5, c1 = p1 % 5;
        int r2 = p2 / 5, c2 = p2 % 5;
        int rd = r1 > r2 ? r1 - r2 : r2 - r1;
        int cd = c1 > c2 ? c1 - c2 : c2 - c1;

        if (is8Ways) return rd <= 1 && cd <= 1 && (rd > 0 || cd > 0);
        else return (rd == 1 && cd == 0) || (rd == 0 && cd == 1);
    }

    private void ShuffleArray(byte[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            int rnd = Random.Range(0, arr.Length);
            byte temp = arr[i]; arr[i] = arr[rnd]; arr[rnd] = temp;
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(0, list.Count);
            T temp = list[i]; list[i] = list[rnd]; list[rnd] = temp;
        }
    }

    private List<CardModel> DecodeDeck(byte[] deck)
    {
        List<CardModel> result = new List<CardModel>(52);
        for (int i = 0; i < 52; i++)
        {
            Suit s = (Suit)(deck[i] / 13);
            int r = (deck[i] % 13) + 1;
            result.Add(new CardModel { suit = s, rank = r });
        }
        return result;
    }
}