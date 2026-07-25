using System.Collections.Generic;
using UnityEngine;

public class SultanFoundationPile : MonoBehaviour, ICardContainer
{
    public Transform Transform => transform;
    private SultanModeManager _mode;

    public void Initialize(SultanModeManager mode, RectTransform tf)
    {
        _mode = mode;
    }

    public CardController GetTopCard()
    {
        // Иерархия Unity — единственный надежный источник истины при работе с UndoManager.
        // Если карта была перемещена обратно через Undo, childCount изменится автоматически.
        int childCount = transform.childCount;
        if (childCount == 0) return null;

        return transform.GetChild(childCount - 1).GetComponent<CardController>();
    }

    public bool CanAccept(CardController card)
    {
        if (card == null) return false;

        CardController top = GetTopCard();
        if (top == null) return false; // Базовые карты расставляются генератором

        // 1. Проверяем масть (должна совпадать)
        if (card.cardModel.suit != top.cardModel.suit) return false;

        // 2. Расчет следующего ранга "по кругу" (13-Король -> 1-Туз)
        int expectedRank = (top.cardModel.rank % 13) + 1;

        return card.cardModel.rank == expectedRank;
    }

    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        card.transform.SetParent(transform);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.SetAsLastSibling();

        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = false;

        // Старый трекинг GameQuestTracker.Instance?.RecordCardToFoundation УДАЛЕН.
        // Теперь все безопасно считается в SultanModeManager.TrackSultanQuest!
    }

    // Метод OnTransformChildrenChanged тоже УДАЛЕН, 
    // так как откат квестов при Undo теперь работает централизованно.

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
        foreach (Transform child in transform) Destroy(child.gameObject);
    }
}