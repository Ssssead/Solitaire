using UnityEngine;
using UnityEngine.EventSystems;

public class OctagonTableauSlot : MonoBehaviour, ICardContainer
{
    [Header("Setup")]
    public OctagonTableauGroup Group;
    public int SlotIndex;

    [Header("Visual")]
    [SerializeField] private float verticalFanOffset = 0f; // Отступ между картами в слоте

    public Transform Transform => transform;

    // --- ЛОГИКА ОТОБРАЖЕНИЯ (FAN) ---
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
            // Сдвигаем каждую следующую карту вниз
            child.localPosition = new Vector3(0, -i * verticalFanOffset, 0);
            child.localRotation = Quaternion.identity;
        }
    }

    // --- ЛОГИКА ICardContainer ---

    public CardController GetTopCard()
    {
        if (transform.childCount == 0) return null;
        return transform.GetChild(transform.childCount - 1).GetComponent<CardController>();
    }

    public bool CanAccept(CardController card)
    {
        if (card == null) return false;

        CardController topCard = GetTopCard();

        // 1. Если слот пуст:
        // По вашим правилам пустые слоты должны заполняться ТОЛЬКО через Refill (из Stock).
        // Поэтому игроку класть в пустой слот запрещаем.
        if (topCard == null) return false;

        // 2. Правило рангов: Кладем меньшую на большую (7 на 8)
        // Масть любая (Sultan/Octagon rules) или чередование цветов? 
        // Обычно в Sultan масть должна совпадать, но вы писали "7 любой масти".
        // Оставим проверку только на Ранг.

        bool correctRank = card.cardModel.rank == topCard.cardModel.rank - 1;

        return correctRank;
    }

    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        // Просто меняем родителя на этот слот
        card.transform.SetParent(this.transform);

        // Позиция исправится автоматически в UpdateLayout (OnTransformChildrenChanged)

        // Если перенесли карту, нужно сообщить группе, чтобы она проверила состояние
        // (хотя для Refill это проверяется глобально в ModeManager)
    }

    public void OnCardIncoming(CardController card) { }

    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        // Подсказка для DragManager, куда визуально летит карта
        int count = transform.childCount;
        return new Vector2(0, -count * verticalFanOffset);
    }
}