using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections;

public enum YukonVariant { Classic, Russian }

public class YukonModeManager : MonoBehaviour, IModeManager, ICardGameMode
{
    [Header("Core Setup")]
    public YukonVariant CurrentVariant = YukonVariant.Classic;
    public Difficulty currentDifficulty = Difficulty.Medium;

    [Header("References")]
    public YukonDeckManager deckManager;
    public Canvas rootCanvas;
    public RectTransform dragLayer;
    public ScoreManager scoreManager;
    public UndoManager undoManager;
    public GameUIController gameUI; // Ссылка на контроллер интерфейса

    [Header("Containers")]
    public Transform tableauSlotsParent;
    public Transform foundationSlotsParent;

    [Header("UI & HUD")]
    public TMP_Text movesText;
    public TMP_Text scoreText;
    public TMP_Text timeText;
    public Button autoWinButton;

    [HideInInspector] public List<YukonTableauPile> tableaus = new List<YukonTableauPile>();
    [HideInInspector] public List<FoundationPile> foundations = new List<FoundationPile>();

    public string GameName => "Yukon";
    public RectTransform DragLayer => dragLayer;
    public Canvas RootCanvas => rootCanvas;
    public bool IsInputAllowed { get; set; } = true;
    public float TableauVerticalGap => 35f;
    public AnimationService AnimationService => null;
    public PileManager PileManager => null;
    public AutoMoveService AutoMoveService => null;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public GameType GameType => GameType.Yukon;

    // --- СТАТИСТИКА И СОСТОЯНИЕ ИГРЫ ---
    private bool hasWonGame = false;
    private bool hasGameStarted = false;
    private float gameTimer = 0f;
    private bool isTimerRunning = false;

