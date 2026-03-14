using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class SpiderDealValidator : MonoBehaviour
{
    [Header("Input Data")]
    public TextAsset msDealsJson;

    [Serializable] private class SaveDataWrapper { public List<QueueSaveData> queues = new List<QueueSaveData>(); }
    [Serializable] private class QueueSaveData { public GameType type; public Difficulty diff; public int param; public List<SerializedDeal> deals; }

    [ContextMenu("Validate Deals")]
    public void ValidateDeals()
    {
        if (msDealsJson == null)
        {
            Debug.LogError("JSON файл не назначен!");
            return;
        }

        Debug.Log("<color=cyan>[Validator] Начинаю глубокую проверку распределения мастей...</color>");

        var data = JsonUtility.FromJson<SaveDataWrapper>(msDealsJson.text);

        int totalDeals = 0;
        int validDeals = 0;
        int brokenDeals = 0;

        foreach (var queue in data.queues)
        {
            if (queue.type != GameType.Spider) continue;

            for (int i = 0; i < queue.deals.Count; i++)
            {
                totalDeals++;
                Deal deal = UnpackDeal(queue.deals[i]);
                string dealId = $"{queue.diff}_{queue.param}Suits_Deal{i + 1}";

                // Передаем queue.param (количество мастей в уровне: 1, 2 или 4)
                if (IsDealValid(deal, dealId, queue.param))
                {
                    validDeals++;
                }
                else
                {
                    brokenDeals++;
                }
            }
        }

        Debug.Log($"<color=cyan>=== ГЛУБОКАЯ ПРОВЕРКА ЗАВЕРШЕНА ===</color>\nВсего проверено: {totalDeals}\n<color=green>Идеальные расклады: {validDeals}</color>\n<color=red>Сломанные расклады: {brokenDeals}</color>");
    }

    private bool IsDealValid(Deal d, string dealId, int suitsCount)
    {
        // Массив [Ранг, Масть]. Берем с запасом (14 для рангов 1-13, и 10 для мастей на случай смещений enum'а)
        int[,] rankSuitCounts = new int[14, 10];
        int totalCards = 0;

        // Анонимная функция для подсчета
        Action<CardInstance> countCard = (c) =>
        {
            int r = c.Card.rank;
            int s = (int)c.Card.suit;
            if (r >= 1 && r <= 13 && s >= 0 && s < 10)
            {
                rankSuitCounts[r, s]++;
            }
            totalCards++;
        };

        // Собираем статистику со стола и из колоды
        foreach (var col in d.tableau) foreach (var c in col) countCard(c);
        foreach (var c in d.stock) countCard(c);

        bool isBroken = false;
        StringBuilder sb = new StringBuilder();

        if (totalCards != 104)
        {
            isBroken = true;
            sb.AppendLine($" - ОШИБКА: Всего карт {totalCards} (Должно быть ровно 104)");
        }

        // Проверяем распределение мастей для каждого ранга от Туза (1) до Короля (13)
        for (int r = 1; r <= 13; r++)
        {
            int totalRankCount = 0;
            List<int> activeSuits = new List<int>(); // Хранит количество карт в тех мастях, которые присутствуют

            for (int s = 0; s < 10; s++)
            {
                totalRankCount += rankSuitCounts[r, s];
                if (rankSuitCounts[r, s] > 0)
                {
                    activeSuits.Add(rankSuitCounts[r, s]);
                }
            }

            bool isRankBroken = false;
            string errorReason = "";

            if (totalRankCount != 8)
            {
                isRankBroken = true;
                errorReason = $"Найдено {totalRankCount} шт. вместо 8.";
            }
            else
            {
                // Проверка математики Паука
                if (suitsCount == 1)
                {
                    // Должна быть 1 масть, в которой ровно 8 карт
                    if (activeSuits.Count != 1 || activeSuits[0] != 8)
                    {
                        isRankBroken = true; errorReason = "Нарушен баланс 1 масти (должно быть 8 карт одного цвета).";
                    }
                }
                else if (suitsCount == 2)
                {
                    // Должны быть 2 масти, в каждой ровно по 4 карты (например, 4 Пики и 4 Черви)
                    if (activeSuits.Count != 2 || activeSuits[0] != 4 || activeSuits[1] != 4)
                    {
                        isRankBroken = true; errorReason = "Нарушен баланс 2 мастей (должно быть ровно 4+4).";
                    }
                }
                else if (suitsCount == 4)
                {
                    // Должны быть 4 масти, в каждой ровно по 2 карты
                    if (activeSuits.Count != 4 || activeSuits.Any(count => count != 2))
                    {
                        isRankBroken = true; errorReason = "Нарушен баланс 4 мастей (должно быть ровно 2+2+2+2).";
                    }
                }
            }

            // Если для этого ранга есть ошибка, формируем красивый вывод
            if (isRankBroken)
            {
                isBroken = true;
                string distribution = "";
                for (int s = 0; s < 10; s++)
                {
                    if (rankSuitCounts[r, s] > 0)
                    {
                        distribution += $"[Масть ID {s}: {rankSuitCounts[r, s]} шт.] ";
                    }
                }
                sb.AppendLine($" - {GetRankName(r)}: {errorReason} Фактически: {distribution}");
            }
        }

        if (isBroken)
        {
            Debug.LogWarning($"<color=red>СЛОМАННЫЙ РАСКЛАД: {dealId}</color>\n{sb.ToString()}");
            return false;
        }

        return true;
    }

    private string GetRankName(int rank)
    {
        switch (rank)
        {
            case 1: return "Туз (A)";
            case 11: return "Валет (J)";
            case 12: return "Дама (Q)";
            case 13: return "Король (K)";
            default: return rank.ToString();
        }
    }

    private Deal UnpackDeal(SerializedDeal sDeal)
    {
        Deal d = new Deal();
        for (int i = 0; i < 10; i++) d.tableau.Add(new List<CardInstance>());
        if (sDeal.tableau != null)
            for (int i = 0; i < sDeal.tableau.Count; i++)
                foreach (var sCard in sDeal.tableau[i].cards)
                    d.tableau[i].Add(sCard.ToRuntime());
        if (sDeal.stock != null)
            for (int i = sDeal.stock.Count - 1; i >= 0; i--)
                d.stock.Push(sDeal.stock[i].ToRuntime());
        return d;
    }
}