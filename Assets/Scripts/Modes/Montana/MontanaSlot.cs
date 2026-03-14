using System.Collections.Generic;
using UnityEngine;

public class MontanaSlot : MonoBehaviour, ICardContainer
{
    public Transform Transform => transform;
    private MontanaModeManager _mode;

    public int Row { get; private set; }
    public int Col { get; private set; }

    private List<CardController> _cards = new List<CardController>();

    public void Initialize(MontanaModeManager mode, int row, int col)
    {
        _mode = mode;
        Row = row;
        Col = col;
    }

    public CardController GetTopCard() => _cards.Count > 0 ? _cards[0] : null;

    public bool CanAccept(CardController card)
    {
        if (card == null || _cards.Count > 0) return false;

        // В первый столбец можно класть только Тузов
        if (Col == 0) return card.cardModel.rank == 1;

        // Для остальных ячеек смотрим на карту слева
        var leftSlot = _mode.pileManager.GetSlot(Row, Col - 1);
        var leftCard = leftSlot.GetTopCard();

        // Нельзя класть после пустого места или после Короля (мёртвая зона)
        if (leftCard == null || leftCard.cardModel.rank == 13) return false;

        // Карта должна быть той же масти и на 1 ранг старше
        return (card.cardModel.suit == leftCard.cardModel.suit) &&
               (card.cardModel.rank == leftCard.cardModel.rank + 1);
    }

    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        _cards.Add(card);
        card.transform.SetParent(transform);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.localRotation = Quaternion.identity;

        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null) { cg.blocksRaycasts = true; cg.interactable = true; }
    }

    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;

    public void RemoveCard(CardController card) => _cards.Remove(card);

    public void Clear()
    {
        _cards.Clear();
        foreach (Transform child in transform) Destroy(child.gameObject);
    }
}