using System.Collections.Generic;
using UnityEngine;

public class SultanFoundationPile : MonoBehaviour, ICardContainer
{
    public Transform Transform => transform;
    private SultanModeManager _mode;
    private List<CardController> _cards = new List<CardController>();

    public void Initialize(SultanModeManager mode, RectTransform tf)
    {
        _mode = mode;
    }

    public CardController GetTopCard()
    {
        if (_cards.Count == 0) return null;
        return _cards[_cards.Count - 1];
    }

    public bool CanAccept(CardController card)
    {
        if (card == null) return false;

        CardController top = GetTopCard();
        if (top == null) return false; // Пустые дома не принимают карты, они заполняются генератором

        // 1. Проверяем масть (должна совпадать)
        if (card.cardModel.suit != top.cardModel.suit) return false;

        // 2. Расчет следующего ранга "по кругу" (Король 13 -> Туз 1)
        int expectedRank = (top.cardModel.rank % 13) + 1;

        return card.cardModel.rank == expectedRank;
    }

    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        _cards.Add(card);
        card.transform.SetParent(transform);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.SetAsLastSibling();

        // В Домах карты нельзя брать обратно (классическое правило Султана)
        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = false;
    }

    public bool IsComplete()
    {
        var top = GetTopCard();
        // Дом собран, если последняя карта — Дама (Ранг 12)
        return top != null && top.cardModel.rank == 12;
    }

    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;

    public void Clear()
    {
        _cards.Clear();
        foreach (Transform child in transform) Destroy(child.gameObject);
    }
}