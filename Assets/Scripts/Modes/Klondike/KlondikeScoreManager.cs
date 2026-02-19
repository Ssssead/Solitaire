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

    /// <summary>
    /// Вызывайте этот метод из DeckManager, когда игрок кликает по пустой колоде
    /// и карты возвращаются из сброса (Waste) в колоду (Stock).
    /// </summary>
    public void OnDeckRecycled()
    {
        // --- НАСТРОЙКА ОЧКОВ ЗА ПЕРЕСДАЧУ ---
        // Стандартный Клондайк: -100 очков (если игра идет на очки).
        // Если вы хотите 0 (без штрафа), поставьте 0.
        int pointsDelta = 0;

        // 1. Применяем изменение
        int previousScore = CurrentScore;
        // Не уходим ниже нуля
        int newScore = Mathf.Max(0, CurrentScore + pointsDelta);
        CurrentScore = newScore;

        int actualChange = newScore - previousScore;

        // 2. ВАЖНО: Записываем это действие в историю как ОДИН ход.
        // Даже если change = 0, мы должны запушить это в стек, 
        // чтобы при нажатии Undo система знала, что был совершен ход "Пересдача".
        scoreHistory.Push(actualChange);

        Debug.Log($"[Score] Deck Recycled. Penalty: {pointsDelta}. Actual Change: {actualChange}");
    }

    public void OnCardMove(ICardContainer source, ICardContainer target)
    {
        int pointsDelta = 0;

        // --- 1. ЛОГИКА ОЧКОВ ---

        // Цель: FOUNDATION (Дом)
        if (target is FoundationPile)
        {
            if (source is TableauPile) pointsDelta = 10;
            else if (source is WastePile) pointsDelta = 10;
        }
        // Цель: TABLEAU (Игровое поле)
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
            pointsDelta = 0;
        }
        // Цель: STOCK (Колода)
        // ВАЖНО: Этот блок сработает только если вы возвращаете карты по одной (Undo).
        // Массовая пересдача должна идти через OnDeckRecycled.
        else if (target is StockPile)
        {
            pointsDelta = 0;
        }

        // --- 2. ПРИМЕНЕНИЕ ---

        int previousScore = CurrentScore;
        int newScore = Mathf.Max(0, CurrentScore + pointsDelta);
        CurrentScore = newScore;

        int actualChange = newScore - previousScore;

        // --- 3. ИСТОРИЯ ---
        scoreHistory.Push(actualChange);
    }

    public override void OnUndo()
    {
        // Откат последнего действия
        if (scoreHistory.Count > 0)
        {
            int changeToRevert = scoreHistory.Pop();
            // Возвращаем очки обратно (если отняли -100, то changeToRevert = -100, значит Score - (-100) = +100)
            CurrentScore = Mathf.Max(0, CurrentScore - changeToRevert);
        }
    }

    public override void OnCardMove(ICardContainer target)
    {
        OnCardMove(null, target);
    }
}