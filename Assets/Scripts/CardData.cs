using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class CardData : MonoBehaviour
{
    // модель карты (назначаетс€ CardFactory при создании)
    public CardModel model;

    // UI image (назначаетс€ в префабе)
    public Image image;

    // —прайт рубашки (назначаетс€ в префабе или фабрикой)
    public Sprite backSprite;

    // —прайт лица (устанавливаетс€ фабрикой при создании; можно оставить null)
    public Sprite faceSprite;

    [Header("Flip animation")]
    [Tooltip("ќбща€ длительность переворота (сек). ѕоловина Ч закрытие, половина Ч разворот).")]
    public float flipDuration = 0.22f;

    [Tooltip(" рива€ плавности (0..1).")]
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
    /// ”становить картe состо€ние "лицом вверх" или "рубашкой вверх".
    /// ѕо умолчанию анимированно. ѕередав animate=false Ч переключение мгновенно.
    /// </summary>
    public void SetFaceUp(bool faceUp, bool animate = true)
    {
        // если модель есть и faceSprite не назначен, можно попытатьс€ назначить его
        if (faceSprite == null && image != null)
        {
            // ѕредположение: фабрика могла уже поставить image.sprite в момент создани€.
            // —охраним это как faceSprite, чтобы позже использовать при flip.
            faceSprite = image.sprite;
        }

        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        // если требуетс€ мгновенно Ч просто поставим sprite и флаг
        if (!animate)
        {
            isFaceUp = faceUp;
            if (image != null)
            {
                image.sprite = isFaceUp ? (faceSprite ?? image.sprite) : backSprite;
            }
            // убедимс€ что локальный scale нормален
            if (rectTransform != null) rectTransform.localScale = Vector3.one;
            return;
        }

        // если состо€ние не мен€етс€ Ч ничего не делаем
        if (isFaceUp == faceUp)
        {
            // но если не соответствует спрайт (напр. faceSprite недоступен), исправим
            if (image != null)
            {
                var desired = isFaceUp ? (faceSprite ?? image.sprite) : backSprite;
                if (image.sprite != desired) image.sprite = desired;
            }
            return;
        }

        // запускаем анимацию переворота
        flipCoroutine = StartCoroutine(FlipRoutine(faceUp));
    }

    private IEnumerator FlipRoutine(bool targetFaceUp)
    {
        // Ѕлокируем взаимодействие во врем€ flip (т.к. форма мен€етс€)
        if (cg != null)
        {
            cg.blocksRaycasts = false;
            // сохран€ем интерактивность, но лучше заблокировать временно
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

        // если image ещЄ не назначен Ч попробуем получить ссылку
        if (image == null)
            image = GetComponentInChildren<Image>();

        // Ќ≈ мен€ем image.sprite здесь Ч оставим это на SetFaceUp,
        // чтобы фабрика могла создавать карты рубашкой вниз без показа лица.
    }
    /// <summary>
    /// ƒл€ внешних систем: вернуть текущее состо€ние (лицом вверх?)
    /// </summary>
    public bool IsFaceUp()
    {
        return isFaceUp;
    }
}