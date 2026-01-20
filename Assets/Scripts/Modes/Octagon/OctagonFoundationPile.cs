using UnityEngine;

public class OctagonFoundationPile : MonoBehaviour, ICardContainer
{
    public Transform Transform => transform;

    public bool CanAccept(CardController card)
    {
        if (card == null) return false;

        CardController topCard = GetTopCard();

        // 1. Пустая база -> только Туз
        if (topCard == null)
        {
            return card.cardModel.rank == 1; // Ace
        }

        // 2. Правила: Та же масть, ранг +1
        bool sameSuit = card.cardModel.suit == topCard.cardModel.suit;
        bool nextRank = card.cardModel.rank == topCard.cardModel.rank + 1;

        return sameSuit && nextRank;
    }

    // --- НОВЫЙ МЕТОД: Можно ли забрать карту? ---
    public bool CanTakeCard(CardController card)
    {
        // 1. Можно брать только верхнюю карту
        if (GetTopCard() != card) return false;

        int rank = card.cardModel.rank;

        // 2. Нельзя брать Тузов (Rank 1)
        if (rank == 1) return false;

        // 3. Нельзя брать Королей (Rank 13) - если стопка собрана
        if (rank == 13) return false;

        return true;
    }

    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        card.transform.SetParent(this.transform);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.SetAsLastSibling();
    }

    public CardController GetTopCard()
    {
        if (transform.childCount == 0) return null;
        return transform.GetChild(transform.childCount - 1).GetComponent<CardController>();
    }

    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
}