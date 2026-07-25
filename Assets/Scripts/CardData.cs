using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class CardData : MonoBehaviour
{
    // модель карты (назначается CardFactory при создании)
    public CardModel model;

    // UI image (назначается в префабе)
    public Image image;

    // Спрайт рубашки (назначается в префабе или фабрикой)
    public Sprite backSprite;

    // Спрайт лица (устанавливается фабрикой при создании; можно оставить null)
    public Sprite faceSprite;

    [Header("Flip animation")]
    [Tooltip("Общая длительность переворота (сек). Половина — закрытие, половина — разворот).")]
    public float flipDuration = 0.22f;

    [Tooltip("Кривая плавности (0..1).")]
    public AnimationCurve flipCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isFaceUp = false;
    private Coroutine flipCoroutine = null;
    private RectTransform rectTransform;
    private CanvasGroup cg;

    private void Reset()
    {
        rectTransform = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (image == null) image = GetComponentInChildren<Image>();
    }

    private void Awake()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (cg == null) cg = GetComponent<CanvasGroup>();
        if (image == null) image = GetComponentInChildren<Image>();
    }

    /// <summary>
    /// Установить картe состояние "лицом вверх" или "рубашкой вверх".
    /// По умолчанию анимированно. Передав animate=false — переключение мгновенно.
    /// </summary>
    public void SetFaceUp(bool faceUp, bool animate = true)
    {
        // если модель есть и faceSprite не назначен, можно попытаться назначить его
        if (faceSprite == null && image != null)
        {
            faceSprite = image.sprite;
        }

        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        // === 1. МГНОВЕННАЯ СМЕНА (БЕЗ АНИМАЦИИ И БЕЗ ЗВУКА) ===
        if (!animate)
        {
            isFaceUp = faceUp;

            // ЗВУК ОТСЮДА УБРАН!

            if (image != null)
            {
                image.sprite = isFaceUp ? (faceSprite ?? image.sprite) : backSprite;
            }
            // убедимся что локальный scale нормален
            if (rectTransform != null) rectTransform.localScale = Vector3.one;
            return;
        }

        // === 2. ПРОВЕРКА: ЕСЛИ СОСТОЯНИЕ НЕ МЕНЯЕТСЯ ===
        if (isFaceUp == faceUp)
        {
            if (image != null)
            {
                var desired = isFaceUp ? (faceSprite ?? image.sprite) : backSprite;
                if (image.sprite != desired) image.sprite = desired;
            }
            return;
        }

        // === 3. АНИМИРОВАННЫЙ ПЕРЕВОРОТ (ВСТАВЛЯЕМ ЗВУК СЮДА) ===
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Card_Flip");
        }

        // запускаем анимацию переворота
        flipCoroutine = StartCoroutine(FlipRoutine(faceUp));
    }

    private IEnumerator FlipRoutine(bool targetFaceUp)
    {
        // Блокируем взаимодействие во время flip (т.к. форма меняется)
        if (cg != null)
        {
            cg.blocksRaycasts = false;
            // сохраняем интерактивность, но лучше заблокировать временно
        }

        float half = Mathf.Max(0.01f, flipDuration * 0.5f);

        // 1) shrink horizontally (scale.x -> 0)
        float t = 0f;
        Vector3 startScale = rectTransform.localScale;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / half);
            float eased = flipCurve.Evaluate(p);
            float sx = Mathf.Lerp(startScale.x, 0f, eased);
            rectTransform.localScale = new Vector3(sx, startScale.y, startScale.z);
            yield return null;
        }

        // ensure zero-ish
        rectTransform.localScale = new Vector3(0f, startScale.y, startScale.z);

        // swap sprite
        if (image != null)
        {
            image.sprite = targetFaceUp ? (faceSprite ?? image.sprite) : backSprite;
        }
        isFaceUp = targetFaceUp;

        // 2) expand back (scale.x 0 -> 1)
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / half);
            float eased = flipCurve.Evaluate(p);
            float sx = Mathf.Lerp(0f, 1f, eased);
            rectTransform.localScale = new Vector3(sx, startScale.y, startScale.z);
            yield return null;
        }

        rectTransform.localScale = Vector3.one;

        if (cg != null)
        {
            cg.blocksRaycasts = true;
        }

        flipCoroutine = null;
    }
    public void SetModel(CardModel model, Sprite faceSprite)
    {
        this.model = model;
        this.faceSprite = faceSprite;

        // если image ещё не назначен — попробуем получить ссылку
        if (image == null)
            image = GetComponentInChildren<Image>();

        // НЕ меняем image.sprite здесь — оставим это на SetFaceUp,
        // чтобы фабрика могла создавать карты рубашкой вниз без показа лица.
    }
    /// <summary>
    /// Для внешних систем: вернуть текущее состояние (лицом вверх?)
    /// </summary>
    public bool IsFaceUp()
    {
        return isFaceUp;
    }
    public bool IsFlipping()
    {
        return flipCoroutine != null;
    }
    public void UpdateBackVisual(Sprite newBack)
    {
        backSprite = newBack;
        // Если карта сейчас лежит рубашкой вверх, мгновенно обновляем картинку
        if (!isFaceUp && image != null)
        {
            image.sprite = backSprite;
        }
    }
}