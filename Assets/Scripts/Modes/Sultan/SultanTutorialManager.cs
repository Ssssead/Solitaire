using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class SultanTutorialManager : MonoBehaviour, ITutorialManager
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

    [Header("Arrow Animation Settings")]
    public float arrowBounceAmplitude = 12f;
    public float arrowBounceSpeed = 6f;
    private Coroutine arrowAnimCoroutine;

    [Header("Highlights")]
    public List<GameObject> highlightObjects = new List<GameObject>();

    [Header("Sequence")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    public bool IsTutorialActive { get; private set; }
    public int currentStepIndex = 0;

    private SultanModeManager modeManager;
    private Coroutine panelMoveCoroutine;
    private bool stepTransitioning = false;

    private struct TutorialCardAnimData
    {
        public CardController card;
        public ICardContainer targetContainer;
        public Vector3 finalWorldPos;
    }
    private List<TutorialCardAnimData> cardsToAnimate = new List<TutorialCardAnimData>();

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

        modeManager = GetComponent<SultanModeManager>();
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

        // Блокировка кликов
        foreach (var c in allCards)
        {
            if (c.canvasGroup == null) continue;

            // ⚡ ЗАЩИТА: Карты внутри колоды (Stock) НИКОГДА не должны перехватывать лучи,
            // иначе они заблокируют клик по самому объекту колоды SultanStockPile под ними!
            if (modeManager != null && modeManager.pileManager != null &&
                modeManager.pileManager.StockPile != null &&
                c.transform.parent == modeManager.pileManager.StockPile.transform)
            {
                c.canvasGroup.blocksRaycasts = false;
                c.canvasGroup.interactable = false;
                continue;
            }

            bool isFlying = c.GetComponentInParent<ICardContainer>() == null;
            if (isFlying) continue;

            if (currentStepIndex == steps.Count - 1)
            {
                // На последнем шаге (свободная игра) разблокируем все карты на поле
                c.canvasGroup.blocksRaycasts = true;
            }
            else if (step.expectedAction == TutorialActionType.MoveCard || step.expectedAction == TutorialActionType.DoubleClick)
            {
                c.canvasGroup.blocksRaycasts = (c == expectedCard);
            }
            else
            {
                c.canvasGroup.blocksRaycasts = false;
            }
        }

        // ⚡ ПРОВЕРКА ПОБЕДЫ: Шаг переключится ТОЛЬКО тогда, когда все дома собраны!
        if (currentStepIndex == steps.Count - 1)
        {
            if (modeManager.pileManager.Foundations.TrueForAll(f => f.IsComplete()))
            {
                AdvanceStep();
            }
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

        // Сначала ищем только среди открытых карт (защита от дубликатов в колоде)
        foreach (var c in allCards)
        {
            if (c.gameObject.name.Contains(cardName))
            {
                var data = c.GetComponent<CardData>();
                if (data != null && data.IsFaceUp()) return c;
            }
        }

        // Если не нашли открытую, берем любую
        foreach (var c in allCards)
            if (c.gameObject.name.Contains(cardName)) return c;

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

        // ⚡ ИСПРАВЛЕНИЕ: На последнем шаге разрешаем ВСЁ (и двойной клик, и колоду, и ходы)
        if (currentStepIndex == steps.Count - 1) return true;

        TutorialStep step = steps[currentStepIndex];

        if (action != step.expectedAction) return false;

        if (action == TutorialActionType.MoveCard || action == TutorialActionType.DoubleClick)
        {
            if (!string.IsNullOrEmpty(step.expectedCardName) && card != null && !card.gameObject.name.Contains(step.expectedCardName)) return false;

            if (target != null && action == TutorialActionType.MoveCard)
            {
                if (step.expectedTargetPileName == "Reserve" && !(target is SultanReserveSlot)) return false;
                if (step.expectedTargetPileName == "Foundation" && !(target is SultanFoundationPile)) return false;
            }
        }

        return true;
    }

    private void InternalAdvanceStep()
    {
        if (!IsTutorialActive) return;

        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            string endText = "Победа! Вы освоили правила пасьянса Султан.";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue("SultanTutorialEnd");
                if (!string.IsNullOrEmpty(loc) && loc != "SultanTutorialEnd") endText = loc;
            }
            if (instructionText != null) instructionText.text = endText;
            if (stepIndicatorText != null) stepIndicatorText.text = "";

            // ⚡ ДОБАВЛЕНО: Перемещаем панель строго в ЦЕНТР при завершении туториала ⚡
            if (tutorialUIPanel != null)
            {
                if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
                RectTransform targetAnchor = GetAnchorRect(TutorialPanelAnchorType.Center);
                panelMoveCoroutine = StartCoroutine(MovePanelRoutine(targetAnchor));
            }

            DisableAllHighlights();
            IsTutorialActive = false;
        }
        else UpdateUI();
    }

    private void UpdateUI()
    {
        if (currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];

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

        // 1. Запоминаем текущую мировую позицию панели, чтобы она не "прыгнула" при смене якоря
        Vector3 startWorldPos = tutorialUIPanel.position;

        // 2. ⚡ КОПИРУЕМ ЯКОРЯ И ПИВОТ ЦЕЛЕВОЙ ТОЧКИ В ПАНЕЛЬ ⚡
        tutorialUIPanel.anchorMin = targetRect.anchorMin;
        tutorialUIPanel.anchorMax = targetRect.anchorMax;
        tutorialUIPanel.pivot = targetRect.pivot;
        tutorialUIPanel.sizeDelta = targetRect.sizeDelta;

        // 3. Возвращаем панель на место (теперь уже в новой системе координат якоря)
        tutorialUIPanel.position = startWorldPos;

        Vector2 startAnchoredPos = tutorialUIPanel.anchoredPosition;
        Vector2 targetAnchoredPos = targetRect.anchoredPosition;

        // Определяем скорость в зависимости от расстояния (как в Клондайке)
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
        if (arrowAnimCoroutine != null) StopCoroutine(arrowAnimCoroutine);
        if (arrowDown != null) arrowDown.gameObject.SetActive(false);
        if (arrowUp != null) arrowUp.gameObject.SetActive(false);
        if (arrowLeft != null) arrowLeft.gameObject.SetActive(false);
    }

    public IEnumerator PlayTutorialIntro(Deal dummyDeal)
    {
        IsTutorialActive = true;
        this.enabled = true;
        currentStepIndex = 0;

        modeManager.IsInputAllowed = false;
        var pm = modeManager.pileManager;

        if (pm.CenterPile == null || pm.Foundations.Count < 8 || pm.Reserves.Count < 6)
        {
            pm.Initialize(modeManager);
            pm.CreatePiles();
        }

        pm.ClearAllPiles();
        modeManager.cardFactory.DestroyAllCards();
        cardsToAnimate.Clear();

        // 1. Спавним Дома (до 10, Трефы до 9)
        string[] Center = { "Hearts_13_up" };
        string[] F0 = { "Diamonds_10_up" }; // 0 - D
        string[] F1 = { "Hearts_10_up" };   // 1 - H
        string[] F2 = { "Diamonds_10_up" }; // 2 - D
        string[] F3 = { "Clubs_9_up" };     // 3 - C (Девятка для двойного клика)
        string[] F4 = { "Clubs_9_up" };     // 5 - C (Девятка для двойного клика)
        string[] F5 = { "Spades_10_up" };   // 6 - S
        string[] F6 = { "Hearts_10_up" };   // 7 - H
        string[] F7 = { "Spades_10_up" };   // 8 - S

        SpawnAndPlaceCards(Center, pm.CenterPile);
        if (pm.Foundations.Count > 0) SpawnAndPlaceCards(F0, pm.Foundations[0]);
        if (pm.Foundations.Count > 1) SpawnAndPlaceCards(F1, pm.Foundations[1]);
        if (pm.Foundations.Count > 2) SpawnAndPlaceCards(F2, pm.Foundations[2]);
        if (pm.Foundations.Count > 3) SpawnAndPlaceCards(F3, pm.Foundations[3]);
        if (pm.Foundations.Count > 4) SpawnAndPlaceCards(F4, pm.Foundations[4]);
        if (pm.Foundations.Count > 5) SpawnAndPlaceCards(F5, pm.Foundations[5]);
        if (pm.Foundations.Count > 6) SpawnAndPlaceCards(F6, pm.Foundations[6]);
        if (pm.Foundations.Count > 7) SpawnAndPlaceCards(F7, pm.Foundations[7]);

        // 2. Резервы
        string[] R0 = { "Hearts_11_up" };   // J♥ - Для 1 шага
        string[] R1 = { "Spades_11_up" };   // J♠
        string[] R2 = { "Diamonds_12_up" }; // Q♦
        string[] R3 = { "Hearts_12_up" };   // Q♥
        string[] R4 = { };                  // Пустой для 3 шага
        string[] R5 = { };

        if (pm.Reserves.Count > 0) SpawnAndPlaceCards(R0, pm.Reserves[0]);
        if (pm.Reserves.Count > 1) SpawnAndPlaceCards(R1, pm.Reserves[1]);
        if (pm.Reserves.Count > 2) SpawnAndPlaceCards(R2, pm.Reserves[2]);
        if (pm.Reserves.Count > 3) SpawnAndPlaceCards(R3, pm.Reserves[3]);
        if (pm.Reserves.Count > 4) SpawnAndPlaceCards(R4, pm.Reserves[4]);
        if (pm.Reserves.Count > 5) SpawnAndPlaceCards(R5, pm.Reserves[5]);

        // 3. Колода (ровно 14 карт для 100% победы)
        string[] Stock = {
            "Hearts_11_down", "Hearts_12_down",
            "Spades_11_down", "Spades_12_down", "Spades_12_down",
            "Diamonds_11_down", "Diamonds_12_down",
            "Clubs_10_down", "Clubs_11_down", "Clubs_12_down", "Clubs_12_down", // <--- Потерянная 10♣ теперь здесь!
            "Clubs_10_down",    // Для Шага 9 (верхняя часть)
            "Diamonds_11_down", // Для Шага 5
            "Clubs_11_down"     // Для Шага 2
        };
        SpawnAndPlaceCards(Stock, pm.StockPile);

        Canvas.ForceUpdateCanvases();

        yield return StartCoroutine(AnimateCardsDescent());

        DisableAllHighlights();

        if (tutorialUIPanel)
        {
            // Берем якорь из первого шага обучения
            RectTransform targetAnchor = GetAnchorRect(steps.Count > 0 ? steps[0].panelAnchor : TutorialPanelAnchorType.Bottom);
            if (targetAnchor != null)
            {
                // Точно так же копируем параметры перед первым появлением
                tutorialUIPanel.anchorMin = targetAnchor.anchorMin;
                tutorialUIPanel.anchorMax = targetAnchor.anchorMax;
                tutorialUIPanel.pivot = targetAnchor.pivot;
                tutorialUIPanel.sizeDelta = targetAnchor.sizeDelta;

                // Ставим за правый край экрана для выезда
                tutorialUIPanel.anchoredPosition = targetAnchor.anchoredPosition + new Vector2(2500f, 0);
            }
            tutorialUIPanel.gameObject.SetActive(true);
        }

        UpdateUI();
        modeManager.IsInputAllowed = true;
    }

    private void SpawnAndPlaceCards(string[] cardData, ICardContainer target)
    {
        if (cardData.Length == 0) return;
        if (target == null || modeManager.cardFactory == null) return;

        foreach (var data in cardData)
        {
            string[] parts = data.Split('_');
            Suit suit = Suit.Spades;
            if (parts[0] == "Hearts") suit = Suit.Hearts;
            else if (parts[0] == "Clubs") suit = Suit.Clubs;
            else if (parts[0] == "Diamonds") suit = Suit.Diamonds;

            int rank = int.Parse(parts[1]);
            bool isFaceUp = parts[2] == "up";

            CardModel model = new CardModel(suit, rank);
            CardController card = modeManager.cardFactory.CreateCard(model, target.Transform, Vector2.zero);
            if (card == null) continue;

            var sultanCard = card.gameObject.GetComponent<SultanCardController>();
            if (sultanCard == null) sultanCard = card.gameObject.AddComponent<SultanCardController>();
            sultanCard.cardModel = model;
            sultanCard.canvas = modeManager.RootCanvas;
            card.gameObject.name = $"Card_{parts[0]}_{rank}";
            modeManager.dragManager?.RegisterCardEvents(sultanCard);

            target.AcceptCard(sultanCard);

            var cardDataComp = sultanCard.GetComponent<CardData>();
            if (cardDataComp != null) cardDataComp.SetFaceUp(isFaceUp, false);

            var cg = card.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;

            cardsToAnimate.Add(new TutorialCardAnimData { card = card, targetContainer = target });
        }
    }

    private IEnumerator AnimateCardsDescent()
    {
        float duration = 0.7f;
        float elapsed = 0f;

        for (int i = 0; i < cardsToAnimate.Count; i++)
        {
            var animData = cardsToAnimate[i];
            if (animData.card == null) continue;

            animData.finalWorldPos = animData.card.transform.position;
            animData.card.transform.SetParent(modeManager.DragLayer, true);
            animData.card.transform.position = animData.finalWorldPos + new Vector3(0, 2500f, 0);

            var cg = animData.card.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = false; }
            cardsToAnimate[i] = animData;
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Whoosh_Out");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            foreach (var animData in cardsToAnimate)
            {
                if (animData.card != null)
                {
                    Vector3 startPos = animData.finalWorldPos + new Vector3(0, 2500f, 0);
                    animData.card.transform.position = Vector3.Lerp(startPos, animData.finalWorldPos, easedT);
                }
            }
            yield return null;
        }

        foreach (var animData in cardsToAnimate)
        {
            if (animData.card != null)
            {
                animData.card.transform.position = animData.finalWorldPos;
                animData.targetContainer.AcceptCard(animData.card);
            }
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");
    }

    private void InitializeDefaultSteps()
    {
        string clrOrg = "<color=#FCA311>";
        string clrRed = "<color=#FF6B6B>";
        string clrBlk = "<color=#8294FF>";
        string endClr = "</color>";

        // Шаг 1
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial1",
            fallbackText = $"Цель игры — собрать все карты в {clrOrg}Дома{endClr} вокруг Султана.\nПеренесите {clrRed}Валета Червей (J♥){endClr} из {clrOrg}Резерва{endClr} в подходящий Дом.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Hearts_11",
            expectedTargetPileName = "Foundation",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Reserve_0" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 0
        });

        // Шаг 2
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial2",
            fallbackText = $"В 'Султане' колоду можно перелистывать {clrOrg}только 2 раза{endClr}!\nНажмите на {clrOrg}колоду{endClr}, чтобы открыть новую карту.",
            expectedAction = TutorialActionType.ClickStock,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Stock" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 1
        });

        // Шаг 3
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial3",
            fallbackText = $"Нам выпал {clrBlk}Валет Треф (J♣){endClr}. Домам Треф сначала нужны Десятки.\nПеренесите его в {clrOrg}пустую ячейку Резерва{endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Clubs_11",
            expectedTargetPileName = "Reserve",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Waste" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 2
        });

        // Шаг 4
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial4",
            fallbackText = $"Отличный ход! Но пустые резервы лучше беречь.\nНажмите кнопку {clrOrg}'Отмена'{endClr} внизу экрана.",
            expectedAction = TutorialActionType.Undo,
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_Undo" },
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 3
        });

        // Шаг 5
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial5",
            fallbackText = $"Давайте посмотрим следующую карту.\nНажмите на {clrOrg}колоду{endClr} еще раз.",
            expectedAction = TutorialActionType.ClickStock,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Stock" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 1
        });

        // Шаг 6
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial6",
            fallbackText = $"Это {clrRed}Валет Бубен (J♦){endClr}! Он идеально подходит.\nОтправьте его из сброса прямо в нужный {clrOrg}Дом{endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Diamonds_11",
            expectedTargetPileName = "Foundation",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Waste" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 2
        });

        // Шаг 7 (НОВЫЙ: Обучение двойному клику - в Резерв)
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial7",
            fallbackText = $"Карты можно быстро перемещать {clrOrg}двойным кликом{endClr}. Игра сама выберет место: сначала Дома, затем Резерв.\nКликните дважды по {clrBlk}Валету Треф (J♣){endClr}.",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Clubs_11",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Waste" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 2
        });

        // Шаг 8 (НОВЫЙ: Тянем карту для дома)
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial8",
            fallbackText = $"Отлично! Валет улетел в пустой Резерв. Теперь давайте возьмем еще одну карту из {clrOrg}колоды{endClr}.",
            expectedAction = TutorialActionType.ClickStock,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Stock" },
            arrowType = TutorialArrowType.Left,
            arrowAnchorIndex = 1
        });

        // Шаг 9 (НОВЫЙ: Обучение двойному клику - в Дом)
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial9",
            fallbackText = $"Это {clrBlk}Десятка Треф (10♣){endClr}, и она нужна Домам! Кликните по ней {clrOrg}дважды{endClr}, и она полетит прямиком на базу.",
            expectedAction = TutorialActionType.DoubleClick,
            expectedCardName = "Clubs_10",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            highlightElements = new List<string> { "Highlight_Waste" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 2
        });

        // Шаг 10 (Свободная игра)
        steps.Add(new TutorialStep
        {
            localizationKey = "SultanTutorial10",
            fallbackText = $"Вы освоили все правила пасьянса Султан!\nЗавершите игру самостоятельно, распределив {clrOrg}оставшиеся карты{endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "",
            expectedTargetPileName = "Foundation",
            panelAnchor = TutorialPanelAnchorType.Top,
            arrowType = TutorialArrowType.None
        });
    }

    public void HidePanelToLeft()
    {
        if (tutorialUIPanel == null || !tutorialUIPanel.gameObject.activeInHierarchy) return;
        if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
        panelMoveCoroutine = StartCoroutine(MovePanelOutRoutine(tutorialUIPanel.anchoredPosition + new Vector2(-2500f, 0)));
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

    public void HideHighlights() => DisableAllHighlights();

    public void RestorePanelPosition()
    {
        if (!IsTutorialActive) return;
        tutorialUIPanel.gameObject.SetActive(true);
        UpdateUI();
    }
}