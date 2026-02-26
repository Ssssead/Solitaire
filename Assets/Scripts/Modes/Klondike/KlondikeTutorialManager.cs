using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public enum TutorialActionType
{
    MoveCard, ClickStock, Undo, DoubleClick
}

public enum TutorialPanelAnchorType
{
    Bottom, Top, Center
}

// --- НОВОЕ: Типы стрелочек ---
public enum TutorialArrowType
{
    None, Down, Up, Left
}

[System.Serializable]
public class TutorialStep
{
    public string localizationKey;
    [TextArea(2, 4)] public string fallbackText;

    public TutorialActionType expectedAction;
    public string expectedCardName;
    public string expectedTargetPileName;

    public TutorialPanelAnchorType panelAnchor;
    public List<string> highlightElements;

    // --- НОВОЕ: Настройки стрелочки для шага ---
    public TutorialArrowType arrowType;
    public int arrowAnchorIndex = -1; // -1 значит стрелки нет
}

public class KlondikeTutorialManager : MonoBehaviour
{
    [Header("UI")]
    public RectTransform tutorialUIPanel;
    public TMP_Text instructionText;
    public TMP_Text stepIndicatorText;

    [Header("Panel Anchors")]
    public RectTransform topAnchor;
    public RectTransform centerAnchor;
    public RectTransform bottomAnchor;

    // --- НОВЫЙ РАЗДЕЛ ДЛЯ СТРЕЛОК ---
    [Header("Arrows")]
    public RectTransform arrowDown;
    public RectTransform arrowUp;
    public RectTransform arrowLeft;

    [Tooltip("Массив из 10 пустышек-якорей для стрелок")]
    public RectTransform[] arrowAnchors = new RectTransform[10];

    [Header("Arrow Animation Settings")]
    public float arrowBounceAmplitude = 12f; // Размах качания
    public float arrowBounceSpeed = 6f;      // Скорость качания
    private Coroutine arrowAnimCoroutine;

    [Header("Highlights")]
    public List<GameObject> highlightObjects = new List<GameObject>();

    [Header("Sequence")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    public bool IsTutorialActive { get; private set; }
    private int currentStepIndex = 0;
    private KlondikeModeManager modeManager;
    private Coroutine panelMoveCoroutine;

    private ICardContainer initialCardContainer;
    private int initialStockCount;
    private bool stepTransitioning = false;
    private bool autoWinForced = false;

    private Dictionary<RectTransform, Vector2> activeHighlightsOriginalPos = new Dictionary<RectTransform, Vector2>();
    private Coroutine highlightsAnimCoroutine;
    private const float HighlightOffscreenYOffset = 1500f;

    private void Awake()
    {
        IsTutorialActive = GameSettings.IsTutorialMode;

        if (!IsTutorialActive)
        {
            if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);
            DisableAllHighlights();
            this.enabled = false;
            return;
        }

        modeManager = GetComponent<KlondikeModeManager>();
        if (modeManager != null) modeManager.tutorialManager = this;

        steps.Clear();
        InitializeDefaultSteps();
    }

