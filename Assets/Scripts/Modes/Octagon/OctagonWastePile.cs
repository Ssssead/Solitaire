using UnityEngine;

public class OctagonWastePile : MonoBehaviour, ICardContainer
{
    public Transform Transform => transform;
    public int CardCount => transform.childCount;

    public bool CanAccept(CardController card) => false;

    public void AcceptCard(CardController card)
    {
        AddCard(card);
    }

    public void AddCard(CardController card)
    {
        card.transform.SetParent(transform);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.SetAsLastSibling();

        var data = card.GetComponent<CardData>();
        if (data != null) data.SetFaceUp(true, true);

        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = true;
    }

    // --- НОВЫЙ МЕТОД: Взять карту со дна (для Refill) ---
    public CardController DrawBottomCard()
    {
        if (transform.childCount == 0) return null;

        // Берем самую нижнюю карту (индекс 0)
        // Это та карта, которая при Recycle попала бы на верх Stock
        Transform bottomCardTr = transform.GetChild(0);
        CardController card = bottomCardTr.GetComponent<CardController>();

        if (card != null)
        {
            // Отцепляем от Waste
            card.transform.SetParent(null);
        }

        // Обновляем позиции оставшихся (хотя они и так в 0,0, но для порядка)
        UpdateLayout();

        return card;
    }

    public void UpdateLayout()
    {
        foreach (Transform child in transform)
        {
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
        }
    }

    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
}