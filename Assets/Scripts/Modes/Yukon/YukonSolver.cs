using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;

public static class YukonSolver
{
    private const int MAX_DEPTH = 300;

    public class ExtendedSolverResult
    {
        public bool IsSolved;
        public int SolutionMoves;
        public int Backtracks;
    }

    private struct StateKey : IEquatable<StateKey>
    {
        public readonly int TableauHash;
        public readonly int FoundationHash;

        public StateKey(int tHash, int fHash) { TableauHash = tHash; FoundationHash = fHash; }
        public bool Equals(StateKey other) => TableauHash == other.TableauHash && FoundationHash == other.FoundationHash;
        public override int GetHashCode() => unchecked(TableauHash * 31 + FoundationHash);
    }

    public enum MoveType { Foundation, RevealTableau, MoveTableau }

    public class MoveCommand
    {
        public MoveType Type;
        public int FromIdx;
        public int ToIdx;
        public int Count;
    }

    private class SearchNode
    {
        public Deal DealState;
        public int Depth;
        public List<MoveCommand> Moves;
        public int MoveIndex;
        public SearchNode Parent;

        public SearchNode(Deal d, int depth)
        {
            DealState = d; Depth = depth;
            Moves = null; MoveIndex = 0; Parent = null;
        }
    }

    public static IEnumerator SolveAsync(Deal initialDeal, int variantParam, float frameBudgetMs, ExtendedSolverResult result, int maxStates)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        HashSet<StateKey> visited = new HashSet<StateKey>();
        Stack<SearchNode> stack = new Stack<SearchNode>();

        stack.Push(new SearchNode(initialDeal.DeepClone(), 0));
        visited.Add(GetStateKey(initialDeal));

        int statesExplored = 0;
        int backtracks = 0;
        bool solved = false;
        int solutionMoves = 0;

        while (stack.Count > 0)
        {
            if (statesExplored >= maxStates) break;

            if (sw.ElapsedMilliseconds > frameBudgetMs)
            {
                yield return null;
                sw.Restart();
            }

            SearchNode node = stack.Peek();

            if (IsGameWon(node.DealState))
            {
                solved = true;
                solutionMoves = node.Depth;
                break;
            }

            if (node.Depth >= MAX_DEPTH)
            {
                backtracks++;
                stack.Pop();
                continue;
            }

            if (node.Moves == null)
            {
                statesExplored++;
                node.Moves = GenerateMoves(node.DealState, variantParam);
            }

            if (node.MoveIndex < node.Moves.Count)
            {
                MoveCommand move = node.Moves[node.MoveIndex++];
                Deal nextState = ApplyMove(node.DealState, move);
                StateKey key = GetStateKey(nextState);

                if (!visited.Contains(key))
                {
                    visited.Add(key);
                    SearchNode nextNode = new SearchNode(nextState, node.Depth + 1) { Parent = node };
                    stack.Push(nextNode);
                }
            }
            else
            {
                // Тупик на текущем узле (все ходы исчерпаны)
                backtracks++;
                stack.Pop();
            }
        }

