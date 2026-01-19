using System.Collections.Generic;
using UnityEngine;

public class OctagonTableauGroup : MonoBehaviour
{
    [Header("Slots Configuration")]
    [Tooltip("Element 0 = Верхний слот (открытый), Element 4 = Нижний слот")]
    public List<OctagonTableauSlot> Slots;

    // --- ЛОГИКА ОБНОВЛЕНИЯ (FLIP) ---
    // Вызывается менеджером, когда карта ушла из группы
    public void UpdateTopCardState()
    {
        // Проходим по слотам сверху (0) вниз (4)
        for (int i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];

            // Если в слоте есть карты
            if (slot.transform.childCount > 0)
            {
                // Берем самую верхнюю карту этого слота
                var topCard = slot.GetTopCard();

                if (topCard != null)
                {
                    // 1. Открываем (Face Up)
                    var data = topCard.GetComponent<CardData>();
                    if (data != null && !data.IsFaceUp())
                    {
                        // false = без анимации (мгновенно), чтобы избежать багов визуала
                        data.SetFaceUp(true, false);
                    }

                    // 2. Включаем взаимодействие (Raycast)
                    var cg = topCard.GetComponent<CanvasGroup>();
                    if (cg != null) cg.blocksRaycasts = true;

                    // Мы нашли верхнюю карту группы -> прерываем цикл (остальные слоты ниже должны быть закрыты)
                    return;
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

    // --- ДЛЯ РАЗДАЧИ ---
    public void AddCardToSlot(CardController card, int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Slots.Count)
        {
            Slots[slotIndex].AcceptCard(card);
        }
    }
}