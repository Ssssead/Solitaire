using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TriPeaksHintMove
{
    public enum MoveType { PlayTableau, DrawStock }
    public MoveType Type;
    public int TableauIndex;
}

public class TriPeaksHintSolver : MonoBehaviour
{
    private const int MAX_STATES = 200000;
    private const float FRAME_BUDGET_MS = 12f;
    private const bool ALLOW_KING_ACE_WRAP = true;

    private static readonly uint[] bMasks = new uint[28];
    static TriPeaksHintSolver()
    {
        bMasks[9] = (1u << 18) | (1u << 19); bMasks[10] = (1u << 19) | (1u << 20); bMasks[11] = (1u << 20) | (1u << 21);
        bMasks[12] = (1u << 21) | (1u << 22); bMasks[13] = (1u << 22) | (1u << 23); bMasks[14] = (1u << 23) | (1u << 24);
        bMasks[15] = (1u << 24) | (1u << 25); bMasks[16] = (1u << 25) | (1u << 26); bMasks[17] = (1u << 26) | (1u << 27);
        bMasks[3] = (1u << 9) | (1u << 10); bMasks[4] = (1u << 10) | (1u << 11); bMasks[5] = (1u << 12) | (1u << 13);
        bMasks[6] = (1u << 13) | (1u << 14); bMasks[7] = (1u << 15) | (1u << 16); bMasks[8] = (1u << 16) | (1u << 17);
        bMasks[0] = (1u << 3) | (1u << 4); bMasks[1] = (1u << 5) | (1u << 6); bMasks[2] = (1u << 7) | (1u << 8);
    }

    private struct State
    {
        public uint TableauMask;
        public int Cursor;
        public int Rank;
        public ulong Hash => TableauMask | ((ulong)Cursor << 28) | ((ulong)Rank << 34);
    }

    private class SearchNode
    {
        public State State;
        public SearchNode Parent;
        public TriPeaksHintMove Move;
        public int Depth;
        public SearchNode(State s, SearchNode p, TriPeaksHintMove m, int d) { State = s; Parent = p; Move = m; Depth = d; }
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

    private Coroutine activeSearchCoroutine;

    public void FindPath(TriPeaksPileManager pileManager, int currentWasteRank, Action<List<TriPeaksHintMove>> onComplete)
    {
        CancelSearch();
        activeSearchCoroutine = StartCoroutine(FindPathAsync(pileManager, currentWasteRank, onComplete));
    }

    public void CancelSearch()
    {
        if (activeSearchCoroutine != null)
        {
            StopCoroutine(activeSearchCoroutine);
            activeSearchCoroutine = null;
        }
    }

    private IEnumerator FindPathAsync(TriPeaksPileManager pileManager, int currentWasteRank, Action<List<TriPeaksHintMove>> onComplete)
    {
        int[] tRanks = new int[28];
        uint initialMask = 0;

        for (int i = 0; i < 28; i++)
        {
            var slot = pileManager.TableauPiles[i];
            if (slot.HasCard)
            {
                tRanks[i] = slot.CurrentCard.cardModel.rank;
                initialMask |= (1u << i);
            }
        }

        var stockCards = pileManager.Stock.GetCards();
        int[] sRanks = new int[stockCards.Count];

        // Stock достает карты с конца списка
        for (int i = 0; i < stockCards.Count; i++)
        {
            sRanks[i] = stockCards[stockCards.Count - 1 - i].cardModel.rank;
        }

        // Просто используем переданный ранг напрямую
        int startRank = currentWasteRank;

        State rootState = new State { TableauMask = initialMask, Cursor = 0, Rank = startRank };
        var openSet = new PriorityQueue<SearchNode>();
        var closedSet = new HashSet<ulong>();

        openSet.Enqueue(new SearchNode(rootState, null, default, 0), PopCount(initialMask) * 100);

        int statesVisited = 0;
        SearchNode winningNode = null;
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        while (openSet.Count > 0)
        {
            if (sw.ElapsedMilliseconds > FRAME_BUDGET_MS)
            {
                yield return null;
                sw.Restart();
            }

            if (statesVisited > MAX_STATES)
            {
                Debug.LogWarning($"[HintDebug] TriPeaks: Лимит состояний {MAX_STATES}. Тупик.");
                break;
            }

            var current = openSet.Dequeue();
            statesVisited++;

            if (current.State.TableauMask == 0)
            {
                winningNode = current;
                break;
            }

            ulong hash = current.State.Hash;
            if (closedSet.Contains(hash)) continue;
            closedSet.Add(hash);

            uint tMask = current.State.TableauMask;
            int rank = current.State.Rank;
            int cursor = current.State.Cursor;

            // 1. Попытка убрать карту со стола
            for (int i = 0; i < 28; i++)
            {
                if ((tMask & (1u << i)) != 0 && (tMask & bMasks[i]) == 0)
                {
                    int d = Mathf.Abs(rank - tRanks[i]);
                    bool match = ALLOW_KING_ACE_WRAP ? (d == 1 || d == 12) : (d == 1);
                    if (match)
                    {
                        State next = new State { TableauMask = tMask & ~(1u << i), Cursor = cursor, Rank = tRanks[i] };
                        if (!closedSet.Contains(next.Hash))
                        {
                            int f = PopCount(next.TableauMask) * 100 + next.Cursor * 5 + current.Depth + 1;
                            openSet.Enqueue(new SearchNode(next, current, new TriPeaksHintMove { Type = TriPeaksHintMove.MoveType.PlayTableau, TableauIndex = i }, current.Depth + 1), f);
                        }
                    }
                }
            }

            // 2. Если можно - берем из колоды
            if (cursor < sRanks.Length)
            {
                State next = new State { TableauMask = tMask, Cursor = cursor + 1, Rank = sRanks[cursor] };
                if (!closedSet.Contains(next.Hash))
                {
                    int f = PopCount(next.TableauMask) * 100 + next.Cursor * 5 + current.Depth + 1;
                    openSet.Enqueue(new SearchNode(next, current, new TriPeaksHintMove { Type = TriPeaksHintMove.MoveType.DrawStock }, current.Depth + 1), f);
                }
            }
        }

        List<TriPeaksHintMove> path = null;
        if (winningNode != null)
        {
            path = new List<TriPeaksHintMove>();
            var curr = winningNode;
            while (curr.Parent != null)
            {
                path.Add(curr.Move);
                curr = curr.Parent;
            }
            path.Reverse();
        }

        onComplete?.Invoke(path);
    }

    private static int PopCount(uint i)
    {
        i = i - ((i >> 1) & 0x55555555);
        i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
        return (int)((((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
    }
}