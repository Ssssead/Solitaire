using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("UI Buttons")]
    public Button autoWinButton;

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

    [Header("UI")]
    public GameUIController gameUI;

    private bool hasWonGame = false;

    // --- НОВОЕ: Флаг старта игры ---
    private bool hasGameStarted = false;

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
            autoWinButton.gameObject.SetActive(false);
            autoWinButton.onClick.RemoveAllListeners();
            autoWinButton.onClick.AddListener(OnAutoWinClicked);
        }

        StartNewGame();
    }

    public void StartNewGame()
    {
        // 1. Если предыдущая была начата, но не закончена -> Поражение
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }

        IsInputAllowed = true;
        hasWonGame = false;
        hasGameStarted = false; // Сброс флага

        if (defeatManager != null) defeatManager.ResetManager();
        if (scoreManager != null) scoreManager.ResetScore();

        LogDebug("Starting new game...");
        pileManager.CreatePiles();

        if (dragManager != null) dragManager.RefreshContainers();
        deckManager.DealInitial();
        animationService.ReorderAllContainers(pileManager.GetAllContainerTransforms());

        // ВАЖНО: Мы НЕ вызываем OnGameStarted здесь. Ждем первого хода.
    }

    // --- НОВОЕ: Обработка выхода со сцены ---
    private void OnDestroy()
    {
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }
    }
    // ----------------------------------------

    // --- НОВОЕ: Метод регистрации первого хода ---
    public void RegisterMoveAndStartIfNeeded()
    {
        if (!hasGameStarted)
        {
            hasGameStarted = true;
            if (StatisticsManager.Instance != null)
            {
                Difficulty diff = GameSettings.CurrentDifficulty;
                string variant = (stockDealMode == StockDealMode.Draw3) ? "Draw3" : "Draw1";
                StatisticsManager.Instance.OnGameStarted("Klondike", diff, variant);
            }
        }

        // Регистрируем сам ход
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();
    }
    // ---------------------------------------------

    #endregion

    #region IModeManager Implementation

    public ICardContainer FindNearestContainer(CardController card, Vector2 anchoredPosition, float maxDistance)
    {
        return dragManager?.FindNearestContainer(card, anchoredPosition, maxDistance);
    }

    public void OnStockClicked()
    {
        if (!IsInputAllowed) return;

        // --- ИСПРАВЛЕНО: Регистрация хода ---
        RegisterMoveAndStartIfNeeded();
        // ------------------------------------

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.StartTimerIfNotStarted();

        var deckManager = GetComponent<DeckManager>();
        if (deckManager != null) deckManager.DrawFromStock();
    }

    public bool OnDropToBoard(CardController card, Vector2 anchoredPosition)
    {
        return dragManager?.OnDropToBoard(card, anchoredPosition) ?? false;
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        dragManager?.OnCardClicked(card);
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        ICardContainer oldContainer = card.CurrentContainer;
        autoMoveService?.OnCardRightClicked(card);
        StartCoroutine(CheckAutoMoveResult(card, oldContainer));
    }

    private System.Collections.IEnumerator CheckAutoMoveResult(CardController card, ICardContainer oldContainer)
    {
        yield return new WaitForSeconds(0.25f);

        ICardContainer newContainer = card.CurrentContainer;

        if (newContainer != null && newContainer != oldContainer)
        {
            // --- ИСПРАВЛЕНО: Регистрация хода ---
            RegisterMoveAndStartIfNeeded(); // Фиксируем старт игры при успешном авто-ходе
            // ------------------------------------

            if (scoreManager != null) scoreManager.OnCardMove(newContainer);
            if (deckManager != null) deckManager.OnProductiveMoveMade();

            CheckGameState();
        }
    }

    public void OnCardLongPressed(CardController card)
    {
        dragManager?.OnCardLongPressed(card);
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        dragManager?.OnCardDroppedToContainer(card, container);

        if (container is TableauPile || container is FoundationPile)
        {
            if (deckManager == null) deckManager = GetComponent<DeckManager>();
            if (deckManager != null) deckManager.OnProductiveMoveMade();

            // --- ИСПРАВЛЕНО: Регистрация хода ---
            RegisterMoveAndStartIfNeeded();
            // ------------------------------------

            if (scoreManager != null) scoreManager.OnCardMove(container);
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

    #region Public API

    public void RestartGame()
    {
        var deck = GetComponent<DeckManager>();
        if (deck != null)
        {
            deck.difficulty = GameSettings.CurrentDifficulty;
            this.stockDealMode = (GameSettings.KlondikeDrawCount == 3) ? StockDealMode.Draw3 : StockDealMode.Draw1;

            // Если предыдущая игра была начата - поражение
            if (hasGameStarted && !hasWonGame)
            {
                if (StatisticsManager.Instance != null)
                    StatisticsManager.Instance.OnGameAbandoned();
            }

            StartNewGame(); // Вместо deck.RestartGame, чтобы обновить флаги здесь
        }
    }

    public void CheckGameState()
    {
        if (hasWonGame) return;

        if (IsGameWon())
        {
            hasWonGame = true;
            IsInputAllowed = false; // Блокируем ввод
            Debug.Log("Game Won!");

            // 1. ЗАПОМИНАЕМ ХОДЫ
            int finalMoves = 0;
            if (StatisticsManager.Instance != null)
                finalMoves = StatisticsManager.Instance.GetCurrentMoves();

            // 2. ОТПРАВЛЯЕМ В СТАТИСТИКУ (счетчик сбрасывается)
            if (StatisticsManager.Instance != null)
            {
                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;
                StatisticsManager.Instance.OnGameWon(finalScore);
            }

            // 3. ПОКАЗЫВАЕМ UI
            if (gameUI != null) gameUI.OnGameWon(finalMoves);
            return;
        }

        // Проверка авто-победы
        bool canAutoWin = CanAutoWin();
        if (autoWinButton != null && autoWinButton.gameObject.activeSelf != canAutoWin)
        {
            autoWinButton.gameObject.SetActive(canAutoWin);
        }

        if (defeatManager != null) defeatManager.CheckGameStatus();
    }

    public void OnUndoAction()
    {
        // Undo тоже считается активностью
        RegisterMoveAndStartIfNeeded();

        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);
        if (defeatManager != null) defeatManager.OnUndo();
        if (deckManager != null) deckManager.ResetStalemate();
        if (scoreManager != null) scoreManager.OnUndo();
    }

    private bool CanAutoWin()
    {
        if (pileManager.StockPile != null && !pileManager.StockPile.IsEmpty()) return false;
        if (pileManager.WastePile != null && pileManager.WastePile.Count > 0) return false;
        if (pileManager.Tableau != null)
        {
            foreach (var pile in pileManager.Tableau) if (pile.HasHiddenCards()) return false;
        }
        return true;
    }

    private void OnAutoWinClicked()
    {
        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);
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