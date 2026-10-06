using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-50)] // Запускается до GameLayoutManager, чтобы скрыть объекты без моргания
public class PyramidIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public PyramidModeManager modeManager;

    // --- ИЗМЕНЕНИЕ 1: Разделяем панели на две ---
    public RectTransform landscapeTopPanel;
    public RectTransform portraitTopPanel;

    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.3f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.05f;
    public float slotsFadeDuration = 0.5f;

    // --- ИЗМЕНЕНИЕ 2: Двойные позиции ---
    private Vector2 landscapeTopStartPos, landscapeTopHiddenPos;
    private Vector2 portraitTopStartPos, portraitTopHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private bool positionsSaved = false;
    private bool isSkipping = false;
    public bool forceInstantSkip = false;
    public bool IsSkipping => isSkipping;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<PyramidModeManager>();

        // Мгновенно скрываем UI элементы через прозрачность (чтобы избежать 1-кадрового мелькания)
        SetUIVisible(false);

        // Мгновенно скрываем слоты в первый же кадр
        SetSlotsAlpha(0f);
    }

    private void Update()
    {
        // Ускорение по клику или тапу с защитой времени старта сцены
        if (Time.timeSinceLevelLoad > 0.3f)
        {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                isSkipping = true;
            }
        }
    }

    private void SaveInitialPositions()
    {
        if (positionsSaved) return;
        Canvas.ForceUpdateCanvases();

        if (landscapeTopPanel != null)
        {
            landscapeTopStartPos = landscapeTopPanel.anchoredPosition;
            landscapeTopHiddenPos = landscapeTopStartPos + new Vector2(0, 300f);
        }
        if (portraitTopPanel != null)
        {
            portraitTopStartPos = portraitTopPanel.anchoredPosition;
            portraitTopHiddenPos = portraitTopStartPos + new Vector2(0, 300f);
        }

        buttonsStartPos.Clear();
        buttonsHiddenPos.Clear();
        foreach (var btn in bottomButtons)
        {
            if (btn != null)
            {
                buttonsStartPos.Add(btn.anchoredPosition);
                buttonsHiddenPos.Add(btn.anchoredPosition + new Vector2(0, -300f));
            }
        }
        positionsSaved = true;
    }

    public void UpdateSavedPositions()
    {
        // При повороте экрана просто отменяем анимации (якоря остаются верными)
        forceInstantSkip = true;
        isSkipping = true;
    }

    public List<RectTransform> GetTopUIElements()
    {
        var list = new List<RectTransform>();
        if (landscapeTopPanel != null) list.Add(landscapeTopPanel);
        if (portraitTopPanel != null) list.Add(portraitTopPanel);
        return list;
    }

    public List<RectTransform> GetBottomUIElements()
    {
        return bottomButtons != null ? new List<RectTransform>(bottomButtons) : new List<RectTransform>();
    }

    public void PrepareIntro(bool isRestart)
    {
        isSkipping = false;
        forceInstantSkip = false;

        if (!positionsSaved) SaveInitialPositions();

        if (!isRestart)
        {
            SetSlotsAlpha(0f);
            SetUIVisible(false);

            if (landscapeTopPanel != null) landscapeTopPanel.anchoredPosition = landscapeTopHiddenPos;
            if (portraitTopPanel != null) portraitTopPanel.anchoredPosition = portraitTopHiddenPos;

            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null && i < buttonsHiddenPos.Count)
                    bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
        }
        else
        {
            SetUIVisible(true);
            SetSlotsAlpha(1f);
        }
    }

    public IEnumerator PlayIntroSequence()
    {
        isSkipping = false;
        forceInstantSkip = false;

        yield return StartCoroutine(SkippableWait(startDelay));

        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound("Panel_Slide_In");

        // 1. Выезд UI
        if (landscapeTopPanel != null) StartCoroutine(AnimateUIElement(landscapeTopPanel, landscapeTopHiddenPos, landscapeTopStartPos, uiSlideDuration));
        if (portraitTopPanel != null) StartCoroutine(AnimateUIElement(portraitTopPanel, portraitTopHiddenPos, portraitTopStartPos, uiSlideDuration));

        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null && i < buttonsStartPos.Count)
            {
                StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
            }
        }

        // 2. Проявление слотов
        yield return StartCoroutine(FadeInSlots(slotsFadeDuration));
    }

    private IEnumerator SkippableWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            yield return null;
        }
    }

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        var cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();

        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);

            if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, curve.Evaluate(t));
            if (cg != null) cg.alpha = Mathf.Lerp(0f, 1f, curve.Evaluate(t));

            yield return null;
        }

        if (target != null && !forceInstantSkip) target.anchoredPosition = to;
        if (cg != null && !forceInstantSkip) cg.alpha = 1f;

        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound("UI_Drop");
    }

    private void SetUIVisible(bool visible)
    {
        float alpha = visible ? 1f : 0f;

        foreach (var el in GetTopUIElements())
        {
            if (el != null)
            {
                var cg = el.GetComponent<CanvasGroup>();
                if (cg == null) cg = el.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
        }

        foreach (var el in GetBottomUIElements())
        {
            if (el != null)
            {
                var cg = el.GetComponent<CanvasGroup>();
                if (cg == null) cg = el.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
        }
    }

    // Собираем все картинки-подложки слотов, чтобы плавно проявить их
    private List<Image> GetSlotImages()
    {
        List<Image> images = new List<Image>();

        if (modeManager.deckManager != null)
        {
            // Берем только Сток, Сброс и Фундаменты (Дома)
            if (modeManager.deckManager.stockRoot)
            {
                var img = modeManager.deckManager.stockRoot.GetComponent<Image>();
                if (img) images.Add(img);
            }
            if (modeManager.deckManager.wasteRoot)
            {
                var img = modeManager.deckManager.wasteRoot.GetComponent<Image>();
                if (img) images.Add(img);
            }
            if (modeManager.deckManager.leftFoundation)
            {
                var img = modeManager.deckManager.leftFoundation.GetComponent<Image>();
                if (img) images.Add(img);
            }
            if (modeManager.deckManager.rightFoundation)
            {
                var img = modeManager.deckManager.rightFoundation.GetComponent<Image>();
                if (img) images.Add(img);
            }
        }

        return images;
    }

    private void SetSlotsAlpha(float alpha)
    {
        var images = GetSlotImages();
        foreach (var img in images)
        {
            if (img == null) continue;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        var images = GetSlotImages();
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);

            foreach (var img in images)
            {
                if (img == null) continue;
                Color c = img.color;
                c.a = t;
                img.color = c;
            }
            yield return null;
        }
        foreach (var img in images)
        {
            if (img == null) continue;
            Color c = img.color;
            c.a = 1f;
            img.color = c;
        }
    }
}