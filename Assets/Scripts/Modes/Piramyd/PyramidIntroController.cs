using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PyramidIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public PyramidModeManager modeManager;
    public RectTransform topPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.3f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.05f;
    public float slotsFadeDuration = 0.5f;

    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private bool isSkipping = false;
    public bool IsSkipping => isSkipping;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<PyramidModeManager>();
        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();
        PrepareIntro(false);
    }

    private void Update()
    {
        // Ускорение по клику или тапу
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            isSkipping = true;
        }
    }

    private void SaveInitialPositions()
    {
        if (topPanel != null)
        {
            topPanelStartPos = topPanel.anchoredPosition;
            topPanelHiddenPos = topPanelStartPos + new Vector2(0, 300f);
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
    }

    public List<RectTransform> GetTopUIElements()
    {
        var list = new List<RectTransform>();
        if (topPanel != null) list.Add(topPanel);
        return list;
    }

    public List<RectTransform> GetBottomUIElements()
    {
        return bottomButtons != null ? new List<RectTransform>(bottomButtons) : new List<RectTransform>();
    }

    public void PrepareIntro(bool isRestart)
    {
        isSkipping = false;
        if (!isRestart)
        {
            SetSlotsAlpha(0f); // Прячем слоты
            if (topPanel != null) topPanel.anchoredPosition = topPanelHiddenPos;
            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
        }
    }

    public IEnumerator PlayIntroSequence()
    {
        yield return StartCoroutine(SkippableWait(startDelay));

        // 1. Выезд UI
        if (topPanel != null) StartCoroutine(AnimateUIElement(topPanel, topPanelHiddenPos, topPanelStartPos, uiSlideDuration));
        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
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
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            yield return null;
        }
    }

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        while (elapsed < duration)
        {
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(t));
            yield return null;
        }
        if (target != null) target.anchoredPosition = to;
    }

    // Собираем все картинки-подложки слотов, чтобы плавно проявить их
    private List<Image> GetSlotImages()
    {
        List<Image> images = new List<Image>();
        if (modeManager.pileManager != null)
        {
            foreach (var slot in modeManager.pileManager.TableauSlots)
            {
                if (slot != null)
                {
                    var img = slot.GetComponent<Image>();
                    if (img != null) images.Add(img);
                }
            }
        }
        if (modeManager.deckManager != null)
        {
            if (modeManager.deckManager.stockRoot) { var img = modeManager.deckManager.stockRoot.GetComponent<Image>(); if (img) images.Add(img); }
            if (modeManager.deckManager.wasteRoot) { var img = modeManager.deckManager.wasteRoot.GetComponent<Image>(); if (img) images.Add(img); }
            if (modeManager.deckManager.leftFoundation) { var img = modeManager.deckManager.leftFoundation.GetComponent<Image>(); if (img) images.Add(img); }
            if (modeManager.deckManager.rightFoundation) { var img = modeManager.deckManager.rightFoundation.GetComponent<Image>(); if (img) images.Add(img); }
        }
        return images;
    }

    private void SetSlotsAlpha(float alpha)
    {
        var images = GetSlotImages();
        foreach (var img in images)
        {
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
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            foreach (var img in images)
            {
                Color c = img.color;
                c.a = t;
                img.color = c;
            }
            yield return null;
        }
        foreach (var img in images)
        {
            Color c = img.color;
            c.a = 1f;
            img.color = c;
        }
    }
}