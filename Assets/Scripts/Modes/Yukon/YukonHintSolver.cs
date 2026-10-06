using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;

public struct YukonHintMove
{
    public enum MoveType { ToFoundation, TabToTab }
    public MoveType Type;
    public int FromIndex;
    public int ToIndex;
    public int SequenceLength;
}

public class YukonHintSolver : MonoBehaviour
{
    private const int MAX_SEARCH_NODES = 250000;
    private const int MAX_DEPTH = 300;
    private const float FRAME_BUDGET_MS = 15f;

    private struct CardState
    {
        public byte suit;
        public byte rank;
        public bool faceUp;
        public bool IsRed => suit == 1 || suit == 2;
    }

    private class FastBoard
    {
        public byte[] foundations = new byte[4];
        public CardState[,] tableau = new CardState[7, 52];
        public int[] tabLens = new int[7];
        public int variantParam; // 0 = Classic, 1 = Russian

        public ulong GetHash()
        {
            ulong hash = 17;
            ulong fHash = (ulong)foundations[0] | ((ulong)foundations[1] << 4) | ((ulong)foundations[2] << 8) | ((ulong)foundations[3] << 12);
            hash = hash * 397 + fHash;

            // ИСПРАВЛЕНИЕ: Никакой сортировки! Колонки в Юконе не симметричны из-за скрытых карт
            for (int i = 0; i < 7; i++)
            {
                hash = hash * 397 + (ulong)tabLens[i];
                for (int j = 0; j < tabLens[i]; j++)
                {
                    if (tableau[i, j].faceUp)
                        hash = hash * 397 + (ulong)((tableau[i, j].suit << 4) | tableau[i, j].rank);
                }
            }
            return hash;
        }

