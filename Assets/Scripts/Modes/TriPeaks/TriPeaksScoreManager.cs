using UnityEngine;

public class TriPeaksScoreManager : ScoreManager
{
    private int _streak = 0;
    public int BasePoints = 100;
    public int StreakBonus = 50;

    public void AddStreakScore()
    {
        _streak++;
        int points = BasePoints + (_streak * StreakBonus);
        AddScore(points); // Используем метод базового класса
    }

    public void ResetStreak()
    {
        _streak = 0;
    }

    public void CancelStreak()
    {
        // Для Undo: уменьшаем стрик (примитивная логика)
        if (_streak > 0) _streak--;
    }
}