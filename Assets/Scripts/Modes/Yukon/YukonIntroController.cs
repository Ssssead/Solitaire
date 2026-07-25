using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class YukonIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public YukonModeManager modeManager;
    public RectTransform topPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.5f;
    public float slotsFadeDuration = 0.8f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;

    // Переменные для кнопки авто-сбора
    private RectTransform autoCollectBtn;
    private Vector2 autoCollectStartPos;
    private Vector2 autoCollectHiddenPos;

    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private bool isSkipping = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<YukonModeManager>();

        // Подхватываем кнопку из ModeManager
        if (modeManager != null && modeManager.autoWinButton != null)
        {
            autoCollectBtn = modeManager.autoWinButton.GetComponent<RectTransform>();
        }

        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();
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

        if (autoCollectBtn != null)
        {
            autoCollectStartPos = autoCollectBtn.anchoredPosition;
            autoCollectHiddenPos = autoCollectStartPos + new Vector2(0, 300f); // Прячем наверх
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
        if (!isRestart)
        {
            var pileManager = modeManager.GetComponent<PileManager>();
            if (pileManager) pileManager.SetAllSlotsAlpha(0f);

            if (topPanel != null) topPanel.anchoredPosition = topPanelHiddenPos;
            if (autoCollectBtn != null) autoCollectBtn.anchoredPosition = autoCollectHiddenPos;

            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
        }
    }

    public IEnumerator PlayIntroSequence(bool isRestart, Deal deal)
    {
        if (!isRestart)
        {
            yield return StartCoroutine(SkippableWait(startDelay));
            yield return StartCoroutine(FadeInSlots(slotsFadeDuration));
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Panel_Slide_In");
            // Анимируем панель и кнопку одновременно
            if (topPanel != null) StartCoroutine(AnimateUIElement(topPanel, topPanelHiddenPos, topPanelStartPos, uiSlideDuration));
            if (autoCollectBtn != null) StartCoroutine(AnimateUIElement(autoCollectBtn, autoCollectHiddenPos, autoCollectStartPos, uiSlideDuration));

            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                    yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
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
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Drop");
    }

    private IEnumerator FadeInSlots(float duration)
    {
        var pileManager = modeManager.GetComponent<PileManager>();
        if (pileManager == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            pileManager.SetAllSlotsAlpha(t);
            yield return null;
        }
        pileManager.SetAllSlotsAlpha(1f);
    }
}