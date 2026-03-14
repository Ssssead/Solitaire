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

        // --- ИСПРАВЛЕНИЕ: Блокируем клики по всей стопке Foundation ---
        // Поскольку в Пауке карты летят сюда сами, кликать по ним нельзя.
        // Отключение лучей у родителя сделает ВСЕ карты внутри некликабельными!
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.blocksRaycasts = false;
        cg.interactable = false;
        // ----------------------------------------------------------------
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
            }
        }
    }

    public void SetCompleted(Suit suit)
    {
        isFull = true;
        isReserved = false;
        Debug.Log($"Foundation completed: {suit}");
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