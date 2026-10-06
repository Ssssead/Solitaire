using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using YG;

public class KlondikeModeManager : MonoBehaviour, IModeManager, ICardGameMode, ICardClickReceiver
{
    [Header("Core References")]
    public CardFactory cardFactory;
    public Canvas rootCanvas;

    [Header("UI Slots")]
    public Transform tableauSlotsParent;
    public Transform foundationSlotsParent;
    public Transform stockSlot;
    public Transform wasteSlot;

    [Header("Services")]
    public PileManager pileManager;
    public DeckManager deckManager;
    public DragManager dragManager;
    public UndoManager undoManager;
    public AnimationService animationService;
    public AutoMoveService autoMoveService;
    public DefeatManager defeatManager;
    public KlondikeScoreManager scoreManager;
    [Header("Tutorial")]
    public KlondikeTutorialManager tutorialManager;

    [Header("UI & HUD - Landscape")]
    [Tooltip("Текст для отображения количества ходов")]
    public TMP_Text movesText;
    [Tooltip("Текст для отображения очков")]
    public TMP_Text scoreText;
    [Tooltip("Текст для отображения времени")]
    public TMP_Text timeText;

    [Header("UI & HUD - Portrait")]
    [Tooltip("Текст для отображения количества ходов (Портрет)")]
    public TMP_Text portraitMovesText;
    [Tooltip("Текст для отображения очков (Портрет)")]
    public TMP_Text portraitScoreText;
    [Tooltip("Текст для отображения времени (Портрет)")]
    public TMP_Text portraitTimeText;

    [Header("UI Buttons")]
    public Button autoWinButton;
    private RectTransform autoWinRect;
    private Vector2 autoWinShowPos;
    private Vector2 autoWinHidePos;
    private bool isAutoWinVisible = false;
    private Coroutine autoWinAnimCoroutine;
    private ICardContainer lastInteractionSource;

    [Header("Intro")]
    public bool playIntroOnStart = true;
    public GameIntroController introController; // Ссылка на новый контроллер
    [Header("Settings")]
    public StockDealMode stockDealMode = StockDealMode.Draw1;
    public float tableauVerticalGap = 40f;
    public RectTransform dragLayer;

    public AnimationService AnimationService => animationService;
    public PileManager PileManager => pileManager;
    public RectTransform DragLayer => dragLayer;
    public AutoMoveService AutoMoveService => autoMoveService;
    public Canvas RootCanvas => rootCanvas != null ? rootCanvas : GetComponentInParent<Canvas>();
    public float TableauVerticalGap => tableauVerticalGap;
    public StockDealMode StockDealMode => stockDealMode;
    public bool IsInputAllowed { get; set; } = true;
    public GameType GameType => GameType.Klondike;
    public ITutorialManager Tutorial => tutorialManager;
    [Header("UI Controller")]
    public GameUIController gameUI;

    private bool hasWonGame = false;
    private bool hasGameStarted = false;

    // [NEW] Локальный таймер
    private float gameTimer = 0f;
    private bool isTimerRunning = false;
    private bool isRestarting = false;
    public string GameName => "Klondike";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool isInitialized = false;

    #region Initialization

    private void Awake()
    {
        LogDebug("=== Awake Start ===");
        FindMissingComponents();

        if (!ValidateCriticalReferences())
        {
            Debug.LogError("[KlondikeModeManager] Critical references missing! Check Inspector.");
            return;
        }

        object[] initArgs = PrepareInitializationArguments();
        InitializeAllServices(initArgs);

        // --- ФИКС 1: Собираем слоты ЗДЕСЬ, до того как они поменяют родителя! ---
        if (pileManager != null)
        {
            pileManager.CreatePiles();
        }

        isInitialized = true;
        LogDebug("=== Awake Complete ===");
    }

    private void FindMissingComponents()
    {
        pileManager = pileManager ?? GetComponent<PileManager>() ?? FindObjectOfType<PileManager>();
        deckManager = deckManager ?? GetComponent<DeckManager>() ?? FindObjectOfType<DeckManager>();
        dragManager = dragManager ?? GetComponent<DragManager>() ?? FindObjectOfType<DragManager>();
        undoManager = undoManager ?? GetComponent<UndoManager>() ?? FindObjectOfType<UndoManager>();
        animationService = animationService ?? GetComponent<AnimationService>() ?? FindObjectOfType<AnimationService>();
        autoMoveService = autoMoveService ?? GetComponent<AutoMoveService>() ?? FindObjectOfType<AutoMoveService>();
        defeatManager = defeatManager ?? GetComponent<DefeatManager>() ?? FindObjectOfType<DefeatManager>();
        scoreManager = scoreManager ?? GetComponent<KlondikeScoreManager>() ?? FindObjectOfType<KlondikeScoreManager>();

        if (rootCanvas == null) rootCanvas = cardFactory?.rootCanvas ?? FindObjectOfType<Canvas>();
        if (dragLayer == null && rootCanvas != null) dragLayer = rootCanvas.transform as RectTransform;
    }

    private bool ValidateCriticalReferences()
    {
        bool valid = true;
        if (cardFactory == null) { Debug.LogError("[KlondikeModeManager] CardFactory is null!"); valid = false; }
        if (pileManager == null) { Debug.LogError("[KlondikeModeManager] PileManager is null!"); valid = false; }
        if (deckManager == null) { Debug.LogError("[KlondikeModeManager] DeckManager is null!"); valid = false; }
        if (dragManager == null) { Debug.LogError("[KlondikeModeManager] DragManager is null!"); valid = false; }
        if (tableauSlotsParent == null) { Debug.LogError("[KlondikeModeManager] tableauSlotsParent is null!"); valid = false; }
        if (foundationSlotsParent == null) { Debug.LogError("[KlondikeModeManager] foundationSlotsParent is null!"); valid = false; }
        return valid;
    }

