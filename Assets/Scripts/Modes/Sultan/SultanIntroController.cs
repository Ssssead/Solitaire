using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SultanIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public SultanModeManager modeManager;
    public RectTransform topPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float uiSlideDuration = 0.5f;
    public float slotsFadeDuration = 0.5f;

    private Vector2 topPanelStartPos;
    private List<Vector2> bottomButtonsStartPos = new List<Vector2>();

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<SultanModeManager>();

        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();

        // Предварительно прячем UI, но слоты спрячем позже, когда они сгенерируются
        if (topPanel != null) topPanel.anchoredPosition = topPanelStartPos + new Vector2(0, 300f);
        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
                bottomButtons[i].anchoredPosition = bottomButtonsStartPos[i] + new Vector2(0, -300f);
        }
    }

    private void SaveInitialPositions()
    {
        if (topPanel != null) topPanelStartPos = topPanel.anchoredPosition;

        bottomButtonsStartPos.Clear();
        foreach (var btn in bottomButtons)
        {
            if (btn != null) bottomButtonsStartPos.Add(btn.anchoredPosition);
        }
    }

    // Вызывается из DeckManager, когда слоты уже точно существуют
    public void PrepareIntro()
    {
        SetSlotsAlpha(0f);
    }

    public IEnumerator AnimateUIAndSlots()
    {
        // 1. Проявляем слоты на столе
        StartCoroutine(FadeInSlots(slotsFadeDuration));

        // 2. Выдвигаем верхнюю панель
        if (topPanel != null)
            StartCoroutine(AnimateUIElement(topPanel, topPanel.anchoredPosition, topPanelStartPos, uiSlideDuration));

        // 3. Выдвигаем нижние кнопки
        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
            {
                StartCoroutine(AnimateUIElement(bottomButtons[i], bottomButtons[i].anchoredPosition, bottomButtonsStartPos[i], uiSlideDuration));
                yield return new WaitForSeconds(0.05f);
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0f, slotsFadeDuration - (bottomButtons.Count * 0.05f)));
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

    private void SetSlotsAlpha(float alpha)
    {
        if (modeManager?.pileManager != null)
        {
            foreach (var container in modeManager.pileManager.GetAllContainers())
            {
                var cg = (container as MonoBehaviour).GetComponent<CanvasGroup>();
                if (cg == null) cg = (container as MonoBehaviour).gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
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

    public List<RectTransform> GetTopUIElements()
    {
        List<RectTransform> list = new List<RectTransform>();
        if (topPanel != null) list.Add(topPanel);
        return list;
    }

    public List<RectTransform> GetBottomUIElements() => bottomButtons;
}