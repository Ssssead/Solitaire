using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class DealSerializer
{
    // Превращаем Deal в компактную строку
    public static string Serialize(Deal deal)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // 1. Tableau (List<List<CardInstance>>)
            writer.Write((byte)deal.tableau.Count);
            foreach (var pile in deal.tableau)
            {
                writer.Write((byte)pile.Count);
                foreach (var card in pile) WriteCardInstance(writer, card);
            }

            // 2. Stock (Stack<CardInstance>) - сохраняем как список
            var stockList = deal.stock.ToArray();
            // Stack хранит в обратном порядке, развернем для сохранения логики "сверху-вниз"
            Array.Reverse(stockList);
            writer.Write((byte)stockList.Length);
            foreach (var card in stockList) WriteCardInstance(writer, card);

            // 3. Waste (List<CardInstance>)
            writer.Write((byte)deal.waste.Count);
            foreach (var card in deal.waste) WriteCardInstance(writer, card);

            // 4. Foundations (List<List<CardModel>>)
            writer.Write((byte)deal.foundations.Count);
            foreach (var pile in deal.foundations)
            {
                writer.Write((byte)pile.Count);
                foreach (var cardModel in pile) WriteCardModel(writer, cardModel);
            }

            return Convert.ToBase64String(ms.ToArray());
        }
    }

    // Восстанавливаем Deal из строки
    public static Deal Deserialize(string data)
    {
        Deal deal = new Deal
        {
            tableau = new List<List<CardInstance>>(),
            stock = new Stack<CardInstance>(),
            waste = new List<CardInstance>(),
            foundations = new List<List<CardModel>>()
        };

        if (string.IsNullOrEmpty(data)) return deal;

        byte[] bytes = Convert.FromBase64String(data);

        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            // 1. Tableau
            int tableauCount = reader.ReadByte();
            for (int i = 0; i < tableauCount; i++)
            {
                var pile = new List<CardInstance>();
                int count = reader.ReadByte();
                for (int j = 0; j < count; j++) pile.Add(ReadCardInstance(reader));
                deal.tableau.Add(pile);
            }

            // 2. Stock
            int stockCount = reader.ReadByte();
            // Читаем в список, потом пушим в стек, чтобы сохранить порядок
            var tempStock = new List<CardInstance>();
            for (int i = 0; i < stockCount; i++) tempStock.Add(ReadCardInstance(reader));
            // Внимание: Stack.Push кладет наверх. Если мы писали [A, B, C], то читаем [A, B, C].
            // Чтобы A было внизу стека, надо пушить A, потом B. 
            // BinaryWriter писал массив, так что порядок прямой.
            foreach (var c in tempStock) deal.stock.Push(c);

            // 3. Waste
            int wasteCount = reader.ReadByte();
            for (int i = 0; i < wasteCount; i++) deal.waste.Add(ReadCardInstance(reader));

            // 4. Foundations
            int foundCount = reader.ReadByte();
            for (int i = 0; i < foundCount; i++)
            {
                var pile = new List<CardModel>();
                int count = reader.ReadByte();
                for (int j = 0; j < count; j++) pile.Add(ReadCardModel(reader));
                deal.foundations.Add(pile);
            }
        }
        return deal;
    }

    // --- Хелперы упаковки байтов ---

    // Упаковываем карту в 1 байт:
    // Биты 0-5: ID карты (0-51)
    // Бит 6: FaceUp (0 или 1)
    private static void WriteCardInstance(BinaryWriter w, CardInstance c)
    {
        byte id = GetCardID(c.Card);
        if (c.FaceUp) id |= 64; // Устанавливаем 7-й бит (значение 64) как флаг FaceUp
        w.Write(id);
    }

    private static void WriteCardModel(BinaryWriter w, CardModel c)
    {
        w.Write(GetCardID(c));
    }

    private static CardInstance ReadCardInstance(BinaryReader r)
    {
        byte b = r.ReadByte();
        bool faceUp = (b & 64) != 0; // Проверяем бит
        byte id = (byte)(b & 63);    // Очищаем флаг, оставляем только ID
        return new CardInstance(GetCardFromID(id), faceUp);
    }

    private static CardModel ReadCardModel(BinaryReader r)
    {
        byte id = r.ReadByte();
        return GetCardFromID(id);
    }

    // ID = (Suit * 13) + (Rank - 1). Диапазон 0..51
    private static byte GetCardID(CardModel c)
    {
        int suitIdx = (int)c.suit;
        int rankIdx = c.rank - 1;
        return (byte)(suitIdx * 13 + rankIdx);
    }

    private static CardModel GetCardFromID(byte id)
    {
        int suitIdx = id / 13;
        int rank = (id % 13) + 1;
        return new CardModel((Suit)suitIdx, rank);
    }
}