    private object[] PrepareInitializationArguments()
    {
        RectTransform tableauSlotsRect = tableauSlotsParent as RectTransform;
        RectTransform foundationSlotsRect = foundationSlotsParent as RectTransform;
        RectTransform stockSlotRect = stockSlot as RectTransform;
        RectTransform wasteSlotRect = wasteSlot as RectTransform;

        var args = new List<object>
        {
            this, cardFactory, pileManager, deckManager, dragManager, undoManager,
            animationService, autoMoveService, rootCanvas, (object)tableauVerticalGap
        };

        if (tableauSlotsParent != null) args.Add(tableauSlotsParent);
        if (foundationSlotsParent != null) args.Add(foundationSlotsParent);
        if (stockSlot != null) args.Add(stockSlot);
        if (wasteSlot != null) args.Add(wasteSlot);

        if (tableauSlotsRect != null && tableauSlotsRect != tableauSlotsParent as object) args.Add(tableauSlotsRect);
        if (foundationSlotsRect != null && foundationSlotsRect != foundationSlotsParent as object) args.Add(foundationSlotsRect);
        if (stockSlotRect != null && stockSlotRect != stockSlot as object) args.Add(stockSlotRect);
        if (wasteSlotRect != null && wasteSlotRect != wasteSlot as object) args.Add(wasteSlotRect);
        if (dragLayer != null) args.Add(dragLayer);

        return args.ToArray();
    }

    private void InitializeAllServices(object[] availableArgs)
    {
        if (pileManager != null)
        {
            bool hasAllSlots = (pileManager.tableauSlotsParent != null && pileManager.foundationSlotsParent != null && pileManager.stockSlotTransform != null && pileManager.wasteSlotTransform != null);
            if (hasAllSlots) pileManager.Initialize(this, null, null, null, null, tableauVerticalGap);
            else SafeInvokeInitialize(pileManager, availableArgs, "PileManager");
        }

        SafeInvokeInitialize(deckManager, availableArgs, "DeckManager");
        SafeInvokeInitialize(undoManager, availableArgs, "UndoManager");
        SafeInvokeInitialize(animationService, availableArgs, "AnimationService");

        if (dragManager != null)
        {
            dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);
            LogDebug("DragManager initialized explicitly");
        }

        if (autoMoveService != null)
        {
            autoMoveService.Initialize(this, pileManager, undoManager, animationService, rootCanvas, dragLayer, tableauVerticalGap);
            LogDebug("AutoMoveService initialized explicitly");
        }

