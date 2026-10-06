using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using YG;

public class SultanModeManager : MonoBehaviour, IModeManager, ICardGameMode, ICardClickReceiver
{
    [Header("Core References")]
    public CardFactory cardFactory;
    public Canvas rootCanvas;
    public RectTransform dragLayer;

    [Header("UI & HUD")]
    public GameUIController gameUI;
    [Header("UI & HUD - Landscape")]
    public TMP_Text movesText;
    public TMP_Text scoreText;
    public TMP_Text timeText;

    [Header("UI & HUD - Portrait")]
    public TMP_Text portraitMovesText;
    public TMP_Text portraitScoreText;
    public TMP_Text portraitTimeText;

    [Header("Services")]
    public SultanPileManager pileManager;
    public SultanDeckManager deckManager;
    public DragManager dragManager;
    public UndoManager undoManager;
    public AnimationService animationService;
    public SultanAutoMoveService autoMoveService;
    public SultanScoreManager scoreManager;
    public SultanTutorialManager tutorialManager;
    public SultanIntroController introController; // ⚡ ДОБАВЛЕНО: Ссылка на аниматор появления
    [Header("Hint System")]
    public SultanHintSolver hintSolver;
    private Coroutine backgroundSolverCoroutine = null;
    private List<SultanHintMove> cachedHintPath = null;
    [HideInInspector] public bool isExecutingHint = false;
    public bool IsInputAllowed { get; set; } = true;
    public GameType GameType => GameType.Sultan;
    public Difficulty SelectedDifficulty => GameSettings.CurrentDifficulty;
    public ITutorialManager Tutorial => tutorialManager;
    public string GameName => "Sultan";

    public AnimationService AnimationService => animationService;
    public PileManager PileManager => pileManager;
    public RectTransform DragLayer => dragLayer;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;

    private bool hasWonGame = false;
    private bool hasGameStarted = false;
    [HideInInspector] public bool isRestarting = false;

    private float gameTimer = 0f;
    private bool isTimerRunning = false;
    private Coroutine defeatRoutine;
    private int lastWasteChildCount = -1;
    private Transform lastWasteTopCard = null;
    private Dictionary<ICardContainer, System.Reflection.FieldInfo> containerFields = new Dictionary<ICardContainer, System.Reflection.FieldInfo>();
    private float undoLockTime = 0f;
    private const float POST_DRAG_LOCK_DURATION = 0.3f;
    private HashSet<CardController> previousDragLayerCards = new HashSet<CardController>();
    // ---> ПАМЯТЬ КВЕСТОВ ДЛЯ ОТМЕНЫ В СУЛТАНЕ <---
    private struct SultanQuestUndoRecord
    {
        public bool IsToFoundation;
        public bool CompletedFoundation;
        public bool FromWasteToFoundation;
        public bool FromReserve;
        public int CardRank;
    }
    private Stack<SultanQuestUndoRecord> questUndoStack = new Stack<SultanQuestUndoRecord>();
    private void Awake()
    {
        pileManager.Initialize(this);
        dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);
        deckManager.Initialize(this, cardFactory, pileManager);
        autoMoveService.Initialize(this, pileManager, undoManager, animationService, dragLayer);

        if (undoManager != null) undoManager.Initialize(this);

