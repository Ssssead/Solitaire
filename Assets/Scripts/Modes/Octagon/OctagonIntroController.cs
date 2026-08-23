using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OctagonIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public OctagonModeManager modeManager;
    public OctagonPileManager pileManager;
    public RectTransform topPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.5f;
    public float slotsFadeDuration = 0.8f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    [HideInInspector] public bool isSkipping = false;

    private void Awake()
    {
        // ЗАЩИТА: Гарантируем, что время идет, если мы вернулись из меню (где могла быть пауза)
        Time.timeScale = 1f;

        if (modeManager == null) modeManager = GetComponent<OctagonModeManager>();
        if (pileManager == null) pileManager = GetComponent<OctagonPileManager>();

        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();

        // В Awake мы всегда считаем, что это свежий старт (не рестарт)
        PrepareIntro(false);
    }

    private void Update()
    {
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
    public void UpdateSavedPositions()
    {
        SaveInitialPositions();
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

    public void PrepareIntro(bool isRestarting)
    {
        isSkipping = false;

        // <--- РЕШЕНИЕ 1: Идентично Косынке. При рестарте мы вообще не трогаем интерфейс! --->
        if (isRestarting) return;

        // Это выполняется ТОЛЬКО при первом заходе в сцену из меню
        if (topPanel != null) topPanel.anchoredPosition = topPanelHiddenPos;

        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
                bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
        }

        if (pileManager != null)
        {
            pileManager.SetAllSlotsAlpha(0f);
        }
    }

    public IEnumerator PlayIntroSequence(bool isRestart)
    {
        if (isRestart)
        {
            // Для рестарта просто страхуем, что слоты прозрачны и выходим (анимация UI не нужна)
            if (pileManager != null) pileManager.SetAllSlotsAlpha(1f);

            // Если кто-то другой сдвинул кнопки, жестко возвращаем на место без анимации
            if (topPanel != null) topPanel.anchoredPosition = topPanelStartPos;
            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsStartPos[i];
            }

            yield break;
        }

        yield return StartCoroutine(SkippableWait(startDelay));
        yield return StartCoroutine(FadeInSlots(slotsFadeDuration));

        if (topPanel != null)
            StartCoroutine(AnimateUIElement(topPanel, topPanelHiddenPos, topPanelStartPos, uiSlideDuration, "Panel_Slide_In"));

        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
            {
                StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration, "UI_Drop"));
                yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
            }
        }
    }

    // <--- РЕШЕНИЕ 2: Замена Time.deltaTime на Time.unscaledDeltaTime во всех корутинах --->
    private IEnumerator SkippableWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime * (isSkipping ? 15f : 1f);
            yield return null;
        }
    }

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration, string soundName)
    {
        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime * (isSkipping ? 15f : 1f);
            float t = Mathf.Clamp01(elapsed / duration);
            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(t));
            yield return null;
        }

        if (target != null) target.anchoredPosition = to;

        if (AudioManager.Instance != null && !isSkipping)
            AudioManager.Instance.PlaySound(soundName);
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime * (isSkipping ? 15f : 1f);
            float t = Mathf.Clamp01(elapsed / duration);
            if (pileManager != null) pileManager.SetAllSlotsAlpha(t);
            yield return null;
        }
        if (pileManager != null) pileManager.SetAllSlotsAlpha(1f);
    }
}