        public FastBoard Clone()
        {
            FastBoard c = new FastBoard();
            c.variantParam = this.variantParam;
            Array.Copy(foundations, c.foundations, 4);
            Array.Copy(tabLens, c.tabLens, 7);
            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < tabLens[i]; j++) c.tableau[i, j] = tableau[i, j];
            }
            return c;
        }

        public void Apply(YukonHintMove m)
        {
            if (m.Type == YukonHintMove.MoveType.ToFoundation)
            {
                var c = tableau[m.FromIndex, tabLens[m.FromIndex] - 1];
                foundations[c.suit] = c.rank;
                tabLens[m.FromIndex]--;

                // Авто-вскрытие нижней карты
                if (tabLens[m.FromIndex] > 0 && !tableau[m.FromIndex, tabLens[m.FromIndex] - 1].faceUp)
                    tableau[m.FromIndex, tabLens[m.FromIndex] - 1].faceUp = true;
            }
            else
            {
                int start = tabLens[m.FromIndex] - m.SequenceLength;
                for (int k = 0; k < m.SequenceLength; k++)
                    tableau[m.ToIndex, tabLens[m.ToIndex] + k] = tableau[m.FromIndex, start + k];

                tabLens[m.ToIndex] += m.SequenceLength;
                tabLens[m.FromIndex] -= m.SequenceLength;

                // Авто-вскрытие нижней карты
                if (tabLens[m.FromIndex] > 0 && !tableau[m.FromIndex, tabLens[m.FromIndex] - 1].faceUp)
                    tableau[m.FromIndex, tabLens[m.FromIndex] - 1].faceUp = true;
            }
        }
    }

    private struct ScoredMove : IComparable<ScoredMove>
    {
        public YukonHintMove Move;
        public int Score;
        public int CompareTo(ScoredMove other) => other.Score.CompareTo(this.Score); // По убыванию (лучшие в начале)
    }

    private class SearchNode
    {
        public FastBoard Board;
        public SearchNode Parent;
        public YukonHintMove Move;
        public int Depth;

        public YukonHintMove[] Moves;
        public int MoveIndex;

        public SearchNode(FastBoard b, SearchNode p, YukonHintMove m, int d)
        {
            Board = b; Parent = p; Move = m; Depth = d;
            Moves = null; MoveIndex = 0;
        }
    }

    private Coroutine activeSearchCoroutine;

    public void FindPath(List<YukonTableauPile> pmTableaus, List<FoundationPile> pmFoundations, int variantParam, Action<List<YukonHintMove>> onComplete)
    {
        CancelSearch();
        activeSearchCoroutine = StartCoroutine(FindPathAsync(pmTableaus, pmFoundations, variantParam, onComplete));
    }

    public void CancelSearch()
    {
        if (activeSearchCoroutine != null)
        {
            StopCoroutine(activeSearchCoroutine);
            activeSearchCoroutine = null;
        }
    }

    private IEnumerator FindPathAsync(List<YukonTableauPile> pmTableaus, List<FoundationPile> pmFoundations, int variantParam, Action<List<YukonHintMove>> onComplete)
    {
        FastBoard rootBoard = new FastBoard();
        rootBoard.variantParam = variantParam;

        for (int i = 0; i < 4; i++)
        {
            var f = pmFoundations[i];
            if (f.cards.Count > 0) rootBoard.foundations[(int)f.cards[f.cards.Count - 1].cardModel.suit] = (byte)f.cards[f.cards.Count - 1].cardModel.rank;
        }

        for (int i = 0; i < 7; i++)
        {
            var tab = pmTableaus[i];
            rootBoard.tabLens[i] = tab.cards.Count;
            for (int j = 0; j < tab.cards.Count; j++)
            {
                var model = tab.cards[j].cardModel;
                rootBoard.tableau[i, j] = new CardState { suit = (byte)model.suit, rank = (byte)model.rank, faceUp = tab.faceUp[j] };
            }
        }

        var visited = new HashSet<ulong>();
        var stack = new Stack<SearchNode>(); // Используем DFS (как в генераторе)

        stack.Push(new SearchNode(rootBoard, null, default, 0));
        visited.Add(rootBoard.GetHash());

        int statesVisited = 0;
        SearchNode winningNode = null;
        Stopwatch sw = Stopwatch.StartNew();

        ScoredMove[] moveBuffer = new ScoredMove[128];

        while (stack.Count > 0)
        {
            if (sw.ElapsedMilliseconds > FRAME_BUDGET_MS)
            {
                yield return null;
                sw.Restart();
            }

            if (statesVisited > MAX_SEARCH_NODES)
            {
                UnityEngine.Debug.LogWarning($"[HintDebug] Yukon DFS: Превышен лимит узлов ({MAX_SEARCH_NODES}).");
                break;
            }

            SearchNode current = stack.Peek();

            if (IsSolved(current.Board))
            {
                winningNode = current;
                break;
            }

            if (current.Depth >= MAX_DEPTH)
            {
                stack.Pop();
                continue;
            }

            if (current.Moves == null)
            {
                statesVisited++;
                int count = GenerateMoves(current.Board, moveBuffer);

                current.Moves = new YukonHintMove[count];
                for (int i = 0; i < count; i++) current.Moves[i] = moveBuffer[i].Move;
                current.MoveIndex = 0;
            }

            if (current.MoveIndex < current.Moves.Length)
            {
                var move = current.Moves[current.MoveIndex++];

                FastBoard nextBoard = current.Board.Clone();
                nextBoard.Apply(move);

                ulong hash = nextBoard.GetHash();
                if (!visited.Contains(hash))
                {
                    visited.Add(hash);
                    stack.Push(new SearchNode(nextBoard, current, move, current.Depth + 1));
                }
            }
            else
            {
                stack.Pop();
            }
        }

        List<YukonHintMove> path = null;
        if (winningNode != null)
        {
            path = new List<YukonHintMove>();
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

    private int GenerateMoves(FastBoard b, ScoredMove[] buffer)
    {
        int count = 0;

        // 1. В Дом (наивысший приоритет)
        for (int i = 0; i < 7; i++)
        {
            if (b.tabLens[i] == 0) continue;
            var c = b.tableau[i, b.tabLens[i] - 1];
            if (c.faceUp && b.foundations[c.suit] == c.rank - 1)
            {
                buffer[count++] = new ScoredMove
                {
                    Move = new YukonHintMove { Type = YukonHintMove.MoveType.ToFoundation, FromIndex = i, ToIndex = c.suit, SequenceLength = 1 },
                    Score = 100
                };
            }
        }

        // 2. Межстолбцовые ходы
        for (int src = 0; src < 7; src++)
        {
            if (b.tabLens[src] == 0) continue;

            for (int cardIdx = 0; cardIdx < b.tabLens[src]; cardIdx++)
            {
                var c = b.tableau[src, cardIdx];
                if (!c.faceUp) continue;

                int seqLen = b.tabLens[src] - cardIdx;
                bool exposesHidden = (cardIdx > 0 && !b.tableau[src, cardIdx - 1].faceUp);

                for (int dst = 0; dst < 7; dst++)
                {
                    if (src == dst) continue;

                    bool isEmpty = b.tabLens[dst] == 0;
                    if (isEmpty)
                    {
                        if (c.rank == 13 && cardIdx > 0)
                        {
                            buffer[count++] = new ScoredMove
                            {
                                Move = new YukonHintMove { Type = YukonHintMove.MoveType.TabToTab, FromIndex = src, ToIndex = dst, SequenceLength = seqLen },
                                Score = exposesHidden ? 50 : 10
                            };
                        }
                    }
                    else
                    {
                        var targetTop = b.tableau[dst, b.tabLens[dst] - 1];
                        if (targetTop.rank == c.rank + 1)
                        {
                            bool suitMatch = b.variantParam == 1 ? (targetTop.suit == c.suit) : (targetTop.IsRed != c.IsRed);
                            if (suitMatch)
                            {
                                buffer[count++] = new ScoredMove
                                {
                                    Move = new YukonHintMove { Type = YukonHintMove.MoveType.TabToTab, FromIndex = src, ToIndex = dst, SequenceLength = seqLen },
                                    Score = exposesHidden ? 50 : 10
                                };
                            }
                        }
                    }
                }
            }
        }

        // Жадная сортировка для DFS (высший Score встает в начало массива и проверяется первым)
        Array.Sort(buffer, 0, count);
        return count;
    }

    private bool IsSolved(FastBoard b) => b.foundations[0] == 13 && b.foundations[1] == 13 && b.foundations[2] == 13 && b.foundations[3] == 13;
}