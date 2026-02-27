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
    public float wasteSlotGap = 35f;
    public float arrivalThreshold = 50f;

    [Header("Animation")]
    public float animationSpeed = 20f;
    public float fadeSpeed = 5f;

    private GameObject shadowObject;
    private RectTransform shadowRect;
    private CanvasGroup shadowCanvasGroup;

    private Vector2 currentOffset;
    private float currentAlpha;

    private RectTransform myRect;

    private Vector2 lastAnchoredPos;
    private float currentSpeed;

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

        // Удаляем простую тень, если она осталась от прошлых тестов
        Shadow sysShadow = GetComponent<Shadow>();
        if (sysShadow != null) Destroy(sysShadow);

        currentOffset = restingOffset;
        currentAlpha = shadowAlpha;
        lastAnchoredPos = myRect.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) { }

    private void LateUpdate()
    {
        if (myRect == null) return;

        // Скорость полета (используем anchoredPosition для независимости от масштаба экрана)
        if (Time.deltaTime > 0.0001f)
            currentSpeed = (myRect.anchoredPosition - lastAnchoredPos).magnitude / Time.deltaTime;
        else
            currentSpeed = 0f;

        lastAnchoredPos = myRect.anchoredPosition;

        bool isFlying = currentSpeed > 100f;

        AnalyzeContext();

        // Отключение при падении в меню (SceneExitAnimator)
        if (transform.parent != null && transform.parent.GetComponent<Canvas>() != null)
        {
            DestroyShadow();
            return;
        }

        bool shouldDraw = false;
        bool isWideMode = false;
        bool isArriving = myRect.anchoredPosition.magnitude > arrivalThreshold;

        if (isInWaste)
        {
            bool isSlot0Bottom = (transform.parent.name == "Slot_0" && IsBottomCardInStack());
            if (isSlot0Bottom)
            {
                shouldDraw = true;
                isWideMode = true;
            }
            else if (isArriving)
            {
                shouldDraw = true;
                isWideMode = false;
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

        // Жизнь и смерть тени
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

        // Эффект подъема (отдаление тени)
        bool useDragOffset = (isDragging || isFlying || (isInWaste && isArriving)) && !isDying;
        Vector2 targetOffset = useDragOffset ? dragOffset : restingOffset;
        currentOffset = Vector2.Lerp(currentOffset, targetOffset, Time.deltaTime * animationSpeed);

        if (isDying) currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, Time.deltaTime * fadeSpeed);

        UpdateShadowGeometry(isWideMode);
    }

    private void UpdateShadowGeometry(bool isWideMode)
    {
        if (shadowRect.parent != transform.parent) shadowRect.SetParent(transform.parent, true);
        if (shadowRect.GetSiblingIndex() != 0) shadowRect.SetAsFirstSibling();

        float currentCardWidth = myRect.rect.width;
        float finalWidth = currentCardWidth;
        float finalHeight = myRect.rect.height;

        Vector2 targetPivot = new Vector2(0.5f, 1f);
        Vector3 worldPos = new Vector3(myRect.position.x, GetWorldTopY(myRect), myRect.position.z);

        if (isInWaste && isWideMode)
        {
            int extraSlots = CountArrivedNeighbors();
            finalWidth = currentCardWidth + (extraSlots * wasteSlotGap);

            targetPivot = new Vector2(0f, 1f);
            float leftEdgeX = myRect.position.x - (myRect.rect.width * myRect.pivot.x * transform.lossyScale.x);
            worldPos = new Vector3(leftEdgeX, GetWorldTopY(myRect), myRect.position.z);
        }
        else if (!isInWaste && isWideMode)
        {
            RectTransform lastCard = FindLastCardInStack();
            float topY = GetWorldTopY(myRect);
            float bottomY = GetWorldBottomY(lastCard);
            float totalWorldHeight = Mathf.Abs(topY - bottomY);

            float scaleFactor = transform.lossyScale.y;
            if (scaleFactor == 0) scaleFactor = 1f;
            finalHeight = totalWorldHeight / scaleFactor;
        }

        if (shadowRect.pivot != targetPivot) shadowRect.pivot = targetPivot;

        shadowRect.position = worldPos + (Vector3)currentOffset;
        shadowRect.sizeDelta = new Vector2(finalWidth, finalHeight);
        shadowRect.rotation = transform.rotation;

        if (shadowCanvasGroup != null) shadowCanvasGroup.alpha = currentAlpha;

        if (shadowSprite != null && currentCardWidth > 0)
        {
            Image img = shadowObject.GetComponent<Image>();
            if (img != null) img.pixelsPerUnitMultiplier = shadowSprite.rect.width / currentCardWidth;
        }
    }

    // Порог, при котором стопка считается "разорванной"
    private float GetStackBreakThreshold()
    {
        return Mathf.Max(myRect.rect.height, 100f) * 1.2f;
    }

    private bool IsBottomCardInStack()
    {
        if (transform.parent == null) return true;

        int myIndex = transform.GetSiblingIndex();
        RectTransform prevCard = null;

        for (int i = myIndex - 1; i >= 0; i--)
        {
            Transform sibling = transform.parent.GetChild(i);
            if (sibling.name == "Shadow_Monolith" || sibling.name == "CardShadow") continue;
            if (sibling.GetComponent<CardController>() != null)
            {
                prevCard = sibling as RectTransform;
                break;
            }
        }

        if (prevCard == null) return true;

        // --- МАГИЯ ФИЗИЧЕСКОГО РАЗРЫВА ---
        // Если предыдущая карта находится физически слишком далеко,
        // значит мы оторвались от неё (например, летим) и начинаем свою тень!
        float dist = Vector2.Distance(myRect.anchoredPosition, prevCard.anchoredPosition);
        if (dist > GetStackBreakThreshold())
        {
            return true;
        }

        return false;
    }

    private RectTransform FindLastCardInStack()
    {
        RectTransform last = myRect;
        if (transform.parent != null)
        {
            for (int i = transform.GetSiblingIndex() + 1; i < transform.parent.childCount; i++)
            {
                Transform child = transform.parent.GetChild(i);
                if (child.name == "Shadow_Monolith" || child.name == "CardShadow") continue;

                CardController cc = child.GetComponent<CardController>();
                if (cc != null)
                {
                    RectTransform nextRect = child as RectTransform;

                    // --- МАГИЯ ФИЗИЧЕСКОГО РАЗРЫВА ---
                    // Если следующая карта находится далеко, значит она не летит вместе с нами.
                    // Обрываем монолитную тень, чтобы она не растягивалась до самого стола!
                    float dist = Vector2.Distance(last.anchoredPosition, nextRect.anchoredPosition);
                    if (dist > GetStackBreakThreshold())
                    {
                        break;
                    }

                    last = nextRect;
                }
            }
        }
        return last;
    }

    private int CountArrivedNeighbors()
    {
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
                if (rt != null && rt.anchoredPosition.magnitude <= arrivalThreshold) return true;
            }
        }
        return false;
    }

    private void AnalyzeContext()
    {
        if (transform.parent == null) return;

        if (transform.parent.name.Contains("Drag") || transform.parent.GetComponent<DragManager>() != null)
        {
            isDragging = true;
            isInWaste = false;
            return;
        }
        isDragging = false;

        var container = GetComponentInParent<ICardContainer>();
        isInWaste = (container != null && container is WastePile);
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

    private void OnDestroy() { DestroyShadow(); }

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