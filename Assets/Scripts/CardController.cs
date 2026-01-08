// CardController.cs [FINAL: Auto-Lift to DragLayer]
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardController : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Card Data")]
    public CardModel cardModel;
    public Canvas canvas;
    public ICardGameMode CardmodeManager;
    public DragManager dragManager;

    [Header("Settings")]
    [SerializeField] private bool isDraggable = true;
    [SerializeField] private float doubleClickTime = 0.3f;

    // Components
    [HideInInspector] public RectTransform rectTransform;
    [HideInInspector] public CanvasGroup canvasGroup;
    private CardData cardData;

    // Events
    public event Action<CardController> OnPickedUp;
    public event Action<CardController, ICardContainer> OnDroppedToContainer;
    public event Action<CardController> OnDroppedToBoard;
    public event Action<CardController> OnClicked;
    public event Action<CardController> OnDoubleClick;
    public event Action<CardController> OnRightClick;
    public event Action<CardController> OnLongPress;

    // State
    private bool isDragging = false;
    private bool isAnimating = false;
    private Vector2 dragOffset;
    private float lastClickTime = 0f;
    private bool isPressed = false;
    private float pressStartTime = 0f;
    private bool longPressTriggered = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        cardData = GetComponent<CardData>();

        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();

        // --- FIX: Initialize CardmodeManager ---
        if (CardmodeManager == null)
        {
            // Try to find the manager in the scene
            // Since KlondikeModeManager implements ICardGameMode, this will work.
            var manager = FindObjectOfType<KlondikeModeManager>();
            if (manager != null)
            {
                CardmodeManager = manager;
            }
        }
        // ---------------------------------------
    }

    #region Drag & Drop

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 1. Проверка блокировки ввода (Global)
        if (CardmodeManager != null && !CardmodeManager.IsInputAllowed) return;
        
        // 2. --- ИСПРАВЛЕНИЕ: Проверка, открыта ли карта ---
        // Получаем компонент данных карты
        var cardData = GetComponent<CardData>();
        // Если компонента нет (странно) или карта НЕ открыта (IsFaceUp == false) -> выходим
        if (cardData != null && !cardData.IsFaceUp())
        {
            return;
        }
        // --------------------------------------------------

        // 3. Проверка интерактивности UI
        if (canvasGroup != null && !canvasGroup.interactable) return;

        // --- Ваш старый код ---
        isDragging = true;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        // Теперь это событие сработает, ТОЛЬКО если карта открыта
        OnPickedUp?.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // --- ДОБАВЛЕНО ---
        if (CardmodeManager != null && !CardmodeManager.IsInputAllowed) return;
        // -----------------

        if (isDragging)
        {
            // ... ваш код перемещения ...
            if (canvas != null)
            {
                rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (CardmodeManager != null && !CardmodeManager.IsInputAllowed)
        {
            isDragging = false;
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
            // Возвращаем на место, если карта зависла
            transform.localPosition = Vector3.zero;
            return;
        }

        isDragging = false;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        // 1. Находим DragManager, если ссылка потерялась
        if (dragManager == null)
        {
            dragManager = FindObjectOfType<DragManager>();
        }

        if (dragManager != null)
        {
            // 2. ВОТ ЗДЕСЬ ДОЛЖЕН БЫТЬ ВЫЗОВ, КОТОРОГО У ВАС НЕТ
            // Мы спрашиваем у менеджера: "Над чем я сейчас вишу?"
            ICardContainer target = dragManager.FindNearestContainer(this, eventData.position, 0f);

            if (target != null)
            {
                // Если нашли контейнер - вызываем событие успешного сброса
                // DragManager подписан на это событие и обработает логику (OnCardDroppedToContainer)
                OnDroppedToContainer?.Invoke(this, target);
            }
            else
            {
                // Если контейнер не найден, проверяем, может мы бросили просто на фон стола?
                bool droppedOnBoard = dragManager.OnDropToBoard(this, eventData.position);

                if (!droppedOnBoard)
                {
                    // Если никуда не попали - вызываем событие возврата
                    // DragManager вернет карты назад (OnCardDroppedToBoardEvent -> ReturnDraggingStackToOrigin)
                    OnDroppedToBoard?.Invoke(this);
                }
            }
        }
        else
        {
            // Fallback, если менеджера нет
            transform.localPosition = Vector3.zero;
        }
    }

    // --- ДОБАВИТЬ ЭТО СВОЙСТВО ---
    /// <summary>
    /// Возвращает контейнер (стопку), в котором сейчас находится карта.
    /// Определяется через поиск компонента ICardContainer в родительском объекте.
    /// </summary>
    public ICardContainer CurrentContainer
    {
        get
        {
            if (transform.parent == null) return null;
            return transform.parent.GetComponent<ICardContainer>();
        }
    }
    // -----------------------------

    #endregion

    #region Movement & Animation

    public void ForceSnapToContainer(ICardContainer container)
    {
        if (container == null) return;
        if (isAnimating) return;

        StartCoroutine(SnapRoutine(container));
    }

    private IEnumerator SnapRoutine(ICardContainer container)
    {
        isAnimating = true;

        // --- КРИТИЧЕСКОЕ ИЗМЕНЕНИЕ: ПОДНИМАЕМ КАРТУ В DRAGLAYER ---
        // Это гарантирует, что карта всегда летит поверх всего, откуда бы она ни вылетала.
        // Мы пытаемся найти DragLayer через менеджер или Canvas.

        RectTransform layer = null;

        // Use CardmodeManager interface property
        if (CardmodeManager != null && CardmodeManager.DragLayer != null)
        {
            layer = CardmodeManager.DragLayer;
        }
        else if (canvas != null)
        {
            layer = canvas.transform as RectTransform;
        }

        if (layer != null && rectTransform.parent != layer)
        {
            rectTransform.SetParent(layer, true); // true = сохранить мировую позицию
            rectTransform.SetAsLastSibling();     // Рисовать поверх всего в слое
        }

        // Временно отключаем Raycast во время полета
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        // -----------------------------------------------------------

        Vector2 targetAnchored = container.GetDropAnchoredPosition(this);
        Transform targetParent = container.Transform;

        // Расчет целевой мировой позиции
        GameObject tempObj = new GameObject("TempTarget");
        tempObj.transform.SetParent(targetParent, false);
        RectTransform tempRect = tempObj.AddComponent<RectTransform>();
        tempRect.anchoredPosition = targetAnchored;
        Canvas.ForceUpdateCanvases();
        Vector3 targetWorldPos = tempRect.position;
        Destroy(tempObj);

        // Старт анимации
        Vector3 startPos = rectTransform.position;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            rectTransform.position = Vector3.Lerp(startPos, targetWorldPos, t);
            yield return null;
        }

        // 1. Финальная позиция
        rectTransform.position = targetWorldPos;

        // 2. Перенос родителя (с сохранением world position во избежание скачка)
        if (rectTransform.parent != targetParent)
        {
            rectTransform.SetParent(targetParent, true);
        }

        // 3. Логическое добавление
        container.AcceptCard(this);

        // 4. Сброс локального Z
        Vector3 lp = rectTransform.localPosition;
        lp.z = 0f;
        rectTransform.localPosition = lp;

        // 5. Восстановление Raycasts
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        isAnimating = false;
    }

    #endregion

    #region Click Logic & Safety

    public void OnPointerClick(PointerEventData eventData)
    {
        if (CardmodeManager != null && !CardmodeManager.IsInputAllowed) return;
        if (isDragging) isDragging = false;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnRightClick?.Invoke(this);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            float timeSinceLast = Time.time - lastClickTime;
            if (timeSinceLast < doubleClickTime)
            {
                OnDoubleClick?.Invoke(this);
                lastClickTime = 0f;
            }
            else
            {
                OnClicked?.Invoke(this);
                lastClickTime = Time.time;
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            isPressed = true;
            pressStartTime = Time.time;
            longPressTriggered = false;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        if (isDragging) isDragging = false;
    }

    private void Update()
    {
        if (isPressed && !longPressTriggered && !isDragging)
        {
            if (Time.time - pressStartTime > 0.6f)
            {
                longPressTriggered = true;
                OnLongPress?.Invoke(this);
            }
        }
    }

    #endregion
}