using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FreeCellIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public FreeCellModeManager modeManager;
    public RectTransform topPanel;
    public List<RectTransform> topRightElements;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.3f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.05f;
    public float slotsFadeDuration = 0.5f;

    private Vector2 topPanelStartPos;
    private List<Vector2> topRightStartPos = new List<Vector2>();
    private List<Vector2> bottomButtonsStartPos = new List<Vector2>();

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<FreeCellModeManager>();
        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();
        PrepareIntro(false);
    }

    private void SaveInitialPositions()
    {
        if (topPanel != null) topPanelStartPos = topPanel.anchoredPosition;
        topRightStartPos.Clear();
        foreach (var el in topRightElements) if (el != null) topRightStartPos.Add(el.anchoredPosition);
        bottomButtonsStartPos.Clear();
        foreach (var btn in bottomButtons) if (btn != null) bottomButtonsStartPos.Add(btn.anchoredPosition);
    }

    public List<RectTransform> GetTopUIElements()
    {
        var list = new List<RectTransform>();
        if (topPanel != null) list.Add(topPanel);
        if (topRightElements != null) list.AddRange(topRightElements);
        return list;
    }
    public List<RectTransform> GetBottomUIElements()
    {
        return bottomButtons != null ? new List<RectTransform>(bottomButtons) : new List<RectTransform>();
    }

    public void PrepareIntro(bool isRestart)
    {
        // ИСПРАВЛЕНИЕ: Прячем слоты и уводим UI ТОЛЬКО если это не рестарт!
        if (!isRestart)
        {
            SetSlotsAlpha(0f);

            if (topPanel != null) topPanel.anchoredPosition = new Vector2(topPanelStartPos.x, topPanelStartPos.y + 300f);
            for (int i = 0; i < topRightElements.Count; i++)
                if (topRightElements[i] != null && i < topRightStartPos.Count)
                    topRightElements[i].anchoredPosition = new Vector2(topRightStartPos[i].x, topRightStartPos[i].y + 300f);
            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null && i < bottomButtonsStartPos.Count)
                    bottomButtons[i].anchoredPosition = new Vector2(bottomButtonsStartPos[i].x, bottomButtonsStartPos[i].y - 300f);
        }
    }

    public IEnumerator PlayIntroSequence(Deal deal, bool isRestart)
    {
        if (!isRestart)
        {
            yield return new WaitForSeconds(startDelay);

            if (topPanel != null) StartCoroutine(AnimateUIElement(topPanel, topPanel.anchoredPosition, topPanelStartPos, uiSlideDuration));
            for (int i = 0; i < topRightElements.Count; i++)
                if (topRightElements[i] != null && i < topRightStartPos.Count)
                    StartCoroutine(AnimateUIElement(topRightElements[i], topRightElements[i].anchoredPosition, topRightStartPos[i], uiSlideDuration));
            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null && i < bottomButtonsStartPos.Count)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], bottomButtons[i].anchoredPosition, bottomButtonsStartPos[i], uiSlideDuration));
                    yield return new WaitForSeconds(buttonStaggerDelay);
                }

            yield return StartCoroutine(FadeInSlots(slotsFadeDuration));
        }

        if (modeManager.deckManager != null)
            yield return StartCoroutine(modeManager.deckManager.PlayIntroDeal(deal));

        modeManager.IsInputAllowed = true;
    }

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        while (elapsed < duration) { elapsed += Time.deltaTime; if (target) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(elapsed / duration)); yield return null; }
        if (target) target.anchoredPosition = to;
    }

    private void SetSlotsAlpha(float alpha)
    {
        if (modeManager != null && modeManager.pileManager != null && modeManager.pileManager.GetAllContainers().Count > 0)
        {
            foreach (var container in modeManager.pileManager.GetAllContainers())
            {
                var cg = (container as MonoBehaviour).GetComponent<CanvasGroup>();
                if (cg == null) cg = (container as MonoBehaviour).gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
        }
        else
        {
            foreach (var mono in FindObjectsOfType<MonoBehaviour>())
            {
                if (mono is ICardContainer)
                {
                    var cg = mono.GetComponent<CanvasGroup>();
                    if (cg == null) cg = mono.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = alpha;
                }
            }
        }
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        List<CanvasGroup> groups = new List<CanvasGroup>();
        foreach (var c in modeManager.pileManager.GetAllContainers())
        {
            var cg = (c as MonoBehaviour).GetComponent<CanvasGroup>();
            if (cg != null) groups.Add(cg);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            foreach (var cg in groups) if (cg != null) cg.alpha = elapsed / duration;
            yield return null;
        }
        foreach (var cg in groups) if (cg != null) cg.alpha = 1f;
    }
}