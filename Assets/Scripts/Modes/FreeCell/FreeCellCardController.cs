using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class FreeCellCardController : CardController
{
    private FreeCellModeManager freeCellMode;

    private void Start()
    {
        freeCellMode = FindObjectOfType<FreeCellModeManager>();
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (CardmodeManager != null && !CardmodeManager.IsInputAllowed) { eventData.pointerDrag = null; return; }

        var cData = GetComponent<CardData>();
        if (cData != null && !cData.IsFaceUp()) return;

        if (transform.parent != null && transform.parent.GetComponent<FoundationPile>() != null) { eventData.pointerDrag = null; return; }

        if (!IsSubStackValid()) { eventData.pointerDrag = null; return; }

        // --- ИСПРАВЛЕНИЕ: Разрешаем поднять ЛЮБУЮ валидную стопку (даже больше лимита) ---
        if (freeCellMode != null)
        {
            freeCellMode.CurrentDragCount = CountCardsBelow();
            freeCellMode.IsGrabbing = true;
        }

        base.OnBeginDrag(eventData);

        if (freeCellMode != null) freeCellMode.IsGrabbing = false;
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        // Сбрасываем флаг перед обработкой отпускания
        if (freeCellMode != null) freeCellMode.JustFailedDueToLimit = false;

        base.OnEndDrag(eventData);

        // Если дроп сорвался ИМЕННО из-за лимита (флаг поднялся в CanAccept)
        if (freeCellMode != null && freeCellMode.JustFailedDueToLimit)
        {
            freeCellMode.ShakeMoveLimitUI();
            freeCellMode.JustFailedDueToLimit = false; // Очищаем
        }

        // --- ИСПРАВЛЕНИЕ: Чиним Z-Order в FreeCell ячейке (чтобы не пряталась под картами) ---
        if (transform.parent != null && transform.parent.GetComponent<FreeCellPile>() != null)
        {
            StartCoroutine(FlightZOrderFixRoutine());
        }
        else
        {
            Invoke(nameof(ForceEnableRaycast), 0.1f);
        }
    }
    private IEnumerator FlightZOrderFixRoutine()
    {
        var cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = false;

        Canvas tempCanvas = gameObject.GetComponent<Canvas>();
        bool addedCanvas = false;
        if (tempCanvas == null)
        {
            tempCanvas = gameObject.AddComponent<Canvas>();
            addedCanvas = true;
        }

        tempCanvas.overrideSorting = true;
        tempCanvas.sortingOrder = 100;

        // Ждем пока базовая система доставит карту в ячейку (0,0)
        float safeTimer = 0.25f;
        while (safeTimer > 0f && rectTransform.localPosition.sqrMagnitude > 1f)
        {
            safeTimer -= Time.deltaTime;
            yield return null;
        }

        // Финальное выравнивание и уборка
        rectTransform.anchoredPosition = Vector2.zero;

        if (addedCanvas) Destroy(tempCanvas);
        else tempCanvas.overrideSorting = false;

        if (cg != null) cg.blocksRaycasts = true;
    }

    private void ForceEnableRaycast()
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }

    private void OnTransformParentChanged()
    {
        if (freeCellMode != null && transform.parent != null && transform.parent.GetComponent<FoundationPile>() != null)
        {
            freeCellMode.CheckGameState();
        }
    }

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
        return transform.parent.childCount - transform.GetSiblingIndex();
    }

    private bool IsRed(CardModel model)
    {
        return model.suit == Suit.Diamonds || model.suit == Suit.Hearts;
    }
}