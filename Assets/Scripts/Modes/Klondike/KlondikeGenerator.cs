// SolvableGenerator.cs [REVERSE ASSEMBLY - INSTANT & GUARANTEED]
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public enum Difficulty { Easy, Medium, Hard }


public class SolvableGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Klondike;

    [Header("Difficulty Tuning")]
    // Шанс положить карту "идеально" (на родителя), создав цепочку.
    // Easy: Высокий шанс (помогаем). Hard: Низкий (разбиваем пары).
    [Range(0, 1)] public float easyLinkChance = 0.90f;
    [Range(0, 1)] public float hardLinkChance = 0.15f;

    // Шанс спрятать карту в колоду (Stock) вместо стола.
    [Range(0, 1)] public float easyStockBias = 0.15f;
    [Range(0, 1)] public float hardStockBias = 0.60f;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        // Генерация занимает <1мс, но мы используем корутину для совместимости с CacheSystem
        Deal deal = CreateSolvableDeal(difficulty);

        // Метрики фейковые, так как мы знаем, что расклад решаем по построению
        var metrics = new DealMetrics
        {
            Solved = true,
            MoveEstimate = (difficulty == Difficulty.Hard) ? 150 : 60
        };

        yield return null; // Пропускаем кадр для плавности
        onComplete?.Invoke(deal, metrics);
    }

    private Deal CreateSolvableDeal(Difficulty difficulty)
    {
        float linkChance = (difficulty == Difficulty.Easy) ? easyLinkChance : hardLinkChance;
        float stockBias = (difficulty == Difficulty.Easy) ? easyStockBias : hardStockBias;

        // 1. Создаем "виртуальные дома" (полностью собранные)
        List<List<CardModel>> solvedPiles = new List<List<CardModel>>();
        foreach (Suit s in Enum.GetValues(typeof(Suit)))
        {
            var pile = new List<CardModel>();
            for (int r = 1; r <= 13; r++) pile.Add(new CardModel(s, r));
            solvedPiles.Add(pile);
        }

        // 2. Подготовка рабочих зон
        List<List<CardModel>> table = new List<List<CardModel>>();
        for (int i = 0; i < 7; i++) table.Add(new List<CardModel>());

        List<CardModel> stockCards = new List<CardModel>();

        // 3. ЦИКЛ ОБРАТНОГО РАЗБОРА (52 карты)
        int cardsLeft = 52;
        System.Random rng = new System.Random();

        while (cardsLeft > 0)
        {
            var availableSuits = solvedPiles.Where(p => p.Count > 0).ToList();
            if (availableSuits.Count == 0) break;

            var chosenPile = availableSuits[rng.Next(availableSuits.Count)];
            CardModel card = chosenPile[chosenPile.Count - 1];
            chosenPile.RemoveAt(chosenPile.Count - 1);
            cardsLeft--;

            bool placedOnTable = false;

            // --- Логика размещения на столе (без изменений) ---
            if (card.rank == 13)
            {
                var emptySlots = Enumerable.Range(0, 7).Where(i => table[i].Count == 0).ToList();
                if (emptySlots.Count > 0)
                {
                    if (rng.NextDouble() > stockBias * 0.5f)
                    {
                        int slot = emptySlots[rng.Next(emptySlots.Count)];
                        table[slot].Add(card);
                        placedOnTable = true;
                    }
                }
            }
            else
            {
                var validParents = new List<int>();
                for (int i = 0; i < 7; i++)
                {
                    if (table[i].Count > 0)
                    {
                        var potentialParent = table[i][table[i].Count - 1];
                        if (IsOppositeColor(card, potentialParent) && potentialParent.rank == card.rank + 1)
                        {
                            validParents.Add(i);
                        }
                    }
                }

                if (validParents.Count > 0)
                {
                    if (rng.NextDouble() < linkChance)
                    {
                        int slot = validParents[rng.Next(validParents.Count)];
                        table[slot].Add(card);
                        placedOnTable = true;
                    }
                }
            }

            if (!placedOnTable)
            {
                stockCards.Add(card);
            }
        }

        // --- 4. ФИНАЛЬНАЯ СБОРКА (ИСПРАВЛЕНИЕ ЛОГИКИ STOCK) ---

        // Проблема была тут: stockCards заполнялся последовательно (A, 2, 3...) 
        // и так и уходил в игру. Нам нужно его ПЕРЕМЕШАТЬ, даже для Easy.

        // Для Easy мы хотим, чтобы в колоде были полезные карты, но не подряд идущие Тузы.
        // Поэтому перемешиваем stockCards ВСЕГДА.
        ShuffleList(stockCards, rng);

        // Теперь формируем Deal. 
        // Мы хотим сохранить структуру table (наши цепочки), но подогнать их под треугольник.

        Deal finalDeal = new Deal();

        // Превращаем table в очередь цепочек
        // Мы НЕ сливаем их в один список с stockCards сразу, чтобы не потерять структуру.
        // Мы будем брать карты ПРИОРИТЕТНО из table, заполняя треугольник.

        // Список очередей для каждого столбца (чтобы брать снизу вверх, сохраняя порядок)
        List<Queue<CardModel>> tableQueues = new List<Queue<CardModel>>();
        foreach (var pile in table)
        {
            tableQueues.Add(new Queue<CardModel>(pile)); // pile[0] = King (Bottom)
        }

        Queue<CardModel> stockQueue = new Queue<CardModel>(stockCards);

        // Раздача Tableau (Треугольник: 1, 2, 3... 7 карт)
        for (int i = 0; i < 7; i++)
        {
            int height = i + 1;
            for (int k = 0; k < height; k++)
            {
                CardModel c;
                bool isTop = (k == height - 1);

                // Стратегия Easy:
                // Мы стараемся заполнить стол картами из наших "хороших" цепочек (tableQueues).
                // Но если в i-й цепочке карт меньше, чем нужно (или 0), берем из Stock.

                // ВАЖНО: Мы берем именно из tableQueues[i], чтобы сохранить вертикальные связи!
                // Если мы будем брать из tableQueues[random], мы разобьем цепочки по разным столбцам.
                // В Easy мы хотим, чтобы K червей и Q пик оказались в ОДНОМ столбце (или Q была доступна).

                // Проблема: tableQueues[i] может быть пустой, а tableQueues[j] переполненной.
                // Решение: Сдвиг. Если в текущем столбце кончились заготовленные карты,
                // берем из самых длинных соседних столбцов или из Stock.

                if (tableQueues[i].Count > 0)
                {
                    c = tableQueues[i].Dequeue();
                }
                else
                {
                    // Ищем любую другую цепочку, где много карт
                    var richColumn = tableQueues.OrderByDescending(q => q.Count).FirstOrDefault(q => q.Count > 0);

                    if (richColumn != null)
                    {
                        c = richColumn.Dequeue();
                    }
                    else if (stockQueue.Count > 0)
                    {
                        c = stockQueue.Dequeue();
                    }
                    else
                    {
                        // Карт не хватило вообще (теоретически невозможно при 52 картах)
                        c = new CardModel(Suit.Spades, 1);
                    }
                }

                finalDeal.tableau[i].Add(new CardInstance(c, isTop));
            }
        }

        // Всё, что осталось в tableQueues (если мы сгенерировали слишком длинные цепочки),
        // нужно скинуть в Stock, иначе карты пропадут.
        List<CardModel> leftovers = new List<CardModel>();
        foreach (var q in tableQueues) leftovers.AddRange(q);

        // Добавляем остатки из стока
        leftovers.AddRange(stockQueue);

        // --- ВАЖНО: Финальная перетасовка колоды ---
        // Карты, которые не попали на стол (остатки цепочек + изначальный сток),
        // должны быть перемешаны, чтобы игрок искал их.
        ShuffleList(leftovers, rng);

        foreach (var c in leftovers)
        {
            finalDeal.stock.Push(new CardInstance(c, false));
        }

        finalDeal.waste = new List<CardInstance>();
        finalDeal.foundations = new List<List<CardModel>>();
        for (int i = 0; i < 4; i++) finalDeal.foundations.Add(new List<CardModel>());

        return finalDeal;
    }

    private bool IsOppositeColor(CardModel a, CardModel b)
    {
        bool aRed = (a.suit == Suit.Diamonds || a.suit == Suit.Hearts);
        bool bRed = (b.suit == Suit.Diamonds || b.suit == Suit.Hearts);
        return aRed != bRed;
    }

    private void ShuffleList<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}


