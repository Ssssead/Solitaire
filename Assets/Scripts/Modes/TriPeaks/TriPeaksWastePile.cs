using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TriPeaksWastePile : MonoBehaviour, ICardContainer
{
    private List<CardController> _cards = new List<CardController>();

    [Header("Visual Settings")]
    public float Gap = 1f; // Отступ вправо для каждой следующей карты

    public CardController TopCard => _cards.Count > 0 ? _cards.Last() : null;

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

    // --- Логика позиционирования (НОВОЕ) ---
    public Vector3 GetLocalPositionForIndex(int index)
    {
        return new Vector3(index * Gap, 0, 0);
    }

    public Vector3 GetTargetLocalPositionForNextCard()
    {
        return GetLocalPositionForIndex(_cards.Count);
    }

    public void UpdateVisuals()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i] != null)
            {
                _cards[i].transform.localPosition = GetLocalPositionForIndex(i);
                _cards[i].transform.SetSiblingIndex(i);
            }
        }
    }

    // --- ICardContainer Implementation ---
    public Transform Transform => this.transform;
    public bool CanAccept(CardController card) => true;
    public void OnCardIncoming(CardController card) { }

    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        int index = _cards.Contains(card) ? _cards.IndexOf(card) : _cards.Count;
        return new Vector2(index * Gap, 0);
    }

    public void AcceptCard(CardController card)
    {
        AddCard(card);
        UpdateVisuals();
    }
}