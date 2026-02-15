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

    public void OnCardMove(ICardContainer source, ICardContainer target)
    {
        int pointsDelta = 0;

        // --- 1. ЛОГИКА ОЧКОВ ---

        // Цель: FOUNDATION
        if (target is FoundationPile)
        {
            if (source is TableauPile) pointsDelta = 10;
            else if (source is WastePile) pointsDelta = 10;
        }
        // Цель: TABLEAU
        else if (target is TableauPile)
        {
            if (source is WastePile) pointsDelta = 5;
            else if (source is FoundationPile) pointsDelta = -15;
            // Tableau -> Tableau = 0
        }
        // Цель: WASTE (Сброс)
        else if (target is WastePile)
        {
            // Stock -> Waste = 0
            // Tableau -> Waste = 0 (если вдруг возможно)
            pointsDelta = 0;
        }
        // Цель: STOCK (Колода)
        else if (target is StockPile)
        {
            // Waste -> Stock (Recycle) = 0 (или -100 в строгих правилах, но вы просили 0)
            pointsDelta = 0;
        }

        // --- 2. ПРИМЕНЕНИЕ ---

        int previousScore = CurrentScore;

        // Начисляем (не уходим в минус)
        int newScore = Mathf.Max(0, CurrentScore + pointsDelta);
        CurrentScore = newScore;

        // Считаем реальное изменение для истории
        int actualChange = newScore - previousScore;

        // --- 3. ВАЖНО: ЗАПИСЫВАЕМ В ИСТОРИЮ (ДАЖЕ 0) ---
        // Это гарантирует, что при Undo Stock->Waste мы вычтем именно этот 0.
        scoreHistory.Push(actualChange);

        // Debug.Log($"[Score] {source?.GetType().Name} -> {target?.GetType().Name} | Delta: {pointsDelta} | History: {actualChange}");
    }

    public override void OnUndo()
    {
        if (scoreHistory.Count > 0)
        {
            int changeToRevert = scoreHistory.Pop();
            CurrentScore = Mathf.Max(0, CurrentScore - changeToRevert);
        }
    }

    public override void OnCardMove(ICardContainer target)
    {
        OnCardMove(null, target);
    }
}