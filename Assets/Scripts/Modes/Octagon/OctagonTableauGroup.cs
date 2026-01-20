using System.Collections.Generic;
using UnityEngine;

public class OctagonTableauGroup : MonoBehaviour
{
    [Header("Slots Configuration")]
    [Tooltip("Element 0 = Верхний слот (открытый), Element 4 = Нижний слот")]
    public List<OctagonTableauSlot> Slots;

    // --- НОВЫЙ МЕТОД (Исправление ошибки) ---
    public CardController GetTopCard()
    {
        // Ищем первую карту сверху вниз (от 0 к 4)
        for (int i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            // Используем метод слота для получения карты
            var card = slot.GetTopCard();

            if (card != null)
            {
                return card;
            }
        }
        return null; // Группа пуста
    }

    // --- ЛОГИКА ОБНОВЛЕНИЯ (FLIP) ---
    public void UpdateTopCardState()
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];

            if (slot.transform.childCount > 0)
            {
                var topCard = slot.GetTopCard();

                if (topCard != null)
                {
                    var data = topCard.GetComponent<CardData>();
                    if (data != null && !data.IsFaceUp())
                    {
                        // true = с анимацией
                        data.SetFaceUp(true, true);
                    }

                    var cg = topCard.GetComponent<CanvasGroup>();
                    if (cg != null) cg.blocksRaycasts = true;

                    return; // Нашли верхнюю - выходим
                }
            }
        }
    }

    public bool IsEmpty()
    {
        foreach (var slot in Slots)
        {
            if (slot.transform.childCount > 0) return false;
        }
        return true;
    }

    // Используется при раздаче
    public void AddCardToSlot(CardController card, int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Slots.Count)
        {
            Slots[slotIndex].AcceptCard(card);
        }
    }
}