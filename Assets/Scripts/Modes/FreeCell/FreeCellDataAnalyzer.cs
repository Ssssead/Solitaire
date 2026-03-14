using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class FreeCellDataAnalyzer : MonoBehaviour
{
    [Header("Input Data")]
    public TextAsset freeCellDealsJson;

    [Header("Settings")]
    public float maxFrameTimeMs = 25f;

    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }

    public class FreeCellMetrics
    {
        public string DealId;
        public string DifficultyTag;

        // --- СТАТИЧЕСКИЕ МЕТРИКИ (Хаос, Ловушки, Блокировки) ---
        public int FoundationDeadlocks; // Перекрытые карты одной масти (5 на 3)
        public int TableauDeadlocks;    // Перекрытые карты (Черная 7 на Красной 6)
        public int InitialUnsorted;     // Сколько карт не лежат правильной лесенкой
        public int EffectiveAcesDepth;  // Насколько глубоко лежат тузы (в блоках)
        public int EffectiveTwosDepth;
        public int InitialLegalMoves;   // Кол-во возможных ходов со старта
        public float SequenceDensity;   // Процент собранных цепочек
        public int OneSuitCycles;       // Карты одной масти в одной колонке
        public int AutoPlayTraps;       // Ловушки автосброса

        // --- МЕТРИКИ MFR ---
        public int MinFreeCellsRequired;

        // --- ДИНАМИЧЕСКИЕ МЕТРИКИ (При 4 свободных ячейках) ---
        public int StatesVisited_4FC;
        public int DeadEnds_4FC;
        public int WastedStates_4FC;
        public int SolutionLength_4FC;
    }

    private List<FreeCellMetrics> allMetrics = new List<FreeCellMetrics>();

    private void Start()
    {
        if (freeCellDealsJson != null) StartCoroutine(RunDeepAnalysis());
    }

    [ContextMenu("Run Analysis")]
    public void StartAnalysisManual()
    {
        if (Application.isPlaying) StartCoroutine(RunDeepAnalysis());
        else Debug.LogWarning("Run in Play Mode!");
    }

    private IEnumerator RunDeepAnalysis()
    {
        Debug.Log("<color=cyan>[FC Analyzer] Starting HUGE Data Collection (ALL Metrics)...</color>");
        allMetrics.Clear();

        var data = JsonUtility.FromJson<SaveDataWrapper>(freeCellDealsJson.text);
        int totalDeals = data.queues.Where(q => q.type == GameType.FreeCell).Sum(q => q.deals.Count);
        int processed = 0;

        foreach (var queue in data.queues)
        {
            if (queue.type != GameType.FreeCell) continue;

            for (int i = 0; i < queue.deals.Count; i++)
            {
                Deal deal = UnpackDeal(queue.deals[i]);
                string dealId = $"{queue.diff}_Deal_{i + 1}";

                FreeCellMetrics metrics = new FreeCellMetrics { DealId = dealId, DifficultyTag = queue.diff.ToString() };

                // 1. Сбор всей статики
                AnalyzeStaticBoard(deal, metrics);

                // 2. Поиск MFR (Минимальных ячеек)
                metrics.MinFreeCellsRequired = 5;
                for (int fc = 0; fc <= 4; fc++)
                {
                    FreeCellSolver.ExtendedSolverResult mfrResult = new FreeCellSolver.ExtendedSolverResult();
                    yield return StartCoroutine(FreeCellSolver.SolveAsync(deal, maxFrameTimeMs, fc, mfrResult));
                    if (mfrResult.IsSolved)
                    {
                        metrics.MinFreeCellsRequired = fc;
                        break;
                    }
                }

                // 3. Сбор динамики с полным комфортом (4 ячейки)
                FreeCellSolver.ExtendedSolverResult dynamicResult = new FreeCellSolver.ExtendedSolverResult();
                yield return StartCoroutine(FreeCellSolver.SolveAsync(deal, maxFrameTimeMs, 4, dynamicResult));

                if (dynamicResult.IsSolved)
                {
                  //  metrics.StatesVisited_4FC = dynamicResult.StatesVisited;
                    metrics.DeadEnds_4FC = dynamicResult.DeadEnds;
                   // metrics.WastedStates_4FC = dynamicResult.WastedStates;
                  //  metrics.SolutionLength_4FC = dynamicResult.Moves;
                }

                allMetrics.Add(metrics);
                processed++;
                Debug.Log($"<color=green>Processed {processed}/{totalDeals}: {dealId} gathered.</color>");
            }
        }

        ExportToCSV();
    }

    private void AnalyzeStaticBoard(Deal d, FreeCellMetrics m)
    {
        int foundDead = 0, tabDead = 0, unsorted = 0, aceD = 0, twoD = 0, cycles = 0, legalMoves = 0;

        for (int col = 0; col < 8; col++)
        {
            var pile = d.tableau[col];
            if (pile.Count == 0) continue;

            var topCard = pile.Last().Card;
            if (topCard.rank == 1) legalMoves++;
            for (int j = 0; j < 8; j++)
            {
                if (col == j || d.tableau[j].Count == 0) continue;
                var target = d.tableau[j].Last().Card;
                if (IsOppositeColor(topCard, target) && topCard.rank == target.rank - 1) legalMoves++;
            }

            for (int j = 0; j < pile.Count; j++)
            {
                var c = pile[j].Card;

                // Блокировки и глубина
                int effectiveDepth = 0;
                for (int k = pile.Count - 1; k > j; k--)
                {
                    var cardAbove = pile[k].Card;

                    if (IsOppositeColor(cardAbove, pile[k - 1].Card) == false || cardAbove.rank != pile[k - 1].Card.rank - 1)
                        effectiveDepth++;

                    if (cardAbove.suit == c.suit && cardAbove.rank > c.rank) foundDead++;
                    if (IsOppositeColor(cardAbove, c) == false && cardAbove.rank == c.rank + 1) tabDead++;
                    if (cardAbove.suit == c.suit) cycles++;
                }
                if (pile.Count - 1 > j) effectiveDepth++;

                if (c.rank == 1) aceD += effectiveDepth;
                if (c.rank == 2) twoD += effectiveDepth;

                // Unsorted
                if (j < pile.Count - 1)
                {
                    if (IsOppositeColor(c, pile[j + 1].Card) == false || pile[j + 1].Card.rank != c.rank - 1) unsorted++;
                }
            }
        }

        m.InitialLegalMoves = legalMoves;
        m.FoundationDeadlocks = foundDead;
        m.TableauDeadlocks = tabDead;
        m.InitialUnsorted = unsorted;
        m.EffectiveAcesDepth = aceD;
        m.EffectiveTwosDepth = twoD;
        m.OneSuitCycles = cycles;
    }

    private bool IsOppositeColor(CardModel a, CardModel b)
    {
        bool aIsRed = (a.suit == Suit.Diamonds || a.suit == Suit.Hearts);
        bool bIsRed = (b.suit == Suit.Diamonds || b.suit == Suit.Hearts);
        return aIsRed != bIsRed;
    }

    private void ExportToCSV()
    {
        StringBuilder sb = new StringBuilder();
        // Заголовок
        sb.AppendLine("DealId,Difficulty,MinFreeCellsRequired,StatesVisited_4FC,DeadEnds_4FC,WastedStates_4FC,SolutionLength_4FC,InitialLegalMoves,FoundationDeadlocks,TableauDeadlocks,EffectiveAcesDepth,EffectiveTwosDepth,InitialUnsorted,OneSuitCycles");

        foreach (var m in allMetrics)
        {
            sb.AppendLine($"{m.DealId},{m.DifficultyTag},{m.MinFreeCellsRequired},{m.StatesVisited_4FC},{m.DeadEnds_4FC},{m.WastedStates_4FC},{m.SolutionLength_4FC},{m.InitialLegalMoves},{m.FoundationDeadlocks},{m.TableauDeadlocks},{m.EffectiveAcesDepth},{m.EffectiveTwosDepth},{m.InitialUnsorted},{m.OneSuitCycles}");
        }

        string path = Application.dataPath + "/FreeCell_ALL_Metrics.csv";
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"<color=cyan><b>Giant CSV Table Saved to: {path}</b></color>");
    }

    private Deal UnpackDeal(SerializedDeal sDeal)
    {
        Deal d = new Deal();
        for (int i = 0; i < 8; i++) d.tableau.Add(new List<CardInstance>());
        for (int i = 0; i < 4; i++) d.foundations.Add(new List<CardModel>());
        d.waste = new List<CardInstance>();
        d.stock = new Stack<CardInstance>();
        if (sDeal.tableau != null)
            for (int i = 0; i < sDeal.tableau.Count; i++)
                foreach (var sCard in sDeal.tableau[i].cards) d.tableau[i].Add(sCard.ToRuntime());
        return d;
    }
}