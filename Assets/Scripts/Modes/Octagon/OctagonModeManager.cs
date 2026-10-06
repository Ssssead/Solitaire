using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using YG;

public class OctagonModeManager : MonoBehaviour, ICardGameMode, IModeManager, ICardClickReceiver
{
    [Header("Managers")]
    [SerializeField] private OctagonDeckManager deckManager;
    [SerializeField] private OctagonPileManager pileManager;
    [SerializeField] private GameUIController gameUI;
    [SerializeField] private OctagonAnimationService animationService;
    public OctagonTutorialManager tutorialManager;

    [Header("UI Controls")]
    [SerializeField] private Button undoButton;
    [SerializeField] private Button undoAllButton;

    // --- ���������: ������� ������ ---
    [Header("UI & HUD - Landscape")]
    public TMP_Text movesText;
    public TMP_Text timeText;
    public TMP_Text scoreText;

    [Header("UI & HUD - Portrait")]
    public TMP_Text portraitMovesText;
    public TMP_Text portraitTimeText;
    public TMP_Text portraitScoreText;

    [Header("Setup")]
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private Canvas rootCanvas;

    [Header("Game Rules")]
    [SerializeField] private int maxRecycles = 2;
    private int recyclesUsed = 0;
    private bool _isGameFinished = false;
    private bool _isGameWon = false;
    public Difficulty CurrentDifficulty => GameSettings.CurrentDifficulty;
    public GameType GameType => GameType.Octagon;

    private Stack<OctagonMoveRecord> undoStack = new Stack<OctagonMoveRecord>();
    private bool isUndoing = false;
    public ITutorialManager Tutorial => tutorialManager;
    public OctagonIntroController introController;
    public bool playIntroOnStart = true;
    private bool isRestarting = false;

    private float gameTimer = 0f;
    private bool isTimerRunning = false;
    private bool hasGameStarted = false;

    private int currentScore = 0;
    private int foundationCombo = 0;
    private Stack<int> scoreHistory = new Stack<int>();
    private Coroutine defeatRoutine;

    // --- ������������ ���������� ---
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    [Header("Hint System")]
    public OctagonHintSolver hintSolver;
    private Coroutine backgroundSolverCoroutine = null;
    private List<OctagonHintMove> cachedHintPath = null;
    [HideInInspector] public bool isExecutingHint = false;
    public int CurrentScore => currentScore;
    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => null;
    public OctagonAnimationService OctagonAnim => animationService;
    public PileManager PileManager => pileManager;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 25f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public bool IsInputAllowed { get; set; } = false;
    public string GameName => "Octagon";

    private struct OctagonQuestUndoRecord
    {
        public bool IsToFoundation;
        public bool FromWasteToFoundation;
        public bool FromTableauToFoundation;
        public int CardRank;
    }
    private Stack<OctagonQuestUndoRecord> questUndoStack = new Stack<OctagonQuestUndoRecord>();

    private void Start()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        if (deckManager == null) deckManager = GetComponent<OctagonDeckManager>();
        if (pileManager == null) pileManager = GetComponent<OctagonPileManager>();
        if (introController == null) introController = GetComponent<OctagonIntroController>();

        if (animationService == null)
        {
            animationService = GetComponent<OctagonAnimationService>();
            if (animationService == null) animationService = gameObject.AddComponent<OctagonAnimationService>();
        }

        if (undoButton) undoButton.onClick.AddListener(OnUndoAction);
        if (undoAllButton) LongPressHoldTrigger.SubscribeToButton(undoAllButton, OnUndoAllAction);

        FixGameUIReferences();
        StartCoroutine(InterceptDefeatUndoButton());

