using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public static class FreeCellSolver
{
    private const int MAX_DEPTH = 500;
    private const int MAX_STATES = 500000;

    public struct Card
    {
        public byte suit;
        public byte rank;
        public bool IsRed => suit == 1 || suit == 2;
        public Card(int s, int r) { suit = (byte)s; rank = (byte)r; }
    }

    public class State
    {
        public byte[] foundations = new byte[4];
        public Card[] freeCells = new Card[4];
        public Card[][] tableau = new Card[8][];
        public int[] tabLens = new int[8];

        public State() { for (int i = 0; i < 8; i++) tableau[i] = new Card[25]; }

        public State Clone()
        {
            State s = new State();
            Buffer.BlockCopy(foundations, 0, s.foundations, 0, 4);
            Array.Copy(freeCells, s.freeCells, 4);
            Buffer.BlockCopy(tabLens, 0, s.tabLens, 0, 32);
            for (int i = 0; i < 8; i++)
                if (tabLens[i] > 0) Array.Copy(tableau[i], s.tableau[i], tabLens[i]);
            return s;
        }
    }

    public enum MoveType { ToFoundation, ToFreeCell, FreeCellToTab, TabToTab, AutoPlay }

    public struct MoveCommand
    {
        public MoveType Type;
        public sbyte FromIdx;
        public sbyte ToIdx;
        public byte Count;
    }

    [Serializable]
    public class ExtendedSolverResult
    {
        public bool IsSolved;
        public int DeadEnds;
        public int VisualChaos;
        public int Moves;
        public int StatesVisited;

        // --- ÕŒ¬€≈ Ã≈“–» » ƒÀﬂ ¿Õ¿À»«¿ ---
        public int MovesToFirstEmptyCol;
        public int FoundationMoves;
        public int TransitMoves; // —ÛÏÏ‡ ıÓ‰Ó‚ ‚ FreeCell Ë Ó·‡ÚÌÓ
        public int TabToTabMoves;
    }

    private class SearchNode
    {
        public State DealState;
        public SearchNode Parent;
        public MoveCommand MoveMade;
        public short Depth;
        public SearchNode(State s, SearchNode p, MoveCommand m, short d) { DealState = s; Parent = p; MoveMade = m; Depth = d; }
    }

    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, int>> elements = new List<KeyValuePair<T, int>>();
        public int Count => elements.Count;
        public void Enqueue(T item, int priority)
        {
            elements.Add(new KeyValuePair<T, int>(item, priority));
            int ci = elements.Count - 1;
            while (ci > 0) { int pi = (ci - 1) / 2; if (elements[ci].Value >= elements[pi].Value) break; var tmp = elements[ci]; elements[ci] = elements[pi]; elements[pi] = tmp; ci = pi; }
        }
        public T Dequeue()
        {
            int li = elements.Count - 1; var frontItem = elements[0].Key; elements[0] = elements[li]; elements.RemoveAt(li); --li;
            int pi = 0;
            while (true) { int ci = pi * 2 + 1; if (ci > li) break; int rc = ci + 1; if (rc <= li && elements[rc].Value < elements[ci].Value) ci = rc; if (elements[pi].Value <= elements[ci].Value) break; var tmp = elements[pi]; elements[pi] = elements[ci]; elements[ci] = tmp; pi = ci; }
            return frontItem;
        }
    }

    public static IEnumerator SolveAsync(Deal initialDeal, float frameBudgetMs, int allowedFreeCells, ExtendedSolverResult resultOut)
    {
        State rootState = ConvertToInternal(initialDeal);
        resultOut.VisualChaos = CalculateVisualChaos(initialDeal);

        for (int i = allowedFreeCells; i < 4; i++)
        {
            rootState.freeCells[i] = new Card { suit = 255, rank = 255 };
        }

        var openSet = new PriorityQueue<SearchNode>();
        var closedSet = new HashSet<ulong>();

        int rootH = GetHeuristic(rootState);
        openSet.Enqueue(new SearchNode(rootState, null, default, 0), rootH);

        Stopwatch frameSw = Stopwatch.StartNew();
        int statesVisited = 0;
        int deadEnds = 0;
        bool isSolved = false;
        SearchNode winningNode = null;

        while (openSet.Count > 0)
        {
            if (frameSw.ElapsedMilliseconds >= frameBudgetMs)
            {
                yield return null;
                frameSw.Restart();
            }

            var current = openSet.Dequeue();
            if (current.Depth > MAX_DEPTH || statesVisited > MAX_STATES) continue;

            statesVisited++;

            if (IsGameWon(current.DealState))
            {
                isSolved = true;
                winningNode = current;
                break;
            }

            ulong hash = ComputeSymmetricHash(current.DealState);
            if (closedSet.Contains(hash)) continue;
            closedSet.Add(hash);

            List<KeyValuePair<MoveCommand, State>> nextStates = GetNextStatesWithMoves(current.DealState);
            int newUnvisitedStates = 0;

            foreach (var kvp in nextStates)
            {
                ulong nsHash = ComputeSymmetricHash(kvp.Value);
                if (!closedSet.Contains(nsHash))
                {
                    newUnvisitedStates++;
                    short newDepth = (short)(current.Depth + 1);
                    int h = GetHeuristic(kvp.Value);
                    int f = h + (newDepth * 15);
                    openSet.Enqueue(new SearchNode(kvp.Value, current, kvp.Key, newDepth), f);
                }
            }

            if (newUnvisitedStates == 0) deadEnds++;
        }

        resultOut.IsSolved = isSolved;
        resultOut.DeadEnds = deadEnds;
        resultOut.StatesVisited = statesVisited;

        if (winningNode != null)
        {
            resultOut.Moves = winningNode.Depth;

            // --- –≈“–Œ—œ≈ “»¬Õ€… ¿Õ¿À»« œ”“» –≈ÿ≈Õ»ﬂ ---
            int foundMoves = 0;
            int transitMoves = 0;
            int tabMoves = 0;
            int emptyColDepth = -1;

            SearchNode curr = winningNode;
            while (curr.Parent != null)
            {
                if (curr.MoveMade.Type == MoveType.ToFoundation) foundMoves++;
                else if (curr.MoveMade.Type == MoveType.ToFreeCell || curr.MoveMade.Type == MoveType.FreeCellToTab) transitMoves++;
                else if (curr.MoveMade.Type == MoveType.TabToTab) tabMoves++;

                bool hasEmpty = false;
                for (int i = 0; i < 8; i++) if (curr.DealState.tabLens[i] == 0) { hasEmpty = true; break; }
                if (hasEmpty) emptyColDepth = curr.Depth;

                curr = curr.Parent;
            }

            resultOut.MovesToFirstEmptyCol = emptyColDepth;
            resultOut.FoundationMoves = foundMoves;
            resultOut.TransitMoves = transitMoves;
            resultOut.TabToTabMoves = tabMoves;
        }
        else
        {
            resultOut.Moves = 0;
        }
    }

    private static int GetHeuristic(State s)
    {
        int h = 0;
        for (int i = 0; i < 4; i++) h += (13 - s.foundations[i]) * 100;

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < s.tabLens[i]; j++)
            {
                Card c = s.tableau[i][j];
                if (c.rank == s.foundations[c.suit] + 1)
                {
                    int depth = (s.tabLens[i] - 1) - j;
                    h += depth * 40;
                }
                if (j < s.tabLens[i] - 1)
                {
                    Card top = s.tableau[i][j + 1];
                    if (c.IsRed == top.IsRed || top.rank != c.rank - 1) h += 15;
                }
            }
        }
        return h;
    }

    private static ulong ComputeSymmetricHash(State s)
    {
        ulong hash = 17;
        ulong fHash = (ulong)s.foundations[0] | ((ulong)s.foundations[1] << 4) | ((ulong)s.foundations[2] << 8) | ((ulong)s.foundations[3] << 12);
        hash = hash * 397 + fHash;

        int[] fc = new int[4];
        for (int i = 0; i < 4; i++) if (s.freeCells[i].rank > 0) fc[i] = (s.freeCells[i].suit << 4) | s.freeCells[i].rank;
        Array.Sort(fc);
        ulong fcHash = (ulong)fc[0] | ((ulong)fc[1] << 8) | ((ulong)fc[2] << 16) | ((ulong)fc[3] << 24);
        hash = hash * 397 + fcHash;

        ulong[] colHashes = new ulong[8];
        for (int i = 0; i < 8; i++)
        {
            ulong ch = 19;
            for (int j = 0; j < s.tabLens[i]; j++) ch = ch * 397 + (ulong)((s.tableau[i][j].suit << 4) | s.tableau[i][j].rank);
            colHashes[i] = ch;
        }
        Array.Sort(colHashes);
        for (int i = 0; i < 8; i++) hash = hash * 397 + colHashes[i];
        return hash;
    }

    private static int CalculateVisualChaos(Deal d)
    {
        int chaos = 0;
        for (int col = 0; col < 8; col++)
        {
            var pile = d.tableau[col];
            for (int j = 0; j < pile.Count; j++)
            {
                var c = pile[j].Card;
                int effectiveDepth = 0;

                for (int k = pile.Count - 1; k > j; k--)
                {
                    var cardAbove = pile[k].Card;
                    var cardBelow = pile[k - 1].Card;

                    bool topRed = (cardAbove.suit == Suit.Diamonds || cardAbove.suit == Suit.Hearts);
                    bool botRed = (cardBelow.suit == Suit.Diamonds || cardBelow.suit == Suit.Hearts);
                    if (topRed == botRed || cardAbove.rank != cardBelow.rank - 1)
                        effectiveDepth++;

                    if (cardAbove.suit == c.suit && cardAbove.rank > c.rank) chaos++;

                    bool cRed = (c.suit == Suit.Diamonds || c.suit == Suit.Hearts);
                    if (topRed != cRed && cardAbove.rank == c.rank + 1) chaos++;
                }
                if (pile.Count - 1 > j) effectiveDepth++;

                if (c.rank == 1 || c.rank == 2) chaos += effectiveDepth;
            }
        }
        return chaos;
    }

    private static void PerformAutoPlay(State s)
    {
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < 4; i++)
            {
                if (s.freeCells[i].rank > 0 && s.freeCells[i].suit != 255)
                {
                    Card c = s.freeCells[i];
                    if (CanAndSafeToFoundation(s, c))
                    {
                        s.foundations[c.suit] = c.rank;
                        s.freeCells[i].rank = 0;
                        changed = true;
                    }
                }
            }
            for (int i = 0; i < 8; i++)
            {
                if (s.tabLens[i] > 0)
                {
                    Card c = s.tableau[i][s.tabLens[i] - 1];
                    if (CanAndSafeToFoundation(s, c))
                    {
                        s.foundations[c.suit] = c.rank;
                        s.tabLens[i]--;
                        changed = true;
                    }
                }
            }
        } while (changed);
    }

    private static bool CanAndSafeToFoundation(State s, Card c)
    {
        if (s.foundations[c.suit] != c.rank - 1) return false;
        if (c.rank <= 2) return true;
        byte opp1 = (c.suit == 1 || c.suit == 2) ? s.foundations[0] : s.foundations[1];
        byte opp2 = (c.suit == 1 || c.suit == 2) ? s.foundations[3] : s.foundations[2];
        return c.rank <= Math.Min(opp1, opp2) + 1;
    }

    private static List<KeyValuePair<MoveCommand, State>> GetNextStatesWithMoves(State s)
    {
        var next = new List<KeyValuePair<MoveCommand, State>>(24);
        int emptyFC = -1, emptyFCCount = 0;
        for (int i = 0; i < 4; i++) { if (s.freeCells[i].rank == 0) { if (emptyFC == -1) emptyFC = i; emptyFCCount++; } }
        int emptyTab = -1, emptyTabCount = 0;
        for (int i = 0; i < 8; i++) if (s.tabLens[i] == 0) { if (emptyTab == -1) emptyTab = i; emptyTabCount++; }

        for (int i = 0; i < 4; i++)
        {
            if (s.freeCells[i].rank > 0 && s.freeCells[i].suit != 255)
            {
                Card c = s.freeCells[i];
                if (s.foundations[c.suit] == c.rank - 1)
                {
                    State ns = s.Clone(); ns.foundations[c.suit] = c.rank; ns.freeCells[i].rank = 0;
                    PerformAutoPlay(ns); next.Add(new KeyValuePair<MoveCommand, State>(new MoveCommand { Type = MoveType.ToFoundation }, ns));
                }
            }
        }
        for (int i = 0; i < 8; i++)
        {
            if (s.tabLens[i] > 0)
            {
                Card c = s.tableau[i][s.tabLens[i] - 1];
                if (s.foundations[c.suit] == c.rank - 1)
                {
                    State ns = s.Clone(); ns.foundations[c.suit] = c.rank; ns.tabLens[i]--;
                    PerformAutoPlay(ns); next.Add(new KeyValuePair<MoveCommand, State>(new MoveCommand { Type = MoveType.ToFoundation }, ns));
                }
            }
        }

        if (emptyFC != -1)
        {
            for (int i = 0; i < 8; i++)
            {
                if (s.tabLens[i] > 0)
                {
                    State ns = s.Clone(); ns.freeCells[emptyFC] = ns.tableau[i][ns.tabLens[i] - 1]; ns.tabLens[i]--;
                    PerformAutoPlay(ns); next.Add(new KeyValuePair<MoveCommand, State>(new MoveCommand { Type = MoveType.ToFreeCell }, ns));
                }
            }
        }

        for (int f = 0; f < 4; f++)
        {
            if (s.freeCells[f].rank > 0 && s.freeCells[f].suit != 255)
            {
                Card c = s.freeCells[f];
                if (emptyTab != -1)
                {
                    State ns = s.Clone(); ns.tableau[emptyTab][0] = c; ns.tabLens[emptyTab] = 1; ns.freeCells[f].rank = 0;
                    PerformAutoPlay(ns); next.Add(new KeyValuePair<MoveCommand, State>(new MoveCommand { Type = MoveType.FreeCellToTab }, ns));
                }
                for (int t = 0; t < 8; t++)
                {
                    if (s.tabLens[t] > 0)
                    {
                        Card dest = s.tableau[t][s.tabLens[t] - 1];
                        if (c.IsRed != dest.IsRed && c.rank == dest.rank - 1)
                        {
                            State ns = s.Clone(); ns.tableau[t][ns.tabLens[t]] = c; ns.tabLens[t]++; ns.freeCells[f].rank = 0;
                            PerformAutoPlay(ns); next.Add(new KeyValuePair<MoveCommand, State>(new MoveCommand { Type = MoveType.FreeCellToTab }, ns));
                        }
                    }
                }
            }
        }

        for (int from = 0; from < 8; from++)
        {
            if (s.tabLens[from] == 0) continue;
            int seqLen = 1;
            for (int j = s.tabLens[from] - 2; j >= 0; j--)
            {
                Card bot = s.tableau[from][j + 1]; Card top = s.tableau[from][j];
                if (bot.IsRed != top.IsRed && bot.rank == top.rank - 1) seqLen++; else break;
            }

            for (int len = 1; len <= seqLen; len++)
            {
                Card c = s.tableau[from][s.tabLens[from] - len];
                if (emptyTab != -1)
                {
                    int limit = (1 + emptyFCCount) * (1 << (emptyTabCount - 1));
                    if (len <= limit)
                    {
                        State ns = s.Clone();
                        for (int k = 0; k < len; k++) ns.tableau[emptyTab][k] = ns.tableau[from][ns.tabLens[from] - len + k];
                        ns.tabLens[emptyTab] = len; ns.tabLens[from] -= len;
                        PerformAutoPlay(ns); next.Add(new KeyValuePair<MoveCommand, State>(new MoveCommand { Type = MoveType.TabToTab }, ns));
                    }
                }

                for (int to = 0; to < 8; to++)
                {
                    if (from == to || s.tabLens[to] == 0) continue;
                    Card dest = s.tableau[to][s.tabLens[to] - 1];
                    if (c.IsRed != dest.IsRed && c.rank == dest.rank - 1)
                    {
                        int limit = (1 + emptyFCCount) * (1 << emptyTabCount);
                        if (len <= limit)
                        {
                            State ns = s.Clone();
                            for (int k = 0; k < len; k++) ns.tableau[to][ns.tabLens[to] + k] = ns.tableau[from][ns.tabLens[from] - len + k];
                            ns.tabLens[to] += len; ns.tabLens[from] -= len;
                            PerformAutoPlay(ns); next.Add(new KeyValuePair<MoveCommand, State>(new MoveCommand { Type = MoveType.TabToTab }, ns));
                        }
                    }
                }
            }
        }
        return next;
    }

    private static State ConvertToInternal(Deal d)
    {
        State s = new State();
        for (int i = 0; i < 4; i++) s.foundations[i] = (byte)d.foundations[i].Count;
        for (int i = 0; i < d.waste.Count && i < 4; i++) s.freeCells[i] = new Card((int)d.waste[i].Card.suit, d.waste[i].Card.rank);
        for (int i = 0; i < 8; i++)
        {
            s.tabLens[i] = d.tableau[i].Count;
            for (int j = 0; j < d.tableau[i].Count; j++) s.tableau[i][j] = new Card((int)d.tableau[i][j].Card.suit, d.tableau[i][j].Card.rank);
        }
        PerformAutoPlay(s);
        return s;
    }

    private static bool IsGameWon(State s) => s.foundations[0] == 13 && s.foundations[1] == 13 && s.foundations[2] == 13 && s.foundations[3] == 13;
}