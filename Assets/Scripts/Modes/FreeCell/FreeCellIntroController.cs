using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Запускаем скрипт раньше всех, чтобы спрятать элементы до того, как Unity их нарисует
[DefaultExecutionOrder(-50)]
public class FreeCellIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public FreeCellModeManager modeManager;

    public RectTransform landscapeTopPanel;
    public RectTransform portraitTopPanel;

    public List<RectTransform> topRightElements;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.3f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.05f;
    public float slotsFadeDuration = 0.5f;

    private Vector2 landscapeTopStartPos, landscapeTopHiddenPos;
    private Vector2 portraitTopStartPos, portraitTopHiddenPos;

    private List<Vector2> topRightStartPos = new List<Vector2>();
    private List<Vector2> bottomButtonsStartPos = new List<Vector2>();

    private bool positionsSaved = false;
    private bool isSkipping = false;
    public bool forceInstantSkip = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<FreeCellModeManager>();

        // 1. Мгновенно скрываем UI элементы через прозрачность
        SetUIVisible(false);

        // 2. ЖЕСТКО скрываем все слоты в первый же кадр (защита от мигания)
        HideAllSlotsImmediately();
    }

    private void Update()
    {
        // Защита от случайного скипа при прокликивании меню
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
        // Ищем все контейнеры на сцене и отключаем им альфу до начала игры
        foreach (var mono in FindObjectsOfType<MonoBehaviour>(true))
        {
            if (mono is ICardContainer)
            {
                var cg = mono.GetComponent<CanvasGroup>();
                if (cg == null) cg = mono.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
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

        topRightStartPos.Clear();
        foreach (var el in topRightElements) if (el != null) topRightStartPos.Add(el.anchoredPosition);

        bottomButtonsStartPos.Clear();
        foreach (var btn in bottomButtons) if (btn != null) bottomButtonsStartPos.Add(btn.anchoredPosition);

        positionsSaved = true;
    }

    public void UpdateSavedPositions()
    {
        // При повороте экрана просто пропускаем анимации. 
        // Позиции пересохранять не нужно, так как отступы якорей (AnchoredPosition) остаются верными!
        forceInstantSkip = true;
        isSkipping = true;
    }

    public List<RectTransform> GetTopUIElements()
    {
        var list = new List<RectTransform>();
        if (landscapeTopPanel != null) list.Add(landscapeTopPanel);
        if (portraitTopPanel != null) list.Add(portraitTopPanel);
        if (topRightElements != null) list.AddRange(topRightElements);
        return list;
    }

    public List<RectTransform> GetBottomUIElements()
    {
        return bottomButtons != null ? new List<RectTransform>(bottomButtons) : new List<RectTransform>();
    }

    public void PrepareIntro(bool isRestart)
    {
        if (!positionsSaved) SaveInitialPositions();

        if (!isRestart)
        {
            SetSlotsAlpha(0f);
            SetUIVisible(false);

            if (landscapeTopPanel != null) landscapeTopPanel.anchoredPosition = landscapeTopHiddenPos;
            if (portraitTopPanel != null) portraitTopPanel.anchoredPosition = portraitTopHiddenPos;

            for (int i = 0; i < topRightElements.Count; i++)
                if (topRightElements[i] != null && i < topRightStartPos.Count)
                    topRightElements[i].anchoredPosition = new Vector2(topRightStartPos[i].x, topRightStartPos[i].y + 300f);

            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null && i < bottomButtonsStartPos.Count)
                    bottomButtons[i].anchoredPosition = new Vector2(bottomButtonsStartPos[i].x, bottomButtonsStartPos[i].y - 300f);
        }
        else
        {
            SetUIVisible(true);
            SetSlotsAlpha(1f);
        }
    }

    public IEnumerator PlayIntroSequence(Deal deal, bool isRestart)
    {
        // --- ГЛАВНЫЙ ФИКС: Сбрасываем флаги прямо перед самым стартом анимации ---
        isSkipping = false;
        forceInstantSkip = false;

        if (!isRestart)
        {
            yield return StartCoroutine(SkippableWait(startDelay));

            if (AudioManager.Instance != null && !forceInstantSkip)
                AudioManager.Instance.PlaySound("Panel_Slide_In");

            if (landscapeTopPanel != null) StartCoroutine(AnimateUIElement(landscapeTopPanel, landscapeTopHiddenPos, landscapeTopStartPos, uiSlideDuration));
            if (portraitTopPanel != null) StartCoroutine(AnimateUIElement(portraitTopPanel, portraitTopHiddenPos, portraitTopStartPos, uiSlideDuration));

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

        if (modeManager.deckManager != null && deal != null)
        {
            yield return StartCoroutine(modeManager.deckManager.PlayIntroDeal(deal));
        }

        modeManager.IsInputAllowed = true;
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

            if (target) target.anchoredPosition = Vector2.LerpUnclamped(from, to, curve.Evaluate(t));
            if (cg) cg.alpha = Mathf.Lerp(0f, 1f, curve.Evaluate(t));

            yield return null;
        }

        // Гарантированно выставляем финальные значения, даже если анимация была пропущена
        if (target) target.anchoredPosition = to;
        if (cg) cg.alpha = 1f;

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

    private void SetSlotsAlpha(float alpha)
    {
        if (modeManager != null && modeManager.pileManager != null)
        {
            foreach (var container in modeManager.pileManager.GetAllContainers())
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
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        List<CanvasGroup> groups = new List<CanvasGroup>();
        if (modeManager.pileManager != null)
        {
            foreach (var c in modeManager.pileManager.GetAllContainers())
            {
                var mono = c as MonoBehaviour;
                if (mono != null)
                {
                    var cg = mono.GetComponent<CanvasGroup>();
                    if (cg != null) groups.Add(cg);
                }
            }
        }

        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);

            foreach (var cg in groups) if (cg != null) cg.alpha = t;
            yield return null;
        }
        foreach (var cg in groups) if (cg != null) cg.alpha = 1f;
    }
}