    private void LateUpdate()
    {
        if (!IsTutorialActive || stepTransitioning || currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];
        CardController expectedCard = FindCardByName(step.expectedCardName);
        var allCards = FindObjectsOfType<CardController>();

        // 1. Блокировка кликов
        foreach (var c in allCards)
        {
            if (c.canvasGroup != null)
            {
                bool isFlying = c.GetComponentInParent<ICardContainer>() == null;
                if (isFlying) continue;

                if (step.expectedAction == TutorialActionType.MoveCard || step.expectedAction == TutorialActionType.DoubleClick)
                {
                    if (string.IsNullOrEmpty(step.expectedCardName)) c.canvasGroup.blocksRaycasts = false;
                    else c.canvasGroup.blocksRaycasts = (c == expectedCard);
                }
                else if (step.expectedAction == TutorialActionType.ClickStock)
                {
                    bool isStock = c.GetComponentInParent<StockPile>() != null;
                    c.canvasGroup.blocksRaycasts = isStock;
                }
                else if (step.expectedAction == TutorialActionType.Undo)
                {
                    c.canvasGroup.blocksRaycasts = false;
                }
            }
        }

        // 2. Кнопка автосбора
        if (currentStepIndex == steps.Count - 1 && !autoWinForced)
        {
            autoWinForced = true;
            if (modeManager != null) modeManager.DebugForceShowAutoWin();
        }

        // 3. Проверка выполнения шага
        bool shouldAdvance = false;

        if (step.expectedAction == TutorialActionType.MoveCard || step.expectedAction == TutorialActionType.DoubleClick)
        {
            if (expectedCard != null)
            {
                ICardContainer current = expectedCard.GetComponentInParent<ICardContainer>();
                if (current != null && current != initialCardContainer)
                {
                    if (step.expectedTargetPileName == "Foundation" && current is FoundationPile) shouldAdvance = true;
                    else if (step.expectedTargetPileName == "Tableau" && current is TableauPile) shouldAdvance = true;
                }
            }
            else if (string.IsNullOrEmpty(step.expectedCardName))
            {
                if (modeManager.IsGameWon()) shouldAdvance = true;
            }
        }
        else if (step.expectedAction == TutorialActionType.ClickStock)
        {
            if (modeManager?.pileManager?.StockPile != null)
            {
                int currentStockCount = modeManager.pileManager.StockPile.transform.childCount;
                if (currentStockCount < initialStockCount) shouldAdvance = true;
            }
        }
        else if (step.expectedAction == TutorialActionType.Undo)
        {
            if (expectedCard != null)
            {
                ICardContainer current = expectedCard.GetComponentInParent<ICardContainer>();
                if (current != null && current != initialCardContainer) shouldAdvance = true;
            }
        }

        if (shouldAdvance)
        {
            stepTransitioning = true;
            StartCoroutine(AdvanceStepRoutine());
        }
    }

