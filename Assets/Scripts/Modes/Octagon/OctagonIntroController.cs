using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class OctagonIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public OctagonModeManager modeManager;
    public OctagonPileManager pileManager;

    // --- ДВЕ ПАНЕЛИ ---
    public RectTransform landscapeTopPanel;
    public RectTransform portraitTopPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.5f;
    public float slotsFadeDuration = 0.8f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    private Vector2 landscapeTopStartPos, landscapeTopHiddenPos;
    private Vector2 portraitTopStartPos, portraitTopHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    [HideInInspector] public bool isSkipping = false;
    public bool forceInstantSkip = false;
    private bool positionsSaved = false;

    private void Awake()
    {
        Time.timeScale = 1f;

        if (modeManager == null) modeManager = GetComponent<OctagonModeManager>();
        if (pileManager == null) pileManager = GetComponent<OctagonPileManager>();

        // Мгновенно скрываем всё в первый кадр
        SetUIVisible(false);
        HideAllSlotsImmediately();
    }

    private void Update()
    {
        if (Time.timeSinceLevelLoad > 0.3f)
        {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                isSkipping = true;
            }
        }
    }

    private void HideAllSlotsImmediately()
    {
        if (pileManager != null) pileManager.SetAllSlotsAlpha(0f);
    }

    private void SaveInitialPositions()
    {
        if (positionsSaved) return;
        Canvas.ForceUpdateCanvases();

        if (landscapeTopPanel != null)
        {
            landscapeTopStartPos = landscapeTopPanel.anchoredPosition;
            landscapeTopHiddenPos = landscapeTopStartPos + new Vector2(0, 1000f);
        }
        if (portraitTopPanel != null)
        {
            portraitTopStartPos = portraitTopPanel.anchoredPosition;
            portraitTopHiddenPos = portraitTopStartPos + new Vector2(0, 1000f);
        }

        buttonsStartPos.Clear();
        buttonsHiddenPos.Clear();
        foreach (var btn in bottomButtons)
        {
            if (btn != null)
            {
                buttonsStartPos.Add(btn.anchoredPosition);
                buttonsHiddenPos.Add(btn.anchoredPosition + new Vector2(0, -1000f));
            }
        }
        positionsSaved = true;
    }

    public void UpdateSavedPositions()
    {
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

    public void PrepareIntro(bool isRestarting)
    {
        isSkipping = false;
        forceInstantSkip = false;

        if (!positionsSaved) SaveInitialPositions();

        if (!isRestarting)
        {
            SetUIVisible(false);
            HideAllSlotsImmediately();

            if (landscapeTopPanel != null) landscapeTopPanel.anchoredPosition = landscapeTopHiddenPos;
            if (portraitTopPanel != null) portraitTopPanel.anchoredPosition = portraitTopHiddenPos;

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null && i < buttonsHiddenPos.Count)
                    bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
            }
        }
        else
        {
            SetUIVisible(true);
            if (pileManager != null) pileManager.SetAllSlotsAlpha(1f);
        }
    }

    public IEnumerator PlayIntroSequence(bool isRestart)
    {
        if (isRestart) yield break;

        isSkipping = false;
        forceInstantSkip = false;

        yield return StartCoroutine(SkippableWait(startDelay));
        yield return StartCoroutine(FadeInSlots(slotsFadeDuration));

        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound("Panel_Slide_In");

        if (landscapeTopPanel != null) StartCoroutine(AnimateUIElement(landscapeTopPanel, landscapeTopHiddenPos, landscapeTopStartPos, uiSlideDuration, "Panel_Slide_In"));
        if (portraitTopPanel != null) StartCoroutine(AnimateUIElement(portraitTopPanel, portraitTopHiddenPos, portraitTopStartPos, uiSlideDuration, "Panel_Slide_In"));

        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null && i < buttonsStartPos.Count)
            {
                StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration, "UI_Drop"));
                yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
            }
        }
    }

    private IEnumerator SkippableWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            elapsed += Time.unscaledDeltaTime * (isSkipping ? 15f : 1f);
            yield return null;
        }
    }

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration, string soundName)
    {
        var cg = target.GetComponent<CanvasGroup>();
        if (cg == null) cg = target.gameObject.AddComponent<CanvasGroup>();

        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            elapsed += Time.unscaledDeltaTime * (isSkipping ? 15f : 1f);
            float t = Mathf.Clamp01(elapsed / duration);

            if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, curve.Evaluate(t));
            if (cg != null) cg.alpha = Mathf.Lerp(0f, 1f, curve.Evaluate(t));
            yield return null;
        }

        if (target != null && !forceInstantSkip) target.anchoredPosition = to;
        if (cg != null && !forceInstantSkip) cg.alpha = 1f;

        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound(soundName);
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            elapsed += Time.unscaledDeltaTime * (isSkipping ? 15f : 1f);
            float t = Mathf.Clamp01(elapsed / duration);
            if (pileManager != null) pileManager.SetAllSlotsAlpha(t);
            yield return null;
        }
        if (pileManager != null) pileManager.SetAllSlotsAlpha(1f);
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
}