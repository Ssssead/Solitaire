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
    public PyramidAnimationManager animManager;

    [Header("UI References")]
    [SerializeField] private Button dealButton;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button undoAllButton;

    [Header("Animation Settings")]
    [SerializeField] private float dealAnimDuration = 0.3f;
    [SerializeField] private float removeAnimDuration = 0.5f;
    [SerializeField] private float recycleDelay = 0.05f;

    private CardController selectedA;
    private int currentRound = 1;
    private int totalRounds = 1;
    private Difficulty currentDifficulty;
    private Stack<PyramidMoveRecord> undoStack = new Stack<PyramidMoveRecord>();

    public string GameName => "Pyramid";
    public RectTransform DragLayer => animManager ? animManager.dragLayerRect : null;
    public Canvas RootCanvas => null;
    public PileManager PileManager => null;
    public AnimationService AnimationService => null;
    public AutoMoveService AutoMoveService => null;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public bool IsInputAllowed { get; set; } = true;

    private void Start()
    {
        if (!animManager) animManager = FindObjectOfType<PyramidAnimationManager>();
        InitializeGame(GameSettings.CurrentDifficulty, GameSettings.RoundsCount);
    }

    public void InitializeGame(Difficulty difficulty, int rounds)
    {
        currentDifficulty = difficulty;
        totalRounds = rounds;
        currentRound = 1;
        undoStack.Clear();
        if (scoreManager) scoreManager.ResetScore();
        SetupButtons();
        StatisticsManager.Instance.OnGameStarted("Pyramid", difficulty, rounds.ToString());
        StartRound(true);
    }

    private void SetupButtons()
    {
        if (dealButton != null) { dealButton.onClick.RemoveAllListeners(); dealButton.onClick.AddListener(OnDealButtonClicked); }
        var globalUndo = FindObjectOfType<UndoManager>();
        if (globalUndo != null) { if (undoButton == null) undoButton = globalUndo.undoButton; if (undoAllButton == null) undoAllButton = globalUndo.undoAllButton; }
        if (undoButton != null) { undoButton.onClick.RemoveAllListeners(); undoButton.onClick.AddListener(OnUndoAction); }
        if (undoAllButton != null) { undoAllButton.onClick.RemoveAllListeners(); undoAllButton.onClick.AddListener(OnUndoAllAction); }
    }

    private void StartRound(bool isFirstRound)
    {
        IsInputAllowed = false;
        selectedA = null;
        undoStack.Clear();
        StartCoroutine(GenerateAndStartSequence(isFirstRound));
    }

    private IEnumerator GenerateAndStartSequence(bool isFirstRound)
    {
        if (!isFirstRound)
        {
            List<CardController> leftovers = new List<CardController>();
            if (pileManager.Stock != null) while (!pileManager.Stock.IsEmpty) leftovers.Add(pileManager.Stock.Draw());
            if (pileManager.Waste != null) leftovers.AddRange(pileManager.Waste.DrawAll());

            if (leftovers.Count > 0 && deckManager.rightFoundation != null && animManager != null)
                yield return StartCoroutine(animManager.ClearRemainingCards(leftovers, deckManager.rightFoundation));

            deckManager.ClearBoard();
        }

        Deal deal = null; float timeout = 2f;
        while (deal == null && timeout > 0) { deal = DealCacheSystem.Instance.GetDeal(GameType.Pyramid, currentDifficulty, totalRounds); if (deal == null) { yield return new WaitForSeconds(0.1f); timeout -= 0.1f; } }
        if (deal == null) yield break;

        var cardsToAnimate = deckManager.InstantiateDeal(deal);

        if (!isFirstRound && animManager != null) yield return StartCoroutine(animManager.PlayNewRoundEntry(deckManager.stockRoot));
        if (animManager != null) yield return StartCoroutine(animManager.PlayDealAnimation(cardsToAnimate));
        else pileManager.UpdateLocks();

        IsInputAllowed = true; UpdateUIState();
    }

    // --- GAMEPLAY ACTIONS ---

    public void OnDealButtonClicked()
    {
        if (!IsInputAllowed) return;
        DeselectCard();
        if (pileManager.Stock.IsEmpty)
        {
            if (pileManager.Waste.GetCards().Count > 0) StartCoroutine(RecycleRoutine());
            return;
        }
        StartCoroutine(DealRoutine());
    }

    private IEnumerator DealRoutine()
    {
        IsInputAllowed = false;
        CardController card = pileManager.Stock.Draw();

        yield return StartCoroutine(animManager.MoveCardLinear(card, deckManager.wasteRoot.position, dealAnimDuration, () => {
            pileManager.Waste.Add(card);
        }));

        var move = new PyramidMoveRecord { Type = PyramidMoveRecord.MoveType.Deal, DealtCard = card };
        undoStack.Push(move);
        StatisticsManager.Instance.RegisterMove();
        IsInputAllowed = true; UpdateUIState();
    }

    private IEnumerator RecycleRoutine()
    {
        IsInputAllowed = false;
        List<CardController> wasteCards = pileManager.Waste.DrawAll();
        wasteCards.Reverse();
        Vector3 targetPos = deckManager.stockRoot.position;

        foreach (var card in wasteCards)
        {
            // Анимация полета в сток
            StartCoroutine(animManager.MoveCardToStockAndDisable(card, targetPos, dealAnimDuration));
            yield return new WaitForSeconds(recycleDelay);
        }

        yield return new WaitForSeconds(dealAnimDuration);
        pileManager.Stock.AddRange(wasteCards);

        var move = new PyramidMoveRecord { Type = PyramidMoveRecord.MoveType.Recycle, RecycledCards = new List<CardController>(wasteCards) };
        undoStack.Push(move);
        IsInputAllowed = true; UpdateUIState();
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        if (!IsInteractable(card)) return;
        if (card.cardModel.rank == 13) { StartCoroutine(RemoveSequence(card, null)); return; }
        if (selectedA == null) { SelectCard(card); }
        else if (selectedA == card) { DeselectCard(); }
        else
        {
            if (card.cardModel.rank + selectedA.cardModel.rank == 13) StartCoroutine(RemoveSequence(selectedA, card));
            else { DeselectCard(); SelectCard(card); }
        }
    }
    public void OnStockClicked(CardController card) { if (pileManager.Stock.HasCard(card)) { if (card == pileManager.Stock.Peek()) OnCardClicked(card); } else if (pileManager.Waste.HasCard(card)) { if (card == pileManager.Waste.TopCard()) OnCardClicked(card); } }
    public void OnStockClicked() { }

    private IEnumerator RemoveSequence(CardController cardA, CardController cardB)
    {
        IsInputAllowed = false; DeselectCard();
        var move = new PyramidMoveRecord { Type = (cardB == null) ? PyramidMoveRecord.MoveType.RemoveKing : PyramidMoveRecord.MoveType.RemovePair };
        Transform targetA = null; Transform targetB = null;
        if (cardB == null) targetA = GetClosestFoundation(cardA);
        else
        {
            if (deckManager.leftFoundation != null && deckManager.rightFoundation != null)
            {
                if (cardA.transform.position.x <= cardB.transform.position.x) { targetA = deckManager.leftFoundation; targetB = deckManager.rightFoundation; }
                else { targetA = deckManager.rightFoundation; targetB = deckManager.leftFoundation; }
            }
            else { targetA = GetClosestFoundation(cardA); targetB = GetClosestFoundation(cardB); }
        }
        SaveCardInfo(move, cardA, targetA);
        if (cardB != null) SaveCardInfo(move, cardB, targetB);
        undoStack.Push(move);
        pileManager.RemoveCardFromSystem(cardA);
        if (cardB != null) pileManager.RemoveCardFromSystem(cardB);
        pileManager.UpdateLocks();

        StartCoroutine(animManager.AnimateRemoveBallistic(cardA, targetA, () => { if (cardA) { cardA.transform.SetParent(targetA); cardA.gameObject.SetActive(false); } }));
        if (cardB != null) StartCoroutine(animManager.AnimateRemoveBallistic(cardB, targetB, () => { if (cardB) { cardB.transform.SetParent(targetB); cardB.gameObject.SetActive(false); } }));

        yield return new WaitForSeconds(0.5f);

        if (scoreManager) scoreManager.AddPoints(cardB == null ? 5 : 10);
        StatisticsManager.Instance.RegisterMove();
        if (pileManager.IsPyramidCleared())
        {
            if (currentRound < totalRounds) { currentRound++; StartCoroutine(GenerateAndStartSequence(false)); }
            else StatisticsManager.Instance.OnGameWon(scoreManager ? scoreManager.Score : 0);
        }
        else { IsInputAllowed = true; UpdateUIState(); }
    }

    // --- UNDO ---
    public void OnUndoAction() { if (undoStack.Count == 0 || !IsInputAllowed) return; StartCoroutine(UndoSequence(undoStack.Pop(), false)); }
    public void OnUndoAllAction() { if (undoStack.Count == 0 || !IsInputAllowed) return; StartCoroutine(UndoAllRoutine()); }
    private IEnumerator UndoAllRoutine() { IsInputAllowed = false; DeselectCard(); while (undoStack.Count > 0) { yield return StartCoroutine(UndoSequence(undoStack.Pop(), true)); yield return new WaitForSeconds(0.05f); } IsInputAllowed = true; UpdateUIState(); }

    private IEnumerator UndoSequence(PyramidMoveRecord move, bool immediate)
    {
        if (!immediate) IsInputAllowed = false;
        DeselectCard();

        if (move.Type == PyramidMoveRecord.MoveType.Deal)
        {
            // --- UNDO DEAL: Waste -> Stock ---
            CardController cDeal = move.DealtCard;
            pileManager.Waste.Remove(cDeal);

            if (!immediate)
            {
                yield return StartCoroutine(animManager.MoveCardLinear(cDeal, deckManager.stockRoot.position, dealAnimDuration, () => {
                    pileManager.Stock.Add(cDeal);
                }));
            }
            else
            {
                pileManager.Stock.Add(cDeal);
            }
            cDeal.GetComponent<CardData>().SetFaceUp(true);
        }
        else if (move.Type == PyramidMoveRecord.MoveType.Recycle)
        {
            // --- UNDO RECYCLE: Stock -> Waste ---
            // Здесь добавлена анимация возврата карт!

            pileManager.Stock.Clear();
            var cardsToWaste = new List<CardController>(move.RecycledCards);
            cardsToWaste.Reverse();

            foreach (var c in cardsToWaste)
            {
                if (!immediate)
                {
                    // Подготовка позиции перед полетом
                    c.transform.position = deckManager.stockRoot.position;

                    // Анимация
                    StartCoroutine(animManager.MoveCardLinear(c, deckManager.wasteRoot.position, dealAnimDuration, () => {
                        pileManager.Waste.Add(c);
                    }));

                    // Задержка между картами для красоты
                    yield return new WaitForSeconds(recycleDelay);
                }
                else
                {
                    c.GetComponent<CardData>().SetFaceUp(true);
                    pileManager.Waste.Add(c);
                }
            }

            // Если была анимация, ждем чуть дольше, чтобы последняя карта долетела
            if (!immediate) yield return new WaitForSeconds(dealAnimDuration);
        }
        else
        {
            // Undo Remove
            List<Coroutine> activeAnims = new List<Coroutine>();
            foreach (var info in move.RemovedCards)
            {
                var c = info.Card;
                Transform startT = info.WentToLeftFoundation ? deckManager.leftFoundation : deckManager.rightFoundation;
                if (startT == null) startT = deckManager.stockRoot;

                Vector3 targetPos = Vector3.zero; Transform targetParent = null;
                if (info.SourceSlot != null) { targetPos = info.SourceSlot.transform.position; targetParent = info.SourceSlot.transform; info.SourceSlot.Card = c; }
                else if (info.WasInWaste) { targetPos = deckManager.wasteRoot.position; targetParent = deckManager.wasteRoot; pileManager.Waste.Add(c); }
                else if (info.WasInStock) { targetPos = deckManager.stockRoot.position; targetParent = deckManager.stockRoot; pileManager.Stock.Add(c); }

                if (immediate)
                {
                    c.gameObject.SetActive(true);
                    c.transform.SetParent(targetParent); c.transform.localPosition = Vector3.zero;
                    if (targetParent == deckManager.wasteRoot || targetParent == deckManager.stockRoot) c.transform.SetAsLastSibling();
                    if (c.canvasGroup) c.canvasGroup.interactable = true;
                    c.GetComponent<CardData>().image.color = Color.white;
                }
                else
                {
                    activeAnims.Add(StartCoroutine(animManager.ReturnCardFromFoundation(c, startT.position, targetPos, targetParent, removeAnimDuration)));
                }
            }
            if (!immediate) foreach (var anim in activeAnims) yield return anim;
            if (scoreManager) scoreManager.AddPoints(move.Type == PyramidMoveRecord.MoveType.RemoveKing ? -5 : -10);
        }

        if (!immediate) IsInputAllowed = true;
        UpdateUIState();
    }

    private Transform GetClosestFoundation(CardController card) { if (!deckManager.leftFoundation || !deckManager.rightFoundation) return deckManager.stockRoot; float d1 = Vector3.Distance(card.transform.position, deckManager.leftFoundation.position); float d2 = Vector3.Distance(card.transform.position, deckManager.rightFoundation.position); return d1 < d2 ? deckManager.leftFoundation : deckManager.rightFoundation; }
    private void SaveCardInfo(PyramidMoveRecord move, CardController c, Transform targetFoundation) { var info = new PyramidMoveRecord.RemovedCardInfo { Card = c }; var slot = pileManager.TableauSlots.Find(s => s.Card == c); if (slot != null) info.SourceSlot = slot; else if (pileManager.Stock.HasCard(c)) info.WasInStock = true; else if (pileManager.Waste.HasCard(c)) info.WasInWaste = true; info.WentToLeftFoundation = (targetFoundation == deckManager.leftFoundation); move.RemovedCards.Add(info); }
    private void UpdateUIState() { pileManager.UpdateLocks(); if (dealButton != null) dealButton.interactable = (!pileManager.Stock.IsEmpty || !pileManager.Waste.GetCards().Count.Equals(0)) && IsInputAllowed; bool hasHistory = undoStack.Count > 0 && IsInputAllowed; if (undoButton != null) undoButton.interactable = hasHistory; if (undoAllButton != null) undoAllButton.interactable = hasHistory; }
    private void SelectCard(CardController c) { selectedA = c; deckManager.SetCardHighlight(c, true); }
    private void DeselectCard() { if (selectedA != null) { deckManager.SetCardHighlight(selectedA, false); selectedA = null; } }
    private bool IsInteractable(CardController c) => c.canvasGroup != null && c.canvasGroup.interactable && c.gameObject.activeInHierarchy;
    private void CheckWin() { }
    public void RestartGame() { StatisticsManager.Instance.OnGameAbandoned(); InitializeGame(currentDifficulty, totalRounds); }
    public void CheckGameState() { }
    public void OnCardDoubleClicked(CardController card) => OnCardClicked(card);
}