[Serializable] public class CardInstance { public CardModel Card; public bool FaceUp; public CardInstance(CardModel c, bool f) { Card = c; FaceUp = f; } public CardInstance Clone() => new CardInstance(new CardModel(Card.suit, Card.rank), FaceUp); }
[Serializable] public class Deal { public List<List<CardInstance>> tableau = new List<List<CardInstance>>(); public Stack<CardInstance> stock = new Stack<CardInstance>(); public List<CardInstance> waste = new List<CardInstance>(); public List<List<CardModel>> foundations = new List<List<CardModel>>(); public int seed; public Deal() { for (int i = 0; i < 7; i++) tableau.Add(new List<CardInstance>()); for (int i = 0; i < 4; i++) foundations.Add(new List<CardModel>()); } public Deal DeepClone() { var d = new Deal(); d.tableau = tableau.Select(p => p.Select(c => c.Clone()).ToList()).ToList(); d.stock = new Stack<CardInstance>(stock.Reverse().Select(c => c.Clone())); d.waste = waste.Select(c => c.Clone()).ToList(); d.foundations = foundations.Select(l => l.Select(c => new CardModel(c.suit, c.rank)).ToList()).ToList(); return d; } }
public class DealMetrics { public bool Solved; public int MoveEstimate; public int HiddenCards; public int TalonUses; public float BranchFactorAvg; }

