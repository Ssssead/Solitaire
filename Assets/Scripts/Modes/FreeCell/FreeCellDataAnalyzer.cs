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

    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }

    public class StructuralMetrics
    {
        public string DealId;
        public string DifficultyTag;

        public int MaxAceDepth;         // Самый глубоко зарытый Туз
        public int TotalAcesDepth;      // Сумма глубин всех Тузов
        public int TotalTwosDepth;      // Сумма глубин всех Двоек

        public int FoundationBlocks;    // Карты лежат поверх младших карт ТОЙ ЖЕ масти
        public int ColorDeadlocks;      // Карты лежат на картах ТОГО ЖЕ цвета
        public int TrappedKings;        // Короли не в корне столбца
        public int HighCardRoots;       // Столбцы, в корне которых лежат В, Д, К

        public int ShallowestCol;       // Размер самого короткого столбца
        public int SafeTabMoves;        // Ходы Tableau-to-Tableau со старта
        public int ReadyToHome;         // Карты, готовые сразу улететь в Дом
    }

    private List<StructuralMetrics> allMetrics = new List<StructuralMetrics>();

    private void Start()
    {
        if (freeCellDealsJson != null) StartCoroutine(RunAnalysis());
    }

    [ContextMenu("Run Analysis Manual")]
    public void StartAnalysisManual()
    {
        if (Application.isPlaying) StartCoroutine(RunAnalysis());
        else Debug.LogWarning("Run in Play Mode!");
    }

    private IEnumerator RunAnalysis()
    {
        Debug.Log("<color=cyan>[FC Analyzer] Starting STRUCTURAL Data Collection...</color>");
        allMetrics.Clear();

        var data = JsonUtility.FromJson<SaveDataWrapper>(freeCellDealsJson.text);
        if (data == null || data.queues == null) { Debug.LogError("Failed to parse JSON!"); yield break; }

        int totalDeals = data.queues.Where(q => q.type == GameType.FreeCell).Sum(q => q.deals.Count);
        int processed = 0;

        foreach (var queue in data.queues)
        {
            if (queue.type != GameType.FreeCell) continue;

            for (int i = 0; i < queue.deals.Count; i++)
            {
                Deal deal = UnpackDeal(queue.deals[i]);
                string dealId = $"{queue.diff}_Deal_{i + 1}";

                StructuralMetrics metrics = new StructuralMetrics { DealId = dealId, DifficultyTag = queue.diff.ToString() };

                AnalyzeStructuralBoard(deal, metrics);

                allMetrics.Add(metrics);
                processed++;

                // Чтобы не вешать Unity, делаем паузу каждый кадр
                yield return null;
            }
        }

        ExportToCSV();
    }

    private void AnalyzeStructuralBoard(Deal d, StructuralMetrics m)
    {
        int maxAceDepth = 0, totalAcesDepth = 0, totalTwosDepth = 0;
        int foundationBlocks = 0, colorDeadlocks = 0, trappedKings = 0, highCardRoots = 0;
        int shallowestCol = 99;
        int safeTabMoves = 0, readyToHome = 0;

        // Поиск доступных ходов со старта
        for (int col = 0; col < 8; col++)
        {
            if (d.tableau[col].Count == 0) continue;
            var topCard = d.tableau[col].Last().Card;

            if (topCard.rank == 1) readyToHome++; // Туз

            for (int j = 0; j < 8; j++)
            {
                if (col == j || d.tableau[j].Count == 0) continue;
                var target = d.tableau[j].Last().Card;
                if (IsOppositeColor(topCard, target) && topCard.rank == target.rank - 1) safeTabMoves++;
            }
        }

        // Анализ внутренностей столбцов
        for (int col = 0; col < 8; col++)
        {
            var pile = d.tableau[col];
            if (pile.Count < shallowestCol) shallowestCol = pile.Count;
            if (pile.Count == 0) continue;

            // Проверка корня (самая нижняя визуально карта, индекс 0)
            if (pile[0].Card.rank >= 11) highCardRoots++; // В, Д, К в корне

            for (int j = 0; j < pile.Count; j++)
            {
                var c = pile[j].Card;
                int depthOverCard = (pile.Count - 1) - j; // Сколько карт лежат НАД текущей

                if (c.rank == 1)
                {
                    totalAcesDepth += depthOverCard;
                    if (depthOverCard > maxAceDepth) maxAceDepth = depthOverCard;
                }
                if (c.rank == 2) totalTwosDepth += depthOverCard;

                // Если Король не в корне (индекс > 0)
                if (c.rank == 13 && j > 0) trappedKings++;

                // Сравниваем карту со всеми, что лежат ПОД ней в этом столбце
                for (int under = 0; under < j; under++)
                {
                    var cardUnder = pile[under].Card;

                    // Ловушка фундамента (например: над Пиковой 3 лежит Пиковая 8)
                    if (c.suit == cardUnder.suit && c.rank > cardUnder.rank)
                    {
                        foundationBlocks++;
                    }
                }

                // Сравниваем карту с той, что лежит непосредственно НАД ней
                if (j < pile.Count - 1)
                {
                    var cardAbove = pile[j + 1].Card;
                    if (!IsOppositeColor(c, cardAbove)) colorDeadlocks++;
                }
            }
        }

        m.MaxAceDepth = maxAceDepth;
        m.TotalAcesDepth = totalAcesDepth;
        m.TotalTwosDepth = totalTwosDepth;
        m.FoundationBlocks = foundationBlocks;
        m.ColorDeadlocks = colorDeadlocks;
        m.TrappedKings = trappedKings;
        m.HighCardRoots = highCardRoots;
        m.ShallowestCol = shallowestCol;
        m.SafeTabMoves = safeTabMoves;
        m.ReadyToHome = readyToHome;
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
        sb.AppendLine("DealId,Difficulty,MaxAceDepth,TotalAcesDepth,TotalTwosDepth,FoundationBlocks,ColorDeadlocks,TrappedKings,HighCardRoots,ShallowestCol,SafeTabMoves,ReadyToHome");

        foreach (var m in allMetrics)
        {
            sb.AppendLine($"{m.DealId},{m.DifficultyTag},{m.MaxAceDepth},{m.TotalAcesDepth},{m.TotalTwosDepth},{m.FoundationBlocks},{m.ColorDeadlocks},{m.TrappedKings},{m.HighCardRoots},{m.ShallowestCol},{m.SafeTabMoves},{m.ReadyToHome}");
        }

        string path = Application.dataPath + "/FreeCell_ADVANCED_Metrics.csv";
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"<color=cyan><b>CSV Saved to: {path}</b></color>");
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