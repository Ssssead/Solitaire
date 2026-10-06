using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpiderIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public SpiderModeManager modeManager;

    // --- ИЗМЕНЕНИЕ 1: Две панели вместо одной ---
    public RectTransform landscapeTopPanel;
    public RectTransform portraitTopPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.5f;
    public float slotsFadeDuration = 0.8f;
    public float deckFlyDuration = 1.0f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    // --- ИЗМЕНЕНИЕ 2: Двойные позиции ---
    private Vector2 landscapeTopStartPos, landscapeTopHiddenPos;
    private Vector2 portraitTopStartPos, portraitTopHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private bool isSkipping = false;
    public bool forceInstantSkip = false; // Полезно при повороте экрана во время интро

    // Обновляем методы интерфейса
    public List<RectTransform> GetTopUIElements()
    {
        var list = new List<RectTransform>();
        if (landscapeTopPanel != null) list.Add(landscapeTopPanel);
        if (portraitTopPanel != null) list.Add(portraitTopPanel);
        return list;
    }

    public List<RectTransform> GetBottomUIElements() => bottomButtons;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<SpiderModeManager>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            isSkipping = true;
        }
    }

    public void SetupIntro(bool skipUI)
    {
        isSkipping = false;
        forceInstantSkip = false;
        Canvas.ForceUpdateCanvases();

        if (buttonsStartPos.Count == 0)
        {
            SaveInitialPositions();
        }

        if (!skipUI)
        {
            // Прячем ОБЕ панели
            if (landscapeTopPanel != null) landscapeTopPanel.anchoredPosition = landscapeTopHiddenPos;
            if (portraitTopPanel != null) portraitTopPanel.anchoredPosition = portraitTopHiddenPos;

            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];

            SetSlotsAlpha(0f);
        }
    }

    private void SaveInitialPositions()
    {
        // Сохраняем позиции для ОБЕИХ панелей
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

    public void UpdateSavedPositions()
    {
        // При повороте экрана отменяем анимации
        forceInstantSkip = true;
        isSkipping = true;
        SaveInitialPositions();
    }

    public IEnumerator PlayIntro(bool skipUI)
    {
        if (!skipUI)
        {
            yield return StartCoroutine(SkippableWait(startDelay));

            StartCoroutine(FadeInSlots(slotsFadeDuration));

            // Анимируем ОБЕ панели
            if (landscapeTopPanel != null || portraitTopPanel != null)
            {
                if (AudioManager.Instance != null && !forceInstantSkip)
                    AudioManager.Instance.PlaySound("Panel_Slide_In");

                if (landscapeTopPanel != null)
                    StartCoroutine(AnimateUIElement(landscapeTopPanel, landscapeTopHiddenPos, landscapeTopStartPos, uiSlideDuration));

                if (portraitTopPanel != null)
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

            yield return StartCoroutine(SkippableWait(uiSlideDuration));
        }
        else
        {
            SetSlotsAlpha(1f);
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
            if (forceInstantSkip) break;
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
            if (forceInstantSkip) break;
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, curve.Evaluate(t));
            yield return null;
        }

        if (target != null && !forceInstantSkip) target.anchoredPosition = to;

        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound("UI_Drop");
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break;
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            SetSlotsAlpha(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        SetSlotsAlpha(1f);
    }

    private void SetSlotsAlpha(float alpha)
    {
        if (modeManager == null || modeManager.pileManager == null) return;

        foreach (var t in modeManager.pileManager.TableauPiles)
        {
            if (t != null)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
        }
        foreach (var f in modeManager.pileManager.FoundationPiles)
        {
            if (f != null)
            {
                var cg = f.GetComponent<CanvasGroup>();
                if (cg == null) cg = f.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
        }

        if (modeManager.pileManager.StockPile != null)
        {
            var cg = modeManager.pileManager.StockPile.GetComponent<CanvasGroup>();
            if (cg == null) cg = modeManager.pileManager.StockPile.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = alpha;
        }
    }
}