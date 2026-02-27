using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class SultanCardController : CardController
{
    private SultanModeManager _mode;
    private bool _isAnimating = false;

    // --- ДАННЫЕ ДЛЯ UNDO ---
    public ICardContainer SourceContainer { get; private set; }
    public Transform OriginalParent { get; private set; }
    public Vector3 OriginalLocalPosition { get; private set; }
    public int OriginalSiblingIndex { get; private set; }
    public bool WasFaceUp { get; private set; }

    private void Start()
    {
        _mode = FindObjectOfType<SultanModeManager>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        this.OnDoubleClick += HandleDoubleClick;
    }

    private void OnDestroy() { this.OnDoubleClick -= HandleDoubleClick; }

    private void HandleDoubleClick(CardController card)
    {
        if (_mode != null && _mode.IsInputAllowed)
            _mode.OnCardDoubleClicked(this);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        var data = GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) return;
        base.OnPointerClick(eventData);
    }

    // --- ФИКСАЦИЯ СОСТОЯНИЯ ДЛЯ UNDO ---
    public void CaptureStateForUndo()
    {
        SourceContainer = transform.parent?.GetComponent<ICardContainer>();
        OriginalParent = transform.parent;
        OriginalSiblingIndex = transform.GetSiblingIndex();
        OriginalLocalPosition = transform.localPosition;

        var data = GetComponent<CardData>();
        WasFaceUp = data != null && data.IsFaceUp();
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (_isAnimating || (_mode != null && !_mode.IsInputAllowed)) { eventData.pointerDrag = null; return; }

        var data = GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) { eventData.pointerDrag = null; return; }

        if (transform.parent != null && transform.parent.GetComponent<SultanCenterPile>() != null)
        {
            eventData.pointerDrag = null; return;
        }

        // Запоминаем состояние перед отрывом от стола
        CaptureStateForUndo();

        transform.SetParent(_mode.DragLayer, true);
        transform.SetAsLastSibling();

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (_isAnimating || rectTransform == null || canvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (_isAnimating) return;

        ICardContainer target = _mode.FindNearestContainer(this, eventData.position, 0f);

        if (target != null) AnimateMoveTo(target);
        else AnimateReturn();
    }

    private void AnimateMoveTo(ICardContainer target)
    {
        _isAnimating = true;
        StartCoroutine(MoveRoutine(target.Transform, target.GetDropAnchoredPosition(this), () =>
        {
            target.AcceptCard(this);
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

            _mode.OnCardDroppedToContainer(this, target);
            _isAnimating = false;
        }));
    }

    private void AnimateReturn()
    {
        _isAnimating = true;
        StartCoroutine(MoveRoutine(OriginalParent, OriginalLocalPosition, () =>
        {
            if (OriginalParent != null)
            {
                transform.SetParent(OriginalParent);
                transform.SetSiblingIndex(OriginalSiblingIndex);
            }
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
            _isAnimating = false;
        }, true)); // true означает, что летим по LocalPosition
    }

    private IEnumerator MoveRoutine(Transform targetParent, Vector3 targetPos, System.Action onComplete, bool isLocal = false)
    {
        if (targetParent != null) transform.SetParent(targetParent, true);

        Vector3 startPos = isLocal ? transform.localPosition : (Vector3)rectTransform.anchoredPosition;
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            Vector3 current = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            if (isLocal) transform.localPosition = current;
            else rectTransform.anchoredPosition = current;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isLocal) transform.localPosition = targetPos;
        else rectTransform.anchoredPosition = targetPos;

        onComplete?.Invoke();
    }

    public void SetAnimating(bool state)
    {
        _isAnimating = state;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = !state;
    }
}