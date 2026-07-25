using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))] // Гарантируем наличие CanvasGroup
public class SpiderFoundationPile : MonoBehaviour, ICardContainer
{
    [Header("Spider References")]
    public SpiderModeManager spiderMode;

    [Header("Visuals")]
    public Image iconImage;

    private bool isFull = false;

    // --- ICardContainer Свойства ---
    public Transform Transform => transform;
    public bool IsFull => isFull;
    public bool isReserved = false;

    private void Start()
    {
        if (spiderMode == null)
        {
            spiderMode = FindObjectOfType<SpiderModeManager>();
        }

        // --- Блокируем клики по всей стопке Foundation ---
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    public void ResetFoundation()
    {
        isFull = false;
        isReserved = false;
    }

    private void OnTransformChildrenChanged()
    {
        // Сбрасываем ТОЛЬКО если игра официально откатывает ход назад (Undo)
        if (transform.childCount == 0 && spiderMode != null && spiderMode.undoManager != null && spiderMode.undoManager.IsUndoing)
        {
            if (isFull || isReserved)
            {
                isFull = false;
                isReserved = false;
                Debug.Log("[Foundation] Emptied via Undo. Resetting status.");

                if (spiderMode.ScoreManager != null)
                {
                    spiderMode.ScoreManager.RemoveRowBonus();
                }

                // Забираем прогресс 13 карт и 1 стопки
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, -13);
                GameQuestTracker.Instance?.SendEvent(QuestActionType.CompleteFoundationPile, -1);

                // ---> ИСПРАВЛЕНИЕ: ОТКАТ РАНГОВ ДЛЯ ЗАДАНИЙ <---
                // Забираем прогресс всех 13 рангов (Туз, Двойка... Король, Дама)
                for (int i = 1; i <= 13; i++)
                {
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -1, i.ToString());
                }
                // ------------------------------------------------
            }
        }
    }

    public void SetCompleted(Suit suit)
    {
        isFull = true;
        isReserved = false;
        Debug.Log($"Foundation completed: {suit}");

        // Отправляем 13 карт и 1 собранную стопку
        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 13);
        GameQuestTracker.Instance?.SendEvent(QuestActionType.CompleteFoundationPile, 1);

        // ---> ИСПРАВЛЕНИЕ: ВЫПОЛНЕНИЕ ЗАДАНИЙ НА РАНГИ <---
        // Так как в Пауке стопка собирается целиком, в ней гарантированно есть все 13 рангов.
        // Отправляем их в трекер через цикл:
        for (int i = 1; i <= 13; i++)
        {
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, i.ToString());
        }
        // ----------------------------------------------------
    }

    // --- ICardContainer Реализация ---

    public bool CanAccept(CardController card) => false;

    public void AcceptCard(CardController card)
    {
        card.transform.SetParent(transform);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.SetAsLastSibling();
    }

    public void OnCardIncoming(CardController card) { }

    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
}