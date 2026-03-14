using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MontanaModeManager : MonoBehaviour, IModeManager, ICardGameMode
{
    [Header("Core References")]
    public CardFactory cardFactory;
    public Canvas rootCanvas;
    public RectTransform dragLayer;

    [Header("UI & HUD")]
    public GameUIController gameUI;
    [Tooltip("Текст для отображения количества ходов")]
    public TMP_Text movesText;
    [Tooltip("Текст для отображения времени")]
    public TMP_Text timeText;
    [Tooltip("Текст для отображения очков")]
    public TMP_Text scoreText;
    [Tooltip("Текст ТОЛЬКО для числа оставшихся пересдач")]
    public TMP_Text reshufflesText;

    [Header("Settings")]
    public bool IsHardMode = false; // 0 = Classic (пусто слева), 1 = Hard (пусто справа)

    [Header("Services")]
    public MontanaPileManager pileManager;
    public MontanaDeckManager deckManager;
    public DragManager dragManager;
    public UndoManager undoManager;
    public MontanaAnimationService animationService;
    public MontanaAutoMoveService autoMoveService;
    public MontanaScoreManager scoreManager;

    // --- ICardGameMode Свойства ---
    public bool IsInputAllowed { get; set; } = true;
    public GameType GameType => GameType.Montana;
    public string GameName => "Montana";

    public AnimationService AnimationService => null;
    public PileManager PileManager => pileManager;
    public RectTransform DragLayer => dragLayer;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;

    // --- Локальные переменные состояния ---
    private bool hasGameStarted = false;
    private bool hasWonGame = false;
    public bool isRestarting = false;
    private float gameTimer = 0f;
    private ICardContainer lastInteractionSource;
    [Header("UI Buttons")]
    public UnityEngine.UI.Button undoButton;
    public UnityEngine.UI.Button undoAllButton;

    private Stack<MontanaMoveRecord> undoStack = new Stack<MontanaMoveRecord>();
    private bool isUndoing = false;

    // --- НОВОЕ: Снимок начальной расстановки для Undo All ---
    private Dictionary<CardController, MontanaSlot> initialBoardState = new Dictionary<CardController, MontanaSlot>();

    // --- ЛОГИКА ПЕРЕСДАЧ ---
    public int MaxReshuffles => IsHardMode ? 5 : 3;
    public int CurrentReshufflesLeft { get; private set; }

    #region Initialization & Core Loop

    private void Awake()
    {
        pileManager.Initialize(this);
        deckManager.Initialize(this, cardFactory, pileManager);
        dragManager?.Initialize(this, rootCanvas, dragLayer, undoManager);
        autoMoveService.Initialize(this, pileManager);
        animationService.Initialize(this);

        if (undoButton != null) undoButton.onClick.AddListener(OnUndoButtonClicked);
        if (undoAllButton != null) undoAllButton.onClick.AddListener(OnUndoAllButtonClicked); // <--- ПОДПИСКА
    }

    private void Start() => StartNewGame();

    private void Update()
    {
        if (hasGameStarted && !hasWonGame)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }
    }

    private void OnDestroy()
    {
        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }
    }

    public void StartNewGame()
    {
        // --- ЧТЕНИЕ НАСТРОЕК ИЗ МЕНЮ ---
        IsHardMode = GameSettings.MontanaHard;

        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameAbandoned();

        IsInputAllowed = false;
        hasGameStarted = false;
        hasWonGame = false;
        gameTimer = 0f;
        CurrentReshufflesLeft = MaxReshuffles;

        // --- НОВОЕ: Сброс локального Undo ---
        undoStack.Clear();
        UpdateUndoButton();

        scoreManager.ResetScore();
        undoManager?.ResetHistory(); // Глобальный (на всякий случай)
        cardFactory.DestroyAllCards();

        pileManager.CreatePiles();
        dragManager?.RefreshContainers();

        UpdateFullUI();
        deckManager.DealInitial();
    }

    public void RestartGame()
    {
        isRestarting = true;
        StopAllCoroutines();
        StartNewGame();
    }

    #endregion

    #region Statistics & Game State

    public void RegisterMoveAndStartIfNeeded()
    {
        if (!IsInputAllowed) return;

        if (!hasGameStarted)
        {
            hasGameStarted = true;

            if (StatisticsManager.Instance != null)
            {
                // Берем сложность и вариант игры из GameSettings
                Difficulty diff = GameSettings.CurrentDifficulty;
                string variant = IsHardMode ? "Hard" : "Classic";
                StatisticsManager.Instance.OnGameStarted(GameName, diff, variant);
            }
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        UpdateFullUI();
    }

    public void CheckGameState()
    {
        if (hasWonGame) return;

        UpdateLockedCards();

        // --- НОВОЕ: Синхронизируем состояние кнопок ---
        UpdateUndoButton();

        if (IsGameWon())
        {
            hasWonGame = true;
            IsInputAllowed = false;
            undoManager?.ClearAndLock();

            if (StatisticsManager.Instance != null)
            {
                int finalMoves = StatisticsManager.Instance.GetCurrentMoves();
                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;

                StatisticsManager.Instance.OnGameWon(finalScore);
                if (gameUI != null) gameUI.OnGameWon(finalMoves);
            }
        }
    }
    public void SaveInitialState()
    {
        initialBoardState.Clear();
        foreach (var slot in pileManager.Slots)
        {
            var card = slot.GetTopCard();
            if (card != null)
            {
                initialBoardState[card] = slot;
            }
        }
        UpdateUndoButton();
    }
    public void UpdateLockedCards()
    {
        // 1. Сначала снимаем блокировку со всех карт на поле (нужно для корректной работы Undo)
        foreach (var slot in pileManager.Slots)
        {
            var card = slot.GetTopCard();
            if (card != null)
            {
                card.GetComponent<MontanaCardController>()?.SetLockedState(false);
            }
        }

        // 2. Идем по рядам (сверху вниз) и ищем правильные цепочки слева направо
        for (int r = 0; r < 4; r++)
        {
            var firstCard = pileManager.GetSlot(r, 0).GetTopCard();

            // Цепочка блокируется ТОЛЬКО если она начинается с Туза в нулевой колонке
            if (firstCard != null && firstCard.cardModel.rank == 1)
            {
                firstCard.GetComponent<MontanaCardController>()?.SetLockedState(true);

                for (int c = 1; c < 13; c++)
                {
                    var card = pileManager.GetSlot(r, c).GetTopCard();
                    var prevCard = pileManager.GetSlot(r, c - 1).GetTopCard();

                    if (card == null || prevCard == null) break; // Слот пустой — цепочка обрывается

                    // Если карта подходит к предыдущей (одна масть и на 1 старше), блокируем и её
                    if (card.cardModel.suit == prevCard.cardModel.suit &&
                        card.cardModel.rank == prevCard.cardModel.rank + 1)
                    {
                        card.GetComponent<MontanaCardController>()?.SetLockedState(true);
                    }
                    else
                    {
                        break; // Карта не подходит — цепочка обрывается
                    }
                }
            }
        }
    }
    public bool IsGameWon()
    {
        // Проверяем, что в каждом ряду первые 13 колонок собраны от Туза до Короля
        for (int r = 0; r < 4; r++)
        {
            var firstCard = pileManager.GetSlot(r, 0).GetTopCard();
            if (firstCard == null || firstCard.cardModel.rank != 1) return false;

            for (int c = 1; c < 13; c++)
            {
                var card = pileManager.GetSlot(r, c).GetTopCard();
                var prevCard = pileManager.GetSlot(r, c - 1).GetTopCard();

                if (card == null || prevCard == null) return false;
                if (card.cardModel.suit != prevCard.cardModel.suit ||
                    card.cardModel.rank != prevCard.cardModel.rank + 1) return false;
            }
            // 14-я колонка (индекс 13) должна быть пустой
        }
        return true;
    }

    // Вызов пересдачи (Привяжите к кнопке в UI)
    public void PerformReshuffle()
    {
        if (!IsInputAllowed || CurrentReshufflesLeft <= 0) return;

        RegisterMoveAndStartIfNeeded();

        CurrentReshufflesLeft--;

        // --- ОЧИЩАЕМ ЛОКАЛЬНУЮ ИСТОРИЮ ХОДОВ ---
        undoStack.Clear();
        UpdateUndoButton();

        UpdateFullUI();
        StartCoroutine(deckManager.ReshuffleRoutine());
    }

    #endregion

    #region User Interactions

    public void OnUndoAction() { }
    private IEnumerator UndoRoutine()
    {
        isUndoing = true;
        IsInputAllowed = false;

        var record = undoStack.Pop();
        UpdateUndoButton();

        // 1. Логически и физически вынимаем карту из текущего слота
        record.TargetSlot.RemoveCard(record.Card);

        // 2. Анимация полета назад
        record.Card.transform.SetParent(DragLayer, true);
        record.Card.transform.SetAsLastSibling();

        var mCard = record.Card.GetComponent<MontanaCardController>();
        if (mCard != null) mCard.SetAnimating(true);

        Vector3 startPos = record.Card.transform.position;
        Vector3 endPos = record.SourceSlot.Transform.position;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = t * t * (3f - 2f * t); // Мягкое торможение
            record.Card.transform.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }

        record.Card.transform.position = endPos;

        // 3. Возвращаем карту логически в старый слот
        record.SourceSlot.AcceptCard(record.Card);
        if (mCard != null) mCard.SetAnimating(false);

        // 4. Откатываем очки и фиксируем отмену как новый ход (стандарт Klondike/MonteCarlo)
        RegisterMoveAndStartIfNeeded();
        scoreManager.OnUndo();

        // 5. Пересчитываем затемнение карт
        UpdateLockedCards();

        // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
        isUndoing = false;
        IsInputAllowed = true;
        UpdateUndoButton(); // <--- ДОБАВЛЕНО: Обновляем кнопку, когда блокировки уже сняты!
        UpdateFullUI();
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        var montanaCard = card.GetComponent<MontanaCardController>();
        ICardContainer source = montanaCard != null ? montanaCard.SourceContainer : lastInteractionSource;

        // 1. Сохраняем ход локально
        if (source is MontanaSlot sourceSlot && container is MontanaSlot targetSlot)
        {
            undoStack.Push(new MontanaMoveRecord
            {
                Card = card,
                SourceSlot = sourceSlot,
                TargetSlot = targetSlot
            });
            UpdateUndoButton();
        }

        // 2. УДАЛЕНО: Вызов глобального dragManager

        RegisterMoveAndStartIfNeeded();
        if (source != null) scoreManager.OnCardMove(source, container);

        UpdateFullUI();
        CheckGameState();
    }
    private void UpdateUndoButton()
    {
        bool canUndo = (undoStack.Count > 0 && !isUndoing && IsInputAllowed);
        // Undo All активна, если есть ходы в стеке ИЛИ если мы делали пересдачу
        bool canUndoAll = ((undoStack.Count > 0 || CurrentReshufflesLeft < MaxReshuffles) && !isUndoing && IsInputAllowed);

        if (undoButton != null) undoButton.interactable = canUndo;
        if (undoAllButton != null) undoAllButton.interactable = canUndoAll;
    }

    // Обработчик нажатия на UI кнопку
    public void OnUndoButtonClicked()
    {
        if (undoStack.Count == 0 || isUndoing || !IsInputAllowed) return;
        StartCoroutine(UndoRoutine());
    }
    public void OnUndoAllButtonClicked()
    {
        // Кнопка срабатывает, если есть ходы ИЛИ если мы тратили пересдачу
        bool canUndoAll = (undoStack.Count > 0 || CurrentReshufflesLeft < MaxReshuffles);

        // Если условия не выполнены, идет анимация или заблокирован ввод — игнорируем клик
        if (!canUndoAll || isUndoing || !IsInputAllowed) return;

        StartCoroutine(UndoAllRoutine());
    }
    private IEnumerator UndoAllRoutine()
    {
        isUndoing = true;
        IsInputAllowed = false;

        // ЭТАП 1: Изымаем все карты из их текущих слотов
        foreach (var kvp in initialBoardState)
        {
            var card = kvp.Key;
            var currentSlot = card.GetComponentInParent<MontanaSlot>();
            if (currentSlot != null) currentSlot.RemoveCard(card);
        }

        // ЭТАП 2: Раскладываем их по стартовым слотам
        foreach (var kvp in initialBoardState)
        {
            var card = kvp.Key;
            var initialSlot = kvp.Value;

            initialSlot.AcceptCard(card);

            var mCard = card.GetComponent<MontanaCardController>();
            if (mCard != null) mCard.SetAnimating(false);
        }

        undoStack.Clear();
        CurrentReshufflesLeft = MaxReshuffles;
        scoreManager.ResetScore();

        RegisterMoveAndStartIfNeeded();
        UpdateLockedCards();

        // --- ИСПРАВЛЕНИЕ ЗДЕСЬ (Правильный порядок) ---
        isUndoing = false;
        IsInputAllowed = true;
        UpdateUndoButton(); // <--- ПЕРЕНЕСЕНО СЮДА
        UpdateFullUI();

        yield break;
    }
    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        lastInteractionSource = card.GetComponentInParent<ICardContainer>();
        dragManager?.OnCardClicked(card);
    }

    public void OnCardDoubleClicked(CardController card)
    {
        lastInteractionSource = card.GetComponentInParent<ICardContainer>();
        autoMoveService.OnCardRightClicked(card);
    }

    public void OnCardLongPressed(CardController card) => dragManager?.OnCardLongPressed(card);

    // Вспомогательный поиск контейнеров через Overlap (Геометрию), переписанный для Montana
    public ICardContainer FindNearestContainer(CardController card, Vector2 anchoredPosition, float maxDistance)
    {
        ICardContainer bestContainer = null;
        float bestOverlapArea = 0f;
        Rect cardRect = GetWorldRect(card.rectTransform);

        foreach (var slot in pileManager.Slots)
        {
            Rect slotRect = GetWorldRect(slot.GetComponent<RectTransform>());
            float area = GetIntersectionArea(cardRect, slotRect);

            if (area > bestOverlapArea && slot.CanAccept(card))
            {
                bestOverlapArea = area;
                bestContainer = slot;
            }
        }
        return bestContainer;
    }

    public bool OnDropToBoard(CardController card, Vector2 anchoredPosition) => dragManager?.OnDropToBoard(card, anchoredPosition) ?? false;
    public void OnStockClicked() { }
    public void OnKeyboardPick(CardController card) { }
    public bool IsMatchInProgress() => hasGameStarted;

    #endregion

    #region UI & Helpers
    public void RegisterCardEvents(CardController card)
    {
        // Привязываем карту к текущему режиму игры и менеджеру перетаскивания
        card.CardmodeManager = this;
        card.dragManager = dragManager;
    }
    private void UpdateFullUI()
    {
        if (scoreText != null)
            scoreText.text = scoreManager.CurrentScore.ToString();

        // Берем ходы строго из статистики, как в Клондайке
        if (movesText != null)
        {
            movesText.text = (!hasGameStarted) ? "0" : (StatisticsManager.Instance != null ? StatisticsManager.Instance.GetCurrentMoves().ToString() : "0");
        }

        if (reshufflesText != null)
            reshufflesText.text = CurrentReshufflesLeft.ToString();

        UpdateTimeUI();
    }

    private void UpdateTimeUI()
    {
        if (timeText != null)
        {
            int t = Mathf.FloorToInt(gameTimer);
            timeText.text = string.Format("{0}:{1:00}", t / 60, t % 60);
        }
    }

    // Математика для Overlap прямоугольников
    private Rect GetWorldRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float xMin = corners[0].x, xMax = corners[0].x;
        float yMin = corners[0].y, yMax = corners[0].y;

        for (int i = 1; i < 4; i++)
        {
            if (corners[i].x < xMin) xMin = corners[i].x;
            if (corners[i].x > xMax) xMax = corners[i].x;
            if (corners[i].y < yMin) yMin = corners[i].y;
            if (corners[i].y > yMax) yMax = corners[i].y;
        }
        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private float GetIntersectionArea(Rect r1, Rect r2)
    {
        float xMin = Mathf.Max(r1.x, r2.x);
        float xMax = Mathf.Min(r1.x + r1.width, r2.x + r2.width);
        float yMin = Mathf.Max(r1.y, r2.y);
        float yMax = Mathf.Min(r1.y + r1.height, r2.y + r2.height);

        float w = xMax - xMin, h = yMax - yMin;
        return (w > 0 && h > 0) ? w * h : 0f;
    }

    #endregion
}
public class MontanaMoveRecord
{
    public CardController Card;
    public MontanaSlot SourceSlot;
    public MontanaSlot TargetSlot;
}