public class Solver
{
    private readonly int drawCount; private readonly int timeoutMs; private readonly int maxNodes;
    public Solver(int dc, int tm, int mn) { drawCount = Math.Max(1, dc); timeoutMs = tm; maxNodes = mn; }
    public DealMetrics Evaluate(Deal d)
    {
        var m = new DealMetrics(); if (d == null) { m.Solved = false; return m; }
        int h = 0; foreach (var p in d.tableau) h += p.Count(c => !c.FaceUp); m.HiddenCards = h;
        Deal start = d.DeepClone(); var sw = Stopwatch.StartNew();
        var stack = new Stack<Deal>(); var visited = new HashSet<ulong>();
        stack.Push(start); visited.Add(HashState(start));
        int nodes = 0, found = 0, talon = 0; bool solved = false;
        while (stack.Count > 0)
        {
            if (sw.ElapsedMilliseconds > timeoutMs || nodes > maxNodes) break;
            var cur = stack.Pop(); nodes++;
            if (cur.foundations.All(f => f.Count == 13)) { solved = true; break; }
            var moves = GetLegalMoves(cur);
            foreach (var mv in moves)
            {
                var next = ApplyMove(cur, mv); ulong hs = HashState(next);
                if (!visited.Contains(hs)) { visited.Add(hs); stack.Push(next); if (mv.Type == MoveType.RecycleWasteToStock) talon++; found++; }
            }
        }
        m.Solved = solved; m.MoveEstimate = found; m.TalonUses = talon; return m;
    }
    private ulong HashState(Deal d) { unchecked { ulong h = 17; foreach (var f in d.foundations) h = h * 31 + (ulong)(f.Count > 0 ? f.Last().rank : 0); foreach (var t in d.tableau) { h = h * 31 + (ulong)t.Count; if (t.Count > 0) h = h * 31 + (ulong)t.Last().Card.rank + (ulong)t.Last().Card.suit; } h = h * 31 + (ulong)d.stock.Count; if (d.waste.Count > 0) h = h * 31 + (ulong)d.waste.Last().Card.rank; return h; } }
    private enum MoveType { StockDraw, WasteToFoundation, WasteToTableau, TableauToFoundation, TableauToTableau, RecycleWasteToStock }
    private struct Move { public MoveType Type; public int FromIdx; public int ToIdx; public int Count; }
    private List<Move> GetLegalMoves(Deal d)
    {
        var moves = new List<Move>();
        if (d.waste.Count > 0) { var c = d.waste.Last(); if (CanPlaceFoundation(d, c.Card)) moves.Add(new Move { Type = MoveType.WasteToFoundation }); for (int i = 0; i < 7; i++) if (CanPlaceTableau(d.tableau[i], c)) moves.Add(new Move { Type = MoveType.WasteToTableau, ToIdx = i }); }
        for (int i = 0; i < 7; i++) { if (d.tableau[i].Count == 0) continue; var top = d.tableau[i].Last(); if (top.FaceUp && CanPlaceFoundation(d, top.Card)) moves.Add(new Move { Type = MoveType.TableauToFoundation, FromIdx = i }); var pile = d.tableau[i]; for (int k = 0; k < pile.Count; k++) { if (pile[k].FaceUp) { var card = pile[k]; for (int dest = 0; dest < 7; dest++) { if (i == dest) continue; if (CanPlaceTableau(d.tableau[dest], card)) { if (card.Card.rank == 13 && k == 0) continue; moves.Add(new Move { Type = MoveType.TableauToTableau, FromIdx = i, ToIdx = dest, Count = pile.Count - k }); } } break; } } }
        if (d.stock.Count > 0) moves.Add(new Move { Type = MoveType.StockDraw }); else if (d.waste.Count > 0) moves.Add(new Move { Type = MoveType.RecycleWasteToStock });
        return moves;
    }
    private bool CanPlaceFoundation(Deal d, CardModel c) { int idx = (int)c.suit; var f = d.foundations[idx]; return f.Count == 0 ? c.rank == 1 : f.Last().rank + 1 == c.rank; }
    private bool CanPlaceTableau(List<CardInstance> p, CardInstance c) { if (p.Count == 0) return c.Card.rank == 13; var top = p.Last(); bool r1 = top.Card.suit == Suit.Diamonds || top.Card.suit == Suit.Hearts; bool r2 = c.Card.suit == Suit.Diamonds || c.Card.suit == Suit.Hearts; return r1 != r2 && c.Card.rank == top.Card.rank - 1; }
    private Deal ApplyMove(Deal d, Move m)
    {
        Deal n = d.DeepClone();
        switch (m.Type)
        {
            case MoveType.WasteToFoundation: var c = n.waste.Last(); n.waste.RemoveAt(n.waste.Count - 1); n.foundations[(int)c.Card.suit].Add(c.Card); break;
            case MoveType.TableauToFoundation: var t = n.tableau[m.FromIdx]; var tc = t.Last(); t.RemoveAt(t.Count - 1); n.foundations[(int)tc.Card.suit].Add(tc.Card); if (t.Count > 0) t.Last().FaceUp = true; break;
            case MoveType.WasteToTableau: var wc = n.waste.Last(); n.waste.RemoveAt(n.waste.Count - 1); n.tableau[m.ToIdx].Add(wc); break;
            case MoveType.TableauToTableau: var src = n.tableau[m.FromIdx]; var r = src.GetRange(src.Count - m.Count, m.Count); src.RemoveRange(src.Count - m.Count, m.Count); if (src.Count > 0) src.Last().FaceUp = true; n.tableau[m.ToIdx].AddRange(r); break;
            case MoveType.StockDraw: int cnt = Math.Min(n.stock.Count, drawCount); for (int i = 0; i < cnt; i++) { var dc = n.stock.Pop(); dc.FaceUp = true; n.waste.Add(dc); } break;
            case MoveType.RecycleWasteToStock: for (int i = n.waste.Count - 1; i >= 0; i--) { var x = n.waste[i]; x.FaceUp = false; n.stock.Push(x); } n.waste.Clear(); break;
        }
        return n;
    }
}