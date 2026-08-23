using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class CardData : MonoBehaviour
{
    public CardModel model;
    public Image image;
    public Sprite backSprite;
    public Sprite faceSprite;

    [Header("Flip animation")]
    public float flipDuration = 0.22f;
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

    public void SetFaceUp(bool faceUp, bool animate = true)
    {
        if (faceSprite == null && image != null) faceSprite = image.sprite;

        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        if (!animate)
        {
            isFaceUp = faceUp;
            if (image != null) image.sprite = isFaceUp ? (faceSprite ?? image.sprite) : backSprite;

            // === ФИКС ПРОПОРЦИЙ ===
            // Жестко приравниваем X к Y. Карта больше никогда не будет сплющенной!
            if (rectTransform != null)
            {
                Vector3 s = rectTransform.localScale;
                s.x = Mathf.Abs(s.y);
                rectTransform.localScale = s;
            }
            return;
        }

        if (isFaceUp == faceUp)
        {
            if (image != null)
            {
                var desired = isFaceUp ? (faceSprite ?? image.sprite) : backSprite;
                if (image.sprite != desired) image.sprite = desired;
            }
            return;
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Flip");

        flipCoroutine = StartCoroutine(FlipRoutine(faceUp));
    }

    private IEnumerator FlipRoutine(bool targetFaceUp)
    {
        if (cg != null) cg.blocksRaycasts = false;

        float half = Mathf.Max(0.01f, flipDuration * 0.5f);

        // Определяем стартовый фактор от 0 до 1 (на случай, если прервали прошлую анимацию)
        float startY = Mathf.Max(0.0001f, Mathf.Abs(rectTransform.localScale.y));
        float startFactor = Mathf.Clamp01(Mathf.Abs(rectTransform.localScale.x) / startY);

        // 1) shrink horizontally (factor -> 0)
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / half);
            float eased = flipCurve.Evaluate(p);

            float currentFactor = Mathf.Lerp(startFactor, 0f, eased);

            Vector3 s = rectTransform.localScale;
            // === ГЛАВНЫЙ ФИКС ===
            // X динамически вычисляется как процент от Y каждый кадр
            s.x = currentFactor * Mathf.Abs(s.y);
            rectTransform.localScale = s;
            yield return null;
        }

        if (image != null) image.sprite = targetFaceUp ? (faceSprite ?? image.sprite) : backSprite;
        isFaceUp = targetFaceUp;

        // 2) expand back (0 -> 1)
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / half);
            float eased = flipCurve.Evaluate(p);

            float currentFactor = Mathf.Lerp(0f, 1f, eased);

            Vector3 s = rectTransform.localScale;
            s.x = currentFactor * Mathf.Abs(s.y);
            rectTransform.localScale = s;
            yield return null;
        }

        // Финальное выравнивание пропорций (1:1)
        Vector3 finalScale = rectTransform.localScale;
        finalScale.x = Mathf.Abs(finalScale.y);
        rectTransform.localScale = finalScale;

        if (cg != null) cg.blocksRaycasts = true;
        flipCoroutine = null;
    }

    public void SetModel(CardModel model, Sprite faceSprite)
    {
        this.model = model;
        this.faceSprite = faceSprite;
        if (image == null) image = GetComponentInChildren<Image>();
    }

    public bool IsFaceUp() => isFaceUp;
    public bool IsFlipping() => flipCoroutine != null;

    public void UpdateBackVisual(Sprite newBack)
    {
        backSprite = newBack;
        if (!isFaceUp && image != null) image.sprite = backSprite;
    }
}