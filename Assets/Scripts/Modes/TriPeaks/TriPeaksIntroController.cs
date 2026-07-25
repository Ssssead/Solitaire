using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TriPeaksIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public TriPeaksModeManager modeManager; // Ссылка на главный менеджер
    public RectTransform topPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.2f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;
    public float slotFadeDuration = 0.5f; // Длительность проявления слота

    public bool IsSkipping { get; private set; }

    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private void Awake()
    {
        // Попробуем автоматически найти менеджер, если не назначен в инспекторе
        if (modeManager == null) modeManager = FindObjectOfType<TriPeaksModeManager>();

        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();

        PrepareIntro(true);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            IsSkipping = true;
        }
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

    public void PrepareIntro(bool playIntro)
    {
        IsSkipping = false;

        if (playIntro)
        {
            // Прячем слот Waste
            if (modeManager != null && modeManager.pileManager != null)
            {
                modeManager.pileManager.SetWasteSlotAlpha(0f);
            }

            if (topPanel != null) topPanel.anchoredPosition = topPanelHiddenPos;
            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
            }
        }
        else
        {
            // Показываем слот Waste мгновенно (для рестартов без интро)
            if (modeManager != null && modeManager.pileManager != null)
            {
                modeManager.pileManager.SetWasteSlotAlpha(1f);
            }

            if (topPanel != null) topPanel.anchoredPosition = topPanelStartPos;
            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsStartPos[i];
            }
        }
    }

    public IEnumerator PlayUIIntroSequence()
    {
        yield return StartCoroutine(SkippableWait(startDelay));

        // <--- ЗВУК: НАЧАЛО ДВИЖЕНИЯ ИНТЕРФЕЙСА --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Panel_Slide_In");

        // Запускаем проявление слота Waste ПАРАЛЛЕЛЬНО с выездом UI
        StartCoroutine(FadeInWasteSlot(slotFadeDuration));

        if (topPanel != null)
            StartCoroutine(AnimateUIElement(topPanel, topPanelHiddenPos, topPanelStartPos, uiSlideDuration));

        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
            {
                StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
            }
        }
    }

    // НОВАЯ КОРУТИНА: Плавное проявление слота
    private IEnumerator FadeInWasteSlot(float duration)
    {
        if (modeManager == null || modeManager.pileManager == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
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
            float speed = IsSkipping ? 15f : 1f;
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
            float speed = IsSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(t));
            yield return null;
        }
        if (target != null) target.anchoredPosition = to;

        // <--- ЗВУК: ЭЛЕМЕНТ UI ПРИЗЕМЛИЛСЯ --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Drop");
    }
}