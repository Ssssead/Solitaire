using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PyramidModeManager : MonoBehaviour, ICardGameMode
{
    [Header("Managers")]
    public PyramidDeckManager deckManager;
    public PyramidPileManager pileManager;
    public PyramidScoreManager scoreManager;

    [Header("UI References")]
    [SerializeField] private RectTransform dragLayerRect;
    [SerializeField] private Canvas rootCanvasRef;
    [SerializeField] private Button dealButton;

    [Header("Animation Settings")]
    [SerializeField] private float dealDuration = 0.2f;
    [SerializeField] private float recycleDelay = 0.05f;

    // --- State ---
    private CardController selectedA;
    private int currentRound = 1;
    private int totalRounds = 1;
    private Difficulty currentDifficulty;

    private Stack<PyramidMoveRecord> undoStack = new Stack<PyramidMoveRecord>();

    // --- ICardGameMode Props ---
    public string GameName => "Pyramid";
    public RectTransform DragLayer => dragLayerRect;
    public Canvas RootCanvas => rootCanvasRef;
    public PileManager PileManager => null;
    public AnimationService AnimationService => null;
    public AutoMoveService AutoMoveService => null;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public bool IsInputAllowed { get; set; } = true;

    // --- Initialization ---

    private void Start()
    {
        InitializeGame(Difficulty.Easy, 1);
    }

    public void InitializeGame(Difficulty difficulty, int rounds)
    {
        currentDifficulty = difficulty;
        totalRounds = rounds;
        currentRound = 1;
        undoStack.Clear();

        if (scoreManager) scoreManager.ResetScore();

        if (dealButton != null)
        {
            dealButton.onClick.RemoveAllListeners();
            dealButton.onClick.AddListener(OnDealButtonClicked);
        }

        StatisticsManager.Instance.OnGameStarted("Pyramid", difficulty, rounds.ToString());
        StartRound();
    }

    private void StartRound()
    {
        IsInputAllowed = false;
        selectedA = null;
        undoStack.Clear();
        StartCoroutine(GenerateAndStart());
    }

    private IEnumerator GenerateAndStart()
    {
        Deal deal = null;
        while (deal == null)
        {
            deal = DealCacheSystem.Instance.GetDeal(GameType.Pyramid, currentDifficulty, totalRounds);
            if (deal == null) yield return new WaitForSeconds(0.1f);
        }

        deckManager.InstantiateDeal(deal);
        IsInputAllowed = true;
        UpdateUIState();
    }

    // --- BUTTON LOGIC (ANIMATED) ---

    public void OnDealButtonClicked()
    {
        if (!IsInputAllowed) return;

        DeselectCard();

        // 1. Recycle
        if (pileManager.Stock.IsEmpty)
        {
            if (pileManager.Waste.GetCards().Count > 0)
            {
                StartCoroutine(RecycleRoutine());
            }
            return;
        }

        // 2. Deal
        StartCoroutine(DealRoutine());
    }

    private IEnumerator DealRoutine()
    {
        IsInputAllowed = false;

        CardController card = pileManager.Stock.Draw();

        // Полет из Stock в Waste
        yield return StartCoroutine(MoveCardAnimation(card, deckManager.wasteRoot.position));

        pileManager.Waste.Add(card);

        var move = new PyramidMoveRecord
        {
            Type = PyramidMoveRecord.MoveType.Deal,
            DealtCard = card
        };
        undoStack.Push(move);

        StatisticsManager.Instance.RegisterMove();

        IsInputAllowed = true;
        UpdateUIState();
    }

    private IEnumerator RecycleRoutine()
    {
        IsInputAllowed = false;

        List<CardController> wasteCards = pileManager.Waste.DrawAll();
        wasteCards.Reverse();

        Vector3 targetPos = deckManager.stockRoot.position;

        foreach (var card in wasteCards)
        {
            StartCoroutine(MoveCardToStockAndDisable(card, targetPos));
            yield return new WaitForSeconds(recycleDelay);
        }

        yield return new WaitForSeconds(dealDuration);

        pileManager.Stock.AddRange(wasteCards);

        var move = new PyramidMoveRecord
        {
            Type = PyramidMoveRecord.MoveType.Recycle,
            RecycledCards = new List<CardController>(wasteCards)
        };
        undoStack.Push(move);

        IsInputAllowed = true;
        UpdateUIState();
    }

    private IEnumerator MoveCardToStockAndDisable(CardController card, Vector3 targetPos)
    {
        card.transform.SetParent(dragLayerRect, true);

        // --- ИСПРАВЛЕНИЕ: Ставим true, чтобы карты летели лицом вверх ---
        var data = card.GetComponent<CardData>();
        if (data) data.SetFaceUp(true);

        float elapsed = 0f;
        Vector3 startPos = card.transform.position;

        while (elapsed < dealDuration)
        {
            card.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / dealDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        card.transform.position = targetPos;
        // Временно паркуем в StockRoot до завершения логики
        card.transform.SetParent(deckManager.stockRoot);
        card.transform.localPosition = Vector3.zero;
    }

    private IEnumerator MoveCardAnimation(CardController card, Vector3 targetWorldPos)
    {
        card.transform.SetParent(dragLayerRect, true);

        var data = card.GetComponent<CardData>();
        if (data) data.SetFaceUp(true);

        Vector3 startPos = card.transform.position;
        float elapsed = 0f;

        while (elapsed < dealDuration)
        {
            float t = elapsed / dealDuration;
            card.transform.position = Vector3.Lerp(startPos, targetWorldPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        card.transform.position = targetWorldPos;
    }

    // --- CLICK LOGIC ---

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        if (!IsInteractable(card)) return;

        if (card.cardModel.rank == 13)
        {
            RemoveCards(card, null);
            return;
        }

        if (selectedA == null)
        {
            SelectCard(card);
        }
        else if (selectedA == card)
        {
            DeselectCard();
        }
        else
        {
            if (card.cardModel.rank + selectedA.cardModel.rank == 13)
            {
                RemoveCards(selectedA, card);
            }
            else
            {
                DeselectCard();
                SelectCard(card);
            }
        }
    }

    public void OnStockClicked(CardController card)
    {
        if (pileManager.Stock.HasCard(card))
        {
            if (card == pileManager.Stock.Peek())
                OnCardClicked(card);
        }
        else if (pileManager.Waste.HasCard(card))
        {
            if (card == pileManager.Waste.TopCard())
                OnCardClicked(card);
        }
    }
    public void OnStockClicked() { }

    // --- GAME ACTIONS ---

    private void RemoveCards(CardController a, CardController b)
    {
        DeselectCard();

        var move = new PyramidMoveRecord
        {
            Type = (b == null) ? PyramidMoveRecord.MoveType.RemoveKing : PyramidMoveRecord.MoveType.RemovePair
        };
        SaveCardInfo(move, a);
        if (b != null) SaveCardInfo(move, b);
        undoStack.Push(move);

        pileManager.RemoveCardFromSystem(a);
        if (b != null) pileManager.RemoveCardFromSystem(b);

        a.gameObject.SetActive(false);
        if (b != null) b.gameObject.SetActive(false);

        if (scoreManager) scoreManager.AddPoints(b == null ? 5 : 10);
        StatisticsManager.Instance.RegisterMove();

        UpdateUIState();
        CheckWin();
    }

    private void SaveCardInfo(PyramidMoveRecord move, CardController c)
    {
        var info = new PyramidMoveRecord.RemovedCardInfo { Card = c };
        var slot = pileManager.TableauSlots.Find(s => s.Card == c);

        if (slot != null) info.SourceSlot = slot;
        else if (pileManager.Stock.HasCard(c)) info.WasInStock = true;
        else if (pileManager.Waste.HasCard(c)) info.WasInWaste = true;

        move.RemovedCards.Add(info);
    }

    // --- UNDO ---

    public void OnUndoAction()
    {
        if (undoStack.Count == 0 || !IsInputAllowed) return;

        var move = undoStack.Pop();
        DeselectCard();

        if (move.Type == PyramidMoveRecord.MoveType.Deal)
        {
            CardController cDeal = move.DealtCard;
            pileManager.Waste.Remove(cDeal);
            pileManager.Stock.Add(cDeal);

            // Убеждаемся, что при возврате в Stock она FaceUp
            cDeal.GetComponent<CardData>().SetFaceUp(true);
        }
        else if (move.Type == PyramidMoveRecord.MoveType.Recycle)
        {
            pileManager.Stock.Clear();
            var cardsToWaste = new List<CardController>(move.RecycledCards);
            cardsToWaste.Reverse();

            foreach (var c in cardsToWaste)
            {
                c.GetComponent<CardData>().SetFaceUp(true);
                pileManager.Waste.Add(c);
            }
        }
        else
        {
            foreach (var info in move.RemovedCards)
            {
                var c = info.Card;
                c.gameObject.SetActive(true);

                if (info.SourceSlot != null)
                {
                    info.SourceSlot.Card = c;
                    c.transform.SetParent(info.SourceSlot.transform);
                    c.transform.localPosition = Vector3.zero;
                }
                else if (info.WasInWaste)
                {
                    pileManager.Waste.Add(c);
                }
                else if (info.WasInStock)
                {
                    pileManager.Stock.Add(c);
                }
                c.GetComponent<CardData>().image.color = Color.white;
            }
            if (scoreManager) scoreManager.AddPoints(move.Type == PyramidMoveRecord.MoveType.RemoveKing ? -5 : -10);
        }

        UpdateUIState();
    }

    // --- HELPERS ---

    private void UpdateUIState()
    {
        pileManager.UpdateLocks();
        if (dealButton != null)
        {
            bool canDeal = !pileManager.Stock.IsEmpty;
            bool canRecycle = pileManager.Stock.IsEmpty && !pileManager.Waste.GetCards().Count.Equals(0);
            dealButton.interactable = canDeal || canRecycle;
        }
    }

    private void SelectCard(CardController c)
    {
        selectedA = c;
        var data = c.GetComponent<CardData>();
        if (data && data.image) data.image.color = Color.yellow;
    }

    private void DeselectCard()
    {
        if (selectedA != null)
        {
            var data = selectedA.GetComponent<CardData>();
            if (data && data.image) data.image.color = Color.white;
            selectedA = null;
        }
    }

    private bool IsInteractable(CardController c) =>
        c.canvasGroup != null && c.canvasGroup.interactable && c.gameObject.activeInHierarchy;

    private void CheckWin()
    {
        if (pileManager.IsPyramidCleared())
        {
            if (currentRound < totalRounds) { currentRound++; StartRound(); }
            else StatisticsManager.Instance.OnGameWon(scoreManager ? scoreManager.Score : 0);
        }
    }

    public void RestartGame() { StatisticsManager.Instance.OnGameAbandoned(); InitializeGame(currentDifficulty, totalRounds); }
    public void CheckGameState() { }
    public void OnCardDoubleClicked(CardController card) => OnCardClicked(card);
}