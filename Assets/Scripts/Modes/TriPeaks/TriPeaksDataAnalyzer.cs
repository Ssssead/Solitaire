using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class TriPeaksDataAnalyzer : MonoBehaviour
{
    [Header("Input Data")]
    public TextAsset msDealsJson;

    [Header("Settings")]
    public float maxFrameTimeMs = 15f;

    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<JsonDeal> deals; }
    [Serializable] private class ArrayWrapper { public List<JsonDeal> deals; }
    [Serializable] public class JsonCard { public int suit; public int rank; public bool faceUp; }
    [Serializable] public class JsonTableauRow { public List<JsonCard> cards; }
    [Serializable] public class JsonDeal { public string id; public List<JsonTableauRow> tableau; public List<JsonCard> stock; }

    private struct DealAnalysis
    {
        public string Id;
        public Difficulty Diff;

        // 1. СТАТИЧЕСКИЕ МЕТРИКИ (Топология)
        public int KingsOnPeaks;
        public int KingsInStock;
        public int ColorTraps;       // Красная на черной или наоборот
        public int PeakLateMatches;  // Спасательные карты в конце колоды

        // 2. RANDOM GREEDY (Слепой кликер)
        public int RandWinRate;
        public float RandAvgLeft;

        // 3. SMART GREEDY (Имитация логики человека)
        public int SmartWinRate;
        public float SmartAvgLeft;
        public float SmartUselessDraws;
        public int SmartFirstChain;
        public int SmartMaxChain;

        // 4. DFS OPTIMAL (Математическая сложность графа)
        public bool IsSolvable;
        public int TotalSolutions;
        public int StatesToFirstWin;
        public int BacktracksToFirstWin;
        public int OptStockUsed;
        public int OptMaxChain;
        public float OptBranching;
    }

    private List<DealAnalysis> allAnalyses = new List<DealAnalysis>();
    private string detailedLogPath;

    private static readonly uint[] bMasks = new uint[28];
    static TriPeaksDataAnalyzer()
    {
        bMasks[9] = (1u << 18) | (1u << 19); bMasks[10] = (1u << 19) | (1u << 20); bMasks[11] = (1u << 20) | (1u << 21);
        bMasks[12] = (1u << 21) | (1u << 22); bMasks[13] = (1u << 22) | (1u << 23); bMasks[14] = (1u << 23) | (1u << 24);
        bMasks[15] = (1u << 24) | (1u << 25); bMasks[16] = (1u << 25) | (1u << 26); bMasks[17] = (1u << 26) | (1u << 27);
        bMasks[3] = (1u << 9) | (1u << 10); bMasks[4] = (1u << 10) | (1u << 11); bMasks[5] = (1u << 12) | (1u << 13);
        bMasks[6] = (1u << 13) | (1u << 14); bMasks[7] = (1u << 15) | (1u << 16); bMasks[8] = (1u << 16) | (1u << 17);
        bMasks[0] = (1u << 3) | (1u << 4); bMasks[1] = (1u << 5) | (1u << 6); bMasks[2] = (1u << 7) | (1u << 8);
    }

    [ContextMenu("Run Ultimate Analysis")]
    public void StartAnalysisManual()
    {
        detailedLogPath = Application.dataPath + "/TriPeaks_Ultimate_Metrics.csv";
        if (msDealsJson != null) StartCoroutine(RunDeepAnalysis());
    }

    private IEnumerator RunDeepAnalysis()
    {
        string json = msDealsJson.text.Trim();
        List<Tuple<JsonDeal, Difficulty>> deals = new List<Tuple<JsonDeal, Difficulty>>();

        try { var msc = JsonUtility.FromJson<SaveDataWrapper>(json); if (msc?.queues != null) foreach (var q in msc.queues) if (q.type == GameType.TriPeaks && q.deals != null) foreach (var d in q.deals) deals.Add(new Tuple<JsonDeal, Difficulty>(d, q.diff)); } catch { }
        if (deals.Count == 0) try { var arr = JsonUtility.FromJson<ArrayWrapper>(json); if (arr?.deals != null) foreach (var d in arr.deals) deals.Add(new Tuple<JsonDeal, Difficulty>(d, Difficulty.Medium)); } catch { }
        if (deals.Count == 0) try { var s = JsonUtility.FromJson<JsonDeal>(json); if (s?.tableau != null) deals.Add(new Tuple<JsonDeal, Difficulty>(s, Difficulty.Medium)); } catch { }

        allAnalyses.Clear();
        int processed = 0;

        foreach (var item in deals)
        {
            Deal deal = RebuildDeal(item.Item1);
            if (deal == null) continue;

            DealAnalysis analysis = new DealAnalysis { Id = string.IsNullOrEmpty(item.Item1.id) ? $"Deal_{processed}" : item.Item1.id, Diff = item.Item2 };

            CalculateStaticMetrics(deal, ref analysis);
            RunSimulations(deal, ref analysis);
            yield return null;

            AnalyzeOptimalPath(deal, ref analysis);
            yield return null;

            allAnalyses.Add(analysis);
            processed++;

            string color = analysis.Diff == Difficulty.Hard ? "red" : (analysis.Diff == Difficulty.Medium ? "orange" : "green");
            Debug.Log($"<color={color}>[{processed}/{deals.Count}] {analysis.Diff}</color> | SmartWin: {analysis.SmartWinRate}% | Sols: {analysis.TotalSolutions} | Traps: {analysis.ColorTraps} | Backtracks: {analysis.BacktracksToFirstWin}");
        }

        WriteLogsToFile();
        Debug.Log($"<color=green>[Analyzer] СОХРАНЕНО В: {detailedLogPath}</color>");
    }

    private void CalculateStaticMetrics(Deal deal, ref DealAnalysis analysis)
    {
        int[] tRanks = new int[28]; int[] tSuits = new int[28];
        for (int i = 0; i < 28; i++) { tRanks[i] = deal.tableau[i][0].Card.rank; tSuits[i] = (int)deal.tableau[i][0].Card.suit; }
        int[] sRanks = new int[deal.stock.Count]; var stockArr = deal.stock.ToArray();
        for (int i = 0; i < stockArr.Length; i++) sRanks[i] = stockArr[i].Card.rank;

        // Kings placement
        int kPeaks = 0; for (int i = 0; i <= 2; i++) if (tRanks[i] == 13) kPeaks++;
        int kStock = 0; foreach (int r in sRanks) if (r == 13) kStock++;
        analysis.KingsOnPeaks = kPeaks;
        analysis.KingsInStock = kStock;

        // Color Traps (Визуальный обман)
        int colorTraps = 0;
        for (int i = 0; i < 28; i++)
        {
            uint mask = bMasks[i];
            if (mask == 0) continue;
            bool isRedParent = (tSuits[i] == 1 || tSuits[i] == 2); // Hearts or Diamonds
            for (int child = 0; child < 28; child++)
            {
                if ((mask & (1u << child)) != 0)
                {
                    bool isRedChild = (tSuits[child] == 1 || tSuits[child] == 2);
                    if (isRedParent != isRedChild) colorTraps++;
                }
            }
        }
        analysis.ColorTraps = colorTraps;

        // Peak Late Matches
        int lateStockStart = Mathf.Max(0, sRanks.Length - 6);
        int peakSafeties = 0;
        int[] peakRanks = { tRanks[0], tRanks[1], tRanks[2] };
        foreach (int pr in peakRanks)
            for (int i = lateStockStart; i < sRanks.Length; i++)
                if (IsAdj(pr, sRanks[i])) { peakSafeties++; break; }
        analysis.PeakLateMatches = peakSafeties;
    }

    private void RunSimulations(Deal deal, ref DealAnalysis analysis)
    {
        int[] tRanks = new int[28]; for (int i = 0; i < 28; i++) tRanks[i] = deal.tableau[i][0].Card.rank;
        int[] sRanks = new int[deal.stock.Count]; var stockArr = deal.stock.ToArray();
        for (int i = 0; i < stockArr.Length; i++) sRanks[i] = stockArr[i].Card.rank;

        System.Random rng = new System.Random(deal.GetHashCode());
        int iterations = 100;

        int rWins = 0; int sWins = 0;
        int rLeft = 0; int sLeft = 0;
        int sFirstChainSum = 0; int sMaxChainSum = 0; int sUselessDrawsSum = 0;

        for (int iter = 0; iter < iterations; iter++)
        {
            // RANDOM
            uint tMaskR = (1u << 28) - 1; int cursorR = 1; int rankR = sRanks[0];
            while (tMaskR > 0)
            {
                List<int> moves = GetValidMoves(tMaskR, rankR, tRanks);
                if (moves.Count > 0) { int m = moves[rng.Next(moves.Count)]; tMaskR &= ~(1u << m); rankR = tRanks[m]; }
                else if (cursorR < sRanks.Length) rankR = sRanks[cursorR++]; else break;
            }
            int l1 = 0; for (int i = 0; i < 28; i++) if ((tMaskR & (1u << i)) != 0) l1++;
            if (l1 == 0) rWins++; rLeft += l1;

            // SMART
            uint tMaskS = (1u << 28) - 1; int cursorS = 1; int rankS = sRanks[0];
            int currentChain = 0; int maxChain = 0; bool firstChainActive = true; int uselessDraws = 0;

            while (tMaskS > 0)
            {
                List<int> moves = GetValidMoves(tMaskS, rankS, tRanks);
                if (moves.Count > 0)
                {
                    int bestMove = moves[0]; int bestScore = -1;
                    foreach (int m in moves)
                    {
                        int score = 0;
                        if (GetValidMoves(tMaskS & ~(1u << m), tRanks[m], tRanks).Count > 0) score += 10;
                        for (int i = 0; i < 28; i++) if ((bMasks[i] & (1u << m)) != 0) score++;
                        if (score > bestScore) { bestScore = score; bestMove = m; }
                        else if (score == bestScore && rng.NextDouble() > 0.5) bestMove = m;
                    }
                    tMaskS &= ~(1u << bestMove); rankS = tRanks[bestMove];
                    currentChain++; if (currentChain > maxChain) maxChain = currentChain;
                }
                else
                {
                    if (firstChainActive) { sFirstChainSum += currentChain; firstChainActive = false; }
                    if (cursorS < sRanks.Length)
                    {
                        rankS = sRanks[cursorS++]; currentChain = 0;
                        if (GetValidMoves(tMaskS, rankS, tRanks).Count == 0) uselessDraws++;
                    }
                    else break;
                }
            }
            int l2 = 0; for (int i = 0; i < 28; i++) if ((tMaskS & (1u << i)) != 0) l2++;
            if (l2 == 0) sWins++; sLeft += l2;
            if (firstChainActive) sFirstChainSum += currentChain;
            sMaxChainSum += maxChain;
            sUselessDrawsSum += uselessDraws;
        }

        analysis.RandWinRate = rWins; analysis.RandAvgLeft = (float)rLeft / iterations;
        analysis.SmartWinRate = sWins; analysis.SmartAvgLeft = (float)sLeft / iterations;
        analysis.SmartFirstChain = sFirstChainSum / iterations;
        analysis.SmartMaxChain = sMaxChainSum / iterations;
        analysis.SmartUselessDraws = (float)sUselessDrawsSum / iterations;
    }

    private List<int> GetValidMoves(uint tMask, int currentRank, int[] tRanks)
    {
        List<int> moves = new List<int>(5);
        for (int i = 0; i < 28; i++) if ((tMask & (1u << i)) != 0 && (tMask & bMasks[i]) == 0 && IsAdj(currentRank, tRanks[i])) moves.Add(i);
        return moves;
    }

    private class OptNode { public ulong State; public int Depth; public int Branching; public int StockUsed; public int MaxChain; public int CurrentChain; }

    private void AnalyzeOptimalPath(Deal deal, ref DealAnalysis analysis)
    {
        int[] tRanks = new int[28]; for (int i = 0; i < 28; i++) tRanks[i] = deal.tableau[i][0].Card.rank;
        int[] sRanks = new int[deal.stock.Count]; var stockArr = deal.stock.ToArray();
        for (int i = 0; i < stockArr.Length; i++) sRanks[i] = stockArr[i].Card.rank;

        Dictionary<ulong, int> memo = new Dictionary<ulong, int>(50000);
        int n_sol = 0; int nodes = 0; int firstWinNodes = -1; int firstWinDepth = 0;
        int bestStock = int.MaxValue; int bestMaxChain = 0; long bestBranchingSum = 0; int bestDepth = 1;

        int DFS(OptNode node)
        {
            nodes++;
            // СИЛЬНО УВЕЛИЧЕННЫЕ ЛИМИТЫ
            if (nodes > 500000) return 0;

            uint tMask = (uint)(node.State & 0xFFFFFFF);
            if (tMask == 0)
            {
                if (firstWinNodes == -1) { firstWinNodes = nodes; firstWinDepth = node.Depth; }
                if (node.StockUsed < bestStock) { bestStock = node.StockUsed; bestMaxChain = node.MaxChain; bestBranchingSum = node.Branching; bestDepth = node.Depth; }
                return 1;
            }
            if (memo.TryGetValue(node.State, out int cached)) return cached;

            int cursor = (int)((node.State >> 28) & 0x3F); int rank = (int)((node.State >> 34) & 0xF); int paths = 0;

            List<int> validMoves = GetValidMoves(tMask, rank, tRanks);
            int branchingFactor = validMoves.Count + (cursor < sRanks.Length ? 1 : 0);

            foreach (int move in validMoves)
            {
                int newChain = node.CurrentChain + 1;
                paths += DFS(new OptNode
                {
                    State = (tMask & ~(1u << move)) | ((ulong)cursor << 28) | ((ulong)tRanks[move] << 34),
                    Depth = node.Depth + 1,
                    Branching = node.Branching + branchingFactor,
                    StockUsed = node.StockUsed,
                    CurrentChain = newChain,
                    MaxChain = Mathf.Max(node.MaxChain, newChain)
                });
                if (paths >= 100000) break; // Новый лимит решений 100k
            }

            if (cursor < sRanks.Length && paths < 100000)
                paths += DFS(new OptNode
                {
                    State = tMask | ((ulong)(cursor + 1) << 28) | ((ulong)sRanks[cursor] << 34),
                    Depth = node.Depth + 1,
                    Branching = node.Branching + branchingFactor,
                    StockUsed = cursor + 1,
                    CurrentChain = 0,
                    MaxChain = node.MaxChain
                });

            if (paths > 100000) paths = 100000; memo[node.State] = paths; return paths;
        }

        ulong startState = ((1u << 28) - 1) | (1ul << 28) | ((ulong)sRanks[0] << 34);
        n_sol = DFS(new OptNode { State = startState, Depth = 0, Branching = 0, StockUsed = 1, CurrentChain = 0, MaxChain = 0 });

        analysis.TotalSolutions = n_sol;
        analysis.StatesToFirstWin = firstWinNodes == -1 ? nodes : firstWinNodes;
        analysis.BacktracksToFirstWin = firstWinNodes == -1 ? 0 : (firstWinNodes - firstWinDepth);
        analysis.IsSolvable = n_sol > 0;

        if (analysis.IsSolvable)
        {
            analysis.OptStockUsed = bestStock;
            analysis.OptMaxChain = bestMaxChain;
            analysis.OptBranching = (float)bestBranchingSum / bestDepth;
        }
    }

    private Deal RebuildDeal(JsonDeal j)
    {
        if (j == null || j.tableau == null || j.stock == null) return null;
        Deal d = new Deal(); d.tableau = new List<List<CardInstance>>(); d.stock = new Stack<CardInstance>();
        foreach (var r in j.tableau) { var list = new List<CardInstance>(); if (r.cards?.Count > 0) list.Add(new CardInstance(new CardModel((Suit)r.cards[0].suit, r.cards[0].rank), r.cards[0].faceUp)); d.tableau.Add(list); }
        for (int i = j.stock.Count - 1; i >= 0; i--) d.stock.Push(new CardInstance(new CardModel((Suit)j.stock[i].suit, j.stock[i].rank), j.stock[i].faceUp));
        return d;
    }

    private bool IsAdj(int r1, int r2) { int d = Math.Abs(r1 - r2); return d == 1 || d == 12; }

    private void WriteLogsToFile()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("DealId,Difficulty,KingsOnPeaks,KingsInStock,ColorTraps,PeakLateMatches,RandWinRate,RandAvgLeft,SmartWinRate,SmartAvgLeft,SmartUselessDraws,SmartFirstChain,SmartMaxChain,IsSolvable,TotalSolutions,StatesToFirstWin,BacktracksToFirstWin,OptStockUsed,OptMaxChain,OptAvgBranching");
        foreach (var r in allAnalyses) sb.AppendLine($"{r.Id},{r.Diff},{r.KingsOnPeaks},{r.KingsInStock},{r.ColorTraps},{r.PeakLateMatches},{r.RandWinRate},{r.RandAvgLeft:F1},{r.SmartWinRate},{r.SmartAvgLeft:F1},{r.SmartUselessDraws:F1},{r.SmartFirstChain},{r.SmartMaxChain},{r.IsSolvable},{r.TotalSolutions},{r.StatesToFirstWin},{r.BacktracksToFirstWin},{r.OptStockUsed},{r.OptMaxChain},{r.OptBranching:F2}");
        File.WriteAllText(detailedLogPath, sb.ToString());
    }
}