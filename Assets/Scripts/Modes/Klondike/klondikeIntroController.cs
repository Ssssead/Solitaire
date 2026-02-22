using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public KlondikeModeManager modeManager;
    public RectTransform topPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.5f;
    public float slotsFadeDuration = 0.8f;
    public float deckFlyDuration = 1.2f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<KlondikeModeManager>();
        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();
        PrepareIntro(false);
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
        if (!isRestart)
        {
            modeManager.PileManager.SetAllSlotsAlpha(0f);
            if (topPanel != null) topPanel.anchoredPosition = topPanelHiddenPos;
            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
        }
    }

    public IEnumerator PlayIntroSequence(bool isRestart)
    {
        if (!isRestart)
        {
            yield return new WaitForSeconds(startDelay);
            yield return StartCoroutine(FadeInSlots(slotsFadeDuration));

            if (topPanel != null) StartCoroutine(AnimateUIElement(topPanel, topPanelHiddenPos, topPanelStartPos, uiSlideDuration));
            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                    yield return new WaitForSeconds(buttonStaggerDelay);
                }
        }

        if (modeManager.deckManager != null)
        {
            // Здесь запускаем полет колоды Клондайка
            yield return StartCoroutine(modeManager.deckManager.PlayIntroDeckArrival(deckFlyDuration));
        }
    }

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(elapsed / duration));
            yield return null;
        }
        if (target != null) target.anchoredPosition = to;
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            modeManager.PileManager.SetAllSlotsAlpha(elapsed / duration);
            yield return null;
        }
        modeManager.PileManager.SetAllSlotsAlpha(1f);
    }
}