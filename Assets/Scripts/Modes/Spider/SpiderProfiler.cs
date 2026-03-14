using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
/*
public class SpiderProfiler : MonoBehaviour
{
    [Header("Input Data")]
    public TextAsset msDealsJson;

    public float frameBudgetMs = 15.0f;

    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }

    private class ProfilerStats
    {
        public int Count = 0;
        public int MinScore = int.MaxValue;
        public int MaxScore = int.MinValue;
        public long TotalScore = 0;

        public void Add(int score)
        {
            Count++;
            TotalScore += score;
            if (score < MinScore) MinScore = score;
            if (score > MaxScore) MaxScore = score;
        }
        public int Average => Count > 0 ? (int)(TotalScore / Count) : 0;
    }

    private void Start()
    {
        if (msDealsJson != null) StartCoroutine(RunProfileSession());
    }

    private IEnumerator RunProfileSession()
    {
        Debug.Log("<color=cyan>[Profiler] Running Analysis with Final User Formula & Survival Architecture...</color>");

        SaveDataWrapper data = JsonUtility.FromJson<SaveDataWrapper>(msDealsJson.text);
        var results = new Dictionary<string, ProfilerStats>();

        foreach (var queue in data.queues)
        {
            if (queue.type != GameType.Spider) continue;

            string key = $"{queue.diff} (Suits: {queue.param})";
            if (!results.ContainsKey(key)) results[key] = new ProfilerStats();

            Debug.Log($"Analyzing {queue.deals.Count} deals for {key}...");

            for (int i = 0; i < queue.deals.Count; i++)
            {
                Deal deal = UnpackDeal(queue.deals[i]);

                SpiderSolver.SolverResult result = new SpiderSolver.SolverResult();
                yield return StartCoroutine(SpiderSolver.SolveAsync(deal, queue.param, frameBudgetMs, result));

                results[key].Add(result.TotalScore);

                Debug.Log($"   Deal {i + 1} | Stat: {result.StaticScore} | Dyn: {result.DynamicScore} (Fail: {!result.IsSolved}, Moves: {result.TotalMoves}, Deals: {result.StockDealsUsed}) | Total: {result.TotalScore}");
            }
        }

        Debug.Log("<color=yellow>=========================================</color>");
        Debug.Log("<color=green>PROFILING COMPLETE! FINAL MATH TARGETS:</color>");

        foreach (var kvp in results)
        {
            var stats = kvp.Value;
            Debug.Log($"<b>{kvp.Key}</b> -> TargetMinScore = {stats.MinScore}, TargetMaxScore = {stats.MaxScore} (Average: {stats.Average})");
        }
        Debug.Log("<color=yellow>=========================================</color>");
    }

    private Deal UnpackDeal(SerializedDeal sDeal)
    {
        Deal d = new Deal();
        for (int i = 0; i < 10; i++) d.tableau.Add(new List<CardInstance>());
        if (sDeal.tableau != null)
        {
            for (int i = 0; i < sDeal.tableau.Count; i++)
                foreach (var sCard in sDeal.tableau[i].cards)
                    d.tableau[i].Add(sCard.ToRuntime());
        }
        if (sDeal.stock != null)
        {
            for (int i = sDeal.stock.Count - 1; i >= 0; i--)
                d.stock.Push(sDeal.stock[i].ToRuntime());
        }
        return d;
    }
}
*/