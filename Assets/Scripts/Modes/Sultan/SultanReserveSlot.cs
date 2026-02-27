using UnityEngine;

public class SultanReserveSlot : MonoBehaviour, ICardContainer
{
    public Transform Transform => transform;
    private SultanModeManager _mode;

    public void Initialize(SultanModeManager mode, RectTransform tf)
    {
        _mode = mode;
    }

    // Проверяем, может ли резерв принять карту
    public bool CanAccept(CardController card)
    {
        // Резервный слот принимает абсолютно любую карту, 
        // но ТОЛЬКО если в нем сейчас нет других карт (он пуст).
        return transform.childCount == 0;
    }

    // Логика принятия карты в слот
    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        // Делаем карту дочерним объектом слота
        card.transform.SetParent(transform);

        // Сбрасываем позицию точно по центру слота
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.localRotation = Quaternion.identity;
    }

    public void OnCardIncoming(CardController card)
    {
        // Сюда можно добавить визуальную подсветку слота при наведении карты
    }

    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        return Vector2.zero; // Карта всегда падает ровно в центр
    }

    public void Clear()
    {
        // Очистка слота при перезапуске игры
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }
}