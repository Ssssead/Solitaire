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
    public Vector2 fallingPixelOffset = new Vector2(20f, -20f);

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

    // --- НОВЫЕ ПЕРЕМЕННЫЕ ДЛЯ ОТСЛЕЖИВАНИЯ ПОЛЕТА ---
    private Vector3 lastWorldPos;
    private float currentSpeed;

    // Состояния
    private bool isDying = false;
    private bool isDragging = false;
    private bool isInWaste = false;

    private void Awake()
    {
        myRect = GetComponent<RectTransform>();

        if (shadowSprite == null)
        {
            var img = GetComponent<Image>();
            if (img != null) shadowSprite = img.sprite;
        }

        currentOffset = restingOffset;
        currentAlpha = shadowAlpha;
        lastWorldPos = myRect.position;
    }

    // Методы интерфейсов нужны для корректной работы EventSystem
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) { }

    private void LateUpdate()
    {
        if (myRect == null) return;

        // --- ОТКЛЮЧЕНИЕ ПРИ ВЫХОДЕ СО СЦЕНЫ ---
        // Если родитель карты - это сам Canvas, значит карту забрал SceneExitAnimator.
        // Выключаем геймплейную тень и прекращаем работу скрипта.
        if (transform.parent != null && transform.parent.GetComponent<Canvas>() != null)
        {
            DestroyShadow();
            return;
        }

        // --- 1. ВЫЧИСЛЯЕМ СКОРОСТЬ КАРТЫ ---
        if (Time.deltaTime > 0.0001f)
        {
            currentSpeed = (myRect.position - lastWorldPos).magnitude / Time.deltaTime;
        }
        else
        {
            currentSpeed = 0f;
        }
        lastWorldPos = myRect.position;

        bool isFlyingGameplay = currentSpeed > 50f;

        AnalyzeContext();

        bool shouldDraw = false;
        bool isWideMode = false;
        bool isArriving = myRect.anchoredPosition.magnitude > arrivalThreshold;

        // --- ЛОГИКА ОТОБРАЖЕНИЯ ---
        if (isDragging || isFlyingGameplay)
        {
            shouldDraw = true;
            isWideMode = false;
        }
        else
        {
            if (isInWaste)
            {
                bool isSlot0Bottom = (transform.parent.name == "Slot_0" && IsBottomCardInStack());
                if (isSlot0Bottom)
                {
                    shouldDraw = true;
                    isWideMode = true;
                }
            }
            else
            {
                if (IsBottomCardInStack())
                {
                    shouldDraw = true;
                    isWideMode = true;
                }
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

        if (isDying && currentAlpha <= 0.01f)
        {
            DestroyShadow();
            return;
        }

        if (shadowObject == null)
        {
            if (isDying) return;
            CreateMonolithShadow();
        }

        // 3. СМЕЩЕНИЕ (OFFSET)
        bool useDragOffset = (isDragging || isFlyingGameplay) && !isDying;
        Vector2 targetOffset = useDragOffset ? dragOffset : restingOffset;
        currentOffset = Vector2.Lerp(currentOffset, targetOffset, Time.deltaTime * animationSpeed);

        if (isDying) currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, Time.deltaTime * fadeSpeed);

        // 4. ОТРИСОВКА
        UpdateShadowGeometry(isWideMode);
    }

    private void UpdateShadowGeometry(bool isWideMode)
    {
        // Привязка к родителю
        if (shadowRect.parent != transform.parent) shadowRect.SetParent(transform.parent, true);

        // --- ИСПРАВЛЕНИЕ Z-INDEX ДЛЯ ПАДАЮЩИХ КАРТ ---
        if (isWideMode)
        {
            // Монолитная тень в слоте всегда лежит в самом низу стопки
            if (shadowRect.GetSiblingIndex() != 0) shadowRect.SetAsFirstSibling();
        }
        else
        {
            // Персональная тень (летящая или падающая карта) должна быть строго под картой!
            // Иначе на главном Canvas она провалится под зеленый фон.
            int shadowIdx = shadowRect.GetSiblingIndex();
            int cardIdx = transform.GetSiblingIndex();

            if (shadowIdx < cardIdx - 1)
            {
                shadowRect.SetSiblingIndex(cardIdx - 1);
            }
            else if (shadowIdx > cardIdx)
            {
                shadowRect.SetSiblingIndex(cardIdx);
            }
        }
        // ---------------------------------------------

        // Берем актуальную ширину карты каждый кадр
        float currentCardWidth = myRect.rect.width;
        float finalWidth = currentCardWidth;
        float finalHeight = myRect.rect.height;

        // Настройки Pivot и Позиции по умолчанию (Центр)
        Vector2 targetPivot = new Vector2(0.5f, 1f);
        Vector3 worldPos = new Vector3(myRect.position.x, GetWorldTopY(myRect), myRect.position.z);

        if (isInWaste && isWideMode)
        {
            // === РЕЖИМ WASTE (ШИРОКАЯ) ===
            int extraSlots = CountArrivedNeighbors();
            finalWidth = currentCardWidth + (extraSlots * wasteSlotGap);

            targetPivot = new Vector2(0f, 1f);
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

        // Применение
        if (shadowRect.pivot != targetPivot) shadowRect.pivot = targetPivot;

        shadowRect.position = worldPos + (Vector3)currentOffset;
        shadowRect.sizeDelta = new Vector2(finalWidth, finalHeight);
        shadowRect.rotation = transform.rotation;

        if (shadowCanvasGroup != null) shadowCanvasGroup.alpha = currentAlpha;

        // --- ДИНАМИЧЕСКИЙ ПЕРЕСЧЕТ 9-SLICE ---
        if (shadowSprite != null && currentCardWidth > 0)
        {
            Image img = shadowObject.GetComponent<Image>();
            if (img != null) img.pixelsPerUnitMultiplier = shadowSprite.rect.width / currentCardWidth;
        }
    }

    private int CountArrivedNeighbors()
    {
        // Мы в Slot_0. Ищем Slot_1 и Slot_2 в родителе (WasteSlots)
        if (transform.parent == null || transform.parent.parent == null) return 0;
        Transform root = transform.parent.parent;
        int count = 0;

        if (CheckSlotHasArrivedCard(root, "Slot_1")) count++;
        if (CheckSlotHasArrivedCard(root, "Slot_2")) count++;

        return count;
    }

    private bool CheckSlotHasArrivedCard(Transform root, string slotName)
    {
        Transform slot = root.Find(slotName);
        if (slot == null) return false;

        foreach (Transform child in slot)
        {
            if (child.name == "Shadow_Monolith") continue;

            if (child.GetComponent<CardController>() != null)
            {
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

        // Защищает тень от утолщения при старте
        float currentCardWidth = myRect.rect.width;
        if (shadowSprite != null && currentCardWidth > 0)
        {
            float ratio = shadowSprite.rect.width / currentCardWidth;
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

    private void OnDestroy()
    {
        DestroyShadow();
    }

    private void DestroyShadow()
    {
        if (shadowObject != null)
        {
            if (Application.isPlaying) Destroy(shadowObject);
            else DestroyImmediate(shadowObject);
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