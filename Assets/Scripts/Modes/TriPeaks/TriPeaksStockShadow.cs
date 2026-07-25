using UnityEngine;
using UnityEngine.UI;

public class TriPeaksStockShadow : MonoBehaviour
{
    [Header("References")]
    public Transform stockSlotTransform;
    public Sprite shadowSprite;

    [Header("Settings")]
    public Vector2 offset = new Vector2(4f, -4f);

    [Tooltip("Используйте отрицательные значения (например, -10), если сама картинка тени имеет прозрачные поля и кажется слишком широкой.")]
    public float widthAdjustment = 0f;
    public float heightAdjustment = 0f;

    [Range(0f, 1f)] public float maxAlpha = 0.5f;
    public float transitionSpeed = 20f;

    private GameObject shadowObject;
    private RectTransform shadowRect;
    private Image shadowImage;

    private float currentWidth;
    private float currentHeight;
    private float currentCenterX;
    private float currentAlpha;

    private bool isRatioSet = false;

    private void Start()
    {
        if (stockSlotTransform == null) stockSlotTransform = transform;

        shadowObject = new GameObject("StockDynamicShadow");
        shadowRect = shadowObject.AddComponent<RectTransform>();

        shadowRect.SetParent(stockSlotTransform, false);
        shadowRect.SetAsFirstSibling();

        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);

        shadowImage = shadowObject.AddComponent<Image>();
        shadowImage.sprite = shadowSprite;
        shadowImage.color = new Color(0, 0, 0, 0);
        shadowImage.type = Image.Type.Sliced;
        shadowImage.raycastTarget = false;

        currentWidth = 100f;
        currentHeight = 150f;
        currentCenterX = 0f;
        currentAlpha = 0f;
    }

    private void Update()
    {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        int cardCount = 0;

        float cardWidth = 0f;
        float cardHeight = 0f;

        // Ищем границы стопки и параллельно считываем размер реальной карты
        foreach (Transform child in stockSlotTransform)
        {
            if (child != shadowRect && child.gameObject.activeSelf)
            {
                float x = child.localPosition.x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                cardCount++;

                // Автоматически берем размер с первой найденной карты
                if (cardWidth == 0f)
                {
                    RectTransform rt = child as RectTransform;
                    if (rt != null && rt.rect.width > 0)
                    {
                        cardWidth = rt.rect.width;
                        cardHeight = rt.rect.height;
                    }
                }
            }
        }

        // Единожды настраиваем качество 9-slice на основе реального размера карты
        if (cardCount > 0 && !isRatioSet && shadowSprite != null && cardWidth > 0)
        {
            float ratio = shadowSprite.rect.width / cardWidth;
            shadowImage.pixelsPerUnitMultiplier = ratio;

            // Чтобы тень не выростала из нуля, сразу задаем ей правильный стартовый размер
            currentWidth = cardWidth + (maxX - minX) + widthAdjustment;
            currentHeight = cardHeight + heightAdjustment;
            shadowRect.sizeDelta = new Vector2(currentWidth, currentHeight);

            isRatioSet = true;
        }

        float targetWidth = currentWidth;
        float targetHeight = currentHeight;
        float targetCenterX = currentCenterX;
        float targetAlpha = 0f;

        if (cardCount > 0)
        {
            // Формируем финальный размер
            targetWidth = cardWidth + (maxX - minX) + widthAdjustment;
            targetHeight = cardHeight + heightAdjustment;
            targetCenterX = (minX + maxX) / 2f;
            targetAlpha = maxAlpha;
        }

        // Плавная интерполяция
        float dt = Time.deltaTime * transitionSpeed;
        currentWidth = Mathf.Lerp(currentWidth, targetWidth, dt);
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, dt);
        currentCenterX = Mathf.Lerp(currentCenterX, targetCenterX, dt);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, dt);

        shadowRect.sizeDelta = new Vector2(currentWidth, currentHeight);
        shadowRect.localPosition = new Vector3(currentCenterX + offset.x, offset.y, 0f);

        Color c = shadowImage.color;
        c.a = currentAlpha;
        shadowImage.color = c;

        // Всегда держим тень позади карт
        if (shadowRect.GetSiblingIndex() != 0)
        {
            shadowRect.SetAsFirstSibling();
        }
    }
}