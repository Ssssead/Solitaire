using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class MontanaIntroController : MonoBehaviour, IIntroController
{
    [Header("Core Reference")]
    public MontanaModeManager modeManager;

    [Header("Top UI Elements")]
    public RectTransform landscapeTopPanel;
    public RectTransform portraitTopPanel;
    public RectTransform topExtraButton;

    [Header("Bottom UI Elements")]
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float slotsFadeDuration = 0.5f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    private Vector2 landscapeTopStartPos, landscapeTopHiddenPos;
    private Vector2 portraitTopStartPos, portraitTopHiddenPos;
    private Vector2 topExtraButtonStartPos, topExtraButtonHiddenPos;
    private List<Vector2> bottomButtonsStartPos = new List<Vector2>();
    private List<Vector2> bottomButtonsHiddenPos = new List<Vector2>();

    private bool positionsSaved = false;
    private bool isSkipping = false;
    public bool forceInstantSkip = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = FindObjectOfType<MontanaModeManager>();

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
        // Интро стартует на -50 (до создания PileManager), поэтому мы скроем 
        // слоты, обратившись к GameLayoutManager напрямую.
        if (GameLayoutManager.Instance != null)
        {
            foreach (var s in GameLayoutManager.Instance.slots)
            {
                if (s.targetSlot != null)
                {
                    var cg = s.targetSlot.GetComponent<CanvasGroup>();
                    if (cg == null) cg = s.targetSlot.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                }
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
            landscapeTopHiddenPos = landscapeTopStartPos + new Vector2(0, 1000f);
        }
        if (portraitTopPanel != null)
        {
            portraitTopStartPos = portraitTopPanel.anchoredPosition;
            portraitTopHiddenPos = portraitTopStartPos + new Vector2(0, 1000f);
        }
        if (topExtraButton != null)
        {
            topExtraButtonStartPos = topExtraButton.anchoredPosition;
            topExtraButtonHiddenPos = topExtraButtonStartPos + new Vector2(0, 1000f);
        }

        bottomButtonsStartPos.Clear();
        bottomButtonsHiddenPos.Clear();
        foreach (var btn in bottomButtons)
        {
            if (btn != null)
            {
                bottomButtonsStartPos.Add(btn.anchoredPosition);
                bottomButtonsHiddenPos.Add(btn.anchoredPosition + new Vector2(0, -1000f));
            }
        }
        positionsSaved = true;
    }

    public void UpdateSavedPositions()
    {
        forceInstantSkip = true;
        isSkipping = true;
    }

    public void PrepareIntro(bool isRestarting)
    {
        isSkipping = false;
        forceInstantSkip = false;

        if (!positionsSaved) SaveInitialPositions();

        if (modeManager != null) modeManager.IsInputAllowed = false;

        if (isRestarting)
        {
            SetUIVisible(true);
            SetSlotsAlpha(1f);
        }
        else
        {
            SetSlotsAlpha(0f);
            SetUIVisible(false);

            if (landscapeTopPanel != null) landscapeTopPanel.anchoredPosition = landscapeTopHiddenPos;
            if (portraitTopPanel != null) portraitTopPanel.anchoredPosition = portraitTopHiddenPos;
            if (topExtraButton != null) topExtraButton.anchoredPosition = topExtraButtonHiddenPos;

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null && i < bottomButtonsHiddenPos.Count)
                    bottomButtons[i].anchoredPosition = bottomButtonsHiddenPos[i];
            }
        }
    }

    public IEnumerator PlayIntroSequence(bool isRestarting)
    {
        if (isRestarting) yield break;

        isSkipping = false;
        forceInstantSkip = false;

        yield return StartCoroutine(FadeInSlots(slotsFadeDuration));

        if (landscapeTopPanel != null)
            StartCoroutine(AnimateUIElement(landscapeTopPanel, landscapeTopHiddenPos, landscapeTopStartPos, uiSlideDuration, "Panel_Slide_In", true));
        if (portraitTopPanel != null)
            StartCoroutine(AnimateUIElement(portraitTopPanel, portraitTopHiddenPos, portraitTopStartPos, uiSlideDuration, "Panel_Slide_In", true));

        if (topExtraButton != null)
            StartCoroutine(AnimateUIElement(topExtraButton, topExtraButtonHiddenPos, topExtraButtonStartPos, uiSlideDuration, "Panel_Slide_In", true));

        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null && i < bottomButtonsStartPos.Count)
            {
                StartCoroutine(AnimateUIElement(bottomButtons[i], bottomButtonsHiddenPos[i], bottomButtonsStartPos[i], uiSlideDuration, "UI_Drop", false));
                yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
            }
        }

        yield return StartCoroutine(SkippableWait(uiSlideDuration));
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

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration, string soundName, bool playSoundAtStart)
    {
        if (playSoundAtStart && AudioManager.Instance != null && !forceInstantSkip && !string.IsNullOrEmpty(soundName))
        {
            AudioManager.Instance.PlaySound(soundName);
        }

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

        if (!playSoundAtStart && AudioManager.Instance != null && !forceInstantSkip && !string.IsNullOrEmpty(soundName))
        {
            AudioManager.Instance.PlaySound(soundName);
        }
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

    private void SetSlotsAlpha(float alpha)
    {
        if (modeManager != null && modeManager.pileManager != null)
        {
            foreach (var slot in modeManager.pileManager.Slots)
            {
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg == null) cg = slot.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
        }
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            SetSlotsAlpha(t);
            yield return null;
        }
        SetSlotsAlpha(1f);
    }

    public List<RectTransform> GetTopUIElements()
    {
        List<RectTransform> list = new List<RectTransform>();
        if (landscapeTopPanel != null) list.Add(landscapeTopPanel);
        if (portraitTopPanel != null) list.Add(portraitTopPanel);
        if (topExtraButton != null) list.Add(topExtraButton);
        return list;
    }

    public List<RectTransform> GetBottomUIElements()
    {
        List<RectTransform> list = new List<RectTransform>();
        foreach (var b in bottomButtons) if (b != null) list.Add(b);
        return list;
    }
}