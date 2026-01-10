// KlondikeModeManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Главный менеджер режима игры Косынка (Klondike Solitaire).
/// Координирует работу всех сервисов и компонентов.
/// Реализует интерфейс IModeManager для интеграции с системой карточных игр.
/// </summary>
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
    public bool IsInputAllowed { get; set; } = true; // По умолчанию true

    [Header("UI")]
    public GameUIController gameUI; // Ссылка на новый скрипт

    private bool hasWonGame = false; // Флаг, чтобы не вызывать победу много раз
    public string GameName => "Klondike"; // Имя для статистики






    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private bool isInitialized = false;

    #region Initialization

    private void Awake()
    {
        LogDebug("=== Awake Start ===");

        // 1) Находим компоненты если не назначены
        FindMissingComponents();

        // 2) Проверяем критические зависимости
        if (!ValidateCriticalReferences())
        {
            Debug.LogError("[KlondikeModeManager] Critical references missing! Check Inspector.");
            return;
        }

        // 3) Подготавливаем аргументы для Initialize
        object[] initArgs = PrepareInitializationArguments();

        // 4) Инициализируем все сервисы
        InitializeAllServices(initArgs);

        isInitialized = true;
        LogDebug("=== Awake Complete ===");
    }

    /// <summary>
    /// Находит отсутствующие компоненты автоматически.
    /// </summary>
    private void FindMissingComponents()
    {
        // Пытаемся найти компоненты локально или в сцене
        pileManager = pileManager ?? GetComponent<PileManager>() ?? FindObjectOfType<PileManager>();
        deckManager = deckManager ?? GetComponent<DeckManager>() ?? FindObjectOfType<DeckManager>();
        dragManager = dragManager ?? GetComponent<DragManager>() ?? FindObjectOfType<DragManager>();
        undoManager = undoManager ?? GetComponent<UndoManager>() ?? FindObjectOfType<UndoManager>();
        animationService = animationService ?? GetComponent<AnimationService>() ?? FindObjectOfType<AnimationService>();
        autoMoveService = autoMoveService ?? GetComponent<AutoMoveService>() ?? FindObjectOfType<AutoMoveService>();
        defeatManager = defeatManager ?? GetComponent<DefeatManager>() ?? FindObjectOfType<DefeatManager>();
        scoreManager = scoreManager ?? GetComponent<KlondikeScoreManager>() ?? FindObjectOfType<KlondikeScoreManager>();

        // Canvas
        if (rootCanvas == null)
        {
            rootCanvas = cardFactory?.rootCanvas ?? FindObjectOfType<Canvas>();
        }

        // DragLayer
        if (dragLayer == null && rootCanvas != null)
        {
            dragLayer = rootCanvas.transform as RectTransform;
        }
    }

    /// <summary>
    /// Проверяет наличие критически важных ссылок.
    /// </summary>
    private bool ValidateCriticalReferences()
    {
        bool valid = true;

        if (cardFactory == null)
        {
            Debug.LogError("[KlondikeModeManager] CardFactory is null!");
            valid = false;
        }

        if (pileManager == null)
        {
            Debug.LogError("[KlondikeModeManager] PileManager is null!");
            valid = false;
        }

        if (deckManager == null)
        {
            Debug.LogError("[KlondikeModeManager] DeckManager is null!");
            valid = false;
        }

        if (dragManager == null)
        {
            Debug.LogError("[KlondikeModeManager] DragManager is null!");
            valid = false;
        }

        if (rootCanvas == null)
        {
            Debug.LogWarning("[KlondikeModeManager] Canvas is null!");
        }

        if (tableauSlotsParent == null)
        {
            Debug.LogError("[KlondikeModeManager] tableauSlotsParent is null!");
            valid = false;
        }

        if (foundationSlotsParent == null)
        {
            Debug.LogError("[KlondikeModeManager] foundationSlotsParent is null!");
            valid = false;
        }

        return valid;
    }

    /// <summary>
    /// Подготавливает массив аргументов для инициализации сервисов.
    /// </summary>
    private object[] PrepareInitializationArguments()
    {
        RectTransform tableauSlotsRect = tableauSlotsParent as RectTransform;
        RectTransform foundationSlotsRect = foundationSlotsParent as RectTransform;
        RectTransform stockSlotRect = stockSlot as RectTransform;
        RectTransform wasteSlotRect = wasteSlot as RectTransform;

        // ВАЖНО: не добавляем в массив одинаковые объекты дважды!
        // Это может привести к тому что SafeInvokeInitialize перепутает аргументы

        var args = new List<object>
        {
            this,                       // KlondikeModeManager
            cardFactory,
            pileManager,
            deckManager,
            dragManager,
            undoManager,
            animationService,
            autoMoveService,
            rootCanvas,
            (object)tableauVerticalGap  // boxing float
        };

        // Добавляем Transform/RectTransform только если они уникальны
        // PileManager должен использовать уже назначенные в Inspector ссылки

        // Для других компонентов (не PileManager) добавляем слоты
        if (tableauSlotsParent != null) args.Add(tableauSlotsParent);
        if (foundationSlotsParent != null) args.Add(foundationSlotsParent);
        if (stockSlot != null) args.Add(stockSlot);
        if (wasteSlot != null) args.Add(wasteSlot);

        if (tableauSlotsRect != null && tableauSlotsRect != tableauSlotsParent as object)
            args.Add(tableauSlotsRect);
        if (foundationSlotsRect != null && foundationSlotsRect != foundationSlotsParent as object)
            args.Add(foundationSlotsRect);
        if (stockSlotRect != null && stockSlotRect != stockSlot as object)
            args.Add(stockSlotRect);
        if (wasteSlotRect != null && wasteSlotRect != wasteSlot as object)
            args.Add(wasteSlotRect);
        if (dragLayer != null)
            args.Add(dragLayer);

        return args.ToArray();
    }

    /// <summary>
    /// Инициализирует все сервисы используя рефлексию.
    /// </summary>
    private void InitializeAllServices(object[] availableArgs)
    {
        // 1. PileManager (особый случай)
        if (pileManager != null)
        {
            bool hasAllSlots = (pileManager.tableauSlotsParent != null &&
                               pileManager.foundationSlotsParent != null &&
                               pileManager.stockSlotTransform != null &&
                               pileManager.wasteSlotTransform != null);

            if (hasAllSlots)
            {
                pileManager.Initialize(this, null, null, null, null, tableauVerticalGap);
            }
            else
            {
                SafeInvokeInitialize(pileManager, availableArgs, "PileManager");
            }
        }

        // 2. DeckManager, UndoManager, AnimationService (можно оставить SafeInvoke, там нет путаницы с RectTransform)
        SafeInvokeInitialize(deckManager, availableArgs, "DeckManager");
        SafeInvokeInitialize(undoManager, availableArgs, "UndoManager");
        SafeInvokeInitialize(animationService, availableArgs, "AnimationService");

        // 3. DragManager - ИНИЦИАЛИЗИРУЕМ ЯВНО!
        if (dragManager != null)
        {
            // Передаем this, canvas и ЯВНО dragLayer
            dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);
            LogDebug("DragManager initialized explicitly with DragLayer: " + (dragLayer != null ? dragLayer.name : "null"));
        }

        // 4. AutoMoveService - ИНИЦИАЛИЗИРУЕМ ЯВНО!
        if (autoMoveService != null)
        {
            // (Manager, PileManager, UndoManager, AnimationService, Canvas, DragLayer, gap)
            autoMoveService.Initialize(
                this,
                pileManager,
                undoManager,
                animationService,
                rootCanvas,
                dragLayer, // <-- Важно!
                tableauVerticalGap
            );
            LogDebug("AutoMoveService initialized explicitly with DragLayer");
        }

        if (defeatManager != null)
        {
            defeatManager.Initialize(pileManager, gameUI);
        }
    }

    private void Start()
    {
        if (!isInitialized)
        {
            Debug.LogError("[KlondikeModeManager] Not initialized properly!");
            return;
        }
        // --- ИНТЕГРАЦИЯ С МЕНЮ ---
        // 1. Применяем сложность
        var deckManager = GetComponent<DeckManager>() ?? FindObjectOfType<DeckManager>();
        if (deckManager != null)
        {
            deckManager.difficulty = GameSettings.CurrentDifficulty;
        }

        // 2. Применяем режим сдачи (конвертируем int в enum)
        if (GameSettings.KlondikeDrawCount == 3)
            this.stockDealMode = StockDealMode.Draw3;
        else
            this.stockDealMode = StockDealMode.Draw1;
        // -------------------------
        // Скрываем кнопку на старте
        if (autoWinButton != null)
        {
            autoWinButton.gameObject.SetActive(false);
            autoWinButton.onClick.RemoveAllListeners();
            autoWinButton.onClick.AddListener(OnAutoWinClicked);
        }

        StartNewGame();
    }

    /// <summary>
    /// Запускает новую игру.
    /// </summary>
    public void StartNewGame()
    {
        // 1. Если предыдущая игра была начата, но не закончена - засчитываем поражение
        // (Это обновит серию побед и историю)
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameAbandoned();

        IsInputAllowed = true;
        hasWonGame = false;

        if (defeatManager != null) defeatManager.ResetManager();
        if (scoreManager != null) scoreManager.ResetScore(); // <-- СБРОС ОЧКОВ

        LogDebug("Starting new game...");
        pileManager.CreatePiles();

        // ... (ваш код RefreshContainers и DealInitial) ...
        if (dragManager != null) dragManager.RefreshContainers();
        deckManager.DealInitial();
        animationService.ReorderAllContainers(pileManager.GetAllContainerTransforms());

        // 2. Сообщаем статистике, что началась конкретная игра
        if (StatisticsManager.Instance != null)
        {
            Difficulty diff = GameSettings.CurrentDifficulty;
            // Превращаем enum DrawMode в строку "Draw1" или "Draw3"
            string variant = (stockDealMode == StockDealMode.Draw3) ? "Draw3" : "Draw1";

            // "Klondike" тут жестко задано, так как это KlondikeModeManager
            StatisticsManager.Instance.OnGameStarted("Klondike", diff, variant);
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
        // Если ввод запрещен - игнорируем клик
        if (!IsInputAllowed) return;

        // --- СТАТИСТИКА: Таймер ---
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.StartTimerIfNotStarted();
            // Можно ли считать клик по стоку за "Ход" (Move)? 
            // В классике обычно считаются только перемещения карт, но таймер запустить надо.
            StatisticsManager.Instance.RegisterMove();
        }
        // --------------------------

        var deckManager = GetComponent<DeckManager>();
        if (deckManager != null)
        {
            deckManager.DrawFromStock();
        }
    }

    public bool OnDropToBoard(CardController card, Vector2 anchoredPosition)
    {
        return dragManager?.OnDropToBoard(card, anchoredPosition) ?? false;
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return; // Блокировка
        dragManager?.OnCardClicked(card);
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // 1. Запоминаем текущий контейнер
        ICardContainer oldContainer = card.CurrentContainer;

        // 2. Запускаем авто-ход
        autoMoveService?.OnCardRightClicked(card);

        // 3. Ждем результата и начисляем очки
        StartCoroutine(CheckAutoMoveResult(card, oldContainer));
    }

    private System.Collections.IEnumerator CheckAutoMoveResult(CardController card, ICardContainer oldContainer)
    {
        // Ждем 0.25 секунды, пока карта летит (в это время у нее нет контейнера)
        yield return new WaitForSeconds(0.25f);

        // Теперь карта приземлилась
        ICardContainer newContainer = card.CurrentContainer;

        // Если контейнер изменился — значит, ход был успешным
        if (newContainer != null && newContainer != oldContainer)
        {
            // --- СТАТИСТИКА И ОЧКИ ---

            // 1. Начисляем очки
            if (scoreManager != null) scoreManager.OnCardMove(newContainer);

            // 2. Статистика ходов
            if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();

            // 3. Сброс пата
            if (deckManager != null) deckManager.OnProductiveMoveMade();

            // 4. Проверка победы
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

            // --- ДОБАВЛЕНО: СТАТИСТИКА ---
            // 1. Считаем очки
            if (scoreManager != null) scoreManager.OnCardMove(container);

            // 2. Регистрируем +1 ход в статистику
            if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
            // -----------------------------
        }
    }

    public void OnKeyboardPick(CardController card)
    {
        dragManager?.OnKeyboardPick(card);
    }

    #endregion

    #region Card Event Registration

    /// <summary>
    /// Регистрирует события для карты (вызывается при создании каждой карты).
    /// </summary>
    public void RegisterCardEvents(CardController card)
    {
        if (card == null) return;

        dragManager?.RegisterCardEvents(card);
        autoMoveService?.RegisterCardForAutoMove(card);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Безопасный вызов метода Initialize через рефлексию.
    /// Пытается найти подходящую перегрузку Initialize и вызвать её с правильными аргументами.
    /// </summary>
    private void SafeInvokeInitialize(object component, object[] availableArgs, string componentName)
    {
        if (component == null)
        {
            LogDebug($"Skipping {componentName}: component is null");
            return;
        }

        Type componentType = component.GetType();

        // Получаем все методы Initialize
        var initMethods = componentType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                       .Where(m => string.Equals(m.Name, "Initialize", StringComparison.OrdinalIgnoreCase))
                                       .OrderBy(m => m.GetParameters().Length)  // Пробуем короткие перегрузки первыми
                                       .ToArray();

        if (initMethods.Length == 0)
        {
            LogDebug($"No Initialize method found for {componentName}");
            return;
        }

        // Пробуем каждую перегрузку
        foreach (var method in initMethods)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];
            bool canInvoke = true;

            // Подбираем аргументы для каждого параметра
            for (int i = 0; i < parameters.Length; i++)
            {
                Type paramType = parameters[i].ParameterType;
                bool found = false;

                // Ищем подходящий аргумент в availableArgs
                foreach (var arg in availableArgs)
                {
                    if (arg == null) continue;

                    if (paramType.IsAssignableFrom(arg.GetType()))
                    {
                        args[i] = arg;
                        found = true;
                        break;
                    }

                    // Поддержка примитивных типов
                    if (paramType.IsPrimitive || paramType == typeof(float) || paramType == typeof(double))
                    {
                        try
                        {
                            if (arg is IConvertible)
                            {
                                args[i] = Convert.ChangeType(arg, paramType);
                                found = true;
                                break;
                            }
                        }
                        catch { /* ignore */ }
                    }
                }

                if (!found)
                {
                    canInvoke = false;
                    break;
                }
            }

            if (!canInvoke) continue;

            // Пытаемся вызвать метод
            try
            {
                method.Invoke(component, args);
                LogDebug($"✓ Initialized {componentName} using {method.Name}({parameters.Length} params)");
                return;  // Успешно вызвали - выходим
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KMM] Failed to invoke Initialize on {componentName}: {ex.Message}");
            }
        }

        // Если дошли сюда - ни одна перегрузка не подошла
        Debug.LogWarning($"[KMM] No suitable Initialize overload found for {componentName}");
    }

    private void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[KlondikeModeManager] {message}");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Перезапускает игру.
    /// </summary>
    public void RestartGame()
    {
        // Вся логика настройки переехала сюда из UI
        var deck = GetComponent<DeckManager>();
        if (deck != null)
        {
            deck.difficulty = GameSettings.CurrentDifficulty;
            // Настраиваем режим раздачи
            this.stockDealMode = (GameSettings.KlondikeDrawCount == 3) ? StockDealMode.Draw3 : StockDealMode.Draw1;

            deck.RestartGame();
        }
    }

    public void CheckGameState()
    {
        if (hasWonGame) return;

        if (IsGameWon())
        {
            hasWonGame = true;
            Debug.Log("Game Won!");

            // --- ДОБАВЛЕНО: Сохранение победы ---
            if (StatisticsManager.Instance != null)
            {
                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;
                // Передаем очки в менеджер
                StatisticsManager.Instance.OnGameWon(finalScore);
            }
            // ------------------------------------

            if (gameUI != null) gameUI.OnGameWon(); // Показываем UI победы
            return;
        }

        bool canAutoWin = CanAutoWin();

        if (autoWinButton != null)
        {
            // Показываем кнопку только если условие выполнено
            if (autoWinButton.gameObject.activeSelf != canAutoWin)
            {
                autoWinButton.gameObject.SetActive(canAutoWin);
            }
        }
        // 3. НОВОЕ: Проверка поражения
        // Вызываем только если игра не выиграна и авто-вин не активен (или активен, не важно)
        if (defeatManager != null)
        {
            defeatManager.CheckGameStatus();
        }
    }
    public void OnUndoAction()
    {
        // 1. Скрываем кнопку авто-победы
        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);

        // 2. Сообщаем DefeatManager
        if (defeatManager != null) defeatManager.OnUndo();

        // 3. Сброс счетчика пата
        if (deckManager != null) deckManager.ResetStalemate();

        // --- ИСПРАВЛЕНИЕ: Вычитаем очки при отмене ---
        if (scoreManager != null)
        {
            scoreManager.OnUndo();
        }
        // ---------------------------------------------
    }



    private bool CanAutoWin()
    {
        // 1. Stock должен быть пуст
        if (pileManager.StockPile != null && !pileManager.StockPile.IsEmpty()) return false;

        // 2. Waste должен быть пуст
        if (pileManager.WastePile != null && pileManager.WastePile.Count > 0) return false;

        // 3. В Tableau не должно быть скрытых карт
        if (pileManager.Tableau != null)
        {
            foreach (var pile in pileManager.Tableau)
            {
                if (pile.HasHiddenCards()) return false;
            }
        }

        return true;
    }

    private void OnAutoWinClicked()
    {
        // Скрываем кнопку чтобы не нажали дважды
        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);

        // Запускаем процесс через AutoMoveService
        if (autoMoveService != null)
        {
            StartCoroutine(autoMoveService.PlayAutoWinAnimation());
        }
    }


    /// <summary>
    /// Проверяет, выиграна ли игра (все foundations заполнены).
    /// </summary>
    public bool IsGameWon()
    {
        if (pileManager?.Foundations == null) return false;

        foreach (var foundation in pileManager.Foundations)
        {
            if (foundation == null || !foundation.IsComplete())
            {
                return false;
            }
        }

        return true;
    }

    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug: Restart Game")]
    private void DebugRestartGame()
    {
        RestartGame();
    }

    [ContextMenu("Debug: Check Win Condition")]
    private void DebugCheckWin()
    {
        bool won = IsGameWon();
        Debug.Log($"Game is {(won ? "WON" : "NOT won")}");
    }

    [ContextMenu("Debug: Validate Setup")]
    private void DebugValidateSetup()
    {
        FindMissingComponents();
        bool valid = ValidateCriticalReferences();

        if (valid)
        {
            Debug.Log("✓ All critical references are valid!");
        }
        else
        {
            Debug.LogError("✗ Some critical references are missing!");
        }
    }
#endif
}
public enum StockDealMode
{
    Draw1 = 1,
    Draw3 = 3
}