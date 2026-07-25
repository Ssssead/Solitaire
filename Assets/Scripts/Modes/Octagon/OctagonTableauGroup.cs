using System.Collections.Generic;
using UnityEngine;

// Добавляем ICardContainer, чтобы SceneExitAnimator мог работать с фоном
public class OctagonTableauGroup : MonoBehaviour, ICardContainer
{
    [Header("Slots Configuration")]
    [Tooltip("Element 0 = Верхний слот (открытый), Element 4 = Нижний слот")]
    public List<OctagonTableauSlot> Slots;

    // --- ИНТЕРФЕЙС ICardContainer (Только для анимаций) ---
    public Transform Transform => transform;
    public bool CanAccept(CardController card) => false; // Прямо в группу кидать нельзя
    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
    public void AcceptCard(CardController card) { }
    // ------------------------------------------------------

    public CardController GetTopCard()
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            var card = slot.GetTopCard();
            if (card != null) return card;
        }
        return null;
    }

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
                        data.SetFaceUp(true, true);

                        // ---> ДОБАВЛЕНО: Засчитываем переворот закрытой карты <---
                        if (!GameSettings.IsTutorialMode)
                        {
                            GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, 1);
                        }
                        // --------------------------------------------------------
                    }

                    var cg = topCard.GetComponent<CanvasGroup>();
                    if (cg != null) cg.blocksRaycasts = true;
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

    public void AddCardToSlot(CardController card, int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Slots.Count)
        {
            Slots[slotIndex].AcceptCard(card);
        }
    }
}