        StartNewGame();
    }

    private void FixGameUIReferences()
    {
        if (gameUI == null) return;
        var field = gameUI.GetType().GetField("activeGameMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(gameUI, this);
    }

    private IEnumerator InterceptDefeatUndoButton()
    {
        yield return new WaitForSeconds(0.5f);
        if (gameUI != null && gameUI.defeatPanel != null)
        {
            Button[] buttons = gameUI.defeatPanel.GetComponentsInChildren<Button>(true);
            foreach (Button b in buttons)
            {
                bool isUndo = false;
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    if (b.onClick.GetPersistentMethodName(i) == "OnUndoOneClicked")
                    {
                        isUndo = true;
                        break;
                    }
                }
                if (!isUndo && b.gameObject.name.IndexOf("undo", StringComparison.OrdinalIgnoreCase) >= 0) isUndo = true;

                if (isUndo)
                {
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(() =>
                    {
                        gameUI.defeatPanel.SetActive(false);
                        OnUndoAction();
                    });
                }
            }
        }
    }

    public void StartNewGame()
    {
        // ---> ���������: ������� �������� ��������� <---
        if (hintSolver != null) hintSolver.CancelSearch();
        cachedHintPath = null;
        isExecutingHint = false;

        if (hasGameStarted && !_isGameWon && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }
        // ДОБАВИТЬ ЭТУ СТРОКУ:
        SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());

        GameQuestTracker.Instance?.StartMatch("Octagon", GameSettings.CurrentDifficulty, "Standard");

        if (defeatRoutine != null)
        {
            StopCoroutine(defeatRoutine);
            defeatRoutine = null;
        }

        hasGameStarted = false;
        _isGameFinished = false;
        _isGameWon = false;
        IsInputAllowed = false;
        recyclesUsed = 0;
        undoStack.Clear();

        currentScore = 0;
        foundationCombo = 0;
        scoreHistory.Clear();

        gameTimer = 0f;
        isTimerRunning = false;
        UpdateFullUI();

        if (introController != null) introController.PrepareIntro(isRestarting);
        if (deckManager != null) deckManager.ClearBoard();

        Deal dealToPlay = null;

        if (GameSettings.IsTutorialMode && tutorialManager != null)
        {
            dealToPlay = tutorialManager.GenerateTutorialDeal();
        }
        else if (DealCacheSystem.Instance != null)
        {
            dealToPlay = DealCacheSystem.Instance.GetDeal(GameType.Octagon, CurrentDifficulty, 0);
        }

        StartCoroutine(IntroSequenceRoutine(dealToPlay));
    }

    private IEnumerator IntroSequenceRoutine(Deal deal)
    {
        yield return null;

        if (playIntroOnStart && introController != null)
        {
            yield return StartCoroutine(introController.PlayIntroSequence(isRestarting));
        }

        if (deal != null && deckManager != null)
        {
            if (introController != null) deckManager.isSkippingIntro = introController.isSkipping;
            deckManager.ApplyDeal(deal);
        }
        else
        {
            IsInputAllowed = true;
        }

        isRestarting = false;
    }

    public bool IsMatchInProgress()
    {
        return hasGameStarted;
    }

    private void Update()
    {
        // --- ������ �������� ��� �������� ������ ---
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            StartCoroutine(DelayedLayoutFixRoutine());
        }

        if (!GameSettings.IsTutorialMode || tutorialManager == null || !tutorialManager.IsTutorialActive)
        {
            if (undoButton) undoButton.interactable = undoStack.Count > 0 && !isUndoing;
            if (undoAllButton) undoAllButton.interactable = undoStack.Count > 0 && !isUndoing;
        }

        if (isTimerRunning && !_isGameFinished)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }
    }

    private IEnumerator DelayedLayoutFixRoutine()
    {
        // ���� 3 �����, ���� GameLayoutManager ���������� �����
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();

        // 1. ��������� ������� �������� ������ ����
        var factory = FindObjectOfType<CardFactory>();
        if (factory != null)
        {
            factory.UpdateAllCardsSize();
        }

        // 2. ��������������� ������ (�������) ������, ��� ��� ����� �������� ������
        if (pileManager != null)
        {
            if (pileManager.StockPile != null)
            {
                for (int i = 0; i < pileManager.StockPile.transform.childCount; i++)
                {
                    Transform t = pileManager.StockPile.transform.GetChild(i);
                    t.localPosition = new Vector3(i * pileManager.StockPile.offsetX, i * pileManager.StockPile.offsetY, 0f);
                }
            }
            if (pileManager.WastePile != null)
            {
                for (int i = 0; i < pileManager.WastePile.transform.childCount; i++)
                {
                    Transform t = pileManager.WastePile.transform.GetChild(i);
                    t.localPosition = new Vector3(i * pileManager.WastePile.offsetX, i * pileManager.WastePile.offsetY, 0f);
                }
            }
        }
    }

    private void UpdateTimeUI()
    {
        int totalSeconds = Mathf.FloorToInt(gameTimer);
        string tText = string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);

        if (timeText != null) timeText.text = tText;
        if (portraitTimeText != null) portraitTimeText.text = tText;
    }

    private void UpdateFullUI()
    {
        string mText = "0";
        if (hasGameStarted && StatisticsManager.Instance != null)
            mText = StatisticsManager.Instance.GetCurrentMoves().ToString();

        if (movesText != null) movesText.text = mText;
        if (portraitMovesText != null) portraitMovesText.text = mText;

        if (!GameSettings.IsTutorialMode || tutorialManager == null || !tutorialManager.IsTutorialActive)
        {
            if (undoButton != null) undoButton.interactable = (undoStack != null && undoStack.Count > 0);
            if (undoAllButton != null) undoAllButton.interactable = (undoStack != null && undoStack.Count > 0);
        }

        string sText = currentScore.ToString();
        if (scoreText != null) scoreText.text = sText;
        if (portraitScoreText != null) portraitScoreText.text = sText;

        UpdateTimeUI();
    }

    public void RegisterMoveAndStartIfNeeded()
    {
        if (!IsInputAllowed && !isExecutingHint) return;

        GameQuestTracker.Instance?.RecordMove();

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true;

            if (StatisticsManager.Instance != null)
            {
                StatisticsManager.Instance.OnGameStarted(GameName, CurrentDifficulty, "Classic");
            }
            // ДОБАВИТЬ ЭТУ СТРОКУ:
            SimpleMetricsTracker.Instance?.TrackLevelStart(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }

        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.RegisterMove();
        }

        UpdateFullUI();
    }

    private void OnCardPlacedToFoundation()
    {
        foundationCombo++;
        currentScore += 5 * foundationCombo;
        UpdateFullUI();
    }

    private void ResetFoundationCombo()
    {
        foundationCombo = 0;
    }

    public void OnStockClicked()
    {
        if ((!IsInputAllowed && !isExecutingHint) || _isGameFinished || isUndoing) return;

        GameQuestTracker.Instance?.RecordStockDraw();

        if (pileManager.StockPile.CardCount == 0)
        {
            if (recyclesUsed >= maxRecycles)
            {
                CheckGameState();
                return;
            }
        }
        if (pileManager.StockPile.CardCount > 0)
        {
            int savedScore = currentScore;
            ResetFoundationCombo();

            RegisterMoveAndStartIfNeeded();
            StartCoroutine(AnimateStockToWaste(savedScore));
        }
        else if (pileManager.WastePile.CardCount > 0)
        {
            if (recyclesUsed < maxRecycles)
            {
                int savedScore = currentScore;
                ResetFoundationCombo();

                RegisterMoveAndStartIfNeeded();
                recyclesUsed++;
                StartCoroutine(AnimateRecycle(savedScore));
            }
        }
    }

    public void OnCardCreate(CardController card) { }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed || _isGameFinished || isUndoing) return;

        if (GameSettings.AutoMoveClickMode == 1)
        {
            ProcessAutoMove(card);
        }
    }

    private void ProcessAutoMove(CardController card)
    {
        var data = card.GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) return;

        if (!IsCardMovable(card))
        {
            return;
        }

        foreach (var f in pileManager.FoundationPiles)
        {
            if (f.CanAccept(card))
            {
                int savedScore = currentScore;
                StartCoroutine(AnimateAutoMove(card, f, savedScore));
                return;
            }
        }
        foreach (var group in pileManager.TableauGroups)
        {
            foreach (var slot in group.Slots)
            {
                if (card.transform.parent == slot.transform) break;
                if (slot.CanAccept(card))
                {
                    var top = slot.GetTopCard();
                    if (top != null && top.cardModel.suit == card.cardModel.suit)
                    {
                        int savedScore = currentScore;
                        StartCoroutine(AnimateAutoMove(card, slot, savedScore));
                        return;
                    }
                }
                if (slot.transform.childCount > 0) break;
            }
        }

        StartCoroutine(animationService.AnimateShake(card));
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        int savedScore = currentScore;

        var octCard = card as OctagonCardController;
        ICardContainer source = octCard != null ? octCard.SourceContainer : card.GetComponentInParent<ICardContainer>();

        if (source == container)
        {
            StartCoroutine(PlayDelayedSound("Card_Drop_Fail", 0.2f));
            return;
        }

        RegisterMoveAndStartIfNeeded();

        OctagonMoveRecord record = new OctagonMoveRecord();
        bool wasFaceUp = true;
        record.AddMove(card, source, container, wasFaceUp);

        if (container is OctagonFoundationPile)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Foundation_Success");
            OnCardPlacedToFoundation();
        }
        else
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");
            ResetFoundationCombo();
        }

        CheckRevealCardUnder(source, record, card);

        if (!GameSettings.IsTutorialMode)
        {
            bool isFromCorner = source is OctagonTableauSlot || source is OctagonTableauGroup;
            bool isFromWaste = source is OctagonWastePile;

            if (isFromCorner && (container is OctagonTableauSlot || container is OctagonTableauGroup))
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveTableauToTableau, 1);

            if (container is OctagonFoundationPile)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 1);
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());

                if (isFromWaste) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, 1);
                else if (isFromCorner) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, 1);
            }

            if (record.RevealedCard != null) GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, 1);

            if (source is OctagonTableauSlot slot && slot.Group != null)
            {
                slot.Group.UpdateTopCardState();
                if (slot.Group.IsEmpty()) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, 1);
            }
        }

        StartCoroutine(CheckRefillAndFinalizeMove(record, savedScore));
    }

    private IEnumerator PlayDelayedSound(string soundName, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(soundName);
        }
    }

    private void CheckRevealCardUnder(ICardContainer source, OctagonMoveRecord record, CardController movingCard)
    {
        if (source is OctagonTableauSlot sourceSlot && sourceSlot.Group != null)
        {
            var group = sourceSlot.Group;
            CardController candidateToReveal = null;

            for (int i = 0; i < group.Slots.Count; i++)
            {
                var slot = group.Slots[i];
                var cardInSlot = slot.GetTopCard();

                if (cardInSlot != null)
                {
                    if (cardInSlot == movingCard) continue;
                    candidateToReveal = cardInSlot;
                    break;
                }
            }

            if (candidateToReveal != null)
            {
                var data = candidateToReveal.GetComponent<CardData>();
                if (data != null && !data.IsFaceUp())
                {
                    record.RevealedCard = candidateToReveal;
                }
            }
        }
    }

    private IEnumerator CheckRefillAndFinalizeMove(OctagonMoveRecord currentRecord, int savedScore)
    {
        IsInputAllowed = false;
        yield return new WaitForSeconds(0.1f);

        foreach (var group in pileManager.TableauGroups)
        {
            if (group.IsEmpty())
            {
                if (pileManager.StockPile.CardCount + pileManager.WastePile.CardCount > 0)
                {
                    yield return StartCoroutine(RefillGroupRoutine(group, currentRecord));
                    break;
                }
            }
        }

        scoreHistory.Push(savedScore);
        undoStack.Push(currentRecord);
        IsInputAllowed = true;

        CheckGameState();

        // ---> ���������: ��������� ������ ��������� � ���� ����� ���� <---
        isExecutingHint = false;
        StartBackgroundSolver();
    }

    private IEnumerator RefillGroupRoutine(OctagonTableauGroup group, OctagonMoveRecord record)
    {
        List<(CardController card, ICardContainer source)> moveList = new List<(CardController, ICardContainer)>();
        int needed = 5;

        while (moveList.Count < needed && pileManager.StockPile.CardCount > 0)
        {
            var c = pileManager.StockPile.PopTopCard();
            if (c != null) moveList.Add((c, pileManager.StockPile));
            else break;
        }

        while (moveList.Count < needed && pileManager.WastePile.CardCount > 0)
        {
            var c = pileManager.WastePile.PopBottomCard();
            if (c != null) moveList.Add((c, pileManager.WastePile));
            else break;
        }

        if (moveList.Count == 0) yield break;

        int maxSlots = group.Slots.Count;
        float duration = 0.3f;

        for (int i = 0; i < moveList.Count; i++)
        {
            var item = moveList[i];
            var card = item.card;
            var source = item.source;

            int targetSlotIndex = (maxSlots - 1) - i;
            if (targetSlotIndex >= 0 && targetSlotIndex < group.Slots.Count)
            {
                var targetSlot = group.Slots[targetSlotIndex];
                bool isLastOne = (i == moveList.Count - 1);
                bool wasFaceUpInSource = (source is OctagonWastePile);

                record.AddMove(card, source, targetSlot, wasFaceUpInSource);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySound("Card_Flip");
                }

                StartCoroutine(animationService.AnimateMoveCard(
                    card, targetSlot.Transform, Vector3.zero, duration, isLastOne,
                    () =>
                    {
                        targetSlot.AcceptCard(card);

                        if (AudioManager.Instance != null)
                        {
                            AudioManager.Instance.PlaySound("Card_Drop_Success");
                        }

                        var cg = card.GetComponent<CanvasGroup>();
                        if (cg) cg.blocksRaycasts = isLastOne;
                    }
                ));

                yield return new WaitForSeconds(0.1f);
            }
        }

        yield return new WaitForSeconds(duration);
    }

    public void OnUndoAction()
    {
        if (isUndoing || undoStack.Count == 0) return;

        if (_isGameFinished)
        {
            _isGameFinished = false;
            isTimerRunning = true;
        }
        else if (!IsInputAllowed)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Back");
        }

        GameQuestTracker.Instance?.RecordUndoUsed();

        RegisterMoveAndStartIfNeeded();
        StartCoroutine(UndoAnimatedRoutine());
    }

    public void OnUndoAllAction()
    {
        if (isUndoing || undoStack.Count == 0) return;

        if (_isGameFinished)
        {
            _isGameFinished = false;
            isTimerRunning = true;
        }
        else if (!IsInputAllowed)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Back");
            AudioManager.Instance.PlaySound("Card_Whoosh_Out");
        }

        GameQuestTracker.Instance?.RecordUndoUsed();

        RegisterMoveAndStartIfNeeded();

        IsInputAllowed = false;
        isUndoing = true;

        while (undoStack.Count > 0)
        {
            OctagonMoveRecord record = undoStack.Pop();
            if (scoreHistory.Count > 0) currentScore = scoreHistory.Pop();
            PerformImmediateUndo(record);
        }

        ResetFoundationCombo();

        foreach (var g in pileManager.TableauGroups) g.UpdateTopCardState();

        isUndoing = false;
        IsInputAllowed = true;
        UpdateFullUI();

        // ---> ���������: ���� ���� ��� ���������� ����� <---
        StartBackgroundSolver();
    }

    private void PerformImmediateUndo(OctagonMoveRecord record)
    {
        if (record.SubMoves.Count > 0)
        {
            var firstMove = record.SubMoves[0];
            if (firstMove.Source == pileManager.WastePile && firstMove.Target == pileManager.StockPile)
                recyclesUsed = Mathf.Max(0, recyclesUsed - 1);
        }

        if (record.RevealedCard != null)
        {
            GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, -1);
            var data = record.RevealedCard.GetComponent<CardData>();
            if (data != null) data.SetFaceUp(false, false);
        }

        for (int i = record.SubMoves.Count - 1; i >= 0; i--)
        {
            var move = record.SubMoves[i];
            CardController card = move.Card;
            ICardContainer targetContainer = move.Source;
            ICardContainer currentContainer = card.GetComponentInParent<ICardContainer>();

            if (targetContainer == null || card == null) continue;

            if (currentContainer is OctagonFoundationPile)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, -1);
                if (card.cardModel.rank > 0)
                {
                    GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(card.cardModel.rank);
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -1, card.cardModel.rank.ToString());
                }

                if (targetContainer is OctagonWastePile)
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, -1);
                else if (targetContainer is OctagonTableauSlot || targetContainer is OctagonTableauGroup)
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, -1);
            }

            if (targetContainer is OctagonTableauSlot tsAntiCheat && tsAntiCheat.Group != null && tsAntiCheat.Group.IsEmpty())
                GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, -1);

            bool targetFaceUp = move.WasFaceUp;
            if (targetContainer is OctagonStockPile) targetFaceUp = false;
            else if (targetContainer is OctagonWastePile) targetFaceUp = true;

            Vector3 targetLocalPos = Vector3.zero;

            if (targetContainer is OctagonStockPile spPos)
                targetLocalPos = new Vector3(spPos.CardCount * spPos.offsetX, spPos.CardCount * spPos.offsetY, 0f);
            else if (targetContainer is OctagonWastePile wpPos)
                targetLocalPos = new Vector3(wpPos.CardCount * wpPos.offsetX, wpPos.CardCount * wpPos.offsetY, 0f);
            else
                targetLocalPos = targetContainer.GetDropAnchoredPosition(card);

            card.transform.SetParent(targetContainer.Transform);
            card.rectTransform.anchoredPosition = targetLocalPos;
            card.transform.localRotation = Quaternion.identity;

            targetContainer.AcceptCard(card);

            if (targetContainer is OctagonTableauSlot tsLayout) tsLayout.UpdateLayout();
            else if (targetContainer is OctagonWastePile wpLayout) wpLayout.UpdateLayout();

            if (currentContainer is OctagonWastePile oldWp) oldWp.UpdateLayout();
            else if (currentContainer is OctagonTableauSlot oldTs) oldTs.UpdateLayout();

            var data = card.GetComponent<CardData>();
            if (data) data.SetFaceUp(targetFaceUp, false);

            var cg = card.GetComponent<CanvasGroup>();
            if (cg) cg.blocksRaycasts = move.WasRaycastBlocked;
        }
    }

    private IEnumerator UndoAnimatedRoutine()
    {
        isUndoing = true;
        IsInputAllowed = false;

        GameQuestTracker.Instance?.RecordUndoUsed();

        if (scoreHistory.Count > 0) currentScore = scoreHistory.Pop();
        ResetFoundationCombo();
        UpdateFullUI();

        OctagonMoveRecord record = undoStack.Pop();

        if (record.SubMoves.Count > 0)
        {
            var firstMove = record.SubMoves[0];
            if (firstMove.Source == pileManager.WastePile && firstMove.Target == pileManager.StockPile)
                recyclesUsed = Mathf.Max(0, recyclesUsed - 1);
        }

        if (record.RevealedCard != null)
        {
            GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, -1);
            var data = record.RevealedCard.GetComponent<CardData>();
            if (data != null) data.SetFaceUp(false, true);
        }

        float undoMoveDuration = 0.25f;
        float undoInterval = 0.08f;

        for (int i = record.SubMoves.Count - 1; i >= 0; i--)
        {
            var move = record.SubMoves[i];
            CardController card = move.Card;
            ICardContainer targetContainer = move.Source;
            ICardContainer currentContainer = card.GetComponentInParent<ICardContainer>();

            if (targetContainer == null || card == null) continue;

            if (currentContainer is OctagonFoundationPile)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, -1);
                if (card.cardModel.rank > 0)
                {
                    GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(card.cardModel.rank);
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -1, card.cardModel.rank.ToString());
                }

                if (targetContainer is OctagonWastePile)
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, -1);
                else if (targetContainer is OctagonTableauSlot || targetContainer is OctagonTableauGroup)
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, -1);
            }

            if (targetContainer is OctagonTableauSlot ts && ts.Group != null && ts.Group.IsEmpty())
                GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, -1);

            bool targetFaceUp = move.WasFaceUp;
            if (targetContainer is OctagonStockPile) targetFaceUp = false;
            else if (targetContainer is OctagonWastePile) targetFaceUp = true;

            Vector3 targetLocalPos = Vector3.zero;
            if (targetContainer is OctagonStockPile sp)
                targetLocalPos = new Vector3(sp.CardCount * sp.offsetX, sp.CardCount * sp.offsetY, 0f);
            else if (targetContainer is OctagonWastePile wp)
                targetLocalPos = new Vector3(wp.CardCount * wp.offsetX, wp.CardCount * wp.offsetY, 0f);
            else
                targetLocalPos = targetContainer.GetDropAnchoredPosition(card);


            bool isStockWasteMove = false;
            if (currentContainer != null)
            {
                isStockWasteMove = (targetContainer is OctagonStockPile || targetContainer is OctagonWastePile) &&
                                   (currentContainer is OctagonStockPile || currentContainer is OctagonWastePile);
            }

            if (AudioManager.Instance != null)
            {
                if (isStockWasteMove) AudioManager.Instance.PlaySound("Card_Flip");
                else AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            }

            StartCoroutine(animationService.AnimateMoveCard(
                card,
                targetContainer.Transform,
                targetLocalPos,
                undoMoveDuration,
                targetFaceUp,
                () =>
                {
                    targetContainer.AcceptCard(card);
                    var cg = card.GetComponent<CanvasGroup>();
                    if (cg) cg.blocksRaycasts = move.WasRaycastBlocked;

                    if (targetContainer is OctagonWastePile wp) wp.UpdateLayout();
                    if (targetContainer is OctagonTableauSlot tSlot) tSlot.UpdateLayout();

                    if (AudioManager.Instance != null && !isStockWasteMove)
                        AudioManager.Instance.PlaySound("Card_Drop_Success");
                }
            ));

            if (currentContainer is OctagonWastePile oldWp) oldWp.UpdateLayout();
            if (currentContainer is OctagonTableauSlot oldTs) oldTs.UpdateLayout();

            yield return new WaitForSeconds(undoInterval);
        }

        yield return new WaitForSeconds(undoMoveDuration);
        foreach (var g in pileManager.TableauGroups) g.UpdateTopCardState();

        IsInputAllowed = true;
        isUndoing = false;

        // ---> ���������: ���� ���� ��� ������������ ����� <---
        StartBackgroundSolver();
    }

    private IEnumerator AnimateStockToWaste(int savedScore)
    {
        IsInputAllowed = false;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Card_Flip");
        }

        var card = pileManager.StockPile.PopTopCard();
        if (card != null)
        {
            OctagonMoveRecord record = new OctagonMoveRecord();
            record.AddMove(card, pileManager.StockPile, pileManager.WastePile, false);

            scoreHistory.Push(savedScore);
            undoStack.Push(record);

            int nextWasteIndex = pileManager.WastePile.CardCount;
            Vector3 targetLocalPos = new Vector3(
                nextWasteIndex * pileManager.WastePile.offsetX,
                nextWasteIndex * pileManager.WastePile.offsetY,
                0f
            );

            yield return StartCoroutine(animationService.AnimateMoveCard(
                card,
                pileManager.WastePile.transform,
                targetLocalPos,
                0.25f,
                true,
                () =>
                {
                    pileManager.WastePile.AddCard(card);
                    CheckGameState();
                }
            ));
        }
        IsInputAllowed = true;

        // ---> ���������: ��������� ������ ��������� � ���� ����� ���� <---
        isExecutingHint = false;
        StartBackgroundSolver();
    }

    private IEnumerator AnimateRecycle(int savedScore)
    {
        IsInputAllowed = false;
        OctagonMoveRecord record = new OctagonMoveRecord();
        var cards = new List<CardController>(pileManager.WastePile.GetComponentsInChildren<CardController>());

        int virtualStockCount = pileManager.StockPile.CardCount;

        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var card = cards[i];
            record.AddMove(card, pileManager.WastePile, pileManager.StockPile, true);

            Vector3 targetLocalPos = new Vector3(
                virtualStockCount * pileManager.StockPile.offsetX,
                virtualStockCount * pileManager.StockPile.offsetY,
                0f
            );
            virtualStockCount++;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound("Card_Flip");
            }

            StartCoroutine(animationService.AnimateMoveCard(
                card, pileManager.StockPile.transform, targetLocalPos, 0.15f, false,
                () => { pileManager.StockPile.AddCard(card); }
            ));
            yield return new WaitForSeconds(0.02f);
        }

        scoreHistory.Push(savedScore);
        undoStack.Push(record);

        yield return new WaitForSeconds(0.3f);
        pileManager.WastePile.UpdateLayout();

        IsInputAllowed = true;
        CheckGameState();

        // ---> ���������: ��������� ������ ��������� � ���� ����� ���� <---
        isExecutingHint = false;
        StartBackgroundSolver();
    }

    private IEnumerator AnimateAutoMove(CardController card, ICardContainer target, int savedScore)
    {
        RegisterMoveAndStartIfNeeded();
        IsInputAllowed = false;

        ICardContainer source = card.GetComponentInParent<ICardContainer>();
        if (source == null && card is OctagonCardController octCard) source = octCard.SourceContainer;

        OctagonMoveRecord record = new OctagonMoveRecord();
        bool wasFaceUp = true;
        record.AddMove(card, source, target, wasFaceUp);

        CheckRevealCardUnder(source, record, card);

        Vector3 targetLocalPos = target.GetDropAnchoredPosition(card);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Whoosh_Out");

        yield return StartCoroutine(animationService.AnimateMoveCard(
            card, target.Transform, targetLocalPos, 0.2f, true,
            () =>
            {
                target.AcceptCard(card);

                if (target is OctagonFoundationPile)
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Foundation_Success");
                    OnCardPlacedToFoundation();
                }
                else
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");
                    ResetFoundationCombo();
                }

                if (!GameSettings.IsTutorialMode)
                {
                    bool isFromCorner = source is OctagonTableauSlot || source is OctagonTableauGroup;
                    bool isFromWaste = source is OctagonWastePile;

                    if (target is OctagonFoundationPile)
                    {
                        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 1);
                        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());

                        if (isFromWaste) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, 1);
                        else if (isFromCorner) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, 1);
                    }

                    if (record.RevealedCard != null) GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, 1);

                    if (source is OctagonTableauSlot slot && slot.Group != null)
                    {
                        slot.Group.UpdateTopCardState();
                        if (slot.Group.IsEmpty()) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, 1);
                    }
                }

                StartCoroutine(CheckRefillAndFinalizeMove(record, savedScore));
            }
        ));
    }

    public ICardContainer FindNearestContainer(CardController card, Vector2 screenPos, float maxDistance)
    {
        Rect cardRect = GetWorldRect(card.rectTransform);
        ICardContainer best = null;
        float bestArea = 0f;
        List<ICardContainer> all = new List<ICardContainer>();
        all.AddRange(pileManager.FoundationPiles);
        foreach (var g in pileManager.TableauGroups) all.AddRange(g.Slots);

        foreach (var c in all)
        {
            var mono = c as MonoBehaviour;
            if (mono == null) continue;
            Rect targetRect;
            CardController top = null;
            if (c is OctagonTableauSlot ts) top = ts.GetTopCard();
            else if (c is OctagonFoundationPile fp) top = fp.GetTopCard();

            if (top != null) targetRect = GetWorldRect(top.rectTransform);
            else targetRect = GetWorldRect(mono.transform as RectTransform);

            float area = GetIntersectionArea(cardRect, targetRect);
            if (area > bestArea && c.CanAccept(card)) { bestArea = area; best = c; }
        }
        return best;
    }

    private bool IsCardMovable(CardController card)
    {
        Transform p = card.transform.parent;
        if (p == null || p.GetComponent<OctagonStockPile>()) return false;
        return p.GetChild(p.childCount - 1) == card.transform;
    }

    private Rect GetWorldRect(RectTransform rt) { Vector3[] c = new Vector3[4]; rt.GetWorldCorners(c); return new Rect(c[0].x, c[0].y, Mathf.Abs(c[2].x - c[0].x), Mathf.Abs(c[2].y - c[0].y)); }
    private float GetIntersectionArea(Rect r1, Rect r2) { float w = Mathf.Min(r1.xMax, r2.xMax) - Mathf.Max(r1.xMin, r2.xMin); float h = Mathf.Min(r1.yMax, r2.yMax) - Mathf.Max(r1.yMin, r2.yMin); return (w > 0 && h > 0) ? w * h : 0f; }

    public void CheckGameState()
    {
        if (_isGameFinished) return;

        int k = 0;
        foreach (var f in pileManager.FoundationPiles)
        {
            if (f.GetTopCard()?.cardModel.rank == 13) k++;
        }

        if (k == 8)
        {
            _isGameFinished = true;
            _isGameWon = true;
            isTimerRunning = false;
            undoStack.Clear();

            int finalMoves = 0;
            if (GameSettings.IsTutorialMode && tutorialManager != null)
            {
                string victoryText = "<color=#FCA311>�����������!</color> �� ������� ������ �������� � ������� ������� ��������������!";
                tutorialManager.ShowVictoryStep(victoryText);
            }
            if (StatisticsManager.Instance != null)
            {
                finalMoves = StatisticsManager.Instance.GetCurrentMoves();
                StatisticsManager.Instance.OnGameWon(currentScore);
            }
            // ДОБАВИТЬ ЭТУ СТРОКУ:
            SimpleMetricsTracker.Instance?.TrackLevelWin(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());

            if (gameUI)
            {
                gameUI.OnGameWon(finalMoves);
                if (gameUI.winScoreText != null)
                {
                    gameUI.winScoreText.text = currentScore.ToString();
                }
            }
            return;
        }

        if (defeatRoutine != null) StopCoroutine(defeatRoutine);
        defeatRoutine = StartCoroutine(ShowDefeatRoutine());
    }

    private IEnumerator ShowDefeatRoutine()
    {
        while (!IsInputAllowed || isUndoing || (dragLayer != null && dragLayer.childCount > 0))
        {
            yield return null;
        }

        yield return new WaitForSeconds(1.5f);
        if (_isGameFinished) yield break;

        if (!IsInputAllowed || isUndoing) yield break;

        if (!HasAnyValidMove() && gameUI != null)
        {
            _isGameFinished = true;
            isTimerRunning = false;
            IsInputAllowed = false;

            gameUI.OnGameLost();
        }

        defeatRoutine = null;
    }

    private bool HasAnyValidMove()
    {
        if (dragLayer != null && dragLayer.childCount > 0) return true;
        if (pileManager.StockPile.CardCount > 0) return true;
        if (pileManager.WastePile.CardCount > 0 && recyclesUsed < maxRecycles) return true;

        var topWaste = pileManager.WastePile.GetTopCard();
        if (topWaste != null)
        {
            var wasteData = topWaste.GetComponent<CardData>();
            if (wasteData != null && wasteData.IsFaceUp())
            {
                foreach (var f in pileManager.FoundationPiles)
                    if (f.CanAccept(topWaste)) return true;

                foreach (var group in pileManager.TableauGroups)
                    foreach (var slot in group.Slots)
                        if (slot.CanAccept(topWaste)) return true;
            }
        }

        foreach (var group in pileManager.TableauGroups)
        {
            int totalCardsInGroup = 0;
            foreach (var s in group.Slots) totalCardsInGroup += s.transform.childCount;

            foreach (var slot in group.Slots)
            {
                var topCard = slot.GetTopCard();
                if (topCard == null) continue;

                var topData = topCard.GetComponent<CardData>();
                if (topData == null || !topData.IsFaceUp()) continue;

                foreach (var f in pileManager.FoundationPiles)
                {
                    if (f.CanAccept(topCard)) return true;
                }

                bool isProgressiveMove = false;

                if (totalCardsInGroup == 1 && (pileManager.WastePile.CardCount > 0 || pileManager.StockPile.CardCount > 0))
                {
                    isProgressiveMove = true;
                }
                else if (slot.transform.childCount > 1)
                {
                    for (int i = slot.transform.childCount - 2; i >= 0; i--)
                    {
                        var underCard = slot.transform.GetChild(i).GetComponent<CardController>();
                        var underData = underCard?.GetComponent<CardData>();
                        if (underData == null) break;

                        if (!underData.IsFaceUp())
                        {
                            isProgressiveMove = true;
                            break;
                        }
                        else
                        {
                            foreach (var f in pileManager.FoundationPiles)
                            {
                                if (f.CanAccept(underCard))
                                {
                                    isProgressiveMove = true;
                                    break;
                                }
                            }
                            if (isProgressiveMove) break;
                        }
                    }
                }

                if (isProgressiveMove)
                {
                    foreach (var targetGroup in pileManager.TableauGroups)
                    {
                        foreach (var targetSlot in targetGroup.Slots)
                        {
                            if (slot == targetSlot) continue;
                            if (targetSlot.CanAccept(topCard)) return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private void OnDestroy()
    {
        if (hasGameStarted && !_isGameWon && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }
        // ДОБАВИТЬ ЭТУ СТРОКУ:
        SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
    }

    public bool OnDropToBoard(CardController c, Vector2 p)
    {
        StartCoroutine(PlayDelayedSound("Card_Drop_Fail", 0.2f));
        return false;
    }

    public void OnUndoActionDummy() { }
    public void RestartGame()
    {
        // ---> ���������: ������� �������� ��������� <---
        if (hintSolver != null) hintSolver.CancelSearch();
        cachedHintPath = null;
        isExecutingHint = false;

        isRestarting = true;
        StopAllCoroutines();
        StartNewGame();
    }
    public void TrackOctagonQuest(CardController card, ICardContainer source, ICardContainer container)
    {
        if (!IsInputAllowed || GameSettings.IsTutorialMode) return;

        bool isToFoundation = false;
        bool fromWasteToFoundation = false;
        bool fromTableauToFoundation = false;

        if (container is OctagonFoundationPile)
        {
            isToFoundation = true;
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 1);
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());

            if (source is OctagonWastePile)
            {
                fromWasteToFoundation = true;
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, 1);
            }
            else if (source is OctagonTableauSlot)
            {
                fromTableauToFoundation = true;
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, 1);
            }
        }

        if (source is OctagonFoundationPile && container != source)
        {
            GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(card.cardModel.rank);
            if (container is OctagonWastePile) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, -1);
            else if (container is OctagonTableauSlot) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, -1);
        }

        questUndoStack.Push(new OctagonQuestUndoRecord
        {
            IsToFoundation = isToFoundation,
            FromWasteToFoundation = fromWasteToFoundation,
            FromTableauToFoundation = fromTableauToFoundation,
            CardRank = card.cardModel.rank
        });
    }
    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed || _isGameFinished || isUndoing) return;

        if (GameSettings.AutoMoveClickMode == 0)
        {
            var octCard = card as OctagonCardController;
            if (octCard != null && octCard.transform.parent == dragLayer)
            {
                octCard.StopAllCoroutines();
                if (octCard.SourceContainer != null && octCard.SourceContainer.Transform != null)
                {
                    octCard.transform.SetParent(octCard.SourceContainer.Transform, true);
                    octCard.transform.localPosition = Vector3.zero;
                }
            }

            ProcessAutoMove(card);
        }
    }
    public void OnCardDoubleClicked(CardController c, bool b) { OnCardDoubleClicked(c); }
    public void OnCardLongPressed(CardController c) { }
    public void OnKeyboardPick(CardController c) { }
    public void StartBackgroundSolver()
    {
        if (backgroundSolverCoroutine != null) StopCoroutine(backgroundSolverCoroutine);
        if (hintSolver != null) hintSolver.CancelSearch();
        cachedHintPath = null;
        backgroundSolverCoroutine = StartCoroutine(BackgroundSolverRoutine());
    }

    private IEnumerator BackgroundSolverRoutine()
    {
        while ((dragLayer != null && dragLayer.childCount > 0) || !IsInputAllowed)
            yield return null;

        yield return new WaitForSeconds(0.1f);

        if (_isGameWon || _isGameFinished) yield break;

        bool solverFinished = false;
        if (hintSolver == null) hintSolver = gameObject.AddComponent<OctagonHintSolver>();

        int recycles = 0;
        var field = GetType().GetField("recyclesUsed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) recycles = (int)field.GetValue(this);

        hintSolver.FindPath(pileManager, recycles, path => {
            cachedHintPath = path;
            solverFinished = true;
        });

        while (!solverFinished) yield return null;
        backgroundSolverCoroutine = null;
    }

    public void RequestHint(System.Action onWaitStart, System.Action<bool> onHintResult)
    {
        if (isExecutingHint) return;
        if (_isGameWon || _isGameFinished || !IsInputAllowed) { onHintResult?.Invoke(false); return; }
        StartCoroutine(HintRoutine(onWaitStart, onHintResult));
    }

    private IEnumerator HintRoutine(System.Action onWaitStart, System.Action<bool> onResult)
    {
        // FIX (deadlock): start the background solver BEFORE disabling input, not after.
        // BackgroundSolverRoutine's own guard ("while (... || !IsInputAllowed) yield return null;")
        // waits for IsInputAllowed to be true before it ever calls hintSolver.FindPath(...).
        // The old order set IsInputAllowed = false FIRST, so that guard waited forever for
        // input to be re-enabled - which only happens after backgroundSolverCoroutine finishes.
        // Two coroutines waiting on each other = deadlock: FindPath() was never called at all,
        // hence zero HintSolver logs and zero errors, while the hint panel hung forever.
        // RequestHint() already guarantees IsInputAllowed == true at this point, so it is safe
        // to kick off the solver first and only then block input for the duration of the wait.
        if (cachedHintPath == null && backgroundSolverCoroutine == null) StartBackgroundSolver();

        IsInputAllowed = false;

        if (backgroundSolverCoroutine != null)
        {
            onWaitStart?.Invoke();
            while (backgroundSolverCoroutine != null) yield return null;
        }

        if (cachedHintPath != null && cachedHintPath.Count > 0)
        {
            var nextMove = cachedHintPath[0];
            cachedHintPath.Clear(); // ������� ��� �� ������� �������

            isExecutingHint = true;
            ExecuteHintMove(nextMove);

            onResult?.Invoke(true);
        }
        else
        {
            IsInputAllowed = true;
            onResult?.Invoke(false);
        }
    }

    private void ExecuteHintMove(OctagonHintMove cmd)
    {
        if (cmd.Type == OctagonHintMove.MoveType.StockDraw || cmd.Type == OctagonHintMove.MoveType.Recycle)
        {
            OnStockClicked();
            return;
        }

        CardController card = null;
        ICardContainer source = null;
        ICardContainer target = null;

        if (cmd.Type == OctagonHintMove.MoveType.WasteToTableau || cmd.Type == OctagonHintMove.MoveType.WasteToFoundation)
        {
            source = pileManager.WastePile;
            card = pileManager.WastePile.GetTopCard();
        }
        else
        {
            int gIdx = cmd.From / 5;
            int sIdx = cmd.From % 5;
            source = pileManager.TableauGroups[gIdx].Slots[sIdx];
            card = ((OctagonTableauSlot)source).GetTopCard();
        }

        if (cmd.Type == OctagonHintMove.MoveType.Foundation || cmd.Type == OctagonHintMove.MoveType.WasteToFoundation)
        {
            target = pileManager.FoundationPiles[cmd.TargetFoundation];
        }
        else
        {
            int gIdx = cmd.To / 5;
            int sIdx = cmd.To % 5;
            target = pileManager.TableauGroups[gIdx].Slots[sIdx];
        }

        // DIAGNOSTIC: log exactly what the hint tried to do and why it did/didn't apply.
        // If this keeps rejecting the very first move of the solver's path over and over
        // (same path recomputed again and again), the printed suit/rank values below will
        // show directly whether card/source/target came back null, or whether CanAccept()
        // is failing because the target foundation's current suit doesn't match the card -
        // which would point at a mismatch between OctagonHintSolver's suit-to-foundation-index
        // assumption (GetFoundationIndex: suitBase = suit * 2) and how FoundationPiles are
        // actually ordered/assigned in this scene.
        bool canAccept = card != null && target != null && target.CanAccept(card);
        string cardDesc = card != null ? $"{card.cardModel.suit} {card.cardModel.rank}" : "NULL";
        string targetTopDesc = "n/a";
        var targetFoundation = target as OctagonFoundationPile;
        if (targetFoundation != null)
        {
            var t = targetFoundation.GetTopCard();
            targetTopDesc = t != null ? $"{t.cardModel.suit} {t.cardModel.rank}" : "empty";
        }
        Debug.Log($"[HintExec] cmd={cmd.Type} From={cmd.From} To={cmd.To} TargetFoundation={cmd.TargetFoundation} | card={cardDesc} | source={(source == null ? "NULL" : "ok")} | target={(target == null ? "NULL" : "ok")} | targetTop={targetTopDesc} | CanAccept={canAccept}");

        if (card != null && source != null && target != null && canAccept)
        {
            StartCoroutine(AnimateHintMove(card, source, target));
        }
        else
        {
            Debug.LogWarning($"[HintExec] \u0425\u043e\u0434 \u041e\u0422\u041a\u041b\u041e\u041d\u0401\u041d, \u043f\u0435\u0440\u0435\u0437\u0430\u043f\u0443\u0441\u043a\u0430\u044e \u043f\u043e\u0438\u0441\u043a. cmd={cmd.Type} From={cmd.From} To={cmd.To} TargetFoundation={cmd.TargetFoundation}");
            isExecutingHint = false;
            IsInputAllowed = true;
            StartBackgroundSolver();
        }
    }

    private IEnumerator AnimateHintMove(CardController card, ICardContainer source, ICardContainer target)
    {
        card.transform.SetParent(dragLayer, true);
        card.transform.SetAsLastSibling();

        Vector3 targetLocalPos = target.GetDropAnchoredPosition(card);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Whoosh_Out");

        yield return StartCoroutine(animationService.AnimateMoveCard(
            card, target.Transform, targetLocalPos, 0.2f, true, null));

        target.AcceptCard(card);

        // ���� ����� ��� �������� ����, �������� ���������� ������ ����� � ������� CheckGameState
        OnCardDroppedToContainer(card, target);
    }
}