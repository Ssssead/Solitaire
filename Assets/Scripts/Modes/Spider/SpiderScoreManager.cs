using UnityEngine;

public class SpiderScoreManager : MonoBehaviour
{
    private const int START_SCORE = 500;
    private const int ROW_BONUS = 100;
    private const int ACTION_PENALTY = 1;

    public int CurrentScore { get; private set; }

    public void ResetScore()
    {
        CurrentScore = START_SCORE;
    }

    public void ApplyPenalty()
    {
        CurrentScore -= ACTION_PENALTY;
        // В Пауке счет может уходить в минус
    }

    public void AddRowBonus()
    {
        CurrentScore += ROW_BONUS;
    }
    public void RemoveRowBonus()
    {
        CurrentScore -= 100; // Или константа ROW_BONUS
    }
}