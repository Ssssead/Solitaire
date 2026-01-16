using System;
using System.Collections.Generic;
using System.Linq;

public static class KlondikeSolver
{
    private const int MAX_DEPTH = 1000;
    private const int MAX_STATES = 20000;
    private const int GREEDY_LIMIT = 800;

    private class SolveContext
    {
        public int TotalTrapsFound = 0;
        public int StatesVisited = 0;
        public HashSet<string> History = new HashSet<string>();
        public bool Solved = false;
        public int DrawParam;
        public int SolutionMoves = 0;
        public int CriticalDecisions = 0;
    }

    private enum MoveType { Foundation, RevealTableau, WasteToTableau, MoveTableau, StockDraw, RecycleWaste }

    private class MoveCommand
    {
        public MoveType Type;
        public int FromIdx;
        public int ToIdx;
        public int Count;
        public int Priority;
    }

    [Serializable]
    public struct ExtendedSolverResult
    {
        public bool IsSolved;
        public Difficulty EstimatedDifficulty;
        public int Traps;
        public int Moves;
        public int Decisions;
    }

    public static ExtendedSolverResult SolveExtended(Deal initialDeal, int drawCount)
    {
        SolveContext ctx = new SolveContext { DrawParam = drawCount };
        bool success = DFS(initialDeal, 0, ctx);

        if (!success) return new ExtendedSolverResult { IsSolved = false };

        Difficulty diff;
        // Классификация на основе Развилок (как советуют на форуме)
        if (ctx.CriticalDecisions == 0) diff = Difficulty.Easy;
        else if (ctx.CriticalDecisions < 5) diff = Difficulty.Medium;
        else diff = Difficulty.Hard;

        return new ExtendedSolverResult
        {
            IsSolved = true,
            EstimatedDifficulty = diff,
            Traps = ctx.TotalTrapsFound,
            Moves = ctx.SolutionMoves,
            Decisions = ctx.CriticalDecisions
        };
    }

    // Обертка для старого кода
    public static SolverResult Solve(Deal initialDeal, int drawCount)
    {
        var res = SolveExtended(initialDeal, drawCount);
        return new SolverResult
        {
            IsSolved = res.IsSolved,
            EstimatedDifficulty = res.EstimatedDifficulty,
            MovesCount = res.Traps,
            StockPasses = res.Moves
        };
    }

    private static bool DFS(Deal currentDeal, int depth, SolveContext ctx)
    {
        if (depth > MAX_DEPTH) return false;
        if (ctx.Solved) return true;
        ctx.StatesVisited++;
        if (ctx.StatesVisited > MAX_STATES) return false;

        if (IsGameWon(currentDeal))
        {
            ctx.Solved = true;
            ctx.SolutionMoves = depth;
            return true;
        }

        string hash = GetStateHash(currentDeal);
        if (ctx.History.Contains(hash)) return false;
        ctx.History.Add(hash);

        List<MoveCommand> moves = GetPossibleMoves(currentDeal, ctx.DrawParam);
        SortMovesByHeuristic(moves);

        bool foundPath = false;
        int localTraps = 0;

        foreach (var move in moves)
        {
            Deal nextState = ApplyMove(currentDeal, move);

            if (DFS(nextState, depth + 1, ctx))
            {
                foundPath = true;
                // Если был выбор, и альтернатива вела в тупик -> Это Критическая Развилка
                if (localTraps > 0 && IsSignificantMove(move))
                {
                    ctx.CriticalDecisions++;
                }
                break;
            }
            else
            {
                if (IsSignificantMove(move))
                {
                    localTraps++;
                    ctx.TotalTrapsFound++;
                }
            }
        }

        ctx.History.Remove(hash);
        return foundPath;
    }

    // --- ДЛЯ КОМПИЛЯЦИИ: ВСТАВЬТЕ СЮДА ВЕСЬ ОСТАЛЬНОЙ КОД (Greedy, GetMoves, etc) ---
    // (Код идентичен предыдущему файлу, я его сокращаю здесь, но он ОБЯЗАН быть)

    public static bool IsSolvableByGreedy(Deal initialDeal, int drawCount)
    {
        Deal d = initialDeal.DeepClone();
        int movesWithoutAction = 0;
        int safetyLimit = GREEDY_LIMIT;
        while (movesWithoutAction < 3 && safetyLimit > 0)
        {
            safetyLimit--;
            bool moveMade = false;
            if (TryMoveToFoundationGreedy(d)) { moveMade = true; movesWithoutAction = 0; continue; }
            if (TryTableauMoveGreedy(d, onlyRevealing: true)) { moveMade = true; movesWithoutAction = 0; continue; }
            if (TryWasteToTableauGreedy(d)) { moveMade = true; movesWithoutAction = 0; continue; }
            if (d.stock.Count > 0) { DrawCards(d, drawCount); moveMade = true; movesWithoutAction = 0; }
            else if (d.waste.Count > 0) { RecycleWaste(d); movesWithoutAction++; moveMade = true; }
            if (!moveMade) break;
        }
        return IsGameWon(d);
    }

