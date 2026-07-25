using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class YukonTutorialManager : MonoBehaviour, ITutorialManager
{
    [Header("UI")]
    public RectTransform tutorialUIPanel;
    public TMP_Text instructionText;
    public TMP_Text stepIndicatorText;

    [Header("Panel Anchors")]
    public RectTransform topAnchor;
    public RectTransform centerAnchor;
    public RectTransform bottomAnchor;

    [Header("UI Buttons to Block")]
    public UnityEngine.UI.Button undoButton;
    public UnityEngine.UI.Button undoAllButton;
    public UnityEngine.UI.Button autoWinButton;

    [Header("Arrows")]
    public RectTransform arrowDown;
    public RectTransform arrowUp;
    public RectTransform arrowLeft;
    public RectTransform[] arrowAnchors = new RectTransform[10];

    [Header("Arrow Animation Settings")]
    public float arrowBounceAmplitude = 12f;
    public float arrowBounceSpeed = 6f;
    private Coroutine arrowAnimCoroutine;

    [Header("Highlights")]
    public List<GameObject> highlightObjects = new List<GameObject>();

    [Header("Sequence")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    public bool IsTutorialActive { get; private set; }
    private int currentStepIndex = 0;

    private YukonModeManager modeManager;
    private Coroutine panelMoveCoroutine;

    // --- ФИКС: Ссылка для временного хранения DeckManager ---
    private YukonDeckManager _tempDeckManager;

    private ICardContainer initialCardContainer;
    private bool stepTransitioning = false;

    private Dictionary<RectTransform, Vector2> activeHighlightsOriginalPos = new Dictionary<RectTransform, Vector2>();

    private void Awake()
    {
        IsTutorialActive = GameSettings.IsTutorialMode;

        if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);

        if (!IsTutorialActive)
        {
            DisableAllHighlights();
            this.enabled = false;
            return;
        }

        modeManager = GetComponent<YukonModeManager>();

        steps.Clear();
        InitializeDefaultSteps();
    }

    private void LateUpdate()
    {
        if (!IsTutorialActive || stepTransitioning || currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];
        CardController expectedCard = FindCardByName(step.expectedCardName);
        var allCards = FindObjectsOfType<YukonCardController>();

        if (undoAllButton != null) undoAllButton.interactable = false;
        if (undoButton != null) undoButton.interactable = (step.expectedAction == TutorialActionType.Undo);
        if (autoWinButton != null) autoWinButton.interactable = (step.expectedAction == TutorialActionType.ClickAuto);

        foreach (var c in allCards)
        {
            if (c.canvasGroup == null) continue;

            bool isFlying = c.GetComponentInParent<ICardContainer>() == null;
            if (isFlying) continue;

            if (step.expectedAction == TutorialActionType.MoveCard || step.expectedAction == TutorialActionType.DoubleClick)
            {
                c.canvasGroup.blocksRaycasts = (c == expectedCard);
            }
            else
            {
                c.canvasGroup.blocksRaycasts = false;
            }
        }

        bool shouldAdvance = false;

        if (step.expectedAction == TutorialActionType.MoveCard || step.expectedAction == TutorialActionType.DoubleClick)
        {
            if (expectedCard != null)
            {
                ICardContainer currentContainer = expectedCard.GetComponentInParent<ICardContainer>();
                if (currentContainer != null && currentContainer != initialCardContainer)
                {
                    if (step.expectedTargetPileName == "Foundation" && currentContainer is FoundationPile)
                    {
                        shouldAdvance = true;
                    }
                    else if (step.expectedTargetPileName == "Tableau" && currentContainer is YukonTableauPile tp)
                    {
                        if (step.localizationKey == "YukonTutorial1" && tp.gameObject.name.Contains("0")) shouldAdvance = true;
                        else if (step.localizationKey == "YukonTutorial2" && tp.gameObject.name.Contains("1")) shouldAdvance = true;
                        else if (step.localizationKey == "YukonTutorial4" && tp.gameObject.name.Contains("2")) shouldAdvance = true;
                        else if (step.localizationKey == "YukonTutorial6" && tp.cards.Count == 1 && tp.cards[0] == expectedCard) shouldAdvance = true;
                    }

                    if (!shouldAdvance && currentContainer is YukonTableauPile)
                    {
                        StartCoroutine(modeManager.undoManager.UndoLastCoroutine());
                    }
                }
            }
        }
        else if (step.expectedAction == TutorialActionType.Undo)
        {
            if (expectedCard != null)
            {
                ICardContainer currentContainer = expectedCard.GetComponentInParent<ICardContainer>();
                if (currentContainer != null && currentContainer != initialCardContainer) shouldAdvance = true;
            }
        }
        else if (step.expectedAction == TutorialActionType.ClickAuto)
        {
            var fPiles = FindObjectsOfType<FoundationPile>();
            int totalComplete = 0;
            foreach (var f in fPiles) if (f.cards.Count == 13) totalComplete++;
            if (totalComplete == 4) shouldAdvance = true;
        }

        if (shouldAdvance)
        {
            AdvanceStep();
        }
    }

    public void AdvanceStep()
    {
        if (!stepTransitioning && IsTutorialActive && gameObject.activeInHierarchy)
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

    private CardController FindCardByName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;
        var allCards = FindObjectsOfType<YukonCardController>();
        foreach (var c in allCards)
        {
            if (c.gameObject.name == exactName) return c;
        }
        return null;
    }

    private void InternalAdvanceStep()
    {
        if (!IsTutorialActive) return;

        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            string endText = "Поздравляем! Вы освоили все механики Юкона!";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue("YukonTutorialEnd");
                if (!string.IsNullOrEmpty(loc)) endText = loc;
            }
            if (instructionText != null) instructionText.text = endText;
            if (stepIndicatorText != null) stepIndicatorText.text = "";

            DisableAllHighlights();

            if (tutorialUIPanel != null)
            {
                RectTransform targetAnchor = GetAnchorRect(TutorialPanelAnchorType.Center);
                StartCoroutine(MovePanelRoutine(targetAnchor));
            }

            IsTutorialActive = false;
            UnlockAllElements();
        }
        else
        {
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];

        if (step.localizationKey == "YukonTutorial2")
        {
            modeManager.CurrentVariant = YukonVariant.Russian;
        }
        else
        {
            modeManager.CurrentVariant = YukonVariant.Classic;
        }

        CardController expectedCard = FindCardByName(step.expectedCardName);
        initialCardContainer = expectedCard != null ? expectedCard.GetComponentInParent<ICardContainer>() : null;

        if (instructionText != null)
        {
            string textToShow = step.fallbackText;
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue(step.localizationKey);
                if (!string.IsNullOrEmpty(loc)) textToShow = loc;
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
                activeArrow.SetParent(targetArrowAnchor, false);
                activeArrow.anchoredPosition = Vector2.zero;
                arrowAnimCoroutine = StartCoroutine(AnimateArrowBounce(activeArrow, step.arrowType));
            }
        }

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

    private IEnumerator AnimateArrowBounce(RectTransform arrow, TutorialArrowType type)
    {
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime * arrowBounceSpeed;
            float offset = Mathf.Sin(elapsed) * arrowBounceAmplitude;

            if (type == TutorialArrowType.Down || type == TutorialArrowType.Up) arrow.anchoredPosition = new Vector2(0, offset);
            else if (type == TutorialArrowType.Left) arrow.anchoredPosition = new Vector2(offset, 0);

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
        if (tutorialUIPanel == null || !tutorialUIPanel.gameObject.activeInHierarchy) return;
        if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);

        Vector2 targetPos = tutorialUIPanel.anchoredPosition + new Vector2(-2500f, 0);
        panelMoveCoroutine = StartCoroutine(MovePanelOutRoutine(targetPos));
        UnlockAllElements();
    }

    private IEnumerator MovePanelOutRoutine(Vector2 targetAnchoredPos)
    {
        Vector2 startAnchoredPos = tutorialUIPanel.anchoredPosition;
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startAnchoredPos, targetAnchoredPos, t);
            yield return null;
        }

        tutorialUIPanel.anchoredPosition = targetAnchoredPos;
        tutorialUIPanel.gameObject.SetActive(false);
    }

    public void RestorePanelPosition()
    {
        if (!IsTutorialActive) return;
        UpdateUI();
    }

    public void HideHighlights()
    {
        DisableAllHighlights();
        UnlockAllElements();
    }

    private void UnlockAllElements()
    {
        IsTutorialActive = false;

        // Добавлена проверка на null, чтобы не было ошибки в обычной игре
        if (modeManager != null)
        {
            modeManager.CurrentVariant = YukonVariant.Classic;
        }

        if (undoAllButton != null) undoAllButton.interactable = true;
        if (undoButton != null) undoButton.interactable = true;
        if (autoWinButton != null) autoWinButton.interactable = true;

        var allCards = FindObjectsOfType<YukonCardController>();
        foreach (var c in allCards)
        {
            if (c.canvasGroup != null) c.canvasGroup.blocksRaycasts = true;
        }
    }

    // ==========================================
    // СТАРТОВАЯ РАССТАНОВКА ЮКОНА
    // ==========================================

    public IEnumerator PlayTutorialIntro(Deal dummyDeal)
    {
        modeManager.IsInputAllowed = false;

        if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);
        DisableAllHighlights();

        if (modeManager.introController != null) modeManager.introController.PrepareIntro(false);

        _tempDeckManager = modeManager.deckManager;
        modeManager.deckManager = null; // Отвязываем, чтобы IntroController не запустил раздачу

        _tempDeckManager.ClearBoard(); // Очищаем стол через временную ссылку

        // 1. Создаем карты в Домах 
        
        string[] F_Spades = { "Spades_1_up", "Spades_2_up", "Spades_3_up", "Spades_4_up", "Spades_5_up", "Spades_6_up", "Spades_7_up", "Spades_8_up" };
        string[] F_Hearts = { "Hearts_1_up", "Hearts_2_up", "Hearts_3_up", "Hearts_4_up", "Hearts_5_up", "Hearts_6_up", "Hearts_7_up", "Hearts_8_up", "Hearts_9_up" };
        string[] F_Clubs = { "Clubs_1_up", "Clubs_2_up", "Clubs_3_up", "Clubs_4_up", "Clubs_5_up", "Clubs_6_up", "Clubs_7_up", "Clubs_8_up", "Clubs_9_up" };
        string[] F_Diamonds = { "Diamonds_1_up", "Diamonds_2_up", "Diamonds_3_up", "Diamonds_4_up", "Diamonds_5_up", "Diamonds_6_up", "Diamonds_7_up", "Diamonds_8_up", "Diamonds_9_up" };
        
        SpawnAndPlaceCards(F_Spades, modeManager.foundations[0]);
        SpawnAndPlaceCards(F_Hearts, modeManager.foundations[1]);
        SpawnAndPlaceCards(F_Clubs, modeManager.foundations[2]);
        SpawnAndPlaceCards(F_Diamonds, modeManager.foundations[3]);

        // 2. Расклад стола 
        string[] T0 = { "Hearts_11_up" };   // J♥
        string[] T1 = { "Spades_11_up" };   // J♠
        string[] T2 = { "Hearts_13_up" };   // K♥
        string[] T3 = { "Diamonds_12_down", "Spades_10_up" }; // Q♦, 10♠
        string[] T4 = { "Clubs_13_up" };    // K♣
        string[] T5 = { "Clubs_10_up" };    // 10♣

        // ОГРОМНАЯ стопка в последней (7-й) колонке:
        string[] T6 = {
    "Spades_9_down",  // <--- НОВАЯ КАРТА: скрытая Девятка Пик в самом низу
    "Spades_13_down", // Скрытый Король Пик над ней
    "Clubs_12_up",    // Дама Треф (за которую мы тянем в 4-м шаге)
    "Diamonds_13_up",
    "Spades_12_up",
    "Diamonds_11_up",
    "Hearts_12_up",
    "Clubs_11_up",
    "Hearts_10_up",
    "Diamonds_10_up"
};

        SpawnAndPlaceCards(T0, modeManager.tableaus[0]);
        SpawnAndPlaceCards(T1, modeManager.tableaus[1]);
        SpawnAndPlaceCards(T2, modeManager.tableaus[2]);
        SpawnAndPlaceCards(T3, modeManager.tableaus[3]);
        SpawnAndPlaceCards(T4, modeManager.tableaus[4]);
        SpawnAndPlaceCards(T5, modeManager.tableaus[5]);
        SpawnAndPlaceCards(T6, modeManager.tableaus[6]);

        Canvas.ForceUpdateCanvases();

        // Ждем 1 кадр, чтобы объекты проинициализировались
        yield return null;

        // =================================================================
        // --- ФИКС СЖАТИЯ: Жестко задаем отступы до записи координат ---
        // =================================================================
        foreach (var tPile in modeManager.tableaus)
        {
            if (tPile == null) continue;
            tPile.StopAllCoroutines(); // Гасим любые плавные анимации выравнивания

            float currentY = 0f;
            for (int i = 0; i < tPile.cards.Count; i++)
            {
                var card = tPile.cards[i];
                card.StopAllCoroutines();

                // Принудительно расставляем по местам в локальных координатах
                card.rectTransform.anchoredPosition = new Vector2(0, currentY);

                bool isFaceUp = true;
                var data = card.GetComponent<CardData>();
                if (data != null) isFaceUp = data.IsFaceUp();

                // Читаем зазор по правилам Юкона: 35 для открытых, 10 для закрытых
                currentY -= isFaceUp ? 35f : 10f;
            }
        }
        // =================================================================

        List<ICardContainer> animSequence = new List<ICardContainer>();
        animSequence.AddRange(modeManager.tableaus);
        animSequence.AddRange(modeManager.foundations);

        Vector2 offScreenOffset = new Vector2(0, 1500f);
        Dictionary<CardController, Vector2> targetAnchors = new Dictionary<CardController, Vector2>();

        // Теперь targetAnchors запомнит ИДЕАЛЬНЫЕ позиции
        foreach (var container in animSequence)
        {
            var mono = container as MonoBehaviour;
            if (mono == null) continue;
            foreach (Transform child in mono.transform)
            {
                var card = child.GetComponent<CardController>();
                if (card != null)
                {
                    targetAnchors[card] = card.rectTransform.anchoredPosition;
                    card.rectTransform.anchoredPosition += offScreenOffset;
                }
            }
        }

        // Проигрываем UI интро
        if (modeManager.introController != null) yield return StartCoroutine(modeManager.introController.PlayIntroSequence(false, dummyDeal));

        modeManager.deckManager = _tempDeckManager;

        // Анимация падения наших кастомных стопок
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

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameStarted("Yukon", Difficulty.Easy, "Tutorial");

        currentStepIndex = 0;
        DisableAllHighlights();
        modeManager.CurrentVariant = YukonVariant.Classic;

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

            // Используем сохраненную ссылку на фабрику
            var deckParams = _tempDeckManager.cardFactory;
            var newCard = Instantiate(deckParams.cardPrefab, modeManager.RootCanvas.transform, false).GetComponent<YukonCardController>();

            newCard.name = $"Card_{suitStr}_{rank}";
            newCard.cardModel = model;
            newCard.canvas = modeManager.RootCanvas;

            var cData = newCard.GetComponent<CardData>();
            if (cData != null && deckParams.spriteDb != null)
            {
                Sprite face = deckParams.spriteDb.GetSprite(model.suit, model.rank);
                cData.backSprite = deckParams.spriteDb.GetCurrentBackSprite();
                cData.SetModel(model, face);
            }

            if (target is YukonTableauPile tableau)
            {
                tableau.AcceptCard(newCard);
                newCard.SetFaceUp(isFaceUp, true);
            }
            else if (target is FoundationPile foundation)
            {
                foundation.AcceptCard(newCard);
                newCard.SetFaceUp(true, true);
            }
        }

        if (target is YukonTableauPile tPile) tPile.ForceRecalculateLayout();
    }

    // ==========================================
    // СЦЕНАРИЙ ШАГОВ
    // ==========================================

    private void InitializeDefaultSteps()
    {
        string clrOrg = "<color=#FCA311>";
        string clrBlk = "<color=#8294FF>";
        string clrRed = "<color=#FF6B6B>";
        string endClr = "</color>";

        steps.Add(new TutorialStep
        {
            localizationKey = "YukonTutorial1",
            fallbackText = $"В нашей игре есть два варианта: {clrOrg}'Классический'{endClr} и 'Русский'. В Классическом карты чередуются по цвету.\nПеренесите черную {clrBlk}Десятку Пик (10♠){endClr} на красного {clrRed}Валета Червей (J♥){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Spades_10",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_Tableau_0", "Highlight_Tableau_3" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 0
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "YukonTutorial2",
            fallbackText = $"Правила {clrOrg}'Русского'{endClr} варианта отличаются — там карты собираются строго в масть.\nДавайте перенесем ту же {clrBlk}Десятку Пик (10♠){endClr} на {clrBlk}Валета Пик (J♠){endClr}, чтобы посмотреть, как это работает.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Spades_10",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_Tableau_1" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 1
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "YukonTutorial3",
            fallbackText = $"Отлично! Теперь мы знаем оба варианта. Давайте отменим этот ход и продолжим обучение по классическим правилам.\nНажмите кнопку {clrOrg}'Отмена'{endClr}.",
            expectedAction = TutorialActionType.Undo,
            expectedCardName = "Card_Spades_10",
            expectedTargetPileName = "",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_UndoButton" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 2
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "YukonTutorial4",
            fallbackText = $"Главная особенность Юкона — перенос стопок. Можно брать ЛЮБУЮ открытую карту и перетащить всю огромную стопку над ней!\nВозьмите черную {clrBlk}Даму Треф (Q♣){endClr} из последней колонки и перенесите на {clrRed}Короля Червей (K♥){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Clubs_12",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            // ИЗМЕНЕНИЕ: Подсвечиваем 2-ю и 6-ю (последнюю) колонки
            highlightElements = new List<string> { "Highlight_Tableau_2", "Highlight_Tableau_6" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 3
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "YukonTutorial5",
            fallbackText = $"Карты можно быстро отправлять в {clrOrg}Дома{endClr} двойным кликом.\nОтправим туда {clrBlk}Десятку Треф (10♣){endClr}!",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Card_Clubs_10",
            expectedTargetPileName = "Foundation",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_Foundation" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 4
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "YukonTutorial6",
            fallbackText = $"В пустые ячейки на поле можно класть {clrOrg}ТОЛЬКО{endClr} Королей.\nПеренесите {clrBlk}Короля Пик (K♠){endClr} в пустую колонку, чтобы открыть карту под ним.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Spades_13",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_EmptyTableau" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 5
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "YukonTutorial7",
            fallbackText = $"Вы освоили все механики! На столе больше нет закрытых карт.\nНажмите кнопку {clrOrg}'АВТО'{endClr}, чтобы быстро завершить игру!",
            expectedAction = TutorialActionType.ClickAuto,
            expectedCardName = "",
            expectedTargetPileName = "",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_AutoWinButton" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 6
        });
    }
}