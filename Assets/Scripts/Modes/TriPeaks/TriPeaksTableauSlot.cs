using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TriPeaksTableauPile : MonoBehaviour, ICardContainer
{
    [Header("Configuration")]
    public List<TriPeaksTableauPile> BlockingSlots;

    [Header("State")]
    public CardController CurrentCard;
    public bool HasCard => CurrentCard != null;

    public void AddCard(CardController card)
    {
        CurrentCard = card;
        card.transform.SetParent(this.transform);
        // —брос позиции в 0 - теперь это безопасно, так как метод вызываетс€ ѕќ—Ћ≈ полета
        card.transform.localPosition = Vector3.zero;
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

    // --- Interface ---
    public Transform Transform => this.transform;
    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
    public void AcceptCard(CardController card) { AddCard(card); }
    public bool CanAccept(CardController card) => false;
}