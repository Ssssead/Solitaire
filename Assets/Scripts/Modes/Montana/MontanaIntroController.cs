using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MontanaIntroController : MonoBehaviour, IIntroController
{
    [Header("Core Reference")]
    public MontanaModeManager modeManager;

    [Header("Top UI Elements (Выезжают СВЕРХУ)")]
    public RectTransform topPanelMain;
    public RectTransform topExtraButton;

    [Header("Bottom UI Elements (Выезжают СНИЗУ)")]
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float slotsFadeDuration = 0.5f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;
    private Vector2 topExtraButtonStartPos;
    private Vector2 topExtraButtonHiddenPos;
    private List<Vector2> bottomButtonsStartPos = new List<Vector2>();
    private List<Vector2> bottomButtonsHiddenPos = new List<Vector2>();

    private bool isSkipping = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = FindObjectOfType<MontanaModeManager>();

        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();
        PrepareIntro(modeManager.isRestarting);
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
        if (topPanelMain != null)
        {
            topPanelStartPos = topPanelMain.anchoredPosition;
            topPanelHiddenPos = topPanelStartPos + new Vector2(0, 300f);
        }

        if (topExtraButton != null)
        {
            topExtraButtonStartPos = topExtraButton.anchoredPosition;
            topExtraButtonHiddenPos = topExtraButtonStartPos + new Vector2(0, 300f);
        }

        bottomButtonsStartPos.Clear();
        bottomButtonsHiddenPos.Clear();
        foreach (var btn in bottomButtons)
        {
            if (btn != null)
            {
                bottomButtonsStartPos.Add(btn.anchoredPosition);
                bottomButtonsHiddenPos.Add(btn.anchoredPosition + new Vector2(0, -300f));
            }
        }
    }
    public void UpdateSavedPositions()
    {
        SaveInitialPositions();
    }
    public void PrepareIntro(bool isRestarting)
    {
        isSkipping = false;
        if (modeManager != null) modeManager.IsInputAllowed = false;

        if (isRestarting)
        {
            if (topPanelMain != null) topPanelMain.anchoredPosition = topPanelStartPos;
            if (topExtraButton != null) topExtraButton.anchoredPosition = topExtraButtonStartPos;

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = bottomButtonsStartPos[i];
            }
            SetSlotsAlpha(1f);
        }
        else
        {
            if (topPanelMain != null) topPanelMain.anchoredPosition = topPanelHiddenPos;
            if (topExtraButton != null) topExtraButton.anchoredPosition = topExtraButtonHiddenPos;

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = bottomButtonsHiddenPos[i];
            }
            SetSlotsAlpha(0f);
        }
    }

    public IEnumerator PlayIntroSequence(bool isRestarting)
    {
        if (isRestarting) yield break;

        yield return StartCoroutine(FadeInSlots(slotsFadeDuration));

        // --- ИЗМЕНЕНИЕ: Звук панели передаем с флагом playSoundAtStart = true ---
        if (topPanelMain != null)
            StartCoroutine(AnimateUIElement(topPanelMain, topPanelHiddenPos, topPanelStartPos, uiSlideDuration, "Panel_Slide_In", true));

        if (topExtraButton != null)
            StartCoroutine(AnimateUIElement(topExtraButton, topExtraButtonHiddenPos, topExtraButtonStartPos, uiSlideDuration, "Panel_Slide_In", true));

        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
            {
                // --- ИЗМЕНЕНИЕ: Звук кнопок играем В КОНЦЕ движения (playSoundAtStart = false) ---
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
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            yield return null;
        }
    }

    // --- ИЗМЕНЕНИЕ: Добавлен параметр bool playSoundAtStart ---
    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration, string soundName, bool playSoundAtStart)
    {
        // Звук выезда панели логично играть СРАЗУ
        if (playSoundAtStart && AudioManager.Instance != null && !string.IsNullOrEmpty(soundName))
        {
            AudioManager.Instance.PlaySound(soundName);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);

            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, easedT);
            yield return null;
        }

        if (target != null) target.anchoredPosition = to;

        // Звук падения/удара кнопок играем В КОНЦЕ
        if (!playSoundAtStart && AudioManager.Instance != null && !string.IsNullOrEmpty(soundName))
        {
            AudioManager.Instance.PlaySound(soundName);
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
        List<CanvasGroup> groups = new List<CanvasGroup>();

        if (modeManager != null && modeManager.pileManager != null)
        {
            foreach (var slot in modeManager.pileManager.Slots)
            {
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg != null) groups.Add(cg);
            }
        }

        while (elapsed < duration)
        {
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);

            foreach (var cg in groups) if (cg != null) cg.alpha = t;
            yield return null;
        }
        foreach (var cg in groups) if (cg != null) cg.alpha = 1f;
    }

    public List<RectTransform> GetTopUIElements()
    {
        List<RectTransform> list = new List<RectTransform>();
        if (topPanelMain != null) list.Add(topPanelMain);
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