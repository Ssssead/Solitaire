using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class YukonDataAnalyzer : MonoBehaviour
{
    [Header("Input Data")]
    public TextAsset dealsJson;

    [Header("Settings")]
    public float maxFrameTimeMs = 15f;
    public int analyzerStatesLimit = 50000;

    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }
    [Serializable] public class SerializedCard { public int suit; public int rank; public bool faceUp; }
    [Serializable] public class SerializedPile { public List<SerializedCard> cards = new List<SerializedCard>(); }
    [Serializable] public class SerializedDeal { public List<SerializedPile> tableau = new List<SerializedPile>(); public List<SerializedCard> stock = new List<SerializedCard>(); }

    private struct DealAnalysis
    {
        public string Id;
        public string Variant;
        public Difficulty Diff;

        // Топологические метрики (Старые + НОВЫЕ)
        public int InversionDepthSum;
        public int CardsUnderKings;     // НОВАЯ: Сколько карт замуровано под открытыми Королями
        public int FaceDownLowCards;    // НОВАЯ: Сколько Тузов, 2-к и 3-к лежат рубашкой вверх
        public int MaxSortedChain;      // НОВАЯ: Самая длинная уже собранная цепочка карт
        public int ShortestColumnSize;  // НОВАЯ: Длина самого короткого столбца (цена пустого слота)

        // Когнитивные метрики из Солвера
        public bool IsSolved;
        public int SolutionMoves;
        public int SolverScore;
        public int Backtracks;
        public int TrapSeverity;
        public int FirstExposureMove;
    }

    private List<DealAnalysis> allAnalyses = new List<DealAnalysis>();
    private string detailedLogPath;

    [ContextMenu("Run Analysis")]
    public void StartAnalysisManual()
    {
        detailedLogPath = Path.Combine(Application.dataPath, "YukonUltimateMetrics.csv");
        if (dealsJson != null) StartCoroutine(RunDeepAnalysis());
        else Debug.LogError("[YukonAnalyzer] Не назначен JSON файл!");
    }

    private IEnumerator RunDeepAnalysis()
    {
        Debug.Log("[YukonAnalyzer] Старт сбора ультимативных метрик...");
        SaveDataWrapper data = JsonUtility.FromJson<SaveDataWrapper>(dealsJson.text);
        allAnalyses.Clear();

        int totalDeals = data.queues.Sum(q => q.deals.Count);
        int processed = 0;

        foreach (var queue in data.queues)
        {
            if (queue.type != GameType.Yukon) continue;
            string variantName = queue.param == 1 ? "Russian" : "Classic";

            for (int dealIndex = 0; dealIndex < queue.deals.Count; dealIndex++)
            {
                Deal deal = RebuildDeal(queue.deals[dealIndex]);
                if (deal == null) continue;

                DealAnalysis analysis = new DealAnalysis { Id = $"{variantName}_{queue.diff}_{dealIndex}", Variant = variantName, Diff = queue.diff };

                CalculateStaticMetrics(deal, queue.param, ref analysis);

                YukonSolver.ExtendedSolverResult solverRes = new YukonSolver.ExtendedSolverResult();
                yield return StartCoroutine(YukonSolver.SolveAsync(deal, queue.param, maxFrameTimeMs, solverRes, analyzerStatesLimit));

                analysis.IsSolved = solverRes.IsSolved;
              //  analysis.SolutionMoves = solverRes.Moves;
               // analysis.SolverScore = solverRes.Score;

                analysis.Backtracks = solverRes.Backtracks;
              //  analysis.TrapSeverity = solverRes.TrapSeverity;
             //   analysis.FirstExposureMove = solverRes.FirstExposureMove;

                allAnalyses.Add(analysis);
                processed++;
                if (processed % 5 == 0) Debug.Log($"[YukonAnalyzer] Обработано {processed}/{totalDeals}");
            }
        }

        WriteLogsToFile();
        Debug.Log($"<color=green>[YukonAnalyzer] ГОТОВО! Файл сохранен: {detailedLogPath}</color>");
    }

    private void CalculateStaticMetrics(Deal d, int param, ref DealAnalysis analysis)
    {
        analysis.InversionDepthSum = 0;
        analysis.CardsUnderKings = 0;
        analysis.FaceDownLowCards = 0;
        analysis.MaxSortedChain = 0;
        analysis.ShortestColumnSize = 99; // Для поиска минимума

        for (int i = 0; i < 7; i++)
        {
            var pile = d.tableau[i];

            // Расстояние до пустого слота
            if (pile.Count < analysis.ShortestColumnSize) analysis.ShortestColumnSize = pile.Count;

            int currentSortedChain = 1;

            for (int j = 0; j < pile.Count; j++)
            {
                var card = pile[j];
                int depth = (pile.Count - 1) - j;

                // 1. Считаем скрытые стартеры
                if (!card.FaceUp && card.Card.rank <= 3)
                {
                    analysis.FaceDownLowCards++;
                }

                // 2. Считаем массу под открытыми Королями
                if (card.FaceUp && card.Card.rank == 13)
                {
                    // В Unity index 0 - это нижняя карта (ближе к столу), 
                    // поэтому все карты с индексом < j лежат ПОД королем
                    analysis.CardsUnderKings += j;
                }

                // 3. Считаем цепочки уже собранных карт
                if (j > 0 && card.FaceUp && pile[j - 1].FaceUp)
                {
                    var prevCard = pile[j - 1];
                    bool isCorrectSequence = false;

                    if (param == 1) // Russian: Та же масть, на 1 младше (так как идем снизу вверх)
                        isCorrectSequence = (card.Card.suit == prevCard.Card.suit && card.Card.rank == prevCard.Card.rank - 1);
                    else // Classic: Чередование цветов, на 1 младше
                        isCorrectSequence = (IsOppositeColor(card.Card, prevCard.Card) && card.Card.rank == prevCard.Card.rank - 1);

                    if (isCorrectSequence) currentSortedChain++;
                    else currentSortedChain = 1;

                    if (currentSortedChain > analysis.MaxSortedChain) analysis.MaxSortedChain = currentSortedChain;
                }

                // 4. Инверсии
                for (int k = j + 1; k < pile.Count; k++)
                {
                    var coveringCard = pile[k];
                    if (param == 1)
                    {
                        if (coveringCard.Card.suit == card.Card.suit && coveringCard.Card.rank > card.Card.rank)
                            analysis.InversionDepthSum += depth;
                    }
                    else
                    {
                        if (!IsOppositeColor(coveringCard.Card, card.Card) && coveringCard.Card.rank > card.Card.rank)
                            analysis.InversionDepthSum += depth;
                    }
                }
            }
        }
    }

    private bool IsOppositeColor(CardModel a, CardModel b)
    {
        bool rA = (a.suit == Suit.Diamonds || a.suit == Suit.Hearts);
        bool rB = (b.suit == Suit.Diamonds || b.suit == Suit.Hearts);
        return rA != rB;
    }

    private Deal RebuildDeal(SerializedDeal sDeal)
    {
        if (sDeal == null || sDeal.tableau == null) return null;
        Deal d = new Deal();
        d.tableau.Clear();
        foreach (var row in sDeal.tableau)
        {
            List<CardInstance> rowList = new List<CardInstance>();
            if (row.cards != null) foreach (var c in row.cards) rowList.Add(new CardInstance(new CardModel((Suit)c.suit, c.rank), c.faceUp));
            d.tableau.Add(rowList);
        }
        return d;
    }

    private void WriteLogsToFile()
    {
        StringBuilder sb = new StringBuilder();
        // Заголовки включают все новые метрики
        sb.AppendLine("DealId,Variant,Difficulty,IsSolved,SolutionMoves,SolverScore,InversionDepthSum,CardsUnderKings,FaceDownLowCards,MaxSortedChain,ShortestColumnSize,Backtracks,TrapSeverity,FirstExposureMove,PlayerDifficultyRating");

        foreach (var r in allAnalyses)
        {
            sb.AppendLine($"{r.Id},{r.Variant},{r.Diff},{r.IsSolved},{r.SolutionMoves},{r.SolverScore},{r.InversionDepthSum},{r.CardsUnderKings},{r.FaceDownLowCards},{r.MaxSortedChain},{r.ShortestColumnSize},{r.Backtracks},{r.TrapSeverity},{r.FirstExposureMove},");
        }
        File.WriteAllText(detailedLogPath, sb.ToString());
    }
}