    private static List<MoveCommand> GetPossibleMoves(Deal d, int drawCount)
    {
        var moves = new List<MoveCommand>();
        for (int i = 0; i < 7; i++)
        {
            if (d.tableau[i].Count > 0)
            {
                var c = d.tableau[i].Last();
                if (c.FaceUp && CanAddToFoundation(d, c.Card))
                    moves.Add(new MoveCommand { Type = MoveType.Foundation, FromIdx = i });
            }
        }
        if (d.waste.Count > 0 && CanAddToFoundation(d, d.waste.Last().Card))
            moves.Add(new MoveCommand { Type = MoveType.Foundation, FromIdx = -1 });

        for (int i = 0; i < 7; i++)
        {
            if (d.tableau[i].Count == 0) continue;
            var pile = d.tableau[i];
            int faceUpIdx = -1;
            for (int k = 0; k < pile.Count; k++) { if (pile[k].FaceUp) { faceUpIdx = k; break; } }
            if (faceUpIdx == -1) continue;
            bool exposesHidden = (faceUpIdx > 0 && !pile[faceUpIdx - 1].FaceUp);
            bool emptiesCol = (faceUpIdx == 0);
            var card = pile[faceUpIdx].Card;
            for (int dest = 0; dest < 7; dest++)
            {
                if (i == dest) continue;
                if (CanPlaceOnTableau(d, dest, card))
                {
                    if (emptiesCol && card.rank == 13 && d.tableau[dest].Count == 0) continue;
                    MoveType t = exposesHidden ? MoveType.RevealTableau : MoveType.MoveTableau;
                    moves.Add(new MoveCommand { Type = t, FromIdx = i, ToIdx = dest, Count = pile.Count - faceUpIdx });
                }
            }
        }
        if (d.waste.Count > 0)
        {
            var c = d.waste.Last().Card;
            for (int dest = 0; dest < 7; dest++)
            {
                if (CanPlaceOnTableau(d, dest, c))
                    moves.Add(new MoveCommand { Type = MoveType.WasteToTableau, ToIdx = dest });
            }
        }
        if (d.stock.Count > 0) moves.Add(new MoveCommand { Type = MoveType.StockDraw });
        else if (d.waste.Count > 0) moves.Add(new MoveCommand { Type = MoveType.RecycleWaste });
        return moves;
    }

    private static Deal ApplyMove(Deal oldState, MoveCommand m)
    {
        Deal d = oldState.DeepClone();
        switch (m.Type)
        {
            case MoveType.Foundation:
                if (m.FromIdx == -1) { var c = d.waste.Last(); d.waste.RemoveAt(d.waste.Count - 1); d.foundations[(int)c.Card.suit].Add(c.Card); }
                else { var c = d.tableau[m.FromIdx].Last(); d.tableau[m.FromIdx].RemoveAt(d.tableau[m.FromIdx].Count - 1); d.foundations[(int)c.Card.suit].Add(c.Card); if (d.tableau[m.FromIdx].Count > 0) d.tableau[m.FromIdx].Last().FaceUp = true; }
                break;
            case MoveType.RevealTableau:
            case MoveType.MoveTableau:
                var src = d.tableau[m.FromIdx]; var moving = src.GetRange(src.Count - m.Count, m.Count); src.RemoveRange(src.Count - m.Count, m.Count); if (src.Count > 0) src.Last().FaceUp = true; d.tableau[m.ToIdx].AddRange(moving); break;
            case MoveType.WasteToTableau:
                var wc = d.waste.Last(); d.waste.RemoveAt(d.waste.Count - 1); d.tableau[m.ToIdx].Add(wc); break;
            case MoveType.StockDraw:
                var x = d.stock.Pop(); x.FaceUp = true; d.waste.Add(x); break;
            case MoveType.RecycleWaste:
                RecycleWaste(d); break;
        }
        return d;
    }

