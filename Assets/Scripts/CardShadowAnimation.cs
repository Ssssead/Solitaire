using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class CardDragShadow : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    [Header("Shadow Settings")]
    public Sprite shadowSprite;

    [Range(0f, 1f)]
    public float shadowAlpha = 0.4f;

    [Header("Offsets")]
    public Vector2 restingOffset = new Vector2(0.1f, -0.1f);
    public Vector2 dragOffset = new Vector2(0.5f, -0.5f);

    [Header("Waste Settings")]
    public float wasteSlotGap = 35f; // Шаг между картами в сбросе
    public float arrivalThreshold = 50f; // Дистанция, когда считаем, что карта прилетела

    [Header("Animation")]
    public float animationSpeed = 20f;
    public float fadeSpeed = 5f;

    // Внутренние переменные
    private GameObject shadowObject;
    private RectTransform shadowRect;
    private CanvasGroup shadowCanvasGroup;

    private Vector2 currentOffset;
    private float currentAlpha;

    private RectTransform myRect;
    private float singleCardWidth;

    // Состояния
    private bool isDying = false;
    private bool isDragging = false;
    private bool isInWaste = false;

    private void Awake()
    {
        myRect = GetComponent<RectTransform>();
        singleCardWidth = myRect.rect.width;

        if (shadowSprite == null)
        {
            var img = GetComponent<Image>();
            if (img != null) shadowSprite = img.sprite;
        }

        currentOffset = restingOffset;
        currentAlpha = shadowAlpha;
    }

    // Методы интерфейсов нужны для корректной работы EventSystem
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) { }

    private void LateUpdate()
    {
        AnalyzeContext();

        // 1. ПРОВЕРКИ СОСТОЯНИЯ
        bool shouldDraw = false;
        bool isWideMode = false;

        // Проверяем, летит ли карта сейчас (анимация перемещения)
        // anchoredPosition стремится к (0,0) при посадке в слот
        bool isArriving = myRect.anchoredPosition.magnitude > arrivalThreshold;

        if (isInWaste)
        {
            // --- ЛОГИКА WASTE ---
            bool isSlot0Bottom = (transform.parent.name == "Slot_0" && IsBottomCardInStack());

            if (isSlot0Bottom)
            {
                // Мы - "Главный" (в Slot_0). Рисуем общую тень всегда.
                shouldDraw = true;
                isWideMode = true; // Пытаемся растянуться на соседей
            }
            else
            {
                // Мы в Slot_1 или Slot_2.
                // Если мы еще летим -> рисуем свою личную маленькую тень.
                // Если прилетели -> ничего не рисуем (нас покроет тень от Slot_0).
                if (isArriving)
                {
                    shouldDraw = true;
                    isWideMode = false; // Обычная одиночная тень
                }
                else
                {
                    shouldDraw = false;
                }
            }
        }
        else
        {
            // --- ЛОГИКА TABLEAU / DRAG ---
            // Рисуем тень, только если мы нижняя карта в стопке
            if (IsBottomCardInStack())
            {
                shouldDraw = true;
                isWideMode = true; // Растягиваемся вниз
            }
        }

        // 2. ЖИЗНЬ / СМЕРТЬ ТЕНИ
        if (!shouldDraw)
        {
            isDying = true;
        }
        else
        {
            isDying = false;
            currentAlpha = Mathf.MoveTowards(currentAlpha, shadowAlpha, Time.deltaTime * fadeSpeed * 2);
        }

        // Удаление полностью прозрачной тени
        if (isDying && currentAlpha <= 0.01f)
        {
            DestroyShadow();
            return;
        }

        // Создание объекта тени
        if (shadowObject == null)
        {
            if (isDying) return;
            CreateMonolithShadow();
        }

        // 3. СМЕЩЕНИЕ (OFFSET)
        // Если летим (Drag) или прибываем (Arriving в Waste) -> Большая тень
        // Иначе -> Маленькая тень
        bool useDragOffset = (isDragging || (isInWaste && isArriving)) && !isDying;

        Vector2 targetOffset = useDragOffset ? dragOffset : restingOffset;
        currentOffset = Vector2.Lerp(currentOffset, targetOffset, Time.deltaTime * animationSpeed);

        // Анимация исчезновения
        if (isDying) currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, Time.deltaTime * fadeSpeed);

        // 4. ОТРИСОВКА
        UpdateShadowGeometry(isWideMode);
    }

    private void UpdateShadowGeometry(bool isWideMode)
    {
        // Привязка к родителю
        if (shadowRect.parent != transform.parent) shadowRect.SetParent(transform.parent, true);

        // Z-Index: Тень всегда первая
        if (shadowRect.GetSiblingIndex() != 0) shadowRect.SetAsFirstSibling();

        float finalWidth = singleCardWidth;
        float finalHeight = myRect.rect.height;

        // Настройки Pivot и Позиции по умолчанию (Центр)
        Vector2 targetPivot = new Vector2(0.5f, 1f);
        Vector3 worldPos = new Vector3(myRect.position.x, GetWorldTopY(myRect), myRect.position.z);

        if (isInWaste && isWideMode)
        {
            // === РЕЖИМ WASTE (ШИРОКАЯ) ===
            // 1. Считаем ширину с учетом ТОЛЬКО прилетевших соседей
            int extraSlots = CountArrivedNeighbors();
            finalWidth = singleCardWidth + (extraSlots * wasteSlotGap);

            // 2. Pivot в левый верхний угол (чтобы росла вправо)
            targetPivot = new Vector2(0f, 1f);

            // 3. Позиция левого края карты
            float leftEdgeX = myRect.position.x - (myRect.rect.width * myRect.pivot.x * transform.lossyScale.x);
            worldPos = new Vector3(leftEdgeX, GetWorldTopY(myRect), myRect.position.z);
        }
        else if (!isInWaste && isWideMode)
        {
            // === РЕЖИМ TABLEAU (ВЫСОКАЯ) ===
            RectTransform lastCard = FindLastCardInStack();
            float topY = GetWorldTopY(myRect);
            float bottomY = GetWorldBottomY(lastCard);
            float totalWorldHeight = Mathf.Abs(topY - bottomY);

            float scaleFactor = transform.lossyScale.y;
            if (scaleFactor == 0) scaleFactor = 1f;
            finalHeight = totalWorldHeight / scaleFactor;
        }
        // else: Одиночная тень (Flyer) использует дефолтные параметры (width=single, pivot=center)

        // Применение
        if (shadowRect.pivot != targetPivot) shadowRect.pivot = targetPivot;

        shadowRect.position = worldPos + (Vector3)currentOffset;
        shadowRect.sizeDelta = new Vector2(finalWidth, finalHeight);
        shadowRect.rotation = transform.rotation;

        if (shadowCanvasGroup != null) shadowCanvasGroup.alpha = currentAlpha;
    }

    private int CountArrivedNeighbors()
    {
        // Мы в Slot_0. Ищем Slot_1 и Slot_2 в родителе (WasteSlots)
        if (transform.parent == null || transform.parent.parent == null) return 0;
        Transform root = transform.parent.parent;
        int count = 0;

        // Проверяем Slot_1
        if (CheckSlotHasArrivedCard(root, "Slot_1")) count++;

        // Проверяем Slot_2
        if (CheckSlotHasArrivedCard(root, "Slot_2")) count++;

        return count;
    }

    private bool CheckSlotHasArrivedCard(Transform root, string slotName)
    {
        Transform slot = root.Find(slotName);
        if (slot == null) return false;

        foreach (Transform child in slot)
        {
            // Пропускаем саму тень
            if (child.name == "Shadow_Monolith") continue;

            // Нашли карту?
            if (child.GetComponent<CardController>() != null)
            {
                // ВАЖНО: Считаем карту "существующей", только если она уже прилетела (anchored < threshold)
                // Если она еще летит, мы её игнорируем, чтобы тень не появлялась раньше времени.
                RectTransform rt = child as RectTransform;
                if (rt != null && rt.anchoredPosition.magnitude <= arrivalThreshold)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void AnalyzeContext()
    {
        if (transform.parent == null) return;

        // Если родитель DragLayer -> Мы точно летим в руке игрока
        if (transform.parent.name == "DragLayer" || transform.parent.GetComponent<DragManager>() != null)
        {
            isDragging = true;
            isInWaste = false;
            return;
        }
        isDragging = false;

        var container = GetComponentInParent<ICardContainer>();
        isInWaste = (container != null && container is WastePile);
    }

    private bool IsBottomCardInStack()
    {
        if (transform.parent == null) return true;

        int myIndex = transform.GetSiblingIndex();

        for (int i = 0; i < myIndex; i++)
        {
            Transform sibling = transform.parent.GetChild(i);
            if (sibling.name == "Shadow_Monolith") continue;
            // Если нашли другую карту ПЕРЕД собой -> мы не нижние
            if (sibling.GetComponent<CardController>() != null) return false;
        }
        return true;
    }

    private RectTransform FindLastCardInStack()
    {
        RectTransform last = myRect;
        if (transform.parent != null)
        {
            for (int i = transform.GetSiblingIndex() + 1; i < transform.parent.childCount; i++)
            {
                Transform child = transform.parent.GetChild(i);
                if (child.name == "Shadow_Monolith") continue;
                if (child.GetComponent<CardController>() != null) last = child as RectTransform;
            }
        }
        return last;
    }

    private void CreateMonolithShadow()
    {
        shadowObject = new GameObject("Shadow_Monolith");
        shadowRect = shadowObject.AddComponent<RectTransform>();
        shadowRect.pivot = new Vector2(0.5f, 1f);

        Image img = shadowObject.AddComponent<Image>();
        img.sprite = shadowSprite;
        img.color = Color.black;
        img.raycastTarget = false;
        img.type = Image.Type.Sliced;

        if (shadowSprite != null && singleCardWidth > 0)
        {
            float ratio = shadowSprite.rect.width / singleCardWidth;
            img.pixelsPerUnitMultiplier = ratio;
        }
        else img.pixelsPerUnitMultiplier = 1f;

        shadowCanvasGroup = shadowObject.AddComponent<CanvasGroup>();
        shadowCanvasGroup.alpha = currentAlpha;
        shadowCanvasGroup.blocksRaycasts = false;
        shadowCanvasGroup.interactable = false;

        shadowRect.SetParent(transform.parent, false);
        shadowRect.localScale = Vector3.one;
        shadowRect.SetAsFirstSibling();
    }

    private void DestroyShadow()
    {
        if (shadowObject != null)
        {
            Destroy(shadowObject);
            shadowObject = null;
        }
    }

    private float GetWorldTopY(RectTransform rt)
    {
        return rt.position.y + (rt.rect.height * (1f - rt.pivot.y) * rt.lossyScale.y);
    }
    private float GetWorldBottomY(RectTransform rt)
    {
        return rt.position.y - (rt.rect.height * rt.pivot.y * rt.lossyScale.y);
    }

   
}