        if (defeatManager != null) defeatManager.Initialize(pileManager, gameUI);
    }

    private void Start()
    {
        if (!isInitialized) return;

        var deck = GetComponent<DeckManager>() ?? FindObjectOfType<DeckManager>();
        if (deck != null) deck.difficulty = GameSettings.CurrentDifficulty;

        this.stockDealMode = (GameSettings.KlondikeDrawCount == 3) ? StockDealMode.Draw3 : StockDealMode.Draw1;

        if (autoWinButton != null)
        {
            autoWinRect = autoWinButton.GetComponent<RectTransform>();
            autoWinShowPos = autoWinRect.anchoredPosition;
            float hideOffsetY = (autoWinShowPos.y < 0) ? -250f : 250f;
            autoWinHidePos = autoWinShowPos + new Vector2(0, hideOffsetY);
            autoWinRect.anchoredPosition = autoWinHidePos;
            autoWinButton.gameObject.SetActive(false);
            autoWinButton.onClick.RemoveAllListeners();
            autoWinButton.onClick.AddListener(OnAutoWinClicked);
        }

        // Вся логика старта теперь централизована здесь
        StartNewGame();
    }
    /// <summary>
    /// Инициализирует логику, очищает стол, но НЕ запускает DealInitial.
    /// </summary>
    private void InitializeGameLogicOnly()
    {
        IsInputAllowed = false; // Блокируем ввод пока идет интро
        hasWonGame = false;
        hasGameStarted = false;
        UpdateFullUI();

        if (defeatManager != null) defeatManager.ResetManager();
        if (scoreManager != null) scoreManager.ResetScore();

        pileManager.CreatePiles();
        if (dragManager != null) dragManager.RefreshContainers();

        // Очищаем деку от старых карт, если были
        cardFactory.DestroyAllCards();
    }

    // [NEW] Обновление таймера
    private void Update()
    {
        if (isTimerRunning && !hasWonGame)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }
    }

    public void StartNewGame()
    {
        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameAbandoned();

        // ДОБАВИТЬ ЭТУ СТРОКУ:
        SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());

        string variant = (GameSettings.KlondikeDrawCount == 3) ? "Draw3" : "Draw1";
        GameQuestTracker.Instance?.StartMatch("Klondike", GameSettings.CurrentDifficulty, variant);

        IsInputAllowed = false;
        hasWonGame = false;
        hasGameStarted = false;
        gameTimer = 0f;
        isTimerRunning = false;
        isAutoWinVisible = false;
        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);

        if (defeatManager != null) defeatManager.ResetManager();
        if (scoreManager != null) scoreManager.ResetScore();
        if (undoManager != null && undoManager.GetType().GetMethod("ResetHistory") != null)
            undoManager.GetType().GetMethod("ResetHistory").Invoke(undoManager, null);
        if (gameUI != null) gameUI.ResetHints();
        UpdateFullUI();
        pileManager.ClearAllPiles();

        // --- ФИКС 2: Убран вызов pileManager.CreatePiles(), так как слоты уже собраны ---

        if (dragManager != null) dragManager.RefreshContainers();
        cardFactory.DestroyAllCards();

        if (introController != null) introController.PrepareIntro(isRestarting);

        Deal cachedDeal = null;
        if (DealCacheSystem.Instance != null)
        {
            int drawParam = (stockDealMode == StockDealMode.Draw3) ? 3 : 1;
            cachedDeal = DealCacheSystem.Instance.GetDeal(GameType.Klondike, GameSettings.CurrentDifficulty, drawParam);
        }

        StartCoroutine(IntroSequenceRoutine(cachedDeal));
    }
    private IEnumerator IntroSequenceRoutine(Deal deal)
    {
        yield return null;

        // --- ВЕТКА ОБУЧЕНИЯ ---
        if (GameSettings.IsTutorialMode && tutorialManager != null)
        {
            yield return StartCoroutine(tutorialManager.PlayTutorialIntro(deal));
            isRestarting = false;
            IsInputAllowed = true;
            UpdateFullUI();
        }
        // --- ОБЫЧНАЯ ИГРА ---
        else
        {
            bool cardsDealtByIntro = false;
            if (playIntroOnStart && introController != null)
            {
                yield return StartCoroutine(introController.PlayIntroSequence(isRestarting));
                cardsDealtByIntro = true;
            }
            else if (deckManager != null && isRestarting)
            {
                yield return StartCoroutine(deckManager.PlayIntroDeckArrival(1.2f));
                cardsDealtByIntro = true;
            }

            if (!cardsDealtByIntro)
            {
                if (deal != null && deckManager != null) deckManager.LoadDeal(deal);
                else if (deckManager != null) deckManager.DealInitial();
            }

            animationService?.ReorderAllContainers(pileManager.GetAllContainerTransforms());
            isRestarting = false;
            IsInputAllowed = true;
            UpdateFullUI();
            StartBackgroundSolver();
        }
    }

    private void OnDestroy()
    {
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();

            // ДОБАВИТЬ ЭТУ СТРОКУ:
            SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }
    }

    public void RegisterMoveAndStartIfNeeded()
    {
        if (!IsInputAllowed) return;
        GameQuestTracker.Instance?.RecordMove();
        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true;

            if (StatisticsManager.Instance != null)
            {
                Difficulty diff = GameSettings.CurrentDifficulty;
                string variant = GameSettings.GetCurrentVariantString(GameType.Klondike);
                StatisticsManager.Instance.OnGameStarted(GameName, diff, variant);
            }
            SimpleMetricsTracker.Instance?.TrackLevelStart(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        UpdateFullUI();
        CheckGameState();

        // Запускаем фоновый поиск пути, если это был ручной ход игрока
        if (!isExecutingHint && hasGameStarted && !hasWonGame)
        {
            StartBackgroundSolver();
        }
    }

    // [NEW] Единый метод обновления интерфейса
    private void UpdateFullUI()
    {
        // 1. Ходы
        string movesStr = "0";
        if (hasGameStarted && StatisticsManager.Instance != null)
        {
            movesStr = $"{StatisticsManager.Instance.GetCurrentMoves()}";
        }

        if (movesText != null) movesText.text = movesStr;
        if (portraitMovesText != null) portraitMovesText.text = movesStr;

        // 2. Очки
        string scoreStr = "0";
        if (hasGameStarted)
        {
            int score = scoreManager != null ? scoreManager.CurrentScore : 0;
            scoreStr = $"{score}";
        }

        if (scoreText != null) scoreText.text = scoreStr;
        if (portraitScoreText != null) portraitScoreText.text = scoreStr;

        // 3. Время
        if (!hasGameStarted)
        {
            UpdateTimeUI(); // Сбросит в 0:00
        }
    }

    // [NEW] Обновление только текста времени
    private void UpdateTimeUI()
    {
        int totalSeconds = Mathf.FloorToInt(gameTimer);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        string timeStr = string.Format("{0}:{1:00}", minutes, seconds);

        if (timeText != null) timeText.text = timeStr;
        if (portraitTimeText != null) portraitTimeText.text = timeStr;
    }

    #endregion

    #region IModeManager Implementation

    public ICardContainer FindNearestContainer(CardController card, Vector2 anchoredPosition, float maxDistance)
    {
        return dragManager?.FindNearestContainer(card, anchoredPosition, maxDistance);
    }

    public void OnStockClicked()
    {
        if (!IsInputAllowed) return;

        // --- [NEW] Проверка туториала ---
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.ClickStock)) return;
        }
        // --------------------------------

        // Регистрируем ход
        RegisterMoveAndStartIfNeeded();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.StartTimerIfNotStarted();

        bool isStockEmpty = pileManager.StockPile.IsEmpty();
        bool isWasteHasCards = !pileManager.WastePile.IsEmpty();

        bool isRecycle = (isStockEmpty && isWasteHasCards);

        // Выполняем действие
        var deckManager = GetComponent<DeckManager>();
        if (deckManager != null) deckManager.DrawFromStock();

        // Уведомляем ScoreManager о ходе для истории Undo
        if (scoreManager != null)
        {
            if (isRecycle) scoreManager.OnCardMove(pileManager.WastePile, pileManager.StockPile);
            else scoreManager.OnCardMove(pileManager.StockPile, pileManager.WastePile);
        }

        // --- [NEW] Шаг туториала вперед ---
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            tutorialManager.AdvanceStep();
        }
        // ----------------------------------

        UpdateFullUI();
    }
  public void OnUndoAllAction()
    {
        RegisterMoveAndStartIfNeeded();

        isAutoWinVisible = false;
        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);

        if (deckManager != null) deckManager.ResetStalemate();
        if (scoreManager != null) scoreManager.ResetScore();

        // <--- ЗАМЕНИЛИ ЗВУК: ИСПОЛЬЗУЕМ СТАНДАРТНУЮ ОТМЕНУ --->
        if (AudioManager.Instance != null)
        {
            // Звук нажатия кнопки
            AudioManager.Instance.PlaySound("UI_Back");
            
            // Поскольку полета нет, играем "шлепок" карт мгновенно
            AudioManager.Instance.PlaySound("Card_Drop_Success"); 
            
            // Если хотите добавить немного шелеста, раскомментируйте строчку ниже:
            // AudioManager.Instance.PlaySound("Card_Deal"); 
        }

        UpdateFullUI();
    }

    public bool OnDropToBoard(CardController card, Vector2 anchoredPosition)
    {
        // --- [FIX] Строгая проверка туториала ---
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            // Передаем 300f (DragManager сам посчитает пересечение Rect)
            ICardContainer target = dragManager?.FindNearestContainer(card, anchoredPosition, 300f);

            // Запрещаем бросок, если туториал не разрешает
            if (!tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, target))
            {
                return false;
            }
        }

        // Физически выполняем бросок на стол
        bool success = dragManager?.OnDropToBoard(card, anchoredPosition) ?? false;

        // --- [FIX] Если бросок успешен и мы в обучении -> двигаем шаг вперед! ---
        if (success && tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            tutorialManager.AdvanceStep();
        }

        return success;
    }
    public bool IsMatchInProgress()
    {
        return hasGameStarted;
    }
    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // Запоминаем, откуда уходит карта
        lastInteractionSource = card.GetComponentInParent<ICardContainer>();

        // Проверяем тип платформы через плагин YG2
        // Если это мобильное устройство или планшет — запускаем авто-перенос
        if (YG2.envir.isMobile || YG2.envir.isTablet)
        {
            // Отменяем анимацию микро-свайпа, которую добавили в прошлом шаге
            dragManager?.ForceSnapBackLastDrop();

            // Запускаем перелет карты
            ExecuteAutoMove(card);
        }

        // Для ПК (Desktop) мы ничего не делаем на одинарный клик, 
        // так как авто-перенос срабатывает только на OnCardDoubleClicked.
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // На ПК (Desktop) авто-перемещение срабатывает только по двойному клику
        if (YG2.envir.isDesktop)
        {
            ExecuteAutoMove(card);
        }
    }
    private void ExecuteAutoMove(CardController card)
    {
        // --- Проверка туториала ---
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.DoubleClick, card)) return;
        }
        // --------------------------------

        ICardContainer oldContainer = card.GetComponentInParent<ICardContainer>();
        lastInteractionSource = oldContainer;

        autoMoveService?.OnCardRightClicked(card);

        StartCoroutine(CheckAutoMoveResult(card, oldContainer));
    }

    private System.Collections.IEnumerator CheckAutoMoveResult(CardController card, ICardContainer oldContainer)
    {
        // Ждем завершения хода
        yield return new WaitForSeconds(0.25f);

        ICardContainer newContainer = card.CurrentContainer;

        // Если карта переместилась
        if (newContainer != null && newContainer != oldContainer)
        {
            if (scoreManager != null) scoreManager.OnCardMove(oldContainer, newContainer);
            if (deckManager != null) deckManager.OnProductiveMoveMade();

            // --- [NEW] Шаг туториала вперед ---
            if (tutorialManager != null && tutorialManager.IsTutorialActive)
            {
                tutorialManager.AdvanceStep();
            }
            // ----------------------------------

            CheckGameState();
            UpdateFullUI();
        }
    }

    public void OnCardLongPressed(CardController card)
    {
        // Ищем вверх по иерархии
        lastInteractionSource = card.GetComponentInParent<ICardContainer>();
        dragManager?.OnCardLongPressed(card);
    }
    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        ICardContainer source = lastInteractionSource;

        if (source == null && dragManager != null)
        {
            source = dragManager.GetSourceContainer();
        }

        dragManager?.OnCardDroppedToContainer(card, container);

        if (container is TableauPile || container is FoundationPile)
        {
            if (deckManager != null) deckManager.OnProductiveMoveMade();
            RegisterMoveAndStartIfNeeded();

            if (scoreManager != null)
            {
                scoreManager.OnCardMove(source, container);
            }
            if (container is FoundationPile && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound("Card_Foundation_Success");
            }
            // --- [NEW] Шаг туториала вперед ---
            if (tutorialManager != null && tutorialManager.IsTutorialActive)
            {
                // Проверяем, что это был именно тот ход, который мы ждали
                if (tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, container))
                {
                    tutorialManager.AdvanceStep();
                }
            }

            UpdateFullUI();
        }
    }

    public void OnKeyboardPick(CardController card)
    {
        dragManager?.OnKeyboardPick(card);
    }

    #endregion

    #region Card Event Registration

    public void RegisterCardEvents(CardController card)
    {
        if (card == null) return;
        dragManager?.RegisterCardEvents(card);
        autoMoveService?.RegisterCardForAutoMove(card);
    }

    #endregion

    #region Utility Methods

    private void SafeInvokeInitialize(object component, object[] availableArgs, string componentName)
    {
        if (component == null) return;

        Type componentType = component.GetType();
        var initMethods = componentType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                       .Where(m => string.Equals(m.Name, "Initialize", StringComparison.OrdinalIgnoreCase))
                                       .OrderBy(m => m.GetParameters().Length)
                                       .ToArray();

        if (initMethods.Length == 0) return;

        foreach (var method in initMethods)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];
            bool canInvoke = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                Type paramType = parameters[i].ParameterType;
                bool found = false;

                foreach (var arg in availableArgs)
                {
                    if (arg == null) continue;
                    if (paramType.IsAssignableFrom(arg.GetType())) { args[i] = arg; found = true; break; }

                    if (paramType.IsPrimitive || paramType == typeof(float) || paramType == typeof(double))
                    {
                        try { if (arg is IConvertible) { args[i] = Convert.ChangeType(arg, paramType); found = true; break; } } catch { }
                    }
                }

                if (!found) { canInvoke = false; break; }
            }

            if (!canInvoke) continue;

            try { method.Invoke(component, args); return; }
            catch (Exception ex) { Debug.LogWarning($"[KMM] Failed to invoke Initialize: {ex.Message}"); }
        }
    }

    private void LogDebug(string message)
    {
        if (showDebugLogs) Debug.Log($"[KlondikeModeManager] {message}");
    }

    #endregion
    #region Hint System

    private List<KlondikeHintSolver.HintMoveCommand> cachedHintPath = null;
    private Coroutine backgroundSolverCoroutine = null;
    private bool isExecutingHint = false;

    // Вызывается автоматически после любого ручного хода игрока (сброс кэша)
    public void StartBackgroundSolver()
    {
        if (backgroundSolverCoroutine != null) StopCoroutine(backgroundSolverCoroutine);
        cachedHintPath = null;
        backgroundSolverCoroutine = StartCoroutine(BackgroundSolverRoutine());
    }

    private IEnumerator BackgroundSolverRoutine()
    {
        // 1. ОЖИДАНИЕ АНИМАЦИЙ: Ждем, пока летящие карты в слое DragLayer приземлятся
        if (dragLayer != null)
        {
            while (dragLayer.childCount > 0)
            {
                yield return null;
            }
        }

        // Даем компонентам Unity еще 0.1 сек на то, чтобы записать карты в логические списки
        yield return new WaitForSeconds(0.1f);

        if (hasWonGame)
        {
            backgroundSolverCoroutine = null;
            yield break;
        }

        Deal currentDeal = GetCurrentDealState();

        // Считаем все карты на доске. Если их не 52 (сбой анимации), откладываем поиск
        int totalCards = currentDeal.stock.Count + currentDeal.waste.Count;
        for (int i = 0; i < 7; i++) totalCards += currentDeal.tableau[i].Count;
        for (int i = 0; i < 4; i++) totalCards += currentDeal.foundations[i].Count;

        if (totalCards != 52)
        {
            backgroundSolverCoroutine = null;
            yield break;
        }

        int drawParam = (stockDealMode == StockDealMode.Draw3) ? 3 : 1;

        yield return StartCoroutine(KlondikeHintSolver.GetHintPathAsync(currentDeal, drawParam, 12f, (path) => {
            cachedHintPath = path;
        }));

        backgroundSolverCoroutine = null;
    }

    public void RequestHint(System.Action onWaitStart, System.Action<bool> onHintResult)
    {
        if (!IsInputAllowed) return;
        StartCoroutine(HintRoutine(onWaitStart, onHintResult));
    }

    private IEnumerator HintRoutine(System.Action onWaitStart, System.Action<bool> onResult)
    {
        IsInputAllowed = false;

        if (cachedHintPath == null && backgroundSolverCoroutine == null)
        {
            backgroundSolverCoroutine = StartCoroutine(BackgroundSolverRoutine());
        }

        if (backgroundSolverCoroutine != null)
        {
            onWaitStart?.Invoke();
            while (backgroundSolverCoroutine != null) yield return null;
        }

        IsInputAllowed = true;

        if (cachedHintPath != null && cachedHintPath.Count > 0)
        {
            var nextMove = cachedHintPath[0];
            cachedHintPath.RemoveAt(0);

            isExecutingHint = true;
            ExecuteSolverMove(nextMove);
            isExecutingHint = false;

            onResult?.Invoke(true);
        }
        else
        {
            onResult?.Invoke(false);
        }
    }

    private Deal GetCurrentDealState()
    {
        Deal d = new Deal();

        // Читаем напрямую из логических списков (cards), чтобы анимации полета нам не мешали
        for (int i = 0; i < 7; i++)
        {
            foreach (var cardCtrl in pileManager.Tableau[i].cards)
            {
                var cardData = cardCtrl.GetComponent<CardData>();
                d.tableau[i].Add(new CardInstance(cardData.model, cardData.IsFaceUp()));
            }
        }
        for (int i = 0; i < 4; i++)
        {
            foreach (var cardCtrl in pileManager.Foundations[i].cards)
            {
                var cardData = cardCtrl.GetComponent<CardData>();
                d.foundations[i].Add(cardData.model);
            }
        }
        if (pileManager.StockPile != null)
        {
            foreach (var cardCtrl in pileManager.StockPile.cards)
            {
                d.stock.Push(new CardInstance(cardCtrl.cardModel, false));
            }
        }
        if (pileManager.WastePile != null)
        {
            foreach (var cardCtrl in pileManager.WastePile.cards)
            {
                d.waste.Add(new CardInstance(cardCtrl.cardModel, true));
            }
        }
        return d;
    }

    private void ExecuteSolverMove(KlondikeHintSolver.HintMoveCommand move)
    {
        if (move.Type == KlondikeHintSolver.HintMoveType.StockDraw || move.Type == KlondikeHintSolver.HintMoveType.RecycleWaste)
        {
            bool isStockEmpty = pileManager.StockPile.IsEmpty();
            bool isWasteHasCards = !pileManager.WastePile.IsEmpty();
            bool isRecycle = (isStockEmpty && isWasteHasCards);

            deckManager.DrawFromStock();

            if (scoreManager != null)
            {
                if (isRecycle) scoreManager.OnCardMove(pileManager.WastePile, pileManager.StockPile);
                else scoreManager.OnCardMove(pileManager.StockPile, pileManager.WastePile);
            }

            // === ФИКС ДЛЯ ПАНЕЛИ ПОРАЖЕНИЯ ===
            if (deckManager != null) deckManager.OnProductiveMoveMade();

            RegisterMoveAndStartIfNeeded();
            CheckGameState();
            return;
        }

        CardController targetCard = null;
        ICardContainer targetContainer = null;
        ICardContainer sourceContainer = null;

        if (move.Type == KlondikeHintSolver.HintMoveType.Foundation)
        {
            if (move.FromIdx == -1)
            {
                targetCard = GetTopCard(pileManager.WastePile);
                sourceContainer = pileManager.WastePile;
            }
            else
            {
                targetCard = GetCardFromTableau(move.FromIdx, 1);
                sourceContainer = pileManager.Tableau[move.FromIdx];
            }
            if (targetCard != null)
            {
                targetContainer = pileManager.Foundations[move.ToIdx];
            }
        }
        else if (move.Type == KlondikeHintSolver.HintMoveType.MoveTableau || move.Type == KlondikeHintSolver.HintMoveType.RevealTableau)
        {
            targetCard = GetCardFromTableau(move.FromIdx, move.Count);
            sourceContainer = pileManager.Tableau[move.FromIdx];
            targetContainer = pileManager.Tableau[move.ToIdx];
        }
        else if (move.Type == KlondikeHintSolver.HintMoveType.WasteToTableau)
        {
            targetCard = GetTopCard(pileManager.WastePile);
            sourceContainer = pileManager.WastePile;
            targetContainer = pileManager.Tableau[move.ToIdx];
        }
        else if (move.Type == KlondikeHintSolver.HintMoveType.FoundationToTableau)
        {
            targetCard = GetTopCard(pileManager.Foundations[move.FromIdx]);
            sourceContainer = pileManager.Foundations[move.FromIdx];
            targetContainer = pileManager.Tableau[move.ToIdx];
        }

        if (targetCard != null && targetContainer != null && sourceContainer != null)
        {
            bool success = autoMoveService.ExecuteHintMove(targetCard, targetContainer);
            if (success)
            {
                if (scoreManager != null)
                {
                    scoreManager.OnCardMove(sourceContainer, targetContainer);
                }

                // === ФИКС ДЛЯ ПАНЕЛИ ПОРАЖЕНИЯ ===
                if (deckManager != null) deckManager.OnProductiveMoveMade();

                RegisterMoveAndStartIfNeeded();
                CheckGameState();
            }
        }
    }

    private CardController GetTopCard(ICardContainer container)
    {
        if (container is TableauPile t) return t.cards.Count > 0 ? t.cards[t.cards.Count - 1] : null;
        if (container is WastePile w) return w.cards.Count > 0 ? w.cards[w.cards.Count - 1] : null;
        if (container is FoundationPile f) return f.cards.Count > 0 ? f.cards[f.cards.Count - 1] : null;
        if (container is StockPile s) return s.cards.Count > 0 ? s.cards[s.cards.Count - 1] : null;
        return null;
    }

    private CardController GetCardFromTableau(int col, int count)
    {
        var tableau = pileManager.Tableau[col];
        if (tableau.cards.Count >= count)
        {
            return tableau.cards[tableau.cards.Count - count];
        }
        return null;
    }

    #endregion


    #region Public API

    public void RestartGame()
    {
        isRestarting = true; // Указываем, что это рестарт
        StopAllCoroutines(); // Останавливаем старые анимации и генерации

        this.stockDealMode = (GameSettings.KlondikeDrawCount == 3) ? StockDealMode.Draw3 : StockDealMode.Draw1;
        if (deckManager != null) deckManager.difficulty = GameSettings.CurrentDifficulty;

        StartNewGame();
    }
    private IEnumerator SoftRestartRoutine(DeckManager deck)
    {
        IsInputAllowed = false;
        hasWonGame = false;
        hasGameStarted = false;
        gameTimer = 0f;
        isTimerRunning = false;
        isAutoWinVisible = false;

        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);
        if (defeatManager != null) defeatManager.ResetManager();
        if (scoreManager != null) scoreManager.ResetScore();

        UpdateFullUI();
        pileManager.ClearAllPiles();

        // --- ФИКС 3: Убран вызов pileManager.CreatePiles() ---

        if (dragManager != null) dragManager.RefreshContainers();
        cardFactory.DestroyAllCards();

        yield return null;

        if (deck != null)
        {
            yield return StartCoroutine(deck.PlayIntroDeckArrival(1.2f));
        }
        else
        {
            StartNewGame();
        }

        IsInputAllowed = true;
        animationService?.ReorderAllContainers(pileManager.GetAllContainerTransforms());
        UpdateFullUI();
    }

    public void ExecuteSoftRestart()
    {
        // --- ШАГ 0: ПРИМЕНЯЕМ НОВЫЕ НАСТРОЙКИ ---
        this.stockDealMode = (GameSettings.KlondikeDrawCount == 3) ? StockDealMode.Draw3 : StockDealMode.Draw1;

        if (deckManager != null)
        {
            deckManager.difficulty = GameSettings.CurrentDifficulty;
        }

        Debug.Log($"[KMM] Soft Restarting with: {GameSettings.CurrentDifficulty}, {this.stockDealMode}");
        // -----------------------------------------

        // 1. СНАЧАЛА честно закрываем старую игру
        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameAbandoned();

        // ДОБАВИТЬ ЭТУ СТРОКУ:
        SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());

        // 2. ЗАТЕМ сообщаем трекеру настройки нового матча
        string variant = (GameSettings.KlondikeDrawCount == 3) ? "Draw3" : "Draw1";
        GameQuestTracker.Instance?.StartMatch("Klondike", GameSettings.CurrentDifficulty, variant);

        IsInputAllowed = false;
        hasWonGame = false;
        hasGameStarted = false;
        gameTimer = 0f;
        isTimerRunning = false;

        isAutoWinVisible = false;
        if (autoWinButton) autoWinButton.gameObject.SetActive(false);

        // 2. Сброс менеджеров
        if (defeatManager != null) defeatManager.ResetManager();
        if (scoreManager != null) scoreManager.ResetScore();
        if (undoManager != null) undoManager.ResetHistory();
        if (gameUI != null) gameUI.ResetHints();
        UpdateFullUI();

        // 3. Очистка стола
        pileManager.ClearAllPiles();
        cardFactory.DestroyAllCards();

        // 4. Запуск цепочки
        StartCoroutine(RestartSequenceRoutine());
    }

    private IEnumerator RestartSequenceRoutine()
    {
        yield return null; // Ждем кадр очистки

        if (deckManager != null)
        {
            // DeckManager внутри себя использует (mode.stockDealMode) и (this.difficulty),
            // которые мы только что обновили в ШАГЕ 0.
            yield return StartCoroutine(deckManager.PlayIntroDeckArrival(1.5f));
        }

        IsInputAllowed = true;
        animationService.ReorderAllContainers(pileManager.GetAllContainerTransforms());

        // Еще раз обновляем UI, чтобы убедиться, что всё (очки, ходы) по нулям
        UpdateFullUI();
    }
    [ContextMenu("Debug: Force Auto Win Button")]
    public void DebugForceShowAutoWin()
    {
        isAutoWinVisible = true;
        if (autoWinAnimCoroutine != null) StopCoroutine(autoWinAnimCoroutine);
        autoWinAnimCoroutine = StartCoroutine(AnimateAutoWinButton(true));
        Debug.Log("[Debug] Forcing Auto Win Button Show");
    }
    public void CheckGameState()
    {
        if (hasWonGame) return;

        if (IsGameWon())
        {
            hasWonGame = true;
            IsInputAllowed = false; // Отключаем клики игрока
            isTimerRunning = false;

            // --- НОВОЕ: Жестко блокируем Undo ---
            if (undoManager != null) undoManager.ClearAndLock();
            // ------------------------------------

            // Обновляем статистику
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

        // Проверяем возможность авто-победы
        bool canAutoWin = CanAutoWin();

        if (canAutoWin != isAutoWinVisible)
        {
            isAutoWinVisible = canAutoWin;
            if (autoWinAnimCoroutine != null) StopCoroutine(autoWinAnimCoroutine);
            autoWinAnimCoroutine = StartCoroutine(AnimateAutoWinButton(canAutoWin));
        }

        if (defeatManager != null) defeatManager.CheckGameStatus();
    }

    public void OnUndoAction()
    {
        Debug.Log("Undo Action Triggered!");
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.Undo)) return;
        }

        RegisterMoveAndStartIfNeeded();

        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);
        if (defeatManager != null) defeatManager.OnUndo();
        if (deckManager != null) deckManager.ResetStalemate();
        if (scoreManager != null) scoreManager.OnUndo();

        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            tutorialManager.AdvanceStep();
        }

        // <--- ДОБАВЛЯЕМ ЗВУКИ ОТМЕНЫ В ОБХОД UNDO MANAGER --->
        if (AudioManager.Instance != null)
        {
            // Звук нажатия на кнопку (глухой стук)
            AudioManager.Instance.PlaySound("UI_Back");
            // Звук полета карты назад
           // AudioManager.Instance.PlaySound("Card_Deal");

            // Запускаем таймер на 0.25 сек (стандартное время анимации Undo), 
            // чтобы издать звук приземления ровно в момент прилета карты
            StartCoroutine(DelayedUndoDropSound(0.25f));
        }

        UpdateFullUI();
    }
    private IEnumerator DelayedUndoDropSound(float delay)
    {
        // Ждем, пока карта физически долетит до места
        yield return new WaitForSeconds(delay);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");
    }

    private bool CanAutoWin()
    {
        // 1. Если идет раздача, рецикл или любая блокировка ввода - кнопку НЕ показываем
        if (!IsInputAllowed) return false;

        // Обратите внимание: используем IsDealing (свойство с большой буквы), 
        // которое мы добавили в DeckManager в прошлом шаге.
        if (deckManager != null && (deckManager.IsRecycling || deckManager.isDealing)) return false;

        // 2. Проверяем Tableau: если есть хоть одна закрытая карта - нельзя
        if (pileManager.Tableau != null)
        {
            foreach (var pile in pileManager.Tableau)
            {
                if (pile.HasHiddenCards()) return false;
            }
        }

        // 3. ИСПРАВЛЕНИЕ: Проверяем, что Колода (Stock) и Сброс (Waste) пусты.
        // Это обязательно, так как скрипт авто-победы не умеет доставать карты оттуда.
        if (pileManager.StockPile != null && !pileManager.StockPile.IsEmpty()) return false;
        if (pileManager.WastePile != null && pileManager.WastePile.Count > 0) return false;

        return true;
    }
    private IEnumerator AnimateAutoWinButton(bool show)
    {
        float duration = 0.4f;
        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        Vector2 start = autoWinRect.anchoredPosition;
        Vector2 end = show ? autoWinShowPos : autoWinHidePos;

        if (show) autoWinButton.gameObject.SetActive(true);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(elapsed / duration);
            autoWinRect.anchoredPosition = Vector2.Lerp(start, end, t);
            yield return null;
        }

        autoWinRect.anchoredPosition = end;
        if (!show) autoWinButton.gameObject.SetActive(false);
    }
    private void OnAutoWinClicked()
    {
        if (AudioManager.Instance != null)
        {
            // Обычный клик, чтобы игрок понял, что кнопка сработала
            AudioManager.Instance.PlaySound("UI_Click");
        }
        RegisterMoveAndStartIfNeeded();

        // --- НОВОЕ: Сразу отключаем ввод и жестко блокируем Undo ---
        IsInputAllowed = false;
        if (undoManager != null) undoManager.ClearAndLock();
        // ---------------------------------------------------------

        // Прячем кнопку (анимацией)
        isAutoWinVisible = false;
        if (autoWinAnimCoroutine != null) StopCoroutine(autoWinAnimCoroutine);
        autoWinAnimCoroutine = StartCoroutine(AnimateAutoWinButton(false));

        // Запускаем анимацию полета карт
        if (autoMoveService != null) StartCoroutine(autoMoveService.PlayAutoWinAnimation());

        // Запускаем отслеживание очков во время полета карт
        StartCoroutine(AutoWinScoreListener());
    }

    private IEnumerator AutoWinScoreListener()
    {
        // Подсчитываем, сколько карт уже в домах
        int previousFoundationCount = 0;
        if (pileManager.Foundations != null)
        {
            foreach (var f in pileManager.Foundations) previousFoundationCount += f.Count;
        }

        // Следим, пока игра не выиграна
        while (!hasWonGame)
        {
            yield return new WaitForSeconds(0.1f); // Проверка 10 раз в секунду

            int currentCount = 0;
            if (pileManager.Foundations != null)
            {
                foreach (var f in pileManager.Foundations) currentCount += f.Count;
            }

            // Если карт стало больше -> начисляем очки
            if (currentCount > previousFoundationCount)
            {
                int diff = currentCount - previousFoundationCount;

                // Начисляем очки за каждую прилетевшую карту
                // Обычно за перемещение в Foundation дают 10 очков
                if (scoreManager != null)
                {
                    // Мы не можем использовать OnCardMove, т.к. не знаем откуда прилетела карта.
                    // Поэтому используем ручное добавление, если оно есть, или имитируем.
                    // Предположим, что у scoreManager есть метод AddScore(int amount).
                    // Если его нет, используйте свойство CurrentScore += ...

                    // Вариант А (если есть AddScore):
                    // scoreManager.AddScore(diff * 10);

                    // Вариант Б (через рефлексию или свойство, если оно доступно для записи):
                    // scoreManager.CurrentScore += diff * 10; 

                    // Вариант В (имитация хода из Tableau, дает +10):
                    // scoreManager.OnCardMove(pileManager.GetTableau(0), pileManager.GetFoundation(0)); 

                    // Самый надежный вариант без доступа к ScoreManager:
                    // Просто обновляем UI, если scoreManager сам не обновляется.
                    // Но скорее всего вам нужно добавить метод AddManualScore(int points) в KlondikeScoreManager.

                    // ВРЕМЕННОЕ РЕШЕНИЕ (напишите метод AddPoints в ScoreManager):
                    // scoreManager.AddPoints(diff * 10);

                    // Если метода нет, попробуем обновить через событие хода (костыль, но сработает для +10):
                    for (int i = 0; i < diff; i++)
                        scoreManager.OnCardMove(pileManager.Tableau[0], pileManager.Foundations[0]);
                }

                UpdateFullUI();
                previousFoundationCount = currentCount;
            }
        }
    }

    public bool IsGameWon()
    {
        // --- ФИКС 4: Защита от ложной победы, если фундаментов 0 ---
        if (pileManager?.Foundations == null || pileManager.Foundations.Count == 0) return false;

        foreach (var foundation in pileManager.Foundations)
        {
            if (foundation == null || !foundation.IsComplete()) return false;
        }
        return true;
    }

    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug: Restart Game")]
    private void DebugRestartGame() { RestartGame(); }

    [ContextMenu("Debug: Check Win Condition")]
    private void DebugCheckWin() { bool won = IsGameWon(); Debug.Log($"Game is {(won ? "WON" : "NOT won")}"); }

    [ContextMenu("Debug: Validate Setup")]
    private void DebugValidateSetup()
    {
        FindMissingComponents();
        bool valid = ValidateCriticalReferences();
        if (valid) Debug.Log("✓ All critical references are valid!");
        else Debug.LogError("✗ Some critical references are missing!");
    }
#endif
}

public enum StockDealMode
{
    Draw1 = 1,
    Draw3 = 3
}