using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TriPeaksTutorialManager : MonoBehaviour, ITutorialManager
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

    [Tooltip("Массив из 11 якорей: 0-8 для слотов стола, 9 для Колоды (Stock), 10 для кнопки Отмены (Undo).")]
    public RectTransform[] arrowAnchors = new RectTransform[11];

    [Header("Arrow Animation")]
    public float arrowBounceAmplitude = 12f;
    public float arrowBounceSpeed = 6f;

    [Header("Highlights")]
    public List<GameObject> highlightObjects = new List<GameObject>();

    [Header("Sequence")]
    public List<TutorialStepTriPeaks> steps = new List<TutorialStepTriPeaks>();

    public bool IsTutorialActive { get; private set; }
    private int currentStepIndex = 0;

    private TriPeaksModeManager modeManager;
    private Coroutine panelMoveCoroutine;

    private int initialStockCount;
    private bool stepTransitioning = false;
    private bool isIntroAnimating = false;

    private RectTransform activeArrow;
    private TutorialArrowType activeArrowType;
    private int currentActiveAnchorIndex = -1; // <--- Защита от спама позиции

    [System.Serializable]
    public class TutorialStepTriPeaks
    {
        public string localizationKey;
        [TextArea(2, 4)] public string fallbackText;

        public TutorialActionType expectedAction;
        public List<string> expectedCardNames = new List<string>();

        public TutorialPanelAnchorType panelAnchor;
        public List<string> highlightElements;

        public TutorialArrowType arrowType = TutorialArrowType.Down;
        public List<int> arrowAnchorIndices = new List<int>();
    }

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

        modeManager = GetComponent<TriPeaksModeManager>();
        if (modeManager != null) modeManager.tutorialManager = this;

        steps.Clear();
        InitializeSteps();
    }

    private void Update()
    {
        // Анимация стрелки (работает свободно, так как её больше никто не сбрасывает)
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

        TutorialStepTriPeaks step = steps[currentStepIndex];

        CardController currentExpectedCard = null;
        if (step.expectedAction == TutorialActionType.MoveCard)
        {
            currentExpectedCard = GetCurrentExpectedCard(step);
        }

        var allCards = FindObjectsOfType<CardController>(true);

        // 1. БЛОКИРОВКА КЛИКОВ ПО КАРТАМ
        foreach (var c in allCards)
        {
            if (c.canvasGroup == null) continue;

            if (step.expectedAction == TutorialActionType.MoveCard)
            {
                c.canvasGroup.blocksRaycasts = (currentExpectedCard != null && c == currentExpectedCard);
            }
            else if (step.expectedAction == TutorialActionType.ClickStock)
            {
                bool isStock = modeManager != null && modeManager.pileManager.Stock.Contains(c);
                c.canvasGroup.blocksRaycasts = isStock;
            }
            else
            {
                c.canvasGroup.blocksRaycasts = false;
            }
        }

        // 2. БЛОКИРОВКА КНОПКИ UNDO
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
            if (AreAllExpectedCardsInWaste(step)) shouldAdvance = true;
        }
        else if (step.expectedAction == TutorialActionType.ClickStock)
        {
            if (modeManager != null && modeManager.pileManager.Stock != null)
            {
                if (modeManager.pileManager.Stock.Transform.childCount < initialStockCount) shouldAdvance = true;
            }
        }
        else if (step.expectedAction == TutorialActionType.Undo)
        {
            CardController firstCard = FindCardByName(step.expectedCardNames.Count > 0 ? step.expectedCardNames[0] : "");
            if (firstCard != null && modeManager.pileManager.FindSlotWithCard(firstCard) != null)
                shouldAdvance = true;
        }

        if (shouldAdvance) AdvanceStep();
    }

    private CardController GetCurrentExpectedCard(TutorialStepTriPeaks step)
    {
        if (step.expectedCardNames == null || step.expectedCardNames.Count == 0) return null;

        for (int i = 0; i < step.expectedCardNames.Count; i++)
        {
            CardController card = FindCardByName(step.expectedCardNames[i]);
            if (card != null && modeManager.pileManager.FindSlotWithCard(card) != null)
            {
                if (i < step.arrowAnchorIndices.Count)
                {
                    ApplyArrow(step.arrowType, step.arrowAnchorIndices[i]); // <--- Вызывает только если нужен сдвиг
                }
                return card;
            }
        }
        return null;
    }

    private bool AreAllExpectedCardsInWaste(TutorialStepTriPeaks step)
    {
        if (step.expectedCardNames == null || step.expectedCardNames.Count == 0) return true;

        foreach (string cardName in step.expectedCardNames)
        {
            CardController card = FindCardByName(cardName);
            if (card == null) continue;

            if (modeManager.pileManager.FindSlotWithCard(card) != null || modeManager.pileManager.Stock.Contains(card))
            {
                return false;
            }
        }
        return true;
    }

    // ИСПРАВЛЕННЫЙ МЕТОД: Больше не сбрасывает анимацию каждый кадр
    private void ApplyArrow(TutorialArrowType type, int anchorIndex)
    {
        // Защита от сброса позиции: если стрелка уже стоит на нужном якоре, ничего не делаем
        if (currentActiveAnchorIndex == anchorIndex && activeArrow != null && activeArrow.gameObject.activeSelf)
            return;

        currentActiveAnchorIndex = anchorIndex;
        activeArrowType = type;

        if (arrowDown) arrowDown.gameObject.SetActive(false);
        if (arrowUp) arrowUp.gameObject.SetActive(false);
        if (arrowLeft) arrowLeft.gameObject.SetActive(false);

        activeArrow = null;

        if (type != TutorialArrowType.None && anchorIndex >= 0 && anchorIndex < arrowAnchors.Length)
        {
            activeArrow = type == TutorialArrowType.Down ? arrowDown : (type == TutorialArrowType.Up ? arrowUp : arrowLeft);
            RectTransform targetAnchor = arrowAnchors[anchorIndex];

            if (activeArrow != null && targetAnchor != null)
            {
                activeArrow.gameObject.SetActive(true);
                activeArrow.SetParent(targetAnchor, false);
                activeArrow.anchoredPosition = Vector2.zero; // Сбрасываем ОДИН РАЗ при смене якоря
            }
        }
    }

    private CardController FindCardByName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;

        string targetName = $"Card_{exactName}";
        var allCards = FindObjectsOfType<CardController>(true);

        foreach (var c in allCards)
        {
            if (c.gameObject.name == targetName) return c;
        }
        return null;
    }

    public void AdvanceStep()
    {
        if (!stepTransitioning && IsTutorialActive && gameObject.activeInHierarchy)
        {
            stepTransitioning = true;
            if (arrowDown) arrowDown.gameObject.SetActive(false);
            if (arrowUp) arrowUp.gameObject.SetActive(false);
            if (arrowLeft) arrowLeft.gameObject.SetActive(false);
            activeArrow = null;
            currentActiveAnchorIndex = -1; // Сбрасываем якорь

            StartCoroutine(AdvanceStepRoutine());
        }
    }

    private IEnumerator AdvanceStepRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        InternalAdvanceStep();
        stepTransitioning = false;
        if (IsTutorialActive) UpdateUI();
    }

    private void InternalAdvanceStep()
    {
        if (!IsTutorialActive) return;

        currentStepIndex++;

        if (currentStepIndex >= steps.Count || IsTableauEmpty())
        {
            string endText = "Поздравляем! Вы освоили Три Пика!";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue("TriPeaksTutorialEnd");
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
        else
        {
            UpdateUI();
        }
    }

    private bool IsTableauEmpty()
    {
        foreach (var slot in modeManager.pileManager.TableauPiles)
        {
            if (slot.HasCard) return false;
        }
        return true;
    }

    private void UpdateUI()
    {
        if (currentStepIndex >= steps.Count || stepTransitioning || isIntroAnimating) return;

        TutorialStepTriPeaks step = steps[currentStepIndex];

        if (modeManager != null && modeManager.pileManager.Stock != null)
            initialStockCount = modeManager.pileManager.Stock.Transform.childCount;

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

        currentActiveAnchorIndex = -1; // Принудительно заставляем стрелку обновиться для нового шага

        // ВАЖНОЕ ИСПРАВЛЕНИЕ: Теперь стрелки рисуются даже если нет карт (на Колоду и Отмену)
        if (step.expectedAction == TutorialActionType.MoveCard)
        {
            GetCurrentExpectedCard(step);
        }
        else if (step.arrowAnchorIndices != null && step.arrowAnchorIndices.Count > 0)
        {
            ApplyArrow(step.arrowType, step.arrowAnchorIndices[0]);
        }

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
        currentActiveAnchorIndex = -1; // Сбрасываем якорь
    }

    private void UnlockAllElements()
    {
        IsTutorialActive = false;
        var allCards = FindObjectsOfType<CardController>(true);
        foreach (var c in allCards) if (c.canvasGroup != null) c.canvasGroup.blocksRaycasts = true;
    }

    private void OnDisable() { HidePanelToLeft(); UnlockAllElements(); }
    private void OnDestroy() { if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false); }

    // ==========================================
    // ИДЕАЛЬНАЯ РАЗДАЧА 
    // ==========================================
    public IEnumerator PlayTutorialIntro(Deal dummyDeal)
    {
        isIntroAnimating = true;
        modeManager.IsInputAllowed = false;
        if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);
        DisableAllHighlights();

        modeManager.pileManager.ClearAll();
        modeManager.cardFactory.DestroyAllCards();

        if (modeManager.introController != null)
        {
            modeManager.introController.PrepareIntro(true);
            yield return StartCoroutine(modeManager.introController.PlayUIIntroSequence());
        }

        string[] cardModels = {
            "Diamonds_8_down", // Slot 0
            "Clubs_2_down",    // Slot 1
            "Hearts_5_down",   // Slot 2
            "Hearts_6_up",     // Slot 3
            "Clubs_7_up",      // Slot 4
            "Spades_13_up",    // Slot 5 (King)
            "Hearts_1_up",     // Slot 6 (Ace)
            "Diamonds_3_up",   // Slot 7
            "Spades_4_up"      // Slot 8
        };

        List<CardController> stockCards = new List<CardController>();
        List<CardController> tableauCards = new List<CardController>();

        for (int i = 0; i < 9; i++)
        {
            var card = CreateTutorialCard(cardModels[i]);
            card.transform.SetParent(modeManager.pileManager.Stock.transform);
            card.transform.localPosition = new Vector3(-5000f, 0, 0);
            tableauCards.Add(card);
        }

        string wasteModel = "Spades_5_up";
        var wasteCard = CreateTutorialCard(wasteModel);
        wasteCard.transform.SetParent(modeManager.pileManager.Stock.transform);
        wasteCard.transform.localPosition = new Vector3(-5000f, 0, 0);

        string[] stockModels = { "Clubs_9_down", "Hearts_10_down", "Spades_11_down", "Diamonds_12_down" };
        foreach (var m in stockModels)
        {
            var sc = CreateTutorialCard(m);
            sc.transform.SetParent(modeManager.pileManager.Stock.transform);
            sc.transform.localPosition = new Vector3(-5000f, 0, 0);
            stockCards.Add(sc);
            modeManager.pileManager.Stock.AddCard(sc);
        }

        List<CardController> allCardsToFly = new List<CardController>();
        allCardsToFly.AddRange(stockCards);
        allCardsToFly.Add(wasteCard);
        for (int i = tableauCards.Count - 1; i >= 0; i--)
        {
            allCardsToFly.Add(tableauCards[i]);
        }

        Canvas.ForceUpdateCanvases();

        float screenW = modeManager.rootCanvas.GetComponent<RectTransform>().rect.width;
        float gap = modeManager.pileManager.Stock.Gap > 0 ? modeManager.pileManager.Stock.Gap : 5f;

        yield return StartCoroutine(modeManager.animationService.AnimateDeckArrivalAndExpand(allCardsToFly, modeManager.pileManager.Stock.transform, screenW, stockCards.Count, () => false, 1f, gap));

        float delayPerCard = modeManager.totalDealDuration / (tableauCards.Count + 1);
        for (int i = 0; i < tableauCards.Count; i++)
        {
            CardController card = tableauCards[i];
            TriPeaksTableauPile targetSlot = modeManager.pileManager.TableauPiles[i];
            bool isFaceUp = cardModels[i].EndsWith("up");

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

            StartCoroutine(modeManager.animationService.AnimateMoveCard(card, targetSlot.transform, modeManager.dealFlyDuration, isFaceUp, () =>
            {
                targetSlot.AddCard(card);
            }));

            yield return new WaitForSeconds(delayPerCard);
        }

        var logicField = typeof(TriPeaksModeManager).GetField("_logicTopCardModel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (logicField != null) logicField.SetValue(modeManager, wasteCard.cardModel);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Flip");

        Vector3 offset = modeManager.pileManager.Waste.GetTargetLocalPositionForNextCard();
        yield return StartCoroutine(modeManager.animationService.AnimateMoveCard(wasteCard, modeManager.pileManager.Waste.transform, modeManager.dealFlyDuration, true, () =>
        {
            modeManager.pileManager.Waste.AddCard(wasteCard);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");
        }, offset));

        modeManager.pileManager.Stock.UpdateVisuals();

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

        Suit suit = Suit.Spades;
        if (suitStr == "Hearts") suit = Suit.Hearts;
        else if (suitStr == "Clubs") suit = Suit.Clubs;
        else if (suitStr == "Diamonds") suit = Suit.Diamonds;

        CardModel model = new CardModel(suit, rank);
        CardController card = modeManager.cardFactory.CreateCard(model, modeManager.pileManager.Stock.transform, Vector2.zero);

        card.gameObject.name = $"Card_{suitStr}_{rank}";
        card.CardmodeManager = modeManager;
        card.OnClicked += modeManager.OnCardClicked;

        var cData = card.GetComponent<CardData>();
        if (cData)
        {
            cData.SetFaceUp(false, false);
            if (cData.image) cData.image.color = Color.white;
        }

        return card;
    }

    private void InitializeSteps()
    {
        string clrOrg = "<color=#FCA311>";
        string clrBlk = "<color=#8294FF>";
        string clrRed = "<color=#FF6B6B>";
        string endClr = "</color>";

        steps.Add(new TutorialStepTriPeaks
        {
            localizationKey = "TriPeaksTutorial1",
            fallbackText = $"Цель — {clrOrg}убрать все карты{endClr}. Выбирайте открытые карты на {clrOrg}1 ранг старше или младше{endClr} вашей (сейчас это {clrBlk}5♠{endClr}). Собирайте длинные {clrOrg}комбо{endClr} ради очков!\nСнимите: {clrRed}6♥{endClr} -> {clrBlk}7♣{endClr} -> {clrRed}8♦{endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardNames = new List<string> { "Hearts_6", "Clubs_7", "Diamonds_8" },
            arrowAnchorIndices = new List<int> { 0, 1, 2 },
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up
        });

        steps.Add(new TutorialStepTriPeaks
        {
            localizationKey = "TriPeaksTutorial3",
            fallbackText = $"Доступных ходов больше нет. Возьмите новую карту из {clrOrg}колоды{endClr}.",
            expectedAction = TutorialActionType.ClickStock,
            panelAnchor = TutorialPanelAnchorType.Center,
            highlightElements = new List<string> { "Highlight_Stock" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndices = new List<int> { -1 }
        });

        steps.Add(new TutorialStepTriPeaks
        {
            localizationKey = "TriPeaksTutorial4",
            fallbackText = $"Нам выпала {clrRed}Дама(Q♦){endClr}. Ранги карт идут {clrOrg}по кругу{endClr}: на {clrBlk}Короля(K){endClr} можно положить {clrRed}Туза(A){endClr}, а на Туза — {clrBlk}Двойку(2){endClr}. Соберите эту цепочку!",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardNames = new List<string> { "Spades_13", "Hearts_1", "Clubs_2" },
            arrowAnchorIndices = new List<int> { 3, 4, 5 },
            panelAnchor = TutorialPanelAnchorType.Center,
            arrowType = TutorialArrowType.Up
        });

        steps.Add(new TutorialStepTriPeaks
        {
            localizationKey = "TriPeaksTutorial7",
            fallbackText = $"Давайте проверим как работает кнопка {clrOrg}'Отмена'{endClr}. Нажмите на неё.",
            expectedAction = TutorialActionType.Undo,
            expectedCardNames = new List<string> { "Clubs_2" },
            panelAnchor = TutorialPanelAnchorType.Center,
            highlightElements = new List<string> { "Highlight_UndoButton" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndices = new List<int> { 6 }
        });

        steps.Add(new TutorialStepTriPeaks
        {
            localizationKey = "TriPeaksTutorial8",
            fallbackText = $"Отлично! Верните {clrBlk}Двойку Треф(2♣){endClr} и очистите стол: {clrBlk}2{endClr} -> {clrRed}3{endClr} -> {clrBlk}4{endClr} -> {clrRed}5{endClr}!",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardNames = new List<string> { "Clubs_2", "Diamonds_3", "Spades_4", "Hearts_5" },
            arrowAnchorIndices = new List<int> { 5, 7, 8, 9 },
            panelAnchor = TutorialPanelAnchorType.Center,
            arrowType = TutorialArrowType.Up
        });
    }
}