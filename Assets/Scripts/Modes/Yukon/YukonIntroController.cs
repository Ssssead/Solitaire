using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class YukonIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public YukonModeManager modeManager;

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

    private RectTransform autoCollectBtn;
    private Vector2 autoCollectStartPos;
    private Vector2 autoCollectHiddenPos;

    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private bool isSkipping = false;
    public bool forceInstantSkip = false;
    private bool positionsSaved = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<YukonModeManager>();

        if (modeManager != null && modeManager.autoWinButton != null)
        {
            autoCollectBtn = modeManager.autoWinButton.GetComponent<RectTransform>();
        }

        SetUIVisible(false);
        SetSlotsAlpha(0f); // Жестко скрываем каждый слот в первый кадр
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

    // --- НОВОЕ: Прямое управление альфой каждого слота ---
    private void SetSlotsAlpha(float alpha)
    {
        if (modeManager == null) return;
        var pileManager = modeManager.GetComponent<PileManager>();
        if (pileManager == null) return;

        foreach (var container in pileManager.GetAllContainers())
        {
            var mono = container as MonoBehaviour;
            if (mono != null)
            {
                var cg = mono.GetComponent<CanvasGroup>();
                if (cg == null) cg = mono.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
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
            portraitTopHiddenPos = portraitTopStartPos + new Vector2(0, 400f);
        }

        if (autoCollectBtn != null)
        {
            autoCollectStartPos = autoCollectBtn.anchoredPosition;
            autoCollectHiddenPos = autoCollectStartPos + new Vector2(0, 300f);
        }

        buttonsStartPos.Clear();
        buttonsHiddenPos.Clear();
        foreach (var btn in bottomButtons)
        {
            if (btn != null)
            {
                buttonsStartPos.Add(btn.anchoredPosition);
                buttonsHiddenPos.Add(btn.anchoredPosition + new Vector2(0, -400f));
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
        if (autoCollectBtn != null) list.Add(autoCollectBtn);
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
            if (autoCollectBtn != null) autoCollectBtn.anchoredPosition = autoCollectHiddenPos;

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

    public IEnumerator PlayIntroSequence(bool isRestart, Deal deal)
    {
        isSkipping = false;
        forceInstantSkip = false;

        if (!isRestart)
        {
            yield return StartCoroutine(SkippableWait(startDelay));

            // Проявляем слоты напрямую
            yield return StartCoroutine(FadeInSlots(slotsFadeDuration));

            if (AudioManager.Instance != null && !forceInstantSkip)
                AudioManager.Instance.PlaySound("Panel_Slide_In");

            if (landscapeTopPanel != null) StartCoroutine(AnimateUIElement(landscapeTopPanel, landscapeTopHiddenPos, landscapeTopStartPos, uiSlideDuration));
            if (portraitTopPanel != null) StartCoroutine(AnimateUIElement(portraitTopPanel, portraitTopHiddenPos, portraitTopStartPos, uiSlideDuration));
            if (autoCollectBtn != null) StartCoroutine(AnimateUIElement(autoCollectBtn, autoCollectHiddenPos, autoCollectStartPos, uiSlideDuration));

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null && i < buttonsStartPos.Count)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                    yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
                }
            }
        }

        if (modeManager.deckManager != null)
        {
            yield return StartCoroutine(modeManager.deckManager.PlayIntroDeckArrival(deal, isRestart));
        }
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