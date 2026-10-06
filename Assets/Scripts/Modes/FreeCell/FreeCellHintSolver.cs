using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct HintMoveCommand
{
    public enum MoveType { ToFoundation, ToFreeCell, FreeCellToTab, TabToTab }
    public MoveType Type;
    public int FromIndex;
    public int ToIndex;
    public int SequenceLength;
}

public class FreeCellHintSolver : MonoBehaviour
{
    // Максимальное количество состояний для проверки. A* обычно находит путь за 2000-5000 шагов.
    private const int MAX_SEARCH_NODES = 50000;
    private const float FRAME_BUDGET_MS = 12f;

    private struct CardState
    {
        public byte suit;
        public byte rank;
        public bool IsRed => suit == 1 || suit == 2;
    }

    private class FastBoard
    {
        public byte[] foundations = new byte[4];
        public CardState[] freeCells = new CardState[4];
        public CardState[,] tableau = new CardState[8, 52];
        public int[] tabLens = new int[8];

        public ulong GetHash()
        {
            ulong hash = 17;
            ulong fHash = (ulong)foundations[0] | ((ulong)foundations[1] << 4) | ((ulong)foundations[2] << 8) | ((ulong)foundations[3] << 12);
            hash = hash * 397 + fHash;

            int[] fc = new int[4];
            for (int i = 0; i < 4; i++) if (freeCells[i].rank > 0) fc[i] = (freeCells[i].suit << 4) | freeCells[i].rank;
            System.Array.Sort(fc);
            ulong fcHash = (ulong)fc[0] | ((ulong)fc[1] << 8) | ((ulong)fc[2] << 16) | ((ulong)fc[3] << 24);
            hash = hash * 397 + fcHash;

            ulong[] colHashes = new ulong[8];
            for (int i = 0; i < 8; i++)
            {
                ulong ch = 19;
                for (int j = 0; j < tabLens[i]; j++) ch = ch * 397 + (ulong)((tableau[i, j].suit << 4) | tableau[i, j].rank);
                colHashes[i] = ch;
            }
            System.Array.Sort(colHashes);
            for (int i = 0; i < 8; i++) hash = hash * 397 + colHashes[i];

            return hash;
        }

