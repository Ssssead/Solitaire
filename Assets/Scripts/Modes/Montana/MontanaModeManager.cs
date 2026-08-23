using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using YG;

public class MontanaModeManager : MonoBehaviour, IModeManager, ICardGameMode, ICardClickReceiver
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
    public bool IsHardMode = false;

    [Header("Services")]
    public MontanaPileManager pileManager;
    public MontanaDeckManager deckManager;
    public DragManager dragManager;
    public UndoManager undoManager;
    public MontanaAnimationService animationService;
    public MontanaAutoMoveService autoMoveService;
    public MontanaScoreManager scoreManager;
    public MontanaTutorialManager tutorialManager;

    [HideInInspector] public MontanaPuzzleManager puzzleManager;

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
    public UnityEngine.UI.Button reshuffleButton;

    private Stack<MontanaMoveRecord> undoStack = new Stack<MontanaMoveRecord>();
    private bool isUndoing = false;
    public ITutorialManager Tutorial => tutorialManager;

    private Dictionary<CardController, MontanaSlot> initialBoardState = new Dictionary<CardController, MontanaSlot>();

    public int MaxReshuffles => IsHardMode ? 5 : 3;
    public int CurrentReshufflesLeft { get; private set; }

    private bool isDefeatPending = false;
    private Coroutine reshufflePulseCoroutine;
    private Vector3 originalReshuffleScale = Vector3.one;

    #region Initialization & Core Loop

    private void Awake()
    {
        puzzleManager = gameObject.AddComponent<MontanaPuzzleManager>();

        pileManager.Initialize(this);
        deckManager.Initialize(this, cardFactory, pileManager);
        dragManager?.Initialize(this, rootCanvas, dragLayer, undoManager);
        autoMoveService.Initialize(this, pileManager);
        animationService.Initialize(this);

        if (undoButton != null) undoButton.onClick.AddListener(OnUndoButtonClicked);
        if (undoAllButton != null) LongPressHoldTrigger.SubscribeToButton(undoAllButton, OnUndoAllButtonClicked);

        if (reshuffleButton != null) originalReshuffleScale = reshuffleButton.transform.localScale;
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
        IsHardMode = GameSettings.MontanaHard;

        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }

        // 2. ЗАТЕМ сообщаем трекеру настройки
        string variant = IsHardMode ? "Hard" : "Standard";
        GameQuestTracker.Instance?.StartMatch("Montana", GameSettings.CurrentDifficulty, variant);

        IsInputAllowed = false;
        hasGameStarted = false;
        hasWonGame = false;
        isDefeatPending = false;
        gameTimer = 0f;
        CurrentReshufflesLeft = MaxReshuffles;

        StopReshufflePulse();

        undoStack.Clear();
        UpdateUndoButton();

        scoreManager.ResetScore();
        undoManager?.ResetHistory();
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

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordMove();
        // ----------------------

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            if (StatisticsManager.Instance != null)
            {
                Difficulty diff = GameSettings.CurrentDifficulty;
                string variant = GameSettings.GetCurrentVariantString(GameType.Montana);
                StatisticsManager.Instance.OnGameStarted("Montana", diff, variant);
            }
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        UpdateFullUI();
    }

    public void CheckGameState()
    {
        if (hasWonGame || isDefeatPending) return;

        UpdateLockedCards();
        UpdateUndoButton();

        if (IsGameWon())
        {
            hasWonGame = true;
            IsInputAllowed = false;
            undoManager?.ClearAndLock();
            StopReshufflePulse();

            // ---> ДОБАВИТЬ ЭТО <---
            if (!GameSettings.IsTutorialMode)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.WinWithRemainingShuffles, CurrentReshufflesLeft);
            }
            // ----------------------

            if (StatisticsManager.Instance != null)
            {
                int finalMoves = StatisticsManager.Instance.GetCurrentMoves();
                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;

                StatisticsManager.Instance.OnGameWon(finalScore);
                if (gameUI != null) gameUI.OnGameWon(finalMoves);
            }
            return;
        }

        if (!HasAvailableMoves())
        {
            if (CurrentReshufflesLeft <= 0)
            {
                StopReshufflePulse();
                StartCoroutine(ShowDefeatPanelRoutine(1f));
            }
            else
            {
                StartReshufflePulse();
            }
        }
        else
        {
            StopReshufflePulse();
        }
    }

    private IEnumerator ShowDefeatPanelRoutine(float delay)
    {
        isDefeatPending = true;
        IsInputAllowed = false;

        yield return new WaitForSeconds(delay);

        if (gameUI != null) gameUI.OnGameLost();

        isDefeatPending = false;
    }

    private void StartReshufflePulse()
    {
        if (reshufflePulseCoroutine != null || reshuffleButton == null) return;
        reshufflePulseCoroutine = StartCoroutine(ReshufflePulseRoutine());
    }

    private void StopReshufflePulse()
    {
        if (reshufflePulseCoroutine != null)
        {
            StopCoroutine(reshufflePulseCoroutine);
            reshufflePulseCoroutine = null;
            if (reshuffleButton != null) reshuffleButton.transform.localScale = originalReshuffleScale;
        }
    }

    private IEnumerator ReshufflePulseRoutine()
    {
        float elapsed = 0f;
        float speed = 5f;
        float maxScale = 1.15f;

        while (true)
        {
            elapsed += Time.deltaTime * speed;
            float scale = Mathf.Lerp(1f, maxScale, (Mathf.Sin(elapsed) + 1f) / 2f);

            if (reshuffleButton != null)
            {
                reshuffleButton.transform.localScale = originalReshuffleScale * scale;
            }
            yield return null;
        }
    }

    private bool HasAvailableMoves()
    {
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 14; c++)
            {
                var slot = pileManager.GetSlot(r, c);
                if (slot.GetTopCard() == null)
                {
                    if (c == 0) return true;

                    var leftCard = pileManager.GetSlot(r, c - 1).GetTopCard();
                    if (leftCard != null && leftCard.cardModel.rank != 13)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
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
        foreach (var slot in pileManager.Slots)
        {
            var card = slot.GetTopCard();
            if (card != null) card.GetComponent<MontanaCardController>()?.SetLockedState(false);
        }

        Dictionary<MontanaCardController, int> levitatingCards = new Dictionary<MontanaCardController, int>();

        for (int r = 0; r < 4; r++)
        {
            var firstCard = pileManager.GetSlot(r, 0).GetTopCard();

            if (firstCard != null && firstCard.cardModel.rank == 1)
            {
                int currentChain = 1;
                var mFirstCard = firstCard.GetComponent<MontanaCardController>();
                if (mFirstCard != null) mFirstCard.SetLockedState(true);

                List<MontanaCardController> lockedCardsInRow = new List<MontanaCardController>();
                if (mFirstCard != null) lockedCardsInRow.Add(mFirstCard);

                for (int c = 1; c < 13; c++)
                {
                    var card = pileManager.GetSlot(r, c).GetTopCard();
                    var prevCard = pileManager.GetSlot(r, c - 1).GetTopCard();

                    if (card == null || prevCard == null) break;

                    if (card.cardModel.suit == prevCard.cardModel.suit &&
                        card.cardModel.rank == prevCard.cardModel.rank + 1)
                    {
                        var mCard = card.GetComponent<MontanaCardController>();
                        if (mCard != null)
                        {
                            mCard.SetLockedState(true);
                            lockedCardsInRow.Add(mCard);
                        }
                        currentChain++;
                    }
                    else break;
                }

                if (currentChain == 13)
                {
                    for (int i = 0; i < lockedCardsInRow.Count; i++)
                    {
                        levitatingCards[lockedCardsInRow[i]] = i;
                    }
                }
            }
        }

        float waveDelay = 0.055f;
        foreach (var slot in pileManager.Slots)
        {
            var card = slot.GetTopCard();
            if (card != null)
            {
                var mCard = card.GetComponent<MontanaCardController>();
                if (mCard != null)
                {
                    if (levitatingCards.ContainsKey(mCard)) mCard.SetLevitating(true, levitatingCards[mCard] * waveDelay);
                    else mCard.SetLevitating(false, slot.Col * waveDelay);
                }
            }
        }
    }

    public bool IsGameWon()
    {
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
        }
        return true;
    }

    public void PerformReshuffle()
    {
        // === ЖЕСТКИЙ ПЕРЕХВАТ ДЛЯ ТУТОРИАЛА ===
        if (GameSettings.IsTutorialMode)
        {
            // Если ссылка в инспекторе слетела, находим скрипт принудительно
            if (tutorialManager == null)
                tutorialManager = GetComponent<MontanaTutorialManager>();

            if (tutorialManager != null && tutorialManager.isActiveAndEnabled)
            {
                // Запускаем обучающую пересдачу и БЛОКИРУЕМ удаление карт!
                tutorialManager.OnTutorialReshuffleClicked();
                return;
            }
        }
        // ======================================

        if (!IsInputAllowed || CurrentReshufflesLeft <= 0) return;

        // ---> ДОБАВИТЬ ЭТО: Считаем пересдачу как обращение к колоде (для комбо) <---
        GameQuestTracker.Instance?.RecordStockDraw();
        GameQuestTracker.Instance?.ResetCombo();
        // ----------------------------------------------------------------------------

        StopReshufflePulse();
        RegisterMoveAndStartIfNeeded();

        CurrentReshufflesLeft--;

        UpdateUndoButton();

        UpdateFullUI();
        StartCoroutine(deckManager.ReshuffleRoutine());
    }

    #endregion

    #region User Interactions

    public void OnUndoAction() { }

    // --- ОТМЕНА ОДНОГО ХОДА ---
    private IEnumerator UndoRoutine()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Back");

        isUndoing = true;
        IsInputAllowed = false;

        var record = undoStack.Pop();
        UpdateUndoButton();

        // ---> ИСПРАВЛЕННЫЙ ОТКАТ ПРОГРЕССА КВЕСТОВ <---
        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveTableauToTableau, -1);

        if (record.WasCorrectChain)
        {
            GameQuestTracker.Instance?.SendEvent(QuestActionType.FillMontanaGap, -1);

            // ---> ОТКАТ ДЛЯ ОБЩИХ ЗАДАНИЙ НА ДОМ И РАНГИ КАРТ <---
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, -1);
            if (record.Card != null)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -1, record.Card.cardModel.rank.ToString());
            }
            // -----------------------------------------------------
        }
        if (record.CompletedRow)
        {
            GameQuestTracker.Instance?.SendEvent(QuestActionType.CompleteMontanaRow, -1);
        }
        // ----------------------------------------------

        record.TargetSlot.RemoveCard(record.Card);
        record.Card.transform.SetParent(DragLayer, true);
        record.Card.transform.SetAsLastSibling();

        var mCard = record.Card.GetComponent<MontanaCardController>();
        if (mCard != null)
        {
            mCard.SetAnimating(true);
            mCard.SetLockedState(false); // Делаем карту белой в полете
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Whoosh_Out");

        Vector3 startPos = record.Card.transform.position;
        Vector3 endPos = record.SourceSlot.Transform.position;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = t * t * (3f - 2f * t);
            record.Card.transform.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }

        record.Card.transform.position = endPos;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");

        record.SourceSlot.AcceptCard(record.Card);
        if (mCard != null) mCard.SetAnimating(false);

        RegisterMoveAndStartIfNeeded();
        scoreManager.OnUndo();
        UpdateLockedCards();

        isUndoing = false;
        IsInputAllowed = true;
        UpdateUndoButton();

        UpdateFullUI();
        CheckGameState();
    }

    // --- ОТМЕНА ПЕРЕСДАЧИ ---
    private IEnumerator UndoReshuffleRoutine()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Back");

        isUndoing = true;
        IsInputAllowed = false;

        var record = undoStack.Pop();
        UpdateUndoButton();

        List<CardController> movingCards = new List<CardController>();
        List<Vector3> startPositions = new List<Vector3>();
        List<MontanaSlot> targetSlots = new List<MontanaSlot>();

        foreach (var kvp in record.BoardState)
        {
            var card = kvp.Key;
            var oldSlot = kvp.Value;
            var currentSlot = pileManager.Slots.Find(s => s.GetTopCard() == card);

            if (currentSlot != oldSlot)
            {
                movingCards.Add(card);
                startPositions.Add(card.transform.position);
                targetSlots.Add(oldSlot);

                if (currentSlot != null) currentSlot.RemoveCard(card);
                card.transform.SetParent(DragLayer, true);

                var mCard = card.GetComponent<MontanaCardController>();
                if (mCard != null)
                {
                    mCard.SetAnimating(true);
                    mCard.SetLockedState(false); // Делаем светлой в полете
                }
            }
        }

        yield return new WaitForSeconds(0.1f);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", 0.4f, 0.1f);

        Vector3 gatherPos = pileManager.Slots[55].Transform.position;
        float gatherDuration = 0.4f;
        float gatherElapsed = 0f;

        while (gatherElapsed < gatherDuration)
        {
            gatherElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(gatherElapsed / gatherDuration);
            t = t * t * (3f - 2f * t);

            for (int i = 0; i < movingCards.Count; i++)
            {
                movingCards[i].transform.position = Vector3.Lerp(startPositions[i], gatherPos, t);
            }
            yield return null;
        }

        foreach (var c in movingCards) c.transform.position = gatherPos;

        yield return new WaitForSeconds(0.2f);

        float dealSpeed = 0.02f;
        float cardMoveDuration = 0.25f;

        var sortedIndices = Enumerable.Range(0, movingCards.Count)
            .OrderBy(i => pileManager.Slots.IndexOf(targetSlots[i]))
            .ToList();

        for (int i = 0; i < sortedIndices.Count; i++)
        {
            var card = movingCards[sortedIndices[i]];
            var targetSlot = targetSlots[sortedIndices[i]];
            StartCoroutine(UndoMoveCardToSlotRoutine(card, targetSlot, cardMoveDuration));
            yield return new WaitForSeconds(dealSpeed);
        }

        yield return new WaitForSeconds(cardMoveDuration);

        CurrentReshufflesLeft++;
        if (puzzleManager != null) puzzleManager.UndoCheckpoint();

        RegisterMoveAndStartIfNeeded();
        scoreManager.OnUndo();
        UpdateLockedCards();

        isUndoing = false;
        IsInputAllowed = true;
        UpdateUndoButton();
        UpdateFullUI();
        CheckGameState();
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        var montanaCard = card.GetComponent<MontanaCardController>();
        ICardContainer source = montanaCard != null ? montanaCard.SourceContainer : lastInteractionSource;

        if (source is MontanaSlot sourceSlot && container is MontanaSlot targetSlot)
        {
            // Проверяем условия выполнения заданий
            bool isRowComplete = CheckIfRowCompleted(targetSlot.Row);
            bool isCorrectChain = IsSlotInCorrectChain(targetSlot.Row, targetSlot.Col);

            undoStack.Push(new MontanaMoveRecord
            {
                IsReshuffle = false,
                Card = card,
                SourceSlot = sourceSlot,
                TargetSlot = targetSlot,
                CompletedRow = isRowComplete,
                WasCorrectChain = isCorrectChain // Запоминаем для Undo
            });
            UpdateUndoButton();

            // ---> ТРЕКИНГ ЗАДАНИЙ КОВРИКА <---
            if (!GameSettings.IsTutorialMode)
            {
                // Задание "Точечная работа": Успешно заполнить любой пробел
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveTableauToTableau, 1);

                // Задания "Наведение порядка" и "Легкий старт": Учитываем ТОЛЬКО правильный порядок ряда
                if (isCorrectChain)
                {
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.FillMontanaGap, 1);

                    // ---> ДОБАВЛЕНО ДЛЯ ОБЩИХ ЗАДАНИЙ (Перенос в дом и Ранги карт) <---
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 1);
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());
                    // ------------------------------------------------------------------

                    // Задание "Без перетасовок": Серия заполнения пробелов подряд
                    GameQuestTracker.Instance?.IncrementCombo(QuestActionType.ComboGapsWithoutShuffle);
                }

                // Задание "Идеальные ряды" (Полностью собранный ряд до Короля)
                if (isRowComplete)
                {
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.CompleteMontanaRow, 1);
                }
            }
            // -------------------------------------
        }

        RegisterMoveAndStartIfNeeded();
        if (source != null) scoreManager.OnCardMove(source, container);

        UpdateFullUI();
        CheckGameState();

        if (montanaCard != null && montanaCard.IsLockedCard)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound("Card_Foundation_Success");
            }
        }
    }

    public void PushReshuffleRecord(Dictionary<CardController, MontanaSlot> state)
    {
        undoStack.Push(new MontanaMoveRecord
        {
            IsReshuffle = true,
            BoardState = state
        });
        UpdateUndoButton();
    }
    private bool CheckIfRowCompleted(int r)
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
        return true;
    }
    private void UpdateUndoButton()
    {
        bool canUndo = (undoStack.Count > 0 && !isUndoing && IsInputAllowed);
        bool canUndoAll = ((undoStack.Count > 0 || CurrentReshufflesLeft < MaxReshuffles) && !isUndoing && IsInputAllowed);

        if (undoButton != null) undoButton.interactable = canUndo;
        if (undoAllButton != null) undoAllButton.interactable = canUndoAll;
    }

    public void OnUndoButtonClicked()
    {
        if (undoStack.Count == 0 || isUndoing || !IsInputAllowed) return;

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // ----------------------

        var record = undoStack.Peek();
        if (record.IsReshuffle)
        {
            StartCoroutine(UndoReshuffleRoutine());
        }
        else
        {
            StartCoroutine(UndoRoutine());
        }
    }

    public void OnUndoAllButtonClicked()
    {
        bool canUndoAll = (undoStack.Count > 0 || CurrentReshufflesLeft < MaxReshuffles);
        if (!canUndoAll || isUndoing || !IsInputAllowed) return;

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // ----------------------

        StartCoroutine(UndoAllRoutine());
    }

    // --- ОТМЕНА ВСЕХ ХОДОВ И ПЕРЕСДАЧ ---
    private IEnumerator UndoAllRoutine()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Back");

        // ---> УМНЫЙ АНТИ-ЧИТ ПРИ ПОЛНОЙ ОТМЕНЕ С УЧЕТОМ ЦЕПОЧЕК <---
        int movesToRollback = undoStack.Count(r => !r.IsReshuffle);
        int correctChainsToRollback = undoStack.Count(r => !r.IsReshuffle && r.WasCorrectChain);
        int rowsToRollback = undoStack.Count(r => !r.IsReshuffle && r.CompletedRow);

        if (movesToRollback > 0) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveTableauToTableau, -movesToRollback);

        if (correctChainsToRollback > 0)
        {
            GameQuestTracker.Instance?.SendEvent(QuestActionType.FillMontanaGap, -correctChainsToRollback);

            // ---> ОТКАТ ОБЩИХ ЗАДАНИЙ НА ДОМ И РАНГИ ПРИ ПОЛНОМ СБРОСЕ <---
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, -correctChainsToRollback);

            foreach (var record in undoStack)
            {
                if (!record.IsReshuffle && record.WasCorrectChain && record.Card != null)
                {
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -1, record.Card.cardModel.rank.ToString());
                }
            }
        }

        if (rowsToRollback > 0) GameQuestTracker.Instance?.SendEvent(QuestActionType.CompleteMontanaRow, -rowsToRollback);
        // ----------------------------------------------------------

        isUndoing = true;
        IsInputAllowed = false;

        List<CardController> allCards = new List<CardController>();
        List<Vector3> startPositions = new List<Vector3>();
        List<MontanaSlot> targetSlots = new List<MontanaSlot>();

        // Собираем абсолютно все карты со стола
        foreach (var kvp in initialBoardState)
        {
            var card = kvp.Key;
            var initialSlot = kvp.Value;

            if (card == null) continue; // Защита от удаленных карт

            allCards.Add(card);
            startPositions.Add(card.transform.position);
            targetSlots.Add(initialSlot);

            // ИСПРАВЛЕНИЕ: Ищем слот логически, а не через иерархию, чтобы избежать багов, если карта была в полете
            var currentSlot = pileManager.Slots.FirstOrDefault(s => s.GetTopCard() == card);
            if (currentSlot != null) currentSlot.RemoveCard(card);

            card.transform.SetParent(DragLayer, true);

            var mCard = card.GetComponent<MontanaCardController>();
            if (mCard != null)
            {
                mCard.SetAnimating(true);
                mCard.SetLockedState(false); // Делаем белой в полете
            }
        }

        yield return new WaitForSeconds(0.1f);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", 0.5f, 0.1f);

        // All 52 cards gather at the deck position
        Vector3 gatherPos = pileManager.Slots[55].Transform.position;
        float gatherDuration = 0.5f;
        float gatherElapsed = 0f;

        while (gatherElapsed < gatherDuration)
        {
            // ИСПРАВЛЕНИЕ: Добавлено прибавление времени. Без него цикл был бесконечным!
            gatherElapsed += Time.deltaTime;

            float t = Mathf.Clamp01(gatherElapsed / gatherDuration);
            t = t * t * (3f - 2f * t);

            for (int i = 0; i < allCards.Count; i++)
            {
                allCards[i].transform.position = Vector3.Lerp(startPositions[i], gatherPos, t);
            }
            yield return null;
        }

        foreach (var c in allCards) c.transform.position = gatherPos;

        yield return new WaitForSeconds(0.2f);

        float dealSpeed = 0.015f;
        float cardMoveDuration = 0.25f;

        var sortedIndices = Enumerable.Range(0, allCards.Count)
            .OrderBy(i => pileManager.Slots.IndexOf(targetSlots[i]))
            .ToList();

        for (int i = 0; i < sortedIndices.Count; i++)
        {
            var card = allCards[sortedIndices[i]];
            var targetSlot = targetSlots[sortedIndices[i]];

            StartCoroutine(UndoMoveCardToSlotRoutine(card, targetSlot, cardMoveDuration));
            yield return new WaitForSeconds(dealSpeed);
        }

        yield return new WaitForSeconds(cardMoveDuration);

        // Сброс статистики
        undoStack.Clear();
        CurrentReshufflesLeft = MaxReshuffles;
        scoreManager.ResetScore();
        if (puzzleManager != null) puzzleManager.ResetCheckpoints();

        RegisterMoveAndStartIfNeeded();
        UpdateLockedCards();

        isUndoing = false;
        IsInputAllowed = true;
        UpdateUndoButton();
        UpdateFullUI();

        CheckGameState();
    }

    // Вспомогательная корутина для анимации полета одной карты из стопки на стол
    private IEnumerator UndoMoveCardToSlotRoutine(CardController card, MontanaSlot targetSlot, float duration)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

        Vector3 startPos = card.transform.position;
        Vector3 endPos = targetSlot.Transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);
            card.transform.position = Vector3.Lerp(startPos, endPos, easedT);
            yield return null;
        }

        card.transform.position = endPos;
        targetSlot.AcceptCard(card);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");

        var mCard = card.GetComponent<MontanaCardController>();
        if (mCard != null) mCard.SetAnimating(false);
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // Запоминаем источник на всякий случай
        lastInteractionSource = card.GetComponentInParent<ICardContainer>();

        // На мобилках и планшетах запускаем авто-перенос
        if (GameSettings.AutoMoveClickMode == 0)
        {
            // Защита от микро-свайпов: отменяем технический захват карты
            var mCard = card as MontanaCardController;
            if (mCard != null && mCard.transform.parent == dragLayer)
            {
                mCard.StopAllCoroutines();
                mCard.SetAnimating(false);
                if (mCard.OriginalParent != null)
                {
                    mCard.transform.SetParent(mCard.OriginalParent, true);
                    mCard.transform.localPosition = mCard.OriginalLocalPosition;
                    mCard.transform.SetSiblingIndex(mCard.OriginalSiblingIndex);
                }
            }

            ExecuteAutoMove(card);
        }

        // ВНИМАНИЕ: Блок else для ПК удален! 
        // Одинарный клик на ПК ничего не делает, поэтому бесконечного цикла больше не будет.
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // На ПК авто-перенос срабатывает только по двойному клику
        if (GameSettings.AutoMoveClickMode == 1)
        {
            ExecuteAutoMove(card);
        }
    }

    private void ExecuteAutoMove(CardController card)
    {
        lastInteractionSource = card.GetComponentInParent<ICardContainer>();
        // Передаем управление сервису авто-хода (он сам найдет пустое место)
        autoMoveService?.OnCardRightClicked(card);
    }

    public void OnCardLongPressed(CardController card) => dragManager?.OnCardLongPressed(card);

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
        card.CardmodeManager = this;
        card.dragManager = dragManager;
    }

    private void UpdateFullUI()
    {
        if (scoreText != null)
            scoreText.text = scoreManager.CurrentScore.ToString();

        if (movesText != null)
            movesText.text = (!hasGameStarted) ? "0" : (StatisticsManager.Instance != null ? StatisticsManager.Instance.GetCurrentMoves().ToString() : "0");

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
    private bool IsSlotInCorrectChain(int row, int col)
    {
        // Проверяем всю цепочку от начала ряда до текущей выбранной колонки
        for (int c = 0; c <= col; c++)
        {
            var slot = pileManager.GetSlot(row, c);
            var card = slot?.GetTopCard();
            if (card == null) return false;

            if (c == 0)
            {
                // Самая первая карта в ряду (колонка 0) обязана быть Тузом (ранг 1)
                if (card.cardModel.rank != 1) return false;
            }
            else
            {
                var prevSlot = pileManager.GetSlot(row, c - 1);
                var prevCard = prevSlot?.GetTopCard();
                if (prevCard == null) return false;

                // Каждая следующая карта должна строго совпадать по масти и быть на 1 ранг старше
                if (card.cardModel.suit != prevCard.cardModel.suit || card.cardModel.rank != prevCard.cardModel.rank + 1)
                {
                    return false;
                }
            }
        }
        return true;
    }
    #endregion
}

public class MontanaMoveRecord
{
    public bool IsReshuffle;
    public CardController Card;
    public MontanaSlot SourceSlot;
    public MontanaSlot TargetSlot;
    public Dictionary<CardController, MontanaSlot> BoardState;
    public bool CompletedRow;
    public bool WasCorrectChain; // <--- ДОБАВЛЕНО: Была ли карта частью правильного ряда
}