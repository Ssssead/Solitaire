using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PyramidModeManager : MonoBehaviour, ICardGameMode
{
    [Header("Managers")]
    public PyramidDeckManager deckManager;
    public PyramidPileManager pileManager;
    public PyramidScoreManager scoreManager;
    public PyramidAnimationManager animManager;

    private GameUIController gameUI;

    [Header("UI References")]
    [SerializeField] private Button dealButton;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button undoAllButton;

    [Header("HUD (On Scene Texts)")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text movesText;

    [Header("Animation Settings")]
    [SerializeField] private float dealAnimDuration = 0.3f;
    [SerializeField] private float removeAnimDuration = 0.5f;
    [SerializeField] private float recycleDelay = 0.05f;

    [Header("Game Rules")]
    [SerializeField] private int maxRecycles = 2;

    private CardController selectedA;
    private int currentRound = 1;
    private int totalRounds = 1;
    private Difficulty currentDifficulty;
    private Stack<PyramidMoveRecord> undoStack = new Stack<PyramidMoveRecord>();
    private int recyclesRemaining;
    private Coroutine defeatRoutine;

    // --- State Flags ---
    private bool _hasGameStarted = false; // Был ли сделан первый ход?
    private bool _isGameWon = false;      // Была ли игра выиграна?

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
        gameUI = FindObjectOfType<GameUIController>();

        InitializeGame(GameSettings.CurrentDifficulty, GameSettings.RoundsCount);
    }

    public void InitializeGame(Difficulty difficulty, int rounds)
    {
        currentDifficulty = difficulty;
        totalRounds = rounds;
        currentRound = 1;
        undoStack.Clear();
        recyclesRemaining = maxRecycles;
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);

        // Сброс флагов
        _hasGameStarted = false;
        _isGameWon = false;

        if (scoreManager) scoreManager.ResetScore();

        SetupButtons();

        // ВАЖНО: Мы НЕ вызываем OnGameStarted здесь, чтобы не накручивать счетчик игр без ходов.
        // Мы сохраняем параметры, но вызовем старт позже.

        StartRound(true);
    }

    // --- НОВЫЙ МЕТОД: Регистрирует начало игры при первом действии ---
    private void EnsureGameStarted()
    {
        if (!_hasGameStarted)
        {
            _hasGameStarted = true;
            StatisticsManager.Instance.OnGameStarted("Pyramid", currentDifficulty, totalRounds.ToString());
        }
    }

    // --- НОВЫЙ МЕТОД: Обработка выхода со сцены (меню/закрытие) ---
    private void OnDestroy()
    {
        // Если ходы были сделаны, но игра не выиграна -> Засчитываем поражение
        if (_hasGameStarted && !_isGameWon)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }
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
        recyclesRemaining = maxRecycles;
        if (pileManager != null) pileManager.ResetRowFlags();
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);

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
                yield return StartCoroutine(animManager.ClearRemainingCards(leftovers, deckManager.rightFoundation, removeAnimDuration));
            deckManager.ClearBoard();
        }

        Deal deal = null; float timeout = 2f;
        while (deal == null && timeout > 0) { deal = DealCacheSystem.Instance.GetDeal(GameType.Pyramid, currentDifficulty, totalRounds); if (deal == null) { yield return new WaitForSeconds(0.1f); timeout -= 0.1f; } }
        if (deal == null) yield break;

        var cardsToAnimate = deckManager.InstantiateDeal(deal);

        if (!isFirstRound && animManager != null) yield return StartCoroutine(animManager.PlayNewRoundEntry(deckManager.stockRoot));
        if (animManager != null) yield return StartCoroutine(animManager.PlayDealAnimation(cardsToAnimate));
        else pileManager.UpdateLocks();

        IsInputAllowed = true;
        UpdateUIState();
        CheckGameState();
    }

    // --- GAMEPLAY ACTIONS ---

    public void OnDealButtonClicked()
    {
        if (!IsInputAllowed) return;
        DeselectCard();

        // Регистрируем старт игры перед любым действием
        EnsureGameStarted();

        if (pileManager.Stock.IsEmpty)
        {
            if (pileManager.Waste.GetCards().Count > 0)
            {
                if (recyclesRemaining > 0) { recyclesRemaining--; StartCoroutine(RecycleRoutine()); }
            }
            return;
        }
        StartCoroutine(DealRoutine());
    }

    private IEnumerator DealRoutine()
    {
        IsInputAllowed = false;
        CardController card = pileManager.Stock.Draw();
        yield return StartCoroutine(animManager.MoveCardLinear(card, deckManager.wasteRoot.position, dealAnimDuration, () => { pileManager.Waste.Add(card); }));
        var move = new PyramidMoveRecord { Type = PyramidMoveRecord.MoveType.Deal, DealtCard = card };
        undoStack.Push(move);
        StatisticsManager.Instance.RegisterMove();
        IsInputAllowed = true;
        UpdateUIState();
        CheckGameState();
    }

    private IEnumerator RecycleRoutine()
    {
        IsInputAllowed = false;
        List<CardController> wasteCards = pileManager.Waste.DrawAll();
        wasteCards.Reverse();
        Vector3 targetPos = deckManager.stockRoot.position;
        foreach (var card in wasteCards) { StartCoroutine(animManager.MoveCardToStockAndDisable(card, targetPos, dealAnimDuration)); yield return new WaitForSeconds(recycleDelay); }
        yield return new WaitForSeconds(dealAnimDuration);
        pileManager.Stock.AddRange(wasteCards);
        var move = new PyramidMoveRecord { Type = PyramidMoveRecord.MoveType.Recycle, RecycledCards = new List<CardController>(wasteCards) };
        undoStack.Push(move);
        IsInputAllowed = true;
        UpdateUIState();
        CheckGameState();
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        if (!IsInteractable(card)) return;

        // Клик по карте - потенциальное действие
        EnsureGameStarted();

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

        // Убеждаемся, что старт засчитан (на всякий случай)
        EnsureGameStarted();

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

        int pointsToAdd = 5;
        List<int> clearedRows = pileManager.CheckForNewClearedRows();
        int[] rowBonuses = new int[] { 500, 250, 150, 100, 75, 50, 25 };
        foreach (int row in clearedRows) { if (row >= 0 && row < rowBonuses.Length) pointsToAdd += rowBonuses[row]; }
        move.ScoreGained = pointsToAdd;
        move.ClearedRows = clearedRows;
        if (scoreManager) scoreManager.AddPoints(pointsToAdd);
        StatisticsManager.Instance.RegisterMove();

        StartCoroutine(animManager.AnimateRemoveBallistic(cardA, targetA, () => { if (cardA) { cardA.transform.SetParent(targetA); cardA.gameObject.SetActive(false); } }));
        if (cardB != null) StartCoroutine(animManager.AnimateRemoveBallistic(cardB, targetB, () => { if (cardB) { cardB.transform.SetParent(targetB); cardB.gameObject.SetActive(false); } }));

        if (pileManager.IsPyramidCleared())
        {
            if (defeatRoutine != null) StopCoroutine(defeatRoutine);
            yield return new WaitForSeconds(removeAnimDuration);
            if (currentRound < totalRounds) { currentRound++; StartRound(false); }
            else
            {
                _isGameWon = true; // ПОБЕДА
                StatisticsManager.Instance.OnGameWon(scoreManager ? scoreManager.Score : 0);
                if (gameUI != null) gameUI.OnGameWon();
            }
        }
        else
        {
            yield return null;
            IsInputAllowed = true;
            UpdateUIState();
            CheckGameState();
        }
    }

    public void OnUndoAction()
    {
        if (undoStack.Count == 0 || !IsInputAllowed) return;

        // Undo тоже может считаться активностью, если вдруг нажали до первого хода (хотя вряд ли)
        EnsureGameStarted();

        StartCoroutine(UndoSequence(undoStack.Pop(), false));
    }

    public void OnUndoAllAction()
    {
        if (undoStack.Count == 0 || !IsInputAllowed) return;
        EnsureGameStarted();
        StartCoroutine(UndoAllRoutine());
    }

    private IEnumerator UndoAllRoutine()
    {
        IsInputAllowed = false; DeselectCard();
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);
        if (gameUI != null && gameUI.defeatPanel.activeSelf) gameUI.defeatPanel.SetActive(false);
        while (undoStack.Count > 0) { var move = undoStack.Pop(); ApplyUndoImmediate(move); }
        IsInputAllowed = true; UpdateUIState(); yield return null;
    }

    private void ApplyUndoImmediate(PyramidMoveRecord move)
    {
        if (move.Type == PyramidMoveRecord.MoveType.Deal)
        {
            CardController cDeal = move.DealtCard; pileManager.Waste.Remove(cDeal);
            cDeal.transform.SetParent(deckManager.stockRoot); cDeal.transform.localPosition = Vector3.zero; cDeal.transform.SetAsLastSibling(); cDeal.transform.localRotation = Quaternion.identity;
            pileManager.Stock.Add(cDeal); cDeal.GetComponent<CardData>().SetFaceUp(true);
        }
        else if (move.Type == PyramidMoveRecord.MoveType.Recycle)
        {
            recyclesRemaining++; pileManager.Stock.Clear();
            var cardsToWaste = new List<CardController>(move.RecycledCards); cardsToWaste.Reverse();
            foreach (var c in cardsToWaste) { c.GetComponent<CardData>().SetFaceUp(true); c.transform.SetParent(deckManager.wasteRoot); c.transform.localPosition = Vector3.zero; c.transform.SetAsLastSibling(); c.transform.localRotation = Quaternion.identity; pileManager.Waste.Add(c); }
        }
        else
        {
            foreach (var info in move.RemovedCards)
            {
                var c = info.Card; c.gameObject.SetActive(true); c.GetComponent<CardData>().image.color = Color.white; c.transform.localRotation = Quaternion.identity;
                Transform targetParent = null;
                if (info.SourceSlot != null) { targetParent = info.SourceSlot.transform; info.SourceSlot.Card = c; }
                else if (info.WasInWaste) { targetParent = deckManager.wasteRoot; pileManager.Waste.Add(c); }
                else if (info.WasInStock) { targetParent = deckManager.stockRoot; pileManager.Stock.Add(c); }
                if (targetParent != null) { c.transform.SetParent(targetParent); c.transform.localPosition = Vector3.zero; if (cardIsTop(targetParent, c)) c.transform.SetAsLastSibling(); if (c.canvasGroup) c.canvasGroup.interactable = true; }
            }
            if (scoreManager) scoreManager.AddPoints(-move.ScoreGained);
            if (move.ClearedRows != null) foreach (int row in move.ClearedRows) pileManager.RestoreRowFlag(row);
        }
    }

    private IEnumerator UndoSequence(PyramidMoveRecord move, bool immediate)
    {
        if (!immediate) IsInputAllowed = false; DeselectCard();
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);
        if (gameUI != null && gameUI.defeatPanel.activeSelf) gameUI.defeatPanel.SetActive(false);
        float dur = immediate ? 0f : 0.3f;

        if (!immediate && StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        if (move.Type == PyramidMoveRecord.MoveType.Deal)
        {
            CardController cDeal = move.DealtCard; pileManager.Waste.Remove(cDeal); cDeal.transform.localRotation = Quaternion.identity;
            if (!immediate) yield return StartCoroutine(animManager.MoveCardLinear(cDeal, deckManager.stockRoot.position, dur, () => pileManager.Stock.Add(cDeal))); else pileManager.Stock.Add(cDeal);
            cDeal.GetComponent<CardData>().SetFaceUp(true);
        }
        else if (move.Type == PyramidMoveRecord.MoveType.Recycle)
        {
            recyclesRemaining++; pileManager.Stock.Clear();
            var cardsToWaste = new List<CardController>(move.RecycledCards); cardsToWaste.Reverse();
            foreach (var c in cardsToWaste) { c.transform.localRotation = Quaternion.identity; if (!immediate) { c.transform.position = deckManager.stockRoot.position; StartCoroutine(animManager.MoveCardLinear(c, deckManager.wasteRoot.position, dur, () => pileManager.Waste.Add(c))); yield return new WaitForSeconds(0.05f); } else { c.GetComponent<CardData>().SetFaceUp(true); pileManager.Waste.Add(c); } }
            if (!immediate) yield return new WaitForSeconds(dur);
        }
        else
        {
            List<Coroutine> activeAnims = new List<Coroutine>();
            foreach (var info in move.RemovedCards)
            {
                var c = info.Card; c.gameObject.SetActive(true); c.transform.localRotation = Quaternion.identity;
                Transform startT = info.WentToLeftFoundation ? deckManager.leftFoundation : deckManager.rightFoundation; if (startT == null) startT = deckManager.stockRoot;
                Vector3 targetPos = Vector3.zero; Transform targetParent = null;
                if (info.SourceSlot != null) { targetPos = info.SourceSlot.transform.position; targetParent = info.SourceSlot.transform; info.SourceSlot.Card = c; }
                else if (info.WasInWaste) { targetPos = deckManager.wasteRoot.position; targetParent = deckManager.wasteRoot; pileManager.Waste.Add(c); }
                else if (info.WasInStock) { targetPos = deckManager.stockRoot.position; targetParent = deckManager.stockRoot; pileManager.Stock.Add(c); }
                c.GetComponent<CardData>().image.color = Color.white;
                if (immediate) { c.transform.SetParent(targetParent); c.transform.localPosition = Vector3.zero; if (cardIsTop(targetParent, c)) c.transform.SetAsLastSibling(); if (c.canvasGroup) c.canvasGroup.interactable = true; } else activeAnims.Add(StartCoroutine(animManager.ReturnCardFromFoundation(c, startT.position, targetPos, targetParent, removeAnimDuration)));
            }
            if (!immediate) foreach (var anim in activeAnims) yield return anim;
            if (scoreManager) scoreManager.AddPoints(-move.ScoreGained);
            if (move.ClearedRows != null) foreach (int row in move.ClearedRows) pileManager.RestoreRowFlag(row);
        }
        if (!immediate) IsInputAllowed = true;
        UpdateUIState();
    }

    private bool cardIsTop(Transform parent, CardController c) { return parent == deckManager.wasteRoot || parent == deckManager.stockRoot; }
    private Transform GetClosestFoundation(CardController card) { if (!deckManager.leftFoundation || !deckManager.rightFoundation) return deckManager.stockRoot; float d1 = Vector3.Distance(card.transform.position, deckManager.leftFoundation.position); float d2 = Vector3.Distance(card.transform.position, deckManager.rightFoundation.position); return d1 < d2 ? deckManager.leftFoundation : deckManager.rightFoundation; }
    private void SaveCardInfo(PyramidMoveRecord move, CardController c, Transform targetFoundation) { var info = new PyramidMoveRecord.RemovedCardInfo { Card = c }; var slot = pileManager.TableauSlots.Find(s => s.Card == c); if (slot != null) info.SourceSlot = slot; else if (pileManager.Stock.HasCard(c)) info.WasInStock = true; else if (pileManager.Waste.HasCard(c)) info.WasInWaste = true; info.WentToLeftFoundation = (targetFoundation == deckManager.leftFoundation); move.RemovedCards.Add(info); }
    private void SelectCard(CardController c) { selectedA = c; deckManager.SetCardHighlight(c, true); }
    private void DeselectCard() { if (selectedA != null) { deckManager.SetCardHighlight(selectedA, false); selectedA = null; } }
    private bool IsInteractable(CardController c) => c.canvasGroup != null && c.canvasGroup.interactable && c.gameObject.activeInHierarchy;
    private void UpdateUIState()
    {
        pileManager.UpdateLocks();
        if (dealButton != null) { bool canDeal = !pileManager.Stock.IsEmpty; bool canRecycle = pileManager.Stock.IsEmpty && pileManager.Waste.GetCards().Count > 0 && recyclesRemaining > 0; dealButton.interactable = (canDeal || canRecycle) && IsInputAllowed; }
        bool hasHistory = undoStack.Count > 0 && IsInputAllowed;
        if (undoButton != null) undoButton.interactable = hasHistory;
        if (undoAllButton != null) undoAllButton.interactable = hasHistory;
        if (scoreText != null && scoreManager != null) scoreText.text = scoreManager.Score.ToString();
        if (movesText != null && StatisticsManager.Instance != null) movesText.text = StatisticsManager.Instance.GetCurrentMoves().ToString();
    }
    public void CheckGameState()
    {
        if (pileManager.IsPyramidCleared()) return;
        if (!pileManager.Stock.IsEmpty) return;
        if (recyclesRemaining > 0 && !pileManager.Waste.GetCards().Count.Equals(0)) return;
        if (!pileManager.HasValidMove()) { if (defeatRoutine != null) StopCoroutine(defeatRoutine); defeatRoutine = StartCoroutine(ShowDefeatRoutine()); }
    }
    private IEnumerator ShowDefeatRoutine() { yield return new WaitForSeconds(1.0f); bool stillNoMoves = !pileManager.HasValidMove() && pileManager.Stock.IsEmpty && (recyclesRemaining <= 0 || pileManager.Waste.GetCards().Count == 0); if (stillNoMoves && gameUI != null) gameUI.OnGameLost(); defeatRoutine = null; }
    public void RestartGame() { StatisticsManager.Instance.OnGameAbandoned(); InitializeGame(currentDifficulty, totalRounds); }
    public void OnCardDoubleClicked(CardController card) => OnCardClicked(card);
}