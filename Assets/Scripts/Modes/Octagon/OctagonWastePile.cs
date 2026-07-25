using UnityEngine;

public class OctagonWastePile : MonoBehaviour, ICardContainer
{
    [Header("3D Stacking Effect")]
    [Tooltip("Горизонтальный отступ между картами (отрицательный - влево)")]
    public float offsetX = -2f;
    [Tooltip("Вертикальный отступ между картами")]
    public float offsetY = 0f;

    public Transform Transform => transform;
    public int CardCount => transform.childCount;

    public bool CanAccept(CardController card) => false;

    public void AcceptCard(CardController card)
    {
        AddCard(card);
    }

    // --- ОБНОВЛЕНО: Метод теперь учитывает кумулятивный отступ ---
    public void AddCard(CardController card)
    {
        if (card == null) return;

        card.transform.SetParent(transform, true);
        card.transform.SetAsLastSibling();

        int cardIndex = transform.childCount - 1;
        if (cardIndex < 0) cardIndex = 0;

        // Устанавливаем смещенную локальную позицию
        card.transform.localPosition = new Vector3(cardIndex * offsetX, cardIndex * offsetY, 0f);
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;

        var data = card.GetComponent<CardData>();
        if (data != null) data.SetFaceUp(true, false); // false, чтобы не дублировать звук переворота из анимации

        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = true;
    }

    // ==========================================
    // НОВОЕ: Возвращает верхнюю карту сброса
    // ==========================================
    public CardController GetTopCard()
    {
        if (transform.childCount == 0) return null;

        // В Unity верхняя карта визуально — это последний дочерний объект
        return transform.GetChild(transform.childCount - 1).GetComponent<CardController>();
    }

    public CardController PopBottomCard()
    {
        if (transform.childCount == 0) return null;

        // Перебираем элементы снизу вверх, пока не найдем карту с контроллером
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform bottomCardTr = transform.GetChild(i);
            CardController card = bottomCardTr.GetComponent<CardController>();

            if (card != null)
            {
                Canvas root = GetComponentInParent<Canvas>();
                if (root) card.transform.SetParent(root.transform, true);
                else card.transform.SetParent(null, true);

                UpdateLayout();
                return card;
            }
        }

        return null; // Если детей много, но среди них нет карт
    }

    public CardController DrawBottomCard() => PopBottomCard();

    // --- ОБНОВЛЕНО: Пересчет лейаута с учетом новых отступов ---
    public void UpdateLayout()
    {
        int count = transform.childCount;
        for (int i = 0; i < count; i++)
        {
            Transform child = transform.GetChild(i);
            child.localPosition = new Vector3(i * offsetX, i * offsetY, 0f);
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
        }
    }

    public void OnCardIncoming(CardController card) { }

    // Позволяет внешним скриптам узнать, где должна приземлиться карта
    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        int count = transform.childCount;
        return new Vector2(count * offsetX, count * offsetY);
    }
}