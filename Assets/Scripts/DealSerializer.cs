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
        if (deal == null) return "";

        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // 1. Tableau (List<List<CardInstance>>)
            if (deal.tableau != null)
            {
                writer.Write((byte)deal.tableau.Count);
                foreach (var pile in deal.tableau)
                {
                    if (pile != null)
                    {
                        writer.Write((byte)pile.Count);
                        foreach (var card in pile) WriteCardInstance(writer, card);
                    }
                    else writer.Write((byte)0);
                }
            }
            else writer.Write((byte)0);

            // 2. Stock (Stack<CardInstance>)
            if (deal.stock != null)
            {
                var stockList = deal.stock.ToArray();
                Array.Reverse(stockList);
                writer.Write((byte)stockList.Length);
                foreach (var card in stockList) WriteCardInstance(writer, card);
            }
            else writer.Write((byte)0);

            // 3. Waste (List<CardInstance>)
            if (deal.waste != null)
            {
                writer.Write((byte)deal.waste.Count);
                foreach (var card in deal.waste) WriteCardInstance(writer, card);
            }
            else writer.Write((byte)0);

            // 4. Foundations (List<List<CardModel>>)
            if (deal.foundations != null)
            {
                writer.Write((byte)deal.foundations.Count);
                foreach (var pile in deal.foundations)
                {
                    if (pile != null)
                    {
                        writer.Write((byte)pile.Count);
                        foreach (var cardModel in pile) WriteCardModel(writer, cardModel);
                    }
                    else writer.Write((byte)0);
                }
            }
            else writer.Write((byte)0);

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

        if (string.IsNullOrEmpty(data))
        {
            FillFoundations(deal); // Подстраховка для старта
            return deal;
        }

        try
        {
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
                var tempStock = new List<CardInstance>();
                for (int i = 0; i < stockCount; i++) tempStock.Add(ReadCardInstance(reader));
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
        }
        catch (Exception e)
        {
            Debug.LogError($"[DealSerializer] Десериализация не удалась: {e.Message}");
        }

        // [ВАЖНО] Восстанавливаем пустые фундаменты (как это делал старый UnpackLegacyDeal)
        // Иначе игра может сломаться при попытке положить карту в базу
        if (deal.foundations.Count == 0)
        {
            FillFoundations(deal);
        }

        return deal;
    }

    private static void FillFoundations(Deal deal)
    {
        for (int i = 0; i < 8; i++) deal.foundations.Add(new List<CardModel>());
    }

    // --- Хелперы упаковки байтов ---

    private static void WriteCardInstance(BinaryWriter w, CardInstance c)
    {
        byte id = GetCardID(c.Card);
        if (c.FaceUp) id |= 64;
        w.Write(id);
    }

    private static void WriteCardModel(BinaryWriter w, CardModel c)
    {
        w.Write(GetCardID(c));
    }

    private static CardInstance ReadCardInstance(BinaryReader r)
    {
        byte b = r.ReadByte();
        bool faceUp = (b & 64) != 0;
        byte id = (byte)(b & 63);
        return new CardInstance(GetCardFromID(id), faceUp);
    }

    private static CardModel ReadCardModel(BinaryReader r)
    {
        byte id = r.ReadByte();
        return GetCardFromID(id);
    }

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