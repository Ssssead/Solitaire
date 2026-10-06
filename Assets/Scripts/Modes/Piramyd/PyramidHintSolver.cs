using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct PyramidHintMove
{
    public enum MoveType { RemoveKing, RemovePair, Deal, Recycle }
    public MoveType Type;
    public CardController CardA;
    public CardController CardB;
}

public class PyramidHintSolver : MonoBehaviour
{
    private const int MAX_STATES = 150000;
    private const float FRAME_BUDGET_MS = 12f;

    public struct State : IEquatable<State>
    {
        public uint TableauMask;
        public uint StockMask;
        public byte Cursor;
        public byte Recycles;

        public ulong GetHash() => TableauMask | ((ulong)StockMask << 28) | ((ulong)Cursor << 52) | ((ulong)Recycles << 58);
        public bool Equals(State other) => TableauMask == other.TableauMask && StockMask == other.StockMask && Cursor == other.Cursor && Recycles == other.Recycles;
        public override int GetHashCode() => GetHash().GetHashCode();
    }

    private struct MoveInfo
    {
        public State NextState;
        public PyramidHintMove.MoveType Type;
        public int IdxA;
        public int IdxB;
    }

    private class SearchNode
    {
        public State State;
        public SearchNode Parent;
        public MoveInfo Move;
        public int Depth;
        public SearchNode(State s, SearchNode p, MoveInfo m, int d) { State = s; Parent = p; Move = m; Depth = d; }
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

    private static readonly uint[] BlockingMasks = new uint[28];
    static PyramidHintSolver()
    {
        int cardIndex = 0;
        for (int row = 0; row < 7; row++)
        {
            for (int col = 0; col <= row; col++)
            {
                uint mask = 0;
                if (row < 6) mask = (1u << (cardIndex + row + 1)) | (1u << (cardIndex + row + 2));
                BlockingMasks[cardIndex] = mask;
                cardIndex++;
            }
        }
    }

    private Coroutine activeSearchCoroutine;

    public void FindPath(PyramidPileManager pileManager, int recyclesRemaining, int maxRecycles, Action<List<PyramidHintMove>> onComplete)
    {
        CancelSearch();
        activeSearchCoroutine = StartCoroutine(FindPathAsync(pileManager, recyclesRemaining, maxRecycles, onComplete));
    }

    public void CancelSearch()
    {
        if (activeSearchCoroutine != null)
        {
            StopCoroutine(activeSearchCoroutine);
            activeSearchCoroutine = null;
        }
    }

    private IEnumerator FindPathAsync(PyramidPileManager pileManager, int recyclesRemaining, int maxRecycles, Action<List<PyramidHintMove>> onComplete)
    {
        CardController[] tabCards = new CardController[28];
        int[] ranks = new int[52];
        uint initialTabMask = 0;

        for (int i = 0; i < 28; i++)
        {
            var slot = pileManager.TableauSlots[i];
            if (slot.Card != null)
            {
                tabCards[i] = slot.Card;
                ranks[i] = slot.Card.cardModel.rank;
                initialTabMask |= (1u << i);
            }
        }

        List<CardController> listCards = new List<CardController>();
        var wasteCards = pileManager.Waste.GetCards();
        foreach (var c in wasteCards) listCards.Add(c);

        var stockCards = pileManager.Stock.GetCards();
        for (int i = stockCards.Count - 1; i >= 0; i--) listCards.Add(stockCards[i]);

        uint initialStockMask = (1u << listCards.Count) - 1;
        byte initialCursor = (byte)wasteCards.Count;
        byte initialRecycles = (byte)(maxRecycles - recyclesRemaining);

        for (int i = 0; i < listCards.Count; i++) ranks[28 + i] = listCards[i].cardModel.rank;

        State rootState = new State { TableauMask = initialTabMask, StockMask = initialStockMask, Cursor = initialCursor, Recycles = initialRecycles };

        var openSet = new PriorityQueue<SearchNode>();
        var closedSet = new HashSet<ulong>();
        openSet.Enqueue(new SearchNode(rootState, null, default, 0), PopCount(initialTabMask) * 10);

        int statesVisited = 0;
        SearchNode winningNode = null;
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        MoveInfo[] moveBuffer = new MoveInfo[128];
        int[] expBuffer = new int[28];

        while (openSet.Count > 0)
        {
            if (sw.ElapsedMilliseconds > FRAME_BUDGET_MS)
            {
                yield return null;
                sw.Restart();
            }

            if (statesVisited > MAX_STATES)
            {
                Debug.LogWarning($"[PyramidHint] Достигнут лимит {MAX_STATES}. Путь не найден.");
                break;
            }

            var current = openSet.Dequeue();
            statesVisited++;

            if (current.State.TableauMask == 0)
            {
                winningNode = current;
                break;
            }

            ulong hash = current.State.GetHash();
            if (closedSet.Contains(hash)) continue;
            closedSet.Add(hash);

            int moveCount = GenerateMoves(current.State, ranks, maxRecycles, moveBuffer, expBuffer);
            for (int i = 0; i < moveCount; i++)
            {
                var m = moveBuffer[i];
                if (!closedSet.Contains(m.NextState.GetHash()))
                {
                    int h = PopCount(m.NextState.TableauMask) * 10 + m.NextState.Recycles * 5;
                    int f = current.Depth + 1 + h;
                    openSet.Enqueue(new SearchNode(m.NextState, current, m, current.Depth + 1), f);
                }
            }
        }

        List<PyramidHintMove> path = null;
        if (winningNode != null)
        {
            path = new List<PyramidHintMove>();
            var curr = winningNode;
            while (curr.Parent != null)
            {
                var info = curr.Move;
                var hm = new PyramidHintMove { Type = info.Type };
                if (info.Type == PyramidHintMove.MoveType.RemoveKing || info.Type == PyramidHintMove.MoveType.RemovePair)
                {
                    hm.CardA = GetCard(info.IdxA, tabCards, listCards);
                    if (info.Type == PyramidHintMove.MoveType.RemovePair) hm.CardB = GetCard(info.IdxB, tabCards, listCards);
                }
                path.Add(hm);
                curr = curr.Parent;
            }
            path.Reverse();
        }

        onComplete?.Invoke(path);
    }

