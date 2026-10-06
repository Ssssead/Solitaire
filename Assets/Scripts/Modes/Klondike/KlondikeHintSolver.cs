using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public static class KlondikeHintSolver
{
    public enum HintMoveType { Foundation, RevealTableau, WasteToTableau, MoveTableau, StockDraw, RecycleWaste, FoundationToTableau }

    public class HintMoveCommand
    {
        public HintMoveType Type;
        public int FromIdx;
        public int ToIdx;
        public int Count;
        public int Priority;
        public bool FlippedCard;
    }

    private const int MAX_DEPTH = 300;
    private const int MAX_STATES = 200000;

    // Безошибочный 64-битный хэш состояния стола
    private struct StateKey : IEquatable<StateKey>
    {
        public readonly ulong Hash1;
        public readonly ulong Hash2;

        public StateKey(ulong h1, ulong h2) { Hash1 = h1; Hash2 = h2; }
        public bool Equals(StateKey other) => Hash1 == other.Hash1 && Hash2 == other.Hash2;
        public override int GetHashCode() => (int)(Hash1 ^ Hash2);
    }

    private class SearchNode
    {
        public int Recycles;
        public List<HintMoveCommand> Moves;
        public int MoveIndex;
        public void Reset() { Moves = null; MoveIndex = 0; Recycles = 0; }
    }

    public static IEnumerator GetHintPathAsync(Deal initialDeal, int drawCount, float frameBudgetMs, Action<List<HintMoveCommand>> onResult)
    {
        int statesVisited = 0;

        // Словарь для мемоизации: Ключ - Состояние стола, Значение - Кол-во прокруток колоды
        Dictionary<StateKey, int> visited = new Dictionary<StateKey, int>();

        SearchNode[] stack = new SearchNode[MAX_DEPTH + 1];
        for (int i = 0; i < stack.Length; i++) stack[i] = new SearchNode();

        int sp = 0;
        stack[0].Reset();

        Deal board = initialDeal.DeepClone();
        List<HintMoveCommand> currentPath = new List<HintMoveCommand>();
        List<HintMoveCommand> winningPath = null;

        Stopwatch sw = new Stopwatch();
        sw.Start();

        while (sp >= 0)
        {
            if (sw.ElapsedMilliseconds >= frameBudgetMs)
            {
                yield return null;
                sw.Restart();
            }

            SearchNode node = stack[sp];

            if (node.Moves == null)
            {
                if (sp >= MAX_DEPTH || statesVisited > MAX_STATES)
                {
                    sp--;
                    if (sp >= 0) UndoLastMove(board, currentPath);
                    continue;
                }

                statesVisited++;

                if (IsGameWon(board))
                {
                    winningPath = new List<HintMoveCommand>(currentPath);
                    break;
                }

                StateKey key = GetStateKey(board);

                // Проверяем, были ли мы тут более эффективным путем
                if (visited.TryGetValue(key, out int prevRecycles))
                {
                    if (node.Recycles >= prevRecycles)
                    {
                        sp--;
                        if (sp >= 0) UndoLastMove(board, currentPath);
                        continue;
                    }
                    else
                    {
                        visited[key] = node.Recycles; // Обновляем лучшим результатом
                    }
                }
                else
                {
                    visited.Add(key, node.Recycles);
                }

                node.Moves = GetPossibleMoves(board, drawCount);
                node.MoveIndex = 0;
            }

            if (node.MoveIndex < node.Moves.Count)
            {
                var move = node.Moves[node.MoveIndex];
                node.MoveIndex++;

                int nextRecycles = node.Recycles + (move.Type == HintMoveType.RecycleWaste ? 1 : 0);
                int maxRecycles = (drawCount == 3) ? 20 : 10;

                if (nextRecycles <= maxRecycles)
                {
                    ApplyMove(board, move);
                    currentPath.Add(move);

                    sp++;
                    stack[sp].Reset();
                    stack[sp].Recycles = nextRecycles;
                }
            }
            else
            {
                sp--;
                if (sp >= 0) UndoLastMove(board, currentPath);
            }
        }

        onResult?.Invoke(winningPath);
    }

    private static void UndoLastMove(Deal d, List<HintMoveCommand> path)
    {
        var m = path[path.Count - 1];
        path.RemoveAt(path.Count - 1);
        UndoMove(d, m);
    }

    private static int GetValidFoundationIndex(Deal d, CardModel c)
    {
        int emptyIdx = -1;
        for (int i = 0; i < 4; i++)
        {
            var f = d.foundations[i];
            if (f.Count > 0)
            {
                if (f[f.Count - 1].suit == c.suit && f[f.Count - 1].rank == c.rank - 1) return i;
            }
            else if (emptyIdx == -1)
            {
                emptyIdx = i;
            }
        }
        return c.rank == 1 ? emptyIdx : -1;
    }

    private static List<HintMoveCommand> GetPossibleMoves(Deal d, int drawCount)
    {
        var moves = new List<HintMoveCommand>(12);

        for (int i = 0; i < 7; i++)
        {
            var pile = d.tableau[i];
            if (pile.Count > 0)
            {
                var c = pile[pile.Count - 1];
                if (c.FaceUp)
                {
                    int fIdx = GetValidFoundationIndex(d, c.Card);
                    if (fIdx != -1) moves.Add(new HintMoveCommand { Type = HintMoveType.Foundation, FromIdx = i, ToIdx = fIdx });
                }
            }
        }

        if (d.waste.Count > 0)
        {
            int fIdx = GetValidFoundationIndex(d, d.waste[d.waste.Count - 1].Card);
            if (fIdx != -1) moves.Add(new HintMoveCommand { Type = HintMoveType.Foundation, FromIdx = -1, ToIdx = fIdx });
        }

        for (int i = 0; i < 7; i++)
        {
            var pile = d.tableau[i];
            if (pile.Count == 0) continue;

            int faceUpIdx = -1;
            for (int k = 0; k < pile.Count; k++) { if (pile[k].FaceUp) { faceUpIdx = k; break; } }
            if (faceUpIdx == -1) continue;

            var card = pile[faceUpIdx].Card;
            for (int dest = 0; dest < 7; dest++)
            {
                if (i == dest) continue;
                if (CanPlaceOnTableau(d, dest, card))
                {
                    if (faceUpIdx == 0 && card.rank == 13 && d.tableau[dest].Count == 0) continue;
                    bool exposesHidden = (faceUpIdx > 0 && !pile[faceUpIdx - 1].FaceUp);
                    moves.Add(new HintMoveCommand
                    {
                        Type = exposesHidden ? HintMoveType.RevealTableau : HintMoveType.MoveTableau,
                        FromIdx = i,
                        ToIdx = dest,
                        Count = pile.Count - faceUpIdx
                    });
                }
            }
        }

        if (d.waste.Count > 0)
        {
            var c = d.waste[d.waste.Count - 1].Card;
            for (int dest = 0; dest < 7; dest++)
            {
                if (CanPlaceOnTableau(d, dest, c))
                    moves.Add(new HintMoveCommand { Type = HintMoveType.WasteToTableau, ToIdx = dest });
            }
        }

        for (int s = 0; s < 4; s++)
        {
            var f = d.foundations[s];
            if (f.Count > 0)
            {
                var c = f[f.Count - 1];
                for (int dest = 0; dest < 7; dest++)
                {
                    if (CanPlaceOnTableau(d, dest, c))
                    {
                        if (d.tableau[dest].Count == 0 && c.rank == 13) continue;
                        moves.Add(new HintMoveCommand { Type = HintMoveType.FoundationToTableau, FromIdx = s, ToIdx = dest });
                    }
                }
            }
        }

        if (d.stock.Count > 0) moves.Add(new HintMoveCommand { Type = HintMoveType.StockDraw, Count = drawCount });
        else if (d.waste.Count > 0) moves.Add(new HintMoveCommand { Type = HintMoveType.RecycleWaste });

        foreach (var m in moves)
        {
            switch (m.Type)
            {
                case HintMoveType.RevealTableau: m.Priority = 100; break;
                case HintMoveType.Foundation: m.Priority = 90; break;
                case HintMoveType.WasteToTableau: m.Priority = 80; break;
                case HintMoveType.MoveTableau: m.Priority = 50; break;
                case HintMoveType.StockDraw: m.Priority = 40; break;
                case HintMoveType.RecycleWaste: m.Priority = 20; break;
                case HintMoveType.FoundationToTableau: m.Priority = 0; break;
            }
        }

        for (int i = 1; i < moves.Count; i++)
        {
            var temp = moves[i];
            int j = i - 1;
            while (j >= 0 && moves[j].Priority < temp.Priority) { moves[j + 1] = moves[j]; j--; }
            moves[j + 1] = temp;
        }

        return moves;
    }

    private static void ApplyMove(Deal d, HintMoveCommand m)
    {
        m.FlippedCard = false;
        switch (m.Type)
        {
            case HintMoveType.Foundation:
                if (m.FromIdx == -1)
                {
                    var c = d.waste[d.waste.Count - 1];
                    d.waste.RemoveAt(d.waste.Count - 1);
                    d.foundations[m.ToIdx].Add(c.Card);
                }
                else
                {
                    var src = d.tableau[m.FromIdx];
                    var c = src[src.Count - 1];
                    src.RemoveAt(src.Count - 1);
                    d.foundations[m.ToIdx].Add(c.Card);
                    if (src.Count > 0 && !src[src.Count - 1].FaceUp) { src[src.Count - 1].FaceUp = true; m.FlippedCard = true; }
                }
                break;
            case HintMoveType.RevealTableau:
            case HintMoveType.MoveTableau:
                var sTab = d.tableau[m.FromIdx];
                var moving = sTab.GetRange(sTab.Count - m.Count, m.Count);
                sTab.RemoveRange(sTab.Count - m.Count, m.Count);
                if (sTab.Count > 0 && !sTab[sTab.Count - 1].FaceUp) { sTab[sTab.Count - 1].FaceUp = true; m.FlippedCard = true; }
                d.tableau[m.ToIdx].AddRange(moving);
                break;
            case HintMoveType.WasteToTableau:
                var wc = d.waste[d.waste.Count - 1];
                d.waste.RemoveAt(d.waste.Count - 1);
                d.tableau[m.ToIdx].Add(wc);
                break;
            case HintMoveType.FoundationToTableau:
                var f = d.foundations[m.FromIdx];
                var cardToReturn = f[f.Count - 1];
                f.RemoveAt(f.Count - 1);
                d.tableau[m.ToIdx].Add(new CardInstance(cardToReturn, true));
                break;
            case HintMoveType.StockDraw:
                int cardsToDraw = Math.Min(m.Count, d.stock.Count);
                m.Count = cardsToDraw;
                for (int i = 0; i < cardsToDraw; i++)
                {
                    var x = d.stock.Pop(); x.FaceUp = true; d.waste.Add(x);
                }
                break;
            case HintMoveType.RecycleWaste:
                m.Count = d.waste.Count;
                for (int i = d.waste.Count - 1; i >= 0; i--) { var c = d.waste[i]; c.FaceUp = false; d.stock.Push(c); }
                d.waste.Clear();
                break;
        }
    }

    private static void UndoMove(Deal d, HintMoveCommand m)
    {
        switch (m.Type)
        {
            case HintMoveType.Foundation:
                var f = d.foundations[m.ToIdx];
                var c = f[f.Count - 1];
                f.RemoveAt(f.Count - 1);
                if (m.FromIdx == -1) d.waste.Add(new CardInstance(c, true));
                else
                {
                    var src = d.tableau[m.FromIdx];
                    if (m.FlippedCard) src[src.Count - 1].FaceUp = false;
                    src.Add(new CardInstance(c, true));
                }
                break;
            case HintMoveType.RevealTableau:
            case HintMoveType.MoveTableau:
                var srcTab = d.tableau[m.FromIdx];
                var destTab = d.tableau[m.ToIdx];
                var moving = destTab.GetRange(destTab.Count - m.Count, m.Count);
                destTab.RemoveRange(destTab.Count - m.Count, m.Count);
                if (m.FlippedCard) srcTab[srcTab.Count - 1].FaceUp = false;
                srcTab.AddRange(moving);
                break;
            case HintMoveType.WasteToTableau:
                var wDest = d.tableau[m.ToIdx];
                var wCard = wDest[wDest.Count - 1];
                wDest.RemoveAt(wDest.Count - 1);
                d.waste.Add(wCard);
                break;
            case HintMoveType.FoundationToTableau:
                var fDest = d.tableau[m.ToIdx];
                var fCard = fDest[fDest.Count - 1];
                fDest.RemoveAt(fDest.Count - 1);
                d.foundations[m.FromIdx].Add(fCard.Card);
                break;
            case HintMoveType.StockDraw:
                for (int i = 0; i < m.Count; i++)
                {
                    var sc = d.waste[d.waste.Count - 1];
                    d.waste.RemoveAt(d.waste.Count - 1);
                    sc.FaceUp = false;
                    d.stock.Push(sc);
                }
                break;
            case HintMoveType.RecycleWaste:
                for (int i = 0; i < m.Count; i++)
                {
                    var rc = d.stock.Pop();
                    rc.FaceUp = true;
                    d.waste.Add(rc);
                }
                break;
        }
    }

    private static StateKey GetStateKey(Deal d)
    {
        unchecked
        {
            ulong h1 = 17;
            ulong h2 = 19;
            for (int i = 0; i < 7; i++)
            {
                var pile = d.tableau[i];
                h1 = h1 * 31 + (ulong)pile.Count;
                int closed = 0;
                for (int k = 0; k < pile.Count; k++)
                {
                    if (pile[k].FaceUp)
                    {
                        ulong val = (ulong)(pile[k].Card.rank + (int)pile[k].Card.suit * 13);
                        h1 = h1 * 31 + val;
                        h2 = h2 * 17 + val;
                    }
                    else closed++;
                }
                h2 = h2 * 31 + (ulong)closed;
            }
            for (int i = 0; i < 4; i++) { h1 = h1 * 31 + (ulong)d.foundations[i].Count; }
            h1 = h1 * 31 + (ulong)d.waste.Count;
            for (int i = 0; i < d.waste.Count; i++)
            {
                ulong val = (ulong)(d.waste[i].Card.rank + (int)d.waste[i].Card.suit * 13);
                h2 = h2 * 31 + val;
            }
            h1 = h1 * 31 + (ulong)d.stock.Count;
            return new StateKey(h1, h2);
        }
    }

    private static bool CanPlaceOnTableau(Deal d, int idx, CardModel c)
    {
        var pile = d.tableau[idx];
        if (pile.Count == 0) return c.rank == 13;
        var top = pile[pile.Count - 1].Card;
        bool rA = (c.suit == Suit.Diamonds || c.suit == Suit.Hearts);
        bool rB = (top.suit == Suit.Diamonds || top.suit == Suit.Hearts);
        return rA != rB && top.rank == c.rank + 1;
    }

    private static bool CanAddToFoundation(Deal d, CardModel c)
    {
        var f = d.foundations[(int)c.suit];
        return f.Count == 0 ? c.rank == 1 : f[f.Count - 1].rank == c.rank - 1;
    }

    private static bool IsGameWon(Deal d)
    {
        int f = 0;
        for (int i = 0; i < 4; i++) f += d.foundations[i].Count;
        return f == 52;
    }
}