using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TriPeaksStockPile : MonoBehaviour, ICardContainer
{
    private List<CardController> _cards = new List<CardController>();

    // --- ДОБАВЛЕНО: Свойство для проверки пустоты ---
    public bool IsEmpty => _cards.Count == 0;
    // -----------------------------------------------

    public void AddCard(CardController card)
    {
        _cards.Add(card);
        card.transform.SetParent(transform);
    }

    public void RemoveCard(CardController card)
    {
        _cards.Remove(card);
    }

    public CardController DrawCard()
    {
        if (_cards.Count == 0) return null;
        return _cards.Last();
    }

    public bool Contains(CardController card) => _cards.Contains(card);

    public void Clear()
    {
        foreach (var c in _cards) if (c) Destroy(c.gameObject);
        _cards.Clear();
    }

    public void UpdateVisuals()
    {
        // Логика визуализации стопки
    }

    // --- ICardContainer Implementation ---

    public Transform Transform => this.transform;

    public bool CanAccept(CardController card) => true;

    public void OnCardIncoming(CardController card) { }

    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        return Vector2.zero;
    }

    public void AcceptCard(CardController card)
    {
        AddCard(card);
    }
}