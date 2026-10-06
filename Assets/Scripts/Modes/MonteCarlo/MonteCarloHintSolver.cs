using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct MonteCarloHintMove
{
    public int Idx1;
    public int Idx2;
}

public class MonteCarloHintSolver : MonoBehaviour
{
    private const int MAX_SEARCH_NODES = 200000;
    private const float FRAME_BUDGET_MS = 12f;

    private class FastBoard
    {
        public byte[] board = new byte[25]; // 0 значит пусто
        public int stockIdx;
        public byte[] fullDeck;
        public bool is8Ways;

        public ulong GetHash()
        {
            ulong hash = 17;
            for (int i = 0; i < 25; i++) hash = hash * 397 + board[i];
            return hash;
        }

        public FastBoard Clone()
        {
            FastBoard c = new FastBoard();
            Array.Copy(this.board, c.board, 25);
            c.stockIdx = this.stockIdx;
            c.fullDeck = this.fullDeck;
            c.is8Ways = this.is8Ways;
            return c;
        }

        public void Apply(MonteCarloHintMove m)
        {
            int remCount = 0;
            byte[] temp = new byte[25];

            // Собираем все оставшиеся карты
            for (int i = 0; i < 25; i++)
            {
                if (board[i] != 0 && i != m.Idx1 && i != m.Idx2)
                {
                    temp[remCount++] = board[i];
                }
            }

            int emptySlotsCount = 25 - remCount;

            // Сдвигаем оставшиеся карты в конец доски (как в игре)
            for (int i = 0; i < remCount; i++)
            {
                board[emptySlotsCount + i] = temp[i];
            }

            // Раздаем новые карты из колоды в освободившиеся ячейки в начале
            for (int i = emptySlotsCount - 1; i >= 0; i--)
            {
                if (stockIdx < fullDeck.Length)
                {
                    board[i] = fullDeck[stockIdx++];
                }
                else
                {
                    board[i] = 0;
                }
            }
        }
    }

    private class SearchNode
    {
        public FastBoard Board;
        public SearchNode Parent;
        public MonteCarloHintMove Move;
        public int Depth;
        public List<MonteCarloHintMove> Moves;
        public int MoveIndex;

        public SearchNode(FastBoard b, SearchNode p, MonteCarloHintMove m, int d)
        {
            Board = b; Parent = p; Move = m; Depth = d;
            Moves = null; MoveIndex = 0;
        }
    }

    private Coroutine activeSearchCoroutine;

    public void FindPath(MonteCarloPileManager pm, bool is8Ways, Action<List<MonteCarloHintMove>> onComplete)
    {
        CancelSearch();
        activeSearchCoroutine = StartCoroutine(FindPathAsync(pm, is8Ways, onComplete));
    }

    public void CancelSearch()
    {
        if (activeSearchCoroutine != null)
        {
            StopCoroutine(activeSearchCoroutine);
            activeSearchCoroutine = null;
        }
    }

