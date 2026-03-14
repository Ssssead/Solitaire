using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class SpiderDataAnalyzer : MonoBehaviour
{
    [Header("Input Data")]
    public TextAsset msDealsJson;

    [Header("Settings")]
    public float maxFrameTimeMs = 25f;
    public int maxSearchNodes = 1500000;
    public int maxDepth = 2500;

    [Header("Suit Filters")]
    public bool run1Suit = true;
    public bool run2Suits = true;
    public bool run4Suits = true;

    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }

    private List<SpiderMetrics> allMetrics = new List<SpiderMetrics>();
    private string detailedLogPath;

    private void Start()
    {
        detailedLogPath = Application.dataPath + "/SpiderDetailedLogs.txt";
        if (msDealsJson != null) StartCoroutine(RunDeepAnalysis());
    }

    [ContextMenu("Run Analysis")]
    public void StartAnalysisManual()
    {
        detailedLogPath = Application.dataPath + "/SpiderDetailedLogs.txt";
        if (Application.isPlaying) StartCoroutine(RunDeepAnalysis());
        else Debug.LogWarning("Run in Play Mode!");
    }

    private void AppendToLog(string text)
    {
        try { File.AppendAllText(detailedLogPath, text + "\n"); }
        catch (Exception e) { Debug.LogError("Failed to write log: " + e.Message); }
    }

    // Вспомогательный метод для отображения мастей буквами
    private char GetSuitChar(byte suit)
    {
        // Clubs = 0, Diamonds = 1, Hearts = 2, Spades = 3
        string suits = "CDHS"; 
        return suit < suits.Length ? suits[suit] : '?';
    }

    private IEnumerator RunDeepAnalysis()
    {
        Debug.Log("<color=cyan>[Analyzer] Starting Solver with FIXED REVEAL LOGIC & CHAR SUITS...</color>");

        File.WriteAllText(detailedLogPath, "=== SPIDER SOLVER DETAILED LOGS ===\n\n");
        allMetrics.Clear();

        var data = JsonUtility.FromJson<SaveDataWrapper>(msDealsJson.text);

        int totalDeals = data.queues
            .Where(q => q.type == GameType.Spider &&
                       ((q.param == 1 && run1Suit) ||
                        (q.param == 2 && run2Suits) ||
                        (q.param == 4 && run4Suits)))
            .Sum(q => q.deals.Count);

        int processed = 0;
        int brokenCount = 0;

        foreach (var queue in data.queues)
        {
            if (queue.type != GameType.Spider) continue;

            if (queue.param == 1 && !run1Suit) continue;
            if (queue.param == 2 && !run2Suits) continue;
            if (queue.param == 4 && !run4Suits) continue;

            for (int i = 0; i < queue.deals.Count; i++)
            {
                Deal deal = UnpackDeal(queue.deals[i]);
                string dealId = $"{queue.diff}_{queue.param}Suits_Deal{i + 1}";

                SpiderMetrics metrics = new SpiderMetrics
                {
                    DealId = dealId,
                    Difficulty = queue.diff.ToString(),
                    SuitsCount = queue.param
                };

                if (!IsDealValid(deal, dealId, queue.param))
                {
                    Debug.LogWarning($"<color=orange>Skipping {dealId} - Broken data.</color>");
                    metrics.Difficulty = "BROKEN";
                    metrics.IsSolved = false;
                    allMetrics.Add(metrics);
                    processed++;
                    brokenCount++;
                    continue;
                }

                CalculateStaticMetrics(deal, metrics);
                yield return StartCoroutine(RunAStarSearch(deal, metrics));

                allMetrics.Add(metrics);
                processed++;

                if (metrics.IsSolved)
                    Debug.Log($"<color=green>Processed {processed}/{totalDeals}: {metrics.DealId} | States: {metrics.StatesVisited} | Solved: TRUE</color>");
                else
                    Debug.Log($"<color=red>Processed {processed}/{totalDeals}: {metrics.DealId} | States: {metrics.StatesVisited} | Solved: FALSE</color>");
            }
        }

        Debug.Log($"<color=cyan>Analysis Complete! Detailed logs saved to: {detailedLogPath}</color>");
        ExportToCSV();
    }

    private bool IsDealValid(Deal d, string dealId, int suitsCount)
    {
        int[,] rankSuitCounts = new int[14, 10];
        int totalCards = 0;
        Action<CardInstance> countCard = (c) => {
            int r = c.Card.rank; int s = (int)c.Card.suit;
            if (r >= 1 && r <= 13 && s >= 0 && s < 10) rankSuitCounts[r, s]++;
            totalCards++;
        };
        foreach (var col in d.tableau) foreach (var c in col) countCard(c);
        foreach (var c in d.stock) countCard(c);
        if (totalCards != 104) return false;
        for (int r = 1; r <= 13; r++)
        {
            int totalRankCount = 0;
            List<int> activeSuits = new List<int>();
            for (int s = 0; s < 10; s++)
            {
                totalRankCount += rankSuitCounts[r, s];
                if (rankSuitCounts[r, s] > 0) activeSuits.Add(rankSuitCounts[r, s]);
            }
            if (totalRankCount != 8) return false;
            if (suitsCount == 1 && (activeSuits.Count != 1 || activeSuits[0] != 8)) return false;
            if (suitsCount == 2 && (activeSuits.Count != 2 || activeSuits[0] != 4 || activeSuits[1] != 4)) return false;
            if (suitsCount == 4 && (activeSuits.Count != 4 || activeSuits.Any(count => count != 2))) return false;
        }
        return true;
    }

    public struct Card { public byte rank; public byte suit; public bool faceUp; }
    public struct InternalDeal
    {
        public List<Card>[] tableau; public List<Card> stock;
        public InternalDeal Clone()
        {
            InternalDeal d = new InternalDeal(); d.tableau = new List<Card>[10];
            for (int i = 0; i < 10; i++) d.tableau[i] = new List<Card>(this.tableau[i]);
            d.stock = new List<Card>(this.stock); return d;
        }
    }

    private InternalDeal ConvertToInternal(Deal d)
    {
        InternalDeal id = new InternalDeal(); id.tableau = new List<Card>[10];
        for (int i = 0; i < 10; i++)
        {
            id.tableau[i] = new List<Card>();
            foreach (var c in d.tableau[i]) id.tableau[i].Add(new Card { rank = (byte)c.Card.rank, suit = (byte)(int)c.Card.suit, faceUp = c.FaceUp });
        }
        id.stock = new List<Card>(); var stockArr = d.stock.ToArray(); Array.Reverse(stockArr);
        foreach (var c in stockArr) id.stock.Add(new Card { rank = (byte)c.Card.rank, suit = (byte)(int)c.Card.suit, faceUp = c.FaceUp });
        return id;
    }

    private IEnumerator RunAStarSearch(Deal startDeal, SpiderMetrics metrics)
    {
        InternalDeal internalRoot = ConvertToInternal(startDeal);
        var openSet = new PriorityQueue<SearchNode>();
        var closedSet = new HashSet<ulong>();

        var root = new SearchNode(internalRoot, null, default, 0, 0);
        openSet.Enqueue(root, 0);

        int statesVisited = 0;
        SearchNode winningNode = null;
        SearchNode bestNode = root;
        int maxScore = int.MinValue;

        var sw = Stopwatch.StartNew();

        while (openSet.Count > 0)
        {
            if (sw.ElapsedMilliseconds > maxFrameTimeMs) { yield return null; sw.Restart(); }
            if (statesVisited >= maxSearchNodes) break;

            var current = openSet.Dequeue();
            statesVisited++;

            int cardsLeft = current.State.tableau.Sum(c => c.Count) + current.State.stock.Count;
            int hidden = current.State.tableau.Sum(c => c.Count(x => !x.faceUp));

            int score = (current.SuitsCompleted * 100000) - (cardsLeft * 100) - (hidden * 10) + current.Depth;
            if (score > maxScore) { maxScore = score; bestNode = current; }

            if (cardsLeft == 0 || current.SuitsCompleted == 8) { winningNode = current; break; }
            if (current.Depth > maxDepth) continue;

            ulong stateHash = ComputeSymmetricHash(current.State);
            if (closedSet.Contains(stateHash)) continue;
            closedSet.Add(stateHash);

            List<MoveCommand> moves;
            if (metrics.SuitsCount == 1) moves = GetMoves_1Suit(current.State);
            else moves = GetMoves_MultiSuit(current.State, metrics.SuitsCount);

            foreach (var move in moves)
            {
                int newSuits = 0;
                InternalDeal nextState = ApplyMove(current.State, move, out newSuits);
                int totalSuits = current.SuitsCompleted + newSuits;

                int fCost;
                if (metrics.SuitsCount == 1) fCost = Heuristic_1Suit(nextState, totalSuits) + current.Depth;
                else fCost = Heuristic_MultiSuit(nextState, totalSuits, metrics.SuitsCount) + current.Depth;

                SearchNode nextNode = new SearchNode(nextState, current, move, current.Depth + 1, totalSuits);
                openSet.Enqueue(nextNode, fCost);
            }
        }

        metrics.StatesVisited = statesVisited;
        metrics.IsSolved = winningNode != null;

        if (winningNode != null)
        {
            metrics.CompletedSuits = 8;
            ExtractDynamicMetrics(winningNode, metrics);
            AppendToLog(GetSuccessReport(winningNode, metrics));
        }
        else
        {
            metrics.CompletedSuits = bestNode.SuitsCompleted;
            string failLog = GetFailureReport(bestNode, metrics.DealId, statesVisited);
            AppendToLog(failLog);
            Debug.LogWarning($"Deal Failed: {metrics.DealId}. See SpiderDetailedLogs.txt for details.");
        }
    }

    private List<MoveCommand> GetMoves_1Suit(InternalDeal d)
    {
        List<MoveCommand> moves = new List<MoveCommand>();
        bool hasEmptyCols = d.tableau.Any(c => c.Count == 0);

        for (int from = 0; from < 10; from++)
        {
            if (d.tableau[from].Count == 0) continue;
            int maxHeight = GetValidSequenceHeight(d.tableau[from]);

            int hiddenInFrom = d.tableau[from].Count(c => !c.faceUp);
            int faceUpCount = d.tableau[from].Count - hiddenInFrom;

            for (int count = maxHeight; count >= 1; count--)
            {
                var movingCard = d.tableau[from][d.tableau[from].Count - count];
                bool isSplit = (count < maxHeight);
                if (isSplit) continue;

                // ИСПРАВЛЕНА ЛОГИКА ВСКРЫТИЯ
                bool revealsHidden = (count == faceUpCount) && (hiddenInFrom > 0);
                bool emptiesCol = (count == faceUpCount) && (hiddenInFrom == 0);
                bool movedToEmpty = false;

                for (int to = 0; to < 10; to++)
                {
                    if (from == to) continue;

                    if (d.tableau[to].Count == 0)
                    {
                        if (emptiesCol || movedToEmpty) continue;
                        movedToEmpty = true;
                        moves.Add(new MoveCommand { Type = MoveType.MoveColumn, From = from, To = to, Count = count, IsInSuit = true, IsSplit = false, RevealsHidden = revealsHidden, EmptiesColumn = emptiesCol });
                    }
                    else
                    {
                        var target = d.tableau[to].Last();
                        if (target.rank == movingCard.rank + 1)
                        {
                            moves.Add(new MoveCommand { Type = MoveType.MoveColumn, From = from, To = to, Count = count, IsInSuit = true, IsSplit = false, RevealsHidden = revealsHidden, EmptiesColumn = emptiesCol });
                        }
                    }
                }
            }
        }

        if (d.stock.Count > 0 && !hasEmptyCols) moves.Add(new MoveCommand { Type = MoveType.StockDraw });

        if (hasEmptyCols && d.stock.Count > 0)
        {
            var fills = moves.Where(m => m.Type == MoveType.MoveColumn && d.tableau[m.To].Count == 0).ToList();
            if (fills.Count > 0) return fills;
        }

        var perfectReveals = moves.Where(m => m.Type == MoveType.MoveColumn && m.RevealsHidden && m.IsInSuit).ToList();
        if (perfectReveals.Count > 0) return new List<MoveCommand> { perfectReveals[0] };

        var anyReveals = moves.Where(m => m.Type == MoveType.MoveColumn && m.RevealsHidden).ToList();
        if (anyReveals.Count > 0) return anyReveals;

        var perfectMerges = moves.Where(m => m.Type == MoveType.MoveColumn && m.IsInSuit && d.tableau[m.To].Count > 0 && !m.EmptiesColumn).ToList();
        if (perfectMerges.Count > 0) return perfectMerges;

        return moves;
    }

    private int Heuristic_1Suit(InternalDeal d, int suitsCompleted)
    {
        int h = 0;
        h -= suitsCompleted * 100000;
        h += d.tableau.Sum(c => c.Count(x => !x.faceUp)) * 10000;
        h -= d.tableau.Count(c => c.Count == 0) * 5000;

        foreach (var col in d.tableau)
        {
            int hidden = col.Count(x => !x.faceUp);
            int seqLength = 1;
            for (int i = hidden; i < col.Count - 1; i++)
            {
                var top = col[i]; var bot = col[i + 1];
                if (bot.rank == top.rank - 1) { seqLength++; h -= seqLength * 200; }
                else { seqLength = 1; h += 3000; }
            }
        }
        return h;
    }

    private List<MoveCommand> GetMoves_MultiSuit(InternalDeal d, int suitsCount)
    {
        List<MoveCommand> allMoves = new List<MoveCommand>();
        bool hasEmptyCols = d.tableau.Any(c => c.Count == 0);
        int totalHidden = d.tableau.Sum(c => c.Count(x => !x.faceUp));
        bool isEndgame = (totalHidden == 0 && d.stock.Count == 0);

        for (int from = 0; from < 10; from++)
        {
            if (d.tableau[from].Count == 0) continue;
            int maxHeight = GetValidSequenceHeight(d.tableau[from]);

            int hiddenInFrom = d.tableau[from].Count(c => !c.faceUp);
            int faceUpCount = d.tableau[from].Count - hiddenInFrom;

            for (int count = maxHeight; count >= 1; count--)
            {
                var movingCard = d.tableau[from][d.tableau[from].Count - count];
                bool isSplit = (count < maxHeight);
                bool isPerfectSplit = false;

                if (isSplit)
                {
                    var parent = d.tableau[from][d.tableau[from].Count - count - 1];
                    isPerfectSplit = (parent.suit == movingCard.suit && parent.rank == movingCard.rank + 1);
                }

                // ИСПРАВЛЕНА ЛОГИКА ВСКРЫТИЯ СТОПОК
                bool revealsHidden = (count == faceUpCount) && (hiddenInFrom > 0);
                bool emptiesCol = (count == faceUpCount) && (hiddenInFrom == 0);
                bool movedToEmpty = false;

                for (int to = 0; to < 10; to++)
                {
                    if (from == to) continue;

                    if (d.tableau[to].Count == 0)
                    {
                        if (emptiesCol || movedToEmpty) continue;
                        if (isPerfectSplit) continue; // Запрет рвать идеальную масть ради пустого слота

                        movedToEmpty = true;
                        allMoves.Add(new MoveCommand { Type = MoveType.MoveColumn, From = from, To = to, Count = count, IsInSuit = false, IsSplit = isSplit, RevealsHidden = revealsHidden, EmptiesColumn = emptiesCol });
                    }
                    else
                    {
                        if (isPerfectSplit)
                        {
                            var tCard = d.tableau[to].Last();
                            if (tCard.suit == movingCard.suit) continue;
                        }

                        var target = d.tableau[to].Last();
                        if (target.rank == movingCard.rank + 1)
                        {
                            bool sameSuit = (target.suit == movingCard.suit);

                            bool isPurposeful = true;
                            if (!sameSuit && !revealsHidden && !emptiesCol)
                            {
                                isPurposeful = false;

                                // Проверяем, обнажает ли этот ход полезную открытую карту
                                if (d.tableau[from].Count > count + hiddenInFrom)
                                {
                                    var exposed = d.tableau[from][d.tableau[from].Count - count - 1];

                                    for (int i = 0; i < 10; i++)
                                    {
                                        if (i == from || i == to) continue;
                                        if (d.tableau[i].Count > 0 && d.tableau[i].Last().suit == exposed.suit && d.tableau[i].Last().rank == exposed.rank + 1)
                                        {
                                            isPurposeful = true; break;
                                        }
                                    }

                                    if (!isPurposeful)
                                    {
                                        for (int i = 0; i < 10; i++)
                                        {
                                            if (i == from || i == to) continue;
                                            if (d.tableau[i].Count > 0)
                                            {
                                                int h = GetValidSequenceHeight(d.tableau[i]);
                                                var child = d.tableau[i][d.tableau[i].Count - h];
                                                if (child.suit == exposed.suit && child.rank == exposed.rank - 1)
                                                {
                                                    isPurposeful = true; break;
                                                }
                                            }
                                        }
                                    }

                                    if (!isPurposeful && isEndgame)
                                    {
                                        for (int i = 0; i < 10; i++)
                                        {
                                            if (i == from || i == to || d.tableau[i].Count == 0) continue;
                                            if (d.tableau[i].Last().rank == exposed.rank + 1) { isPurposeful = true; break; }
                                        }
                                    }
                                }

                                // Мягкий буфер: разрешаем перекладку, если есть пустые слоты
                                if (!isPurposeful && hasEmptyCols && !isSplit)
                                {
                                    isPurposeful = true;
                                }
                            }

                            if (isPurposeful)
                            {
                                allMoves.Add(new MoveCommand { Type = MoveType.MoveColumn, From = from, To = to, Count = count, IsInSuit = sameSuit, IsSplit = isSplit, RevealsHidden = revealsHidden, EmptiesColumn = emptiesCol });
                            }
                        }
                    }
                }
            }
        }

        var perfectReveals = allMoves.Where(m => m.RevealsHidden && m.IsInSuit && !m.IsSplit).ToList();
        if (perfectReveals.Count > 0) return perfectReveals;

        var anyReveals = allMoves.Where(m => m.RevealsHidden).ToList();
        var perfectMerges = allMoves.Where(m => m.IsInSuit && !m.IsSplit && d.tableau[m.To].Count > 0 && !m.EmptiesColumn).ToList();
        var emptyColMoves = allMoves.Where(m => m.EmptiesColumn).ToList();
        var inSuitBuilds = allMoves.Where(m => m.IsInSuit).ToList();

        var highTier = new List<MoveCommand>();
        highTier.AddRange(anyReveals);
        foreach (var m in perfectMerges) if (!highTier.Contains(m)) highTier.Add(m);
        if (!hasEmptyCols) foreach (var m in emptyColMoves) if (!highTier.Contains(m)) highTier.Add(m);

        if (highTier.Count > 0) return highTier;

        if (inSuitBuilds.Count > 0) return inSuitBuilds;

        if (d.stock.Count > 0 && !hasEmptyCols) allMoves.Add(new MoveCommand { Type = MoveType.StockDraw });

        return allMoves;
    }

    private int Heuristic_MultiSuit(InternalDeal d, int suitsCompleted, int suitsCount)
    {
        int h = 0;
        h -= suitsCompleted * 2000000;

        int totalHidden = d.tableau.Sum(c => c.Count(x => !x.faceUp));
        h += totalHidden * 10000;

        int emptyCols = d.tableau.Count(c => c.Count == 0);
        h -= emptyCols * 15000;

        // ДИНАМИЧЕСКИЙ СТРАХ КОЛОДЫ
        int stockDealsDone = 5 - (d.stock.Count / 10);
        if (totalHidden > 0)
        {
            h += (stockDealsDone * stockDealsDone) * (totalHidden * 100);
        }

        foreach (var col in d.tableau)
        {
            int hidden = col.Count(x => !x.faceUp);
            if (col.Count == hidden) continue;

            int inSuitStreak = 1;

            for (int i = hidden; i < col.Count - 1; i++)
            {
                var top = col[i]; var bot = col[i + 1];

                if (bot.rank == top.rank - 1)
                {
                    if (bot.suit == top.suit)
                    {
                        inSuitStreak++;
                    }
                    else
                    {
                        h -= (inSuitStreak * inSuitStreak) * (suitsCount == 4 ? 400 : 300);
                        inSuitStreak = 1;

                        int blockPenalty = (hidden > 0) ? (hidden * 250) : (suitsCount == 2 ? 200 : 50);
                        h += blockPenalty;
                    }
                }
                else
                {
                    h -= (inSuitStreak * inSuitStreak) * (suitsCount == 4 ? 400 : 300);
                    inSuitStreak = 1;
                    h += 5000;
                }
            }
            h -= (inSuitStreak * inSuitStreak) * (suitsCount == 4 ? 400 : 300);
        }
        return h;
    }

    private string GetFailureReport(SearchNode node, string dealId, int states)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[LOSS] --- FAILED: {dealId} ---");
        sb.AppendLine($"States Visited: {states} | Max Depth Reached: {node.Depth}");
        sb.AppendLine($"Best State -> Hidden: {node.State.tableau.Sum(c => c.Count(x => !x.faceUp))} | Stock Left: {node.State.stock.Count} | Suits Collected: {node.SuitsCompleted}");
        sb.AppendLine("Tableau State (Closest to win):");
        for (int i = 0; i < 10; i++)
        {
            var col = node.State.tableau[i];
            int hidden = col.Count(x => !x.faceUp);
            string topCards = "";
            for (int k = hidden; k < col.Count; k++)
            {
                // ИСПОЛЬЗУЕМ БУКВЫ ДЛЯ МАСТЕЙ!
                topCards += $"{col[k].rank}{GetSuitChar(col[k].suit)} ";
            }
            sb.AppendLine($"  Col {i}: [{hidden} hidden] -> {topCards}");
        }
        sb.AppendLine("--------------------------------------------------\n");
        return sb.ToString();
    }

    private string GetSuccessReport(SearchNode winNode, SpiderMetrics m)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[WIN] +++ SOLVED: {m.DealId} +++");
        sb.AppendLine($"States Visited: {m.StatesVisited} | Total Moves: {m.TotalMoves} | Stock Deals: {m.TotalStockDeals}");

        List<MoveCommand> path = new List<MoveCommand>();
        SearchNode curr = winNode;
        while (curr.Parent != null) { path.Add(curr.MoveMade); curr = curr.Parent; }
        path.Reverse();

        sb.AppendLine("Move Sequence:");
        for (int i = 0; i < path.Count; i++)
        {
            var move = path[i];
            if (move.Type == MoveType.StockDraw)
                sb.AppendLine($"  {i + 1}. Draw Stock");
            else
                sb.AppendLine($"  {i + 1}. Move {move.Count} card(s) from Col {move.From} to Col {move.To} (InSuit: {move.IsInSuit}, Reveals: {move.RevealsHidden})");
        }
        sb.AppendLine("--------------------------------------------------\n");
        return sb.ToString();
    }

    private ulong ComputeSymmetricHash(InternalDeal d)
    {
        ulong[] colHashes = new ulong[10];
        for (int i = 0; i < 10; i++)
        {
            var col = d.tableau[i];
            ulong h = 17;
            int hidden = col.Count(c => !c.faceUp);
            h = h * 1009 + (ulong)hidden;

            for (int k = 0; k < hidden; k++)
            {
                var card = col[k];
                h = h * 31 + (ulong)(k * 200 + card.rank + card.suit * 14);
            }

            for (int k = hidden; k < col.Count; k++)
            {
                var card = col[k];
                h = h * 1009 + (ulong)card.rank + ((ulong)card.suit * 100);
            }
            colHashes[i] = h;
        }

        Array.Sort(colHashes);
        ulong finalHash = 17;
        foreach (var ch in colHashes) finalHash = unchecked(finalHash * 1009 + ch);
        finalHash = unchecked(finalHash ^ ((ulong)d.stock.Count * 987654321ul));
        return finalHash;
    }

    private InternalDeal ApplyMove(InternalDeal old, MoveCommand m, out int suitsCompleted)
    {
        suitsCompleted = 0;
        InternalDeal d = old.Clone();

        if (m.Type == MoveType.MoveColumn)
        {
            var src = d.tableau[m.From];
            var dst = d.tableau[m.To];
            var range = src.GetRange(src.Count - m.Count, m.Count);
            src.RemoveRange(src.Count - m.Count, m.Count);

            if (src.Count > 0)
            {
                var top = src[src.Count - 1];
                top.faceUp = true;
                src[src.Count - 1] = top;
            }

            dst.AddRange(range);
            if (CheckAndRemoveFullSuit(dst)) suitsCompleted++;
        }
        else
        {
            for (int i = 0; i < 10; i++)
            {
                if (d.stock.Count > 0)
                {
                    var c = d.stock[d.stock.Count - 1];
                    d.stock.RemoveAt(d.stock.Count - 1);
                    c.faceUp = true;
                    d.tableau[i].Add(c);
                    if (CheckAndRemoveFullSuit(d.tableau[i])) suitsCompleted++;
                }
            }
        }
        return d;
    }

    private bool CheckAndRemoveFullSuit(List<Card> pile)
    {
        if (pile.Count < 13) return false;
        var top = pile.Last();
        if (top.rank != 1) return false;

        int startIndex = pile.Count - 13;
        var baseSuit = pile[startIndex].suit;

        for (int i = 0; i < 13; i++)
        {
            if (!pile[startIndex + i].faceUp || pile[startIndex + i].suit != baseSuit || pile[startIndex + i].rank != (13 - i))
                return false;
        }

        pile.RemoveRange(startIndex, 13);
        if (pile.Count > 0)
        {
            var newTop = pile[pile.Count - 1];
            newTop.faceUp = true;
            pile[pile.Count - 1] = newTop;
        }
        return true;
    }

    private int GetValidSequenceHeight(List<Card> pile)
    {
        if (pile.Count == 0) return 0;
        int h = 1;
        for (int i = pile.Count - 1; i > 0; i--)
        {
            var curr = pile[i];
            var prev = pile[i - 1];
            if (!prev.faceUp) break;
            if (curr.suit == prev.suit && curr.rank == prev.rank - 1) h++;
            else break;
        }
        return h;
    }

    private void CalculateStaticMetrics(Deal d, SpiderMetrics m)
    {
        int minDepth = 999;

        // 1. Считаем легальные стартовые ходы (перекладки открытых карт)
        var exposedCards = d.tableau.Where(c => c.Count > 0).Select(c => c.Last().Card).ToList();
        foreach (var c1 in exposedCards)
            foreach (var c2 in exposedCards)
                if (c1.suit != c2.suit || c1.rank != c2.rank) // не та же самая карта
                    if (c2.rank == c1.rank + 1) m.InitialLegalMoves++;

        foreach (var col in d.tableau)
        {
            if (col.Count < minDepth) minDepth = col.Count;
            if (col.Count == 0) continue;

            int hiddenCount = col.Count(c => !c.FaceUp);
            var firstRevealed = col.FirstOrDefault(c => c.FaceUp);

            if (firstRevealed != null)
            {
                if (firstRevealed.Card.rank == 13) m.KingsOnHidden++;
                if (firstRevealed.Card.rank >= 11) m.HighBlockersPenalty += (firstRevealed.Card.rank - 10) * hiddenCount * 50;
            }

            // ИСПРАВЛЕНИЕ: Ищем тузы и двойки в ЗАКРЫТЫХ картах!
            for (int i = 0; i < hiddenCount; i++)
            {
                if (col[i].Card.rank <= 2) m.BuriedAcesAndTwos += (hiddenCount - i);
            }

            // Анализируем хаос в ЗАКРЫТЫХ картах
            for (int i = 0; i < hiddenCount - 1; i++)
            {
                var top = col[i].Card;
                var bottom = col[i + 1].Card;
                if (bottom.rank == top.rank - 1 && bottom.suit == top.suit) m.HiddenInSuitPairs++;
                else m.HiddenRankBreaks++;

                if (top.rank == 13) m.DeepestKing = Mathf.Max(m.DeepestKing, hiddenCount - i);
            }
            if (hiddenCount > 0 && col[hiddenCount - 1].Card.rank == 13) m.DeepestKing = Mathf.Max(m.DeepestKing, 1);
        }
        m.DistanceToFirstEmpty = minDepth;
    }
    private void ExtractDynamicMetrics(SearchNode winNode, SpiderMetrics m)
    {
        List<MoveCommand> path = new List<MoveCommand>();
        SearchNode curr = winNode;
        while (curr.Parent != null) { path.Add(curr.MoveMade); curr = curr.Parent; }
        path.Reverse();

        m.TotalMoves = path.Count;
        int currentNoRevealStreak = 0; bool stockUsedYet = false;

        foreach (var move in path)
        {
            if (move.Type == MoveType.StockDraw) { m.TotalStockDeals++; stockUsedYet = true; currentNoRevealStreak++; }
            else
            {
                if (!stockUsedYet) m.MovesBeforeFirstDeal++;
                if (!move.IsInSuit) m.CrossSuitMovesMade++;
                if (move.IsSplit) m.SequenceSplits++;
            }
        }
    }

    private void ExportToCSV()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("DealId,Difficulty,Suits,Solved,SuitsCollected,InitialMoves,InSuitPairs,CrossPairs,RankBreaks,KingsHidden,BlockerPenalty,DistToEmpty,BuriedAces,HiddenBreaks,HiddenPairs,DeepestKing,TotalMoves,MovesBeforeDeal,DealsUsed,CrossMovesMade,SplitsMade,StatesVisited");
        foreach (var m in allMetrics) sb.AppendLine($"{m.DealId},{m.Difficulty},{m.SuitsCount},{m.IsSolved},{m.CompletedSuits},{m.InitialLegalMoves},{m.InSuitPairs},{m.CrossSuitPairs},{m.RankBreaks},{m.KingsOnHidden},{m.HighBlockersPenalty},{m.DistanceToFirstEmpty},{m.BuriedAcesAndTwos},{m.HiddenRankBreaks},{m.HiddenInSuitPairs},{m.DeepestKing},{m.TotalMoves},{m.MovesBeforeFirstDeal},{m.TotalStockDeals},{m.CrossSuitMovesMade},{m.SequenceSplits},{m.StatesVisited}");
        string path = Application.dataPath + "/SpiderMetrics.csv";
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"<color=green>Saved to: {path}</color>");
    }

    private Deal UnpackDeal(SerializedDeal sDeal)
    {
        Deal d = new Deal();
        for (int i = 0; i < 10; i++) d.tableau.Add(new List<CardInstance>());
        if (sDeal.tableau != null) for (int i = 0; i < sDeal.tableau.Count; i++) foreach (var sCard in sDeal.tableau[i].cards) d.tableau[i].Add(sCard.ToRuntime());
        if (sDeal.stock != null) for (int i = sDeal.stock.Count - 1; i >= 0; i--) d.stock.Push(sDeal.stock[i].ToRuntime());
        return d;
    }

    public enum MoveType { MoveColumn, StockDraw }
    public struct MoveCommand { public MoveType Type; public int From, To, Count; public bool IsInSuit, IsSplit, RevealsHidden, EmptiesColumn; }
    private class SearchNode { public InternalDeal State; public SearchNode Parent; public MoveCommand MoveMade; public int Depth; public int SuitsCompleted; public SearchNode(InternalDeal state, SearchNode parent, MoveCommand move, int depth, int suits) { State = state; Parent = parent; MoveMade = move; Depth = depth; SuitsCompleted = suits; } }

    public class SpiderMetrics { public string DealId, Difficulty; public int SuitsCount; public bool IsSolved; public int CompletedSuits; public int InitialLegalMoves, InSuitPairs, CrossSuitPairs, RankBreaks, KingsOnHidden, HighBlockersPenalty, DistanceToFirstEmpty, BuriedAcesAndTwos, HiddenRankBreaks, HiddenInSuitPairs, DeepestKing, TotalMoves, MovesBeforeFirstDeal, TotalStockDeals, CrossSuitMovesMade, EmptyColumnsCreated, SequenceSplits, MaxDepthWithoutReveal, StatesVisited; }

    private class PriorityQueue<T> { private List<KeyValuePair<T, int>> elements = new List<KeyValuePair<T, int>>(); public int Count => elements.Count; public void Enqueue(T item, int priority) { elements.Add(new KeyValuePair<T, int>(item, priority)); int ci = elements.Count - 1; while (ci > 0) { int pi = (ci - 1) / 2; if (elements[ci].Value >= elements[pi].Value) break; var tmp = elements[ci]; elements[ci] = elements[pi]; elements[pi] = tmp; ci = pi; } } public T Dequeue() { int li = elements.Count - 1; var frontItem = elements[0].Key; elements[0] = elements[li]; elements.RemoveAt(li); --li; int pi = 0; while (true) { int ci = pi * 2 + 1; if (ci > li) break; int rc = ci + 1; if (rc <= li && elements[rc].Value < elements[ci].Value) ci = rc; if (elements[pi].Value <= elements[ci].Value) break; var tmp = elements[pi]; elements[pi] = elements[ci]; elements[ci] = tmp; pi = ci; } return frontItem; } }

}