using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct SultanHintMove
{
    public enum MoveType { WasteToFoundation, ReserveToFoundation, WasteToReserve, DrawStock, Recycle }
    public MoveType Type;
    public int FromIndex; // 0-5 (для резерва)
    public int ToIndex;   // 0-7 (для фундаментов) или 0-5 (для резерва)
    public int Priority;
}

public class SultanHintSolver : MonoBehaviour
{
    private const int MAX_SEARCH_NODES = 100000;
    private const float FRAME_BUDGET_MS = 12f;

    private class FastBoard
    {
        public byte[] foundSuits = new byte[8];
        public byte[] foundRanks = new byte[8];

        public byte[] reserves = new byte[6]; // 0 = пусто, иначе (suit * 16 + rank)

        public byte[] waste = new byte[104];
        public int wasteCount = 0;

        public byte[] stock = new byte[104];
        public int stockCount = 0;

        public int recyclesUsed = 0;

        public ulong GetHash()
        {
            ulong hash = 17;
            for (int i = 0; i < 8; i++) hash = hash * 397 + foundRanks[i];

            // Симметрия резервов: сортируем, чтобы перестановка карт в резервах не считалась за новый стейт
            ulong[] resArr = new ulong[6];
            for (int i = 0; i < 6; i++) resArr[i] = reserves[i];
            Array.Sort(resArr);
            for (int i = 0; i < 6; i++) hash = hash * 397 + resArr[i];

            hash = hash * 397 + (ulong)wasteCount;

            // ИСПРАВЛЕНИЕ: Хэшируем весь массив сброса, а не только верхнюю карту!
            // Это полностью исключает ложные "тупики" при перелистывании колоды.
            for (int i = 0; i < wasteCount; i++) hash = hash * 397 + waste[i];

            hash = hash * 397 + (ulong)stockCount;
            hash = hash * 397 + (ulong)recyclesUsed;

            return hash;
        }

        public FastBoard Clone()
        {
            FastBoard c = new FastBoard();
            Array.Copy(foundSuits, c.foundSuits, 8);
            Array.Copy(foundRanks, c.foundRanks, 8);
            Array.Copy(reserves, c.reserves, 6);
            Array.Copy(waste, c.waste, 104);
            c.wasteCount = wasteCount;
            Array.Copy(stock, c.stock, 104);
            c.stockCount = stockCount;
            c.recyclesUsed = recyclesUsed;
            return c;
        }

        public void Apply(SultanHintMove m)
        {
            if (m.Type == SultanHintMove.MoveType.WasteToFoundation)
            {
                byte c = waste[--wasteCount];
                foundRanks[m.ToIndex] = (byte)(c % 16);
            }
            else if (m.Type == SultanHintMove.MoveType.ReserveToFoundation)
            {
                byte c = reserves[m.FromIndex];
                reserves[m.FromIndex] = 0;
                foundRanks[m.ToIndex] = (byte)(c % 16);
            }
            else if (m.Type == SultanHintMove.MoveType.WasteToReserve)
            {
                reserves[m.ToIndex] = waste[--wasteCount];
            }
            else if (m.Type == SultanHintMove.MoveType.DrawStock)
            {
                waste[wasteCount++] = stock[--stockCount];
            }
            else if (m.Type == SultanHintMove.MoveType.Recycle)
            {
                for (int i = 0; i < wasteCount; i++) stock[i] = waste[wasteCount - 1 - i];
                stockCount = wasteCount;
                wasteCount = 0;
                recyclesUsed++;
            }
        }
    }

    private class SearchNode
    {
        public FastBoard Board;
        public SearchNode Parent;
        public SultanHintMove Move;
        public int Depth;
        public List<SultanHintMove> Moves;
        public int MoveIndex;

        public SearchNode(FastBoard b, SearchNode p, SultanHintMove m, int d)
        {
            Board = b; Parent = p; Move = m; Depth = d;
            Moves = null; MoveIndex = 0;
        }
    }

    private Coroutine activeSearchCoroutine;

    public void FindPath(SultanPileManager pm, int recyclesUsed, int maxRecycles, Action<List<SultanHintMove>> onComplete)
    {
        CancelSearch();
        activeSearchCoroutine = StartCoroutine(FindPathAsync(pm, recyclesUsed, maxRecycles, onComplete));
    }

    public void CancelSearch()
    {
        if (activeSearchCoroutine != null)
        {
            StopCoroutine(activeSearchCoroutine);
            activeSearchCoroutine = null;
        }
    }