    private static void SortMovesByHeuristic(List<MoveCommand> moves)
    {
        foreach (var m in moves)
        {
            switch (m.Type)
            {
                case MoveType.Foundation: m.Priority = 10; break;
                case MoveType.RevealTableau: m.Priority = 8; break;
                case MoveType.WasteToTableau: m.Priority = 6; break;
                case MoveType.MoveTableau: m.Priority = 4; break;
                case MoveType.StockDraw: m.Priority = 2; break;
                case MoveType.RecycleWaste: m.Priority = 1; break;
            }
        }
        moves.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    private static bool IsSignificantMove(MoveCommand m) => m.Type != MoveType.StockDraw && m.Type != MoveType.RecycleWaste;

    // --- HELPER METHODS ---
    private static bool TryMoveToFoundationGreedy(Deal d)
    {
        for (int i = 0; i < 7; i++) { if (d.tableau[i].Count > 0) { var c = d.tableau[i].Last(); if (c.FaceUp && CanAddToFoundation(d, c.Card)) { d.foundations[(int)c.Card.suit].Add(c.Card); d.tableau[i].RemoveAt(d.tableau[i].Count - 1); if (d.tableau[i].Count > 0) d.tableau[i].Last().FaceUp = true; return true; } } }
        if (d.waste.Count > 0) { var c = d.waste.Last(); if (CanAddToFoundation(d, c.Card)) { d.foundations[(int)c.Card.suit].Add(c.Card); d.waste.RemoveAt(d.waste.Count - 1); return true; } }
        return false;
    }
    private static bool TryTableauMoveGreedy(Deal d, bool onlyRevealing)
    {
        for (int src = 0; src < 7; src++) { if (d.tableau[src].Count == 0) continue; var pile = d.tableau[src]; int faceUpIdx = -1; for (int k = 0; k < pile.Count; k++) { if (pile[k].FaceUp) { faceUpIdx = k; break; } } if (faceUpIdx == -1) continue; bool exposesHidden = (faceUpIdx > 0 && !pile[faceUpIdx - 1].FaceUp); bool emptiesSlot = (faceUpIdx == 0); if (onlyRevealing) { if (!exposesHidden && !emptiesSlot) continue; if (emptiesSlot) continue; } var card = pile[faceUpIdx].Card; for (int dest = 0; dest < 7; dest++) { if (src == dest) continue; if (CanPlaceOnTableau(d, dest, card)) { var moving = pile.GetRange(faceUpIdx, pile.Count - faceUpIdx); d.tableau[dest].AddRange(moving); d.tableau[src].RemoveRange(faceUpIdx, moving.Count); if (d.tableau[src].Count > 0) d.tableau[src].Last().FaceUp = true; return true; } } }
        return false;
    }
    private static bool TryWasteToTableauGreedy(Deal d)
    {
        if (d.waste.Count == 0) return false; var cardInst = d.waste.Last(); for (int i = 0; i < 7; i++) { if (CanPlaceOnTableau(d, i, cardInst.Card)) { d.tableau[i].Add(cardInst); d.waste.RemoveAt(d.waste.Count - 1); return true; } }
        return false;
    }
    private static bool CanPlaceOnTableau(Deal d, int idx, CardModel c) { if (d.tableau[idx].Count == 0) return c.rank == 13; var top = d.tableau[idx].Last().Card; return IsOppositeColor(c, top) && top.rank == c.rank + 1; }
    private static bool CanAddToFoundation(Deal d, CardModel c) { var f = d.foundations[(int)c.suit]; return f.Count == 0 ? c.rank == 1 : f.Last().rank == c.rank - 1; }
    private static bool IsOppositeColor(CardModel a, CardModel b) { bool rA = (a.suit == Suit.Diamonds || a.suit == Suit.Hearts); bool rB = (b.suit == Suit.Diamonds || b.suit == Suit.Hearts); return rA != rB; }
    private static void DrawCards(Deal d, int count) { for (int i = 0; i < count && d.stock.Count > 0; i++) { var c = d.stock.Pop(); c.FaceUp = true; d.waste.Add(c); } }
    private static void RecycleWaste(Deal d) { for (int i = d.waste.Count - 1; i >= 0; i--) { var c = d.waste[i]; c.FaceUp = false; d.stock.Push(c); } d.waste.Clear(); }
    private static bool IsGameWon(Deal d) { int f = 0; foreach (var q in d.foundations) f += q.Count; return f == 52; }
    private static string GetStateHash(Deal d) { int h = 17; foreach (var f in d.foundations) h = h * 31 + (f.Count > 0 ? f.Last().rank : 0); foreach (var t in d.tableau) { if (t.Count > 0) { var c = t.Last().Card; h = h * 31 + (c.rank * 10 + (int)c.suit); } else h = h * 31 + -1; } return $"{h}:{d.stock.Count}:{d.waste.Count}"; }
}