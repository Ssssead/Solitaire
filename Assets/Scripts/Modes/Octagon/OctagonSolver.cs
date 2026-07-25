using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

public static class OctagonSolver
{
    private const int MAX_DEPTH = 500;
    private const int MAX_STATES = 25000;

    public struct Card { public byte rank; public byte suit; }

    public class InternalDeal
    {
        public List<Card>[] tableau = new List<Card>[20];
        public List<Card> stock = new List<Card>();
        public List<Card> waste = new List<Card>();
        public byte[] foundations = new byte[8];
        public byte recyclesUsed = 0;
        public int refillsTriggered = 0;

        public InternalDeal Clone()
        {
            InternalDeal d = new InternalDeal();
            for (int i = 0; i < 20; i++) d.tableau[i] = new List<Card>(this.tableau[i]);
            d.stock = new List<Card>(this.stock);
            d.waste = new List<Card>(this.waste);
            Array.Copy(this.foundations, d.foundations, 8);
            d.recyclesUsed = this.recyclesUsed;
            d.refillsTriggered = this.refillsTriggered;
            return d;
        }
    }

    private class SearchNode
    {
        public InternalDeal State;
        public int Depth;
        // --- ДОБАВЛЕНО: Для восстановления пути ---
        public SearchNode Parent;
        public MoveCommand Move;

        public SearchNode(InternalDeal state, int depth, SearchNode parent = null, MoveCommand move = default)
        {
            State = state;
            Depth = depth;
            Parent = parent;
            Move = move;
        }
    }

    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, int>> elements = new List<KeyValuePair<T, int>>();
        public int Count => elements.Count;

        public void Enqueue(T item, int priority)
        {
            elements.Add(new KeyValuePair<T, int>(item, priority));
            int ci = elements.Count - 1;
            while (ci > 0)
            {
                int pi = (ci - 1) / 2;
                if (elements[ci].Value >= elements[pi].Value) break;
                var tmp = elements[ci]; elements[ci] = elements[pi]; elements[pi] = tmp; ci = pi;
            }
        }