    private IEnumerator FindPathAsync(MonteCarloPileManager pm, bool is8Ways, Action<List<MonteCarloHintMove>> onComplete)
    {
        FastBoard root = new FastBoard();
        root.is8Ways = is8Ways;

        // Колода раздается с конца списка (как в игре)
        root.fullDeck = new byte[pm.StockCards.Count];
        for (int i = 0; i < pm.StockCards.Count; i++)
        {
            var card = pm.StockCards[pm.StockCards.Count - 1 - i];
            root.fullDeck[i] = (byte)((int)card.cardModel.suit * 13 + card.cardModel.rank);
        }
        root.stockIdx = 0;

        for (int i = 0; i < 25; i++)
        {
            if (pm.BoardCards[i] != null)
            {
                var card = pm.BoardCards[i];
                root.board[i] = (byte)((int)card.cardModel.suit * 13 + card.cardModel.rank);
            }
            else
            {
                root.board[i] = 0;
            }
        }

        var stack = new Stack<SearchNode>();
        var closedSet = new HashSet<ulong>();

        stack.Push(new SearchNode(root, null, default, 0));
        closedSet.Add(root.GetHash());

        int statesVisited = 0;
        SearchNode winningNode = null;

        // ИСПРАВЛЕНИЕ: Отслеживаем самый глубокий найденный путь
        SearchNode deepestNode = null;
        int maxDepth = -1;

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        while (stack.Count > 0)
        {
            if (sw.ElapsedMilliseconds > FRAME_BUDGET_MS)
            {
                yield return null;
                sw.Restart();
            }

            var current = stack.Peek();

            // Запоминаем рекордсмена по глубине
            if (current.Depth > maxDepth)
            {
                maxDepth = current.Depth;
                deepestNode = current;
            }

            if (statesVisited > MAX_SEARCH_NODES)
            {
                Debug.LogWarning($"[HintDebug] MonteCarlo DFS: Лимит ({MAX_SEARCH_NODES}). Выдаем лучший найденный ход.");
                winningNode = deepestNode; // Спасительный фоллбэк
                break;
            }

            bool isEmpty = true;
            for (int i = 0; i < 25; i++) if (current.Board.board[i] != 0) { isEmpty = false; break; }

            if (isEmpty)
            {
                winningNode = current;
                break;
            }

            if (current.Moves == null)
            {
                statesVisited++;
                current.Moves = GenerateMoves(current.Board);
            }

            if (current.MoveIndex < current.Moves.Count)
            {
                var move = current.Moves[current.MoveIndex++];

                FastBoard nextBoard = current.Board.Clone();
                nextBoard.Apply(move);

                ulong hash = nextBoard.GetHash();
                if (!closedSet.Contains(hash))
                {
                    closedSet.Add(hash);
                    stack.Push(new SearchNode(nextBoard, current, move, current.Depth + 1));
                }
            }
            else
            {
                stack.Pop();
            }
        }

        List<MonteCarloHintMove> path = null;
        if (winningNode != null)
        {
            path = new List<MonteCarloHintMove>();
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

    private List<MonteCarloHintMove> GenerateMoves(FastBoard b)
    {
        var moves = new List<MonteCarloHintMove>();

        for (int i = 0; i < 25; i++)
        {
            if (b.board[i] == 0) continue;

            for (int j = i + 1; j < 25; j++)
            {
                if (b.board[j] == 0) continue;

                int rankA = (b.board[i] - 1) % 13 + 1;
                int rankB = (b.board[j] - 1) % 13 + 1;

                if (rankA == rankB && IsAdjacent(i, j, b.is8Ways))
                {
                    moves.Add(new MonteCarloHintMove { Idx1 = i, Idx2 = j });
                }
            }
        }

        // Жадная сортировка для быстрого нахождения пути
        moves.Sort((m1, m2) => GetScore(m2, b.is8Ways).CompareTo(GetScore(m1, b.is8Ways)));

        return moves;
    }

    private bool IsAdjacent(int idx1, int idx2, bool is8Ways)
    {
        int r1 = idx1 / 5, c1 = idx1 % 5;
        int r2 = idx2 / 5, c2 = idx2 % 5;
        int rDiff = Mathf.Abs(r1 - r2);
        int cDiff = Mathf.Abs(c1 - c2);

        if (is8Ways) return rDiff <= 1 && cDiff <= 1;
        else return (rDiff == 1 && cDiff == 0) || (rDiff == 0 && cDiff == 1);
    }

    private int GetScore(MonteCarloHintMove move, bool is8Ways)
    {
        int i = move.Idx1;
        int j = move.Idx2;
        int score = (25 - i) * 50; // Бонус за сдвиг с левого верхнего угла

        int r1 = i / 5, c1 = i % 5;
        int r2 = j / 5, c2 = j % 5;
        int rd = Mathf.Abs(r1 - r2);
        int cd = Mathf.Abs(c1 - c2);

        if (rd == 0 && cd == 1) score += 300;
        else if (rd == 1 && cd == 0) score += 200;
        else score += 50;

        int span = Math.Abs(j - i);
        if (span > 1 && span <= 5) score += span * 15;

        return score;
    }
}