    private IEnumerator FindPathAsync(SultanPileManager pm, int recyclesUsed, int maxRecycles, Action<List<SultanHintMove>> onComplete)
    {
        FastBoard root = new FastBoard();
        root.recyclesUsed = recyclesUsed;

        for (int i = 0; i < 8; i++)
        {
            if (i < pm.Foundations.Count)
            {
                var f = pm.Foundations[i];
                var card = f.GetTopCard();
                if (card != null)
                {
                    root.foundSuits[i] = (byte)card.cardModel.suit;
                    root.foundRanks[i] = (byte)card.cardModel.rank;
                }
            }
        }

        for (int i = 0; i < 6; i++)
        {
            if (i < pm.Reserves.Count)
            {
                var r = pm.Reserves[i];
                var cards = r.GetComponentsInChildren<CardController>();
                if (cards.Length > 0)
                {
                    var card = cards[cards.Length - 1];
                    root.reserves[i] = (byte)((int)card.cardModel.suit * 16 + card.cardModel.rank);
                }
            }
        }

        if (pm.WastePile != null)
        {
            var wasteCards = pm.WastePile.GetComponentsInChildren<CardController>();
            root.wasteCount = wasteCards.Length;
            for (int i = 0; i < wasteCards.Length; i++)
            {
                var card = wasteCards[i];
                root.waste[i] = (byte)((int)card.cardModel.suit * 16 + card.cardModel.rank);
            }
        }

        if (pm.StockPile != null)
        {
            var stockCards = pm.StockPile.GetComponentsInChildren<CardController>();
            root.stockCount = stockCards.Length;
            for (int i = 0; i < stockCards.Length; i++)
            {
                var card = stockCards[i];
                root.stock[i] = (byte)((int)card.cardModel.suit * 16 + card.cardModel.rank);
            }
        }

        var stack = new Stack<SearchNode>();
        var closedSet = new HashSet<ulong>();

        stack.Push(new SearchNode(root, null, default, 0));
        closedSet.Add(root.GetHash());

        int statesVisited = 0;
        SearchNode winningNode = null;

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        while (stack.Count > 0)
        {
            if (sw.ElapsedMilliseconds > FRAME_BUDGET_MS)
            {
                yield return null;
                sw.Restart();
            }

            if (statesVisited > MAX_SEARCH_NODES)
            {
                // ИСПРАВЛЕНИЕ: Никаких случайных ходов. Если уперлись в лимит - это тупик.
                Debug.LogWarning($"[HintDebug] Sultan DFS: Лимит ({MAX_SEARCH_NODES}). Тупик, сдаемся.");
                winningNode = null;
                break;
            }

            var current = stack.Peek();

            if (IsSolved(current.Board))
            {
                winningNode = current;
                break;
            }

            if (current.Moves == null)
            {
                statesVisited++;
                current.Moves = GenerateMoves(current.Board, maxRecycles);
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

        List<SultanHintMove> path = null;
        if (winningNode != null)
        {
            path = new List<SultanHintMove>();
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

    private List<SultanHintMove> GenerateMoves(FastBoard b, int maxRecycles)
    {
        var moves = new List<SultanHintMove>(16);

        // 1. Из Waste в Дом
        if (b.wasteCount > 0)
        {
            byte wCard = b.waste[b.wasteCount - 1];
            int suit = wCard / 16;
            int rank = wCard % 16;

            for (int i = 0; i < 8; i++)
            {
                int reqRank = (b.foundRanks[i] % 13) + 1;
                if (b.foundSuits[i] == suit && rank == reqRank)
                    moves.Add(new SultanHintMove { Type = SultanHintMove.MoveType.WasteToFoundation, ToIndex = i, Priority = 5 });
            }
        }

        // 2. Из Резерва в Дом
        for (int r = 0; r < 6; r++)
        {
            if (b.reserves[r] == 0) continue;
            int suit = b.reserves[r] / 16;
            int rank = b.reserves[r] % 16;

            for (int i = 0; i < 8; i++)
            {
                int reqRank = (b.foundRanks[i] % 13) + 1;
                if (b.foundSuits[i] == suit && rank == reqRank)
                    moves.Add(new SultanHintMove { Type = SultanHintMove.MoveType.ReserveToFoundation, FromIndex = r, ToIndex = i, Priority = 5 });
            }
        }

        // 3. Из Waste в Резерв
        if (b.wasteCount > 0)
        {
            for (int r = 0; r < 6; r++)
            {
                if (b.reserves[r] == 0)
                {
                    moves.Add(new SultanHintMove { Type = SultanHintMove.MoveType.WasteToReserve, ToIndex = r, Priority = 2 });
                    break; // Отсечение симметрии (кладем только в ПЕРВЫЙ пустой)
                }
            }
        }

        // 4. Листаем колоду
        if (b.stockCount > 0)
        {
            moves.Add(new SultanHintMove { Type = SultanHintMove.MoveType.DrawStock, Priority = 1 });
        }
        else if (b.wasteCount > 0 && b.recyclesUsed < maxRecycles)
        {
            moves.Add(new SultanHintMove { Type = SultanHintMove.MoveType.Recycle, Priority = 0 });
        }

        moves.Sort((x, y) => y.Priority.CompareTo(x.Priority));
        return moves;
    }

    private bool IsSolved(FastBoard b)
    {
        for (int i = 0; i < 8; i++)
        {
            if (b.foundRanks[i] != 12) return false; // Победа = все Дамы (12)
        }
        return true;
    }
}