        public T Dequeue()
        {
            int li = elements.Count - 1;
            var frontItem = elements[0].Key;
            elements[0] = elements[li];
            elements.RemoveAt(li);
            --li;
            int pi = 0;
            while (true)
            {
                int ci = pi * 2 + 1;
                if (ci > li) break;
                int rc = ci + 1;
                if (rc <= li && elements[rc].Value < elements[ci].Value) ci = rc;
                if (elements[pi].Value <= elements[ci].Value) break;
                var tmp = elements[pi]; elements[pi] = elements[ci]; elements[ci] = tmp; pi = ci;
            }
            return frontItem;
        }
    }

    public enum MoveType { Foundation, TableauToTableau, WasteToTableau, StockDraw, Recycle }

    public struct MoveCommand
    {
        public MoveType Type;
        public int From;
        public int To;
        public int TargetFoundation;
    }

    [Serializable]
    public class ExtendedSolverResult
    {
        public bool IsSolved;
        public int Moves;
        public int StatesVisited;
        // --- ДОБАВЛЕНО: Полный путь решения ---
        public string SolutionPath;
    }

    public static IEnumerator SolveAsync(Deal initialDeal, float frameBudgetMs, ExtendedSolverResult resultOut)
    {
        InternalDeal internalRoot = ConvertToInternal(initialDeal);
        var openSet = new PriorityQueue<SearchNode>();
        var closedSet = new HashSet<ulong>();

        // Корневая нода не имеет родителя
        openSet.Enqueue(new SearchNode(internalRoot, 0), 0);

        int statesVisited = 0;
        SearchNode winningNode = null;
        Stopwatch sw = Stopwatch.StartNew();

        while (openSet.Count > 0)
        {
            if (sw.ElapsedMilliseconds >= frameBudgetMs)
            {
                yield return null;
                sw.Restart();
            }

            if (statesVisited >= MAX_STATES) break;

            var current = openSet.Dequeue();
            statesVisited++;

            if (IsGameWon(current.State))
            {
                winningNode = current;
                break;
            }

            if (current.Depth > MAX_DEPTH) continue;

            ulong stateHash = ComputeSymmetricHash(current.State);
            if (closedSet.Contains(stateHash)) continue;
            closedSet.Add(stateHash);

            List<MoveCommand> moves = GetPossibleMoves(current.State);

            foreach (var move in moves)
            {
                InternalDeal nextState = ApplyMove(current.State, move);
                int hCost = CalculateHeuristic(nextState);
                int fCost = current.Depth + 1 + hCost;

                // Передаем current как родителя, и move как действие
                openSet.Enqueue(new SearchNode(nextState, current.Depth + 1, current, move), fCost);
            }
        }

        resultOut.StatesVisited = statesVisited;
        resultOut.IsSolved = (winningNode != null);
        resultOut.Moves = winningNode != null ? winningNode.Depth : 0;

        // --- ДОБАВЛЕНО: Восстановление пути решения ---
        if (winningNode != null)
        {
            List<string> path = new List<string>();
            SearchNode curr = winningNode;

            while (curr.Parent != null)
            {
                string mStr = curr.Move.Type.ToString();

                // Форматируем ход для читаемости в логах
                if (curr.Move.Type == MoveType.Foundation)
                    mStr += $"(From:{(curr.Move.From == -1 ? "W" : $"T{curr.Move.From}")}->F{curr.Move.TargetFoundation})";
                else if (curr.Move.Type == MoveType.TableauToTableau)
                    mStr += $"(T{curr.Move.From}->T{curr.Move.To})";
                else if (curr.Move.Type == MoveType.WasteToTableau)
                    mStr += $"(W->T{curr.Move.To})";

                path.Add(mStr);
                curr = curr.Parent;
            }

            path.Reverse(); // Переворачиваем, чтобы путь шел от старта к финишу
            resultOut.SolutionPath = string.Join("|", path);
        }
    }

    private static List<MoveCommand> GetPossibleMoves(InternalDeal d)
    {
        List<MoveCommand> moves = new List<MoveCommand>();
        List<MoveCommand> foundationMoves = new List<MoveCommand>();

        if (d.waste.Count > 0)
        {
            int fIdx = GetFoundationIndex(d, d.waste.Last());
            if (fIdx != -1) foundationMoves.Add(new MoveCommand { Type = MoveType.Foundation, From = -1, TargetFoundation = fIdx });
        }
        for (int i = 0; i < 20; i++)
        {
            if (d.tableau[i].Count > 0)
            {
                int fIdx = GetFoundationIndex(d, d.tableau[i].Last());
                if (fIdx != -1) foundationMoves.Add(new MoveCommand { Type = MoveType.Foundation, From = i, TargetFoundation = fIdx });
            }
        }
        if (foundationMoves.Count > 0) return foundationMoves;

        for (int from = 0; from < 20; from++)
        {
            if (d.tableau[from].Count == 0) continue;
            Card movingCard = d.tableau[from].Last();

            for (int to = 0; to < 20; to++)
            {
                if (from == to) continue;
                if (d.tableau[to].Count == 0) continue;

                Card targetCard = d.tableau[to].Last();

                if (targetCard.rank == movingCard.rank + 1)
                {
                    if (d.tableau[from].Count > 1)
                    {
                        Card revealedCard = d.tableau[from][d.tableau[from].Count - 2];
                        if (revealedCard.rank == targetCard.rank)
                        {
                            if (GetFoundationIndex(d, revealedCard) == -1) continue;
                        }
                    }
                    moves.Add(new MoveCommand { Type = MoveType.TableauToTableau, From = from, To = to });
                }
            }
        }

        if (d.waste.Count > 0)
        {
            Card movingCard = d.waste.Last();
            for (int to = 0; to < 20; to++)
            {
                if (d.tableau[to].Count == 0) continue;
                Card targetCard = d.tableau[to].Last();
                if (targetCard.rank == movingCard.rank + 1) moves.Add(new MoveCommand { Type = MoveType.WasteToTableau, To = to });
            }
        }

        if (d.stock.Count > 0) moves.Add(new MoveCommand { Type = MoveType.StockDraw });
        else if (d.waste.Count > 0 && d.recyclesUsed < 2) moves.Add(new MoveCommand { Type = MoveType.Recycle });

        return moves;
    }

    private static InternalDeal ApplyMove(InternalDeal old, MoveCommand m)
    {
        InternalDeal d = old.Clone();

        if (m.Type == MoveType.Foundation)
        {
            Card c = m.From == -1 ? d.waste.Last() : d.tableau[m.From].Last();
            if (m.From == -1) d.waste.RemoveAt(d.waste.Count - 1);
            else d.tableau[m.From].RemoveAt(d.tableau[m.From].Count - 1);
            d.foundations[m.TargetFoundation] = c.rank;
        }
        else if (m.Type == MoveType.TableauToTableau)
        {
            Card c = d.tableau[m.From].Last();
            d.tableau[m.From].RemoveAt(d.tableau[m.From].Count - 1);
            d.tableau[m.To].Add(c);
        }
        else if (m.Type == MoveType.WasteToTableau)
        {
            Card c = d.waste.Last();
            d.waste.RemoveAt(d.waste.Count - 1);
            d.tableau[m.To].Add(c);
        }
        else if (m.Type == MoveType.StockDraw)
        {
            d.waste.Add(d.stock.Last());
            d.stock.RemoveAt(d.stock.Count - 1);
        }
        else if (m.Type == MoveType.Recycle)
        {
            for (int i = d.waste.Count - 1; i >= 0; i--) d.stock.Add(d.waste[i]);
            d.waste.Clear();
            d.recyclesUsed++;
        }

        CheckAutoRefill(d);
        return d;
    }

    private static void CheckAutoRefill(InternalDeal d)
    {
        for (int g = 0; g < 4; g++)
        {
            bool isEmpty = true;
            for (int s = 0; s < 5; s++)
            {
                if (d.tableau[g * 5 + s].Count > 0) { isEmpty = false; break; }
            }

            if (isEmpty && (d.stock.Count > 0 || d.waste.Count > 0))
            {
                d.refillsTriggered++;

                List<Card> collected = new List<Card>();

                while (collected.Count < 5 && d.stock.Count > 0)
                {
                    collected.Add(d.stock.Last());
                    d.stock.RemoveAt(d.stock.Count - 1);
                }
                while (collected.Count < 5 && d.waste.Count > 0)
                {
                    collected.Add(d.waste[0]);
                    d.waste.RemoveAt(0);
                }
                for (int i = 0; i < collected.Count; i++)
                {
                    d.tableau[g * 5 + (4 - i)].Add(collected[i]);
                }
            }
        }
    }

    private static int GetFoundationIndex(InternalDeal d, Card c)
    {
        int suitBase = c.suit * 2;
        if (d.foundations[suitBase] == c.rank - 1) return suitBase;
        if (d.foundations[suitBase + 1] == c.rank - 1) return suitBase + 1;
        return -1;
    }

    private static int CalculateHeuristic(InternalDeal d)
    {
        int h = 0;
        int foundSum = 0;
        for (int i = 0; i < 8; i++) foundSum += d.foundations[i];

        h -= foundSum * 1000;
        h += d.stock.Count * 20;
        h += d.waste.Count * 25;
        h += d.refillsTriggered * 400;

        int sameSuitBonus = 0;
        for (int i = 0; i < 20; i++)
        {
            if (d.tableau[i].Count > 1)
            {
                var topCard = d.tableau[i].Last();
                var cardUnder = d.tableau[i][d.tableau[i].Count - 2];

                if (topCard.suit == cardUnder.suit && cardUnder.rank == topCard.rank + 1)
                {
                    sameSuitBonus += 150;
                }
            }
        }
        h -= sameSuitBonus;

        return h;
    }

    private static ulong ComputeSymmetricHash(InternalDeal d)
    {
        ulong[] groupHashes = new ulong[4];
        for (int g = 0; g < 4; g++)
        {
            ulong[] slotHashes = new ulong[5];
            for (int s = 0; s < 5; s++)
            {
                ulong h = 17;
                foreach (var c in d.tableau[g * 5 + s]) h = h * 31 + (ulong)(c.suit * 13 + c.rank);
                slotHashes[s] = h;
            }
            Array.Sort(slotHashes);
            ulong gh = 19;
            foreach (var sh in slotHashes) gh = gh * 1009 + sh;
            groupHashes[g] = gh;
        }
        Array.Sort(groupHashes);
        ulong finalHash = 23;
        foreach (var gh in groupHashes) finalHash = finalHash * 1009 + gh;

        finalHash ^= (ulong)d.stock.Count * 1234567;
        finalHash ^= (ulong)d.waste.Count * 7654321;
        finalHash ^= (ulong)d.recyclesUsed * 1010101;
        finalHash ^= (ulong)d.refillsTriggered * 8888888;

        ulong fHash = 0;
        for (int i = 0; i < 8; i++) fHash = (fHash << 4) | d.foundations[i];
        finalHash ^= fHash;

        return finalHash;
    }

    private static bool IsGameWon(InternalDeal d)
    {
        for (int i = 0; i < 8; i++) if (d.foundations[i] < 13) return false;
        return true;
    }

    private static InternalDeal ConvertToInternal(Deal d)
    {
        InternalDeal id = new InternalDeal();
        for (int i = 0; i < 20; i++) id.tableau[i] = new List<Card>();
        for (int i = 0; i < 8; i++) id.foundations[i] = 1;

        for (int g = 0; g < 4; g++)
        {
            if (g >= d.tableau.Count) break;
            for (int s = 0; s < 5; s++)
            {
                if (s < d.tableau[g].Count)
                    id.tableau[g * 5 + s].Add(new Card { suit = (byte)d.tableau[g][s].Card.suit, rank = (byte)d.tableau[g][s].Card.rank });
            }
        }
        var stockArr = d.stock.ToArray();
        for (int i = stockArr.Length - 1; i >= 0; i--)
            id.stock.Add(new Card { suit = (byte)stockArr[i].Card.suit, rank = (byte)stockArr[i].Card.rank });

        return id;
    }
}