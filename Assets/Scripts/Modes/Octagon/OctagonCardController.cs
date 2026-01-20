using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OctagonCardController : CardController
{
    private OctagonModeManager _mode;
    private Transform _originalParent;
    private int _originalIndex;
    private bool _isAnimating = false;

    // --- НОВОЕ: Запоминаем источник для нашей системы Undo ---
    public ICardContainer SourceContainer { get; private set; }
    // --------------------------------------------------------

    private void Start()
    {
        _mode = FindObjectOfType<OctagonModeManager>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        this.OnDoubleClick += HandleDoubleClick;
    }

    private void OnDestroy() { this.OnDoubleClick -= HandleDoubleClick; }
    private void HandleDoubleClick(CardController card) { if (_mode != null) _mode.OnCardDoubleClicked(this); }

    public override void OnPointerClick(PointerEventData eventData)
    {
        var data = GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) return;
        base.OnPointerClick(eventData);
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (_isAnimating) { eventData.pointerDrag = null; return; }
        if (_mode != null && !_mode.IsInputAllowed) { eventData.pointerDrag = null; return; }

        var foundation = transform.parent.GetComponent<OctagonFoundationPile>();
        if (foundation != null && !foundation.CanTakeCard(this)) { eventData.pointerDrag = null; return; }
        if (transform.parent != null && transform.parent.GetComponent<OctagonStockPile>() != null) { eventData.pointerDrag = null; return; }
        var data = GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) { eventData.pointerDrag = null; return; }

        _originalParent = transform.parent;
        _originalIndex = transform.GetSiblingIndex();

        // --- ЗАПИСЬ ИСТОЧНИКА ---
        if (_originalParent != null)
            SourceContainer = _originalParent.GetComponent<ICardContainer>();
        // ------------------------

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        if (_mode != null && _mode.DragLayer != null) { transform.SetParent(_mode.DragLayer, true); transform.SetAsLastSibling(); }

        base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (_isAnimating) return;
        if (rectTransform != null && _mode != null && _mode.RootCanvas != null)
        {
            rectTransform.anchoredPosition += eventData.delta / _mode.RootCanvas.scaleFactor;
        }
        else
        {
            base.OnDrag(eventData);
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (_isAnimating) return;

        ICardContainer target = null;
        if (_mode != null)
        {
            target = _mode.FindNearestContainer(this, eventData.position, 0);
        }

        if (target != null)
        {
            AnimateMoveTo(target);
        }
        else
        {
            AnimateReturn();
        }
    }

    private void AnimateMoveTo(ICardContainer target)
    {
        _isAnimating = true;
        StartCoroutine(_mode.OctagonAnim.AnimateMoveCard(
            this,
            target.Transform,
            target.GetDropAnchoredPosition(this),
            0.2f,
            true,
            () =>
            {
                target.AcceptCard(this);
                _mode.OnCardDroppedToContainer(this, target);
                FinishAnimation();
            }
        ));
    }

    private void AnimateReturn()
    {
        _isAnimating = true;
        StartCoroutine(_mode.OctagonAnim.AnimateMoveCard(
            this,
            _originalParent,
            Vector3.zero,
            0.25f,
            true,
            () =>
            {
                if (_originalParent != null)
                {
                    transform.SetParent(_originalParent);
                    transform.SetSiblingIndex(_originalIndex);

                    if (_originalParent.GetComponent<OctagonWastePile>())
                        _originalParent.GetComponent<OctagonWastePile>().UpdateLayout();
                    else if (_originalParent.GetComponent<OctagonTableauSlot>())
                        _originalParent.GetComponent<OctagonTableauSlot>().UpdateLayout();
                    else
                        rectTransform.anchoredPosition = Vector2.zero;
                }
                FinishAnimation();
            }
        ));
    }

    private void FinishAnimation()
    {
        _isAnimating = false;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }
}