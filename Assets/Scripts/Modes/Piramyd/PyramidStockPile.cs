using System.Collections.Generic;
using UnityEngine;

public class PyramidStockPile : MonoBehaviour
{
    private List<CardController> cards = new List<CardController>();

    public bool IsEmpty => cards.Count == 0;
    public int Count => cards.Count;

    public void Add(CardController c)
    {
        c.transform.SetParent(transform);
        c.transform.localPosition = Vector3.zero;
        c.transform.SetAsLastSibling();

        cards.Add(c);
        UpdateInteractability();
    }

    public void AddRange(IEnumerable<CardController> newCards)
    {
        foreach (var c in newCards)
        {
            c.transform.SetParent(transform);
            c.transform.localPosition = Vector3.zero;
            c.transform.SetAsLastSibling();
            cards.Add(c);
        }
        UpdateInteractability();
    }

    public void Remove(CardController c)
    {
        if (cards.Contains(c))
        {
            cards.Remove(c);
            UpdateInteractability();
        }
    }

    public CardController Draw()
    {
        if (IsEmpty) return null;
        var c = cards[cards.Count - 1];
        cards.RemoveAt(cards.Count - 1);
        UpdateInteractability();
        return c;
    }

    public CardController Peek()
    {
        if (IsEmpty) return null;
        return cards[cards.Count - 1];
    }

    public bool HasCard(CardController c) => cards.Contains(c);
    public void Clear() => cards.Clear();

    public void UpdateInteractability()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            bool isTop = (i == cards.Count - 1);
            CardController card = cards[i];

            if (card.canvasGroup != null)
            {
                // Логическая блокировка кликов остается (только верхняя)
                card.canvasGroup.interactable = isTop;
                card.canvasGroup.blocksRaycasts = isTop;
            }

            // ВИЗУАЛ: Все карты всегда белые (без затемнения)
            var cardData = card.GetComponent<CardData>();
            if (cardData != null && cardData.image != null)
            {
                cardData.image.color = Color.white;
            }
        }
    }
}