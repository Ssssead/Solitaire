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

    // --- НОВОЕ: Флаг пропуска ---
    private bool isSkipping = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<SultanModeManager>();

        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();

        if (topPanel != null) topPanel.anchoredPosition = topPanelStartPos + new Vector2(0, 300f);
        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
                bottomButtons[i].anchoredPosition = bottomButtonsStartPos[i] + new Vector2(0, -300f);
        }
    }

    // --- НОВОЕ: Отслеживаем клик для пропуска ---
    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            isSkipping = true;
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

    public void PrepareIntro(bool isRestart)
    {
        isSkipping = false; // Сбрасываем флаг

        if (!isRestart)
        {
            SetSlotsAlpha(0f);

            if (topPanel != null) topPanel.anchoredPosition = topPanelStartPos + new Vector2(0, 300f);
            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null)
                    bottomButtons[i].anchoredPosition = bottomButtonsStartPos[i] + new Vector2(0, -300f);
            }
        }
    }

    public IEnumerator AnimateUIAndSlots(bool isRestart)
    {
        if (!isRestart)
        {
            StartCoroutine(FadeInSlots(slotsFadeDuration));

            if (topPanel != null)
                StartCoroutine(AnimateUIElement(topPanel, topPanel.anchoredPosition, topPanelStartPos, uiSlideDuration));

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], bottomButtons[i].anchoredPosition, bottomButtonsStartPos[i], uiSlideDuration));
                    yield return StartCoroutine(SkippableWait(0.05f)); // ИСПОЛЬЗУЕМ SKIPPABLE WAIT
                }
            }

            yield return StartCoroutine(SkippableWait(Mathf.Max(0f, slotsFadeDuration - (bottomButtons.Count * 0.05f))));
        }
        else
        {
            yield return null;
        }
    }

    // --- НОВОЕ: Настраиваемое ожидание ---
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
            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(elapsed / duration));
            yield return null;
        }
        if (target != null) target.anchoredPosition = to;

        // --- НОВОЕ: Звук приземления панели ---
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Drop");
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
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
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