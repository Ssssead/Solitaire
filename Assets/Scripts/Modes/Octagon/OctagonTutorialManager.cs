using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class OctagonTutorialManager : MonoBehaviour, ITutorialManager
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

    [Header("Arrows")]
    public RectTransform arrowDown;
    public RectTransform arrowUp;
    public RectTransform arrowLeft;
    [Tooltip("Массив якорей. Создайте пустышки над нужными картами и назначьте их сюда.")]
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
    private bool stepTransitioning = false;
    private Coroutine panelMoveCoroutine;

    private OctagonModeManager modeManager;
    private OctagonPileManager pileManager;
    private bool lastDragState = false;
    private int lastArrowStepIndex = -1;

    private void Awake()
    {
        modeManager = FindObjectOfType<OctagonModeManager>();
        pileManager = FindObjectOfType<OctagonPileManager>();
        InitializeSteps();
    }

    private void InitializeSteps()
    {
        steps.Clear();

        steps.Add(new TutorialStep
        {
            localizationKey = "OctagonTutorial1",
            fallbackText = "Добро пожаловать в Восьмиугольник! До победы осталось совсем немного. Давайте открывать новые карты: кликните по колоде. Учтите: за всю игру колоду можно перелистать <color=#FCA311>только 2 раза</color>!",
            expectedAction = TutorialActionType.ClickStock,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 0
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "OctagonTutorial2",
            fallbackText = "На столе карты собираются по убыванию, при этом масть может быть <color=#FCA311>ЛЮБОЙ</color>. Перенесите <color=#FF6B6B>Даму Червей (Q♥)</color> из сброса на <color=#8294FF>Короля Треф (K♣)</color> на столе.",
            expectedAction = TutorialActionType.MoveCard,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 1,
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 2
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "OctagonTutorial3",
            fallbackText = "В левой группе ячеек осталась всего одна карта. Перенесите <color=#8294FF>Валета Треф (J♣)</color> на нашу <color=#FF6B6B>Даму Червей (Q♥)</color>. Группа опустеет и <color=#FCA311>автоматически заполнится</color> новыми картами!",
            expectedAction = TutorialActionType.MoveCard,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 3,
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 2
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "OctagonTutorial4",
            fallbackText = "Новые карты нам сейчас не помогли, а Валет перекрыл нужную нам карту. Давайте вернем всё назад! Нажмите кнопку <color=#FCA311>'Отменить ход'</color>.",
            expectedAction = TutorialActionType.Undo,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 4
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "OctagonTutorial5",
            fallbackText = "Иногда для спасения ситуации можно забрать нужную карту прямо из Дома! (Нельзя брать только Тузов и Королей). Вытащите <color=#FF6B6B>Даму Бубен (Q♦)</color> из Дома и положите её на <color=#8294FF>Короля Пик (K♠)</color>.",
            expectedAction = TutorialActionType.MoveCard,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 5,
            arrowType2 = TutorialArrowType.Down,
            arrowAnchorIndex2 = 6
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "OctagonTutorial6",
            fallbackText = "Блестяще! Двойной клик отправляет карту в Дом, ИЛИ на стол, но на столе <color=#FCA311>масть должна совпасть точь-в-точь</color>. Дважды кликните по <color=#FF6B6B>Валету Бубен (J♦)</color>.",
            expectedAction = TutorialActionType.DoubleClick,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 7
        });

        steps.Add(new TutorialStep
        {
            localizationKey = "OctagonTutorial7",
            fallbackText = "Убрав Валета, мы разблокировали финальные карты! Теперь дважды кликните по открывшемуся <color=#FF6B6B>Королю Червей (K♥)</color>, и он отправится прямиком в Дом.",
            expectedAction = TutorialActionType.DoubleClick,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 8
        });

        // 8-й шаг (Свободная игра)
        steps.Add(new TutorialStep
        {
            localizationKey = "OctagonTutorial8",  // <-- Изменено с End на 8
            fallbackText = "Вы освоили все механики Восьмиугольника!\nНа столе осталось всего несколько открытых карт. Раскидайте их по Домам и завершите игру!",
            expectedAction = TutorialActionType.MoveCard,
            panelAnchor = TutorialPanelAnchorType.Center,
            arrowType = TutorialArrowType.None
        });
    }

    public void StartTutorial()
    {
        IsTutorialActive = true;
        currentStepIndex = 0;

        tutorialUIPanel.gameObject.SetActive(true);
        if (undoAllButton) undoAllButton.interactable = false;

        if (arrowAnimCoroutine != null) StopCoroutine(arrowAnimCoroutine);
        arrowAnimCoroutine = StartCoroutine(AnimateArrowsRoutine());

        ShowStep(0);
    }
    public void ShowVictoryStep(string localizedVictoryText)
    {
        if (tutorialUIPanel != null)
        {
            tutorialUIPanel.gameObject.SetActive(true);
            instructionText.text = localizedVictoryText;

            if (stepIndicatorText != null) stepIndicatorText.gameObject.SetActive(false);

            if (centerAnchor != null)
            {
                if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
                // Запускаем перелет с передачей якоря
                panelMoveCoroutine = StartCoroutine(MovePanelRoutine(centerAnchor));
            }

            if (arrowDown) arrowDown.gameObject.SetActive(false);
            if (arrowUp) arrowUp.gameObject.SetActive(false);
            if (arrowLeft) arrowLeft.gameObject.SetActive(false);
        }
    }
    public Deal GenerateTutorialDeal()
    {
        Deal deal = new Deal();
        for (int i = 0; i < 4; i++) deal.tableau.Add(new List<CardInstance>());
        for (int i = 0; i < 8; i++) deal.foundations.Add(new List<CardModel>());

        for (int i = 1; i <= 13; i++) deal.foundations[0].Add(new CardModel(Suit.Spades, i));
        for (int i = 1; i <= 11; i++) deal.foundations[1].Add(new CardModel(Suit.Spades, i));

        for (int i = 1; i <= 12; i++) deal.foundations[2].Add(new CardModel(Suit.Hearts, i));
        for (int i = 1; i <= 11; i++) deal.foundations[3].Add(new CardModel(Suit.Hearts, i));

        for (int i = 1; i <= 10; i++) deal.foundations[4].Add(new CardModel(Suit.Clubs, i));
        for (int i = 1; i <= 12; i++) deal.foundations[5].Add(new CardModel(Suit.Clubs, i));

        for (int i = 1; i <= 12; i++) deal.foundations[6].Add(new CardModel(Suit.Diamonds, i)); // Верхняя Q♦ (ее снимем в Шаге 5)
        for (int i = 1; i <= 9; i++) deal.foundations[7].Add(new CardModel(Suit.Diamonds, i));  // ИСПРАВЛЕНО: До 9-ки! Чтобы Дом 100% отверг J♦ и он полетел на Стол

        // Игровой стол (Tableau)
        deal.tableau[0].Add(new CardInstance(new CardModel(Suit.Clubs, 11), true));    // J♣
        deal.tableau[1].Add(new CardInstance(new CardModel(Suit.Clubs, 13), true));    // K♣
        deal.tableau[2].Add(new CardInstance(new CardModel(Suit.Spades, 13), true));   // K♠
        deal.tableau[3].Add(new CardInstance(new CardModel(Suit.Diamonds, 11), true)); // J♦
        deal.tableau[3].Add(new CardInstance(new CardModel(Suit.Hearts, 13), false));  // K♥

        // ИСПРАВЛЕНО: Колода (Stock) - Порядок обратный, чтобы Дама Червей (Q♥) легла на самый верх стопки!
        deal.stock.Push(new CardInstance(new CardModel(Suit.Hearts, 12), false));   // Q♥ (Откроется по ПЕРВОМУ клику)
        deal.stock.Push(new CardInstance(new CardModel(Suit.Spades, 12), false));   // Q♠
        deal.stock.Push(new CardInstance(new CardModel(Suit.Hearts, 13), false));   // K♥
        deal.stock.Push(new CardInstance(new CardModel(Suit.Clubs, 13), false));    // K♣
        deal.stock.Push(new CardInstance(new CardModel(Suit.Clubs, 12), false));    // Q♣
        deal.stock.Push(new CardInstance(new CardModel(Suit.Diamonds, 10), false)); // 10♦
        deal.stock.Push(new CardInstance(new CardModel(Suit.Diamonds, 13), false)); // K♦
        deal.stock.Push(new CardInstance(new CardModel(Suit.Diamonds, 12), false)); // Q♦
        deal.stock.Push(new CardInstance(new CardModel(Suit.Diamonds, 13), false)); // K♦ 

        return deal;
    }

    private void Update()
    {
        if (!IsTutorialActive || stepTransitioning || steps.Count == 0) return;

        // 1. ПЕРЕТАСКИВАЕМЫЕ КАРТЫ: Ищем везде, кроме Домов/Колоды (чтобы скрипт видел их "в воздухе" для стрелочек)
        CardController cardQH = GetCardByCondition(Suit.Hearts, 12, c => c.GetComponentInParent<OctagonFoundationPile>() == null);
        CardController cardJC = GetCardByCondition(Suit.Clubs, 11, c => c.GetComponentInParent<OctagonFoundationPile>() == null);
        CardController cardQD = GetCardByCondition(Suit.Diamonds, 12, c => c.GetComponentInParent<OctagonStockPile>() == null);
        CardController cardJD = GetCardByCondition(Suit.Diamonds, 11, c => c.GetComponentInParent<OctagonFoundationPile>() == null);
        CardController cardKH = GetCardByCondition(Suit.Hearts, 13, c => c.GetComponentInParent<OctagonStockPile>() == null && c.GetComponentInParent<OctagonWastePile>() == null);

        // 2. КАРТЫ-ЦЕЛИ (K♣, K♠): Ищем СТРОГО на столе, чтобы не перепутать с их дубликатами в колоде!
        CardController cardKC = GetCardByCondition(Suit.Clubs, 13, c => c.GetComponentInParent<OctagonTableauSlot>() != null);
        CardController cardKS = GetCardByCondition(Suit.Spades, 13, c => c.GetComponentInParent<OctagonTableauSlot>() != null);

        EnforceStrictInputRestrictions(cardQH, cardJC, cardQD, cardJD, cardKH);
        UpdateDynamicArrows(cardQH, cardJC, cardQD);

        if (currentStepIndex == 0)
        {
            if (cardQH != null && cardQH.GetComponentInParent<OctagonWastePile>()) NextStep();
        }
        else if (currentStepIndex == 1)
        {
            // Q♥ перенесли на K♣ (теперь скрипт точно смотрит на того Короля, что лежит на столе)
            if (cardQH != null && cardKC != null && cardQH.transform.parent == cardKC.transform.parent) NextStep();
        }
        else if (currentStepIndex == 2)
        {
            if (cardJC != null && cardQH != null && cardJC.transform.parent == cardQH.transform.parent) NextStep();
        }
        else if (currentStepIndex == 3)
        {
            if (cardJC != null && cardQH != null && cardJC.transform.parent != cardQH.transform.parent) NextStep();
        }
        else if (currentStepIndex == 4)
        {
            if (cardQD != null && cardKS != null && cardQD.transform.parent == cardKS.transform.parent) NextStep();
        }
        else if (currentStepIndex == 5)
        {
            if (cardJD != null && cardQD != null && cardJD.transform.parent == cardQD.transform.parent) NextStep();
        }
        else if (currentStepIndex == 6)
        {
            if (cardKH != null && cardKH.GetComponentInParent<OctagonFoundationPile>()) NextStep();
        }
    }
    private void UpdateDynamicArrows(CardController qh, CardController jc, CardController qd)
    {
        if (currentStepIndex >= steps.Count) return;
        TutorialStep step = steps[currentStepIndex];

        bool isDraggingTargetCard = false;

        // Проверяем, находится ли нужная карта "в воздухе" (то есть оторвана от слота ICardContainer)
        if (currentStepIndex == 1 && qh != null)
        {
            isDraggingTargetCard = qh.GetComponentInParent<ICardContainer>() == null;
        }
        else if (currentStepIndex == 2 && jc != null)
        {
            isDraggingTargetCard = jc.GetComponentInParent<ICardContainer>() == null;
        }
        else if (currentStepIndex == 4 && qd != null)
        {
            isDraggingTargetCard = qd.GetComponentInParent<ICardContainer>() == null;
        }

        // Обновляем стрелки ТОЛЬКО если изменился шаг или мы только что взяли/положили карту (оптимизация)
        if (lastArrowStepIndex != currentStepIndex || lastDragState != isDraggingTargetCard)
        {
            lastArrowStepIndex = currentStepIndex;
            lastDragState = isDraggingTargetCard;

            if (arrowDown) arrowDown.gameObject.SetActive(false);
            if (arrowUp) arrowUp.gameObject.SetActive(false);
            if (arrowLeft) arrowLeft.gameObject.SetActive(false);

            // Если у шага нет второй стрелки ИЛИ карту еще не подняли -> показываем первую стрелку
            if (!isDraggingTargetCard || step.arrowType2 == TutorialArrowType.None)
            {
                SetupArrow(step.arrowType, step.arrowAnchorIndex);
            }
            else
            {
                // Карту подняли (Drag) -> переключаем на вторую стрелку (место назначения)
                SetupArrow(step.arrowType2, step.arrowAnchorIndex2);
            }
        }
    }
    // Хелпер для надежного поиска нужной карты
    private CardController GetCardByCondition(Suit suit, int rank, Func<CardController, bool> condition)
    {
        CardController[] allCards = FindObjectsOfType<CardController>();
        foreach (var c in allCards)
        {
            if (c.cardModel.suit == suit && c.cardModel.rank == rank && condition(c)) return c;
        }
        return null;
    }

    private void EnforceStrictInputRestrictions(CardController qh, CardController jc, CardController qd, CardController jd, CardController kh)
    {
        // =========================================================
        // ЖЕСТКАЯ БЛОКИРОВКА КНОПОК КАЖДЫЙ КАДР
        // (Подавляем базовую логику ModeManager, которая включает их после хода)
        // =========================================================
        if (undoButton != null) undoButton.interactable = (currentStepIndex == 3); // Включена ТОЛЬКО на 4 шаге
        if (undoAllButton != null) undoAllButton.interactable = false;             // Выключена всегда

        if (currentStepIndex >= 7)
        {
            var endStockCg = pileManager.StockPile.GetComponent<CanvasGroup>();
            if (endStockCg != null) endStockCg.blocksRaycasts = modeManager.IsInputAllowed;

            CardController[] allSceneCards = FindObjectsOfType<CardController>();
            foreach (var card in allSceneCards)
            {
                var cg = card.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = modeManager.IsInputAllowed;
            }

            return; // Выходим, больше жесткий контроль карт не нужен
        }

        CardController allowedCard = null;
        bool allowStockClick = false;

        switch (currentStepIndex)
        {
            case 0: allowStockClick = true; break;
            case 1: allowedCard = qh; break;
            case 2: allowedCard = jc; break;
            case 3: break;
            case 4: allowedCard = qd; break;
            case 5: allowedCard = jd; break;
            case 6: allowedCard = kh; break;
        }

        var stockCg = pileManager.StockPile.GetComponent<CanvasGroup>();
        if (stockCg == null) stockCg = pileManager.StockPile.gameObject.AddComponent<CanvasGroup>();
        stockCg.blocksRaycasts = allowStockClick && modeManager.IsInputAllowed;

        CardController[] allCards = FindObjectsOfType<CardController>();
        foreach (var card in allCards)
        {
            var cg = card.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = (card == allowedCard) && modeManager.IsInputAllowed;
        }
    }

    private void NextStep()
    {
        if (stepTransitioning) return;
        StartCoroutine(TransitionStepRoutine());
    }

    private IEnumerator TransitionStepRoutine()
    {
        stepTransitioning = true;

        if (arrowDown) arrowDown.gameObject.SetActive(false);
        if (arrowUp) arrowUp.gameObject.SetActive(false);
        if (arrowLeft) arrowLeft.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.4f);

        currentStepIndex++;
        if (currentStepIndex < steps.Count) ShowStep(currentStepIndex);
        else EndTutorial();

        stepTransitioning = false;
    }

    // ИСПРАВЛЕНО: Добавлена плавная анимация перелета панели (как в Косынке)
    private void ShowStep(int index)
    {
        TutorialStep step = steps[index];
        instructionText.text = step.fallbackText;
        if (stepIndicatorText != null) stepIndicatorText.text = (index + 1) + "/" + steps.Count;

        RectTransform targetAnchor = bottomAnchor;
        if (step.panelAnchor == TutorialPanelAnchorType.Top) targetAnchor = topAnchor;
        else if (step.panelAnchor == TutorialPanelAnchorType.Center) targetAnchor = centerAnchor;

        if (targetAnchor != null && tutorialUIPanel != null)
        {
            tutorialUIPanel.gameObject.SetActive(true);

            if (index > 0)
            {
                if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
                panelMoveCoroutine = StartCoroutine(MovePanelRoutine(targetAnchor));
            }
            else
            {
                tutorialUIPanel.anchorMin = targetAnchor.anchorMin;
                tutorialUIPanel.anchorMax = targetAnchor.anchorMax;
                tutorialUIPanel.pivot = targetAnchor.pivot;
                tutorialUIPanel.sizeDelta = targetAnchor.sizeDelta;
                tutorialUIPanel.anchoredPosition = targetAnchor.anchoredPosition;
            }
        }

        // =========================================================
        // ЖЕСТКАЯ БЛОКИРОВКА КНОПОК
        // =========================================================
        if (undoButton != null) undoButton.interactable = (index == 3); // Разрешаем ТОЛЬКО на 4-м шаге (индекс 3)
        if (undoAllButton != null) undoAllButton.interactable = false;  // Выключена всегда

        // Принудительно сбрасываем кэш, чтобы UpdateDynamicArrows мгновенно перерисовал стрелки для нового шага
        lastArrowStepIndex = -1;
    }

    // Корутина плавного движения панели
    private IEnumerator MovePanelRoutine(RectTransform targetRect)
    {
        if (targetRect == null) yield break;

        // 1. Запоминаем текущую мировую позицию панели, чтобы она не "прыгнула" при смене якоря
        Vector3 startWorldPos = tutorialUIPanel.position;

        // 2. КОПИРУЕМ ЯКОРЯ И ПИВОТ ЦЕЛЕВОЙ ТОЧКИ В ПАНЕЛЬ
        tutorialUIPanel.anchorMin = targetRect.anchorMin;
        tutorialUIPanel.anchorMax = targetRect.anchorMax;
        tutorialUIPanel.pivot = targetRect.pivot;
        tutorialUIPanel.sizeDelta = targetRect.sizeDelta;

        // 3. Возвращаем панель на место (теперь уже в новой системе координат якоря)
        tutorialUIPanel.position = startWorldPos;

        Vector2 startAnchoredPos = tutorialUIPanel.anchoredPosition;
        Vector2 targetAnchoredPos = targetRect.anchoredPosition;

        // Определяем скорость в зависимости от расстояния
        float distance = Vector2.Distance(startAnchoredPos, targetAnchoredPos);
        float duration = distance > 1000f ? 0.6f : 0.3f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            // Плавное замедление в конце (как в Султане)
            t = 1f - Mathf.Pow(1f - t, 3f);
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startAnchoredPos, targetAnchoredPos, t);
            yield return null;
        }
        tutorialUIPanel.anchoredPosition = targetAnchoredPos;
    }

    private void SetupArrow(TutorialArrowType type, int anchorIndex)
    {
        if (type == TutorialArrowType.None || anchorIndex < 0 || anchorIndex >= arrowAnchors.Length) return;

        RectTransform anchor = arrowAnchors[anchorIndex];
        if (anchor == null) return;

        RectTransform activeArrow = null;
        if (type == TutorialArrowType.Down) activeArrow = arrowDown;
        else if (type == TutorialArrowType.Up) activeArrow = arrowUp;
        else if (type == TutorialArrowType.Left) activeArrow = arrowLeft;

        if (activeArrow != null)
        {
            activeArrow.gameObject.SetActive(true);
            activeArrow.SetParent(anchor, false);
            activeArrow.anchoredPosition = Vector2.zero;
        }
    }

    private IEnumerator AnimateArrowsRoutine()
    {
        while (true)
        {
            float bounce = Mathf.Sin(Time.unscaledTime * arrowBounceSpeed) * arrowBounceAmplitude;
            Vector2 offset = new Vector2(0, bounce);

            if (arrowDown && arrowDown.gameObject.activeSelf) arrowDown.anchoredPosition = offset;
            if (arrowUp && arrowUp.gameObject.activeSelf) arrowUp.anchoredPosition = offset;
            if (arrowLeft && arrowLeft.gameObject.activeSelf) arrowLeft.anchoredPosition = new Vector2(bounce, 0);

            yield return null;
        }
    }

    public void EndTutorial()
    {
        IsTutorialActive = false;
        tutorialUIPanel.gameObject.SetActive(false);

        if (arrowDown) arrowDown.gameObject.SetActive(false);
        if (arrowUp) arrowUp.gameObject.SetActive(false);
        if (arrowLeft) arrowLeft.gameObject.SetActive(false);

        if (arrowAnimCoroutine != null) StopCoroutine(arrowAnimCoroutine);

        if (undoButton != null) undoButton.interactable = true;
        if (undoAllButton != null) undoAllButton.interactable = true;

        CardController[] allCards = FindObjectsOfType<CardController>();
        foreach (var card in allCards)
        {
            var cg = card.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;
        }

        var stockCg = pileManager.StockPile.GetComponent<CanvasGroup>();
        if (stockCg != null) stockCg.blocksRaycasts = true;

        GameSettings.IsTutorialMode = false;
    }

    public void HidePanelToLeft()
    {
        if (tutorialUIPanel == null || !tutorialUIPanel.gameObject.activeInHierarchy) return;
        if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
        panelMoveCoroutine = StartCoroutine(MovePanelOutRoutine(tutorialUIPanel.anchoredPosition + new Vector2(-2500f, 0)));
    }

    public void RestorePanelPosition()
    {
        // --- ИСПРАВЛЕНИЕ: Блокируем показ панели, если это не обучающая игра ---
        if (!IsTutorialActive) return;

        if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
        ShowStep(currentStepIndex);
    }

    private IEnumerator MovePanelOutRoutine(Vector2 targetAnchoredPos)
    {
        Vector2 startPos = tutorialUIPanel.anchoredPosition;
        float elapsed = 0f, duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startPos, targetAnchoredPos, elapsed / duration);
            yield return null;
        }
        tutorialUIPanel.anchoredPosition = targetAnchoredPos;
        tutorialUIPanel.gameObject.SetActive(false);
    }

    public void HideHighlights() { }
}