using UnityEngine;
using System.Collections.Generic;

public class YukonScoreManager : ScoreManager
{
    [Header("Scoring Rules")]
    [Tooltip("Очки за перенос карты в Дом")]
    public int foundationReward = 10;

    [Tooltip("Очки за открытие закрытой карты")]
    public int revealReward = 5;

    [Tooltip("Штраф за возврат карты из Дома на стол")]
    public int foundationPenalty = -15;

    // Стек для точного отката очков при отмене хода (Undo)
    private Stack<int> scoreHistory = new Stack<int>();

    // Накопитель очков за один текущий ход
    private int currentMoveDelta = 0;

    public override void ResetScore()
    {
        CurrentScore = 0;
        scoreHistory.Clear();
        currentMoveDelta = 0;
    }

    // 1. Вызывается ПЕРЕД началом расчета очков за ход
    public void BeginMove()
    {
        currentMoveDelta = 0;
    }

    // 2. Добавляет или отнимает очки, запоминая разницу
    public void AddReward(int amount)
    {
        int oldScore = CurrentScore;
        CurrentScore += amount;

        // Счет не может быть меньше нуля
        CurrentScore = Mathf.Max(0, CurrentScore);

        // Запоминаем реальное изменение (даже если уперлись в ноль)
        currentMoveDelta += (CurrentScore - oldScore);
    }

    // 3. Сохраняет результат хода в историю (для кнопки Undo)
    public void CommitMove()
    {
        scoreHistory.Push(currentMoveDelta);
    }

    // Откат последнего хода
    public override void OnUndo()
    {
        if (scoreHistory.Count > 0)
        {
            int delta = scoreHistory.Pop();
            CurrentScore -= delta;
            CurrentScore = Mathf.Max(0, CurrentScore);
        }
    }
}