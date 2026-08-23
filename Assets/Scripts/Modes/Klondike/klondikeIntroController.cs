using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public KlondikeModeManager modeManager;
    public RectTransform landscapeTopPanel;
    public RectTransform portraitTopPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.5f;
    public float slotsFadeDuration = 0.8f;
    public float deckFlyDuration = 1.2f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    [Header("Hide Distances")]
    public float topHideOffset = 1500f;
    public float bottomHideOffset = -1500f;

    private Vector2 landscapeTopStartPos, landscapeTopHiddenPos;
    private Vector2 portraitTopStartPos, portraitTopHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private bool isSkipping = false;
    public bool forceInstantSkip = false; // [NEW] Экстренный пропуск
    private bool isHidden = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<KlondikeModeManager>();
        SaveInitialPositions();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            isSkipping = true;
        }
    }

    // --- Реализация интерфейса IIntroController ---
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

    public void UpdateSavedPositions()
    {
        // [NEW] GameLayoutManager вызывает этот метод при повороте экрана.
        // Заставляем интро мгновенно закончиться.
        forceInstantSkip = true;
        isSkipping = true;

        if (isHidden)
        {
            foreach (var btn in bottomButtons)
            {
                if (btn != null) { btn.anchoredPosition = Vector2.zero; btn.offsetMin = Vector2.zero; btn.offsetMax = Vector2.zero; }
            }
            Canvas.ForceUpdateCanvases();
        }

        buttonsStartPos.Clear();
        buttonsHiddenPos.Clear();
        foreach (var btn in bottomButtons)
        {
            if (btn != null)
            {
                buttonsStartPos.Add(btn.anchoredPosition);
                buttonsHiddenPos.Add(btn.anchoredPosition + new Vector2(0, bottomHideOffset));
            }
        }

        if (isHidden)
        {
            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
            }
        }
    }
    // ----------------------------------------------

    private void SaveInitialPositions()
    {
        if (landscapeTopPanel != null)
        {
            landscapeTopStartPos = landscapeTopPanel.anchoredPosition;
            landscapeTopHiddenPos = landscapeTopStartPos + new Vector2(0, topHideOffset);
        }
        if (portraitTopPanel != null)
        {
            portraitTopStartPos = portraitTopPanel.anchoredPosition;
            portraitTopHiddenPos = portraitTopStartPos + new Vector2(0, topHideOffset);
        }

        buttonsStartPos.Clear();
        buttonsHiddenPos.Clear();
        foreach (var btn in bottomButtons)
        {
            if (btn != null)
            {
                buttonsStartPos.Add(btn.anchoredPosition);
                buttonsHiddenPos.Add(btn.anchoredPosition + new Vector2(0, bottomHideOffset));
            }
        }
    }

    public void PrepareIntro(bool isRestart)
    {
        isSkipping = false;
        forceInstantSkip = false;

        if (!isRestart)
        {
            isHidden = true;
            SetSlotsAlpha(0f);

            if (landscapeTopPanel != null) landscapeTopPanel.anchoredPosition = landscapeTopHiddenPos;
            if (portraitTopPanel != null) portraitTopPanel.anchoredPosition = portraitTopHiddenPos;

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
            }
        }
    }

    public IEnumerator PlayIntroSequence(bool isRestart)
    {
        if (!isRestart)
        {
            isSkipping = false;

            yield return StartCoroutine(SkippableWait(startDelay));
            yield return StartCoroutine(FadeInSlots(slotsFadeDuration));

            if (landscapeTopPanel != null)
            {
                StartCoroutine(AnimateUIElement(landscapeTopPanel, landscapeTopHiddenPos, landscapeTopStartPos, uiSlideDuration));
            }
            if (portraitTopPanel != null)
            {
                StartCoroutine(AnimateUIElement(portraitTopPanel, portraitTopHiddenPos, portraitTopStartPos, uiSlideDuration));
            }

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                    yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
                }
            }

            isHidden = false;
        }

        if (modeManager != null && modeManager.deckManager != null)
        {
            yield return StartCoroutine(modeManager.deckManager.PlayIntroDeckArrival(deckFlyDuration));
        }
    }

    private IEnumerator SkippableWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break; // [NEW] Прерываем паузу
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
            if (forceInstantSkip) break; // [NEW] Прерываем полет UI
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);

            if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, curve.Evaluate(t));
            yield return null;
        }

        // Если анимация прервана поворотом, GameLayoutManager сам выставит UI куда нужно.
        // Поэтому финальную позицию применяем, ТОЛЬКО если не было экстренного пропуска.
        if (target != null && !forceInstantSkip) target.anchoredPosition = to;

        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound("UI_Drop");
    }

    private void SetSlotsAlpha(float alpha)
    {
        if (GameLayoutManager.Instance == null) return;

        foreach (var slotElement in GameLayoutManager.Instance.slots)
        {
            if (slotElement.targetSlot != null)
            {
                CanvasGroup cg = slotElement.targetSlot.GetComponent<CanvasGroup>();
                if (cg == null) cg = slotElement.targetSlot.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
        }
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break; // [NEW] Прерываем затухание
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);

            SetSlotsAlpha(t);
            yield return null;
        }

        SetSlotsAlpha(1f);
    }
}