    private IEnumerator AdvanceStepRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        InternalAdvanceStep();
        stepTransitioning = false;
    }

    private CardController FindCardByName(string cardName)
    {
        if (string.IsNullOrEmpty(cardName)) return null;
        var allCards = FindObjectsOfType<CardController>();
        foreach (var c in allCards)
            if (c.gameObject.name.Contains(cardName)) return c;
        return null;
    }

    public void AdvanceStep() { }

    public bool IsActionAllowed(TutorialActionType action, CardController card = null, ICardContainer target = null)
    {
        return true;
    }

    private void InternalAdvanceStep()
    {
        if (!IsTutorialActive) return;

        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            string endText = "Поздравляем! Вы освоили все механики. Удачи в игре!";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue("KlondikeTutorialEnd");
                if (!string.IsNullOrEmpty(loc) && loc != "KlondikeTutorialEnd") endText = loc;
            }
            if (instructionText != null) instructionText.text = endText;
            if (stepIndicatorText != null) stepIndicatorText.text = "";
            DisableAllHighlights();
            IsTutorialActive = false;
        }
        else UpdateUI();
    }

    private void UpdateUI()
    {
        if (currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];

        CardController expectedCard = FindCardByName(step.expectedCardName);
        initialCardContainer = expectedCard != null ? expectedCard.GetComponentInParent<ICardContainer>() : null;
        if (modeManager != null && modeManager.pileManager.StockPile != null)
            initialStockCount = modeManager.pileManager.StockPile.transform.childCount;

        if (instructionText != null)
        {
            string textToShow = step.fallbackText;
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue(step.localizationKey);
                if (!string.IsNullOrEmpty(loc) && loc != step.localizationKey) textToShow = loc;
            }
            instructionText.text = textToShow;
        }

        if (stepIndicatorText != null) stepIndicatorText.text = $"{currentStepIndex + 1}/{steps.Count}";

        if (tutorialUIPanel != null)
        {
            if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
            RectTransform targetAnchor = GetAnchorRect(step.panelAnchor);
            panelMoveCoroutine = StartCoroutine(MovePanelRoutine(targetAnchor));
        }

        // --- УПРАВЛЕНИЕ СТРЕЛОЧКОЙ ---
        if (arrowAnimCoroutine != null) StopCoroutine(arrowAnimCoroutine);
        if (arrowDown != null) arrowDown.gameObject.SetActive(false);
        if (arrowUp != null) arrowUp.gameObject.SetActive(false);
        if (arrowLeft != null) arrowLeft.gameObject.SetActive(false);

        if (step.arrowType != TutorialArrowType.None && step.arrowAnchorIndex >= 0 && step.arrowAnchorIndex < arrowAnchors.Length)
        {
            RectTransform activeArrow = null;
            if (step.arrowType == TutorialArrowType.Down) activeArrow = arrowDown;
            else if (step.arrowType == TutorialArrowType.Up) activeArrow = arrowUp;
            else if (step.arrowType == TutorialArrowType.Left) activeArrow = arrowLeft;

            RectTransform targetArrowAnchor = arrowAnchors[step.arrowAnchorIndex];

            if (activeArrow != null && targetArrowAnchor != null)
            {
                activeArrow.gameObject.SetActive(true);
                // Делаем стрелку ребенком якоря, чтобы она идеально позиционировалась
                activeArrow.SetParent(targetArrowAnchor, false);
                activeArrow.anchoredPosition = Vector2.zero;

                arrowAnimCoroutine = StartCoroutine(AnimateArrowBounce(activeArrow, step.arrowType));
            }
        }

        // --- ОБНОВЛЕННАЯ ЛОГИКА ХАЙЛАЙТОВ ---
        activeHighlightsOriginalPos.Clear();
        foreach (var highlight in highlightObjects)
        {
            if (highlight == null) continue;
            bool shouldBeActive = step.highlightElements != null && step.highlightElements.Contains(highlight.name);
            highlight.SetActive(shouldBeActive);

            if (shouldBeActive)
            {
                RectTransform rt = highlight.GetComponent<RectTransform>();
                if (rt != null && !activeHighlightsOriginalPos.ContainsKey(rt))
                {
                    activeHighlightsOriginalPos.Add(rt, rt.anchoredPosition);
                }
            }
        }
    }

    // --- АНИМАЦИЯ СТРЕЛОЧКИ ---
    private IEnumerator AnimateArrowBounce(RectTransform arrow, TutorialArrowType type)
    {
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime * arrowBounceSpeed;
            float offset = Mathf.Sin(elapsed) * arrowBounceAmplitude;

            if (type == TutorialArrowType.Down || type == TutorialArrowType.Up)
            {
                arrow.anchoredPosition = new Vector2(0, offset);
            }
            else if (type == TutorialArrowType.Left)
            {
                arrow.anchoredPosition = new Vector2(offset, 0);
            }
            yield return null;
        }
    }

    private RectTransform GetAnchorRect(TutorialPanelAnchorType type)
    {
        switch (type)
        {
            case TutorialPanelAnchorType.Top: return topAnchor;
            case TutorialPanelAnchorType.Bottom: return bottomAnchor;
            case TutorialPanelAnchorType.Center: return centerAnchor;
            default: return centerAnchor;
        }
    }

    private IEnumerator MovePanelRoutine(RectTransform targetRect)
    {
        if (targetRect == null) yield break;

        Vector3 startWorldPos = tutorialUIPanel.position;

        tutorialUIPanel.anchorMin = targetRect.anchorMin;
        tutorialUIPanel.anchorMax = targetRect.anchorMax;
        tutorialUIPanel.pivot = targetRect.pivot;
        tutorialUIPanel.sizeDelta = targetRect.sizeDelta;

        tutorialUIPanel.position = startWorldPos;

        Vector2 startAnchoredPos = tutorialUIPanel.anchoredPosition;
        Vector2 targetAnchoredPos = targetRect.anchoredPosition;

        float distance = Vector2.Distance(startAnchoredPos, targetAnchoredPos);
        float duration = distance > 1000f ? 0.6f : 0.3f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startAnchoredPos, targetAnchoredPos, t);
            yield return null;
        }
        tutorialUIPanel.anchoredPosition = targetAnchoredPos;
    }

    private void DisableAllHighlights()
    {
        foreach (var highlight in highlightObjects) if (highlight != null) highlight.SetActive(false);
        activeHighlightsOriginalPos.Clear();

        if (arrowAnimCoroutine != null) StopCoroutine(arrowAnimCoroutine);
        if (arrowDown != null) arrowDown.gameObject.SetActive(false);
        if (arrowUp != null) arrowUp.gameObject.SetActive(false);
        if (arrowLeft != null) arrowLeft.gameObject.SetActive(false);
    }

    public void HidePanelToLeft()
    {
        if (tutorialUIPanel != null && tutorialUIPanel.gameObject.activeSelf)
        {
            if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
            panelMoveCoroutine = StartCoroutine(MovePanelOffscreenLeft());
        }
    }

    public void RestorePanelPosition()
    {
        if (!IsTutorialActive) return;
        UpdateUI();
    }

    public void HideHighlights()
    {
        if (tutorialUIPanel != null && tutorialUIPanel.gameObject.activeSelf)
        {
            if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
            panelMoveCoroutine = StartCoroutine(MovePanelOffscreenLeft());
        }

        // Убираем стрелки жестко перед анимацией выхода
        if (arrowAnimCoroutine != null) StopCoroutine(arrowAnimCoroutine);
        if (arrowDown != null) arrowDown.gameObject.SetActive(false);
        if (arrowUp != null) arrowUp.gameObject.SetActive(false);
        if (arrowLeft != null) arrowLeft.gameObject.SetActive(false);

        if (activeHighlightsOriginalPos != null && activeHighlightsOriginalPos.Count > 0)
        {
            if (highlightsAnimCoroutine != null) StopCoroutine(highlightsAnimCoroutine);
            highlightsAnimCoroutine = StartCoroutine(AnimateHighlightsOut());
        }
        else
        {
            foreach (var highlight in highlightObjects)
                if (highlight != null) highlight.SetActive(false);
        }
    }

    private IEnumerator MovePanelOffscreenLeft()
    {
        Vector2 startPos = tutorialUIPanel.anchoredPosition;
        Vector2 targetPos = new Vector2(-2500f, startPos.y);

        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        tutorialUIPanel.anchoredPosition = targetPos;
    }

    private IEnumerator AnimateHighlightsOut()
    {
        float duration = 0.6f;
        float elapsed = 0f;

        List<RectTransform> highlightsToAnimate = new List<RectTransform>(activeHighlightsOriginalPos.Keys);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);

            foreach (var rt in highlightsToAnimate)
            {
                if (rt == null) continue;
                Vector2 originalPos = activeHighlightsOriginalPos[rt];
                Vector2 targetPos = originalPos + new Vector2(0, HighlightOffscreenYOffset);
                rt.anchoredPosition = Vector2.Lerp(originalPos, targetPos, t);
            }
            yield return null;
        }

        foreach (var rt in highlightsToAnimate)
        {
            if (rt != null) rt.gameObject.SetActive(false);
        }
    }

    private IEnumerator AnimateHighlightsIn()
    {
        float duration = 0.7f;
        float elapsed = 0f;

        List<RectTransform> highlightsToAnimate = new List<RectTransform>(activeHighlightsOriginalPos.Keys);

        foreach (var rt in highlightsToAnimate)
        {
            if (rt == null) continue;
            rt.gameObject.SetActive(true);
            rt.anchoredPosition = activeHighlightsOriginalPos[rt] + new Vector2(0, HighlightOffscreenYOffset);
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);

            foreach (var rt in highlightsToAnimate)
            {
                if (rt == null) continue;
                Vector2 originalPos = activeHighlightsOriginalPos[rt];
                Vector2 startPos = originalPos + new Vector2(0, HighlightOffscreenYOffset);
                rt.anchoredPosition = Vector2.Lerp(startPos, originalPos, t);
            }
            yield return null;
        }

        foreach (var rt in highlightsToAnimate)
        {
            if (rt != null) rt.anchoredPosition = activeHighlightsOriginalPos[rt];
        }
    }

    public IEnumerator PlayTutorialIntro(Deal dummyDeal)
    {
        modeManager.IsInputAllowed = false;
        var tempDeck = modeManager.deckManager;
        modeManager.deckManager = null;

        if (modeManager.introController != null) yield return StartCoroutine(modeManager.introController.PlayIntroSequence(false));

        modeManager.deckManager = tempDeck;
        PileManager pm = modeManager.pileManager;
        pm.ClearAllPiles();
        modeManager.cardFactory.DestroyAllCards();

        string[] F0 = { "Spades_1_up", "Spades_2_up", "Spades_3_up", "Spades_4_up", "Spades_5_up" };
        string[] F1 = { "Hearts_1_up", "Hearts_2_up", "Hearts_3_up", "Hearts_4_up", "Hearts_5_up" };
        string[] F2 = { "Clubs_1_up", "Clubs_2_up", "Clubs_3_up", "Clubs_4_up", "Clubs_5_up", "Clubs_6_up", "Clubs_7_up", "Clubs_8_up", "Clubs_9_up" };
        string[] F3 = { "Diamonds_1_up", "Diamonds_2_up", "Diamonds_3_up", "Diamonds_4_up", "Diamonds_5_up", "Diamonds_6_up", "Diamonds_7_up", "Diamonds_8_up", "Diamonds_9_up" };

        string[] Stock = { "Hearts_9_down", "Spades_10_down" };

        string[] T0 = { "Spades_6_up" };
        string[] T1 = { "Hearts_6_down", "Clubs_10_up" };
        string[] T2 = { "Spades_13_up", "Hearts_12_up", "Clubs_11_up" };
        string[] T3 = { "Spades_8_down", "Spades_7_down", "Hearts_13_up", "Clubs_12_up", "Diamonds_11_up" };
        string[] T4 = { "Diamonds_13_up", "Spades_12_up", "Hearts_11_up" };
        string[] T5 = { "Hearts_8_down", "Hearts_7_down", "Diamonds_10_up", "Spades_9_up" };
        string[] T6 = { "Clubs_13_up", "Diamonds_12_up", "Spades_11_up", "Hearts_10_up" };

        SpawnAndPlaceCards(F0, pm.Foundations[0]);
        SpawnAndPlaceCards(F1, pm.Foundations[1]);
        SpawnAndPlaceCards(F2, pm.Foundations[2]);
        SpawnAndPlaceCards(F3, pm.Foundations[3]);
        SpawnAndPlaceCards(Stock, pm.StockPile);
        SpawnAndPlaceCards(T0, pm.Tableau[0]);
        SpawnAndPlaceCards(T1, pm.Tableau[1]);
        SpawnAndPlaceCards(T2, pm.Tableau[2]);
        SpawnAndPlaceCards(T3, pm.Tableau[3]);
        SpawnAndPlaceCards(T4, pm.Tableau[4]);
        SpawnAndPlaceCards(T5, pm.Tableau[5]);
        SpawnAndPlaceCards(T6, pm.Tableau[6]);

        Canvas.ForceUpdateCanvases();
        Vector2 offScreenOffset = new Vector2(0, 1500f);
        Dictionary<CardController, Vector2> targetAnchors = new Dictionary<CardController, Vector2>();
        List<ICardContainer> animSequence = new List<ICardContainer> { pm.Tableau[0], pm.Tableau[1], pm.Tableau[2], pm.Tableau[3], pm.Tableau[4], pm.Tableau[5], pm.Tableau[6], pm.StockPile, pm.Foundations[0], pm.Foundations[1], pm.Foundations[2], pm.Foundations[3] };

        foreach (var container in animSequence)
        {
            var mono = container as MonoBehaviour;
            if (mono == null) continue;
            foreach (Transform child in mono.transform)
            {
                var card = child.GetComponent<CardController>();
                if (card != null) { targetAnchors[card] = card.rectTransform.anchoredPosition; card.rectTransform.anchoredPosition += offScreenOffset; }
            }
        }

        foreach (var container in animSequence)
        {
            var mono = container as MonoBehaviour;
            if (mono == null) continue;
            var cardsInStack = new List<CardController>();
            foreach (Transform child in mono.transform)
            {
                var card = child.GetComponent<CardController>();
                if (card != null) cardsInStack.Add(card);
            }
            if (cardsInStack.Count > 0) StartCoroutine(AnimateStackDrop(cardsInStack, targetAnchors, offScreenOffset, 0.3f));
            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(0.3f);
        modeManager.animationService.ReorderAllContainers(pm.GetAllContainerTransforms());

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameStarted("Klondike", Difficulty.Easy, "Tutorial");
        if (modeManager.scoreManager != null) modeManager.scoreManager.ResetScore();

        currentStepIndex = 0;
        DisableAllHighlights();

        if (tutorialUIPanel)
        {
            RectTransform targetAnchor = GetAnchorRect(steps.Count > 0 ? steps[0].panelAnchor : TutorialPanelAnchorType.Bottom);
            if (targetAnchor != null)
            {
                tutorialUIPanel.anchorMin = targetAnchor.anchorMin;
                tutorialUIPanel.anchorMax = targetAnchor.anchorMax;
                tutorialUIPanel.pivot = targetAnchor.pivot;
                tutorialUIPanel.sizeDelta = targetAnchor.sizeDelta;
                tutorialUIPanel.anchoredPosition = targetAnchor.anchoredPosition + new Vector2(2500f, 0);
            }
            tutorialUIPanel.gameObject.SetActive(true);
        }

        UpdateUI();
        modeManager.IsInputAllowed = true;
    }

    private IEnumerator AnimateStackDrop(List<CardController> cardsInStack, Dictionary<CardController, Vector2> targetAnchors, Vector2 offScreenOffset, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            foreach (var card in cardsInStack) if (card != null) card.rectTransform.anchoredPosition = Vector2.Lerp(targetAnchors[card] + offScreenOffset, targetAnchors[card], t);
            yield return null;
        }
        foreach (var card in cardsInStack) if (card != null) card.rectTransform.anchoredPosition = targetAnchors[card];
    }

    private void SpawnAndPlaceCards(string[] cardData, ICardContainer target)
    {
        foreach (var data in cardData)
        {
            string[] parts = data.Split('_');
            string suitStr = parts[0];
            int rank = int.Parse(parts[1]);
            bool isFaceUp = parts[2] == "up";

            Suit suit = Suit.Spades;
            if (suitStr == "Hearts") suit = Suit.Hearts;
            else if (suitStr == "Clubs") suit = Suit.Clubs;
            else if (suitStr == "Diamonds") suit = Suit.Diamonds;

            CardModel model = new CardModel(suit, rank);
            CardController card = modeManager.cardFactory.CreateCard(model, ((MonoBehaviour)target).transform, Vector2.zero);

            card.gameObject.name = $"Card_{suitStr}_{rank}";
            modeManager.RegisterCardEvents(card);

            if (target is TableauPile tableau) tableau.AddCard(card, isFaceUp);
            else if (target is FoundationPile foundation) foundation.AcceptCard(card);
            else
            {
                var add2 = target.GetType().GetMethod("AddCard", new System.Type[] { typeof(CardController), typeof(bool) });
                if (add2 != null) add2.Invoke(target, new object[] { card, isFaceUp });
                else target.GetType().GetMethod("AddCard", new System.Type[] { typeof(CardController) })?.Invoke(target, new object[] { card });
                card.GetComponent<CardData>()?.SetFaceUp(isFaceUp, false);
            }
        }
        if (target is TableauPile tPile) { tPile.StopAllCoroutines(); tPile.ForceRebuildLayout(); }
    }
    private void InitializeDefaultSteps()
    {
        string clrOrg = "<color=#FCA311>";
        string clrBlk = "<color=#8294FF>";
        string clrRed = "<color=#FF6B6B>";
        string endClr = "</color>";

        // 1 (Стрелка вниз, над 6)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial1",
            fallbackText = $"Цель игры — собрать карты в {clrOrg}'Дома'{endClr} сверху.\nПеретащите открытую {clrBlk}Шестерку Пик(6♠){endClr} в первый {clrOrg}Дом{endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Spades_6",
            expectedTargetPileName = "Foundation",
            panelAnchor = TutorialPanelAnchorType.Center,
            highlightElements = new List<string> { "Highlight_Foundation" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 0
        });

        // 2 (Стрелка вверх, под 10)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial2",
            fallbackText = $"На {clrOrg}Игровом столе{endClr} карты складываются по убыванию с чередованием цвета.\nПеретащите {clrBlk}черную Десятку Треф(10♣){endClr} на {clrRed}красного Валета Бубен(J♦){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Clubs_10",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_Tableau" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 1
        });

        // 3 (Стрелка вверх, как во 2)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial3",
            fallbackText = $"Отлично, открылась скрытая карта! Их можно быстро отправлять в {clrOrg}Дом{endClr} двойным кликом.\nДважды кликните по {clrRed}Шестерке Червей(6♥){endClr}.",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Hearts_6",
            expectedTargetPileName = "Foundation",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 1
        });

        // 4 (Стрелка вниз, над Королем)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial4",
            fallbackText = $"В пустые ячейки на поле можно класть {clrOrg}ТОЛЬКО{endClr} Королей.\nПеретащите {clrRed}Короля Червей(K♥){endClr} в первую пустую колонку.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Hearts_13",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_EmptyTableau" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 2
        });

        // 5 (Стрелка вниз, над 10♦)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial5",
            fallbackText = $"Вы можете переносить сразу несколько открытых карт.\nПеренесите стопку с {clrRed}красной Десяткой Бубен(10♦){endClr} на {clrBlk}черного Валета Треф(J♣){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Diamonds_10",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 3
        });

        // 6 (Стрелка влево, справа от колоды)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial6",
            fallbackText = $"Нет доступных ходов? Используйте {clrOrg}колоду{endClr} (слева сверху). Кликните по ней, чтобы взять карту.",
            expectedAction = TutorialActionType.ClickStock,
            expectedCardName = "",
            expectedTargetPileName = "",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Stock" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 4
        });

        // 7 (Стрелка влево, справа от 10♠)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial7",
            fallbackText = $"Нам выпала {clrBlk}Десятка Пик(10♠){endClr}. Положите её на открывшегося {clrRed}Валета Червей(J♥){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Spades_10",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 5
        });

        // 8 (Стрелка вниз, над Undo)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial8",
            fallbackText = $"Отличный ход! Но иногда действие нужно отменить. Давайте проверим как работает кнопка {clrOrg}'Отмена'{endClr} внизу экрана.",
            expectedAction = TutorialActionType.Undo,
            expectedCardName = "Spades_10",
            expectedTargetPileName = "",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_UndoButton" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 6
        });

        // 9 (Стрелка влево, как в 7)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial9",
            fallbackText = $"Верните {clrBlk}Десятку Пик(10♠){endClr} обратно на {clrRed}Валета Червей(J♥){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Spades_10",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 5
        });

        // 10 (Стрелка вверх, под 9♦)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial10",
            fallbackText = $"Иногда карту выгодно вернуть из {clrOrg}Дома{endClr} обратно на поле.\nПеретащите {clrRed}Девятку Бубен(9♦){endClr} из {clrOrg}Дома{endClr} на {clrBlk}черную Десятку Пик(10♠){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Diamonds_9",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Foundation" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 7
        });

        // 11 (Стрелка влево, как в 6)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial11",
            fallbackText = $"В нашей обучающей {clrOrg}колоде{endClr} осталась последняя карта. Достанем её!\nКликните по колоде.",
            expectedAction = TutorialActionType.ClickStock,
            expectedCardName = "",
            expectedTargetPileName = "",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Stock" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 4
        });

        // 12 (Стрелка влево, как в 7)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial12",
            fallbackText = $"Положите {clrRed}Девятку Червей(9♥){endClr} на {clrBlk}Десятку Треф(10♣){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Hearts_9",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 5
        });

        // 13 (Стрелка вверх, под 7♠)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial13",
            fallbackText = $"Колода пуста! Теперь отправим открывшиеся карты в {clrOrg}Дом{endClr}.\nДважды кликните по {clrBlk}Семерке Пик(7♠){endClr}.",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Spades_7",
            expectedTargetPileName = "Foundation",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 8
        });

        // 14 (Стрелка вверх, под 7♥)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial14",
            fallbackText = $"Дважды кликните по {clrRed}Семерке Червей(7♥){endClr}.",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Hearts_7",
            expectedTargetPileName = "Foundation",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 9
        });

        // 15 (Стрелка влево, как в 7 - на кнопку Автосбора)
        steps.Add(new TutorialStep
        {
            localizationKey = "KlondikeTutorial15",
            fallbackText = $"Все карты открыты. Нажмите кнопку {clrOrg}'АВТО'{endClr}!",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "",
            expectedTargetPileName = "",
            panelAnchor = TutorialPanelAnchorType.Center,
            highlightElements = new List<string> { "Highlight_AutoWinButton" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 5
        });
    }
}