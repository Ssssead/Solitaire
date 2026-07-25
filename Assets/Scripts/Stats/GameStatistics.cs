using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class GameStatistics
{
    public List<StatEntry> entries = new List<StatEntry>();

    private Dictionary<string, StatData> lookup = new Dictionary<string, StatData>();

    public void BuildLookup()
    {
        lookup.Clear();
        foreach (var entry in entries)
            if (!lookup.ContainsKey(entry.key)) lookup.Add(entry.key, entry.data);
    }

    public StatData GetData(string key)
    {
        if (lookup.ContainsKey(key)) return lookup[key];
        var newData = new StatData();
        lookup.Add(key, newData);
        entries.Add(new StatEntry { key = key, data = newData });
        return newData;
    }

    public string GetFavoriteGame()
    {
        Dictionary<string, int> gameCounts = new Dictionary<string, int>();

        foreach (var entry in entries)
        {
            if (entry.key == "Global") continue;

            string[] parts = entry.key.Split('_');
            if (parts.Length > 0)
            {
                string gameName = parts[0];
                if (!gameCounts.ContainsKey(gameName)) gameCounts[gameName] = 0;
                gameCounts[gameName] += entry.data.gamesStarted;
            }
        }

        if (gameCounts.Count == 0) return "None";
        return gameCounts.OrderByDescending(x => x.Value).First().Key;
    }

    public void UpdateData(string key, bool won, float time, int moves, int score, string difficultyName, string gameName, string variantName)
    {
        GetData(key).Update(won, time, moves, score, difficultyName, gameName, variantName);
    }

    // --- МИГРАЦИЯ ДАННЫХ И НОРМАЛИЗАЦИЯ ---

    public static string NormalizeVariant(string gameName, string variant)
    {
        if (string.IsNullOrEmpty(variant)) return "Standard";
        string lowerVar = variant.ToLower();

        if (gameName == "Pyramid" || gameName == "TriPeaks")
        {
            if (lowerVar == "1" || lowerVar == "classic" || lowerVar == "1rounds") return "1Rounds";
            if (lowerVar == "2" || lowerVar == "2rounds") return "2Rounds";
            if (lowerVar == "3" || lowerVar == "3rounds") return "3Rounds";
        }
        else if (gameName == "MonteCarlo")
        {
            if (lowerVar == "1" || lowerVar == "8ways") return "8Ways";
            if (lowerVar == "4ways") return "4Ways";
        }
        else if (gameName == "Montana")
        {
            if (lowerVar == "classic" || lowerVar == "standard") return "Standard";
            if (lowerVar == "hard") return "Hard";
        }
        else if (gameName == "Sultan" || gameName == "Octagon" || gameName == "FreeCell")
        {
            if (lowerVar == "classic" || lowerVar == "standard") return "Standard";
        }
        return variant;
    }

    public bool MigrateOldKeys()
    {
        bool modified = false;
        List<StatEntry> toRemove = new List<StatEntry>();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.key == "Global" || entry.key.EndsWith("_Global")) continue;

            string[] parts = entry.key.Split('_');
            if (parts.Length == 3)
            {
                string gameName = parts[0];
                string difficulty = parts[1];
                string variant = parts[2];

                string newVariant = NormalizeVariant(gameName, variant);

                if (newVariant != variant)
                {
                    string newKey = $"{gameName}_{difficulty}_{newVariant}";

                    // Исправляем историю внутри данных
                    foreach (var hist in entry.data.history)
                    {
                        if (hist.variant == variant) hist.variant = newVariant;
                    }

                    // Ищем, нет ли уже новой записи
                    var existingNewEntry = entries.FirstOrDefault(e => e.key == newKey && e != entry);
                    if (existingNewEntry != null)
                    {
                        MergeData(existingNewEntry.data, entry.data);
                        toRemove.Add(entry);
                    }
                    else
                    {
                        entry.key = newKey; // Просто переименовываем
                    }
                    modified = true;
                }
            }
        }

        foreach (var rm in toRemove) entries.Remove(rm);
        return modified;
    }

    private void MergeData(StatData target, StatData source)
    {
        target.gamesStarted += source.gamesStarted;
        target.gamesWon += source.gamesWon;
        target.totalTime += source.totalTime;
        target.totalMoves += source.totalMoves;

        if (source.bestScore > target.bestScore) target.bestScore = source.bestScore;
        if (source.bestTime > 0 && (target.bestTime == 0 || source.bestTime < target.bestTime)) target.bestTime = source.bestTime;
        if (source.fewestMoves > 0 && (target.fewestMoves == 0 || source.fewestMoves < target.fewestMoves)) target.fewestMoves = source.fewestMoves;
        if (source.bestStreak > target.bestStreak) target.bestStreak = source.bestStreak;

        target.history.AddRange(source.history);
        // Сортируем историю по дате (примитивно) и оставляем 10 последних
        target.history = target.history.OrderByDescending(h => h.playedAt).Take(10).ToList();
    }
}

