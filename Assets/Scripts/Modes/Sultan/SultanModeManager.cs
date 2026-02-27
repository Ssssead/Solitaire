using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SultanModeManager : MonoBehaviour, IModeManager, ICardGameMode
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

    [Header("Services")]
    public SultanPileManager pileManager;
    public SultanDeckManager deckManager;
    public DragManager dragManager;
    public UndoManager undoManager;
    public AnimationService animationService;
    public SultanAutoMoveService autoMoveService;
    public SultanScoreManager scoreManager;

    public bool IsInputAllowed { get; set; } = true;
    public GameType GameType => GameType.Sultan;
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

    private float gameTimer = 0f;
    private bool isTimerRunning = false;

    private void Awake()
    {
        pileManager.Initialize(this);
        dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);
        deckManager.Initialize(this, cardFactory, pileManager);
        autoMoveService.Initialize(this, pileManager, undoManager, animationService, dragLayer);

        if (undoManager != null) undoManager.Initialize(this);

        // --- ⚡ ИНТЕГРАЦИЯ С GAME UI CONTROLLER ⚡ ---
        // Жестко прописываем себя в приватное поле GameUIController, 
        // чтобы он точно знал, у кого спрашивать про подтверждение выхода
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
    }

    private void Update()
    {
        if (isTimerRunning && !hasWonGame)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }
    }

    // --- ⚡ ЗАПИСЬ ПОРАЖЕНИЙ (ABANDONED) ПРИ ВЫХОДЕ ⚡ ---
    private void OnDestroy()
    {
        if (hasGameStarted && !hasWonGame)
        {
            if (StatisticsManager.Instance != null)
            {
                StatisticsManager.Instance.OnGameAbandoned();
            }
        }
    }

    public void StartNewGame()
    {
        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameAbandoned();

        IsInputAllowed = false;
        hasWonGame = false;
        hasGameStarted = false;

        gameTimer = 0f;
        isTimerRunning = false;

        pileManager.ClearAllPiles();
        pileManager.CreatePiles();
        dragManager.RefreshContainers();
        cardFactory.DestroyAllCards();

        if (undoManager != null) undoManager.ResetHistory();
        if (scoreManager != null) scoreManager.ResetScore();

        UpdateFullUI();
        deckManager.DealInitial();
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
        }
    }

    public void RegisterMoveAndStartIfNeeded()
    {
        if (!IsInputAllowed) return;

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true;

            if (StatisticsManager.Instance != null)
            {
                // Используем реальную сложность из настроек вместо жесткой Difficulty.Medium
                Difficulty currentDiff = GameSettings.CurrentDifficulty;
                StatisticsManager.Instance.OnGameStarted(GameName, currentDiff, "Classic");
            }
        }

        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.RegisterMove();
        }

        UpdateFullUI();
        CheckGameState();
    }

    private void UpdateFullUI()
    {
        if (movesText != null)
        {
            movesText.text = (!hasGameStarted) ? "0" : (StatisticsManager.Instance != null ? StatisticsManager.Instance.GetCurrentMoves().ToString() : "0");
        }

        if (scoreText != null)
        {
            int score = scoreManager != null ? scoreManager.CurrentScore : 0;
            scoreText.text = (!hasGameStarted) ? "0" : score.ToString();
        }

        if (!hasGameStarted)
        {
            UpdateTimeUI();
        }
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
        if (!IsInputAllowed) return;
        RegisterMoveAndStartIfNeeded();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.StartTimerIfNotStarted();

        deckManager.DrawFromStock();
    }

    public void OnCardClicked(CardController card) { }
    public void OnCardDoubleClicked(CardController card) { autoMoveService.OnCardRightClicked(card); }
    public void OnCardLongPressed(CardController card) { }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        var sultanCard = card.GetComponent<SultanCardController>();
        ICardContainer source = sultanCard != null ? sultanCard.SourceContainer : null;

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
        }

        RegisterMoveAndStartIfNeeded();
    }

    public void OnUndoAction()
    {
        if (!hasGameStarted) return;
        RegisterMoveAndStartIfNeeded();

        if (scoreManager != null) scoreManager.OnUndo();

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
                    float dist = Vector3.Distance(card.transform.position, container.Transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestContainer = container;
                    }
                }

                if (bestContainer != null && minDist < 150f)
                {
                    card.transform.SetParent(bestContainer.Transform, true);

                    var data = card.GetComponent<CardData>();
                    if (data != null)
                    {
                        if (bestContainer is SultanStockPile) data.SetFaceUp(false, false);
                        if (bestContainer is SultanWastePile) data.SetFaceUp(true, false);
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
                if (cg != null)
                {
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                }
            }
        }

        if (pileManager.WastePile != null)
        {
            int count = pileManager.WastePile.transform.childCount;
            for (int i = 0; i < count; i++)
            {
                var cg = pileManager.WastePile.transform.GetChild(i).GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    bool isTop = (i == count - 1);
                    cg.blocksRaycasts = isTop;
                    cg.interactable = isTop;
                }
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
            if (container is MonoBehaviour monoContainer)
            {
                var type = container.GetType();
                var field = type.GetField("cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                         ?? type.GetField("_cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                if (field != null && field.GetValue(container) is System.Collections.IList list)
                {
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var c = list[i] as CardController;
                        if (c != null && c.transform.parent != monoContainer.transform) list.RemoveAt(i);
                    }
                }
            }
        }

        UpdateFullUI();
        CheckGameState();
    }

    public void OnKeyboardPick(CardController card) { }
    public void RestartGame() { StartNewGame(); }
    public bool IsMatchInProgress() => hasGameStarted;
}