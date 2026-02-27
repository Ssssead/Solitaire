using System.Collections.Generic;
using UnityEngine;

public class SultanScoreManager : MonoBehaviour
{
    public int CurrentScore { get; private set; } = 0;
    public int CurrentStreak { get; private set; } = 0; // Текущая серия

    // Стеки для корректного отката при отмене хода
    private Stack<int> scoreHistory = new Stack<int>();
    private Stack<int> streakHistory = new Stack<int>();

    [Header("Score Rules")]
    public int pointsForFoundation = 10;
    [Tooltip("Сколько дополнительных очков давать за каждую карту в серии")]
    public int streakBonus = 10;

    public int penaltyForRecycle = 50;
    public int penaltyForUndo = 5;

    public void ResetScore()
    {
        CurrentScore = 0;
        CurrentStreak = 0;
        scoreHistory.Clear();
        streakHistory.Clear();
    }

    public void OnCardMove(ICardContainer source, ICardContainer target)
    {
        int pointsEarned = 0;

        // Обязательно запоминаем текущую серию ДО ее изменения
        streakHistory.Push(CurrentStreak);

        if (target is SultanFoundationPile)
        {
            // Формула: База + (Серия * Бонус)
            // 1-я карта: 10 + (0 * 10) = 10
            // 2-я карта: 10 + (1 * 10) = 20
            // 5-я карта: 10 + (4 * 10) = 50 
            pointsEarned = pointsForFoundation + (CurrentStreak * streakBonus);
            CurrentStreak++;
        }
        else
        {
            // Перенос в резерв сбрасывает серию
            CurrentStreak = 0;
        }

        AddScore(pointsEarned);
        scoreHistory.Push(pointsEarned);
    }

    // Метод для ручного сброса серии (при клике по колоде)
    public void BreakStreak()
    {
        streakHistory.Push(CurrentStreak);
        scoreHistory.Push(0); // Очков не дали, но ход в истории записан
        CurrentStreak = 0;
    }

    public void OnUndo()
    {
        // 1. Возвращаем очки
        if (scoreHistory.Count > 0)
        {
            int lastScore = scoreHistory.Pop();
            AddScore(-lastScore);
        }

        // 2. Восстанавливаем серию!
        if (streakHistory.Count > 0)
        {
            CurrentStreak = streakHistory.Pop();
        }

        // 3. Штраф за саму отмену
        AddScore(-penaltyForUndo);
    }

    public void OnDeckRecycled()
    {
        BreakStreak();
        AddScore(-penaltyForRecycle);
    }

    private void AddScore(int amount)
    {
        CurrentScore += amount;
        if (CurrentScore < 0) CurrentScore = 0;
    }
}