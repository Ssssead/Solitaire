using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class ButtonHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Button Scale Settings")]
    public float hoverScale = 1.1f;
    public float scaleSpeed = 10f;

    [Header("Tooltip Settings (Optional)")]
    // Можно оставить пустыми, если тултип не нужен
    public RectTransform tooltipBackground;
    public TMP_Text tooltipText;
    public CanvasGroup textCanvasGroup;

    [Header("Animation Config")]
    public float expandSpeed = 15f;
    public float textFadeSpeed = 10f;
    public float paddingWidth = 40f;
    public float maxWidth = 400f;

    private Vector3 initialBtnScale;
    private Vector3 targetBtnScale;
    private float targetWidth;
    private Coroutine tooltipCoroutine;

    // ИСПРАВЛЕНИЕ 1: Используем Awake, чтобы успеть запомнить размер до того,
    // как менеджер выключит кнопку.
    private void Awake()
    {
        initialBtnScale = transform.localScale;

        // ИСПРАВЛЕНИЕ 2: Защита от нуля. Если по какой-то причине размер 0 
        // (например, LayoutGroup еще не пересчитался или объект выключен),
        // принудительно считаем, что нормальный размер = 1.
        if (initialBtnScale == Vector3.zero)
        {
            initialBtnScale = Vector3.one;
        }

        targetBtnScale = initialBtnScale;
    }

    private void Start()
    {
        // Настройка тултипа (если он назначен)
        if (tooltipBackground != null)
        {
            tooltipBackground.gameObject.SetActive(false);
            tooltipBackground.sizeDelta = new Vector2(0, tooltipBackground.sizeDelta.y);
        }

        if (textCanvasGroup != null) textCanvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        // При включении кнопки сбрасываем целевой масштаб на базовый,
        // чтобы она не "застряла" в увеличенном состоянии
        targetBtnScale = initialBtnScale;
        transform.localScale = initialBtnScale;
    }

    private void OnDisable()
    {
        // Сбрасываем размер при выключении
        transform.localScale = initialBtnScale;

        if (tooltipBackground != null) tooltipBackground.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Увеличиваем
        targetBtnScale = initialBtnScale * hoverScale;

        // Если тултип назначен — показываем
        if (tooltipBackground != null && tooltipText != null)
        {
            tooltipBackground.gameObject.SetActive(true);
            tooltipText.ForceMeshUpdate();
            Vector2 textSize = tooltipText.GetPreferredValues(tooltipText.text);
            float calculatedWidth = textSize.x + paddingWidth;
            targetWidth = Mathf.Min(calculatedWidth, maxWidth);

            if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
            tooltipCoroutine = StartCoroutine(AnimateTooltipOpen());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Возвращаем размер
        targetBtnScale = initialBtnScale;

        // Если тултип назначен — прячем
        if (tooltipBackground != null)
        {
            if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
            tooltipBackground.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Плавное изменение масштаба
        if (Vector3.Distance(transform.localScale, targetBtnScale) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetBtnScale, Time.unscaledDeltaTime * scaleSpeed);
        }
        else
        {
            transform.localScale = targetBtnScale;
        }
    }

    private IEnumerator AnimateTooltipOpen()
    {
        if (tooltipBackground == null) yield break;

        float currentWidth = 0f;
        if (textCanvasGroup != null) textCanvasGroup.alpha = 0f;
        tooltipBackground.sizeDelta = new Vector2(currentWidth, tooltipBackground.sizeDelta.y);

        while (Mathf.Abs(currentWidth - targetWidth) > 1f)
        {
            currentWidth = Mathf.Lerp(currentWidth, targetWidth, Time.unscaledDeltaTime * expandSpeed);
            tooltipBackground.sizeDelta = new Vector2(currentWidth, tooltipBackground.sizeDelta.y);
            yield return null;
        }
        tooltipBackground.sizeDelta = new Vector2(targetWidth, tooltipBackground.sizeDelta.y);

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