using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class PyramidDataAnalyzer : MonoBehaviour
{
    [Header("Input Data")]
    public TextAsset msDealsJson;

    [Header("Settings")]
    public float maxFrameTimeMs = 25f;

    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<JsonDeal> deals; }
    [Serializable] public class JsonCard { public int suit; public int rank; public bool faceUp; }
    [Serializable] public class JsonTableauRow { public List<JsonCard> cards; }
    [Serializable] public class JsonDeal { public string id; public List<JsonTableauRow> tableau; public List<JsonCard> stock; }

    private struct DealAnalysis
    {
        public string Id;
        public Difficulty Diff;

        // Static Info
        public int Inversions;
        public int CritTraps;
        public int KingsTop;
        public int KingsMid;
        public int KingsBot;
        public int KingsStock;

        // Greedy Info
        public bool GreedySolved;
        public int GreedyCardsLeft;
        public int GreedyMovesMade;

        // A* Dynamic Search Info
        public int StartBranching;
        public int MinRecyclesNeeded;

        // Stats for exactly 0 recycles allowed
        public bool R0_Solved; public int R0_States; public int R0_Depth; public int R0_Backtrack;

        // Stats for exactly 1 recycle allowed
        public bool R1_Solved; public int R1_States; public int R1_Depth; public int R1_Backtrack;

        // Stats for exactly 2 recycles allowed
        public bool R2_Solved; public int R2_States; public int R2_Depth; public int R2_Backtrack;
    }

    private List<DealAnalysis> allAnalyses = new List<DealAnalysis>();
    private string detailedLogPath;
