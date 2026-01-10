using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Необходим для работы методов Where, ToList, GetRange
using UnityEngine;

public class FreeCellGenerator : BaseGenerator
{
    public override GameType GameType => GameType.FreeCell;

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal deal = new Deal();
        // Создаем 8 списков для 8 столбцов Tableau
        deal.tableau = new List<List<CardInstance>>();
        for (int i = 0; i < 8; i++) deal.tableau.Add(new List<CardInstance>());

        // 1. Создаем полную колоду (52 карты)
        List<CardModel> deck = new List<CardModel>();
        foreach (Suit s in Enum.GetValues(typeof(Suit)))
        {
            for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));
        }

        // 2. Инициализируем RNG
        // Если param передан (seed), используем его, иначе случайное число
        int seed = (param != 0) ? param : UnityEngine.Random.Range(1, 1000000);
        System.Random rng = new System.Random(seed);

        // 3. Выбираем алгоритм тасовки в зависимости от сложности
        switch (difficulty)
        {
            case Difficulty.Easy:
                ShuffleEasy(deck, rng);
                break;
            case Difficulty.Hard:
                ShuffleHard(deck, rng);
                break;
            case Difficulty.Medium:
            default:
                ShuffleMedium(deck, rng);
                break;
        }

        // 4. Раздаем карты по колонкам (слева направо)
        // FreeCell: первые 4 колонки получают 7 карт, последние 4 — по 6 карт.
        // Цикл i % 8 обеспечивает правильное распределение автоматически.
        for (int i = 0; i < deck.Count; i++)
        {
            int colIndex = i % 8;
            // Во FreeCell все карты изначально открыты (FaceUp = true)
            deal.tableau[colIndex].Add(new CardInstance(deck[i], true));
        }

        // 5. Инициализируем остальные (пустые) зоны
        deal.stock = new Stack<CardInstance>();      // Колоды нет
        deal.waste = new List<CardInstance>();       // Сброса нет
        deal.foundations = new List<List<CardModel>>();
        for (int i = 0; i < 4; i++) deal.foundations.Add(new List<CardModel>());

        // Возвращаем результат
        DealMetrics metrics = new DealMetrics(); // Сюда можно добавить расчет сложности, если нужно
        onComplete?.Invoke(deal, metrics);
        yield break;
    }

    // ==========================================
    //            АЛГОРИТМЫ ГЕНЕРАЦИИ
    // ==========================================

    /// <summary>
    /// EASY: "Поддавки". 
    /// Тузы (Rank 1) и Двойки (Rank 2) помещаются в конец списка раздачи.
    /// Поскольку карты раздаются слоями снизу вверх, последние карты в списке 
    /// оказываются ВИЗУАЛЬНО НАВЕРХУ стопок. Игрок сразу видит ходы.
    /// </summary>
    private void ShuffleEasy(List<CardModel> deck, System.Random rng)
    {
        // Разделяем на "легкие" и "тяжелые"
        var easyCards = deck.Where(c => c.rank <= 2).ToList(); // 8 карт
        var hardCards = deck.Where(c => c.rank > 2).ToList();  // 44 карты

        // Перемешиваем группы внутри себя
        ShuffleList(easyCards, rng);
        ShuffleList(hardCards, rng);

        deck.Clear();
        // Сначала кладем "тяжелые" (уйдут вниз)
        deck.AddRange(hardCards);
        // В конце кладем "легкие" (будут сверху)
        deck.AddRange(easyCards);
    }

    /// <summary>
    /// MEDIUM: "Слоистая тасовка" (Stratified Shuffle).
    /// Гарантирует равномерное распределение важных карт по глубине.
    /// Колода делится на 4 слоя по 13 карт. В каждом слое гарантированно 
    /// находятся ровно 2 карты ранга (Ace/2) и 11 остальных.
    /// Это исключает ситуации, когда все тузы лежат на дне или все наверху.
    /// </summary>
    private void ShuffleMedium(List<CardModel> deck, System.Random rng)
    {
        var criticalCards = deck.Where(c => c.rank <= 2).ToList(); // 8 карт
        var fillerCards = deck.Where(c => c.rank > 2).ToList();    // 44 карты

        // Предварительно мешаем исходные кучки
        ShuffleList(criticalCards, rng);
        ShuffleList(fillerCards, rng);

        deck.Clear();

        int layers = 4;           // Количество слоев
        int criticalPerLayer = 2; // 8 / 4 = 2
        int fillerPerLayer = 11;  // 44 / 4 = 11

        for (int i = 0; i < layers; i++)
        {
            List<CardModel> currentLayer = new List<CardModel>();

            // Берем порцию важных карт
            currentLayer.AddRange(criticalCards.GetRange(i * criticalPerLayer, criticalPerLayer));

            // Берем порцию обычных карт
            currentLayer.AddRange(fillerCards.GetRange(i * fillerPerLayer, fillerPerLayer));

            // Перемешиваем слой, чтобы порядок внутри него был случайным
            ShuffleList(currentLayer, rng);

            // Добавляем слой в итоговую колоду
            deck.AddRange(currentLayer);
        }
    }

    /// <summary>
    /// HARD: "Закопать клад".
    /// Тузы и Двойки помещаются в НАЧАЛО списка раздачи.
    /// Первые карты при раздаче попадают на самое дно колонок (индекс 0).
    /// Игроку придется разобрать почти весь стол, чтобы добраться до тузов.
    /// </summary>
    private void ShuffleHard(List<CardModel> deck, System.Random rng)
    {
        var criticalCards = deck.Where(c => c.rank <= 2).ToList();
        var fillerCards = deck.Where(c => c.rank > 2).ToList();

        ShuffleList(criticalCards, rng);
        ShuffleList(fillerCards, rng);

        deck.Clear();
        // Сначала кладем важные (уйдут на дно)
        deck.AddRange(criticalCards);
        // Сверху засыпаем остальными
        deck.AddRange(fillerCards);
    }

    // Стандартный алгоритм тасовки Фишера-Йетса
    private void ShuffleList<T>(List<T> list, System.Random rng)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}