using UnityEngine;

public class TriPeaksScoreManager : ScoreManager
{
    private int _streak = 0;
    public int BasePoints = 100;
    public int StreakBonus = 50;
    public int CurrentStreak => _streak;
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
    public void RestoreScoreAndStreak(int pointsToSubtract, int streakToRestore)
    {
        // Вычитаем заработанные очки
        if (CurrentScore >= pointsToSubtract)
            AddScore(-pointsToSubtract);

        // Восстанавливаем стрик
        _streak = streakToRestore;
    }
    public void CancelStreak()
    {
        // Для Undo: уменьшаем стрик (примитивная логика)
        if (_streak > 0) _streak--;
    }
}