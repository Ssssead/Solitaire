using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class StatsSerializer
{
    public static string Serialize(GameStatistics stats)
    {
        if (stats == null || stats.entries == null) return "";

        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // Сохраняем количество записей (ushort, так как их вряд ли будет больше 65000)
            writer.Write((ushort)stats.entries.Count);

            foreach (var entry in stats.entries)
            {
                writer.Write(entry.key ?? ""); // Защита от null
                WriteStatData(writer, entry.data);
            }

            return Convert.ToBase64String(ms.ToArray());
        }
    }

    public static void Deserialize(string data, out GameStatistics stats)
    {
        stats = new GameStatistics();
        stats.entries = new List<StatEntry>();

        if (string.IsNullOrEmpty(data)) return;

        try
        {
            byte[] bytes = Convert.FromBase64String(data);
            using (var ms = new MemoryStream(bytes))
            using (var reader = new BinaryReader(ms))
            {
                ushort count = reader.ReadUInt16();
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    StatData statData = ReadStatData(reader);

                    stats.entries.Add(new StatEntry { key = key, data = statData });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[StatsSerializer] Ошибка десериализации: {e.Message}. Возможно, формат сохранений изменился.");
        }
    }

    private static void WriteStatData(BinaryWriter w, StatData d)
    {
        if (d == null) d = new StatData(); // Перестраховка

        // Базовая статистика
        w.Write(d.gamesStarted);
        w.Write(d.gamesWon);
        w.Write(d.totalTime);
        w.Write(d.totalMoves);
        w.Write(d.bestScore);
        w.Write(d.bestTime);
        w.Write(d.fewestMoves);
        w.Write(d.currentStreak);
        w.Write(d.bestStreak);

        // Глобальная статистика
        w.Write(d.totalCardsMoved);
        w.Write(d.totalXP);
        w.Write(d.questsCompleted);
        w.Write(d.questStreak);

        // Система уровней
        w.Write(d.currentLevel);
        w.Write(d.currentXP);
        w.Write(d.xpForNextLevel);

        // История (ограничиваем до 255 элементов на всякий случай, хотя у тебя стоит лимит 10)
        if (d.history == null || d.history.Count == 0)
        {
            w.Write((byte)0);
        }
        else
        {
            w.Write((byte)Mathf.Min(d.history.Count, 255));
            foreach (var h in d.history)
            {
                w.Write(h.won);
                w.Write(h.score);
                w.Write(h.time);
                w.Write(h.moves);

                // Для строк обязательно используем fallback на "", так как BinaryWriter упадет при записи null
                w.Write(h.difficulty ?? "");
                w.Write(h.playedAt ?? "");
                w.Write(h.variant ?? "");
                w.Write(h.gameName ?? "");
            }
        }
    }

    private static StatData ReadStatData(BinaryReader r)
    {
        StatData d = new StatData();

        d.gamesStarted = r.ReadInt32();
        d.gamesWon = r.ReadInt32();
        d.totalTime = r.ReadSingle();
        d.totalMoves = r.ReadInt32();
        d.bestScore = r.ReadInt32();
        d.bestTime = r.ReadSingle();
        d.fewestMoves = r.ReadInt32();
        d.currentStreak = r.ReadInt32();
        d.bestStreak = r.ReadInt32();
        d.totalCardsMoved = r.ReadInt32();
        d.totalXP = r.ReadInt32();
        d.questsCompleted = r.ReadInt32();
        d.questStreak = r.ReadInt32();
        d.currentLevel = r.ReadInt32();
        d.currentXP = r.ReadInt32();
        d.xpForNextLevel = r.ReadInt32();

        int historyCount = r.ReadByte();
        d.history = new List<GameHistoryEntry>();

        for (int i = 0; i < historyCount; i++)
        {
            var h = new GameHistoryEntry
            {
                won = r.ReadBoolean(),
                score = r.ReadInt32(),
                time = r.ReadSingle(),
                moves = r.ReadInt32(),
                difficulty = r.ReadString(),
                playedAt = r.ReadString(),
                variant = r.ReadString(),
                gameName = r.ReadString()
            };
            d.history.Add(h);
        }

        return d;
    }
}