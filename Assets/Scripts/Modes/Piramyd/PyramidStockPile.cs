using System.Collections.Generic;
using UnityEngine;

public class PyramidStockPile : MonoBehaviour
{
    private List<CardController> cards = new List<CardController>();

    [Header("Visual Settings")]
    [Tooltip("Смещение каждой следующей карты вправо (для эффекта толщины колоды)")]
    public float stackGap = 4f;

    public bool IsEmpty => cards.Count == 0;
    public int Count => cards.Count;

    public void Add(CardController c)
    {
        c.transform.SetParent(transform);
        c.transform.SetAsLastSibling();

        cards.Add(c);

        UpdateLayout(); // Пересчитываем позиции
        UpdateInteractability();
    }

    public void AddRange(IEnumerable<CardController> newCards)
    {
        foreach (var c in newCards)
        {
            c.transform.SetParent(transform);
            c.transform.SetAsLastSibling();
            cards.Add(c);
        }
        UpdateLayout(); // Пересчитываем позиции
        UpdateInteractability();
    }

    public void Remove(CardController c)
    {
        if (cards.Contains(c))
        {
            cards.Remove(c);
            UpdateLayout(); // Пересчитываем позиции
            UpdateInteractability();
        }
    }

    public CardController Draw()
    {
        if (IsEmpty) return null;
        var c = cards[cards.Count - 1];
        cards.RemoveAt(cards.Count - 1);

        UpdateLayout(); // Пересчитываем позиции
        UpdateInteractability();
        return c;
    }

    public CardController Peek()
    {
        if (IsEmpty) return null;
        return cards[cards.Count - 1];
    }

    public bool HasCard(CardController c) => cards.Contains(c);

    public void Clear()
    {
        cards.Clear();
    }

    // --- НОВОЕ: Пересчет позиций всех карт в стопке ---
    public void UpdateLayout()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
            {
                // Сдвигаем вправо: индекс умножаем на положительный gap
                float xOffset = i * stackGap;

                RectTransform rect = cards[i].GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(xOffset, 0f);
                }
                else
                {
                    cards[i].transform.localPosition = new Vector3(xOffset, 0f, 0f);
                }
            }
        }
    }

    public void UpdateInteractability()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            bool isTop = (i == cards.Count - 1);
            CardController card = cards[i];

            if (card.canvasGroup != null)
            {
                card.canvasGroup.interactable = isTop;
                card.canvasGroup.blocksRaycasts = isTop;
            }

            var cardData = card.GetComponent<CardData>();
            if (cardData != null && cardData.image != null)
            {
                cardData.image.color = Color.white;
            }

            // --- НОВОЕ: Тень только у самой нижней карты стока (индекс 0) ---
            var shadow = card.GetComponent<UnityEngine.UI.Shadow>();
            if (shadow != null) shadow.enabled = (i == 0);
        }
    }
    public Vector3 GetCardWorldPosition(CardController c)
    {
        int idx = cards.IndexOf(c);
        if (idx == -1) return transform.position;
        float xOffset = idx * stackGap;
        return transform.TransformPoint(new Vector3(xOffset, 0f, 0f));
    }
}