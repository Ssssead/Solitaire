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

    // --- ЛЕВИТАЦИЯ ---
    private bool _isLevitating = false;
    private bool _isVisuallyLevitating = false; // Флаг, отвечающий за цвет (белая/серая)
    private Coroutine levitateRoutine;
    private Coroutine transitionRoutine;

    private void Start()
    {
        _mode = FindObjectOfType<MontanaModeManager>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        this.OnDoubleClick += HandleDoubleClick;
    }

    private void OnDestroy()
    {
        this.OnDoubleClick -= HandleDoubleClick;
    }

    // Блокировка карты (вызывается Менеджером)
    public void SetLockedState(bool locked)
    {
        IsLockedCard = locked;
        UpdateVisualState();
    }

    // Динамическое обновление цвета
    private void UpdateVisualState()
    {
        var cardData = GetComponent<CardData>();
        if (cardData != null && cardData.image != null)
        {
            // Карта полностью белая, если она физически парит в воздухе, ИЛИ если она не заблокирована.
            // Серой она становится только если лежит заблокированной на столе.
            cardData.image.color = (_isVisuallyLevitating || !IsLockedCard) ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }

    private void HandleDoubleClick(CardController card)
    {
        if (IsLockedCard) return;
        if (_mode != null && _mode.IsInputAllowed && !_isAnimating)
        {
            // --- ИСПРАВЛЕНИЕ: Передаем двойной клик менеджеру, а не напрямую сервису ---
            _mode.OnCardDoubleClicked(this);
        }
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (IsLockedCard) return;
        var data = GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) return;

        // ВАЖНО: Сначала всегда вызываем базовый метод! 
        // Именно внутри него CardController считывает первый клик и запускает таймер.
        base.OnPointerClick(eventData);

        // Затем отправляем наш одиночный клик в менеджер для мобилок
        if (eventData.clickCount == 1)
        {
            if (_mode != null && _mode.IsInputAllowed)
            {
                _mode.OnCardClicked(this);
            }
        }
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
        if (state) SetLevitating(false, 0f); // Отключаем левитацию при анимации авто-хода или отмены
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

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_PickUp");

        SetLevitating(false, 0f);
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

    private void AnimateMoveTo(ICardContainer target)
    {
        _isAnimating = true;

        if (SourceContainer is MontanaSlot sourceSlot) sourceSlot.RemoveCard(this);

        if (_mode != null && _mode.DragLayer != null)
        {
            transform.SetParent(_mode.DragLayer, true);
            transform.SetAsLastSibling();
        }
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        StartCoroutine(MoveWorldRoutine(target.Transform.position, () =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");

            target.AcceptCard(this);
            if (_mode != null) _mode.OnCardDroppedToContainer(this, target);
            FinishAnimation();
        }));
    }

    private void AnimateReturn()
    {
        _isAnimating = true;
        Vector3 targetWorldPos = OriginalParent != null ? OriginalParent.position : transform.position;

        StartCoroutine(MoveWorldRoutine(targetWorldPos, () =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Fail");

            if (OriginalParent != null)
            {
                transform.SetParent(OriginalParent);
                transform.SetSiblingIndex(OriginalSiblingIndex);
                transform.localPosition = OriginalLocalPosition;
            }
            FinishAnimation();
        }));
    }

    private IEnumerator MoveWorldRoutine(Vector3 targetWorldPos, System.Action onComplete)
    {
        Vector3 startPos = transform.position;
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
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

    // ==========================================
    // ВОЛНОВАЯ ЛЕВИТАЦИЯ
    // ==========================================

    public void SetLevitating(bool state, float delay = 0f)
    {
        if (_isLevitating == state) return;
        _isLevitating = state;

        if (transitionRoutine != null) StopCoroutine(transitionRoutine);

        if (state)
            transitionRoutine = StartCoroutine(StartLevitationRoutine(delay));
        else
            transitionRoutine = StartCoroutine(StopLevitationRoutine(delay));
    }

    private IEnumerator StartLevitationRoutine(float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);

        // <--- ДОБАВЛЕНО: Звук в момент взлета карты --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Foundation_Success");

        // Как только карта физически начинает подниматься - она снова становится ярко-белой
        _isVisuallyLevitating = true;
        UpdateVisualState();

        if (levitateRoutine != null) StopCoroutine(levitateRoutine);
        levitateRoutine = StartCoroutine(LevitateRoutine());
    }

    private IEnumerator StopLevitationRoutine(float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);

        // Как только карта опускается на место - она снова темнеет
        _isVisuallyLevitating = false;
        UpdateVisualState();

        if (levitateRoutine != null) StopCoroutine(levitateRoutine);
        levitateRoutine = null;

        Vector2 startPos = rectTransform.anchoredPosition;
        Quaternion startRot = transform.localRotation;
        Vector3 startScale = transform.localScale;

        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, Vector2.zero, t);
            transform.localRotation = Quaternion.Lerp(startRot, Quaternion.identity, t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.one, t);

            yield return null;
        }

        rectTransform.anchoredPosition = Vector2.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private IEnumerator LevitateRoutine()
    {
        // --- ИЗМЕНЕНО: Скромные значения, чтобы не перекрывать верхний ряд ---
        float liftHeight = 5f; // Подъем всего на 10 пикселей
        float swingAngle = 2.0f; // Мягкое вращение
        float swingSpeed = 3f;
        float bobAmount = 3f;    // Минимальное "дыхание" вверх-вниз
        float bobSpeed = 2.5f;

        float timeOffset = Mathf.Abs(GetInstanceID()) % 10f;
        float easeInDuration = 0.35f;
        float elapsedEase = 0f;

        while (true)
        {
            float t = Time.time + timeOffset;

            float currentMult = 1f;
            float scalePop = 0f;

            if (elapsedEase < easeInDuration)
            {
                elapsedEase += Time.deltaTime;
                float norm = Mathf.Clamp01(elapsedEase / easeInDuration);
                currentMult = norm * norm * (3f - 2f * norm);

                // --- ИЗМЕНЕНО: Эффект прыжка масштаба уменьшен до 5% (+0.05f) ---
                scalePop = Mathf.Sin(norm * Mathf.PI) * 0.05f;
            }

            float newY = (liftHeight + Mathf.Sin(t * bobSpeed) * bobAmount) * currentMult;
            rectTransform.anchoredPosition = new Vector2(0f, newY);

            float angle = Mathf.Cos(t * swingSpeed) * swingAngle * currentMult;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            transform.localScale = Vector3.one * (1f + scalePop);

            yield return null;
        }
    }
}