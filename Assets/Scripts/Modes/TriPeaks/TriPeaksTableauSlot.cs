using UnityEngine;
using System.Collections.Generic;

public class TriPeaksTableauPile : MonoBehaviour, ICardContainer
{
    [Header("Configuration")]
    // Список карт, которые блокируют текущую (лежат поверх неё)
    public List<TriPeaksTableauPile> BlockingSlots;

    [Header("State")]
    public CardController CurrentCard;

    public bool HasCard => CurrentCard != null;

    // --- Специфичные методы ---

    public void AddCard(CardController card)
    {
        CurrentCard = card;
        card.transform.SetParent(this.transform);
    }

    public void RemoveCard(CardController card)
    {
        if (CurrentCard == card) CurrentCard = null;
    }

    public void Clear()
    {
        if (CurrentCard) Destroy(CurrentCard.gameObject);
        CurrentCard = null;
    }

    public bool IsBlocked()
    {
        if (BlockingSlots == null) return false;
        foreach (var slot in BlockingSlots)
        {
            if (slot.HasCard) return true;
        }
        return false;
    }

    // --- Реализация ICardContainer ---

    // 1. Свойство Transform
    public Transform Transform => this.transform;

    // 2. Визуальная реакция на перетаскивание (не используется в кликах, но нужна интерфейсу)
    public void OnCardIncoming(CardController card)
    {
    }

    // 3. Позиция для примагничивания (Vector2)
    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        return Vector2.zero;
    }

    // 4. Логика принятия карты
    public void AcceptCard(CardController card)
    {
        AddCard(card);
    }

    // 5. Проверка возможности приема (всегда false для Tableau в TriPeaks, туда нельзя класть карты)
    public bool CanAccept(CardController card)
    {
        // В TriPeaks карты возвращаются в Tableau только через Undo
        // Для обычного геймплея игрок не может положить карту обратно в пирамиду
        return false;
    }
}