using UnityEngine;
using System.Collections.Generic;

public class KlondikeScoreManager : ScoreManager
{
    private Stack<int> scoreHistory = new Stack<int>();

    public override void ResetScore()
    {
        base.ResetScore();
        scoreHistory.Clear();
    }

    public override void OnCardMove(ICardContainer target)
    {
        int pointsGained = 0;

        // Начисляем очки в зависимости от того, куда упала карта
        if (target is TableauPile) pointsGained = 5;
        else if (target is FoundationPile) pointsGained = 10;

        if (pointsGained > 0)
        {
            AddScore(pointsGained);
        }

        // Запоминаем для Undo
        scoreHistory.Push(pointsGained);
    }

    public override void OnUndo()
    {
        if (scoreHistory.Count > 0)
        {
            int pointsToSubtract = scoreHistory.Pop();
            // Вычитаем очки обратно
            CurrentScore = Mathf.Max(0, CurrentScore - pointsToSubtract);
        }
    }
}