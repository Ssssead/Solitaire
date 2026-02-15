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
            // Запоминаем позицию, где кнопка стоит в сцене (это будет позиция показа)
            autoWinShowPos = autoWinRect.anchoredPosition;
            // Позиция скрытия - сдвигаем вверх за пределы экрана (например, +200 пикселей)
            autoWinHidePos = autoWinShowPos + new Vector2(0, 200f);

            // Сразу скрываем
            autoWinRect.anchoredPosition = autoWinHidePos;
            autoWinButton.gameObject.SetActive(false);
            autoWinButton.onClick.RemoveAllListeners();
            autoWinButton.onClick.AddListener(OnAutoWinClicked);
        }

        // --- ИЗМЕНЕНИЯ ЗДЕСЬ ---
        if (playIntroOnStart && introController != null)
        {
            // Подготавливаем всё (скрываем)
            introController.PrepareIntro();

            // Настраиваем логику игры, но НЕ раздаем карты сразу
            InitializeGameLogicOnly();

            // Запускаем кино
            introController.PlayIntroSequence();
        }
        else
        {
            // Старый быстрый старт
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
            if (StatisticsManager.Instance != null)
                movesText.text = $"{StatisticsManager.Instance.GetCurrentMoves()}";
            else
                movesText.text = "0";
        }

        // 2. Очки
        if (scoreText != null)
        {
            int score = scoreManager != null ? scoreManager.CurrentScore : 0;
            scoreText.text = $"{score}";
        }

        // 3. Время (если игра не идет, сбрасываем или оставляем как есть)
        if (!hasGameStarted)
        {
            UpdateTimeUI();
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

    public bool OnDropToBoard(CardController card, Vector2 anchoredPosition)
    {
        return dragManager?.OnDropToBoard(card, anchoredPosition) ?? false;
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
        var deck = GetComponent<DeckManager>();
        if (deck != null)
        {
            deck.difficulty = GameSettings.CurrentDifficulty;
            this.stockDealMode = (GameSettings.KlondikeDrawCount == 3) ? StockDealMode.Draw3 : StockDealMode.Draw1;

            if (hasGameStarted && !hasWonGame)
            {
                if (StatisticsManager.Instance != null)
                    StatisticsManager.Instance.OnGameAbandoned();
            }

            StartNewGame();
        }
    }

    public void CheckGameState()
    {
        if (hasWonGame) return;

        if (IsGameWon())
        {
            hasWonGame = true;
            IsInputAllowed = false;
            isTimerRunning = false;
            Debug.Log("Game Won!");

            int finalMoves = 0;
            if (StatisticsManager.Instance != null)
                finalMoves = StatisticsManager.Instance.GetCurrentMoves();

            if (StatisticsManager.Instance != null)
            {
                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;
                StatisticsManager.Instance.OnGameWon(finalScore);
            }

            if (gameUI != null) gameUI.OnGameWon(finalMoves);
            return;
        }

        // --- ИСПРАВЛЕНИЕ ЛОГИКИ ПОЯВЛЕНИЯ КНОПКИ ---

        bool canAutoWin = CanAutoWin();

        // Сравниваем не с activeSelf, а с нашей логической переменной isAutoWinVisible
        if (canAutoWin != isAutoWinVisible)
        {
            isAutoWinVisible = canAutoWin;

            // Обязательно останавливаем старую анимацию и запускаем новую
            if (autoWinAnimCoroutine != null) StopCoroutine(autoWinAnimCoroutine);
            autoWinAnimCoroutine = StartCoroutine(AnimateAutoWinButton(canAutoWin));
        }

        // -------------------------------------------

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

        if (autoMoveService != null) StartCoroutine(autoMoveService.PlayAutoWinAnimation());
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