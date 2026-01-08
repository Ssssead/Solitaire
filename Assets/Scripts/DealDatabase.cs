// DealDatabase.cs
using System;
using System.Collections.Generic;
using UnityEngine;

// Главный ассет базы данных
[CreateAssetMenu(fileName = "SolitaireDealDatabase", menuName = "Solitaire/Deal Database")]
public class DealDatabase : ScriptableObject
{
    // Список наборов раскладов (по категориям)
    public List<DealSet> dealSets = new List<DealSet>();

    /// <summary>
    /// Ищет набор раскладов по параметрам.
    /// </summary>
    public DealSet GetSet(GameType type, Difficulty diff, int param)
    {
        return dealSets.Find(s => s.gameType == type && s.difficulty == diff && s.param == param);
    }
}

// Набор раскладов для конкретного режима (например: Klondike / Easy / Draw 1)
[Serializable]
public class DealSet
{
    public string name; // Для удобства в инспекторе
    public GameType gameType;
    public Difficulty difficulty;
    public int param; // DrawCount или SuitCount

    // Список сохраненных раскладов
    public List<SerializedDeal> deals = new List<SerializedDeal>();
}

// --- СЕРИАЛИЗУЕМЫЕ ОБЪЕКТЫ (Unity-friendly wrappers) ---

// Unity не умеет сохранять List<List<T>> и Stack<T>, поэтому нужны обертки

[Serializable]
public class SerializedDeal
{
    public List<SerializedPile> tableau = new List<SerializedPile>(); // 7 стопок
    public List<SerializedCard> stock = new List<SerializedCard>();   // Колода (список вместо стека)
    // Waste и Foundation обычно пусты при старте, их не сохраняем
}

[Serializable]
public class SerializedPile
{
    public List<SerializedCard> cards = new List<SerializedCard>();
}

[Serializable]
public struct SerializedCard
{
    public int suit;
    public int rank;
    public bool faceUp;

    // Конструктор из CardInstance
    public SerializedCard(CardInstance instance)
    {
        suit = (int)instance.Card.suit;
        rank = instance.Card.rank;
        faceUp = instance.FaceUp;
    }

    // Конвертация обратно в CardInstance
    public CardInstance ToRuntime()
    {
        return new CardInstance(new CardModel((Suit)suit, rank), faceUp);
    }
}