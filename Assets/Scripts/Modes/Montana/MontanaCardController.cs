using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MontanaCardController : CardController
{
    private MontanaModeManager _mode;
    private bool _isAnimating = false;
    private Vector3 _dragOffset;

    // Данные для отмены хода (Undo)
    public ICardContainer SourceContainer { get; private set; }
    public Transform OriginalParent { get; private set; }
    public Vector3 OriginalLocalPosition { get; private set; }
    public int OriginalSiblingIndex { get; private set; }

    public bool IsLockedCard { get; private set; } = false;

    private void Start()
    {
        _mode = FindObjectOfType<MontanaModeManager>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        // Подписываемся на двойной клик из базового класса CardController
        this.OnDoubleClick += HandleDoubleClick;
    }

    private void OnDestroy()
    {
        this.OnDoubleClick -= HandleDoubleClick;
    }

    public void SetLockedState(bool locked)
    {
        IsLockedCard = locked;
        var cardData = GetComponent<CardData>();
        if (cardData != null && cardData.image != null)
        {
            cardData.image.color = locked ? new Color(0.65f, 0.65f, 0.65f, 1f) : Color.white;
        }
    }

    // --- ИСПРАВЛЕНИЕ 1: Авто-ход теперь по двойному клику ---
    private void HandleDoubleClick(CardController card)
    {
        if (IsLockedCard) return;
        if (_mode != null && _mode.IsInputAllowed && !_isAnimating)
        {
            _mode.autoMoveService?.OnCardRightClicked(this);
        }
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (IsLockedCard) return;
        var data = GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) return;

        // Базовый класс считает клики. Если кликнуть дважды быстро - сработает OnDoubleClick
        base.OnPointerClick(eventData);
    }

    public void CaptureStateForUndo()
    {
        SourceContainer = GetComponentInParent<ICardContainer>();
        OriginalParent = transform.parent;
        OriginalLocalPosition = transform.localPosition;
        OriginalSiblingIndex = transform.GetSiblingIndex();
    }

    public void SetAnimating(bool state)
    {
        _isAnimating = state;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = !state;
    }

    public void PerformAutoMove(ICardContainer target)
    {
        AnimateMoveTo(target);
    }

    // ==========================================
    // СИСТЕМА DRAG & DROP И АНИМАЦИИ
    // ==========================================

    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (_isAnimating || IsLockedCard) { eventData.pointerDrag = null; return; }
        if (_mode != null && !_mode.IsInputAllowed) { eventData.pointerDrag = null; return; }

        CaptureStateForUndo();
        _mode.dragManager?.OnCardClicked(this);

        if (_mode != null && _mode.DragLayer != null)
        {
            transform.SetParent(_mode.DragLayer, true);
            transform.SetAsLastSibling();
        }

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector3 globalMousePos))
        {
            _dragOffset = rectTransform.position - globalMousePos;
        }
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (_isAnimating || eventData.pointerDrag != gameObject) return;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            _mode.DragLayer,
            eventData.position,
            eventData.pressEventCamera,
            out Vector3 globalMousePos))
        {
            rectTransform.position = globalMousePos + _dragOffset;
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (_isAnimating || eventData.pointerDrag != gameObject) return;

        ICardContainer targetContainer = null;
        if (_mode != null) targetContainer = _mode.FindNearestContainer(this, eventData.position, 0);

        if (targetContainer != null) AnimateMoveTo(targetContainer);
        else AnimateReturn();
    }

    // --- ИСПРАВЛЕНИЕ 2: Всегда летим в слое DragLayer ---
    private void AnimateMoveTo(ICardContainer target)
    {
        _isAnimating = true;

        // Освобождаем старый слот
        if (SourceContainer is MontanaSlot sourceSlot) sourceSlot.RemoveCard(this);

        // Переносим карту на верхний слой (DragLayer), чтобы она летела ПОВЕРХ всех карт
        if (_mode != null && _mode.DragLayer != null)
        {
            transform.SetParent(_mode.DragLayer, true);
            transform.SetAsLastSibling();
        }
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        // Летим к мировым координатам центра целевого слота
        StartCoroutine(MoveWorldRoutine(target.Transform.position, () =>
        {
            // Только после приземления слот забирает карту себе в "дети"
            target.AcceptCard(this);
            if (_mode != null) _mode.OnCardDroppedToContainer(this, target);
            FinishAnimation();
        }));
    }

    private void AnimateReturn()
    {
        _isAnimating = true;

        // Если карту отпустили мимо слота, она уже в DragLayer, пусть там и летит
        Vector3 targetWorldPos = OriginalParent != null ? OriginalParent.position : transform.position;

        StartCoroutine(MoveWorldRoutine(targetWorldPos, () =>
        {
            // Возвращаем старому родителю только в конце
            if (OriginalParent != null)
            {
                transform.SetParent(OriginalParent);
                transform.SetSiblingIndex(OriginalSiblingIndex);
                transform.localPosition = OriginalLocalPosition;
            }
            FinishAnimation();
        }));
    }

    // Абсолютно новая корутина, работающая в мировых координатах
    private IEnumerator MoveWorldRoutine(Vector3 targetWorldPos, System.Action onComplete)
    {
        Vector3 startPos = transform.position;
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Мягкое торможение (SmoothStep)
            float easedT = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPos, targetWorldPos, easedT);
            yield return null;
        }

        transform.position = targetWorldPos;
        onComplete?.Invoke();
    }

    private void FinishAnimation()
    {
        _isAnimating = false;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }
}