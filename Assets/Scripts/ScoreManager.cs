using UnityEngine;

// Базовый класс. Висит на объекте менеджера игры.
public class ScoreManager : MonoBehaviour
{
    public int CurrentScore { get; protected set; } = 0;

    // Виртуальный метод - наследники могут изменить его поведение
    public virtual void ResetScore()
    {
        CurrentScore = 0;
    }

    // Общий метод для добавления/вычитания
    public void AddScore(int amount)
    {
        CurrentScore += amount;
        // Тут можно вызвать событие обновления UI, если нужно
    }

    // Методы, которые будут переопределены в конкретных пасьянсах

    /// <summary>
    /// Вызывается при любом перемещении карты
    /// </summary>
    public virtual void OnCardMove(ICardContainer target)
    {
        // В базовой версии ничего не делаем или просто +1 за ход
    }

    /// <summary>
    /// Вызывается при отмене хода
    /// </summary>
    public virtual void OnUndo()
    {
        // В разных играх Undo работает по-разному
        // Например, в Пауке нужно вернуть вычтенное очко (+1)
        // А в Косынке - вычесть полученные (+5/-10)
    }
}