        if (gameUI != null)
        {
            var field = gameUI.GetType().GetField("activeGameMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(gameUI, this);
            }
        }
    }

    private void Start()
    {
        StartNewGame();
        SetupUndoSounds();
    }

    private void Update()
    {
        if (isTimerRunning && !hasWonGame)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }

        bool mouseReleased = Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended);

        if (mouseReleased)
        {
            foreach (var card in previousDragLayerCards)
            {
                if (card != null && card.transform.parent != dragLayer)
                {
                    StartCoroutine(TemporaryElevateCardRoutine(card, 0.35f));
                }
            }
        }

        previousDragLayerCards.Clear();
        if (dragLayer != null)
        {
            for (int i = 0; i < dragLayer.childCount; i++)
            {
                var card = dragLayer.GetChild(i).GetComponent<CardController>();
                if (card != null) previousDragLayerCards.Add(card);
            }
        }

        if (pileManager != null && pileManager.WastePile != null)
        {
            SyncHierarchyToLogicalList(pileManager.WastePile);

            int currentCount = pileManager.WastePile.transform.childCount;
            Transform currentTop = currentCount > 0 ? pileManager.WastePile.transform.GetChild(currentCount - 1) : null;

            if (currentCount != lastWasteChildCount || currentTop != lastWasteTopCard)
            {
                for (int i = 0; i < currentCount; i++)
                {
                    var cg = pileManager.WastePile.transform.GetChild(i).GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        bool isTop = (i == currentCount - 1);
                        cg.blocksRaycasts = isTop;
                        cg.interactable = isTop;
                    }
                }
                lastWasteChildCount = currentCount;
                lastWasteTopCard = currentTop;
            }
        }
    }

    private IEnumerator TemporaryElevateCardRoutine(CardController card, float duration)
    {
        if (card == null) yield break;

        Canvas tempCanvas = card.GetComponent<Canvas>();
        bool addedCanvas = false;

        if (tempCanvas == null)
        {
            tempCanvas = card.gameObject.AddComponent<Canvas>();
            addedCanvas = true;
        }

        int oldOrder = tempCanvas.sortingOrder;
        bool oldOverride = tempCanvas.overrideSorting;

        tempCanvas.overrideSorting = true;
        tempCanvas.sortingOrder = 30000;

        yield return new WaitForSeconds(duration);

        if (card != null && tempCanvas != null)
        {
            if (addedCanvas)
            {
                Destroy(tempCanvas);
            }
            else
            {
                tempCanvas.overrideSorting = oldOverride;
                tempCanvas.sortingOrder = oldOrder;
            }
        }
    }

    private void InitializeComponents()
    {
        if (cardFactory == null) cardFactory = FindObjectOfType<CardFactory>();
        if (animationService == null) animationService = FindObjectOfType<AnimationService>();
        if (undoManager == null) undoManager = FindObjectOfType<UndoManager>();
        if (dragManager == null) dragManager = FindObjectOfType<DragManager>();

        if (pileManager != null) pileManager.Initialize(this);
        if (deckManager != null) deckManager.Initialize(this, cardFactory, pileManager);
        if (undoManager != null) undoManager.Initialize(this);
        if (dragManager != null) dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);
        if (autoMoveService != null) autoMoveService.Initialize(this, pileManager, undoManager, animationService, dragLayer);

        CacheContainerFields();
    }

    private void CacheContainerFields()
    {
        if (pileManager == null) return;
        foreach (var container in pileManager.GetAllContainers())
        {
            if (container == null) continue;
            var type = container.GetType();
            var field = type.GetField("cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                     ?? type.GetField("_cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            if (field != null) containerFields[container] = field;
        }
    }

    private void SyncHierarchyToLogicalList(ICardContainer container)
    {
        if (container == null || !containerFields.ContainsKey(container)) return;

        var field = containerFields[container];
        if (field.GetValue(container) is System.Collections.IList list)
        {
            Transform parentTransform = ((MonoBehaviour)container).transform;
            int validIndex = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var card = list[i] as CardController;
                if (card != null && card.transform.parent == parentTransform)
                {
                    if (card.transform.GetSiblingIndex() != validIndex)
                    {
                        card.transform.SetSiblingIndex(validIndex);
                    }
                    validIndex++;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
            {
                StatisticsManager.Instance.OnGameAbandoned();
            }
            // ДОБАВИТЬ ЭТУ СТРОКУ:
            SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }

        // ⚡ ДОБАВЛЕНО: Сброс флага туториала при закрытии игры
        GameSettings.IsTutorialMode = false;
    }

    // ==========================================================
    // ⚡ ТОЧКИ ВХОДА ИЗ ГЛАВНОГО МЕНЮ (ДЛЯ НАСТРОЙКИ КНОПОК) ⚡
    // ==========================================================
    public void LaunchTutorialFromMenu(GameObject sultanPanel)
    {
        GameSettings.IsTutorialMode = true;
        if (sultanPanel != null) sultanPanel.SetActive(true);
        StartNewGame();
    }

    public void LaunchRegularGameFromMenu(GameObject sultanPanel)
    {
        GameSettings.IsTutorialMode = false;
        if (sultanPanel != null) sultanPanel.SetActive(true);
        StartNewGame();
    }

    public void StartNewGame()
    {
        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }
        // ДОБАВИТЬ ЭТУ СТРОКУ:
        SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());

        // 2. ЗАТЕМ сообщаем трекеру настройки (У Султана всегда Standard)
        GameQuestTracker.Instance?.StartMatch("Sultan", GameSettings.CurrentDifficulty, "Standard");

        if (defeatRoutine != null)
        {
            StopCoroutine(defeatRoutine);
            defeatRoutine = null;
        }

        if (deckManager != null)
        {
            deckManager.currentDifficulty = SelectedDifficulty;
        }

        IsInputAllowed = false;
        hasWonGame = false;
        hasGameStarted = false;

        gameTimer = 0f;
        isTimerRunning = false;
        if (hintSolver != null) hintSolver.CancelSearch();
        cachedHintPath = null;
        // ВАЖНО: Очистка до развилки!
        pileManager.ClearAllPiles();
        pileManager.CreatePiles();
        dragManager.RefreshContainers();
        cardFactory.DestroyAllCards();

        if (undoManager != null) undoManager.ResetHistory();
        if (scoreManager != null) scoreManager.ResetScore();

        UpdateFullUI();

        // ==========================================
        // ⚡ ЛОГИКА ТУТОРИАЛА (КАК В МОНТЕ-КАРЛО) ⚡
        // ==========================================
        if (GameSettings.IsTutorialMode && tutorialManager != null)
        {
            tutorialManager.enabled = true;
            StartCoroutine(TutorialIntroRoutine());
        }
        else
        {
            // Обычная игра
            if (tutorialManager != null)
            {
                tutorialManager.enabled = false;
                if (tutorialManager.tutorialUIPanel != null)
                    tutorialManager.tutorialUIPanel.gameObject.SetActive(false);
            }
            deckManager.DealInitial();
        }

        isRestarting = false;
    }

    // ⚡ НОВАЯ КОРУТИНА: Последовательный запуск обучения
    private IEnumerator TutorialIntroRoutine()
    {
        if (introController == null) introController = GetComponent<SultanIntroController>();

        // ⚡ ИСПРАВЛЕНИЕ: Прячем слоты до начала анимации ⚡
        // Принудительно сбрасываем прозрачность всех контейнеров в 0, 
        // чтобы анимации интро-контроллера было откуда их плавно проявлять.
        if (pileManager != null)
        {
            foreach (var container in pileManager.GetAllContainers())
            {
                var mono = container as MonoBehaviour;
                if (mono != null)
                {
                    var cg = mono.GetComponent<CanvasGroup>();
                    if (cg == null) cg = mono.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;
                }
            }
        }

        if (introController != null)
        {
            // Теперь эта анимация честно проявит слоты из 0 в 1
            yield return StartCoroutine(introController.AnimateUIAndSlots(isRestarting));
        }

        if (tutorialManager != null)
        {
            // Передаем управление скрипту туториала (он выставит нужные карты и спустит их сверху)
            yield return StartCoroutine(tutorialManager.PlayTutorialIntro(null));
        }
    }

    public void CheckGameState()
    {
        if (hasWonGame) return;

        bool allQueens = true;
        foreach (var foundation in pileManager.Foundations)
        {
            if (!foundation.IsComplete())
            {
                allQueens = false;
                break;
            }
        }

        if (allQueens)
        {
            hasWonGame = true;
            IsInputAllowed = false;
            isTimerRunning = false;
            undoManager?.ClearAndLock();

            if (StatisticsManager.Instance != null)
            {
                int finalMoves = StatisticsManager.Instance.GetCurrentMoves();
                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;

                StatisticsManager.Instance.OnGameWon(finalScore);
                if (gameUI != null) gameUI.OnGameWon(finalMoves);
            }

            SimpleMetricsTracker.Instance?.TrackLevelWin(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
            return;
        }

        if (defeatRoutine != null) StopCoroutine(defeatRoutine);
        defeatRoutine = StartCoroutine(ShowDefeatRoutine());
    }

    private IEnumerator ShowDefeatRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        if (hasWonGame) yield break;

        if (!HasAnyValidMove() && gameUI != null)
        {
            gameUI.OnGameLost();
        }
        defeatRoutine = null;
    }

    private bool HasAnyValidMove()
    {
        if (dragLayer != null && dragLayer.childCount > 0) return true;
        if (pileManager.StockPile != null && pileManager.StockPile.transform.childCount > 0) return true;
        if (pileManager.WastePile != null && pileManager.WastePile.transform.childCount > 0 && deckManager != null && deckManager.HasRecyclesRemaining) return true;

        bool hasEmptyReserve = false;
        foreach (var reserve in pileManager.Reserves)
        {
            if (reserve.GetComponentsInChildren<CardController>().Length == 0) { hasEmptyReserve = true; break; }
        }
        if (hasEmptyReserve && pileManager.WastePile != null && pileManager.WastePile.GetComponentsInChildren<CardController>().Length > 0) return true;

        foreach (var reserve in pileManager.Reserves)
        {
            var cardsInReserve = reserve.GetComponentsInChildren<CardController>();
            if (cardsInReserve.Length > 0)
            {
                var topCard = cardsInReserve[cardsInReserve.Length - 1];
                foreach (var foundation in pileManager.Foundations)
                {
                    if (foundation.CanAccept(topCard)) return true;
                }
            }
        }

        if (pileManager.WastePile != null)
        {
            var cardsInWaste = pileManager.WastePile.GetComponentsInChildren<CardController>();
            if (cardsInWaste.Length > 0)
            {
                var topCard = cardsInWaste[cardsInWaste.Length - 1];
                foreach (var foundation in pileManager.Foundations)
                {
                    if (foundation.CanAccept(topCard)) return true;
                }
            }
        }

        return false;
    }

    public void RegisterMoveAndStartIfNeeded()
    {
        // ИСПРАВЛЕНИЕ 2: Разрешаем старт таймера и учет хода для подсказки
        if (!IsInputAllowed && !isExecutingHint) return;

        GameQuestTracker.Instance?.RecordMove();

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true;

            if (StatisticsManager.Instance != null)
            {
                Difficulty currentDiff = GameSettings.CurrentDifficulty;
                string variant = GameSettings.GetCurrentVariantString(GameType.Sultan);
                StatisticsManager.Instance.OnGameStarted("Sultan", currentDiff, variant);
            }

            SimpleMetricsTracker.Instance?.TrackLevelStart(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }

        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.RegisterMove();
        }

        UpdateFullUI();
        CheckGameState();
        if (!isExecutingHint) StartBackgroundSolver();
    }

    private void UpdateFullUI()
    {
        string mText = "0";
        if (hasGameStarted && StatisticsManager.Instance != null)
        {
            mText = StatisticsManager.Instance.GetCurrentMoves().ToString();
        }
        if (movesText != null) movesText.text = mText;
        if (portraitMovesText != null) portraitMovesText.text = mText;

        string sText = "0";
        if (hasGameStarted && scoreManager != null)
        {
            sText = scoreManager.CurrentScore.ToString();
        }
        if (scoreText != null) scoreText.text = sText;
        if (portraitScoreText != null) portraitScoreText.text = sText;

        if (!hasGameStarted)
        {
            UpdateTimeUI();
        }
    }

    private void UpdateTimeUI()
    {
        int totalSeconds = Mathf.FloorToInt(gameTimer);
        string tText = string.Format("{0}:{1:00}", totalSeconds / 60, totalSeconds % 60);

        if (timeText != null) timeText.text = tText;
        if (portraitTimeText != null) portraitTimeText.text = tText;
    }

    public ICardContainer FindNearestContainer(CardController card, Vector2 anchoredPosition, float maxDistance)
    {
        ICardContainer bestContainer = null;
        float bestArea = 0f;
        Rect cardRect = GetWorldRect(card.rectTransform);
        List<ICardContainer> candidates = new List<ICardContainer>();

        candidates.AddRange(pileManager.Foundations);
        candidates.AddRange(pileManager.Reserves);

        foreach (var container in candidates)
        {
            Rect containerRect = GetWorldRect(container.Transform.GetComponent<RectTransform>());
            float area = GetIntersectionArea(cardRect, containerRect);

            if (area > bestArea && container.CanAccept(card))
            {
                bestArea = area;
                bestContainer = container;
            }
        }

        // ⚡ ДОБАВЛЕНО: Защита от перетаскивания во время туториала
        if (bestContainer != null && tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, bestContainer))
                return null;
        }

        return bestContainer;
    }

    private Rect GetWorldRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return new Rect(corners[0].x, corners[0].y, Mathf.Abs(corners[2].x - corners[0].x), Mathf.Abs(corners[2].y - corners[0].y));
    }

    private float GetIntersectionArea(Rect r1, Rect r2)
    {
        float w = Mathf.Min(r1.xMax, r2.xMax) - Mathf.Max(r1.xMin, r2.xMin);
        float h = Mathf.Min(r1.yMax, r2.yMax) - Mathf.Max(r1.yMin, r2.yMin);
        return (w > 0 && h > 0) ? w * h : 0f;
    }

    public bool OnDropToBoard(CardController card, Vector2 anchoredPosition) => false;

    public void OnStockClicked()
    {
        // ИСПРАВЛЕНИЕ 1: Разрешаем вызов метода, если ход совершает подсказка
        if (!IsInputAllowed && !isExecutingHint) return;

        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.ClickStock)) return;
            if (tutorialManager.steps[tutorialManager.currentStepIndex].expectedAction == TutorialActionType.ClickStock)
            {
                tutorialManager.AdvanceStep();
            }
        }

        GameQuestTracker.Instance?.RecordStockDraw();

        RegisterMoveAndStartIfNeeded();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.StartTimerIfNotStarted();

        deckManager.DrawFromStock();
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // Если это мобилка или планшет — перехватываем одиночный тап
        if (GameSettings.AutoMoveClickMode == 0)
        {
            var dragManager = FindObjectOfType<DragManager>();
            dragManager?.ForceSnapBackLastDrop();

            ExecuteAutoMove(card);
        }
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // На ПК авто-перенос срабатывает только по двойному клику
        if (GameSettings.AutoMoveClickMode == 1)
        {
            ExecuteAutoMove(card);
        }
    }
    private void ExecuteAutoMove(CardController card)
    {
        // 1. Проверки туториала (перенесены из старого метода двойного клика)
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.DoubleClick, card)) return;

            if (tutorialManager.steps[tutorialManager.currentStepIndex].expectedAction == TutorialActionType.DoubleClick)
            {
                tutorialManager.AdvanceStep();
            }
        }

        // 2. Передаем управление сервису авто-хода
        // SultanAutoMoveService уже умеет проверять Дома, Резервы и красиво трясти карту, если ходов нет!
        autoMoveService?.OnCardRightClicked(card);
    }

    public void OnCardLongPressed(CardController card) { }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        // ⚡ ИСПРАВЛЕНО: Продвигаем туториал только если шаг ждет MoveCard и это НЕ финальное доигрывание
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, container))
            {
                if (tutorialManager.steps[tutorialManager.currentStepIndex].expectedAction == TutorialActionType.MoveCard)
                {
                    // Игнорируем вызов AdvanceStep на 10-м шаге свободной игры (его зафиксирует LateUpdate при победе)
                    if (tutorialManager.currentStepIndex < tutorialManager.steps.Count - 1)
                    {
                        tutorialManager.AdvanceStep();
                    }
                }
            }
        }

        var sultanCard = card.GetComponent<SultanCardController>();
        ICardContainer source = sultanCard != null ? sultanCard.SourceContainer : null;

        if (source != null && source == container)
        {
            return;
        }

        if (source != null && source != container)
        {
            if (scoreManager != null) scoreManager.OnCardMove(source, container);

            if (undoManager != null)
            {
                List<CardController> movedCards = new List<CardController> { card };
                List<Transform> parents = new List<Transform> { sultanCard.OriginalParent };
                List<Vector3> positions = new List<Vector3> { sultanCard.OriginalLocalPosition };
                List<int> siblings = new List<int> { sultanCard.OriginalSiblingIndex };

                undoManager.RecordMove(movedCards, source, container, parents, positions, siblings);
            }

            // ---> УЧЕТ И КАТЕГОРИЗАЦИЯ ЗАДАНИЙ ДЛЯ ТЕКУЩЕГО ХОДА <---
            TrackSultanQuest(card, source, container);
        }

        RegisterMoveAndStartIfNeeded();
        CheckGameState();
    }
    public void TrackSultanQuest(CardController card, ICardContainer source, ICardContainer container)
    {
        // ИСПРАВЛЕНИЕ 3: Разрешаем учет квестов во время хода подсказки
        if ((!IsInputAllowed && !isExecutingHint) || GameSettings.IsTutorialMode) return;

        bool isToFoundation = false;
        bool completedFoundation = false;
        bool fromWasteToFoundation = false;
        bool fromReserve = false;

        if (container is SultanFoundationPile)
        {
            isToFoundation = true;
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 1);
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());

            if (card.cardModel.rank == 12)
            {
                completedFoundation = true;
                GameQuestTracker.Instance?.SendEvent(QuestActionType.CompleteFoundationPile, 1);
            }

            if (source is SultanWastePile)
            {
                fromWasteToFoundation = true;
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, 1);
            }
        }

        if (source is SultanReserveSlot)
        {
            fromReserve = true;
            GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, 1);
        }

        if (source is SultanFoundationPile)
        {
            GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(card.cardModel.rank);
            if (card.cardModel.rank == 12) GameQuestTracker.Instance?.SendEvent(QuestActionType.CompleteFoundationPile, -1);
            if (container is SultanWastePile) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, -1);
        }

        questUndoStack.Push(new SultanQuestUndoRecord
        {
            IsToFoundation = isToFoundation,
            CompletedFoundation = completedFoundation,
            FromWasteToFoundation = fromWasteToFoundation,
            FromReserve = fromReserve,
            CardRank = card.cardModel.rank
        });
    }

    public void OnUndoAction()
    {
        if (scoreManager != null)
        {
            scoreManager.OnUndo();
        }
        UpdateFullUI();
        if (hasGameStarted && !hasWonGame)
        {
            isTimerRunning = true;
        }

        if (dragLayer != null && dragLayer.childCount > 0) return;
        if (deckManager != null && deckManager.isDealing) return;
        if (undoManager != null && undoManager.IsUndoing) return;

        // ---> ОТКАТ ПРОГРЕССА КВЕСТОВ ПРИ ОТМЕНЕ ХОДА <---
        GameQuestTracker.Instance?.RecordUndoUsed();

        if (questUndoStack.Count > 0)
        {
            var qRecord = questUndoStack.Pop();

            if (qRecord.IsToFoundation)
            {
                GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(qRecord.CardRank);
            }
            if (qRecord.CompletedFoundation)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.CompleteFoundationPile, -1);
            }
            if (qRecord.FromWasteToFoundation)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, -1);
            }
            if (qRecord.FromReserve)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, -1);
            }
        }
        // ------------------------------------------------

        StartCoroutine(CleanupAfterUndoRoutine());
    }

    private IEnumerator CleanupAfterUndoRoutine()
    {
        yield return new WaitWhile(() => undoManager != null && undoManager.IsUndoing);
        Canvas.ForceUpdateCanvases();

        if (dragLayer != null && dragLayer.childCount > 0)
        {
            List<CardController> strandedCards = new List<CardController>();
            for (int i = 0; i < dragLayer.childCount; i++)
            {
                var cc = dragLayer.GetChild(i).GetComponent<CardController>();
                if (cc != null) strandedCards.Add(cc);
            }

            List<ICardContainer> allContainers = pileManager.GetAllContainers();
            foreach (var card in strandedCards)
            {
                ICardContainer bestContainer = null;
                float minDist = float.MaxValue;

                foreach (var container in allContainers)
                {
                    if (container == null || container.Transform == null) continue;
                    float dist = Vector3.Distance(card.transform.position, container.Transform.position);
                    if (dist < minDist) { minDist = dist; bestContainer = container; }
                }

                if (bestContainer != null)
                {
                    card.transform.SetParent(bestContainer.Transform, true);
                    card.transform.SetAsLastSibling();

                    var data = card.GetComponent<CardData>();
                    if (data != null)
                    {
                        if (bestContainer is SultanStockPile) data.SetFaceUp(false, false);
                        else if (bestContainer is SultanWastePile) data.SetFaceUp(true, false);
                        else data.SetFaceUp(true, false);
                    }
                }
            }
        }

        if (pileManager.StockPile != null)
        {
            pileManager.StockPile.UpdateOffsets();
            for (int i = 0; i < pileManager.StockPile.transform.childCount; i++)
            {
                var cg = pileManager.StockPile.transform.GetChild(i).GetComponent<CanvasGroup>();
                if (cg != null) { cg.blocksRaycasts = false; cg.interactable = false; }
            }
        }

        foreach (var reserve in pileManager.Reserves)
        {
            if (reserve.transform.childCount > 0)
            {
                var cg = reserve.transform.GetChild(reserve.transform.childCount - 1).GetComponent<CanvasGroup>();
                if (cg != null) { cg.blocksRaycasts = true; cg.interactable = true; }
            }
        }

        foreach (var container in pileManager.GetAllContainers())
        {
            if (container == null || !containerFields.ContainsKey(container)) continue;
            var field = containerFields[container];

            if (field != null && field.GetValue(container) is System.Collections.IList list)
            {
                Transform parentTransform = ((MonoBehaviour)container).transform;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var c = list[i] as CardController;
                    if (c != null && c.transform.parent != parentTransform) list.RemoveAt(i);
                }
            }
        }

        UpdateFullUI();
        CheckGameState();
        if (!isExecutingHint) StartBackgroundSolver();
    }

    private void SetupUndoSounds()
    {
        if (undoManager != null)
        {
            if (undoManager.undoButton != null)
            {
                undoManager.undoButton.onClick.AddListener(OnUndoClicked);
            }
            if (undoManager.undoAllButton != null)
            {
                LongPressHoldTrigger.SubscribeToButton(undoManager.undoAllButton, OnUndoAllClicked);
            }
        }
    }

    private void OnUndoClicked()
    {
        if (!IsInputAllowed) return;

        // ⚡ ИСПРАВЛЕНО: Продвигаем туториал только если текущий шаг ОЖИДАЕТ отмену хода
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.Undo)) return;
            if (tutorialManager.steps[tutorialManager.currentStepIndex].expectedAction == TutorialActionType.Undo)
            {
                tutorialManager.AdvanceStep();
            }
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Back");

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // ----------------------

        StartCoroutine(CheckAndPlayUndoSound());
    }

    private void OnUndoAllClicked()
    {
        if (!IsInputAllowed) return;

        if (tutorialManager != null && tutorialManager.IsTutorialActive) return;

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // ----------------------

        if (scoreManager != null)
        {
            scoreManager.ResetScore();
        }
        UpdateFullUI();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Back");
            StartCoroutine(DelayedDropSound(0.25f));
        }
    }

    private IEnumerator CheckAndPlayUndoSound()
    {
        yield return null;

        if (dragLayer != null && dragLayer.childCount > 0)
        {
            var cardObj = dragLayer.GetChild(dragLayer.childCount - 1);
            var sultanCard = cardObj.GetComponent<SultanCardController>();

            if (sultanCard != null && pileManager != null)
            {
                if (sultanCard.OriginalParent != null && sultanCard.OriginalParent != pileManager.StockPile.transform)
                {
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySound("Card_Whoosh_Out");
                        StartCoroutine(DelayedDropSound(0.2f));
                    }
                }
            }
        }
    }

    private IEnumerator DelayedDropSound(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");
    }

    public void OnKeyboardPick(CardController card) { }
    public void RestartGame()
    {
        if (hintSolver != null) hintSolver.CancelSearch();
        cachedHintPath = null;
        isRestarting = true;
        StopAllCoroutines();
        StartNewGame();
    }

    public bool IsMatchInProgress() => hasGameStarted;
    public void StartBackgroundSolver()
    {
        if (backgroundSolverCoroutine != null) StopCoroutine(backgroundSolverCoroutine);
        if (hintSolver != null) hintSolver.CancelSearch();
        cachedHintPath = null;
        backgroundSolverCoroutine = StartCoroutine(BackgroundSolverRoutine());
    }

    private IEnumerator BackgroundSolverRoutine()
    {
        while (dragLayer != null && dragLayer.childCount > 0) yield return null;
        yield return new WaitForSeconds(0.1f);

        if (hasWonGame) yield break;

        bool solverFinished = false;
        if (hintSolver == null) hintSolver = gameObject.AddComponent<SultanHintSolver>();

        int recycles = 0;
        var field = deckManager.GetType().GetField("currentRecycles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) recycles = (int)field.GetValue(deckManager);

        hintSolver.FindPath(pileManager, recycles, deckManager.maxRecycles, path => {
            cachedHintPath = path;
            solverFinished = true;
        });

        while (!solverFinished) yield return null;
        backgroundSolverCoroutine = null;
    }

    public void RequestHint(System.Action onWaitStart, System.Action<bool> onHintResult)
    {
        // 1. ЗАЩИТА ОТ СПАМА: Если подсказка уже в процессе выполнения, 
        // мы молча игнорируем новые клики. Мы НЕ возвращаем false, 
        // чтобы не спровоцировать ложное появление панели "Нет ходов".
        if (isExecutingHint) return;

        if (hasWonGame || !IsInputAllowed) { onHintResult?.Invoke(false); return; }
        StartCoroutine(HintRoutine(onWaitStart, onHintResult));
    }

    private IEnumerator HintRoutine(System.Action onWaitStart, System.Action<bool> onResult)
    {
        IsInputAllowed = false;

        if (cachedHintPath == null && backgroundSolverCoroutine == null) StartBackgroundSolver();

        if (backgroundSolverCoroutine != null)
        {
            onWaitStart?.Invoke();
            while (backgroundSolverCoroutine != null) yield return null;
        }

        if (cachedHintPath != null && cachedHintPath.Count > 0)
        {
            var nextMove = cachedHintPath[0];
            cachedHintPath.Clear(); // Очистка кэша от эффекта бабочки

            isExecutingHint = true;
            ExecuteHintMove(nextMove);

            onResult?.Invoke(true);
        }
        else
        {
            IsInputAllowed = true;
            onResult?.Invoke(false);
        }
    }

    private void ExecuteHintMove(SultanHintMove cmd)
    {
        if (cmd.Type == SultanHintMove.MoveType.DrawStock || cmd.Type == SultanHintMove.MoveType.Recycle)
        {
            OnStockClicked();
            StartCoroutine(WaitAndUnlockAfterHint());
            return;
        }

        CardController card = null;
        ICardContainer source = null;
        ICardContainer target = null;

        if (cmd.Type == SultanHintMove.MoveType.WasteToFoundation || cmd.Type == SultanHintMove.MoveType.WasteToReserve)
        {
            source = pileManager.WastePile;
            var cards = source.Transform.GetComponentsInChildren<CardController>();
            if (cards.Length > 0) card = cards[cards.Length - 1];
        }
        else if (cmd.Type == SultanHintMove.MoveType.ReserveToFoundation)
        {
            source = pileManager.Reserves[cmd.FromIndex];
            var cards = source.Transform.GetComponentsInChildren<CardController>();
            if (cards.Length > 0) card = cards[cards.Length - 1];
        }

        if (cmd.Type == SultanHintMove.MoveType.WasteToFoundation || cmd.Type == SultanHintMove.MoveType.ReserveToFoundation)
            target = pileManager.Foundations[cmd.ToIndex];
        else if (cmd.Type == SultanHintMove.MoveType.WasteToReserve)
            target = pileManager.Reserves[cmd.ToIndex];

        if (card != null && source != null && target != null)
        {
            // ЖЕСТКАЯ ЗАЩИТА: Проверяем ход по физическим правилам игры
            if (!target.CanAccept(card))
            {
                Debug.LogWarning("[HintDebug] Защита сработала: Солвер попытался сделать незаконный ход!");
                UnlockAfterHint();
                return;
            }

            var sultanCard = card.GetComponent<SultanCardController>();
            if (sultanCard != null) sultanCard.CaptureStateForUndo();

            StartCoroutine(AnimateHintMove(card, source, target));
        }
        else
        {
            UnlockAfterHint();
        }
    }

    private IEnumerator AnimateHintMove(CardController card, ICardContainer source, ICardContainer target)
    {
        var sultanCard = card.GetComponent<SultanCardController>();
        if (sultanCard != null) sultanCard.SetAnimating(true);

        if (AudioManager.Instance != null)
        {
            AudioSource whoosh = AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            if (whoosh != null) whoosh.pitch = 1.3f;
        }

        card.transform.SetParent(dragLayer, true);
        card.transform.SetAsLastSibling();

        Vector3 startPos = card.transform.position;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            card.transform.position = Vector3.Lerp(startPos, target.Transform.position, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        target.AcceptCard(card);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Card_Drop_Success");
            if (target is SultanFoundationPile) AudioManager.Instance.PlaySound("Card_Foundation_Success");
        }

        if (sultanCard != null) sultanCard.SetAnimating(false);

        // Этот метод сам начислит очки, квесты и Undo
        OnCardDroppedToContainer(card, target);

        StartCoroutine(WaitAndUnlockAfterHint());
    }

    private IEnumerator WaitAndUnlockAfterHint()
    {
        yield return null;
        while (true)
        {
            // 2. ЖЕСТКАЯ БЛОКИРОВКА ВВОДА:
            // Другие скрипты (например, SultanAnimationService) могут попытаться 
            // разблокировать стол (IsInputAllowed = true) в конце своей анимации.
            // Мы принудительно удерживаем блокировку, пока подсказка полностью не завершит свои дела.
            IsInputAllowed = false;

            bool isAnimating = (dragLayer != null && dragLayer.childCount > 0);
            if (!isAnimating) break;
            yield return null;
        }

        // Удерживаем защиту еще 0.1 секунды для обновления системных массивов
        IsInputAllowed = false;
        yield return new WaitForSeconds(0.1f);

        UnlockAfterHint();
    }

    private void UnlockAfterHint()
    {
        isExecutingHint = false;
        IsInputAllowed = true;

        if (cachedHintPath == null || cachedHintPath.Count == 0) StartBackgroundSolver();

        CheckGameState();
    }
}