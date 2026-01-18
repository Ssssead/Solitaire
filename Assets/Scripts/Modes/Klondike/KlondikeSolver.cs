using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics; // Для Stopwatch

public static class KlondikeSolver
{
    // --- КОНФИГУРАЦИЯ ---
    private const int MAX_DEPTH = 1000;
    private const int MAX_STATES = 30000;

    // --- ВЕСА (Ваша формула) ---
    private const int W_BASE = 10;     // Ходы
    private const int W_ACE = 200;     // Глубина тузов
    private const int W_STOCK = 800;   // Прокрутки
    private const int W_RET = 2500;    // Возврат из дома
    private const int W_BRK = 2000;    // Разрыв цепочки
    private const int W_TRAP = 4000;   // Ловушки

    // --- СТРУКТУРЫ ---
    private struct StateKey : IEquatable<StateKey>
    {
        public readonly int TableauHash;
        public readonly int StockCount;
        public readonly int WasteCount;

        public StateKey(int tHash, int sCount, int wCount)
        {
            TableauHash = tHash; StockCount = sCount; WasteCount = wCount;
        }
        public bool Equals(StateKey other) => TableauHash == other.TableauHash && StockCount == other.StockCount && WasteCount == other.WasteCount;
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + TableauHash;
                hash = hash * 31 + StockCount;
                hash = hash * 31 + WasteCount;
                return hash;
            }
        }
    }

    // Узел поиска для Стека (замена рекурсии)
    private class SearchNode
    {
        public Deal DealState;
        public int Depth;
        public int Recycles;
        public int Returns;
        public int Breaks;

        // Для итеративного перебора ходов
        public List<MoveCommand> Moves;
        public int MoveIndex;

        public SearchNode(Deal d, int depth, int rec, int ret, int brk)
        {
            DealState = d; Depth = depth; Recycles = rec; Returns = ret; Breaks = brk;
            Moves = null; MoveIndex = 0;
        }
    }

    private class SolveContext
    {
        public int StatesVisited = 0;
        public HashSet<StateKey> History = new HashSet<StateKey>();
        public bool Solved = false;
        public int DrawParam;

        public int SolutionMoves = 0;
        public int SolutionRecycles = 0;
        public int FoundationReturns = 0;
        public int SequenceBreaks = 0;
    }

    private enum MoveType { Foundation, RevealTableau, WasteToTableau, MoveTableau, StockDraw, RecycleWaste, FoundationToTableau }

    private class MoveCommand
    {
        public MoveType Type;
        public int FromIdx; public int ToIdx; public int Count; public int Priority; public bool IsSequenceBreak;
    }

    // Класс результата (Reference type, чтобы передавать в корутину)
    [Serializable]
    public class ExtendedSolverResult
    {
        public bool IsSolved;
        public int Score;
        public int Moves;
        public int AceDepth;
        public int Recycles;
        public int Returns;
        public int Breaks;
        public int Traps;
    }

    // =================================================================================
    // АСИНХРОННЫЙ СОЛВЕР (COROUTINE)
    // =================================================================================
    public static IEnumerator SolveAsync(Deal initialDeal, int drawCount, float frameBudgetMs, ExtendedSolverResult resultOut)
    {
        // 1. Статический анализ
        int aceDepthScore = CalculateAceDepthSum(initialDeal);

        // 2. Инициализация
        SolveContext ctx = new SolveContext { DrawParam = drawCount };
        Stack<SearchNode> stack = new Stack<SearchNode>();

        // Пушим начальное состояние
        stack.Push(new SearchNode(initialDeal.DeepClone(), 0, 0, 0, 0));

        Stopwatch sw = new Stopwatch();
        sw.Start();

        // 3. Главный цикл (Iterative DFS)
        while (stack.Count > 0)
        {
            // --- TIME SLICING ---
            // Если бюджет кадра исчерпан, прерываемся и ждем следующий кадр
            if (sw.ElapsedMilliseconds >= frameBudgetMs)
            {
                yield return null;
                sw.Restart();
            }

            // Берем текущий узел (не удаляя, пока не обработаем все ходы)
            SearchNode node = stack.Peek();

            // Если мы только что пришли в этот узел (Moves == null)
            if (node.Moves == null)
            {
                // Проверки отсечения
                if (node.Depth > MAX_DEPTH || ctx.StatesVisited > MAX_STATES || ctx.Solved)
                {
                    stack.Pop();
                    continue;
                }

                ctx.StatesVisited++;

                // Победа?
                if (IsGameWon(node.DealState))
                {
                    ctx.Solved = true;
                    ctx.SolutionMoves = node.Depth;
                    ctx.SolutionRecycles = node.Recycles;
                    ctx.FoundationReturns = node.Returns;
                    ctx.SequenceBreaks = node.Breaks;
                    stack.Pop();
                    continue;
                }

                // Хеширование
                StateKey key = GetStateKey(node.DealState);
                if (ctx.History.Contains(key))
                {
                    stack.Pop();
                    continue;
                }
                ctx.History.Add(key);

                // Генерация ходов
                node.Moves = GetPossibleMoves(node.DealState, ctx.DrawParam);
                SortMovesByHeuristic(node.Moves);
                node.MoveIndex = 0;
            }

            // Обработка следующего хода в списке
            if (node.MoveIndex < node.Moves.Count)
            {
                // Если решение уже найдено в другой ветке - выходим
                if (ctx.Solved)
                {
                    stack.Pop();
                    continue;
                }

                var move = node.Moves[node.MoveIndex];
                node.MoveIndex++; // Сдвигаем индекс

                int nextRecycles = node.Recycles + (move.Type == MoveType.RecycleWaste ? 1 : 0);

                if (nextRecycles <= 12) // Limit recycles
                {
                    // Применяем ход
                    Deal nextState = ApplyMove(node.DealState, move);
                    int nextReturns = node.Returns + (move.Type == MoveType.FoundationToTableau ? 1 : 0);
                    int nextBreaks = node.Breaks + (move.IsSequenceBreak ? 1 : 0);

                    // PUSH (Эмуляция рекурсивного вызова)
                    stack.Push(new SearchNode(nextState, node.Depth + 1, nextRecycles, nextReturns, nextBreaks));
                }
            }
            else
            {
                // Все ходы проверены (Backtracking)
                StateKey key = GetStateKey(node.DealState);
                ctx.History.Remove(key);
                stack.Pop();
            }
        }

        // 4. Формирование результата
        if (ctx.Solved)
        {
            resultOut.IsSolved = true;

            // Расчет Traps (грубая оценка: посещенные минус полезные)
            int effectiveTraps = Math.Max(0, (ctx.StatesVisited - ctx.SolutionMoves) / 10);

            // Формула Score
            int score = 0;
            score += ctx.SolutionMoves * W_BASE;
            score += aceDepthScore * W_ACE;
            score += ctx.SolutionRecycles * W_STOCK;
            score += ctx.FoundationReturns * W_RET;
            score += ctx.SequenceBreaks * W_BRK;
            score += effectiveTraps * W_TRAP;

            resultOut.Score = score;
            resultOut.Moves = ctx.SolutionMoves;
            resultOut.AceDepth = aceDepthScore;
            resultOut.Recycles = ctx.SolutionRecycles;
            resultOut.Returns = ctx.FoundationReturns;
            resultOut.Breaks = ctx.SequenceBreaks;
            resultOut.Traps = effectiveTraps;
        }
        else
        {
            resultOut.IsSolved = false;
        }
    }

    // =================================================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ (LOGIC)
    // =================================================================================

    private static int CalculateAceDepthSum(Deal d)
    {
        int totalDepth = 0;
        for (int i = 0; i < 7; i++)
        {
            var pile = d.tableau[i];
            for (int k = 0; k < pile.Count; k++)
            {
                if (pile[k].Card.rank == 1) totalDepth += (pile.Count - 1) - k;
            }
        }
        var stockList = d.stock.ToList();
        for (int i = 0; i < stockList.Count; i++)
        {
            if (stockList[i].Card.rank == 1) totalDepth += 5 + (i / 3);
        }
        return totalDepth;
    }

    private static StateKey GetStateKey(Deal d)
    {
        unchecked
        {
            int tHash = 19;
            for (int i = 0; i < 7; i++)
            {
                var pile = d.tableau[i];
                if (pile.Count > 0)
                {
                    var c = pile[pile.Count - 1].Card;
                    tHash = tHash * 31 + (c.rank + (int)c.suit * 13);
                    for (int k = 0; k < pile.Count; k++) if (pile[k].FaceUp) { tHash += k; break; }
                }
                else { tHash = tHash * 31 + -1; }
            }
            for (int i = 0; i < 4; i++)
            {
                var f = d.foundations[i];
                tHash = tHash * 17 + (f.Count > 0 ? f[f.Count - 1].rank : 0);
            }
            return new StateKey(tHash, d.stock.Count, d.waste.Count);
        }
    }

    private static List<MoveCommand> GetPossibleMoves(Deal d, int drawCount)
    {
        var moves = new List<MoveCommand>(12);

        // 1. Foundation
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

        // 2. Tableau
        for (int i = 0; i < 7; i++)
        {
            if (d.tableau[i].Count == 0) continue;
            var pile = d.tableau[i];

            int faceUpIdx = -1;
            for (int k = 0; k < pile.Count; k++) { if (pile[k].FaceUp) { faceUpIdx = k; break; } }
            if (faceUpIdx == -1) continue;

            bool isBreak = false;
            var card = pile[faceUpIdx].Card;
            if (faceUpIdx > 0)
            {
                var cardBelow = pile[faceUpIdx - 1];
                if (cardBelow.FaceUp && IsOppositeColor(card, cardBelow.Card) && cardBelow.Card.rank == card.rank + 1)
                    isBreak = true;
            }

            for (int dest = 0; dest < 7; dest++)
            {
                if (i == dest) continue;
                if (CanPlaceOnTableau(d, dest, card))
                {
                    if (faceUpIdx == 0 && card.rank == 13 && d.tableau[dest].Count == 0) continue;
                    bool exposesHidden = (faceUpIdx > 0 && !pile[faceUpIdx - 1].FaceUp);
                    moves.Add(new MoveCommand
                    {
                        Type = exposesHidden ? MoveType.RevealTableau : MoveType.MoveTableau,
                        FromIdx = i,
                        ToIdx = dest,
                        Count = pile.Count - faceUpIdx,
                        IsSequenceBreak = isBreak
                    });
                }
            }
        }

        // 3. Waste -> Tableau
        if (d.waste.Count > 0)
        {
            var c = d.waste.Last().Card;
            for (int dest = 0; dest < 7; dest++)
            {
                if (CanPlaceOnTableau(d, dest, c))
                    moves.Add(new MoveCommand { Type = MoveType.WasteToTableau, ToIdx = dest });
            }
        }

        // 4. Returns
        for (int s = 0; s < 4; s++)
        {
            if (d.foundations[s].Count > 0)
            {
                var c = d.foundations[s].Last();
                for (int dest = 0; dest < 7; dest++)
                {
                    if (CanPlaceOnTableau(d, dest, c))
                    {
                        if (d.tableau[dest].Count == 0 && c.rank == 13) continue;
                        moves.Add(new MoveCommand { Type = MoveType.FoundationToTableau, FromIdx = s, ToIdx = dest });
                    }
                }
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
            case MoveType.FoundationToTableau:
                var suitList = d.foundations[m.FromIdx]; var cardToReturn = suitList.Last(); suitList.RemoveAt(suitList.Count - 1); d.tableau[m.ToIdx].Add(new CardInstance(cardToReturn, true)); break;
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
                case MoveType.FoundationToTableau: m.Priority = 0; break;
            }
        }
        moves.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    private static bool IsSignificantMove(MoveCommand m) => m.Type != MoveType.StockDraw && m.Type != MoveType.RecycleWaste;
    private static bool CanPlaceOnTableau(Deal d, int idx, CardModel c) { if (d.tableau[idx].Count == 0) return c.rank == 13; var top = d.tableau[idx].Last().Card; return IsOppositeColor(c, top) && top.rank == c.rank + 1; }
    private static bool CanAddToFoundation(Deal d, CardModel c) { var f = d.foundations[(int)c.suit]; return f.Count == 0 ? c.rank == 1 : f.Last().rank == c.rank - 1; }
    private static bool IsOppositeColor(CardModel a, CardModel b) { bool rA = (a.suit == Suit.Diamonds || a.suit == Suit.Hearts); bool rB = (b.suit == Suit.Diamonds || b.suit == Suit.Hearts); return rA != rB; }
    private static void RecycleWaste(Deal d) { for (int i = d.waste.Count - 1; i >= 0; i--) { var c = d.waste[i]; c.FaceUp = false; d.stock.Push(c); } d.waste.Clear(); }
    private static bool IsGameWon(Deal d) { int f = 0; foreach (var q in d.foundations) f += q.Count; return f == 52; }
}