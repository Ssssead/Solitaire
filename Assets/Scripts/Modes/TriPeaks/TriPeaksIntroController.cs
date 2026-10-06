using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)] // Запускается первым, чтобы скрыть UI до отрисовки кадра
public class TriPeaksIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public TriPeaksModeManager modeManager;

    // --- ИЗМЕНЕНИЕ 1: Две панели ---
    public RectTransform landscapeTopPanel;
    public RectTransform portraitTopPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.2f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;
    public float slotFadeDuration = 0.5f;

    public bool IsSkipping { get; private set; }
    public bool forceInstantSkip = false;

    // --- ИЗМЕНЕНИЕ 2: Двойные позиции ---
    private Vector2 landscapeTopStartPos, landscapeTopHiddenPos;
    private Vector2 portraitTopStartPos, portraitTopHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private bool positionsSaved = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = FindObjectOfType<TriPeaksModeManager>();

        // Мгновенно скрываем UI элементы
        SetUIVisible(false);

        // Мгновенно скрываем слот сброса
        if (modeManager != null && modeManager.pileManager != null)
        {
            modeManager.pileManager.SetWasteSlotAlpha(0f);
        }
    }

    private void Update()
    {
        // Защита времени, чтобы клик по кнопке в меню не пропустил анимацию
        if (Time.timeSinceLevelLoad > 0.3f)
        {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                IsSkipping = true;
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
        forceInstantSkip = true;
        IsSkipping = true;
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

    public void PrepareIntro(bool playIntro)
    {
        IsSkipping = false;
        forceInstantSkip = false;

        if (!positionsSaved) SaveInitialPositions();

        if (playIntro)
        {
            if (modeManager != null && modeManager.pileManager != null)
            {
                modeManager.pileManager.SetWasteSlotAlpha(0f);
            }

            SetUIVisible(false);

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
            if (modeManager != null && modeManager.pileManager != null)
            {
                modeManager.pileManager.SetWasteSlotAlpha(1f);
            }

            SetUIVisible(true);

            if (landscapeTopPanel != null) landscapeTopPanel.anchoredPosition = landscapeTopStartPos;
            if (portraitTopPanel != null) portraitTopPanel.anchoredPosition = portraitTopStartPos;

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null && i < buttonsStartPos.Count)
                    bottomButtons[i].anchoredPosition = buttonsStartPos[i];
            }
        }
    }

    public IEnumerator PlayUIIntroSequence()
    {
        IsSkipping = false;
        forceInstantSkip = false;

        yield return StartCoroutine(SkippableWait(startDelay));

        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound("Panel_Slide_In");

        // Запускаем проявление слота Waste
        StartCoroutine(FadeInWasteSlot(slotFadeDuration));

        if (landscapeTopPanel != null)
            StartCoroutine(AnimateUIElement(landscapeTopPanel, landscapeTopHiddenPos, landscapeTopStartPos, uiSlideDuration));

        if (portraitTopPanel != null)
            StartCoroutine(AnimateUIElement(portraitTopPanel, portraitTopHiddenPos, portraitTopStartPos, uiSlideDuration));

        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null && i < buttonsStartPos.Count)
            {
                StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
            }
        }
    }

    private IEnumerator FadeInWasteSlot(float duration)
    {
        if (modeManager == null || modeManager.pileManager == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            float speed = IsSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            modeManager.pileManager.SetWasteSlotAlpha(t);
            yield return null;
        }
        modeManager.pileManager.SetWasteSlotAlpha(1f);
    }

    private IEnumerator SkippableWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            float speed = IsSkipping ? 15f : 1f;
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
            float speed = IsSkipping ? 15f : 1f;
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
}