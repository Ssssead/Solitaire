using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class TriPeaksStockPile : MonoBehaviour, ICardContainer
{
    private List<CardController> _cards = new List<CardController>();

    [Header("Visual Settings")]
    public float Gap = 5f; // Убедитесь в инспекторе, что тут НЕ 0 (например, 5 или 10)

    public bool IsEmpty => _cards.Count == 0;

    public void AddCard(CardController card)
    {
        if (!_cards.Contains(card))
        {
            _cards.Add(card);
        }
        card.transform.SetParent(transform);

        // Гарантируем, что карта ловит клики
        var cg = card.GetComponent<CanvasGroup>();
        if (cg)
        {
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }
        var img = card.GetComponent<Image>();
        if (img) img.raycastTarget = true;
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

    // Мгновенная расстановка карт (вызывается при Undo All или старте)
    public void UpdateVisuals()
    {
        int count = _cards.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            CardController card = _cards[i];
            if (card != null)
            {
                float targetX = (i - (count - 1)) * Gap;

                card.transform.localPosition = new Vector3(targetX, 0, 0);
                card.transform.localRotation = Quaternion.identity;
                card.transform.localScale = Vector3.one;

                // Убеждаемся, что порядок в иерархии совпадает с порядком в списке
                card.transform.SetSiblingIndex(i);
            }
        }
    }

    public Transform Transform => this.transform;
    public bool CanAccept(CardController card) => true;
    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
    public void AcceptCard(CardController card) { AddCard(card); UpdateVisuals(); }
}