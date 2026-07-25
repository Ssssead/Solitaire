using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MontanaTutorialManager : MonoBehaviour, ITutorialManager
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
    public RectTransform[] arrowAnchors = new RectTransform[10];

    [Header("Animation Settings")]
    public float arrowBounceAmplitude = 12f;
    public float arrowBounceSpeed = 6f;

    [Header("Highlights")]
    public List<GameObject> highlightObjects = new List<GameObject>();
    private List<TutorialStep> steps = new List<TutorialStep>();

    public bool IsTutorialActive { get; private set; }
    private int currentStepIndex = 0;
    private MontanaModeManager modeManager;
    private Coroutine panelMoveCoroutine;
    private Coroutine arrowAnimCoroutine;
    private bool stepTransitioning = false;

    private MontanaSlot stepStartSlot;
    private CardController cachedExpectedCard;

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

        modeManager = GetComponent<MontanaModeManager>();
        InitializeDefaultSteps();
    }

    private void Start()
    {
        if (!IsTutorialActive) return;

        // Кнопку пересдачи мы больше не трогаем, так как MontanaModeManager 
        // сам вызовет наш публичный метод OnTutorialReshuffleClicked().

        // Для Отмены хода оставляем подписку, так как реальная отмена 
        // тоже должна сработать параллельно с шагом обучения.
        if (modeManager.undoManager != null && modeManager.undoManager.undoButton != null)
        {
            modeManager.undoManager.undoButton.onClick.AddListener(OnTutorialUndoClicked);
        }
    }

    private void LateUpdate()
    {
        if (!IsTutorialActive || stepTransitioning || currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];
        var allCards = FindObjectsOfType<MontanaCardController>();

        foreach (var c in allCards)
        {
            if (c.canvasGroup != null)
            {
                if (c.transform.parent == modeManager.DragLayer) continue;

                if (step.expectedAction == TutorialActionType.MoveCard || step.expectedAction == TutorialActionType.DoubleClick)
                    c.canvasGroup.blocksRaycasts = (c == cachedExpectedCard);
                else
                    c.canvasGroup.blocksRaycasts = false;
            }
        }

        if (modeManager.undoManager != null)
        {
            modeManager.undoManager.isLocked = (step.expectedAction != TutorialActionType.Undo);
        }

        bool shouldAdvance = false;
        if (step.expectedAction == TutorialActionType.MoveCard || step.expectedAction == TutorialActionType.DoubleClick)
        {
            if (cachedExpectedCard != null && stepStartSlot != null)
            {
                MontanaSlot currentSlot = cachedExpectedCard.GetComponentInParent<MontanaSlot>();

                if (currentSlot != null && currentSlot != stepStartSlot &&
                    cachedExpectedCard.transform.localPosition == Vector3.zero &&
                    cachedExpectedCard.transform.parent != modeManager.DragLayer)
                {
                    shouldAdvance = true;
                }
            }
        }

        if (shouldAdvance)
        {
            stepTransitioning = true;
            StartCoroutine(AdvanceStepRoutine());
        }
    }

    public void OnTutorialReshuffleClicked()
    {
        if (stepTransitioning || currentStepIndex >= steps.Count) return;
        TutorialStep step = steps[currentStepIndex];

        if (step.expectedAction == TutorialActionType.ClickStock)
        {
            if (currentStepIndex == 9) // Шаг тупика 1
            {
                stepTransitioning = true;
                StartCoroutine(ExecuteReshuffleAndAdvance(1));
            }
            else if (currentStepIndex == 12) // Шаг тупика 2
            {
                stepTransitioning = true;
                StartCoroutine(ExecuteReshuffleAndAdvance(2));
            }
        }
    }
    private IEnumerator ExecuteReshuffleAndAdvance(int reshuffleNum)
    {
        // Сначала ждем полного завершения пересдачи
        if (reshuffleNum == 1) yield return StartCoroutine(FakeReshuffle1());
        else yield return StartCoroutine(FakeReshuffle2());

        yield return null; // Ждем 1 кадр для обновления слотов

        // И только когда карты легли на места, переключаем шаг обучения!
        InternalAdvanceStep();
        stepTransitioning = false;
    }
    private void OnTutorialUndoClicked()
    {
        if (stepTransitioning || currentStepIndex >= steps.Count) return;
        TutorialStep step = steps[currentStepIndex];

        if (step.expectedAction == TutorialActionType.Undo)
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
        string exactName = "Card_" + cardName;

        var allCards = FindObjectsOfType<MontanaCardController>();
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
            string endText = "<color=#FCA311>Поздравляем!</color> Вы мастерски освоили правила пасьянса Коврик!";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                endText = LocalizationManager.instance.GetLocalizedValue("CarpetTutorialEnd");
            }
            if (instructionText != null) instructionText.text = endText;
            if (stepIndicatorText != null) stepIndicatorText.text = "";

            // --- ДОБАВЛЕНО: Центрируем панель для финального сообщения ---
            if (tutorialUIPanel != null && centerAnchor != null)
            {
                if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
                panelMoveCoroutine = StartCoroutine(MovePanelRoutine(centerAnchor));
            }
            // -------------------------------------------------------------

            DisableAllHighlights();
            IsTutorialActive = false;

            if (modeManager.undoManager != null) modeManager.undoManager.isLocked = false;
        }
        else UpdateUI();
    }
    private void UpdateUI()
    {
        if (currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];

        cachedExpectedCard = FindCardByName(step.expectedCardName);
        if (cachedExpectedCard != null)
        {
            stepStartSlot = cachedExpectedCard.GetComponentInParent<MontanaSlot>();
        }
        else
        {
            stepStartSlot = null;
        }

        if (instructionText != null)
        {
            string textToShow = step.fallbackText;
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                textToShow = LocalizationManager.instance.GetLocalizedValue(step.localizationKey);
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

        foreach (var highlight in highlightObjects)
        {
            if (highlight == null) continue;
            bool shouldBeActive = step.highlightElements != null && step.highlightElements.Contains(highlight.name);
            highlight.SetActive(shouldBeActive);
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
        Vector2 startAnchoredPos = tutorialUIPanel.anchoredPosition;
        Vector2 targetAnchoredPos = targetRect.anchoredPosition;
        tutorialUIPanel.anchorMin = targetRect.anchorMin;
        tutorialUIPanel.anchorMax = targetRect.anchorMax;
        tutorialUIPanel.pivot = targetRect.pivot;
        tutorialUIPanel.sizeDelta = targetRect.sizeDelta;

        float duration = 0.3f;
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
        tutorialUIPanel.gameObject.SetActive(false);
    }

    public void HideHighlights() => DisableAllHighlights();

    public void RestorePanelPosition()
    {
        // --- ИСПРАВЛЕНИЕ: Блокируем показ панели, если это не обучающая игра ---
        if (!IsTutorialActive) return;

        if (tutorialUIPanel != null)
        {
            tutorialUIPanel.gameObject.SetActive(true);
            UpdateUI();
        }
    }

    public IEnumerator PlayTutorialIntro()
    {
        IsTutorialActive = true;
        this.enabled = true;
        steps.Clear();
        InitializeDefaultSteps();

        modeManager.IsInputAllowed = false;
        modeManager.PileManager.ClearAllPiles();
        modeManager.cardFactory.DestroyAllCards();

        if (modeManager.deckManager != null && modeManager.deckManager.introController != null)
        {
            var intro = modeManager.deckManager.introController;
            intro.PrepareIntro(modeManager.isRestarting);
            yield return StartCoroutine(intro.PlayIntroSequence(modeManager.isRestarting));
        }

        List<CardModel> fullDeck = modeManager.cardFactory.CreateFullDeck();
        var specificCards = new Dictionary<string, CardModel>();

        // Строгий список 23 карт для полного детерминизма всех шагов
        string[] targets = {
            "Spades_1", "Spades_2", "Spades_3", "Spades_4", "Spades_5", "Spades_7", "Spades_8", "Spades_13",
            "Clubs_1", "Clubs_13",
            "Diamonds_1", "Diamonds_13",
            "Hearts_1", "Hearts_2", "Hearts_3", "Hearts_4", "Hearts_5", "Hearts_6", "Hearts_7", "Hearts_10", "Hearts_11", "Hearts_12", "Hearts_13"
        };

        for (int i = fullDeck.Count - 1; i >= 0; i--)
        {
            string key = $"{fullDeck[i].suit}_{fullDeck[i].rank}";
            if (targets.Contains(key))
            {
                specificCards[key] = fullDeck[i];
                fullDeck.RemoveAt(i);
            }
        }

        CardModel?[,] gridLayout = new CardModel?[4, 14];

        // 1. Пустые слоты
        List<Vector2Int> gaps = new List<Vector2Int> {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(2, 0),
            new Vector2Int(3, 13)
        };

        // 2. Раскладка Пик
        gridLayout[0, 1] = specificCards["Spades_1"];
        gridLayout[0, 2] = specificCards["Spades_2"];
        gridLayout[0, 3] = specificCards["Spades_3"];
        gridLayout[0, 4] = specificCards["Spades_4"];
        gridLayout[0, 6] = specificCards["Spades_5"];
        gridLayout[0, 5] = specificCards["Clubs_13"]; // Тупик 1

        // 3. Раскладка для Треф (Тупик 2)
        gridLayout[1, 2] = specificCards["Clubs_1"];
        gridLayout[1, 1] = specificCards["Diamonds_13"];

        // 4. Раскладка для Бубен (Тупик 3)
        gridLayout[2, 2] = specificCards["Diamonds_1"];
        gridLayout[2, 1] = specificCards["Hearts_13"];

        // 5. Черви собираем до 7
        gridLayout[3, 0] = specificCards["Hearts_1"];
        gridLayout[3, 1] = specificCards["Hearts_2"];
        gridLayout[3, 2] = specificCards["Hearts_3"];
        gridLayout[3, 3] = specificCards["Hearts_4"];
        gridLayout[3, 4] = specificCards["Hearts_5"];
        gridLayout[3, 5] = specificCards["Hearts_6"];
        gridLayout[3, 6] = specificCards["Hearts_7"];

        gridLayout[3, 12] = specificCards["Spades_13"]; // Тупик 4

        // 6. Карты для будущей пересдачи прячем в безопасные слоты (чтобы не заблокировались)
        gridLayout[1, 3] = specificCards["Hearts_10"];
        gridLayout[1, 4] = specificCards["Hearts_11"];
        gridLayout[1, 5] = specificCards["Hearts_12"];
        gridLayout[1, 6] = specificCards["Spades_7"];
        gridLayout[1, 7] = specificCards["Spades_8"];

        int deckIndex = 0;
        for (int c = 0; c < 14; c++)
        {
            for (int r = 0; r < 4; r++)
            {
                if (gaps.Any(g => g.x == r && g.y == c) || gridLayout[r, c] != null) continue;
                gridLayout[r, c] = fullDeck[deckIndex++];
            }
        }

        yield return StartCoroutine(SpawnAndAnimateCards(gridLayout, modeManager.rootCanvas.transform.localScale.x));

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameStarted("Montana", Difficulty.Easy, "Tutorial");
        if (modeManager.scoreManager != null) modeManager.scoreManager.ResetScore();

        currentStepIndex = 0;
        DisableAllHighlights();
        tutorialUIPanel.gameObject.SetActive(true);
        UpdateUI();

        modeManager.CheckGameState();
        modeManager.IsInputAllowed = true;
    }

    private IEnumerator FakeReshuffle1()
    {
        modeManager.IsInputAllowed = false;
        modeManager.IsHardMode = true;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        // 1. Собираем АБСОЛЮТНО ВСЕ карты
        var allCards = FindObjectsOfType<MontanaCardController>().ToList();

        foreach (var card in allCards)
        {
            var currentSlot = card.GetComponentInParent<MontanaSlot>();
            if (currentSlot != null) currentSlot.RemoveCard(card);

            card.transform.SetParent(modeManager.DragLayer, true);
            card.SetAnimating(true);
        }

        yield return StartCoroutine(GatherCardsAnimation(allCards));

        CardController[,] newLayout = new CardController[4, 14];

        List<Vector2Int> gaps = new List<Vector2Int> {
            new Vector2Int(0, 13), new Vector2Int(1, 13), new Vector2Int(2, 13), new Vector2Int(3, 13)
        };

        // Локальная функция для БЕЗОПАСНОГО извлечения нужной карты по имени
        CardController PullCard(string name)
        {
            var c = allCards.FirstOrDefault(x => x.name == "Card_" + name);
            if (c != null) allCards.Remove(c);
            return c;
        }

        // 2. Расставляем триггеры для обучения (Сложный режим)
        newLayout[1, 12] = PullCard("Hearts_10");
        newLayout[2, 5] = PullCard("Hearts_11"); // J♥ отсюда полетит в 1,13

        newLayout[2, 4] = PullCard("Spades_7");
        newLayout[2, 8] = PullCard("Spades_8"); // 8♠ отсюда полетит в 2,5

        // Блокируем пустые ячейки (Королями), чтобы создать тупики после ходов
        newLayout[2, 7] = PullCard("Clubs_13"); // Блокирует 2,8
        newLayout[0, 12] = PullCard("Diamonds_13"); // Блокирует 0,13
        newLayout[2, 12] = PullCard("Hearts_13"); // Блокирует 2,13
        newLayout[3, 12] = PullCard("Spades_13"); // Блокирует 3,13

        // 3. Выстраиваем красивые ряды (как вы просили) из оставшихся карт
        for (int i = 1; i <= 6; i++) newLayout[0, i - 1] = PullCard($"Spades_{i}");
        for (int i = 1; i <= 8; i++) newLayout[1, i - 1] = PullCard($"Clubs_{i}");
        for (int i = 1; i <= 4; i++) newLayout[2, i - 1] = PullCard($"Diamonds_{i}");
        for (int i = 1; i <= 8; i++) newLayout[3, i - 1] = PullCard($"Hearts_{i}");

        // 4. Безопасно заполняем все остальные пустые слоты (кроме gaps) тем, что осталось
        int cardIdx = 0;
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 14; c++)
            {
                if (gaps.Any(g => g.x == r && g.y == c)) continue;
                if (newLayout[r, c] != null) continue;

                if (cardIdx < allCards.Count)
                {
                    newLayout[r, c] = allCards[cardIdx++];
                }
            }
        }

        yield return StartCoroutine(DealCardsAnimation(newLayout));
        modeManager.CheckGameState();
        modeManager.IsInputAllowed = true;
    }

    private IEnumerator FakeReshuffle2()
    {
        modeManager.IsInputAllowed = false;
        modeManager.IsHardMode = false; // Классический режим
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        var allCards = FindObjectsOfType<MontanaCardController>().ToList();

        foreach (var card in allCards)
        {
            var currentSlot = card.GetComponentInParent<MontanaSlot>();
            if (currentSlot != null) currentSlot.RemoveCard(card);

            card.transform.SetParent(modeManager.DragLayer, true);
            card.SetAnimating(true);
        }

        yield return StartCoroutine(GatherCardsAnimation(allCards));

        CardController[,] newLayout = new CardController[4, 14];

        CardController PullCard(string name)
        {
            var c = allCards.FirstOrDefault(x => x.name == "Card_" + name);
            if (c != null) allCards.Remove(c);
            return c;
        }

        // Выстраиваем идеальную победную ситуацию
        for (int i = 1; i <= 13; i++) newLayout[0, i - 1] = PullCard($"Spades_{i}");
        for (int i = 1; i <= 13; i++) newLayout[1, i - 1] = PullCard($"Clubs_{i}");
        for (int i = 1; i <= 13; i++) newLayout[2, i - 1] = PullCard($"Diamonds_{i}");
        for (int i = 1; i <= 10; i++) newLayout[3, i - 1] = PullCard($"Hearts_{i}");

        // Оставшиеся 3 карты (J, Q, K) ставим в конце, оставляя дырку 3,10
        newLayout[3, 11] = PullCard("Hearts_11");
        newLayout[3, 12] = PullCard("Hearts_12");
        newLayout[3, 13] = PullCard("Hearts_13");

        yield return StartCoroutine(DealCardsAnimation(newLayout));
        modeManager.CheckGameState();
        modeManager.IsInputAllowed = true;
    }

    private IEnumerator SpawnAndAnimateCards(CardModel?[,] gridLayout, float canvasScale)
    {
        MontanaSlot slot0 = modeManager.pileManager.GetSlot(0, 0);
        Vector3 targetStackPos = slot0.Transform.position;
        Vector3 startStackPos = targetStackPos + new Vector3(-1500f * canvasScale, 0, 0);

        List<CardController> cardsToDeal = new List<CardController>();
        List<MontanaSlot> targetSlots = new List<MontanaSlot>();

        int layoutCounter = 0;
        for (int c = 0; c < 14; c++)
        {
            for (int r = 0; r < 4; r++)
            {
                var model = gridLayout[r, c];
                if (model == null) continue;

                var card = modeManager.cardFactory.CreateCard(model.Value, modeManager.DragLayer, Vector2.zero);
                card.gameObject.name = $"Card_{model.Value.suit}_{model.Value.rank}";
                card.GetComponent<CardData>()?.SetFaceUp(true, false);

                Vector3 cardOffset = new Vector3(-layoutCounter * 1f * canvasScale, 0, 0);
                card.transform.position = startStackPos + cardOffset;

                cardsToDeal.Add(card);
                targetSlots.Add(modeManager.pileManager.GetSlot(r, c));
                modeManager.RegisterCardEvents(card);
                layoutCounter++;
            }
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", 0.85f, 0.2f);

        float flyDuration = 0.85f;
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            float easedT = t * t * (3f - 2f * t);
            for (int i = 0; i < cardsToDeal.Count; i++)
            {
                if (cardsToDeal[i] == null) continue; // Защита
                Vector3 cardOffset = new Vector3(i * 1f * canvasScale, 0, 0);
                cardsToDeal[i].transform.position = Vector3.Lerp(startStackPos + cardOffset, targetStackPos + cardOffset, easedT);
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.15f);

        float dealSpeed = 0.015f;
        float cardMoveDuration = 0.22f;

        for (int i = cardsToDeal.Count - 1; i >= 0; i--)
        {
            var card = cardsToDeal[i];
            var targetSlot = targetSlots[i];
            if (card != null) StartCoroutine(MoveCardToSlotRoutine(card, targetSlot, cardMoveDuration));
            yield return new WaitForSeconds(dealSpeed);
        }

        yield return new WaitForSeconds(cardMoveDuration);
    }

    private IEnumerator GatherCardsAnimation(List<MontanaCardController> cards)
    {
        yield return new WaitForSeconds(0.1f);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", 0.4f, 0.1f);

        Vector3 gatherPos = modeManager.pileManager.Slots[55].Transform.position;
        float duration = 0.4f;
        float elapsed = 0f;
        List<Vector3> startPositions = cards.Select(c => c != null ? c.transform.position : gatherPos).ToList();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue; // Защита
                cards[i].transform.position = Vector3.Lerp(startPositions[i], gatherPos, t);
                cards[i].transform.SetAsLastSibling();
            }
            yield return null;
        }
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator DealCardsAnimation(CardController[,] layout)
    {
        float dealSpeed = 0.015f;
        float cardMoveDuration = 0.2f;

        for (int c = 0; c < 14; c++)
        {
            for (int r = 0; r < 4; r++)
            {
                var card = layout[r, c];
                if (card != null)
                {
                    var targetSlot = modeManager.pileManager.GetSlot(r, c);
                    StartCoroutine(MoveCardToSlotRoutine(card, targetSlot, cardMoveDuration));
                    yield return new WaitForSeconds(dealSpeed);
                }
            }
        }
        yield return new WaitForSeconds(cardMoveDuration);
    }

    private IEnumerator MoveCardToSlotRoutine(CardController card, MontanaSlot targetSlot, float duration)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

        Vector3 startPos = card.transform.position;
        Vector3 endPos = targetSlot.Transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (card == null) yield break; // Абсолютная защита от краша
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);
            card.transform.position = Vector3.Lerp(startPos, endPos, easedT);
            yield return null;
        }

        if (card != null)
        {
            card.transform.position = endPos;
            targetSlot.AcceptCard(card);
            card.GetComponent<MontanaCardController>()?.SetAnimating(false);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");
        }
    }

    private void InitializeDefaultSteps()
    {
        // 0
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial1",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Spades_1",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 0
        });

        // 1
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorialUndo",
            expectedAction = TutorialActionType.Undo,
            panelAnchor = TutorialPanelAnchorType.Center,
            highlightElements = new List<string> { "Highlight_UndoButton" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 9
        });

        // 2
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial2",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Spades_1",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 0
        });

        // 3
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial3",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Spades_2",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 1
        });

        // 4
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial4",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Spades_3",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 2
        });

        // 5: Свободная игра (Тянет 4 Пик)
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial5",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Spades_4",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.None
        });

        // 6: Свободная игра (Тянет 5 Пик)
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial5",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Spades_5",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.None
        });

        // 7: Тузы Треф
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorialAces",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Clubs_1",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 3
        });

        // 8: Тузы Бубен
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorialAces2",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Diamonds_1",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 4
        });

        // 9: Истинный тупик 1
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial6",
            expectedAction = TutorialActionType.ClickStock,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_ReshuffleButton" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 5
        });

        // 10: Сложный режим (перенос J Червей)
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial7",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Hearts_11",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 6
        });

        // 11: 8 Пик на базу
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial8",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Spades_8",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 7
        });

        // 12: Истинный тупик 2
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial9",
            expectedAction = TutorialActionType.ClickStock,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_ReshuffleButton" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 5
        });

        // 13: Финал 1
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial10",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Hearts_11",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 8
        });

        // 14: Финал 2
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial11",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Hearts_12",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 10
        });

        // 15: Финал 3
        steps.Add(new TutorialStep
        {
            localizationKey = "CarpetTutorial12",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Hearts_13",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 11
        });
    }
}