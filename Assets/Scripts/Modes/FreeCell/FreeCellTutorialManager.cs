using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class FreeCellTutorialManager : MonoBehaviour, ITutorialManager
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
    public Button undoButton;
    public Button autoButton;

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

    private FreeCellModeManager modeManager;
    private Coroutine panelMoveCoroutine;

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

        modeManager = GetComponent<FreeCellModeManager>();
        if (modeManager != null) modeManager.tutorialManager = this;

        steps.Clear();
        InitializeDefaultSteps();
    }

    private void LateUpdate()
    {
        if (!IsTutorialActive || stepTransitioning || currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];

        FreeCellCardController expectedCard = FindCardByName(step.expectedCardName);
        var allCards = FindObjectsOfType<FreeCellCardController>();

        if (undoButton != null) undoButton.interactable = (step.expectedAction == TutorialActionType.Undo);
        if (autoButton != null) autoButton.interactable = (step.expectedAction == TutorialActionType.ClickAuto);

        foreach (var c in allCards)
        {
            if (c.canvasGroup == null) continue;

            bool isFlying = c.GetComponentInParent<ICardContainer>() == null;
            if (isFlying) continue;

            if (step.expectedAction == TutorialActionType.MoveCard)
            {
                c.canvasGroup.blocksRaycasts = (expectedCard != null && c == expectedCard);
            }
            else
            {
                c.canvasGroup.blocksRaycasts = false;
            }
        }

        if (step.expectedAction == TutorialActionType.MoveCard && expectedCard != null)
        {
            ICardContainer currentContainer = expectedCard.GetComponentInParent<ICardContainer>();

            if (currentContainer != null && currentContainer != initialCardContainer && !modeManager.IsGrabbing)
            {
                bool isCorrectDestination = false;

                if (step.expectedTargetPileName == "FreeCell" && currentContainer is FreeCellPile)
                {
                    isCorrectDestination = true;
                }
                else if (currentContainer is TableauPile tp)
                {
                    if (step.expectedTargetPileName == "EmptyTableau")
                    {
                        if (tp.cards.Count > 0 && tp.cards[0] == expectedCard) isCorrectDestination = true;
                    }
                    else
                    {
                        int cardIndex = tp.cards.IndexOf(expectedCard);
                        if (cardIndex > 0)
                        {
                            string underName = tp.cards[cardIndex - 1].gameObject.name;
                            if (step.localizationKey == "FreeCellTutorial3" && underName.Contains("Clubs_8")) isCorrectDestination = true;
                            if (step.localizationKey == "FreeCellTutorial7" && underName.Contains("Diamonds_7")) isCorrectDestination = true;
                        }
                    }
                }

                if (isCorrectDestination) AdvanceStep();
                else if (modeManager != null && modeManager.undoManager != null) StartCoroutine(modeManager.undoManager.UndoLastCoroutine());
            }
        }
        else if (step.expectedAction == TutorialActionType.Undo)
        {
            if (expectedCard != null)
            {
                ICardContainer currentContainer = expectedCard.GetComponentInParent<ICardContainer>();
                if (currentContainer != null && currentContainer != initialCardContainer) AdvanceStep();
            }
        }
    }

    private IEnumerator AdvanceStepRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        InternalAdvanceStep();
        stepTransitioning = false;
    }

    private FreeCellCardController FindCardByName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;
        var allCards = FindObjectsOfType<FreeCellCardController>();
        foreach (var c in allCards)
        {
            if (c.gameObject.name == exactName || c.gameObject.name == "Card_" + exactName) return c;
        }
        return null;
    }

    public void AdvanceStep()
    {
        if (!stepTransitioning && IsTutorialActive && gameObject.activeInHierarchy)
        {
            stepTransitioning = true;
            StartCoroutine(AdvanceStepRoutine());
        }
    }

    public bool IsActionAllowed(TutorialActionType action, CardController card = null, ICardContainer target = null)
    {
        if (!IsTutorialActive || currentStepIndex >= steps.Count) return true;

        TutorialStep step = steps[currentStepIndex];

        if (action != step.expectedAction) return false;

        if (action == TutorialActionType.ClickAuto)
        {
            AdvanceStep();

            if (step.localizationKey == "FreeCellTutorial2") StartCoroutine(ForceTutorialAutoMove());
            else if (step.localizationKey == "FreeCellTutorial8") StartCoroutine(ForceTutorialWin());

            return true;
        }

        if (action == TutorialActionType.MoveCard)
        {
            if (card != null && !card.gameObject.name.Contains(step.expectedCardName)) return false;

            if (target != null)
            {
                if (step.expectedTargetPileName == "FreeCell" && !(target is FreeCellPile)) return false;

                if (target is TableauPile tp)
                {
                    if (step.expectedTargetPileName == "EmptyTableau" && tp.cards.Count > 0) return false;

                    if (tp.cards.Count > 0)
                    {
                        CardController topCard = tp.cards[tp.cards.Count - 1];
                        string topName = topCard.gameObject.name;

                        if (step.localizationKey == "FreeCellTutorial3" && !topName.Contains("Clubs_8")) return false;
                        if (step.localizationKey == "FreeCellTutorial7" && !topName.Contains("Diamonds_7")) return false;
                    }
                }
            }
        }

        return true;
    }

    private IEnumerator ForceTutorialAutoMove()
    {
        modeManager.IsInputAllowed = false;

        string[] cardsToMove = { "Spades_1", "Spades_2", "Spades_3", "Spades_4", "Spades_5" };

        FoundationPile targetFoundation = null;
        foreach (var f in modeManager.pileManager.Foundations)
        {
            if (f.gameObject.name == "F0") { targetFoundation = f; break; }
        }
        if (targetFoundation == null) targetFoundation = modeManager.pileManager.Foundations[0];

        foreach (string cardName in cardsToMove)
        {
            var card = FindCardByName(cardName);
            if (card != null)
            {
                var source = card.GetComponentInParent<ICardContainer>();
                if (source is TableauPile tab)
                {
                    int idx = tab.cards.IndexOf(card);
                    if (idx != -1) tab.RemoveSequenceFrom(idx);
                }

                card.rectTransform.SetParent(modeManager.DragLayer, true);
                card.rectTransform.SetAsLastSibling();
                targetFoundation.ReserveCard(card);
                card.ForceSnapToContainer(targetFoundation);

                if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

                yield return new WaitForSeconds(0.15f);
            }
        }

        modeManager.IsInputAllowed = true;
    }

    private IEnumerator ForceTutorialWin()
    {
        modeManager.IsInputAllowed = false;
        var pm = modeManager.pileManager;

        bool moved = true;
        while (moved)
        {
            moved = false;

            List<FreeCellCardController> cardsToFly = new List<FreeCellCardController>();

            foreach (var tab in pm.Tableau)
                if (tab.cards.Count > 0) cardsToFly.Add(tab.cards[tab.cards.Count - 1] as FreeCellCardController);

            foreach (var fc in pm.FreeCells)
                if (!fc.IsEmpty) cardsToFly.Add(fc.GetComponentInChildren<FreeCellCardController>());

            foreach (var card in cardsToFly)
            {
                if (card == null) continue;

                string targetName = "F0";
                switch (card.cardModel.suit)
                {
                    case Suit.Spades: targetName = "F0"; break;
                    case Suit.Hearts: targetName = "F1"; break;
                    case Suit.Clubs: targetName = "F2"; break;
                    case Suit.Diamonds: targetName = "F3"; break;
                }

                FoundationPile targetFoundation = null;
                foreach (var f in pm.Foundations)
                {
                    if (f.gameObject.name == targetName) { targetFoundation = f; break; }
                }

                if (targetFoundation != null && targetFoundation.CanAccept(card))
                {
                    var source = card.GetComponentInParent<ICardContainer>();
                    if (source is TableauPile tab) tab.RemoveSequenceFrom(tab.cards.Count - 1);

                    card.rectTransform.SetParent(modeManager.DragLayer, true);
                    card.rectTransform.SetAsLastSibling();
                    targetFoundation.ReserveCard(card);
                    card.ForceSnapToContainer(targetFoundation);

                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

                    yield return new WaitForSeconds(0.05f);
                    moved = true;
                    break;
                }
            }
        }

        yield return new WaitForSeconds(0.5f);

        if (modeManager.gameUI != null)
            modeManager.gameUI.OnGameWon(0);
    }

    private void InternalAdvanceStep()
    {
        if (!IsTutorialActive) return;

        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            string endText = "Обучение завершено! Отличная игра!";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue("FreeCellTutorialEnd");
                if (!string.IsNullOrEmpty(loc) && loc != "FreeCellTutorialEnd") endText = loc;
            }
            if (instructionText != null) instructionText.text = endText;

            if (stepIndicatorText != null) stepIndicatorText.text = "";
            DisableAllHighlights();

            if (tutorialUIPanel != null)
            {
                if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
                panelMoveCoroutine = StartCoroutine(MovePanelRoutine(GetAnchorRect(TutorialPanelAnchorType.Top)));
            }

            IsTutorialActive = false;
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

        FreeCellCardController expectedCard = FindCardByName(step.expectedCardName);
        initialCardContainer = expectedCard != null ? expectedCard.GetComponentInParent<ICardContainer>() : null;

        if (instructionText != null)
        {
            string textToShow = step.fallbackText;
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue(step.localizationKey);
                if (!string.IsNullOrEmpty(loc) && loc != step.localizationKey)
                {
                    textToShow = loc;
                }
            }
            instructionText.text = textToShow;
        }

        if (stepIndicatorText != null) stepIndicatorText.text = $"{currentStepIndex + 1}/{steps.Count}";

        if (tutorialUIPanel != null)
        {
            if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
            panelMoveCoroutine = StartCoroutine(MovePanelRoutine(GetAnchorRect(step.panelAnchor)));
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
            float t = 1f - Mathf.Pow(1f - (elapsed / duration), 3f);
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
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startAnchoredPos, targetAnchoredPos, elapsed / duration);
            yield return null;
        }
        tutorialUIPanel.anchoredPosition = targetAnchoredPos;
        tutorialUIPanel.gameObject.SetActive(false);
    }

    public void RestorePanelPosition()
    {
        if (!IsTutorialActive) return;
        tutorialUIPanel.gameObject.SetActive(true);
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
        if (undoButton != null) undoButton.interactable = true;
        if (autoButton != null) autoButton.interactable = true;

        var allCards = FindObjectsOfType<FreeCellCardController>();
        foreach (var c in allCards)
        {
            if (c.canvasGroup != null) c.canvasGroup.blocksRaycasts = true;
        }
    }

    // ==========================================
    // ГЕНЕРАЦИЯ ИДЕАЛЬНОГО РАСКЛАДА (52 КАРТЫ)
    // ==========================================
    public IEnumerator PlayTutorialIntro(Deal dummyDeal)
    {
        modeManager.IsInputAllowed = false;

        if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);
        DisableAllHighlights();

        if (modeManager.introController != null) modeManager.introController.PrepareIntro(false);

        var pm = modeManager.pileManager;
        pm.ClearAllPiles();
        modeManager.cardFactory.DestroyAllCards();

        // 1. ДОМА (17 карт) - уже лежат на старте
        string[] F_Spades = { };
        string[] F_Hearts = { "Hearts_1_up", "Hearts_2_up", "Hearts_3_up", "Hearts_4_up" };
        string[] F_Clubs = { "Clubs_1_up", "Clubs_2_up", "Clubs_3_up", "Clubs_4_up", "Clubs_5_up", "Clubs_6_up", "Clubs_7_up" };
        string[] F_Diamonds = { "Diamonds_1_up", "Diamonds_2_up", "Diamonds_3_up", "Diamonds_4_up", "Diamonds_5_up", "Diamonds_6_up" };

        FoundationPile f0 = null, f1 = null, f2 = null, f3 = null;
        foreach (var f in pm.Foundations)
        {
            if (f.gameObject.name == "F0") f0 = f;
            else if (f.gameObject.name == "F1") f1 = f;
            else if (f.gameObject.name == "F2") f2 = f;
            else if (f.gameObject.name == "F3") f3 = f;
        }

        if (f0 != null) SpawnAndPlaceCards(F_Spades, f0);
        if (f1 != null) SpawnAndPlaceCards(F_Hearts, f1);
        if (f2 != null) SpawnAndPlaceCards(F_Clubs, f2);
        if (f3 != null) SpawnAndPlaceCards(F_Diamonds, f3);

        // 2. СТОЛ (Остальные 35 карт, сложенные в правильном чередующемся порядке)
        string[] T0 = { "Spades_13_up", "Hearts_12_up", "Spades_11_up", "Hearts_10_up", "Spades_9_up", "Diamonds_8_up", "Spades_1_up", "Hearts_8_up" };
        string[] T1 = { "Hearts_13_up", "Clubs_12_up", "Hearts_11_up", "Clubs_10_up", "Diamonds_9_up", "Spades_8_up", "Diamonds_7_up" };
        string[] T2 = { "Clubs_13_up", "Diamonds_12_up", "Clubs_9_up", "Clubs_8_up" };
        string[] T3 = { "Diamonds_13_up", "Spades_12_up", "Diamonds_11_up", "Spades_10_up", "Hearts_7_up", "Spades_6_up", "Hearts_5_up" };
        string[] T4 = { "Diamonds_10_up" }; // Идеально очищается на Шаге 4
        string[] T5 = { "Clubs_11_up", "Hearts_6_up", "Spades_5_up", "Spades_4_up" };
        string[] T6 = { "Hearts_9_up", "Spades_2_up" };
        string[] T7 = { "Spades_7_up", "Spades_3_up" };

        SpawnAndPlaceCards(T0, pm.Tableau[0]);
        SpawnAndPlaceCards(T1, pm.Tableau[1]);
        SpawnAndPlaceCards(T2, pm.Tableau[2]);
        SpawnAndPlaceCards(T3, pm.Tableau[3]);
        SpawnAndPlaceCards(T4, pm.Tableau[4]);
        SpawnAndPlaceCards(T5, pm.Tableau[5]);
        SpawnAndPlaceCards(T6, pm.Tableau[6]);
        SpawnAndPlaceCards(T7, pm.Tableau[7]);

        foreach (var tPile in pm.Tableau)
        {
            if (tPile != null)
            {
                tPile.StopAllCoroutines();
                for (int i = 0; i < tPile.cards.Count; i++)
                {
                    tPile.cards[i].rectTransform.anchoredPosition = new Vector2(0, -i * modeManager.TableauVerticalGap);
                }
            }
        }

        Canvas.ForceUpdateCanvases();
        yield return null;

        List<ICardContainer> animSequence = new List<ICardContainer>();
        animSequence.AddRange(pm.Tableau);
        animSequence.AddRange(pm.Foundations);

        Vector2 offScreenOffset = new Vector2(0, 1500f);
        Dictionary<CardController, Vector2> targetAnchors = new Dictionary<CardController, Vector2>();

        foreach (var container in animSequence)
        {
            var mono = container as MonoBehaviour;
            if (mono == null) continue;
            foreach (Transform child in mono.transform)
            {
                var card = child.GetComponent<FreeCellCardController>();
                if (card != null)
                {
                    targetAnchors[card] = card.rectTransform.anchoredPosition;
                    card.rectTransform.anchoredPosition += offScreenOffset;
                    if (card.canvasGroup != null) card.canvasGroup.alpha = 1f;
                }
            }
        }

        if (modeManager.introController != null) yield return StartCoroutine(modeManager.introController.PlayIntroSequence(null, false));

        foreach (var container in animSequence)
        {
            var mono = container as MonoBehaviour;
            if (mono == null) continue;
            var cardsInStack = new List<FreeCellCardController>();
            foreach (Transform child in mono.transform)
            {
                var card = child.GetComponent<FreeCellCardController>();
                if (card != null) cardsInStack.Add(card);
            }
            if (cardsInStack.Count > 0) StartCoroutine(AnimateStackDrop(cardsInStack, targetAnchors, offScreenOffset, 0.3f));
            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(0.3f);
        modeManager.AnimationService.ReorderAllContainers(pm.GetAllContainerTransforms());

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

    private IEnumerator AnimateStackDrop(List<FreeCellCardController> cardsInStack, Dictionary<CardController, Vector2> targetAnchors, Vector2 offScreenOffset, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - (elapsed / duration), 3f);
            foreach (var card in cardsInStack)
                if (card != null) card.rectTransform.anchoredPosition = Vector2.Lerp(targetAnchors[card] + offScreenOffset, targetAnchors[card], t);
            yield return null;
        }
        foreach (var card in cardsInStack)
            if (card != null) card.rectTransform.anchoredPosition = targetAnchors[card];
    }

    private void SpawnAndPlaceCards(string[] cardData, ICardContainer target)
    {
        foreach (var data in cardData)
        {
            string[] parts = data.Split('_');
            if (parts.Length < 2) continue;

            string suitStr = parts[0];
            int rank = int.Parse(parts[1]);
            bool isFaceUp = parts.Length > 2 && parts[2] == "up";

            Suit suit = Suit.Spades;
            if (suitStr == "Hearts") suit = Suit.Hearts;
            else if (suitStr == "Clubs") suit = Suit.Clubs;
            else if (suitStr == "Diamonds") suit = Suit.Diamonds;

            CardModel model = new CardModel(suit, rank);

            CardController baseCard = modeManager.cardFactory.CreateCard(model, target.Transform, Vector2.zero);
            FreeCellCardController card = baseCard as FreeCellCardController ?? baseCard.GetComponent<FreeCellCardController>();

            card.gameObject.name = $"Card_{suitStr}_{rank}";
            if (card.canvasGroup != null) card.canvasGroup.alpha = 0f;
            FindObjectOfType<DragManager>()?.RegisterCardEvents(card);

            if (target is TableauPile tp)
            {
                tp.AddCard(card, isFaceUp);
            }
            else if (target is FoundationPile fp)
            {
                fp.AcceptCard(card);
                card.GetComponent<CardData>()?.SetFaceUp(isFaceUp, false);
                card.rectTransform.anchoredPosition = Vector2.zero;
            }
            else if (target is FreeCellPile fc)
            {
                fc.AcceptCard(card);
                card.GetComponent<CardData>()?.SetFaceUp(isFaceUp, false);
                card.rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    private void InitializeDefaultSteps()
    {
        steps.Add(new TutorialStep
        {
            localizationKey = "FreeCellTutorial1",
            fallbackText = "Пиковый Туз заблокирован. Перемести Червонную Восьмерку (8♥) в Свободную ячейку (слева вверху).",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Hearts_8",
            expectedTargetPileName = "FreeCell",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Cells" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 0
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "FreeCellTutorial2",
            fallbackText = "Туз свободен! Нажми «Авто», чтобы отправить все доступные карты в Дома.",
            expectedAction = TutorialActionType.ClickAuto,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Foundation" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 1
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "FreeCellTutorial3",
            fallbackText = "На столе карты чередуются по цвету. Перемести Бубновую Семерку (7♦) на Трефовую Восьмерку (8♣).",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Diamonds_7",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 2
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "FreeCellTutorial4",
            fallbackText = "Индикатор у кнопки Авто показывает макс. длину стопки (сейчас 4). Перемести 10♦ в свободную ячейку, чтобы освободить колонку.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Diamonds_10",
            expectedTargetPileName = "FreeCell",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Indicator" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 3
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "FreeCellTutorial5",
            fallbackText = "Индикатор изменился (6/3). Второе число — лимит для пустой колонки. Перенеси стопку (6♠ и 5♥) в пустую колонку.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Spades_6",
            expectedTargetPileName = "EmptyTableau",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Indicator", "Highlight_Tableau_4" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 4
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "FreeCellTutorial6",
            fallbackText = "Любой ход можно отменить. Давайте проверим как работает кнопка <color=#FCA311>'Отмена'</color> внизу экрана.",
            expectedAction = TutorialActionType.Undo,
            expectedCardName = "Spades_6",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_UndoButton" },
            
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "FreeCellTutorial7",
            fallbackText = "Теперь перенеси эту стопку (6♠ и 5♥) на Бубновую Семерку (7♦).",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Spades_6",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 4
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "FreeCellTutorial8",
            fallbackText = "Обучение пройдено! Жми «Авто», чтобы собрать оставшиеся карты и победить!",
            expectedAction = TutorialActionType.ClickAuto,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_AutoButton" },
           
        });
    }
}