[Serializable]
public class StatEntry
{
    public string key;
    public StatData data;
}

[Serializable]
public class StatData
{
    public int gamesStarted;
    public int gamesWon;
    public float totalTime;
    public int totalMoves;
    public int bestScore = 0;
    public float bestTime = 0;
    public int fewestMoves = 0;
    public int currentStreak = 0;
    public int bestStreak = 0;

    public int totalCardsMoved = 0;
    public int totalXP = 0;
    public int questsCompleted = 0;
    public int questStreak = 0;
    public int questDayStreak = 0;

    public int currentLevel = 1;
    public int currentXP = 0;
    public int xpForNextLevel = 500;

    public List<GameHistoryEntry> history = new List<GameHistoryEntry>();

    public float WinRate => gamesStarted > 0 ? (float)gamesWon / gamesStarted * 100f : 0f;
    public float AvgTime => gamesWon > 0 ? totalTime / gamesWon : 0f;
    public float AvgMoves => gamesWon > 0 ? (float)totalMoves / gamesWon : 0f;

    public bool AddExperience(int amount, bool isGlobal)
    {
        currentXP += amount;
        bool leveledUp = false;

        while (currentXP >= xpForNextLevel)
        {
            currentXP -= xpForNextLevel;
            currentLevel++;
            leveledUp = true;
            CalculateNextLevelTarget(isGlobal);
        }
        return leveledUp;
    }

    private void CalculateNextLevelTarget(bool isGlobal)
    {
        // Базовое количество опыта для первого уровня
        int baseTarget = isGlobal ? 1000 : 500;

        // На сколько увеличивается требование с каждым уровнем (было 2000 и 500)
        int multiplier = isGlobal ? 1000 : 200;

        // Новая формула: База + (Уровень * Шаг)
        xpForNextLevel = baseTarget + (currentLevel * multiplier);
    }

    public void Update(bool won, float time, int moves, int score, string difficultyName, string gameName, string variantName)
    {
        GameHistoryEntry newEntry = new GameHistoryEntry
        {
            won = won,
            score = score,
            time = time,
            moves = moves,
            difficulty = difficultyName,
            playedAt = DateTime.Now.ToString(),
            gameName = gameName,
            variant = variantName
        };

        history.Add(newEntry);
        if (history.Count > 10) history.RemoveAt(0);

        totalTime += time;
        totalMoves += moves;

        if (won)
        {
            gamesWon++;
            if (score > bestScore) bestScore = score;
            if (bestTime == 0 || time < bestTime) bestTime = time;
            if (fewestMoves == 0 || moves < fewestMoves) fewestMoves = moves;
            currentStreak++;
            if (currentStreak > bestStreak) bestStreak = currentStreak;
        }
        else
        {
            currentStreak = 0;
        }
    }
}

[Serializable]
public class GameHistoryEntry
{
    public bool won;
    public int score;
    public float time;
    public int moves;
    public string difficulty;
    public string playedAt;
    public string variant;
    public string gameName;
}