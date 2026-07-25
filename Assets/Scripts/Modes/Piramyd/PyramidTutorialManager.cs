using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class PyramidTutorialManager : MonoBehaviour, ITutorialManager
{
    [Header("UI")]
    public RectTransform tutorialUIPanel;
    public TMP_Text instructionText;
    public TMP_Text stepIndicatorText;

    [Header("Panel Anchors")]
    public RectTransform topAnchor;
    public RectTransform centerAnchor;
    public RectTransform bottomAnchor;

    [Header("Arrows")]
    public RectTransform arrowDown;
    public RectTransform arrowUp;
    public RectTransform arrowLeft;
    public RectTransform[] arrowAnchors = new RectTransform[12];

    [Header("Arrow Animation")]
    public float arrowBounceAmplitude = 12f;
    public float arrowBounceSpeed = 6f;

    [Header("Highlights")]
    public List<GameObject> highlightObjects = new List<GameObject>();

    [Header("Sequence")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    public bool IsTutorialActive { get; private set; }
    private int currentStepIndex = 0;

    private PyramidModeManager modeManager;
    private Coroutine panelMoveCoroutine;

    private int initialStockCount;
    private bool stepTransitioning = false;
    private bool isIntroAnimating = false; // <--- НОВОЕ
    private RectTransform activeArrow;
    private TutorialArrowType activeArrowType;

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

        modeManager = GetComponent<PyramidModeManager>();
        if (modeManager != null) modeManager.tutorialManager = this;

        steps.Clear();
        InitializeDefaultSteps();
    }
    private void Update()
    {
        // Анимация стрелки (только если она есть)
        if (activeArrow != null && activeArrow.gameObject.activeInHierarchy)
        {
            float offset = Mathf.Sin(Time.unscaledTime * arrowBounceSpeed) * arrowBounceAmplitude;
            activeArrow.anchoredPosition = (activeArrowType == TutorialArrowType.Left) ? new Vector2(offset, 0) : new Vector2(0, offset);
        }
    }
    public bool IsActionAllowed(TutorialActionType action)
    {
        if (!IsTutorialActive || currentStepIndex >= steps.Count) return true;
        return steps[currentStepIndex].expectedAction == action;
    }

    private void LateUpdate()
    {
        if (!IsTutorialActive || stepTransitioning || isIntroAnimating || currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];
        CardController expectedCard1 = FindCardByName(step.expectedCardName);
        CardController expectedCard2 = FindCardByName(step.expectedTargetPileName);

        var allCards = FindObjectsOfType<CardController>(true);

        // 2. УПРАВЛЕНИЕ КЛИКАБЕЛЬНОСТЬЮ КАРТ
        foreach (var c in allCards)
        {
            if (c.canvasGroup == null) continue;

            if (step.expectedAction == TutorialActionType.MoveCard)
            {
                bool isTargetCard = (expectedCard1 != null && c == expectedCard1) ||
                                    (expectedCard2 != null && c == expectedCard2);
                c.canvasGroup.blocksRaycasts = isTargetCard;
            }
            else
            {
                c.canvasGroup.blocksRaycasts = false;
            }
        }

        var helpController = FindObjectOfType<PyramidHelpPanelController>();
        if (helpController != null && helpController.toggleButton != null)
        {
            helpController.toggleButton.interactable = (step.expectedAction == TutorialActionType.DoubleClick);
        }

        var globalUndo = FindObjectOfType<UndoManager>();
        if (globalUndo != null)
        {
            if (globalUndo.undoButton != null)
                globalUndo.undoButton.interactable = (step.expectedAction == TutorialActionType.Undo);
            if (globalUndo.undoAllButton != null)
                globalUndo.undoAllButton.interactable = false;
        }

        // 3. ПРОВЕРКА ВЫПОЛНЕНИЯ ШАГА
        bool shouldAdvance = false;

        if (step.expectedAction == TutorialActionType.MoveCard)
        {
            if (expectedCard1 == null || !expectedCard1.gameObject.activeInHierarchy)
                shouldAdvance = true;
        }
        else if (step.expectedAction == TutorialActionType.ClickStock)
        {
            if (modeManager != null && modeManager.pileManager.Stock != null)
            {
                if (modeManager.pileManager.Stock.Count < initialStockCount) shouldAdvance = true;
            }
        }
        else if (step.expectedAction == TutorialActionType.DoubleClick)
        {
            if (helpController != null && helpController.helpPanel != null && helpController.helpPanel.activeInHierarchy)
                shouldAdvance = true;
        }
        else if (step.expectedAction == TutorialActionType.Undo)
        {
            if (expectedCard1 != null && expectedCard1.gameObject.activeInHierarchy)
                shouldAdvance = true;
        }

        if (shouldAdvance)
        {
            AdvanceStep();
        }
    }

    // --- НОВАЯ СИСТЕМА УПРАВЛЕНИЯ СТРЕЛКАМИ ---
    public void OnCardSelectionChanged(CardController selectedCard)
    {
        if (!IsTutorialActive || stepTransitioning || isIntroAnimating || currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];

        // Если у шага нет второй стрелки, прыгать не нужно
        if (step.arrowAnchorIndex2 < 0) return;

        CardController expectedCard1 = FindCardByName(step.expectedCardName);
        CardController expectedCard2 = FindCardByName(step.expectedTargetPileName);

        int targetIndex = step.arrowAnchorIndex;
        TutorialArrowType targetArrowType = step.arrowType;

        // Если выбрали первую карту -> стрелка прыгает на вторую
        if (selectedCard != null && selectedCard == expectedCard1)
        {
            targetIndex = step.arrowAnchorIndex2;
            targetArrowType = step.arrowType2 != TutorialArrowType.None ? step.arrowType2 : step.arrowType;
        }
        // Если выбрали вторую карту -> стрелка прыгает на первую
        else if (selectedCard != null && selectedCard == expectedCard2)
        {
            targetIndex = step.arrowAnchorIndex;
            targetArrowType = step.arrowType;
        }

        ApplyArrow(targetArrowType, targetIndex);
    }
    private void ApplyArrow(TutorialArrowType type, int anchorIndex)
    {
        if (arrowDown) arrowDown.gameObject.SetActive(false);
        if (arrowUp) arrowUp.gameObject.SetActive(false);
        if (arrowLeft) arrowLeft.gameObject.SetActive(false);

        activeArrow = null;

        if (type != TutorialArrowType.None && anchorIndex >= 0 && anchorIndex < arrowAnchors.Length)
        {
            activeArrowType = type;
            activeArrow = type == TutorialArrowType.Down ? arrowDown : (type == TutorialArrowType.Up ? arrowUp : arrowLeft);
            RectTransform targetAnchor = arrowAnchors[anchorIndex];

            if (activeArrow != null && targetAnchor != null)
            {
                activeArrow.gameObject.SetActive(true);
                activeArrow.SetParent(targetAnchor, false);
                activeArrow.anchoredPosition = Vector2.zero;
            }
        }
    }

    private CardController FindCardByName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;
        var allCards = FindObjectsOfType<CardController>(true);
        foreach (var c in allCards)
        {
            if (c.gameObject.name.Contains(exactName)) return c;
        }
        return null;
    }

    public void AdvanceStep()
    {
        if (!stepTransitioning && IsTutorialActive && gameObject.activeInHierarchy)
        {
            stepTransitioning = true;

            // СРАЗУ ПРЯЧЕМ СТРЕЛКИ, ЧТОБЫ ИЗБЕЖАТЬ МЕРЦАНИЯ
            if (arrowDown) arrowDown.gameObject.SetActive(false);
            if (arrowUp) arrowUp.gameObject.SetActive(false);
            if (arrowLeft) arrowLeft.gameObject.SetActive(false);
            activeArrow = null;

            StartCoroutine(AdvanceStepRoutine());
        }
    }
    private IEnumerator AdvanceStepRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        InternalAdvanceStep();
        stepTransitioning = false;

        // Включаем стрелки нового шага
        if (IsTutorialActive) UpdateUI();
    }

    private void InternalAdvanceStep()
    {
        if (!IsTutorialActive) return;

        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            string endText = "Поздравляем! Вы освоили Пирамиду!";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue("PyramidTutorialEnd");
                if (!string.IsNullOrEmpty(loc)) endText = loc;
            }
            if (instructionText != null) instructionText.text = endText;
            if (stepIndicatorText != null) stepIndicatorText.text = "";
            DisableAllHighlights();

            if (tutorialUIPanel != null)
            {
                if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
                panelMoveCoroutine = StartCoroutine(MovePanelRoutine(GetAnchorRect(TutorialPanelAnchorType.Center)));
            }

            IsTutorialActive = false;
        }
        else UpdateUI();
    }

    private void UpdateUI()
    {
        if (currentStepIndex >= steps.Count || stepTransitioning || isIntroAnimating) return;

        TutorialStep step = steps[currentStepIndex];

        if (modeManager != null && modeManager.pileManager.Stock != null)
            initialStockCount = modeManager.pileManager.Stock.Count;

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
            panelMoveCoroutine = StartCoroutine(MovePanelRoutine(GetAnchorRect(step.panelAnchor)));
        }

        // Включаем базовую стрелку для текущего шага
        ApplyArrow(step.arrowType, step.arrowAnchorIndex);

        foreach (var highlight in highlightObjects)
        {
            if (highlight == null) continue;
            bool shouldBeActive = step.highlightElements != null && step.highlightElements.Contains(highlight.name);
            highlight.SetActive(shouldBeActive);
        }
    }

    private RectTransform GetAnchorRect(TutorialPanelAnchorType type)
    {
        return type == TutorialPanelAnchorType.Top ? topAnchor : (type == TutorialPanelAnchorType.Bottom ? bottomAnchor : centerAnchor);
    }

    private IEnumerator MovePanelRoutine(RectTransform targetRect)
    {
        if (targetRect == null) yield break;

        Vector2 startPos = tutorialUIPanel.anchoredPosition;
        Vector2 targetPos = targetRect.anchoredPosition;

        tutorialUIPanel.anchorMin = targetRect.anchorMin;
        tutorialUIPanel.anchorMax = targetRect.anchorMax;
        tutorialUIPanel.pivot = targetRect.pivot;
        tutorialUIPanel.sizeDelta = targetRect.sizeDelta;

        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - (elapsed / duration), 3f);
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        tutorialUIPanel.anchoredPosition = targetPos;
    }

    public void HidePanelToLeft()
    {
        if (tutorialUIPanel != null) tutorialUIPanel.gameObject.SetActive(false);
        if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
        HideHighlights();
    }

    public void RestorePanelPosition() { if (!IsTutorialActive) return; UpdateUI(); }

    public void HideHighlights()
    {
        DisableAllHighlights();
        UnlockAllElements();
    }

    private void DisableAllHighlights()
    {
        foreach (var highlight in highlightObjects) if (highlight != null) highlight.SetActive(false);
        if (arrowDown) arrowDown.gameObject.SetActive(false);
        if (arrowUp) arrowUp.gameObject.SetActive(false);
        if (arrowLeft) arrowLeft.gameObject.SetActive(false);
        activeArrow = null;
    }

    private void UnlockAllElements()
    {
        IsTutorialActive = false;
        var allCards = FindObjectsOfType<CardController>(true);
        foreach (var c in allCards) if (c.canvasGroup != null) c.canvasGroup.blocksRaycasts = true;
    }

    private void OnDisable() { HidePanelToLeft(); UnlockAllElements(); }
    private void OnDestroy() { if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false); }

    public IEnumerator PlayTutorialIntro(Deal dummyDeal)
    {
        isIntroAnimating = true;
        modeManager.IsInputAllowed = false;
        if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);
        DisableAllHighlights();

        if (modeManager.introController != null) modeManager.introController.PrepareIntro(false);

        modeManager.deckManager.ClearBoard();
        modeManager.deckManager.cardFactory.DestroyAllCards();

        string[] PyramidCards = {
            "Hearts_7_up",     // Слот 0: Вершина (Семерка)
            "Spades_8_up",     // Слот 1: 2 ряд (лево)
            "Clubs_4_up",      // Слот 2: 2 ряд (право)
            "Diamonds_12_up",  // Слот 3: 3 ряд (лево)
            "Clubs_1_up",      // Слот 4: 3 ряд (центр)
            "Spades_13_up"     // Слот 5: 3 ряд (право)
        };

        string[] StockCards = { "Hearts_9_up", "Spades_5_up", "Diamonds_6_up" };

        List<CardController> allSpawnedCards = new List<CardController>();

        for (int i = 0; i < PyramidCards.Length && i < modeManager.pileManager.TableauSlots.Count; i++)
        {
            var card = CreateTutorialCard(PyramidCards[i]);
            var slot = modeManager.pileManager.TableauSlots[i];

            card.transform.SetParent(slot.transform, false);
            card.transform.localPosition = Vector3.zero;

            slot.Card = card;

            var info = card.gameObject.AddComponent<CardInfoStorage>();
            info.LinkedSlot = slot.transform;

            allSpawnedCards.Add(card);
        }

        foreach (var stockStr in StockCards)
        {
            var card = CreateTutorialCard(stockStr);
            modeManager.pileManager.Stock.Add(card);
            allSpawnedCards.Add(card);
        }

        Canvas.ForceUpdateCanvases();
        yield return null;

        if (modeManager.introController != null) yield return StartCoroutine(modeManager.introController.PlayIntroSequence());

        Vector3 offScreenOffset = new Vector3(0, 2500f, 0);
        Dictionary<CardController, Vector3> targetLocals = new Dictionary<CardController, Vector3>();

        foreach (var card in allSpawnedCards)
        {
            targetLocals[card] = card.transform.localPosition;
            card.transform.localPosition += offScreenOffset;
            if (card.canvasGroup != null) card.canvasGroup.alpha = 1f;
        }

        yield return StartCoroutine(AnimateStackDrop(allSpawnedCards, targetLocals, offScreenOffset, 0.4f));

        modeManager.pileManager.UpdateLocks();

        currentStepIndex = 0;
        if (tutorialUIPanel)
        {
            RectTransform targetAnchor = GetAnchorRect(steps.Count > 0 ? steps[0].panelAnchor : TutorialPanelAnchorType.Bottom);
            tutorialUIPanel.anchorMin = targetAnchor.anchorMin;
            tutorialUIPanel.anchorMax = targetAnchor.anchorMax;
            tutorialUIPanel.pivot = targetAnchor.pivot;
            tutorialUIPanel.sizeDelta = targetAnchor.sizeDelta;
            tutorialUIPanel.anchoredPosition = targetAnchor.anchoredPosition + new Vector2(2500f, 0);
            tutorialUIPanel.gameObject.SetActive(true);
        }
        isIntroAnimating = false;
        UpdateUI();
        modeManager.IsInputAllowed = true;
    }

    private CardController CreateTutorialCard(string dataStr)
    {
        string[] parts = dataStr.Split('_');
        string suitStr = parts[0];
        int rank = int.Parse(parts[1]);
        bool isFaceUp = parts[2] == "up";

        Suit suit = Suit.Spades;
        if (suitStr == "Hearts") suit = Suit.Hearts;
        else if (suitStr == "Clubs") suit = Suit.Clubs;
        else if (suitStr == "Diamonds") suit = Suit.Diamonds;

        CardModel model = new CardModel(suit, rank);
        CardController card = modeManager.deckManager.cardFactory.CreateCard(model, modeManager.animManager.dragLayerRect, Vector2.zero);

        card.gameObject.name = $"Card_{suitStr}_{rank}";
        card.CardmodeManager = modeManager;
        card.OnClicked += modeManager.OnCardClicked;

        var cData = card.GetComponent<CardData>();
        if (cData)
        {
            cData.SetFaceUp(isFaceUp, false);
            if (cData.image) cData.image.color = Color.white;
        }

        if (card.canvasGroup != null) card.canvasGroup.alpha = 0f;

        return card;
    }

    private IEnumerator AnimateStackDrop(List<CardController> cardsInStack, Dictionary<CardController, Vector3> targetLocals, Vector3 offScreenOffset, float duration)
    {
        float elapsed = 0f;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Whoosh_In");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - (elapsed / duration), 3f);
            foreach (var card in cardsInStack)
            {
                if (card != null)
                {
                    card.transform.localPosition = Vector3.Lerp(targetLocals[card] + offScreenOffset, targetLocals[card], t);
                }
            }
            yield return null;
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");

        foreach (var card in cardsInStack)
        {
            if (card != null)
            {
                card.transform.localPosition = targetLocals[card];
            }
        }
    }

    private void InitializeDefaultSteps()
    {
        string clrOrg = "<color=#FCA311>";
        string clrBlk = "<color=#8294FF>";
        string clrRed = "<color=#FF6B6B>";
        string endClr = "</color>";

        // Шаг 1: Король (Стрелка ВВЕРХ, так как он на столе)
        steps.Add(new TutorialStep
        {
            localizationKey = "PyramidTutorial1",
            fallbackText = $"В Пирамиде нужно убирать карты парами, дающими в сумме {clrOrg}13{endClr}. Король равен 13, его можно убрать одним кликом.\nКликните на {clrBlk}Короля Пик(K♠){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Spades_13",
            panelAnchor = TutorialPanelAnchorType.Top,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 0
        });

        // Шаг 2: Дама + Туз (Оба на столе -> Обе стрелки ВВЕРХ)
        steps.Add(new TutorialStep
        {
            localizationKey = "PyramidTutorial2",
            fallbackText = $"Дама(12) + Туз(1) = {clrOrg}13{endClr}.\nКликните сначала на {clrRed}Даму Бубен(Q♦){endClr}, а затем на {clrBlk}Туза Треф(A♣){endClr}, чтобы убрать их.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Diamonds_12",
            expectedTargetPileName = "Clubs_1",
            panelAnchor = TutorialPanelAnchorType.Top,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 1,
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 2
        });

        // Шаг 3: Подсказка
        steps.Add(new TutorialStep
        {
            localizationKey = "PyramidTutorial3",
            fallbackText = $"Забыли какая карта сколько стоит? Нажмите кнопку {clrOrg}'Подсказка'{endClr} сверху.",
            expectedAction = TutorialActionType.DoubleClick,
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_HintButton" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 3
        });

        // Шаг 4: Кнопка раздачи
        steps.Add(new TutorialStep
        {
            localizationKey = "PyramidTutorial4",
            fallbackText = $"На столе нет подходящих пар. Нажмите на кнопку {clrOrg}'Сдать'{endClr} под колодой, чтобы открыть новые карты. Обратите внимание: доступно только {clrOrg}2 пересдачи{endClr}!",
            expectedAction = TutorialActionType.ClickStock,
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_Stock" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = -1
        });

        // Шаг 5: 5 (колода) + 8 (стол)
        steps.Add(new TutorialStep
        {
            localizationKey = "PyramidTutorial5",
            fallbackText = $"Отлично! Пятерка(5) + Восьмерка(8) = {clrOrg}13{endClr}.\nСоедините {clrBlk}Пятерку Пик(5♠){endClr} в колоде с {clrBlk}Восьмеркой Пик(8♠){endClr} на столе.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Spades_5",
            expectedTargetPileName = "Spades_8",
            panelAnchor = TutorialPanelAnchorType.Top,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 4,  // 5-ка в колоде (Down)
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 5   // 8-ка на столе (Up)
        });

        // Шаг 6: Отмена
        steps.Add(new TutorialStep
        {
            localizationKey = "PyramidTutorial6",
            fallbackText = $"Отличный ход! Но иногда действие нужно отменить. Нажмите кнопку {clrOrg}'Отмена'{endClr} внизу экрана.",
            expectedAction = TutorialActionType.Undo,
            expectedCardName = "Spades_5",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_UndoButton" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 6
        });

        // Шаг 7: Снова 5 + 8
        steps.Add(new TutorialStep
        {
            localizationKey = "PyramidTutorial7",
            fallbackText = $"Теперь давайте снова соединим {clrBlk}Пятерку Пик(5♠){endClr} и {clrBlk}Восьмерку Пик(8♠){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Spades_5",
            expectedTargetPileName = "Spades_8",
            panelAnchor = TutorialPanelAnchorType.Top,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 4,
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 5
        });

        // Шаг 8: 9 (колода) + 4 (стол)
        steps.Add(new TutorialStep
        {
            localizationKey = "PyramidTutorial8",
            fallbackText = $"Девятка(9) + Четверка(4) = {clrOrg}13{endClr}.\nСоедините {clrRed}Девятку Червей(9♥){endClr} в колоде с {clrBlk}Четверкой Треф(4♣){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Hearts_9",
            expectedTargetPileName = "Clubs_4",
            panelAnchor = TutorialPanelAnchorType.Top,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 4,  // 9-ка в колоде (Down)
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 7   // 4-ка на столе (Up)
        });

        // Шаг 9: Семерка + Шестерка
        steps.Add(new TutorialStep
        {
            localizationKey = "PyramidTutorial9",
            fallbackText = $"Путь свободен! Осталась Семерка на вершине и наша Шестерка в отбое.\nКликните по {clrRed}Шестерке Бубен(6♦){endClr} и {clrRed}Семерке Червей(7♥){endClr}!",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Diamonds_6",
            expectedTargetPileName = "Hearts_7",
            panelAnchor = TutorialPanelAnchorType.Top,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 8, // 6-ка в отбое (Down)
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 9  // 7-ка на вершине (Up)
        });
    }
}