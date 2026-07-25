using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class MontanaPuzzleManager : MonoBehaviour
{
    public int MasterSeed { get; private set; }
    public int[] RowSuitRequirements { get; private set; } = new int[4] { -1, -1, -1, -1 };

    public class Checkpoint
    {
        public int[] TargetSuitLengths = new int[4];
        public int GoldenSeed;
    }

    public List<Checkpoint> Checkpoints { get; private set; } = new List<Checkpoint>();
    public int CurrentCheckpointIndex { get; private set; } = 0;

    public void InitializeFromStock(Stack<CardInstance> stock, Difficulty diff, int maxReshuffles)
    {
        CurrentCheckpointIndex = 0;
        if (stock == null || stock.Count < 9)
        {
            Debug.LogError("No solution data found in cache! Please generate new deals.");
        }
        else
        {
            DecodeSolution(stock);
        }
        stock?.Clear();
    }

    public void ResetCheckpoints() => CurrentCheckpointIndex = 0;
    public void UndoCheckpoint() { if (CurrentCheckpointIndex > 0) CurrentCheckpointIndex--; }

    public int GetCurrentTargetLength(int suitIndex)
    {
        if (CurrentCheckpointIndex >= Checkpoints.Count) return 13;
        return Checkpoints[CurrentCheckpointIndex].TargetSuitLengths[suitIndex];
    }

    public int GetRowChainLength(MontanaPileManager pileManager, int row)
    {
        int length = 0; CardModel? expected = null;
        for (int c = 0; c < 13; c++)
        {
            var card = pileManager.GetSlot(row, c).GetTopCard(); if (card == null) break;
            if (c == 0)
            {
                if (card.cardModel.rank == 1) { length = 1; expected = new CardModel(card.cardModel.suit, 2); } else break;
            }
            else
            {
                if (expected.HasValue && card.cardModel.suit == expected.Value.suit && card.cardModel.rank == expected.Value.rank)
                {
                    length++; expected = new CardModel(card.cardModel.suit, card.cardModel.rank + 1);
                }
                else break;
            }
        }
        return length;
    }

    // ====================================================================
    // НОВАЯ СИСТЕМА ОРАКУЛА (ВЕРОЯТНОСТИ И КОНСТРУИРОВАНИЕ)
    // ====================================================================

    public Dictionary<string, int> GetReshuffleWeights(MontanaPileManager pileManager, int rMax, int rLeft, Difficulty diff, bool isHardMode, out bool isWinningRoll)
    {
        // 1. Прогресс Игрока (C)
        int C = 0;
        for (int r = 0; r < 4; r++) C += GetRowChainLength(pileManager, r);

        // 2. Прогресс Золотого пути (goldenC)
        int goldenC = 0;
        if (CurrentCheckpointIndex < Checkpoints.Count)
        {
            var cp = Checkpoints[CurrentCheckpointIndex];
            for (int i = 0; i < 4; i++) goldenC += cp.TargetSuitLengths[i];
        }
        else goldenC = 52;

        CurrentCheckpointIndex++; // Сдвигаем чекпоинт для следующих пересдач

        // 3. Формула вероятности
        float I = (goldenC <= 0) ? 1.0f : Mathf.Clamp01((float)C / goldenC);
        float pBase = ((float)(rMax - rLeft) / rMax) * (1f + ((float)C / 52f) * ((float)rLeft / rMax));

        float wDiff = 0.45f;
        if (diff == Difficulty.Medium) wDiff = 3.00f;
        if (diff == Difficulty.Hard) wDiff = 6.50f;

        float pWin = Mathf.Max(0f, pBase - wDiff * (1f - I));

        // 4. Детерминированный бросок кубика (Хэш стола)
        int boardHash = GetBoardHash(pileManager);
        System.Random rng = new System.Random(boardHash);
        float roll = (float)rng.NextDouble();

        isWinningRoll = roll <= pWin;

        // --- ЛОГИРОВАНИЕ В КОНСОЛЬ ---
        Debug.Log($"<color=cyan>[Оракул] Режим: {diff} | Осталось пересдач: {rLeft}/{rMax}</color>");
        Debug.Log($"<color=cyan>[Оракул] Карт игрока (C): {C} | Карт идеала (goldenC): {goldenC} | Индекс пути (I): {I:F2}</color>");
        Debug.Log($"<color=cyan>[Оракул] Базовый шанс: {pBase:P1} | Штраф за отклонение: {wDiff * (1f - I):P1}</color>");
        Debug.Log($"<color={(isWinningRoll ? "green" : "red")}><b>[Оракул] ИТОГОВЫЙ ШАНС ПОБЕДЫ: {pWin:P1} | Выпало: {roll:P1} -> {(isWinningRoll ? "УСПЕХ (Умный стол)" : "ПРОВАЛ (Случайный стол)")}</b></color>");

        // 5. Выдача расклада
        if (isWinningRoll)
            return FindSolvableWeights(pileManager, rng, isHardMode, rLeft, C);
        else
            return GenerateRandomWeights(pileManager, rng, isHardMode, diff); // <--- Добавили diff
    }

    private Dictionary<string, int> FindSolvableWeights(MontanaPileManager pileManager, System.Random baseRng, bool isHardMode, int rLeft, int currentC)
    {
        int[] startBoard = GetIntBoard(pileManager);
        GetShuffleData(startBoard, isHardMode, out List<int> unlocked, out List<int> slots);

        // Сколько карт алгоритм обязан собрать при проверке
        int targetC = (rLeft == 0) ? 52 : currentC + Mathf.Max(1, (52 - currentC) / (rLeft + 1));

        int[] bestBoardUnlocked = unlocked.ToArray();
        int bestC = 0;

        // Пытаемся найти решаемый расклад (Мгновенная симуляция)
        for (int attempt = 0; attempt < 500; attempt++)
        {
            List<int> testUnlocked = unlocked.OrderBy(x => baseRng.Next()).ToList();
            int[] testBoard = (int[])startBoard.Clone();

            int uIdx = 0;
            foreach (int s in slots) testBoard[s] = (uIdx < testUnlocked.Count) ? testUnlocked[uIdx++] : 0;

            int finalC = SimulateFast(testBoard);

            if (finalC > bestC)
            {
                bestC = finalC;
                bestBoardUnlocked = testUnlocked.ToArray();
            }

            if (finalC >= targetC) break; // Нашли отличный расклад!
        }

        Debug.Log($"<color=yellow>[Оракул] Скрытая симуляция: Требовалось собрать {targetC}, Алгоритм смог собрать {bestC}. Выдаем этот расклад.</color>");

        Dictionary<string, int> weights = new Dictionary<string, int>();
        for (int i = 0; i < bestBoardUnlocked.Length; i++)
        {
            int val = bestBoardUnlocked[i];
            Suit s = (Suit)((val - 1) / 13);
            int r = ((val - 1) % 13) + 1;
            weights[$"{s}_{r}"] = i;
        }
        return weights;
    }

    private Dictionary<string, int> GenerateRandomWeights(MontanaPileManager pileManager, System.Random baseRng, bool isHardMode, Difficulty diff)
    {
        int[] startBoard = GetIntBoard(pileManager);
        GetShuffleData(startBoard, isHardMode, out List<int> unlocked, out List<int> slots);

        // Базовая случайная тасовка (чтобы стол всегда выглядел естественно)
        List<int> finalUnlocked = unlocked.OrderBy(x => baseRng.Next()).ToList();

        if (diff == Difficulty.Hard || diff == Difficulty.Medium)
        {
            // 1. Изымаем всех Королей из тасовки
            var kings = finalUnlocked.Where(v => ((v - 1) % 13) + 1 == 13).ToList();
            finalUnlocked.RemoveAll(v => ((v - 1) % 13) + 1 == 13);

            // 2. Изымаем Двойки и Тройки (только для Харда)
            List<int> earlyCards = new List<int>();
            if (diff == Difficulty.Hard)
            {
                earlyCards = finalUnlocked.Where(v => { int r = ((v - 1) % 13) + 1; return r == 2 || r == 3; }).ToList();
                finalUnlocked.RemoveAll(v => { int r = ((v - 1) % 13) + 1; return r == 2 || r == 3; });
            }

            // 3. Равномерно распределяем Королей в СЕРЕДИНЕ расклада
            // Это избегает "стены" в начале, но гарантирует появление тупиков по мере игры
            int kCount = kings.Count;
            if (kCount > 0)
            {
                int step = finalUnlocked.Count / kCount;
                if (step < 2) step = 2; // Защита от слишком плотной вставки

                for (int i = 0; i < kCount; i++)
                {
                    // Смещение: первый король появится не раньше 2-3 позиции
                    int insertIndex = (i * step) + (step / 2);
                    if (insertIndex > finalUnlocked.Count) insertIndex = finalUnlocked.Count;
                    finalUnlocked.Insert(insertIndex, kings[i]);
                }
            }

            // 4. Добавляем Двойки и Тройки в самый конец (глубокая блокировка)
            if (diff == Difficulty.Hard)
            {
                // Перемешаем их между собой перед добавлением
                earlyCards = earlyCards.OrderBy(x => baseRng.Next()).ToList();
                finalUnlocked.AddRange(earlyCards);
                Debug.Log("<color=red>[Оракул] Злая тасовка (Хард): Короли разбросаны как ловушки, 2-3 спрятаны в конец.</color>");
            }
            else
            {
                Debug.Log("<color=orange>[Оракул] Неприятная тасовка (Медиум): Короли разбросаны как ловушки.</color>");
            }
        }
        else
        {
            Debug.Log("<color=white>[Оракул] Случайная тасовка (Изи).</color>");
        }

        Dictionary<string, int> weights = new Dictionary<string, int>();
        for (int i = 0; i < finalUnlocked.Count; i++)
        {
            int val = finalUnlocked[i];
            Suit s = (Suit)((val - 1) / 13);
            int r = ((val - 1) % 13) + 1;
            weights[$"{s}_{r}"] = i;
        }
        return weights;
    }

    private int SimulateFast(int[] board)
    {
        bool moved = true;
        int safety = 0;
        while (moved && safety < 1000)
        {
            moved = false;
            safety++;
            for (int i = 0; i < 56; i++)
            {
                if (board[i] == 0) // Пустой слот
                {
                    int r = i % 4; int c = i / 4;
                    int nSuit = -1, nRank = -1;

                    if (c == 0)
                    {
                        nRank = 1;
                        nSuit = RowSuitRequirements[r];
                        if (nSuit == -1) // Ищем любого свободного Туза
                        {
                            for (int s = 0; s < 4; s++)
                            {
                                bool used = false;
                                for (int j = 0; j < 56; j++) if (board[j] == s * 13 + 1) { used = true; break; }
                                if (!used) { nSuit = s; break; }
                            }
                        }
                    }
                    else
                    {
                        int left = board[i - 4];
                        if (left != 0)
                        {
                            int lRank = (left - 1) % 13 + 1;
                            if (lRank < 13) { nSuit = (left - 1) / 13; nRank = lRank + 1; }
                        }
                    }

                    if (nRank != -1 && nSuit != -1)
                    {
                        for (int j = 0; j < 56; j++)
                        {
                            if (board[j] == nSuit * 13 + nRank)
                            {
                                board[i] = board[j]; board[j] = 0; moved = true; break;
                            }
                        }
                    }
                }
                if (moved) break;
            }
        }

        int C = 0;
        for (int r = 0; r < 4; r++)
        {
            int first = board[r];
            if (first != 0 && (first - 1) % 13 + 1 == 1)
            {
                int s = (first - 1) / 13; C++;
                for (int c = 1; c < 13; c++)
                {
                    int card = board[c * 4 + r];
                    if (card != 0 && (card - 1) / 13 == s && (card - 1) % 13 + 1 == c + 1) C++;
                    else break;
                }
            }
        }
        return C;
    }

    private int GetBoardHash(MontanaPileManager pileManager)
    {
        unchecked
        {
            int hash = 17;
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 14; c++)
                {
                    var card = pileManager.GetSlot(r, c).GetTopCard();
                    int val = card != null ? (int)card.cardModel.suit * 13 + card.cardModel.rank : 0;
                    hash = hash * 31 + val;
                }
            }
            return hash;
        }
    }

    private int[] GetIntBoard(MontanaPileManager pileManager)
    {
        int[] board = new int[56];
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 14; c++)
            {
                var card = pileManager.GetSlot(r, c).GetTopCard();
                board[c * 4 + r] = card != null ? (int)card.cardModel.suit * 13 + card.cardModel.rank : 0;
            }
        }
        return board;
    }

    private void GetShuffleData(int[] board, bool isHardEmpty, out List<int> unlocked, out List<int> slots)
    {
        unlocked = new List<int>();
        slots = new List<int>();

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
    }

    // ====================================================================
    // DECODE / ENCODE 
    // ====================================================================

    public static void EncodeSolution(int masterSeed, int[] reqSuits, List<int[]> targets, List<int> goldenSeeds, Stack<CardInstance> stock)
    {
        stock.Clear();
        int temp = masterSeed;
        for (int i = 0; i < 4; i++) { PushVal(stock, temp % 52); temp /= 52; }
        for (int i = 0; i < 4; i++) PushVal(stock, reqSuits[i] == -1 ? 4 : reqSuits[i]);
        PushVal(stock, targets.Count);

        for (int i = 0; i < targets.Count; i++)
        {
            for (int s = 0; s < 4; s++) PushVal(stock, targets[i][s]);
            int gSeed = goldenSeeds[i];
            for (int k = 0; k < 4; k++) { PushVal(stock, gSeed % 52); gSeed /= 52; }
        }
    }

    private void DecodeSolution(Stack<CardInstance> stock)
    {
        List<int> data = new List<int>();
        while (stock.Count > 0) data.Add(PopVal(stock));
        data.Reverse();

        int ptr = 0; MasterSeed = 0; int multiplier = 1;
        for (int i = 0; i < 4; i++) { MasterSeed += data[ptr++] * multiplier; multiplier *= 52; }

        RowSuitRequirements = new int[4];
        for (int i = 0; i < 4; i++) { int v = data[ptr++]; RowSuitRequirements[i] = v == 4 ? -1 : v; }

        int cpCount = data[ptr++]; Checkpoints.Clear();
        for (int i = 0; i < cpCount; i++)
        {
            Checkpoint cp = new Checkpoint();
            for (int s = 0; s < 4; s++) cp.TargetSuitLengths[s] = data[ptr++];
            int gSeed = 0; int gMult = 1;
            for (int k = 0; k < 4; k++) { gSeed += data[ptr++] * gMult; gMult *= 52; }
            cp.GoldenSeed = gSeed;
            Checkpoints.Add(cp);
        }
    }

    private static void PushVal(Stack<CardInstance> stock, int val) { Suit s = (Suit)(val / 13); int r = (val % 13) + 1; stock.Push(new CardInstance(new CardModel(s, r), false)); }
    private static int PopVal(Stack<CardInstance> stock) { var c = stock.Pop(); return (int)c.Card.suit * 13 + (c.Card.rank - 1); }
}