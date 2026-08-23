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

    // Флаг пропуска анимации
    private bool isSkipping = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<FreeCellModeManager>();
        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();
        PrepareIntro(false);
    }

    // --- ДОБАВЛЕНО: Отслеживаем клик для ускорения ---
    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            isSkipping = true;
        }
    }
    // -------------------------------------------------

    private void SaveInitialPositions()
    {
        if (topPanel != null) topPanelStartPos = topPanel.anchoredPosition;
        topRightStartPos.Clear();
        foreach (var el in topRightElements) if (el != null) topRightStartPos.Add(el.anchoredPosition);
        bottomButtonsStartPos.Clear();
        foreach (var btn in bottomButtons) if (btn != null) bottomButtonsStartPos.Add(btn.anchoredPosition);
    }
    public void UpdateSavedPositions()
    {
        SaveInitialPositions();
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
        isSkipping = false; // Сброс флага при подготовке

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
            yield return StartCoroutine(SkippableWait(startDelay));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Panel_Slide_In");

            if (topPanel != null) StartCoroutine(AnimateUIElement(topPanel, topPanel.anchoredPosition, topPanelStartPos, uiSlideDuration));
            for (int i = 0; i < topRightElements.Count; i++)
                if (topRightElements[i] != null && i < topRightStartPos.Count)
                    StartCoroutine(AnimateUIElement(topRightElements[i], topRightElements[i].anchoredPosition, topRightStartPos[i], uiSlideDuration));
            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null && i < bottomButtonsStartPos.Count)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], bottomButtons[i].anchoredPosition, bottomButtonsStartPos[i], uiSlideDuration));
                    yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
                }

            yield return StartCoroutine(FadeInSlots(slotsFadeDuration));
        }

        // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
        // Запускаем раздачу ТОЛЬКО если deal существует.
        if (modeManager.deckManager != null && deal != null)
        {
            yield return StartCoroutine(modeManager.deckManager.PlayIntroDeal(deal));
        }

        modeManager.IsInputAllowed = true;
    }

    // --- ДОБАВЛЕНО: Кастомный таймер для пропуска ---
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
    // ------------------------------------------------

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        while (elapsed < duration)
        {
            // Добавлено ускорение
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);

            if (target) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(t));
            yield return null;
        }
        if (target) target.anchoredPosition = to;

        // <--- ЗВУК: ЭЛЕМЕНТ ИНТЕРФЕЙСА ПРИЗЕМЛИЛСЯ --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Drop");
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
            // Добавлено ускорение
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);

            foreach (var cg in groups) if (cg != null) cg.alpha = t;
            yield return null;
        }
        foreach (var cg in groups) if (cg != null) cg.alpha = 1f;
    }
}