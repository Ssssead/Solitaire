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
    public PyramidTutorialManager tutorialManager;
    private GameUIController gameUI;

    [Header("UI References")]
    [SerializeField] private Button dealButton;
    [SerializeField] private Button undoButton;
    [SerializeField] private Button undoAllButton;

    [Header("HUD (On Scene Texts)")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private TMP_Text timeText;

    [Header("Animation Settings")]
    [SerializeField] private float dealAnimDuration = 0.3f;
    [SerializeField] private float removeAnimDuration = 0.5f;
    [SerializeField] private float recycleDelay = 0.05f;
    [SerializeField] private float undoAnimDuration = 0.1f;

    [Header("Game Rules")]
    [SerializeField] private int maxRecycles = 2;

    [Header("Intro & Exit Animation")]
    public bool playIntroOnStart = true;
    public PyramidIntroController introController;
    public SceneExitAnimator exitAnimator;

    private CardController selectedA;
    private int currentRound = 1;
    private int totalRounds = 1;
    private Difficulty currentDifficulty;
    private Stack<PyramidMoveRecord> undoStack = new Stack<PyramidMoveRecord>();
    private int recyclesRemaining;
    private Coroutine defeatRoutine;
    public ITutorialManager Tutorial => tutorialManager;
    private Dictionary<CardController, Coroutine> activeCardRoutines = new Dictionary<CardController, Coroutine>();

    // --- State Flags ---
    private bool _hasGameStarted = false;
    private bool _isGameWon = false;
    private bool isRestarting = false;

    private float gameTimer = 0f;
    private bool isTimerRunning = false;

    public string GameName => "Pyramid";
    public RectTransform DragLayer => animManager ? animManager.dragLayerRect : null;
    public Canvas RootCanvas => null;
    public PileManager PileManager => null;
    public AnimationService AnimationService => null;
    public AutoMoveService AutoMoveService => null;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public bool IsInputAllowed { get; set; } = true;
    public GameType GameType => GameType.Pyramid;

    private void Start()
    {
        if (!animManager) animManager = FindObjectOfType<PyramidAnimationManager>();
        gameUI = FindObjectOfType<GameUIController>();

        InitializeGame(GameSettings.CurrentDifficulty, GameSettings.RoundsCount);
    }

    public bool IsMatchInProgress()
    {
        return _hasGameStarted && !_isGameWon;
    }

    private void Update()
    {
        if (isTimerRunning && !_isGameWon)
        {
            gameTimer += Time.deltaTime;
            UpdateTimerUI();
        }
    }

    private void UpdateTimerUI()
    {
        if (timeText != null)
        {
            System.TimeSpan t = System.TimeSpan.FromSeconds(gameTimer);
            timeText.text = t.ToString(@"m\:ss");
        }
    }

    public void InitializeGame(Difficulty difficulty, int rounds)
    {
        // 1. СНАЧАЛА закрываем прошлую игру
        if (_hasGameStarted && !_isGameWon && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }

        // 2. ЗАТЕМ сообщаем трекеру настройки нового матча
        string variant = GameSettings.GetCurrentVariantString(GameType.Pyramid) ?? "None";
        GameQuestTracker.Instance?.StartMatch("Pyramid", difficulty, variant);

        currentDifficulty = difficulty;
        totalRounds = rounds;
        currentRound = 1;
        undoStack.Clear();
        activeCardRoutines.Clear();
        recyclesRemaining = maxRecycles;
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);

        _hasGameStarted = false;
        _isGameWon = false;

        gameTimer = 0f;
        isTimerRunning = false;
        UpdateTimerUI();

        if (scoreManager) scoreManager.ResetScore();

        SetupButtons();

        StartRound(true);
    }

    private void StopCardRoutine(CardController card)
    {
        if (card == null) return;
        if (activeCardRoutines.TryGetValue(card, out Coroutine routine))
        {
            if (routine != null) StopCoroutine(routine);
            activeCardRoutines.Remove(card);
        }
        card.StopAllCoroutines();
    }

    private void EnsureGameStarted()
    {
        if (!_hasGameStarted)
        {
            _hasGameStarted = true;
            isTimerRunning = true;
            if (StatisticsManager.Instance != null)
            {
                string variant = GameSettings.GetCurrentVariantString(GameType.Pyramid);
                StatisticsManager.Instance.OnGameStarted("Pyramid", currentDifficulty, variant);
            }
        }
    }

    private void OnDestroy()
    {
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
        if (undoAllButton != null) { LongPressHoldTrigger.SubscribeToButton(undoAllButton, OnUndoAllAction); }
    }

    private void StartRound(bool isFirstRound)
    {
        IsInputAllowed = false;
        selectedA = null;
        undoStack.Clear();
        activeCardRoutines.Clear();
        recyclesRemaining = maxRecycles;
        if (pileManager != null) pileManager.ResetRowFlags();
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);

        // --- ДОБАВЛЕНО: Запуск Туториала вместо обычной игры ---
        if (GameSettings.IsTutorialMode && tutorialManager != null && isFirstRound)
        {
            StartCoroutine(tutorialManager.PlayTutorialIntro(null));
        }
        else
        {
            StartCoroutine(GenerateAndStartSequence(isFirstRound));
        }
    }

    // --- ОБНОВЛЕНО: Чистый старт без временных объектов ---
    private IEnumerator GenerateAndStartSequence(bool isFirstRound)
    {
        if (isRestarting)
        {
            deckManager.ClearBoard();
        }
        else if (!isFirstRound)
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

        // --- ИСПРАВЛЕНИЕ 1: Мгновенно убираем карты за экран, чтобы они не мелькали ---
        bool isFullIntro = isFirstRound && playIntroOnStart && introController != null && !isRestarting;
        if (isFullIntro || isRestarting)
        {
            foreach (Transform child in deckManager.stockRoot) child.localPosition = new Vector3(0, -2000f, 0); // Прячем вниз
        }
        else if (!isFirstRound)
        {
            foreach (Transform child in deckManager.stockRoot) child.localPosition = new Vector3(-2000f, 0, 0); // Прячем влево
        }
        // -------------------------------------------------------------------------------

        if (isFullIntro)
        {
            introController.PrepareIntro(false);
            yield return StartCoroutine(introController.PlayIntroSequence());

            if (animManager != null)
                yield return StartCoroutine(animManager.PlayIntroDeckArrival(deckManager.stockRoot, () => introController.IsSkipping));
        }
        else
        {
            if (introController != null) introController.PrepareIntro(true);

            if (isRestarting && animManager != null)
            {
                yield return StartCoroutine(animManager.PlayIntroDeckArrival(deckManager.stockRoot, () => introController != null && introController.IsSkipping));
            }
            else if (!isFirstRound && animManager != null)
            {
                yield return StartCoroutine(animManager.PlayNewRoundEntry(deckManager.stockRoot));
            }
        }

        if (animManager != null)
            yield return StartCoroutine(animManager.PlayDealAnimation(cardsToAnimate, () => introController != null && introController.IsSkipping));
        else
            pileManager.UpdateLocks();

        isRestarting = false;
        IsInputAllowed = true;
        UpdateUIState();
        CheckGameState();
    }

    public void RestartGame()
    {
        currentDifficulty = GameSettings.CurrentDifficulty;
        totalRounds = GameSettings.RoundsCount;
        if (totalRounds < 1) totalRounds = 1;

        isRestarting = true;
        StopAllCoroutines();
        IsInputAllowed = false;

        if (exitAnimator != null)
        {
            exitAnimator.PlayRestartSequence(() => { InitializeGame(currentDifficulty, totalRounds); });
        }
        else
        {
            InitializeGame(currentDifficulty, totalRounds);
        }
    }

    public void OnDealButtonClicked()
    {
        if (!IsInputAllowed) return;

        // <--- ЗВУК 3: КЛИК ПО КНОПКЕ СТОКА --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Click");

        DeselectCard();
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

        // <--- ЗВУК 4: ПЕРЕЛИСТЫВАНИЕ КАРТЫ СТОКА --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Flip");

        StopCardRoutine(card);
        int futureWasteIndex = pileManager.Waste.GetCards().Count;
        Vector3 targetPos = pileManager.Waste.transform.TransformPoint(new Vector3(futureWasteIndex * pileManager.Waste.stackGap, 0f, 0f));

        Coroutine dealRoutine = StartCoroutine(animManager.MoveCardLinear(card, targetPos, dealAnimDuration, () => { pileManager.Waste.Add(card); }));
        activeCardRoutines[card] = dealRoutine;

        yield return dealRoutine;

        var move = new PyramidMoveRecord { Type = PyramidMoveRecord.MoveType.Deal, DealtCard = card };
        undoStack.Push(move);
        StatisticsManager.Instance.RegisterMove();

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordMove();
        GameQuestTracker.Instance?.RecordStockDraw();
        // ----------------------

        IsInputAllowed = true;
        IsInputAllowed = true;
        UpdateUIState();
        CheckGameState();
    }

    private IEnumerator RecycleRoutine()
    {
        IsInputAllowed = false;
        List<CardController> wasteCards = pileManager.Waste.DrawAll();
        wasteCards.Reverse();

        for (int i = 0; i < wasteCards.Count; i++)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Flip");
            var card = wasteCards[i];
            StopCardRoutine(card);

            Vector3 targetPos = pileManager.Stock.transform.TransformPoint(new Vector3(i * pileManager.Stock.stackGap, 0f, 0f));

            Coroutine recRoutine = StartCoroutine(animManager.MoveCardToStockAndDisable(card, targetPos, dealAnimDuration));
            activeCardRoutines[card] = recRoutine;
            yield return new WaitForSeconds(recycleDelay);
        }

        yield return new WaitForSeconds(dealAnimDuration);
        pileManager.Stock.AddRange(wasteCards);
        var move = new PyramidMoveRecord { Type = PyramidMoveRecord.MoveType.Recycle, RecycledCards = new List<CardController>(wasteCards) };
        undoStack.Push(move);

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordMove();
        GameQuestTracker.Instance?.RecordStockDraw(); // Считаем ресайкл тоже как обращение к колоде (и сброс комбо)
        // ----------------------

        IsInputAllowed = true;
        IsInputAllowed = true;
        UpdateUIState();
        CheckGameState();
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        if (!IsInteractable(card)) return;

        EnsureGameStarted();

        if (card.cardModel.rank == 13) { StartCoroutine(RemoveSequence(card, null)); return; }

        if (selectedA == null)
        {
            SelectCard(card);

            // --- ДОБАВЛЕНО ДЛЯ ТУТОРИАЛА ---
            if (GameSettings.IsTutorialMode && tutorialManager is PyramidTutorialManager pyrTut)
            {
                pyrTut.OnCardSelectionChanged(card);
            }
        }
        else if (selectedA == card)
        {
            DeselectCard();

            // --- ДОБАВЛЕНО ДЛЯ ТУТОРИАЛА ---
            if (GameSettings.IsTutorialMode && tutorialManager is PyramidTutorialManager pyrTut)
            {
                pyrTut.OnCardSelectionChanged(null); // Сброс стрелки
            }
        }
        else
        {
            if (card.cardModel.rank + selectedA.cardModel.rank == 13) StartCoroutine(RemoveSequence(selectedA, card));
            else
            {
                DeselectCard();
                SelectCard(card);

                // --- ДОБАВЛЕНО ДЛЯ ТУТОРИАЛА ---
                if (GameSettings.IsTutorialMode && tutorialManager is PyramidTutorialManager pyrTut)
                {
                    pyrTut.OnCardSelectionChanged(card);
                }
            }
        }
    }
    public void OnStockClicked(CardController card) { if (pileManager.Stock.HasCard(card)) { if (card == pileManager.Stock.Peek()) OnCardClicked(card); } else if (pileManager.Waste.HasCard(card)) { if (card == pileManager.Waste.TopCard()) OnCardClicked(card); } }
    public void OnStockClicked() { }

    private IEnumerator RemoveSequence(CardController cardA, CardController cardB)
    {
        IsInputAllowed = false;
        DeselectCard(true);
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

        // ---> ОБНОВЛЕННЫЙ БЛОК ТРЕКИНГА КВЕСТОВ <---
        GameQuestTracker.Instance?.RecordMove();

        if (cardB == null)
        {
            // 1. Убрали Короля (одна карта)
            // Отправляем событие о конкретном ранге (13 - Король). Пару и комбо НЕ прибавляем!
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, cardA.cardModel.rank.ToString());
        }
        else
        {
            // 2. Убрали пару (две карты)
            GameQuestTracker.Instance?.SendEvent(QuestActionType.RemovePyramidPair, 1);
            GameQuestTracker.Instance?.IncrementCombo(QuestActionType.ComboPairsWithoutDraw);

            // На случай заданий типа "Убрать 4 Дамы", отправляем ранги обеих карт в паре
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, cardA.cardModel.rank.ToString());
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, cardB.cardModel.rank.ToString());
        }

        bool fromTableau = false;
        bool fromStockOrWaste = false;

        foreach (var info in move.RemovedCards)
        {
            if (info.SourceSlot != null) fromTableau = true;
            if (info.WasInStock || info.WasInWaste) fromStockOrWaste = true;
        }

        if (fromTableau) GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveFromPyramidFigure, 1);
        if (fromStockOrWaste) GameQuestTracker.Instance?.SendEvent(QuestActionType.RemovePairWithStock, 1);

        if (clearedRows.Contains(0)) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearPeak, 1);
        // -------------------------------------------

        if (scoreText != null && scoreManager != null) scoreText.text = scoreManager.Score.ToString();
        if (movesText != null && StatisticsManager.Instance != null) movesText.text = StatisticsManager.Instance.GetCurrentMoves().ToString();

        // <--- ЗВУК 1: КАРТЫ СРЫВАЮТСЯ С МЕСТА --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Whoosh_Out");

        StopCardRoutine(cardA);
        Coroutine routineA = StartCoroutine(animManager.AnimateRemoveBallistic(cardA, targetA, () =>
        {
            if (cardA)
            {
                cardA.transform.SetParent(targetA);
                cardA.gameObject.SetActive(false);
            }

            // <--- ЗВУК 2: КАРТЫ УСПЕШНО ДОЛЕТЕЛИ В ДОМ --->
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Foundation_Success");
        }));
        activeCardRoutines[cardA] = routineA;

        if (cardB != null)
        {
            StopCardRoutine(cardB);
            Coroutine routineB = StartCoroutine(animManager.AnimateRemoveBallistic(cardB, targetB, () => { if (cardB) { cardB.transform.SetParent(targetB); cardB.gameObject.SetActive(false); } }));
            activeCardRoutines[cardB] = routineB;
        }

        if (pileManager.IsPyramidCleared())
        {
            if (defeatRoutine != null) StopCoroutine(defeatRoutine);
            yield return new WaitForSeconds(removeAnimDuration);

            if (currentRound < totalRounds)
            {
                currentRound++;
                StartRound(false);
            }
            else
            {
                _isGameWon = true;
                isTimerRunning = false;

                int finalMoves = 0;
                if (StatisticsManager.Instance != null)
                    finalMoves = StatisticsManager.Instance.GetCurrentMoves();

                if (StatisticsManager.Instance != null)
                    StatisticsManager.Instance.OnGameWon(scoreManager ? scoreManager.Score : 0);

                // ---> БЛОК ПОБЕДЫ И ОСТАТКА КОЛОДЫ <---
                if (GameQuestTracker.Instance != null)
                {
                    // Для честности к игроку считаем сумму карт в закрытом стоке и открытом сбросе
                    int remainingCards = pileManager.Stock.Count + pileManager.Waste.GetCards().Count;
                    GameQuestTracker.Instance.SendEvent(QuestActionType.WinWithRemainingStock, remainingCards);

                    // Заодно сразу прокидываем событие на оставшиеся пересдачи колоды
                    GameQuestTracker.Instance.SendEvent(QuestActionType.WinWithRemainingShuffles, recyclesRemaining);
                }
                // -----------------------------

                if (gameUI != null)
                    gameUI.OnGameWon(finalMoves);
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

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Back");

        EnsureGameStarted();

        // ---> ДОБАВИТЬ СЮДА <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // -----------------------

        StartCoroutine(UndoSequence(undoStack.Pop(), false));
    }

    public void OnUndoAllAction()
    {
        if (undoStack.Count == 0 || !IsInputAllowed) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Back");

        EnsureGameStarted();

        // ---> ДОБАВИТЬ СЮДА <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // -----------------------

        StartCoroutine(UndoAllRoutine());
    }

    private IEnumerator UndoAllRoutine()
    {
        IsInputAllowed = false; DeselectCard(true);
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);
        if (gameUI != null && gameUI.defeatPanel.activeSelf) gameUI.defeatPanel.SetActive(false);

        // --- ИСПРАВЛЕНИЕ: Запускаем таймер заново, если мы отменили поражение ---
        if (!_isGameWon) isTimerRunning = true;

        while (undoStack.Count > 0) { var move = undoStack.Pop(); ApplyUndoImmediate(move); }

        if (pileManager.Stock != null) pileManager.Stock.UpdateLayout();
        if (pileManager.Waste != null) pileManager.Waste.UpdateLayout();

        IsInputAllowed = true; UpdateUIState(); yield return null;
    }

    private void ApplyUndoImmediate(PyramidMoveRecord move)
    {
        if (move.Type == PyramidMoveRecord.MoveType.Deal)
        {
            CardController cDeal = move.DealtCard;
            StopCardRoutine(cDeal);
            pileManager.Waste.Remove(cDeal);

            cDeal.transform.SetParent(deckManager.stockRoot);
            // ИСПРАВЛЕНИЕ: Возвращаем правильный локальный отступ
            cDeal.transform.localPosition = new Vector3(pileManager.Stock.Count * pileManager.Stock.stackGap, 0, 0);
            cDeal.transform.SetAsLastSibling();
            cDeal.transform.localRotation = Quaternion.identity;

            pileManager.Stock.Add(cDeal);
            cDeal.GetComponent<CardData>().SetFaceUp(true);
        }
        else if (move.Type == PyramidMoveRecord.MoveType.Recycle)
        {
            recyclesRemaining++; pileManager.Stock.Clear();
            var cardsToWaste = new List<CardController>(move.RecycledCards); cardsToWaste.Reverse();
            foreach (var c in cardsToWaste)
            {
                StopCardRoutine(c);
                c.GetComponent<CardData>().SetFaceUp(true);
                c.transform.SetParent(deckManager.wasteRoot);

                // ИСПРАВЛЕНИЕ: Отступ для сброса
                c.transform.localPosition = new Vector3(pileManager.Waste.GetCards().Count * pileManager.Waste.stackGap, 0, 0);
                c.transform.SetAsLastSibling();
                c.transform.localRotation = Quaternion.identity;

                pileManager.Waste.Add(c);
            }
        }
        else
        {
            // ---> ДОБАВИТЬ ЭТОТ БЛОК АНТИ-ЧИТА <---
            GameQuestTracker.Instance?.SendEvent(QuestActionType.RemovePyramidPair, -1);
            bool fromTableau = false;
            bool fromStockOrWaste = false;
            foreach (var info in move.RemovedCards)
            {
                if (info.SourceSlot != null) fromTableau = true;
                if (info.WasInStock || info.WasInWaste) fromStockOrWaste = true;
            }
            if (fromTableau) GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveFromPyramidFigure, -1);
            if (fromStockOrWaste) GameQuestTracker.Instance?.SendEvent(QuestActionType.RemovePairWithStock, -1);
            if (move.ClearedRows != null && move.ClearedRows.Contains(0)) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearPeak, -1);
            // --------------------------------------

            foreach (var info in move.RemovedCards)
            {
                var c = info.Card;
                StopCardRoutine(c);

                c.gameObject.SetActive(true); c.GetComponent<CardData>().image.color = Color.white; c.transform.localRotation = Quaternion.identity;

                Transform targetParent = null;
                Vector3 targetLocalPos = Vector3.zero;

                if (info.SourceSlot != null)
                {
                    targetParent = info.SourceSlot.transform;
                    info.SourceSlot.Card = c;
                    targetLocalPos = Vector3.zero; // В самой пирамиде отступ не нужен (карты центрируются)
                }
                else if (info.WasInWaste)
                {
                    targetParent = deckManager.wasteRoot;
                    targetLocalPos = new Vector3(pileManager.Waste.GetCards().Count * pileManager.Waste.stackGap, 0, 0);
                    pileManager.Waste.Add(c);
                }
                else if (info.WasInStock)
                {
                    targetParent = deckManager.stockRoot;
                    targetLocalPos = new Vector3(pileManager.Stock.Count * pileManager.Stock.stackGap, 0, 0);
                    pileManager.Stock.Add(c);
                }

                if (targetParent != null)
                {
                    c.transform.SetParent(targetParent);
                    c.transform.localPosition = targetLocalPos; // ИСПРАВЛЕНИЕ: Используем вычисленный отступ
                    if (cardIsTop(targetParent, c)) c.transform.SetAsLastSibling();
                    if (c.canvasGroup) c.canvasGroup.interactable = true;

                    // Успокаиваем тень
                    var shadowCtrl = c.GetComponent<PyramidShadowController>();
                    if (shadowCtrl != null) shadowCtrl.SetState(PyramidShadowController.ShadowState.Resting);
                }
            }
            if (scoreManager) scoreManager.AddPoints(-move.ScoreGained);
            if (move.ClearedRows != null) foreach (int row in move.ClearedRows) pileManager.RestoreRowFlag(row);
        }
    }

    private IEnumerator UndoSequence(PyramidMoveRecord move, bool immediate)
    {
        if (!immediate) IsInputAllowed = false;
        DeselectCard(true);
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);
        if (gameUI != null && gameUI.defeatPanel.activeSelf) gameUI.defeatPanel.SetActive(false);

        if (!_isGameWon) isTimerRunning = true;

        float dur = immediate ? 0f : undoAnimDuration;

        if (!immediate && StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        // --- УДАЛЕНО: Общий звук из начала метода ---

        if (move.Type == PyramidMoveRecord.MoveType.Deal)
        {
            // <--- ЗВУК: Отмена перелистывания стока (одна карта) --->
            if (!immediate && AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Flip");

            CardController cDeal = move.DealtCard;
            StopCardRoutine(cDeal);
            pileManager.Waste.Remove(cDeal);
            cDeal.transform.localRotation = Quaternion.identity;

            int futureStockIndex = pileManager.Stock.Count;
            Vector3 targetPos = pileManager.Stock.transform.TransformPoint(new Vector3(futureStockIndex * pileManager.Stock.stackGap, 0f, 0f));

            if (!immediate)
            {
                Coroutine r = StartCoroutine(animManager.MoveCardLinear(cDeal, targetPos, dur, () => pileManager.Stock.Add(cDeal)));
                activeCardRoutines[cDeal] = r;
                yield return r;
            }
            else
            {
                cDeal.transform.position = targetPos;
                pileManager.Stock.Add(cDeal);
            }
            cDeal.GetComponent<CardData>().SetFaceUp(true);
        }
        else if (move.Type == PyramidMoveRecord.MoveType.Recycle)
        {
            recyclesRemaining++;
            pileManager.Stock.Clear();
            var cardsToWaste = new List<CardController>(move.RecycledCards);
            cardsToWaste.Reverse();

            float delayBetweenCards = immediate ? 0f : 0.01f;
            int initialWasteCount = pileManager.Waste.GetCards().Count;

            for (int i = 0; i < cardsToWaste.Count; i++)
            {
                // <--- ЗВУК: Отмена ресайкла (звук переворота для каждой карты в цикле) --->
                if (!immediate && AudioManager.Instance != null)
                    AudioManager.Instance.PlaySound("Card_Flip");

                var c = cardsToWaste[i];
                StopCardRoutine(c);
                c.transform.localRotation = Quaternion.identity;

                int futureWasteIndex = initialWasteCount + i;
                Vector3 targetPos = pileManager.Waste.transform.TransformPoint(new Vector3(futureWasteIndex * pileManager.Waste.stackGap, 0f, 0f));

                if (!immediate)
                {
                    Coroutine r = StartCoroutine(animManager.MoveCardLinear(c, targetPos, dur, () => pileManager.Waste.Add(c)));
                    activeCardRoutines[c] = r;
                    yield return new WaitForSeconds(delayBetweenCards);
                }
                else
                {
                    c.transform.position = targetPos;
                    c.GetComponent<CardData>().SetFaceUp(true);
                    pileManager.Waste.Add(c);
                }
            }
            if (!immediate) yield return new WaitForSeconds(dur);
        }
        else // RemoveKing или RemovePair
        {
            // <--- ЗВУК: Возврат пары или короля на стол (свист с высоким питчем) --->
            if (!immediate && AudioManager.Instance != null)
            {
                AudioSource whooshSource = AudioManager.Instance.PlaySound("Card_Whoosh_Out");
                if (whooshSource != null) whooshSource.pitch = 1.5f;
            }

            // ---> ДОБАВИТЬ ЭТОТ БЛОК АНТИ-ЧИТА <---
            GameQuestTracker.Instance?.SendEvent(QuestActionType.RemovePyramidPair, -1);
            bool fromTableau = false;
            bool fromStockOrWaste = false;
            foreach (var info in move.RemovedCards)
            {
                if (info.SourceSlot != null) fromTableau = true;
                if (info.WasInStock || info.WasInWaste) fromStockOrWaste = true;
            }
            if (fromTableau) GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveFromPyramidFigure, -1);
            if (fromStockOrWaste) GameQuestTracker.Instance?.SendEvent(QuestActionType.RemovePairWithStock, -1);
            if (move.ClearedRows != null && move.ClearedRows.Contains(0)) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearPeak, -1);
            // --------------------------------------

            List<Coroutine> waitAnims = new List<Coroutine>();
            foreach (var info in move.RemovedCards)
            {
                var c = info.Card;
                StopCardRoutine(c);

                c.gameObject.SetActive(true);
                c.transform.localRotation = Quaternion.identity;

                Transform startT = info.WentToLeftFoundation ? deckManager.leftFoundation : deckManager.rightFoundation;
                if (startT == null) startT = deckManager.stockRoot;

                Vector3 targetPos = Vector3.zero;
                Transform targetParent = null;

                if (info.SourceSlot != null)
                {
                    targetPos = info.SourceSlot.transform.position;
                    targetParent = info.SourceSlot.transform;
                    info.SourceSlot.Card = c;
                }
                else if (info.WasInWaste)
                {
                    int futureIndex = pileManager.Waste.GetCards().Count;
                    targetPos = pileManager.Waste.transform.TransformPoint(new Vector3(futureIndex * pileManager.Waste.stackGap, 0f, 0f));
                    targetParent = deckManager.wasteRoot;
                    pileManager.Waste.Add(c);
                }
                else if (info.WasInStock)
                {
                    int futureIndex = pileManager.Stock.Count;
                    targetPos = pileManager.Stock.transform.TransformPoint(new Vector3(futureIndex * pileManager.Stock.stackGap, 0f, 0f));
                    targetParent = deckManager.stockRoot;
                    pileManager.Stock.Add(c);
                }

                c.GetComponent<CardData>().image.color = Color.white;

                if (immediate)
                {
                    c.transform.SetParent(targetParent);
                    c.transform.position = targetPos;
                    if (c.canvasGroup) c.canvasGroup.interactable = true;
                }
                else
                {
                    Coroutine returnRoutine = StartCoroutine(animManager.ReturnCardFromFoundation(c, startT.position, targetPos, targetParent, dur));
                    activeCardRoutines[c] = returnRoutine;
                    waitAnims.Add(returnRoutine);
                }
            }
            if (!immediate) foreach (var anim in waitAnims) yield return anim;

            if (scoreManager) scoreManager.AddPoints(-move.ScoreGained);
            if (move.ClearedRows != null) foreach (int row in move.ClearedRows) pileManager.RestoreRowFlag(row);
        }

        if (!immediate) IsInputAllowed = true;
        UpdateUIState();
    }

    private void SelectCard(CardController c)
    {
        selectedA = c;
        animManager.SelectCard(c);
    }

    private void DeselectCard(bool immediate = false)
    {
        if (selectedA != null)
        {
            Transform originalParent = GetCardParent(selectedA);
            Vector3 targetWorldPos = GetCardWorldPosition(selectedA);
            animManager.DeselectCard(selectedA, originalParent, targetWorldPos, immediate);
            selectedA = null;
        }
    }

    private Transform GetCardParent(CardController c)
    {
        var slot = pileManager.TableauSlots.Find(s => s.Card == c);
        if (slot != null) return slot.transform;
        if (pileManager.Waste.HasCard(c)) return deckManager.wasteRoot;
        if (pileManager.Stock.HasCard(c)) return deckManager.stockRoot;
        return deckManager.stockRoot;
    }

    private Vector3 GetCardWorldPosition(CardController c)
    {
        var slot = pileManager.TableauSlots.Find(s => s.Card == c);
        if (slot != null) return slot.transform.position;
        if (pileManager.Waste.HasCard(c)) return pileManager.Waste.GetCardWorldPosition(c);
        if (pileManager.Stock.HasCard(c)) return pileManager.Stock.GetCardWorldPosition(c);
        return deckManager.stockRoot.position;
    }

    private bool cardIsTop(Transform parent, CardController c) { return parent == deckManager.wasteRoot || parent == deckManager.stockRoot; }
    private Transform GetClosestFoundation(CardController card) { if (!deckManager.leftFoundation || !deckManager.rightFoundation) return deckManager.stockRoot; float d1 = Vector3.Distance(card.transform.position, deckManager.leftFoundation.position); float d2 = Vector3.Distance(card.transform.position, deckManager.rightFoundation.position); return d1 < d2 ? deckManager.leftFoundation : deckManager.rightFoundation; }
    private void SaveCardInfo(PyramidMoveRecord move, CardController c, Transform targetFoundation) { var info = new PyramidMoveRecord.RemovedCardInfo { Card = c }; var slot = pileManager.TableauSlots.Find(s => s.Card == c); if (slot != null) info.SourceSlot = slot; else if (pileManager.Stock.HasCard(c)) info.WasInStock = true; else if (pileManager.Waste.HasCard(c)) info.WasInWaste = true; info.WentToLeftFoundation = (targetFoundation == deckManager.leftFoundation); move.RemovedCards.Add(info); }
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

    private IEnumerator ShowDefeatRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        bool stillNoMoves = !pileManager.HasValidMove() && pileManager.Stock.IsEmpty && (recyclesRemaining <= 0 || pileManager.Waste.GetCards().Count == 0);
        if (stillNoMoves && gameUI != null)
        {
            isTimerRunning = false;
            gameUI.OnGameLost();
        }
        defeatRoutine = null;
    }

    public void OnCardDoubleClicked(CardController card) => OnCardClicked(card);
}