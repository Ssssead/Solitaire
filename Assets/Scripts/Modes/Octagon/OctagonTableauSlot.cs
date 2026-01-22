using UnityEngine;
using UnityEngine.EventSystems;

public class OctagonTableauSlot : MonoBehaviour, ICardContainer
{
    [Header("Setup")]
    public OctagonTableauGroup Group;
    public int SlotIndex;

    [Header("Visual")]
    [SerializeField] private float verticalFanOffset = 30f;

    public Transform Transform => transform;

    private void OnTransformChildrenChanged()
    {
        UpdateLayout();
    }

    public void UpdateLayout()
    {
        int count = transform.childCount;
        for (int i = 0; i < count; i++)
        {
            Transform child = transform.GetChild(i);
            child.localPosition = new Vector3(0, -i * verticalFanOffset, 0);
            child.localRotation = Quaternion.identity;
        }
    }

    public CardController GetTopCard()
    {
        if (transform.childCount == 0) return null;
        return transform.GetChild(transform.childCount - 1).GetComponent<CardController>();
    }

    public bool CanAccept(CardController card)
    {
        if (card == null) return false;

        CardController topCard = GetTopCard();

        // 1. В пустой слот класть нельзя (ваше правило)
        if (topCard == null) return false;

        // 2. Проверка: Верхняя карта должна быть открыта
        var topData = topCard.GetComponent<CardData>();
        if (topData != null && !topData.IsFaceUp()) return false;

        // 3. Правило: Только Ранг (7 на 8). Масть ЛЮБАЯ.
        // Это разрешит Drag 6H -> 7D
        bool correctRank = card.cardModel.rank == topCard.cardModel.rank - 1;

        return correctRank;
    }

    public void AcceptCard(CardController card)
    {
        if (card == null) return;
        card.transform.SetParent(transform);
        // Позиция обновится автоматически через OnTransformChildrenChanged
    }
    public void OnCardIncoming(CardController card) { }

    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        int count = transform.childCount;
        return new Vector2(0, -count * verticalFanOffset);
    }
}