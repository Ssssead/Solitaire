using System.Collections;
using UnityEngine;

public class OctagonAutoMoveService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OctagonModeManager modeManager;
    [SerializeField] private OctagonPileManager pileManager;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 1500f; // Скорость полета карты

    // Метод вызывается из ModeManager при двойном клике
    public bool TryAutoMove(CardController card)
    {
        if (card == null) return false;

        // Ищем подходящую базу (Foundation)
        foreach (var foundation in pileManager.FoundationPiles)
        {
            if (foundation.CanAccept(card))
            {
                StartCoroutine(PerformMoveRoutine(card, foundation));
                return true;
            }
        }

        return false;
    }

    private IEnumerator PerformMoveRoutine(CardController card, OctagonFoundationPile targetPile)
    {
        // ЗАПОМИНАЕМ ИСТОЧНИК ДО СМЕНЫ РОДИТЕЛЯ ДЛЯ КВЕСТОВ
        ICardContainer sourceContainer = card.GetComponentInParent<ICardContainer>();

        Transform oldParent = card.transform.parent;
        card.transform.SetParent(targetPile.transform);

        Vector2 startPos = card.rectTransform.anchoredPosition;
        Vector2 targetPos = Vector2.zero;

        float dist = Vector2.Distance(startPos, targetPos);
        float duration = dist / moveSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            card.rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Финализация
        card.rectTransform.anchoredPosition = targetPos;
        targetPile.AcceptCard(card); // ОБЯЗАТЕЛЬНО КЛАДЕМ КАРТУ ФОРМАЛЬНО

        // ---> ТРЕКИНГ КВЕСТОВ ПРИ АВТОМАТИЧЕСКОМ СБОРЕ (ДВОЙНОЙ КЛИК) <---
        if (!GameSettings.IsTutorialMode)
        {
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 1);
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());

            if (sourceContainer is OctagonWastePile)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, 1);
            }
            else if (sourceContainer is OctagonTableauSlot)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, 1); // <--- Раскрытие углов

                // Если авто-сбор забрал последнюю карту из угла, засчитываем очистку и первооткрывателя
                var slot = sourceContainer as OctagonTableauSlot;
                if (slot != null && slot.Group != null)
                {
                    slot.Group.UpdateTopCardState();
                    if (slot.Group.IsEmpty())
                    {
                        GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, 1);
                    }
                }
            }
        }
        // -----------------------------------------------------------------

        // Оповещаем менеджер о ходе для проверки победы
        modeManager.CheckGameState();
    }
}