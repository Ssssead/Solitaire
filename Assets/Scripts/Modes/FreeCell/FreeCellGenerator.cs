using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeCellGenerator : BaseGenerator
{
    public override GameType GameType => GameType.FreeCell;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal deal = new Deal();
        // Создаем 8 списков для 8 столбцов
        deal.tableau = new List<List<CardInstance>>();
        for (int i = 0; i < 8; i++) deal.tableau.Add(new List<CardInstance>());

        // Генерация полной колоды
        List<CardModel> deck = new List<CardModel>();
        foreach (Suit s in Enum.GetValues(typeof(Suit)))
        {
            for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));
        }

        // Тасовка (param можно использовать как Seed)
        int seed = (param != 0) ? param : UnityEngine.Random.Range(1, 1000000);
        System.Random rng = new System.Random(seed);

        int n = deck.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            CardModel value = deck[k];
            deck[k] = deck[n];
            deck[n] = value;
        }

        // Раздача: карты раздаются по одной слева направо
        for (int i = 0; i < deck.Count; i++)
        {
            int colIndex = i % 8;
            // Все карты в FreeCell открыты (FaceUp = true)
            deal.tableau[colIndex].Add(new CardInstance(deck[i], true));
        }

        // Остальные поля пусты
        deal.stock = new Stack<CardInstance>();
        deal.waste = new List<CardInstance>();
        deal.foundations = new List<List<CardModel>>();
        for (int i = 0; i < 4; i++) deal.foundations.Add(new List<CardModel>());

        // ИСПРАВЛЕНИЕ: Передаем пустую структуру или заполняем только существующие поля
        // Если в DealMetrics есть поле winnableProbability, можно добавить его, иначе просто new DealMetrics()
        DealMetrics metrics = new DealMetrics();

        onComplete?.Invoke(deal, metrics);
        yield break;
    }
}