/*
    private void Start()
    {
        detailedLogPath = Application.dataPath + "/PyramidMaxMetrics.csv";
        if (msDealsJson != null) StartCoroutine(RunDeepAnalysis());
    }

    [ContextMenu("Run Analysis Manual")]
    public void StartAnalysisManual()
    {
        detailedLogPath = Application.dataPath + "/PyramidMaxMetrics.csv";
        if (msDealsJson != null) StartCoroutine(RunDeepAnalysis());
    }
/*
    private IEnumerator RunDeepAnalysis()
    {
        Debug.Log("[PyramidAnalyzer] Parsing JSON...");
        SaveDataWrapper data = null;

        try { data = JsonUtility.FromJson<SaveDataWrapper>(msDealsJson.text); }
        catch (Exception e) { Debug.LogError($"[PyramidAnalyzer] Error: {e.Message}"); yield break; }

        if (data == null || data.queues == null) yield break;

        allAnalyses.Clear();
        int totalDeals = data.queues.Sum(q => q.deals.Count);
        int processed = 0;

        try
        {
            foreach (var queue in data.queues)
            {
                if (queue.type != GameType.Pyramid) continue;

                foreach (var dealData in queue.deals)
                {
                    Deal deal = RebuildDeal(dealData);
                    if (deal == null) continue;

                    DealAnalysis analysis = new DealAnalysis
                    {
                        Id = string.IsNullOrEmpty(dealData.id) ? $"Deal_{processed}" : dealData.id,
                        Diff = queue.diff,
                        MinRecyclesNeeded = -1
                    };

                    // 1. Жадный прогон (Симуляция обычного игрока)
                    var greedyRes = PyramidSolver.SolveGreedyDetailed(deal, 2);
                    analysis.GreedySolved = greedyRes.IsSolved;
                    analysis.GreedyCardsLeft = greedyRes.CardsLeftOnBoard;
                    analysis.GreedyMovesMade = greedyRes.MovesMade;

                    // 2. A* Прогон с лимитом 0 прокруток (R0)
                    var r0_res = new PyramidSolver.SolverResult();
                    yield return StartCoroutine(PyramidSolver.SolveAsync(deal, 0, maxFrameTimeMs, r0_res));

                    // Забираем статику из первого прогона
                    analysis.Inversions = r0_res.Inversions; analysis.CritTraps = r0_res.CriticalTraps;
                    analysis.KingsTop = r0_res.KingsTop; analysis.KingsMid = r0_res.KingsMiddle;
                    analysis.KingsBot = r0_res.KingsBottom; analysis.KingsStock = r0_res.KingsStock;
                    analysis.StartBranching = r0_res.StartBranching;

                    analysis.R0_Solved = r0_res.IsSolved; analysis.R0_States = r0_res.StatesVisited;
                    analysis.R0_Depth = r0_res.MaxDepth; analysis.R0_Backtrack = r0_res.Backtracking;
                    if (r0_res.IsSolved && analysis.MinRecyclesNeeded == -1) analysis.MinRecyclesNeeded = 0;

                    // 3. A* Прогон с лимитом 1 прокрутка (R1)
                    var r1_res = new PyramidSolver.SolverResult();
                    yield return StartCoroutine(PyramidSolver.SolveAsync(deal, 1, maxFrameTimeMs, r1_res));
                    analysis.R1_Solved = r1_res.IsSolved; analysis.R1_States = r1_res.StatesVisited;
                    analysis.R1_Depth = r1_res.MaxDepth; analysis.R1_Backtrack = r1_res.Backtracking;
                    if (r1_res.IsSolved && analysis.MinRecyclesNeeded == -1) analysis.MinRecyclesNeeded = 1;

                    // 4. A* Прогон с лимитом 2 прокрутки (R2)
                    var r2_res = new PyramidSolver.SolverResult();
                    yield return StartCoroutine(PyramidSolver.SolveAsync(deal, 2, maxFrameTimeMs, r2_res));
                    analysis.R2_Solved = r2_res.IsSolved; analysis.R2_States = r2_res.StatesVisited;
                    analysis.R2_Depth = r2_res.MaxDepth; analysis.R2_Backtrack = r2_res.Backtracking;
                    if (r2_res.IsSolved && analysis.MinRecyclesNeeded == -1) analysis.MinRecyclesNeeded = 2;

                    allAnalyses.Add(analysis);

                    processed++;
                    if (processed % 5 == 0) Debug.Log($"[PyramidAnalyzer] Processed {processed}/{totalDeals}...");
                }
            }
        }
        finally
        {
            WriteLogsToFile();
            Debug.Log($"<color=green>[PyramidAnalyzer] FINISHED! Logs written to {detailedLogPath}</color>");
        }
    }
*/
    private Deal RebuildDeal(JsonDeal jsonDeal)
    {
        if (jsonDeal == null || jsonDeal.tableau == null || jsonDeal.stock == null) return null;
        Deal d = new Deal();
        d.tableau.Clear(); d.stock.Clear();

        foreach (var row in jsonDeal.tableau)
        {
            List<CardInstance> rowList = new List<CardInstance>();
            if (row.cards != null)
                foreach (var c in row.cards)
                    rowList.Add(new CardInstance(new CardModel((Suit)c.suit, c.rank), c.faceUp));
            d.tableau.Add(rowList);
        }

        for (int i = jsonDeal.stock.Count - 1; i >= 0; i--)
        {
            var c = jsonDeal.stock[i];
            d.stock.Push(new CardInstance(new CardModel((Suit)c.suit, c.rank), c.faceUp));
        }
        return d;
    }

    private void WriteLogsToFile()
    {
        StringBuilder sb = new StringBuilder();

        // Header
        sb.AppendLine("DealId,Difficulty,MinRecyclesNeeded,GreedySolved,GreedyCardsLeft,GreedyMoves," +
                      "Inversions,CritTraps,KingsTop,KingsMid,KingsBot,KingsStock,StartBranching," +
                      "R0_Solved,R0_States,R0_Depth,R0_Backtrack," +
                      "R1_Solved,R1_States,R1_Depth,R1_Backtrack," +
                      "R2_Solved,R2_States,R2_Depth,R2_Backtrack");

        foreach (var r in allAnalyses)
        {
            sb.AppendLine($"{r.Id},{r.Diff},{r.MinRecyclesNeeded},{r.GreedySolved},{r.GreedyCardsLeft},{r.GreedyMovesMade}," +
                          $"{r.Inversions},{r.CritTraps},{r.KingsTop},{r.KingsMid},{r.KingsBot},{r.KingsStock},{r.StartBranching}," +
                          $"{r.R0_Solved},{r.R0_States},{r.R0_Depth},{r.R0_Backtrack}," +
                          $"{r.R1_Solved},{r.R1_States},{r.R1_Depth},{r.R1_Backtrack}," +
                          $"{r.R2_Solved},{r.R2_States},{r.R2_Depth},{r.R2_Backtrack}");
        }
        File.WriteAllText(detailedLogPath, sb.ToString());
    }
}