        result.IsSolved = solved;
        result.SolutionMoves = solutionMoves;
        result.Backtracks = backtracks;
    }

    private static List<MoveCommand> GenerateMoves(Deal d, int variantParam)
    {
        List<MoveCommand> moves = new List<MoveCommand>();

        // 1. Открытие закрытой карты
        for (int i = 0; i < 7; i++)
        {
            if (d.tableau[i].Count > 0 && !d.tableau[i].Last().FaceUp)
                moves.Add(new MoveCommand { Type = MoveType.RevealTableau, FromIdx = i });
        }

        // 2. В фундамент (только верхняя открытая карта)
        for (int i = 0; i < 7; i++)
        {
            if (d.tableau[i].Count == 0) continue;
            var topCard = d.tableau[i].Last();
            if (topCard.FaceUp && CanAddToFoundation(d, topCard.Card))
            {
                moves.Add(new MoveCommand { Type = MoveType.Foundation, FromIdx = i });
            }
        }

        // 3. Перемещение между колонками (Юкон разрешает двигать любые открытые карты вместе с "хвостом")
        for (int src = 0; src < 7; src++)
        {
            for (int cardIdx = 0; cardIdx < d.tableau[src].Count; cardIdx++)
            {
                if (!d.tableau[src][cardIdx].FaceUp) continue;

                var movingCard = d.tableau[src][cardIdx].Card;
                int countToMove = d.tableau[src].Count - cardIdx;

                for (int dst = 0; dst < 7; dst++)
                {
                    if (src == dst) continue;

                    // Оптимизация: не двигаем собранную колонку (начинающуюся с Короля) в пустую
                    if (d.tableau[dst].Count == 0 && movingCard.rank == 13 && cardIdx == 0) continue;

                    if (CanPlaceOnTableau(d, dst, movingCard, variantParam))
                    {
                        moves.Add(new MoveCommand { Type = MoveType.MoveTableau, FromIdx = src, ToIdx = dst, Count = countToMove });
                    }
                }
            }
        }

        // Жадная сортировка для ускорения (в фундамент и вскрытие всегда в приоритете)
        return moves.OrderByDescending(m => m.Type == MoveType.Foundation ? 3 : (m.Type == MoveType.RevealTableau ? 2 : 1)).ToList();
    }

    private static Deal ApplyMove(Deal d, MoveCommand m)
    {
        Deal next = d.DeepClone();
        if (m.Type == MoveType.Foundation)
        {
            var card = next.tableau[m.FromIdx].Last();
            next.tableau[m.FromIdx].RemoveAt(next.tableau[m.FromIdx].Count - 1);
            next.foundations[(int)card.Card.suit].Add(card.Card);
        }
        else if (m.Type == MoveType.RevealTableau)
        {
            next.tableau[m.FromIdx].Last().FaceUp = true;
        }
        else if (m.Type == MoveType.MoveTableau)
        {
            int startIdx = next.tableau[m.FromIdx].Count - m.Count;
            var subStack = next.tableau[m.FromIdx].GetRange(startIdx, m.Count);
            next.tableau[m.FromIdx].RemoveRange(startIdx, m.Count);
            next.tableau[m.ToIdx].AddRange(subStack);
        }
        return next;
    }

    private static bool CanPlaceOnTableau(Deal d, int destIdx, CardModel c, int variantParam)
    {
        if (d.tableau[destIdx].Count == 0) return c.rank == 13;
        var targetTop = d.tableau[destIdx].Last().Card;
        if (targetTop.rank != c.rank + 1) return false;
        if (variantParam == 1) return targetTop.suit == c.suit;
        return IsOppositeColor(c, targetTop);
    }

    private static bool CanAddToFoundation(Deal d, CardModel c)
    {
        var f = d.foundations[(int)c.suit];
        return f.Count == 0 ? c.rank == 1 : f.Last().rank == c.rank - 1;
    }

    private static bool IsOppositeColor(CardModel a, CardModel b)
    {
        bool rA = (a.suit == Suit.Diamonds || a.suit == Suit.Hearts);
        bool rB = (b.suit == Suit.Diamonds || b.suit == Suit.Hearts);
        return rA != rB;
    }

    private static bool IsGameWon(Deal d)
    {
        return d.foundations.Sum(f => f.Count) == 52;
    }

    private static StateKey GetStateKey(Deal d)
    {
        int tHash = 17;
        for (int i = 0; i < 7; i++)
        {
            var p = d.tableau[i];
            tHash = tHash * 31 + p.Count;
            if (p.Count > 0)
            {
                // Хешируем весь открытый "хвост"
                for (int j = 0; j < p.Count; j++)
                {
                    if (p[j].FaceUp)
                    {
                        tHash = tHash * 31 + (int)p[j].Card.suit * 13 + p[j].Card.rank;
                    }
                }
            }
        }
        int fHash = 17;
        for (int i = 0; i < 4; i++) fHash = fHash * 31 + d.foundations[i].Count;
        return new StateKey(tHash, fHash);
    }
}