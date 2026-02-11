using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class ButtonHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Button Scale Settings")]
    public float hoverScale = 1.1f;
    public float scaleSpeed = 10f;

    [Header("Tooltip Settings")]
    public RectTransform tooltipBackground; // Объект панели (фон)
    public TMP_Text tooltipText;            // Текст внутри
    public CanvasGroup textCanvasGroup;     // CanvasGroup на тексте

    [Header("Animation Config")]
    public float expandSpeed = 15f;
    public float textFadeSpeed = 10f;

    [Tooltip("Добавочная ширина к тексту (например, 40 = по 20 пикселей слева и справа)")]
    public float paddingWidth = 40f;

    [Tooltip("Максимальная ширина панели. Если текст длиннее, он перенесется.")]
    public float maxWidth = 400f;

    // Внутренние переменные
    private Vector3 initialBtnScale;
    private Vector3 targetBtnScale;
    private float targetWidth;
    private Coroutine tooltipCoroutine;

    private void Start()
    {
        initialBtnScale = transform.localScale;
        targetBtnScale = initialBtnScale;

        if (tooltipBackground != null)
        {
            tooltipBackground.gameObject.SetActive(false);
            tooltipBackground.sizeDelta = new Vector2(0, tooltipBackground.sizeDelta.y);
        }

        if (textCanvasGroup != null) textCanvasGroup.alpha = 0f;
    }

    private void OnDisable()
    {
        transform.localScale = initialBtnScale;
        if (tooltipBackground != null) tooltipBackground.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetBtnScale = initialBtnScale * hoverScale;

        if (tooltipBackground != null && tooltipText != null)
        {
            tooltipBackground.gameObject.SetActive(true);

            // 1. Расчет ширины
            tooltipText.ForceMeshUpdate();
            Vector2 textSize = tooltipText.GetPreferredValues(tooltipText.text);
            float calculatedWidth = textSize.x + paddingWidth;
            targetWidth = Mathf.Min(calculatedWidth, maxWidth);

            // 2. Запуск последовательной анимации
            if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
            tooltipCoroutine = StartCoroutine(AnimateTooltipOpen());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetBtnScale = initialBtnScale;

        if (tooltipBackground != null)
        {
            if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
            // Можно сделать плавное закрытие, но пока мгновенное для отзывчивости
            tooltipBackground.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (transform.localScale != targetBtnScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetBtnScale, Time.unscaledDeltaTime * scaleSpeed);
        }
    }

    private IEnumerator AnimateTooltipOpen()
    {
        float currentWidth = 0f;

        // СБРОС: Гарантируем, что текст невидим в начале
        if (textCanvasGroup != null) textCanvasGroup.alpha = 0f;

        // Старт фона с 0
        tooltipBackground.sizeDelta = new Vector2(currentWidth, tooltipBackground.sizeDelta.y);

        // ==========================================
        // ЭТАП 1: ТОЛЬКО РАСШИРЕНИЕ ФОНА
        // ==========================================
        // Ждем, пока ширина не станет очень близка к целевой (98%)
        while (Mathf.Abs(currentWidth - targetWidth) > 1f)
        {
            currentWidth = Mathf.Lerp(currentWidth, targetWidth, Time.unscaledDeltaTime * expandSpeed);
            tooltipBackground.sizeDelta = new Vector2(currentWidth, tooltipBackground.sizeDelta.y);
            yield return null;
        }

        // Фиксируем ширину идеально ровно
        tooltipBackground.sizeDelta = new Vector2(targetWidth, tooltipBackground.sizeDelta.y);

        // ==========================================
        // ЭТАП 2: ТОЛЬКО ПОЯВЛЕНИЕ ТЕКСТА
        // ==========================================
        if (textCanvasGroup != null)
        {
            while (textCanvasGroup.alpha < 0.99f)
            {
                textCanvasGroup.alpha = Mathf.Lerp(textCanvasGroup.alpha, 1f, Time.unscaledDeltaTime * textFadeSpeed);
                yield return null;
            }
            textCanvasGroup.alpha = 1f;
        }
    }
}