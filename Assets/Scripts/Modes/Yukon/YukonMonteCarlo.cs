using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

public static class YukonMonteCarlo
{
    // Оптимизация: используем struct вместо class, чтобы не нагружать сборщик мусора (GC)
    private enum MoveType { Foundation, RevealTableau, MoveTableau }
    private struct MoveCmd { public MoveType Type; public int From; public int To; public int Count; }

    // Асинхронный метод оценки
    public static IEnumerator EvaluateWinRateAsync(Deal deal, int variantParam, int simulations, float frameBudgetMs, Stopwatch frameWatch, Action<float> onResult)
    {
        int wins = 0;

        for (int i = 0; i < simulations; i++)
        {
            // Уважаем бюджет времени кадра! Если превысили - отдаем кадр Unity, чтобы UI не вис
            if (frameWatch.ElapsedMilliseconds > frameBudgetMs)
            {
                yield return null;
                frameWatch.Restart();
            }

            if (SimulateRandomGame(deal.DeepClone(), variantParam))
            {
                wins++;
            }
        }

        onResult?.Invoke((float)wins / simulations);
    }

    private static bool SimulateRandomGame(Deal d, int variantParam)
    {
        int moves = 0;
        int maxMoves = 300;

        Queue<int> recentStates = new Queue<int>();

        while (moves < maxMoves)
        {
            if (IsGameWon(d)) return true;

            var legalMoves = GenerateMoves(d, variantParam);
            if (legalMoves.Count == 0) return false;

            var obviousMoveIndex = legalMoves.FindIndex(m => m.Type == MoveType.Foundation || m.Type == MoveType.RevealTableau);

            if (obviousMoveIndex != -1)
            {
                ApplyMove(d, legalMoves[obviousMoveIndex]);
            }
            else
            {
                legalMoves.Shuffle();
                bool moved = false;

                foreach (var move in legalMoves)
                {
                    Deal nextState = ApplyMoveClone(d, move);
                    int hash = GetStateHash(nextState);

                    if (!recentStates.Contains(hash))
                    {
                        ApplyMove(d, move);
                        recentStates.Enqueue(hash);
                        if (recentStates.Count > 10) recentStates.Dequeue();
                        moved = true;
                        break;
                    }
                }

                if (!moved) return false;
            }

            moves++;
        }

        return false;
    }

    private static List<MoveCmd> GenerateMoves(Deal d, int variantParam)
    {
        List<MoveCmd> moves = new List<MoveCmd>(16); // Предвыделяем память

        for (int i = 0; i < 7; i++)
        {
            if (d.tableau[i].Count > 0 && !d.tableau[i].Last().FaceUp)
                moves.Add(new MoveCmd { Type = MoveType.RevealTableau, From = i });
        }

        for (int i = 0; i < 7; i++)
        {
            if (d.tableau[i].Count == 0) continue;
            var top = d.tableau[i].Last();
            if (top.FaceUp && CanAddToFoundation(d, top.Card))
                moves.Add(new MoveCmd { Type = MoveType.Foundation, From = i });
        }

        for (int src = 0; src < 7; src++)
        {
            for (int j = 0; j < d.tableau[src].Count; j++)
            {
                if (!d.tableau[src][j].FaceUp) continue;
                var movingCard = d.tableau[src][j].Card;
                int count = d.tableau[src].Count - j;

                for (int dst = 0; dst < 7; dst++)
                {
                    if (src == dst) continue;
                    if (d.tableau[dst].Count == 0 && movingCard.rank == 13 && j == 0) continue;

                    if (CanPlaceOnTableau(d, dst, movingCard, variantParam))
                        moves.Add(new MoveCmd { Type = MoveType.MoveTableau, From = src, To = dst, Count = count });
                }
            }
        }
        return moves;
    }

    private static void ApplyMove(Deal d, MoveCmd m)
    {
        if (m.Type == MoveType.Foundation)
        {
            var card = d.tableau[m.From].Last();
            d.tableau[m.From].RemoveAt(d.tableau[m.From].Count - 1);
            d.foundations[(int)card.Card.suit].Add(card.Card);
        }
        else if (m.Type == MoveType.RevealTableau)
        {
            d.tableau[m.From].Last().FaceUp = true;
        }
        else
        {
            int start = d.tableau[m.From].Count - m.Count;
            var sub = d.tableau[m.From].GetRange(start, m.Count);
            d.tableau[m.From].RemoveRange(start, m.Count);
            d.tableau[m.To].AddRange(sub);
        }
    }

    private static Deal ApplyMoveClone(Deal d, MoveCmd m) { var clone = d.DeepClone(); ApplyMove(clone, m); return clone; }

    private static bool CanPlaceOnTableau(Deal d, int dest, CardModel c, int variantParam)
    {
        if (d.tableau[dest].Count == 0) return c.rank == 13;
        var top = d.tableau[dest].Last().Card;
        if (top.rank != c.rank + 1) return false;
        if (variantParam == 1) return top.suit == c.suit;
        bool rA = (c.suit == Suit.Diamonds || c.suit == Suit.Hearts);
        bool rB = (top.suit == Suit.Diamonds || top.suit == Suit.Hearts);
        return rA != rB;
    }

    private static bool CanAddToFoundation(Deal d, CardModel c)
    {
        var f = d.foundations[(int)c.suit];
        return f.Count == 0 ? c.rank == 1 : f.Last().rank == c.rank - 1;
    }

    private static bool IsGameWon(Deal d) { int c = 0; foreach (var f in d.foundations) c += f.Count; return c == 52; }

    private static int GetStateHash(Deal d)
    {
        int h = 17;
        foreach (var p in d.tableau) if (p.Count > 0) h = h * 31 + (p.Last().FaceUp ? 1 : 0) + (int)p.Last().Card.suit * 13 + p.Last().Card.rank;
        return h;
    }
}

public static class ListExtensions
{
    public static void Shuffle<T>(this IList<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1) { n--; int k = rng.Next(n + 1); T value = list[k]; list[k] = list[n]; list[n] = value; }
    }
}