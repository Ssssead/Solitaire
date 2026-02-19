using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class KlondikeModeManager : MonoBehaviour, IModeManager, ICardGameMode
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

    [Header("UI & HUD")]

    [Tooltip("Текст для отображения количества ходов")]
    public TMP_Text movesText;
    [Tooltip("Текст для отображения очков")]
    public TMP_Text scoreText; // [NEW]
    [Tooltip("Текст для отображения времени")]
    public TMP_Text timeText;  // [NEW]

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

    [Header("UI Controller")]
    public GameUIController gameUI;

    private bool hasWonGame = false;
    private bool hasGameStarted = false;

    // [NEW] Локальный таймер
    private float gameTimer = 0f;
    private bool isTimerRunning = false;

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

            // [FIX] Определяем направление скрытия.
            // Если кнопка в нижней половине экрана (y < 0), прячем вниз (-).
            // Если в верхней (y > 0), прячем вверх (+).
            float hideOffsetY = (autoWinShowPos.y < 0) ? -250f : 250f;

            autoWinHidePos = autoWinShowPos + new Vector2(0, hideOffsetY);

            // Сразу ставим в позицию скрытия
            autoWinRect.anchoredPosition = autoWinHidePos;

            // Выключаем объект
            autoWinButton.gameObject.SetActive(false);
            autoWinButton.onClick.RemoveAllListeners();
            autoWinButton.onClick.AddListener(OnAutoWinClicked);
        }

        if (playIntroOnStart && introController != null)
        {
            introController.PrepareIntro();
            InitializeGameLogicOnly();
            introController.PlayIntroSequence();
        }
        else
        {
            StartNewGame();
        }
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
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }

        IsInputAllowed = true;
        hasWonGame = false;
        hasGameStarted = false;

        // Сброс таймера
        gameTimer = 0f;
        isTimerRunning = false;

        UpdateFullUI(); // Обновляем весь UI (ходы, очки, время)

        if (defeatManager != null) defeatManager.ResetManager();
        if (scoreManager != null) scoreManager.ResetScore();

        LogDebug("Starting new game...");
        pileManager.CreatePiles();

        if (dragManager != null) dragManager.RefreshContainers();

        Difficulty currentDiff = GameSettings.CurrentDifficulty;
        int drawParam = (stockDealMode == StockDealMode.Draw3) ? 3 : 1;

        Deal cachedDeal = null;
        if (DealCacheSystem.Instance != null)
        {
            cachedDeal = DealCacheSystem.Instance.GetDeal(GameType.Klondike, currentDiff, drawParam);
        }

        if (cachedDeal != null && deckManager != null)
        {
            Debug.Log($"[KMM] Loading cached deal: {currentDiff} Draw{drawParam}");
            deckManager.LoadDeal(cachedDeal);
        }
        else
        {
            Debug.LogWarning("[KMM] No cached deal found! Falling back to random.");
            deckManager.DealInitial();
        }

        animationService.ReorderAllContainers(pileManager.GetAllContainerTransforms());

        // Еще раз обновляем UI, чтобы сбросить очки после DealInitial
        UpdateFullUI();
    }

    private void OnDestroy()
    {
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }
    }

    public void RegisterMoveAndStartIfNeeded()
    {
        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true; // Запускаем таймер при первом ходе

            if (StatisticsManager.Instance != null)
            {
                Difficulty diff = GameSettings.CurrentDifficulty;
                string variant = (stockDealMode == StockDealMode.Draw3) ? "Draw3" : "Draw1";
                StatisticsManager.Instance.OnGameStarted("Klondike", diff, variant);
            }
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        UpdateFullUI(); // Обновляем все текстовые поля
        CheckGameState();
    }

    // [NEW] Единый метод обновления интерфейса
    private void UpdateFullUI()
    {
        // 1. Ходы
        if (movesText != null)
        {
            // [FIX] Если игра еще не началась (идет анимация или ожидание первого хода),
            // мы ВСЕГДА показываем 0, игнорируя старые данные из StatisticsManager.
            if (!hasGameStarted)
            {
                movesText.text = "0";
            }
            else if (StatisticsManager.Instance != null)
            {
                movesText.text = $"{StatisticsManager.Instance.GetCurrentMoves()}";
            }
            else
            {
                movesText.text = "0";
            }
        }

        // 2. Очки
        if (scoreText != null)
        {
            // То же самое для очков: если игра не началась, очков визуально 0
            if (!hasGameStarted)
            {
                scoreText.text = "0";
            }
            else
            {
                int score = scoreManager != null ? scoreManager.CurrentScore : 0;
                scoreText.text = $"{score}";
            }
        }

        // 3. Время
        if (!hasGameStarted)
        {
            UpdateTimeUI(); // Сбросит в 0:00, так как gameTimer мы обнулили в Restart
        }
    }

    // [NEW] Обновление только текста времени
    private void UpdateTimeUI()
    {
        if (timeText != null)
        {
            int totalSeconds = Mathf.FloorToInt(gameTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timeText.text = string.Format("{0}:{1:00}", minutes, seconds);
        }
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

        // Регистрируем ход
        RegisterMoveAndStartIfNeeded();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.StartTimerIfNotStarted();

        // [FIX] Используем IsEmpty() вместо Count == 0, так как это надежнее
        // и работает, даже если Count - это метод, а не свойство.
        bool isStockEmpty = pileManager.StockPile.IsEmpty();
        bool isWasteHasCards = !pileManager.WastePile.IsEmpty();

        bool isRecycle = (isStockEmpty && isWasteHasCards);

        // Выполняем действие
        var deckManager = GetComponent<DeckManager>();
        if (deckManager != null) deckManager.DrawFromStock();

        // Уведомляем ScoreManager о ходе для истории Undo
        if (scoreManager != null)
        {
            if (isRecycle)
            {
                // Карты летят Waste -> Stock
                scoreManager.OnCardMove(pileManager.WastePile, pileManager.StockPile);
            }
            else
            {
                // Карты летят Stock -> Waste
                scoreManager.OnCardMove(pileManager.StockPile, pileManager.WastePile);
            }
        }

        UpdateFullUI();
    }
    public void OnUndoAllAction()
    {
        // 1. Регистрируем это действие как ход (время идет, счетчик ходов +1)
        RegisterMoveAndStartIfNeeded();

        // 2. Скрываем кнопку авто-победы
        isAutoWinVisible = false;
        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);

        // 3. Сбрасываем Пат в DeckManager
        if (deckManager != null) deckManager.ResetStalemate();

        // 4. СБРОС СЧЕТА В 0 (Исправление)
        if (scoreManager != null) scoreManager.ResetScore();

        // 5. Обновляем UI
        UpdateFullUI();
    }
    public bool OnDropToBoard(CardController card, Vector2 anchoredPosition)
    {
        return dragManager?.OnDropToBoard(card, anchoredPosition) ?? false;
    }
    public bool IsMatchInProgress()
    {
        return hasGameStarted;
    }
    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // [FIX] Используем GetComponentInParent, чтобы найти WastePile сквозь Slot_0
        lastInteractionSource = card.GetComponentInParent<ICardContainer>();

        // Отладка: Если кликнули в Waste, должно написать WastePile
        // if (lastInteractionSource != null) Debug.Log($"Clicked in: {lastInteractionSource.GetType().Name}");

        dragManager?.OnCardClicked(card);
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // [FIX] Явно ищем источник перед тем, как AutoMove начнет двигать карту
        ICardContainer oldContainer = card.GetComponentInParent<ICardContainer>();

        // Запоминаем его и в глобальную переменную (на всякий случай)
        lastInteractionSource = oldContainer;

        autoMoveService?.OnCardRightClicked(card);

        // Передаем НАЙДЕННЫЙ источник в корутину
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
            // [FIX] Передаем сохраненный oldContainer (WastePile) в ScoreManager
            if (scoreManager != null)
            {
                scoreManager.OnCardMove(oldContainer, newContainer);
            }

            if (deckManager != null) deckManager.OnProductiveMoveMade();

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
        // Берем источник из кэша (сохраненного при OnCardClicked)
        ICardContainer source = lastInteractionSource;

        // Если по какой-то причине кэш пуст, пробуем найти через DragManager
        if (source == null && dragManager != null)
        {
            source = dragManager.GetSourceContainer();
        }

        // Выполняем сброс
        dragManager?.OnCardDroppedToContainer(card, container);

        if (container is TableauPile || container is FoundationPile)
        {
            if (deckManager != null) deckManager.OnProductiveMoveMade();

            RegisterMoveAndStartIfNeeded();

            // Начисляем очки
            if (scoreManager != null)
            {
                // [FIX] Здесь source должен быть WastePile. 
                // Если он null, очки не начислятся.
                scoreManager.OnCardMove(source, container);
            }

            UpdateFullUI();
        }

        // Не очищаем lastInteractionSource сразу, иногда он нужен для быстрых кликов,
        // но можно и очистить: lastInteractionSource = null;
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

    #region Public API

    public void RestartGame()
    {
        UpdateFullUI();

        // 1. Применяем настройки (как вы и просили)
        var deck = GetComponent<DeckManager>();
        // Если компонент не на том же объекте, ищем ссылку
        if (deck == null) deck = deckManager;

        if (deck != null)
        {
            deck.difficulty = GameSettings.CurrentDifficulty;
            this.stockDealMode = (GameSettings.KlondikeDrawCount == 3) ? StockDealMode.Draw3 : StockDealMode.Draw1;
        }

        // 2. Учитываем поражение в статистике
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }

        // 3. ВМЕСТО StartNewGame() ЗАПУСКАЕМ КОРУТИНУ
        StartCoroutine(SoftRestartRoutine(deck));
    }
    private IEnumerator SoftRestartRoutine(DeckManager deck)
    {
        // Блокируем ввод
        IsInputAllowed = false;

        // Сброс переменных состояния
        hasWonGame = false;
        hasGameStarted = false;
        gameTimer = 0f;
        isTimerRunning = false;

        // Прячем кнопку авто-победы
        isAutoWinVisible = false;
        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);

        // Сброс менеджеров
        if (defeatManager != null) defeatManager.ResetManager();
        if (scoreManager != null) scoreManager.ResetScore();

        // Обновляем UI (счет 0, время 0:00)
        UpdateFullUI();

        // ОЧИСТКА СТОЛА
        pileManager.ClearAllPiles();
        // Пересоздаем слоты (на всякий случай, если что-то сбилось)
        pileManager.CreatePiles();
        if (dragManager != null) dragManager.RefreshContainers();

        // Удаляем физические объекты карт (те, что упали вниз в SceneExitAnimator)
        cardFactory.DestroyAllCards();

        // Ждем 1 кадр, чтобы Unity успела всё удалить
        yield return null;

        // ПРИЛЕТ НОВОЙ КОЛОДЫ
        if (deck != null)
        {
            // Эта функция сама сгенерирует расклад, анимирует прилет и раздаст карты
            yield return StartCoroutine(deck.PlayIntroDeckArrival(1.2f));
        }
        else
        {
            // Если DeckManager нет, просто запускаем мгновенно
            StartNewGame();
        }

        // Разблокируем ввод
        IsInputAllowed = true;

        // Обновляем Z-индексы слотов
        animationService?.ReorderAllContainers(pileManager.GetAllContainerTransforms());

        // Финальное обновление UI
        UpdateFullUI();
    }

    public void ExecuteSoftRestart()
    {
        // --- ШАГ 0: ПРИМЕНЯЕМ НОВЫЕ НАСТРОЙКИ ---
        // Игрок мог изменить их в Win/Defeat панели перед нажатием New Game.
        // Мы должны обновить локальные переменные и DeckManager.

        this.stockDealMode = (GameSettings.KlondikeDrawCount == 3) ? StockDealMode.Draw3 : StockDealMode.Draw1;

        if (deckManager != null)
        {
            deckManager.difficulty = GameSettings.CurrentDifficulty;
        }

        Debug.Log($"[KMM] Soft Restarting with: {GameSettings.CurrentDifficulty}, {this.stockDealMode}");
        // -----------------------------------------

        // 1. Сброс состояния игры
        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameAbandoned();

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

        UpdateFullUI();

        // 3. Очистка стола
        pileManager.ClearAllPiles();
        cardFactory.DestroyAllCards();

        // 4. Запуск цепочки (DeckManager теперь имеет правильные difficulty/drawMode)
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
            // ... (код победы без изменений) ...
            hasWonGame = true;
            IsInputAllowed = false;
            isTimerRunning = false;

            // Обновляем статистику
            if (StatisticsManager.Instance != null)
            {
                int finalMoves = StatisticsManager.Instance.GetCurrentMoves();
                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;
                StatisticsManager.Instance.OnGameWon(finalScore);
                if (gameUI != null) gameUI.OnGameWon(finalMoves);
            }
            return;
        }

        // Проверяем возможность авто-победы
        bool canAutoWin = CanAutoWin();

        // [DEBUG] Раскомментируйте эту строку, чтобы видеть в консоли, почему кнопка не показывается
        // if (!canAutoWin) Debug.Log($"AutoWin False. Input: {IsInputAllowed}, StockEmpty: {pileManager.StockPile.IsEmpty()}, WasteCount: {pileManager.WastePile.Count}");

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
        // --- ИСПРАВЛЕНО: Undo теперь считается действием (+1 ход) ---
        RegisterMoveAndStartIfNeeded();
        // ------------------------------------------------------------

        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);
        if (defeatManager != null) defeatManager.OnUndo();
        if (deckManager != null) deckManager.ResetStalemate();
        if (scoreManager != null) scoreManager.OnUndo();

        UpdateFullUI(); // Обновляем очки после Undo
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
        RegisterMoveAndStartIfNeeded();

        // Прячем кнопку (анимацией)
        isAutoWinVisible = false;
        if (autoWinAnimCoroutine != null) StopCoroutine(autoWinAnimCoroutine);
        autoWinAnimCoroutine = StartCoroutine(AnimateAutoWinButton(false));

        // Запускаем анимацию полета карт
        if (autoMoveService != null) StartCoroutine(autoMoveService.PlayAutoWinAnimation());

        // [FIX] Запускаем отслеживание очков во время полета карт
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
        if (pileManager?.Foundations == null) return false;
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