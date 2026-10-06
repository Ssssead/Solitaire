using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)] // Выполняется до загрузки макета, чтобы предотвратить моргание
public class MonteCarloIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public MonteCarloModeManager modeManager;

    // --- ИЗМЕНЕНИЕ 1: Двойные панели ---
    public RectTransform landscapeTopPanel;
    public RectTransform portraitTopPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.3f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.05f;
    public float slotsFadeDuration = 0.4f;

    [Space]
    [Tooltip("Сколько времени вся колода вылетает из-за экрана")]
    public float deckFlyDuration = 0.8f;
    [Tooltip("Скорость полета одной карты от колоды до слота")]
    public float cardFlyDuration = 0.15f;
    [Tooltip("Задержка между 'выстрелами' пулемета")]
    public float dealDelay = 0.04f;

    private Vector2 landscapeTopStartPos, landscapeTopHiddenPos;
    private Vector2 portraitTopStartPos, portraitTopHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private bool isSkipping = false;
    public bool forceInstantSkip = false;
    private bool positionsSaved = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<MonteCarloModeManager>();

        SetUIVisible(false);
        if (modeManager != null && modeManager.pileManager != null)
        {
            modeManager.pileManager.SetAllSlotsAlpha(0f);
        }
    }

    private void Update()
    {
        if (Time.timeSinceLevelLoad > 0.3f)
        {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                isSkipping = true;
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
        positionsSaved = true;
    }

    public void UpdateSavedPositions()
    {
        forceInstantSkip = true;
        isSkipping = true;
    }

    public List<RectTransform> GetTopUIElements()
    {
        List<RectTransform> list = new List<RectTransform>();
        if (landscapeTopPanel != null) list.Add(landscapeTopPanel);
        if (portraitTopPanel != null) list.Add(portraitTopPanel);
        return list;
    }

    public List<RectTransform> GetBottomUIElements()
    {
        return new List<RectTransform>(bottomButtons);
    }

    public void PrepareIntro(bool isRestart)
    {
        isSkipping = false;
        forceInstantSkip = false;

        if (!positionsSaved) SaveInitialPositions();

        if (!isRestart)
        {
            SetUIVisible(false);
            if (landscapeTopPanel != null) landscapeTopPanel.anchoredPosition = landscapeTopHiddenPos;
            if (portraitTopPanel != null) portraitTopPanel.anchoredPosition = portraitTopHiddenPos;

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null && i < buttonsHiddenPos.Count)
                    bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
            }
            modeManager.pileManager.SetAllSlotsAlpha(0f);
        }
        else
        {
            SetUIVisible(true);
            modeManager.pileManager.SetAllSlotsAlpha(1f);
        }
    }

    public void InstantHideCardsOffscreen()
    {
        List<CardController> fullDeck = new List<CardController>();
        fullDeck.AddRange(modeManager.pileManager.StockCards);

        for (int i = 0; i < 25; i++)
        {
            if (modeManager.pileManager.BoardCards[i] != null)
                fullDeck.Add(modeManager.pileManager.BoardCards[i]);
        }

        Vector2 offset = modeManager.deckManager.stockCardOffset;

        for (int i = 0; i < fullDeck.Count; i++)
        {
            var c = fullDeck[i];
            c.transform.SetParent(modeManager.pileManager.StockRoot, true);
            c.transform.SetAsLastSibling();
            c.transform.localPosition = new Vector3(-1500f + (offset.x * i), offset.y * i, 0f);
        }
    }

    public IEnumerator PlayIntroSequence(bool isRestart)
    {
        isSkipping = false;
        forceInstantSkip = false;

        if (!isRestart)
        {
            yield return StartCoroutine(SkippableWait(startDelay));

            StartCoroutine(FadeInSlots(slotsFadeDuration));

            if (AudioManager.Instance != null && !forceInstantSkip)
                AudioManager.Instance.PlaySound("Panel_Slide_In");

            if (landscapeTopPanel != null) StartCoroutine(AnimateUIElement(landscapeTopPanel, landscapeTopHiddenPos, landscapeTopStartPos, uiSlideDuration));
            if (portraitTopPanel != null) StartCoroutine(AnimateUIElement(portraitTopPanel, portraitTopHiddenPos, portraitTopStartPos, uiSlideDuration));

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null && i < buttonsStartPos.Count)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                    yield return StartCoroutine(SkippableWait(buttonStaggerDelay));
                }
            }
        }
        else
        {
            yield return StartCoroutine(SkippableWait(0.2f));
        }

        List<CardController> fullDeck = new List<CardController>();
        fullDeck.AddRange(modeManager.pileManager.StockCards);

        for (int i = 0; i < 25; i++)
        {
            if (modeManager.pileManager.BoardCards[i] != null)
                fullDeck.Add(modeManager.pileManager.BoardCards[i]);
        }

        Vector2 offset = modeManager.deckManager.stockCardOffset;

        for (int i = 0; i < fullDeck.Count; i++)
        {
            var c = fullDeck[i];
            Vector3 targetLocal = new Vector3(offset.x * i, offset.y * i, 0f);
            Vector3 targetWorld = modeManager.pileManager.StockRoot.TransformPoint(targetLocal);

            StartCoroutine(modeManager.animationService.AnimateCardLinear(c, targetWorld, GetActualDuration(deckFlyDuration)));
        }

        yield return StartCoroutine(SkippableWait(deckFlyDuration + 0.1f));

        if ((isSkipping || forceInstantSkip) && modeManager.animationService != null)
        {
            modeManager.animationService.StopAllCoroutines();
        }

        Coroutine lastRoutine = null;

        for (int i = 24; i >= 0; i--)
        {
            CardController c = modeManager.pileManager.BoardCards[i];
            if (c == null) continue;

            if (modeManager.animationService.dragLayer != null)
                c.transform.SetParent(modeManager.animationService.dragLayer, true);

            c.transform.SetAsLastSibling();
            lastRoutine = StartCoroutine(FlyAndDock(c, modeManager.pileManager.TableauSlots[i], cardFlyDuration));

            yield return StartCoroutine(SkippableWait(dealDelay));
        }

        if (lastRoutine != null) yield return lastRoutine;
    }

    private IEnumerator FlyAndDock(CardController card, Transform slot, float duration)
    {
        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound("Card_Deal");

        yield return StartCoroutine(modeManager.animationService.AnimateCardLinear(card, slot.position, GetActualDuration(duration)));

        card.transform.SetParent(slot, true);
        card.transform.localPosition = Vector3.zero;
        modeManager.animationService.SetShadowFlying(card, false);

        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound("Card_Drop_Success");
    }

    private float GetActualDuration(float baseDuration)
    {
        if (forceInstantSkip) return 0f;
        return isSkipping ? baseDuration / 15f : baseDuration;
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

            if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, curve.Evaluate(t));
            if (cg != null) cg.alpha = Mathf.Lerp(0f, 1f, curve.Evaluate(t));
            yield return null;
        }

        if (target != null && !forceInstantSkip) target.anchoredPosition = to;
        if (cg != null && !forceInstantSkip) cg.alpha = 1f;

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
            float t = Mathf.Clamp01(elapsed / duration);
            modeManager.pileManager.SetAllSlotsAlpha(t);
            yield return null;
        }
        modeManager.pileManager.SetAllSlotsAlpha(1f);
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
}