    private CardController GetCard(int idx, CardController[] tabCards, List<CardController> listCards)
    {
        if (idx < 28) return tabCards[idx];
        return listCards[idx - 28];
    }

    private static int PopCount(uint i)
    {
        i = i - ((i >> 1) & 0x55555555);
        i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
        return (int)((((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
    }

    private int GenerateMoves(State st, int[] ranks, int maxRecycles, MoveInfo[] outMoves, int[] exp)
    {
        int moveCount = 0;
        int expCount = 0;
        for (int i = 0; i < 28; i++)
            if ((st.TableauMask & (1u << i)) != 0 && (st.TableauMask & BlockingMasks[i]) == 0) exp[expCount++] = i;

        int w = -1;
        for (int i = st.Cursor - 1; i >= 0; i--) { if ((st.StockMask & (1u << i)) != 0) { w = i; break; } }
        int s = -1;
        for (int i = st.Cursor; i < 24; i++) { if ((st.StockMask & (1u << i)) != 0) { s = i; break; } }

        for (int i = 0; i < expCount; i++)
        {
            if (ranks[exp[i]] == 13)
            {
                State next = st; next.TableauMask &= ~(1u << exp[i]);
                outMoves[0] = new MoveInfo { NextState = next, Type = PyramidHintMove.MoveType.RemoveKing, IdxA = exp[i] };
                return 1;
            }
        }

        if (w != -1 && ranks[28 + w] == 13)
        {
            State next = st; next.StockMask &= ~(1u << w);
            outMoves[0] = new MoveInfo { NextState = next, Type = PyramidHintMove.MoveType.RemoveKing, IdxA = 28 + w };
            return 1;
        }
        if (s != -1 && ranks[28 + s] == 13)
        {
            State next = st; next.StockMask &= ~(1u << s);
            outMoves[0] = new MoveInfo { NextState = next, Type = PyramidHintMove.MoveType.RemoveKing, IdxA = 28 + s };
            return 1;
        }

        for (int i = 0; i < expCount; i++)
            for (int j = i + 1; j < expCount; j++)
                if (ranks[exp[i]] + ranks[exp[j]] == 13)
                {
                    State next = st; next.TableauMask &= ~(1u << exp[i]); next.TableauMask &= ~(1u << exp[j]);
                    outMoves[moveCount++] = new MoveInfo { NextState = next, Type = PyramidHintMove.MoveType.RemovePair, IdxA = exp[i], IdxB = exp[j] };
                }

        if (w != -1)
            for (int i = 0; i < expCount; i++)
                if (ranks[exp[i]] + ranks[28 + w] == 13)
                {
                    State next = st; next.TableauMask &= ~(1u << exp[i]); next.StockMask &= ~(1u << w);
                    outMoves[moveCount++] = new MoveInfo { NextState = next, Type = PyramidHintMove.MoveType.RemovePair, IdxA = exp[i], IdxB = 28 + w };
                }

        if (s != -1)
            for (int i = 0; i < expCount; i++)
                if (ranks[exp[i]] + ranks[28 + s] == 13)
                {
                    State next = st; next.TableauMask &= ~(1u << exp[i]); next.StockMask &= ~(1u << s);
                    outMoves[moveCount++] = new MoveInfo { NextState = next, Type = PyramidHintMove.MoveType.RemovePair, IdxA = exp[i], IdxB = 28 + s };
                }

        if (w != -1 && s != -1 && ranks[28 + w] + ranks[28 + s] == 13)
        {
            State next = st; next.StockMask &= ~(1u << w); next.StockMask &= ~(1u << s);
            outMoves[moveCount++] = new MoveInfo { NextState = next, Type = PyramidHintMove.MoveType.RemovePair, IdxA = 28 + w, IdxB = 28 + s };
        }

        if (s != -1)
        {
            State next = st; next.Cursor = (byte)(s + 1);
            outMoves[moveCount++] = new MoveInfo { NextState = next, Type = PyramidHintMove.MoveType.Deal };
        }
        else if (st.Recycles < maxRecycles && st.Cursor > 0)
        {
            State next = st; next.Cursor = 0; next.Recycles++;
            outMoves[moveCount++] = new MoveInfo { NextState = next, Type = PyramidHintMove.MoveType.Recycle };
        }

        return moveCount;
    }
}