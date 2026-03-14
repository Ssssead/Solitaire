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
    [Tooltip("Сколько времени вся колода вылетает из-за экрана (увеличено для плавности)")]
    public float deckFlyDuration = 0.8f;
    [Tooltip("Скорость полета одной карты от колоды до слота")]
    public float cardFlyDuration = 0.15f;
    [Tooltip("Задержка между 'выстрелами' пулемета")]
    public float dealDelay = 0.04f;

    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<MonteCarloModeManager>();
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

    // --- IIntroController Implementation ---
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
    // ---------------------------------------

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

    public IEnumerator PlayIntroSequence(bool isRestart)
    {
        if (!isRestart)
        {
            yield return new WaitForSeconds(startDelay);

            StartCoroutine(FadeInSlots(slotsFadeDuration));
            if (topPanel != null) StartCoroutine(AnimateUIElement(topPanel, topPanelHiddenPos, topPanelStartPos, uiSlideDuration));

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                    yield return new WaitForSeconds(buttonStaggerDelay);
                }
            }
        }
        else
        {
            yield return new WaitForSeconds(0.2f);
        }

        // 2. Вся колода влетает в слот Stock (Собираем правильный порядок карт)
        List<CardController> fullDeck = new List<CardController>();

        // Карты, которые останутся в стоке, идут вниз стопки
        fullDeck.AddRange(modeManager.pileManager.StockCards);

        // Карты, которые раздаются на стол, кладутся СВЕРХУ (BoardCards[24] будет самой верхней)
        for (int i = 0; i < 25; i++)
        {
            if (modeManager.pileManager.BoardCards[i] != null)
                fullDeck.Add(modeManager.pileManager.BoardCards[i]);
        }

        Vector2 offset = modeManager.deckManager.stockCardOffset;

        // Выстраиваем всю колоду "лесенкой" еще за экраном
        for (int i = 0; i < fullDeck.Count; i++)
        {
            var c = fullDeck[i];
            c.transform.SetParent(modeManager.pileManager.StockRoot, true);
            c.transform.SetAsLastSibling(); // Задаем правильный порядок слоев (лесенка вверх)
            c.transform.localPosition = new Vector3(-1500f + (offset.x * i), offset.y * i, 0f);
        }

        // Запускаем полет каждой карты на её законное место со сдвигом в StockRoot
        for (int i = 0; i < fullDeck.Count; i++)
        {
            var c = fullDeck[i];
            Vector3 targetLocal = new Vector3(offset.x * i, offset.y * i, 0f);
            Vector3 targetWorld = modeManager.pileManager.StockRoot.TransformPoint(targetLocal);

            StartCoroutine(modeManager.animationService.AnimateCardLinear(c, targetWorld, deckFlyDuration));
        }

        yield return new WaitForSeconds(deckFlyDuration + 0.1f);

        // 3. ПУЛЕМЕТ: Карты вылетают напрямую в свои слоты, стартуя с текущего места (сохраняя отступ!)
        Coroutine lastRoutine = null;

        for (int i = 24; i >= 0; i--)
        {
            CardController c = modeManager.pileManager.BoardCards[i];
            if (c == null) continue;

            // Переносим в dragLayer, чтобы летящая карта была поверх стопки и сетки
            if (modeManager.animationService.dragLayer != null)
                c.transform.SetParent(modeManager.animationService.dragLayer, true);

            c.transform.SetAsLastSibling();
            lastRoutine = StartCoroutine(FlyAndDock(c, modeManager.pileManager.TableauSlots[i], cardFlyDuration));

           

            yield return new WaitForSeconds(dealDelay);
        }

        if (lastRoutine != null) yield return lastRoutine;

        // 4. Финальная очистка
        for (int i = 0; i < 25; i++)
        {
            var c = modeManager.pileManager.BoardCards[i];
            if (c != null)
            {
                c.transform.SetParent(modeManager.pileManager.TableauSlots[i], true);
                c.transform.localPosition = Vector3.zero;
                modeManager.animationService.SetShadowFlying(c, false);
            }
        }
    }

    private IEnumerator FlyAndDock(CardController card, Transform slot, float duration)
    {
        yield return StartCoroutine(modeManager.animationService.AnimateCardLinear(card, slot.position, duration));
        card.transform.SetParent(slot, true);
        card.transform.localPosition = Vector3.zero;
        modeManager.animationService.SetShadowFlying(card, false);
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
            modeManager.pileManager.SetAllSlotsAlpha(elapsed / duration);
            yield return null;
        }
        modeManager.pileManager.SetAllSlotsAlpha(1f);
    }
}