        public FastBoard Clone()
        {
            FastBoard c = new FastBoard();
            System.Array.Copy(foundations, c.foundations, 4);
            System.Array.Copy(freeCells, c.freeCells, 4);
            System.Array.Copy(tabLens, c.tabLens, 8);
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < tabLens[i]; j++) c.tableau[i, j] = tableau[i, j];
            }
            return c;
        }

        public void Apply(HintMoveCommand m)
        {
            switch (m.Type)
            {
                case HintMoveCommand.MoveType.ToFoundation:
                    if (m.FromIndex < 8)
                    {
                        var c = tableau[m.FromIndex, tabLens[m.FromIndex] - 1];
                        foundations[c.suit] = c.rank;
                        tabLens[m.FromIndex]--;
                    }
                    else
                    {
                        var c = freeCells[m.FromIndex - 8];
                        foundations[c.suit] = c.rank;
                        freeCells[m.FromIndex - 8].rank = 0;
                    }
                    break;
                case HintMoveCommand.MoveType.ToFreeCell:
                    freeCells[m.ToIndex] = tableau[m.FromIndex, tabLens[m.FromIndex] - 1];
                    tabLens[m.FromIndex]--;
                    break;
                case HintMoveCommand.MoveType.FreeCellToTab:
                    tableau[m.ToIndex, tabLens[m.ToIndex]] = freeCells[m.FromIndex];
                    tabLens[m.ToIndex]++;
                    freeCells[m.FromIndex].rank = 0;
                    break;
                case HintMoveCommand.MoveType.TabToTab:
                    int len = m.SequenceLength;
                    int start = tabLens[m.FromIndex] - len;
                    for (int k = 0; k < len; k++)
                        tableau[m.ToIndex, tabLens[m.ToIndex] + k] = tableau[m.FromIndex, start + k];
                    tabLens[m.ToIndex] += len;
                    tabLens[m.FromIndex] -= len;
                    break;
            }
        }
    }

    private class SearchNode
    {
        public FastBoard Board;
        public SearchNode Parent;
        public HintMoveCommand Move;
        public int Depth;

        public SearchNode(FastBoard b, SearchNode p, HintMoveCommand m, int d)
        {
            Board = b; Parent = p; Move = m; Depth = d;
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

    private Coroutine activeSearchCoroutine;

    public void FindPath(FreeCellPileManager pileManager, System.Action<List<HintMoveCommand>> onComplete)
    {
        CancelSearch();
        activeSearchCoroutine = StartCoroutine(FindPathAsync(pileManager, onComplete));
    }

    public void CancelSearch()
    {
        if (activeSearchCoroutine != null)
        {
            StopCoroutine(activeSearchCoroutine);
            activeSearchCoroutine = null;
        }
    }

    private IEnumerator FindPathAsync(FreeCellPileManager pileManager, System.Action<List<HintMoveCommand>> onComplete)
    {
        FastBoard rootBoard = BuildFastBoard(pileManager);

        var openSet = new PriorityQueue<SearchNode>();
        var closedSet = new HashSet<ulong>();

        openSet.Enqueue(new SearchNode(rootBoard, null, default, 0), GetHeuristic(rootBoard));

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

            if (statesVisited > MAX_SEARCH_NODES)
            {
                Debug.LogWarning($"[HintDebug] A*: Превышен лимит узлов ({MAX_SEARCH_NODES}). Тупик.");
                break;
            }

            var current = openSet.Dequeue();
            statesVisited++;

            if (IsSolved(current.Board))
            {
                winningNode = current;
                break;
            }

            ulong hash = current.Board.GetHash();
            if (closedSet.Contains(hash)) continue;
            closedSet.Add(hash);

            var moves = GenerateMoves(current.Board);
            foreach (var move in moves)
            {
                FastBoard nextBoard = current.Board.Clone();
                nextBoard.Apply(move);

                ulong nextHash = nextBoard.GetHash();
                if (!closedSet.Contains(nextHash))
                {
                    int h = GetHeuristic(nextBoard);
                    int g = current.Depth + 1;

                    // Эвристика: направляем солвер отдавать приоритет продвижению к финалу
                    int fCost = h + (g * 5);

                    openSet.Enqueue(new SearchNode(nextBoard, current, move, g), fCost);
                }
            }
        }

        List<HintMoveCommand> path = null;
        if (winningNode != null)
        {
            path = new List<HintMoveCommand>();
            var curr = winningNode;
            while (curr.Parent != null)
            {
                path.Add(curr.Move);
                curr = curr.Parent;
            }
            path.Reverse();
        }

        Debug.Log($"[HintDebug] A* Завершен! Состояний: {statesVisited}. Успех: {path != null}");
        onComplete?.Invoke(path);
    }

    private int GetHeuristic(FastBoard b)
    {
        int h = 0;
        for (int i = 0; i < 4; i++) h += (13 - b.foundations[i]) * 1000; // Огромный приоритет за каждую карту в Доме

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < b.tabLens[i]; j++)
            {
                var c = b.tableau[i, j];

                // Бонус, если мы открыли карту, которую можно забрать в Дом
                if (c.rank == b.foundations[c.suit] + 1)
                {
                    int depth = (b.tabLens[i] - 1) - j;
                    h += depth * 50;
                }

                if (j < b.tabLens[i] - 1)
                {
                    var top = b.tableau[i, j + 1];
                    // Штраф за хаос (соседние карты не собраны в ряд)
                    if (c.IsRed == top.IsRed || top.rank != c.rank - 1) h += 20;
                }
            }
        }
        return h;
    }

    private List<HintMoveCommand> GenerateMoves(FastBoard b)
    {
        var moves = new List<HintMoveCommand>(32);
        int emptyFC = 0; for (int i = 0; i < 4; i++) if (b.freeCells[i].rank == 0) emptyFC++;
        int emptyTab = 0; for (int i = 0; i < 8; i++) if (b.tabLens[i] == 0) emptyTab++;

        // 1. To Foundation
        for (int i = 0; i < 8; i++)
        {
            if (b.tabLens[i] == 0) continue;
            var c = b.tableau[i, b.tabLens[i] - 1];
            if (b.foundations[c.suit] == c.rank - 1)
                moves.Add(new HintMoveCommand { Type = HintMoveCommand.MoveType.ToFoundation, FromIndex = i, ToIndex = c.suit, SequenceLength = 1 });
        }
        for (int i = 0; i < 4; i++)
        {
            if (b.freeCells[i].rank == 0) continue;
            var c = b.freeCells[i];
            if (b.foundations[c.suit] == c.rank - 1)
                moves.Add(new HintMoveCommand { Type = HintMoveCommand.MoveType.ToFoundation, FromIndex = 8 + i, ToIndex = c.suit, SequenceLength = 1 });
        }

        // 2. FreeCell to Tab
        for (int i = 0; i < 4; i++)
        {
            if (b.freeCells[i].rank == 0) continue;
            var c = b.freeCells[i];
            bool movedToEmpty = false;

            for (int t = 0; t < 8; t++)
            {
                if (b.tabLens[t] == 0)
                {
                    if (movedToEmpty) continue; // Защита от симметрии (хватит 1 пустой ячейки)
                    movedToEmpty = true;
                    moves.Add(new HintMoveCommand { Type = HintMoveCommand.MoveType.FreeCellToTab, FromIndex = i, ToIndex = t, SequenceLength = 1 });
                }
                else
                {
                    var top = b.tableau[t, b.tabLens[t] - 1];
                    if (c.IsRed != top.IsRed && c.rank == top.rank - 1)
                        moves.Add(new HintMoveCommand { Type = HintMoveCommand.MoveType.FreeCellToTab, FromIndex = i, ToIndex = t, SequenceLength = 1 });
                }
            }
        }

        // 3. Tab to Tab (Supermoves)
        for (int from = 0; from < 8; from++)
        {
            if (b.tabLens[from] == 0) continue;
            int seqLen = 1;
            for (int j = b.tabLens[from] - 2; j >= 0; j--)
            {
                var bot = b.tableau[from, j + 1]; var top = b.tableau[from, j];
                if (bot.IsRed != top.IsRed && bot.rank == top.rank - 1) seqLen++; else break;
            }

            for (int len = 1; len <= seqLen; len++)
            {
                var c = b.tableau[from, b.tabLens[from] - len];
                bool movedToEmpty = false;

                for (int to = 0; to < 8; to++)
                {
                    if (from == to) continue;
                    bool isEmpty = b.tabLens[to] == 0;

                    if (isEmpty)
                    {
                        if (movedToEmpty) continue;
                        movedToEmpty = true;
                    }

                    int currentEmptyTabs = isEmpty ? emptyTab - 1 : emptyTab;
                    int maxDrag = (1 + emptyFC) * (1 << currentEmptyTabs);

                    if (len > maxDrag) continue;
                    if (isEmpty && len == b.tabLens[from]) continue;

                    if (isEmpty)
                    {
                        moves.Add(new HintMoveCommand { Type = HintMoveCommand.MoveType.TabToTab, FromIndex = from, ToIndex = to, SequenceLength = len });
                    }
                    else
                    {
                        var dest = b.tableau[to, b.tabLens[to] - 1];
                        if (c.IsRed != dest.IsRed && c.rank == dest.rank - 1)
                            moves.Add(new HintMoveCommand { Type = HintMoveCommand.MoveType.TabToTab, FromIndex = from, ToIndex = to, SequenceLength = len });
                    }
                }
            }
        }

        // 4. Tab to FreeCell
        if (emptyFC > 0)
        {
            int targetFC = -1;
            for (int i = 0; i < 4; i++) if (b.freeCells[i].rank == 0) { targetFC = i; break; }

            for (int i = 0; i < 8; i++)
            {
                if (b.tabLens[i] > 0)
                    moves.Add(new HintMoveCommand { Type = HintMoveCommand.MoveType.ToFreeCell, FromIndex = i, ToIndex = targetFC, SequenceLength = 1 });
            }
        }

        return moves;
    }

    private FastBoard BuildFastBoard(FreeCellPileManager pm)
    {
        FastBoard b = new FastBoard();
        for (int i = 0; i < 4; i++)
        {
            var f = pm.Foundations[i];
            if (f.Count > 0) b.foundations[(int)f.GetTopCard().cardModel.suit] = (byte)f.GetTopCard().cardModel.rank;
        }
        for (int i = 0; i < 4; i++)
        {
            if (!pm.FreeCells[i].IsEmpty)
            {
                var c = pm.FreeCells[i].GetComponentInChildren<CardController>();
                b.freeCells[i] = new CardState { suit = (byte)c.cardModel.suit, rank = (byte)c.cardModel.rank };
            }
        }
        for (int i = 0; i < 8; i++)
        {
            var tab = pm.Tableau[i];
            b.tabLens[i] = tab.cards.Count;
            for (int j = 0; j < tab.cards.Count; j++)
                b.tableau[i, j] = new CardState { suit = (byte)tab.cards[j].cardModel.suit, rank = (byte)tab.cards[j].cardModel.rank };
        }
        return b;
    }

    private bool IsSolved(FastBoard b) => b.foundations[0] == 13 && b.foundations[1] == 13 && b.foundations[2] == 13 && b.foundations[3] == 13;
}