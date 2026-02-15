using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class YukonCardController : CardController, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private YukonModeManager _mode;
    private YukonAnimationService _animService;
    private Image _image;
    private CanvasGroup _localCanvasGroup;

    // --- ИСПРАВЛЕНИЕ: СИНХРОНИЗАЦИЯ С CARDDATA ---
    // Мы используем _internalFaceUp как кэш, но главным источником правды является CardData.
    // Это важно, потому что TableauPile может перевернуть карту через CardData, минуя этот контроллер.
    private bool _internalFaceUp;

    public bool IsFaceUp
    {
        get
        {
            var data = GetComponent<CardData>();
            if (data != null) return data.IsFaceUp();
            return _internalFaceUp;
        }
        private set
        {
            _internalFaceUp = value;
        }
    }
    // --------------------------------------------

    // Undo Data
    private Transform _originalParent;
    private int _originalIndex;
    private Vector3 _originalWorldPos;
    private List<Vector3> _originalLocalPositions = new List<Vector3>();

    public ICardContainer SourceContainer { get; private set; }
    public int OriginalSiblingIndex => _originalIndex;

    private List<CardController> _draggedSubStack = new List<CardController>();
    private Vector3 _dragOffset;
    private RectTransform _dragLayer;
    private Camera _uiCamera;

    private bool _isAnimating = false;

    private void Awake()
    {
        _mode = FindObjectOfType<YukonModeManager>();
        _animService = FindObjectOfType<YukonAnimationService>();

        rectTransform = GetComponent<RectTransform>();
        _image = GetComponent<Image>();
        _localCanvasGroup = GetComponent<CanvasGroup>();
        if (_localCanvasGroup == null) _localCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup = _localCanvasGroup;
    }

    public List<CardController> GetMovedCards()
    {
        List<CardController> list = new List<CardController>();
        list.Add(this);
        if (_draggedSubStack != null) list.AddRange(_draggedSubStack);
        return list;
    }

    public List<Vector3> GetSavedLocalPositions() => new List<Vector3>(_originalLocalPositions);

    public void Configure(bool faceUp)
    {
        SetFaceUp(faceUp, true);
    }

    private void UpdateSprite()
    {
        var data = GetComponent<CardData>();
        if (data) data.image.sprite = IsFaceUp ? (data.faceSprite ?? data.image.sprite) : data.backSprite;
    }

    public void SetFaceUp(bool value, bool instant = false)
    {
        // 1. Обновляем локальное состояние
        IsFaceUp = value;

        // 2. Обновляем CardData (ОБЯЗАТЕЛЬНО для синхронизации с базой)
        var data = GetComponent<CardData>();
        if (data != null)
        {
            // Используем метод CardData, чтобы он тоже знал о состоянии
            // Но аккуратно с анимацией, чтобы не двоилось
            data.SetFaceUp(value, false);
        }

        // 3. Визуализация и анимация
        if (instant || _animService == null)
        {
            UpdateSprite();
        }
        else
        {
            // Если состояние реально изменилось визуально - запускаем анимацию
            // (Проверка нужна, чтобы не крутить уже открытую карту)
            StartCoroutine(_animService.AnimateFlip(this, value, UpdateSprite));
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isAnimating) return;
        if (eventData.dragging) return;
        if (eventData.clickCount == 2 && IsFaceUp) _mode.OnCardDoubleClicked(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isAnimating) { eventData.pointerDrag = null; return; }
        if (_mode == null) _mode = FindObjectOfType<YukonModeManager>();

        // Теперь IsFaceUp вернет true, если CardData.faceUp == true, даже если мы сами этого не меняли
        if (!_mode.IsInputAllowed || !IsFaceUp) { eventData.pointerDrag = null; return; }

        _uiCamera = _mode.RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _mode.RootCanvas.worldCamera;
        _dragLayer = _mode.DragLayer;

        Vector3 mouseWorldPos;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform, eventData.position, _uiCamera, out mouseWorldPos);
        _dragOffset = rectTransform.position - mouseWorldPos;

        _originalParent = transform.parent;
        _originalIndex = transform.GetSiblingIndex();
        _originalWorldPos = rectTransform.position;

        if (_originalParent != null)
        {
            SourceContainer = _originalParent.GetComponent<ICardContainer>();
        }

        if (SourceContainer == null)
        {
            SourceContainer = GetComponentInParent<ICardContainer>();
        }

        _originalLocalPositions.Clear();
        _originalLocalPositions.Add(rectTransform.anchoredPosition3D);

        _draggedSubStack.Clear();
        for (int i = _originalIndex + 1; i < _originalParent.childCount; i++)
        {
            var child = _originalParent.GetChild(i).GetComponent<CardController>();
            if (child != null)
            {
                _draggedSubStack.Add(child);
                _originalLocalPositions.Add(child.rectTransform.anchoredPosition3D);
            }
        }

        foreach (var child in _draggedSubStack) child.transform.SetParent(this.transform, true);

        transform.SetParent(_mode.DragLayer, true);

        if (_localCanvasGroup != null) _localCanvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector3 currentMouseWorldPos;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_dragLayer, eventData.position, _uiCamera, out currentMouseWorldPos))
        {
            rectTransform.position = currentMouseWorldPos + _dragOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_localCanvasGroup != null) _localCanvasGroup.blocksRaycasts = true;

        ICardContainer target = _mode.FindNearestContainer(this, _originalParent);

        if (target != null)
        {
            Vector3 targetPos = Vector3.zero;
            if (target is YukonTableauPile tab) targetPos = tab.GetNextCardWorldPosition();
            else if (target is FoundationPile found) targetPos = found.transform.position;

            AnimateToTarget(target, targetPos);
        }
        else
        {
            AnimateReturn();
        }
    }

    public void SetSourceForAutoMove(ICardContainer source)
    {
        SourceContainer = source;
        _originalIndex = transform.GetSiblingIndex();
        _originalLocalPositions.Clear();
        _originalLocalPositions.Add(rectTransform.anchoredPosition3D);
        _draggedSubStack.Clear();
    }

    public void AnimateToTarget(ICardContainer target, Vector3 worldPos)
    {
        _isAnimating = true;
        StartCoroutine(_animService.AnimateCard(transform, worldPos, () =>
        {
            target.AcceptCard(this);
            _mode.OnCardDroppedToContainer(this, target);
            _isAnimating = false;
        }));
    }

    private void AnimateReturn()
    {
        _isAnimating = true;
        StartCoroutine(_animService.AnimateCard(transform, _originalWorldPos, () =>
        {
            RestoreHierarchy();
            _isAnimating = false;
        }));
    }

    private void RestoreHierarchy()
    {
        transform.SetParent(_originalParent, true);
        transform.SetSiblingIndex(_originalIndex);
        foreach (var child in _draggedSubStack) child.transform.SetParent(_originalParent, true);

        var pile = _originalParent.GetComponent<TableauPile>();
        if (pile) pile.StartLayoutAnimationPublic();
        else
        {
            var yPile = _originalParent.GetComponent<YukonTableauPile>();
            if (yPile) yPile.ForceRecalculateLayout();
        }

        _draggedSubStack.Clear();
        rectTransform.localScale = Vector3.one;
    }
}