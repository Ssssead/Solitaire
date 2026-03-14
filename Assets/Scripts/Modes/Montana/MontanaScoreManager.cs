using System.Collections.Generic;
using UnityEngine;

public class MontanaScoreManager : MonoBehaviour
{
    public int CurrentScore { get; private set; } = 0;
    private Stack<int> scoreHistory = new Stack<int>();

    public void ResetScore()
    {
        CurrentScore = 0;
        scoreHistory.Clear();
    }

    public void OnCardMove(ICardContainer source, ICardContainer target)
    {
        int points = 10; // 10 очков за любой успешный ход в Ковре
        CurrentScore += points;
        scoreHistory.Push(points);
    }

    public void OnUndo()
    {
        if (scoreHistory.Count > 0)
        {
            CurrentScore -= scoreHistory.Pop();
        }
    }
}