using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TriPeaksWastePile : MonoBehaviour, ICardContainer
{
    private List<CardController> _cards = new List<CardController>();

    public CardController TopCard => _cards.Count > 0 ? _cards.Last() : null;

    // --- Специфичные методы ---
    public void AddCard(CardController card)
    {
        _cards.Add(card);
        card.transform.SetParent(transform);
    }

    public void RemoveCard(CardController card)
    {
        _cards.Remove(card);
    }

    public void Clear()
    {
        foreach (var c in _cards) if (c) Destroy(c.gameObject);
        _cards.Clear();
    }

    // --- ICardContainer Implementation ---

    public Transform Transform => this.transform;

    public bool CanAccept(CardController card) => true;

    public void OnCardIncoming(CardController card) { }

    // ИСПРАВЛЕНО: Vector2 вместо Vector3
    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        return Vector2.zero;
    }

    public void AcceptCard(CardController card)
    {
        AddCard(card);
    }
}