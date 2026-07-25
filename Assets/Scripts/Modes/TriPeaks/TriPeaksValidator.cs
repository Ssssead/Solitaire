using System;
using System.Collections.Generic;
using UnityEngine;

public static class TriPeaksEvaluator
{
    // ВАЖНО: Если у тебя Король НЕ может лечь на Туза, выставь false!
    private const bool ALLOW_KING_ACE_WRAP = true; // Замени на false, если K-A отключен

    private static readonly uint[] bMasks = new uint[28];
    static TriPeaksEvaluator()
    {
        bMasks[9] = (1u << 18) | (1u << 19); bMasks[10] = (1u << 19) | (1u << 20); bMasks[11] = (1u << 20) | (1u << 21);
        bMasks[12] = (1u << 21) | (1u << 22); bMasks[13] = (1u << 22) | (1u << 23); bMasks[14] = (1u << 23) | (1u << 24);
        bMasks[15] = (1u << 24) | (1u << 25); bMasks[16] = (1u << 25) | (1u << 26); bMasks[17] = (1u << 26) | (1u << 27);
        bMasks[3] = (1u << 9) | (1u << 10); bMasks[4] = (1u << 10) | (1u << 11); bMasks[5] = (1u << 12) | (1u << 13);
        bMasks[6] = (1u << 13) | (1u << 14); bMasks[7] = (1u << 15) | (1u << 16); bMasks[8] = (1u << 16) | (1u << 17);
        bMasks[0] = (1u << 3) | (1u << 4); bMasks[1] = (1u << 5) | (1u << 6); bMasks[2] = (1u << 7) | (1u << 8);
    }

    public struct EvalResult
    {
        public bool IsSolvable;
        public int StatesToFirstWin;
        public int SmartWinRate; // от 0 до 100
        public int KingsOnPeaks;
        public int MaxChain;
    }

    public static EvalResult Evaluate(Deal deal)
    {
        EvalResult res = new EvalResult();
        int[] tRanks = new int[28]; for (int i = 0; i < 28; i++) tRanks[i] = deal.tableau[i][0].Card.rank;
        int[] sRanks = new int[deal.stock.Count]; var stockArr = deal.stock.ToArray();
        for (int i = 0; i < stockArr.Length; i++) sRanks[i] = stockArr[i].Card.rank;

        // 1. Статика
        for (int i = 0; i <= 2; i++) if (tRanks[i] == 13) res.KingsOnPeaks++;

        // 2. Smart Win Rate (Быстрая симуляция 50 игр)
        System.Random rng = new System.Random(deal.GetHashCode());
        int smartWins = 0;
        int maxEverChain = 0;

        for (int iter = 0; iter < 50; iter++)
        {
            uint tMask = (1u << 28) - 1; int cursor = 1; int rank = sRanks[0];
            int currentChain = 0;

            while (tMask > 0)
            {
                List<int> moves = GetMoves(tMask, rank, tRanks);
                if (moves.Count > 0)
                {
                    int bestMove = moves[0]; int bestScore = -1;
                    foreach (int m in moves)
                    {
                        int score = GetMoves(tMask & ~(1u << m), tRanks[m], tRanks).Count > 0 ? 10 : 0;
                        for (int i = 0; i < 28; i++) if ((bMasks[i] & (1u << m)) != 0) score++;
                        if (score > bestScore || (score == bestScore && rng.NextDouble() > 0.5)) { bestScore = score; bestMove = m; }
                    }
                    tMask &= ~(1u << bestMove); rank = tRanks[bestMove];
                    currentChain++; if (currentChain > maxEverChain) maxEverChain = currentChain;
                }
                else { if (cursor < sRanks.Length) { rank = sRanks[cursor++]; currentChain = 0; } else break; }
            }
            if (tMask == 0) smartWins++;
        }
        res.SmartWinRate = (smartWins * 100) / 50;
        res.MaxChain = maxEverChain;

        // 3. States To First Win (DFS поиск до первой победы)
        int nodes = 0;
        bool DFS(ulong state)
        {
            nodes++;
            if (nodes > 40000) return false; // Защита от долгого поиска (Hard кап)

            uint tMask = (uint)(state & 0xFFFFFFF);
            if (tMask == 0) return true;

            int cursor = (int)((state >> 28) & 0x3F); int rank = (int)((state >> 34) & 0xF);

            List<int> moves = GetMoves(tMask, rank, tRanks);
            foreach (int m in moves)
            {
                if (DFS((tMask & ~(1u << m)) | ((ulong)cursor << 28) | ((ulong)tRanks[m] << 34))) return true;
            }

            if (cursor < sRanks.Length)
                if (DFS(tMask | ((ulong)(cursor + 1) << 28) | ((ulong)sRanks[cursor] << 34))) return true;

            return false;
        }

        res.IsSolvable = DFS(((1u << 28) - 1) | (1ul << 28) | ((ulong)sRanks[0] << 34));
        res.StatesToFirstWin = nodes;

        return res;
    }

    private static List<int> GetMoves(uint tMask, int rank, int[] tRanks)
    {
        List<int> moves = new List<int>(5);
        for (int i = 0; i < 28; i++)
        {
            if ((tMask & (1u << i)) != 0 && (tMask & bMasks[i]) == 0)
            {
                int d = Math.Abs(rank - tRanks[i]);
                if (ALLOW_KING_ACE_WRAP) { if (d == 1 || d == 12) moves.Add(i); }
                else { if (d == 1) moves.Add(i); }
            }
        }
        return moves;
    }
}