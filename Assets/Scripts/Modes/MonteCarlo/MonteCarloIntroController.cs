using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonteCarloIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public MonteCarloModeManager modeManager;
    public RectTransform topPanel;
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

    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private bool isSkipping = false;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<MonteCarloModeManager>();
        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();
        PrepareIntro(false);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            isSkipping = true;
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
        List<RectTransform> list = new List<RectTransform>();
        if (topPanel != null) list.Add(topPanel);
        return list;
    }

    public List<RectTransform> GetBottomUIElements()
    {
        return new List<RectTransform>(bottomButtons);
    }

    public void PrepareIntro(bool isRestart)
    {
        if (!isRestart)
        {
            if (topPanel != null) topPanel.anchoredPosition = topPanelHiddenPos;
            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];
            }
            modeManager.pileManager.SetAllSlotsAlpha(0f);
        }
        else
        {
            modeManager.pileManager.SetAllSlotsAlpha(1f);
        }
    }

    // НОВЫЙ МЕТОД: Прячет карты за экран в тот же кадр, чтобы избежать мелькания
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

        if (!isRestart)
        {
            yield return StartCoroutine(SkippableWait(startDelay));

            StartCoroutine(FadeInSlots(slotsFadeDuration));
            if (topPanel != null) StartCoroutine(AnimateUIElement(topPanel, topPanelHiddenPos, topPanelStartPos, uiSlideDuration));

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null)
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

        // --- ИСПРАВЛЕНИЕ: Убиваем корутины полета колоды при скипе, чтобы они не конфликтовали с раздачей ---
        if (isSkipping && modeManager.animationService != null)
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
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Deal");
        yield return StartCoroutine(modeManager.animationService.AnimateCardLinear(card, slot.position, GetActualDuration(duration)));
        card.transform.SetParent(slot, true);
        card.transform.localPosition = Vector3.zero;
        modeManager.animationService.SetShadowFlying(card, false);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");
    }

    private float GetActualDuration(float baseDuration)
    {
        return isSkipping ? baseDuration / 15f : baseDuration;
    }

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
            float t = Mathf.Clamp01(elapsed / duration);
            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(t));
            yield return null;
        }
        if (target != null) target.anchoredPosition = to;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Drop");
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float speed = isSkipping ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            modeManager.pileManager.SetAllSlotsAlpha(t);
            yield return null;
        }
        modeManager.pileManager.SetAllSlotsAlpha(1f);
    }
}