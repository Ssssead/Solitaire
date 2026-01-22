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

    // --- НОВЫЙ МЕТОД: Взять карту со дна и отсоединить ---
    public CardController PopBottomCard()
    {
        if (transform.childCount == 0) return null;

        // Индекс 0 = Самая нижняя карта
        Transform bottomCardTr = transform.GetChild(0);
        CardController card = bottomCardTr.GetComponent<CardController>();

        if (card != null)
        {
            // Отсоединяем от Waste, чтобы индексы сдвинулись
            // Canvas/Root как временный родитель
            Canvas root = GetComponentInParent<Canvas>();
            if (root) card.transform.SetParent(root.transform);
            else card.transform.SetParent(null);
        }

        UpdateLayout(); // Обновляем оставшиеся
        return card;
    }

    // Совместимость со старым кодом
    public CardController DrawBottomCard() => PopBottomCard();

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