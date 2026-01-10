using UnityEngine;
using System.Reflection;

// Убираем ", ICardContainer", так как наследование от TableauPile уже дает это
public class FreeCellTableauPile : TableauPile
{
    private void Start()
    {
        // Внедряем зависимости в базовый класс, чтобы избежать NullReference
        var animService = FindObjectOfType<AnimationService>();
        var type = typeof(TableauPile);

        var fieldAnim = type.GetField("animationService", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldAnim != null && animService != null) fieldAnim.SetValue(this, animService);

        if (GetComponent<CanvasGroup>() == null) gameObject.AddComponent<CanvasGroup>();

        // ВАЖНО: Разблокируем лейаут, иначе карты могут зависнуть
        var fieldLocked = type.GetField("isLayoutLocked", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldLocked != null) fieldLocked.SetValue(this, false);
    }

    // ТЕПЕРЬ ИСПОЛЬЗУЕМ OVERRIDE (работает, так как в TableauPile добавили virtual)
    public override bool CanAccept(CardController card)
    {
        // Отладка: покажет в консоли, доходит ли вообще вызов сюда
        // Debug.Log($"FreeCell Check: {card.name} -> {gameObject.name}");

        if (card == null) return false;

        // 1. Если стопка пуста - принимаем ЛЮБУЮ карту
        if (cards.Count == 0) return true;

        // 2. Иначе стандартные правила (другой цвет + ранг ниже)
        CardController topCard = cards[cards.Count - 1];

        bool isColorDifferent = IsRed(topCard) != IsRed(card);
        bool isRankCorrect = topCard.cardModel.rank == card.cardModel.rank + 1;

        return isColorDifferent && isRankCorrect;
    }

    private bool IsRed(CardController c)
    {
        return c.cardModel.suit == Suit.Diamonds || c.cardModel.suit == Suit.Hearts;
    }
}