using UnityEngine;
using UnityEngine.EventSystems;

public class FreeCellCardController : CardController
{
    private FreeCellModeManager freeCellMode;

    private void Start()
    {
        freeCellMode = FindObjectOfType<FreeCellModeManager>();
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        // 1. Базовые проверки
        if (CardmodeManager != null && !CardmodeManager.IsInputAllowed)
        {
            eventData.pointerDrag = null;
            return;
        }

        var cData = GetComponent<CardData>();
        if (cData != null && !cData.IsFaceUp()) return;

        // --- НОВАЯ ПРОВЕРКА: ЗАПРЕТ БРАТЬ ИЗ FOUNDATION ---
        // Если карта лежит в стопке "Дом" (FoundationPile), мы сразу отменяем драг.
        if (transform.parent != null && transform.parent.GetComponent<FoundationPile>() != null)
        {
            eventData.pointerDrag = null;
            return;
        }
        // -------------------------------------------------

        // 2. ПРОВЕРКА ПОСЛЕДОВАТЕЛЬНОСТИ (для Tableau)
        if (!IsSubStackValid())
        {
            Debug.Log("FreeCell: Неверная последовательность.");
            eventData.pointerDrag = null;
            return;
        }

        // 3. ПРОВЕРКА ЛИМИТА (для Tableau)
        if (freeCellMode != null)
        {
            int draggingCount = CountCardsBelow();
            int limit = freeCellMode.GetMaxDragSequenceSize();

            if (draggingCount > limit)
            {
                Debug.Log($"FreeCell: Лимит превышен ({draggingCount} > {limit}).");
                eventData.pointerDrag = null;
                return;
            }
        }

        // 4. Запускаем базовую логику
        base.OnBeginDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);
        Invoke(nameof(ForceEnableRaycast), 0.1f);
    }

    private void ForceEnableRaycast()
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }

    // --- НОВЫЙ МЕТОД ДЛЯ ПРОВЕРКИ ПОБЕДЫ ---
    private void OnTransformParentChanged()
    {
        // Срабатывает в конце полета, когда карта становится ребенком слота
        if (freeCellMode != null && transform.parent != null)
        {
            // Если нас удочерил Foundation - проверяем победу
            if (transform.parent.GetComponent<FoundationPile>() != null)
            {
                freeCellMode.CheckGameState();
            }
        }
    }
    // ----------------------------------------

    // --- Вспомогательные методы ---
    private bool IsSubStackValid()
    {
        if (transform.parent == null) return true;
        if (transform.GetSiblingIndex() == transform.parent.childCount - 1) return true;

        int myIndex = transform.GetSiblingIndex();
        int totalChilds = transform.parent.childCount;

        for (int i = myIndex; i < totalChilds - 1; i++)
        {
            var current = transform.parent.GetChild(i).GetComponent<CardController>();
            var next = transform.parent.GetChild(i + 1).GetComponent<CardController>();

            if (current == null || next == null) continue;
            if (current.cardModel.rank != next.cardModel.rank + 1) return false;

            bool isCurrentRed = IsRed(current.cardModel);
            bool isNextRed = IsRed(next.cardModel);
            if (isCurrentRed == isNextRed) return false;
        }
        return true;
    }

    private int CountCardsBelow()
    {
        if (transform.parent == null) return 1;
        int myIndex = transform.GetSiblingIndex();
        int total = transform.parent.childCount;
        return total - myIndex;
    }

    private bool IsRed(CardModel model)
    {
        return model.suit == Suit.Diamonds || model.suit == Suit.Hearts;
    }
}