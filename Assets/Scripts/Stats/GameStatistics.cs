using System;
using System.Collections.Generic;
using System.Linq; // Для LINQ
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

    // Метод для получения "Любимой игры" (где больше всего запусков)
    public string GetFavoriteGame()
    {
        // Ключи хранятся как "GameName_Difficulty_Variant"
        // Нам нужно сгруппировать по GameName
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

        // Сортируем и берем топ-1
        return gameCounts.OrderByDescending(x => x.Value).First().Key;
    }

    public void UpdateData(string key, bool won, float time, int moves, int score, string difficultyName, string gameName, string variantName)
    {
        // Передаем difficultyName дальше в StatData
        GetData(key).Update(won, time, moves, score, difficultyName, gameName, variantName);
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
    // --- Basic ---
    public int gamesStarted;
    public int gamesWon;
    public float totalTime;

    // --- Advanced ---
    public int totalMoves;
    public int bestScore = 0;
    public float bestTime = 0;
    public int fewestMoves = 0;
    public int currentStreak = 0;
    public int bestStreak = 0;

    // --- Global / Fun Stats ---
    public int totalCardsMoved = 0;
    public int totalXP = 0;
    public int questsCompleted = 0;
    public int questStreak = 0;

    // --- НОВЫЕ ПОЛЯ: СИСТЕМА УРОВНЕЙ ---
    public int currentLevel = 1;
    public int currentXP = 0;
    public int xpForNextLevel = 500; // Начальное значение для локального уровня

    // --- ИЗМЕНЕНИЕ: Было List<bool>, Стало List<GameHistoryEntry> ---
    public List<GameHistoryEntry> history = new List<GameHistoryEntry>();
    // ---------------------------------------------------------------

    public float WinRate => gamesStarted > 0 ? (float)gamesWon / gamesStarted * 100f : 0f;
    public float AvgTime => gamesWon > 0 ? totalTime / gamesWon : 0f;
    public float AvgMoves => gamesWon > 0 ? (float)totalMoves / gamesWon : 0f;

    // Метод для начисления опыта
    // Возвращает true, если произошел Level Up
    public bool AddExperience(int amount, bool isGlobal)
    {
        currentXP += amount;
        bool leveledUp = false;

        // Проверяем, хватит ли опыта на повышение уровня (цикл while, если дали оч много опыта)
        while (currentXP >= xpForNextLevel)
        {
            currentXP -= xpForNextLevel;
            currentLevel++;
            leveledUp = true;

            // Пересчитываем цель для следующего уровня по вашей формуле
            CalculateNextLevelTarget(isGlobal);
        }

        return leveledUp;
    }

    private void CalculateNextLevelTarget(bool isGlobal)
    {
        // Ваша формула:
        // Глобальный: Level * 2000
        // Локальный: Level * 500

        int multiplier = isGlobal ? 2000 : 500;

        // Формула: NextTarget = CurrentLevel * Multiplier
        // (Например, на 1 уровне нужно 500. На 2 уровне нужно 1000 и т.д.)
        xpForNextLevel = currentLevel * multiplier;
    }

    public void Update(bool won, float time, int moves, int score, string difficultyName, string gameName, string variantName)
    {
        // 1. Создаем запись для истории
        GameHistoryEntry newEntry = new GameHistoryEntry
        {
            won = won,
            score = score,
            time = time,
            moves = moves,
            difficulty = difficultyName,
            playedAt = DateTime.Now.ToString(), // Можно хранить дату
            gameName = gameName,
            variant = variantName
        };

        // 2. Добавляем в список
        history.Add(newEntry);
        if (history.Count > 10) history.RemoveAt(0); // Храним только последние 10

        // 3. Обновляем общую статистику
        if (won)
        {
            gamesWon++;
            totalTime += time;
            totalMoves += moves;
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

// Новый класс для хранения деталей одной игры
[Serializable]
public class GameHistoryEntry
{
    public bool won;
    public int score;
    public float time;
    public int moves;
    public string difficulty; // "Easy", "Hard" и т.д.
    public string playedAt;
    public string variant;
    public string gameName; // "Klondike", "Spider"
}
//public void AddXP(int amount) { totalXP += amount; }
  //  public void AddCardMoves(int amount) { totalCardsMoved += amount; }
 //   public void CompleteQuest() { questsCompleted++; questStreak++; }
//}