    private IEnumerator Start()
    {
        if (undoManager == null) undoManager = FindObjectOfType<UndoManager>();
        if (undoManager != null) undoManager.Initialize(this);

        // Ожидаем конца кадра, чтобы UI и CanvasScaler приняли свои финальные размеры.
        // Это полностью решает баг со сжатыми картами при старте.
        yield return new WaitForEndOfFrame();

        InitializeMode();
    }
    public bool IsMatchInProgress()
    {
        return hasGameStarted;
    }
    private void Update()
    {
        // Обновление локального таймера
        if (isTimerRunning && !hasWonGame)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }
    }

    private void OnDestroy()
    {
        // Засчитываем поражение при выходе, если игра была начата
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }
    }

    public void InitializeMode()
    {
        currentDifficulty = GameSettings.CurrentDifficulty;
        CurrentVariant = GameSettings.YukonRussian ? YukonVariant.Russian : YukonVariant.Classic;

        tableaus.Clear();
        if (tableauSlotsParent)
        {
            tableaus.AddRange(tableauSlotsParent.GetComponentsInChildren<YukonTableauPile>());
            tableaus.Sort((a, b) => a.name.CompareTo(b.name));
        }

        foundations.Clear();
        if (foundationSlotsParent)
        {
            foundations.AddRange(foundationSlotsParent.GetComponentsInChildren<FoundationPile>());
        }

        StartNewGame();
    }

    public void StartNewGame()
    {
        // Засчитываем поражение, если мы рестартим начатую игру
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }

        IsInputAllowed = false;

        // Сброс состояний
        hasWonGame = false;
        hasGameStarted = false;
        gameTimer = 0f;
        isTimerRunning = false;

        if (scoreManager) scoreManager.ResetScore();
        if (autoWinButton) autoWinButton.gameObject.SetActive(false);
        if (undoManager) undoManager.ResetHistory();

        UpdateFullUI(); // Обнуляем UI

        foreach (var t in tableaus) t.Clear();
        foreach (var f in foundations)
        {
            for (int i = f.transform.childCount - 1; i >= 0; i--) Destroy(f.transform.GetChild(i).gameObject);
        }

        if (DealCacheSystem.Instance)
        {
            var deal = DealCacheSystem.Instance.GetDeal(GameType.Yukon, currentDifficulty, 0);
            if (deal != null)
            {
                deckManager.LoadDeal(deal);
                IsInputAllowed = true;
            }
        }
    }

    public void RestartGame() => StartNewGame();

    // --- ЛОГИКА РЕГИСТРАЦИИ ХОДОВ ---
    public void RegisterMoveAndStartIfNeeded()
    {
        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true; // Запускаем таймер при первом ходе

            if (StatisticsManager.Instance != null)
            {
                string variant = CurrentVariant == YukonVariant.Russian ? "Russian" : "Classic";
                StatisticsManager.Instance.OnGameStarted("Yukon", currentDifficulty, variant);
            }
        }

        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.RegisterMove();
        }
    }

    // --- UI ОБНОВЛЕНИЕ ---
    private void UpdateFullUI()
    {
        if (movesText != null)
        {
            if (!hasGameStarted) movesText.text = "0";
            else if (StatisticsManager.Instance != null) movesText.text = $"{StatisticsManager.Instance.GetCurrentMoves()}";
            else movesText.text = "0";
        }

        if (scoreText != null)
        {
            if (!hasGameStarted) scoreText.text = "0";
            else
            {
                int score = scoreManager != null ? scoreManager.CurrentScore : 0;
                scoreText.text = $"{score}";
            }
        }

        if (!hasGameStarted) UpdateTimeUI();
    }

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

    public ICardContainer FindNearestContainer(CardController card, Vector2 unusedPos = default, float unusedDist = 0)
    {
        return FindNearestContainer(card, card.transform.parent);
    }

    public ICardContainer FindNearestContainer(CardController card, Transform ignoreSource)
    {
        ICardContainer bestContainer = null;
        float maxOverlapArea = 0f;
        Rect cardRect = GetWorldRect(card.rectTransform);

        List<MonoBehaviour> candidates = new List<MonoBehaviour>();
        candidates.AddRange(tableaus);
        candidates.AddRange(foundations);

        foreach (var candidate in candidates)
        {
            if (card.transform.IsChildOf(candidate.transform)) continue;
            if (candidate.transform == ignoreSource) continue;

            RectTransform targetRT = candidate.transform as RectTransform;
            if (candidate is YukonTableauPile tab && tab.cards.Count > 0)
            {
                var lastCard = tab.cards[tab.cards.Count - 1];
                if (lastCard != null) targetRT = lastCard.rectTransform;
            }

            float area = GetIntersectionArea(cardRect, GetWorldRect(targetRT));
            if (area > 0 && area > maxOverlapArea)
            {
                ICardContainer container = candidate as ICardContainer;
                if (container != null && container.CanAccept(card))
                {
                    maxOverlapArea = area;
                    bestContainer = container;
                }
            }
        }
        return bestContainer;
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        // 1. Запись в UndoManager
        if (undoManager != null)
        {
            var yCard = card as YukonCardController;
            ICardContainer source = yCard?.SourceContainer;

            if (source != null)
            {
                List<CardController> movedCards = yCard.GetMovedCards();
                List<Transform> parents = new List<Transform>();
                List<Vector3> positions = yCard.GetSavedLocalPositions();
                List<int> siblings = new List<int>();
                int startIndex = yCard.OriginalSiblingIndex;

                for (int i = 0; i < movedCards.Count; i++)
                {
                    parents.Add(source.Transform);
                    siblings.Add(startIndex + i);
                    if (positions == null || positions.Count <= i) positions.Add(Vector3.zero);
                }

                undoManager.RecordMove(movedCards, source, container, parents, positions, siblings);
            }
        }

        // 2. Логика открытия карт
        foreach (var t in tableaus)
        {
            t.ForceUpdateFromTransform();

            if (t.cards.Count > 0)
            {
                int topIndex = t.cards.Count - 1;

                if (t.faceUp.Count > topIndex && !t.faceUp[topIndex])
                {
                    t.CheckAndFlipTop();
                    if (undoManager != null) undoManager.RecordFlipInSource(topIndex);
                }
            }
        }

        // 3. РЕГИСТРАЦИЯ ХОДА ДЛЯ СТАТИСТИКИ
        RegisterMoveAndStartIfNeeded();

        // 4. Очки
        if (scoreManager)
        {
            if (container is FoundationPile) scoreManager.OnCardMove(container);
            else scoreManager.OnCardMove(null);
        }

        CheckGameState();
        UpdateFullUI();
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        if (card.transform.parent != null)
        {
            int myIndex = card.transform.GetSiblingIndex();
            int totalChildren = card.transform.parent.childCount;
            if (myIndex != totalChildren - 1) return;
        }

        foreach (var f in foundations)
        {
            if (f.CanAccept(card))
            {
                if (card is YukonCardController yCard)
                {
                    var source = card.transform.parent.GetComponent<ICardContainer>();
                    yCard.SetSourceForAutoMove(source);
                    yCard.AnimateToTarget(f, f.transform.position);
                }
                break;
            }
        }
    }

    public void OnUndoAction()
    {
        // --- ИСПРАВЛЕНИЕ: Предотвращаем срабатывание при старте игры ---
        // Когда StartNewGame очищает стек Undo, вызывается этот метод.
        // Если игра еще не начата, мы просто выходим, не начисляя ход.
        if (!hasGameStarted) return;
        // ---------------------------------------------------------------

        RegisterMoveAndStartIfNeeded();

        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);
        if (scoreManager != null) scoreManager.OnUndo();

        UpdateFullUI();
    }

    public void CheckGameState()
    {
        if (hasWonGame) return;

        int win = foundations.Count(f => f.Count == 13);
        if (win == 4)
        {
            Debug.Log("Yukon Won!");
            hasWonGame = true;
            IsInputAllowed = false;
            isTimerRunning = false;

            if (autoWinButton) autoWinButton.gameObject.SetActive(false);

            // ОТПРАВКА СТАТИСТИКИ ПОБЕДЫ
            if (StatisticsManager.Instance != null)
            {
                int finalMoves = StatisticsManager.Instance.GetCurrentMoves();
                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;

                StatisticsManager.Instance.OnGameWon(finalScore);

                if (gameUI != null) gameUI.OnGameWon(finalMoves);
            }
        }
    }

    private Rect GetWorldRect(RectTransform rt) { Vector3[] c = new Vector3[4]; rt.GetWorldCorners(c); return new Rect(c[0].x, c[0].y, Mathf.Abs(c[2].x - c[0].x), Mathf.Abs(c[2].y - c[0].y)); }
    private float GetIntersectionArea(Rect r1, Rect r2) { float w = Mathf.Min(r1.xMax, r2.xMax) - Mathf.Max(r1.xMin, r2.xMin); float h = Mathf.Min(r1.yMax, r2.yMax) - Mathf.Max(r1.yMin, r2.yMin); return (w > 0 && h > 0) ? w * h : 0f; }

    public bool OnDropToBoard(CardController c, Vector2 p) => false;
    public void OnCardClicked(CardController c) { }
    public void OnCardLongPressed(CardController c) { }
    public void OnKeyboardPick(CardController c) { }
    public void OnStockClicked() { }
}