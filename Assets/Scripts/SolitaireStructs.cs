using System;
using System.Collections.Generic;
using System.Linq;

// --- 1. ВОССТАНОВЛЕННЫЕ ОПРЕДЕЛЕНИЯ ---

// Перечисление сложности (раньше было в генераторе)
public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

// Метрики расклада (нужны для передачи данных в менеджер)
public class DealMetrics
{
    public bool Solved;
    public int MoveEstimate;
    public int StockPasses;
    public int HiddenCards;
}

// --- 2. СТРУКТУРЫ ДАННЫХ ИГРЫ ---

// Класс состояния конкретной карты
[Serializable]
public class CardInstance
{
    public CardModel Card;
    public bool FaceUp;

    public CardInstance(CardModel c, bool f) { Card = c; FaceUp = f; }

    public CardInstance Clone() => new CardInstance(new CardModel(Card.suit, Card.rank), FaceUp);
}

// Класс всего расклада (Tableau, Stock, Waste, Foundations)
[Serializable]
public class Deal
{
    public List<List<CardInstance>> tableau = new List<List<CardInstance>>(); // 7 стопок
    public Stack<CardInstance> stock = new Stack<CardInstance>();             // Колода
    public List<CardInstance> waste = new List<CardInstance>();               // Сброс
    public List<List<CardModel>> foundations = new List<List<CardModel>>();   // 4 дома

    public Deal()
    {
        for (int i = 0; i < 7; i++) tableau.Add(new List<CardInstance>());
        for (int i = 0; i < 4; i++) foundations.Add(new List<CardModel>());
    }

    public Deal DeepClone()
    {
        var d = new Deal();
        d.tableau = tableau.Select(p => p.Select(c => c.Clone()).ToList()).ToList();
        d.stock = new Stack<CardInstance>(stock.Reverse().Select(c => c.Clone()));
        d.waste = waste.Select(c => c.Clone()).ToList();
        d.foundations = foundations.Select(l => new List<CardModel>(l)).ToList();
        return d;
    }
}

// Результат работы Солвера (внутренняя структура для генератора)
public struct SolverResult
{
    public bool IsSolved;
    public Difficulty EstimatedDifficulty;
    public int MovesCount;
    public int StockPasses;
}