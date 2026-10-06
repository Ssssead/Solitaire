using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static KlondikeModeManager;

public class MonteCarloModeManager : MonoBehaviour, ICardGameMode
{
    [Header("Managers")]
    public MonteCarloDeckManager deckManager;
    public MonteCarloPileManager pileManager;
    public MonteCarloAnimationService animationService;
    public MonteCarloScoreManager scoreManager;
    public GameUIController gameUI;
    public SceneExitAnimator exitAnimator;
    public MonteCarloTutorialManager tutorialManager;
    [Header("UI & HUD - Landscape")]
    public TMP_Text movesText;
    public TMP_Text scoreText;
    public TMP_Text timeText;

    [Header("UI & HUD - Portrait")]
    public TMP_Text portraitMovesText;
    public TMP_Text portraitScoreText;
    public TMP_Text portraitTimeText;

    [Header("UI Buttons")]
    public Button undoButton;
    public Button undoAllButton;

    [Header("Intro Animation")]
    public bool playIntroOnStart = true;
    public MonteCarloIntroController introController;
    [Header("Hint System")]
    public MonteCarloHintSolver hintSolver;
    private Coroutine backgroundSolverCoroutine = null;
    private List<MonteCarloHintMove> cachedHintPath = null;
    private bool isExecutingHint = false;
    [Header("Rules (Set by Menu)")]
    public bool is8Ways = true;

    [Header("Game Settings (Set by Menu)")]
    public Difficulty currentDifficulty = Difficulty.Medium;
    private int currentGameParam = 1;

    private CardController selectedCard;
    private Stack<MonteCarloMoveRecord> undoStack = new Stack<MonteCarloMoveRecord>();
    private bool hasGameStarted = false;
    private bool isUndoing = false;
    private bool isRestarting = false;
    public bool isGameWon = false;

    private float gameTimer = 0f;
    private bool isTimerRunning = false;

    private Coroutine defeatRoutine;
    public CardController SelectedCard => selectedCard;
    public string GameName => "MonteCarlo";
    public GameType GameType => GameType.MonteCarlo;
    public ITutorialManager Tutorial => tutorialManager;
    private bool _isInputAllowed = true;
    public bool IsInputAllowed
    {
        get => _isInputAllowed;
        set
        {
            _isInputAllowed = value;
            UpdateButtonsState();
        }
    }

    public RectTransform DragLayer => animationService ? animationService.dragLayer : null;
    public AnimationService AnimationService => null;
    public PileManager PileManager => null;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => null;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;

    private void Start()
    {
        if (gameUI == null) gameUI = FindObjectOfType<GameUIController>();
        if (exitAnimator == null) exitAnimator = GetComponent<SceneExitAnimator>();

        SetupButtons();
        ApplySettingsFromMenu();
        InitializeGame(currentDifficulty, currentGameParam);
    }

    private void ApplySettingsFromMenu()
    {
        currentDifficulty = GameSettings.CurrentDifficulty;
        is8Ways = !GameSettings.MonteCarlo4Ways;
        currentGameParam = is8Ways ? 1 : 0;
    }

    private void SetupButtons()
    {
        var globalUndo = FindObjectOfType<UndoManager>();
        if (globalUndo != null)
        {
            if (undoButton == null) undoButton = globalUndo.undoButton;
            if (undoAllButton == null) undoAllButton = globalUndo.undoAllButton;
        }

        if (undoButton != null)
        {
            undoButton.onClick.RemoveAllListeners();
            undoButton.onClick.AddListener(OnUndoAction);
        }
        if (undoAllButton != null)
        {
            LongPressHoldTrigger.SubscribeToButton(undoAllButton, OnUndoAllAction);
        }
    }

    private void Update()
    {
        UpdateButtonsState();

        if (isTimerRunning && !isGameWon) gameTimer += Time.deltaTime;

        // Время
        System.TimeSpan t = System.TimeSpan.FromSeconds(gameTimer);
        string timeStr = t.ToString(@"m\:ss");
        if (timeText != null) timeText.text = timeStr;
        if (portraitTimeText != null) portraitTimeText.text = timeStr;

        // Ходы
        string mText = StatisticsManager.Instance != null ? StatisticsManager.Instance.GetCurrentMoves().ToString() : "0";
        if (movesText != null) movesText.text = mText;
        if (portraitMovesText != null) portraitMovesText.text = mText;

        // Очки
        string sText = scoreManager != null ? scoreManager.Score.ToString() : "0";
        if (scoreText != null) scoreText.text = sText;
        if (portraitScoreText != null) portraitScoreText.text = sText;
    }

    private void UpdateButtonsState()
    {
        bool canInteract = _isInputAllowed && !isUndoing && !isGameWon;
        bool hasHistory = undoStack.Count > 0;

        if (undoButton != null) undoButton.interactable = canInteract && hasHistory;
        if (undoAllButton != null) undoAllButton.interactable = canInteract && hasHistory;
    }

    public void InitializeGame(Difficulty diff, int param)
    {
        if (hasGameStarted && !isGameWon && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();

            // ДОБАВИТЬ ЭТУ СТРОКУ:
            SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }

        // 2. ЗАТЕМ сообщаем трекеру настройки нового матча
        string variant = is8Ways ? "8Ways" : "4Ways";
        GameQuestTracker.Instance?.StartMatch("MonteCarlo", diff, variant);
        currentDifficulty = diff;
        currentGameParam = param;
        IsInputAllowed = false;
        isUndoing = false;
        isGameWon = false;
        if (hintSolver != null) hintSolver.CancelSearch();
        cachedHintPath = null;
        if (DealCacheSystem.Instance != null)
        {
            Deal deal = DealCacheSystem.Instance.GetDeal(GameType, currentDifficulty, currentGameParam);
            if (deal != null) StartGame(deal);
            else Debug.LogError("[MonteCarlo] Получен пустой расклад!");
        }
    }

    public void StartGame(Deal deal)
    {
        if (defeatRoutine != null) { StopCoroutine(defeatRoutine); defeatRoutine = null; }

        undoStack.Clear();
        selectedCard = null;
        hasGameStarted = false;
        isGameWon = false;

        gameTimer = 0f;
        isTimerRunning = false;
        if (scoreManager != null) scoreManager.ResetScore();
        MonteCarloDataLogger.Instance?.StartSession(currentDifficulty.ToString(), is8Ways, deal);

        // --- ИЗМЕНЕНО: Проверяем, запущен ли режим обучения из меню ---
        if (GameSettings.IsTutorialMode && tutorialManager != null)
        {
            StartCoroutine(TutorialIntroRoutine());
        }
        else
        {
            StartCoroutine(IntroSequenceRoutine(deal));
        }
    }
    private IEnumerator TutorialIntroRoutine()
    {
        IsInputAllowed = false;

        // Прячем UI до начала анимации, чтобы не мелькало
        if (introController != null) introController.PrepareIntro(false);

        // Ждем 1 кадр, чтобы Unity обновила холст
        yield return null;

        // Запускаем подставной расклад из MonteCarloTutorialManager
        yield return StartCoroutine(tutorialManager.PlayTutorialIntro(null));

        isRestarting = false;
        IsInputAllowed = true;
    }

    private IEnumerator IntroSequenceRoutine(Deal deal)
    {
        IsInputAllowed = false;

        if (playIntroOnStart && introController != null)
        {
            introController.PrepareIntro(isRestarting);
            deckManager.InstantiateDeal(deal, isIntro: true);
            yield return StartCoroutine(introController.PlayIntroSequence(isRestarting));

            // --- ЖЕСТКАЯ ОСТАНОВКА ---
            // Останавливаем все полёты до привязки карт к слотам.
            if (animationService != null) animationService.StopAllCoroutines();
        }
        else
        {
            deckManager.InstantiateDeal(deal, isIntro: false);
        }

        for (int i = 0; i < 25; i++)
        {
            CardController c = pileManager.BoardCards[i];
            if (c != null)
            {
                c.transform.SetParent(pileManager.TableauSlots[i], true);
                c.transform.localPosition = Vector3.zero;
                c.transform.localRotation = Quaternion.identity;
                c.transform.localScale = Vector3.one;

                if (c.canvasGroup != null)
                {
                    c.canvasGroup.alpha = 1f;
                    c.canvasGroup.interactable = true;
                    c.canvasGroup.blocksRaycasts = true;
                }

                if (animationService != null) animationService.SetShadowFlying(c, false);
            }
        }

        for (int i = 0; i < pileManager.StockCards.Count; i++)
        {
            CardController c = pileManager.StockCards[i];
            c.transform.SetParent(pileManager.StockRoot, true);
            c.transform.localPosition = new Vector3(deckManager.stockCardOffset.x * i, deckManager.stockCardOffset.y * i, 0f);
            c.transform.localRotation = Quaternion.identity;
            c.transform.localScale = Vector3.one;

            if (c.canvasGroup != null)
            {
                c.canvasGroup.alpha = 1f;
                c.canvasGroup.interactable = true;
                c.canvasGroup.blocksRaycasts = true;
            }

            if (animationService != null) animationService.SetShadowFlying(c, false);
        }

        pileManager.UpdateShadows();
        isRestarting = false;
        IsInputAllowed = true;
        StartBackgroundSolver();
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed || isUndoing || isGameWon) return;
        if (pileManager.StockCards.Contains(card)) return;
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Error");
                return;
            }
        }
        int clickedIdx = pileManager.GetCardIndex(card);
        if (clickedIdx == -1) return;

        // ИСПРАВЛЕНИЕ: Мы убрали отсюда ложный старт игры.
        // Игра стартует только тогда, когда пара реально собрана.

        if (selectedCard == card)
        {
            DeselectCardSmoothly();
        }
        else if (selectedCard == null)
        {
            SelectCard(card);
        }
        else
        {
            int selectedIdx = pileManager.GetCardIndex(selectedCard);
            if (IsAdjacent(selectedIdx, clickedIdx))
            {
                StartCoroutine(HandleCardInteractionRoutine(selectedCard, card));
            }
            else
            {
                CardController oldCard = selectedCard;
                selectedCard = null;

                if (oldCard != null)
                {
                    int oldIdx = pileManager.GetCardIndex(oldCard);
                    Transform oldSlot = oldIdx != -1 ? pileManager.TableauSlots[oldIdx] : null;
                    if (oldSlot != null) StartCoroutine(animationService.SmoothReturnToSlot(oldCard, oldSlot, 0.2f));
                }

                SelectCard(card, oldCard);
            }
        }
    }
    public void RegisterMoveAndStartIfNeeded()
    {
        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true;

            if (StatisticsManager.Instance != null)
            {
                string variant = is8Ways ? "8Ways" : "4Ways";
                StatisticsManager.Instance.OnGameStarted("MonteCarlo", currentDifficulty, variant);
            }

            SimpleMetricsTracker.Instance?.TrackLevelStart(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        GameQuestTracker.Instance?.RecordMove();
    }
    private void SelectCard(CardController c, CardController previousCard = null)
    {
        selectedCard = c;
        animationService.HighlightSelectedAndDimOthers(c, GetAllNeighbors(c), pileManager.BoardCards, pileManager.TableauSlots, previousCard);
    }

    private void DeselectCardSmoothly()
    {
        if (selectedCard != null)
        {
            int idx = pileManager.GetCardIndex(selectedCard);
            Transform slot = idx != -1 ? pileManager.TableauSlots[idx] : null;

            animationService.ResetAllCardsVisuals(pileManager.BoardCards, pileManager.TableauSlots, selectedCard);

            if (slot != null) StartCoroutine(animationService.SmoothReturnToSlot(selectedCard, slot, 0.2f));

            selectedCard = null;
        }
    }

    private IEnumerator HandleCardInteractionRoutine(CardController c1, CardController c2)
    {
        IsInputAllowed = false;
        int idx1 = pileManager.GetCardIndex(c1);
        int idx2 = pileManager.GetCardIndex(c2);

        bool isMatch = (c1.cardModel.rank == c2.cardModel.rank) && IsAdjacent(idx1, idx2);

        animationService.ResetAllCardsVisuals(pileManager.BoardCards, pileManager.TableauSlots, c1, c2);

        var data2 = c2.GetComponent<CardData>();
        if (data2 && data2.image) data2.image.color = Color.white;

        animationService.SetHeroes(c1, c2);

        if (animationService.dragLayer != null)
        {
            c1.transform.SetParent(animationService.dragLayer, true);
            c2.transform.SetParent(animationService.dragLayer, true);
        }

        yield return StartCoroutine(animationService.FlyToCard(c1, c2, 0.15f));

        if (isMatch)
        {
            yield return StartCoroutine(MatchPairsAndCollapseRoutine(c1, c2, idx1, idx2));
        }
        else
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Error");

            c2.transform.SetParent(pileManager.TableauSlots[idx2], true);
            yield return StartCoroutine(animationService.SmoothReturnToSlot(c1, pileManager.TableauSlots[idx1], 0.2f));
            selectedCard = null;
            IsInputAllowed = true;
        }
    }

    private IEnumerator MatchPairsAndCollapseRoutine(CardController c1, CardController c2, int idx1, int idx2)
    {
        selectedCard = null;

        var move = new MonteCarloMoveRecord();
        MonteCarloDataLogger.Instance?.LogMatch(idx1, idx2, c1.cardModel, c2.cardModel);
        move.Card1 = c1;
        move.Card2 = c2;
        move.PreviousBoardState = (CardController[])pileManager.BoardCards.Clone();

        pileManager.BoardCards[idx1] = null;
        pileManager.BoardCards[idx2] = null;
        pileManager.FoundationCards.Add(c1);
        pileManager.FoundationCards.Add(c2);
        pileManager.UpdateShadows();

        // ИСПРАВЛЕНИЕ: Вызываем универсальный метод старта и учета хода
        RegisterMoveAndStartIfNeeded();

        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 2);
        GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveBoardCard, 2);
        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 2, c1.cardModel.rank.ToString());
        GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveMonteCarloPair, 1, c1.cardModel.rank.ToString());

        if (scoreManager != null)
        {
            move.PointsEarned = scoreManager.CalculateAndAddMatchScore(idx1, idx2);
        }

        animationService.HighlightTwoCards(c1, c2);

        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            tutorialManager.AdvanceStep();
        }

        Coroutine collapseTask = StartCoroutine(CollapseBoardRoutine(move));

        yield return new WaitForSeconds(0.5f);

        int fCount = pileManager.FoundationCards.Count;
        Vector3 localPos1 = new Vector3(deckManager.foundationCardOffset.x * (fCount - 2), deckManager.foundationCardOffset.y * (fCount - 2), 0f);
        Vector3 localPos2 = new Vector3(deckManager.foundationCardOffset.x * (fCount - 1), deckManager.foundationCardOffset.y * (fCount - 1), 0f);

        Vector3 worldPos1 = pileManager.FoundationRoot.TransformPoint(localPos1);
        Vector3 worldPos2 = pileManager.FoundationRoot.TransformPoint(localPos2);

        yield return StartCoroutine(animationService.AnimatePairToFoundationWithRotation(c1, c2, pileManager.FoundationRoot, worldPos1, worldPos2, null));
        yield return collapseTask;

        animationService.ResetAllCardsVisuals(pileManager.BoardCards, pileManager.TableauSlots);
        pileManager.UpdateShadows();

        undoStack.Push(move);
        IsInputAllowed = true;

        if (!isExecutingHint) StartBackgroundSolver();

        CheckGameState();
    }

    // --- ДОБАВЛЕН ВСПОМОГАТЕЛЬНЫЙ МЕТОД ДЛЯ СИНХРОННЫХ ЗВУКОВ ---
    private IEnumerator DelayedDropSound(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");
    }

    private IEnumerator CollapseBoardRoutine(MonteCarloMoveRecord move)
    {
        List<CardController> remainingCards = new List<CardController>();
        for (int i = 0; i < 25; i++)
        {
            if (pileManager.BoardCards[i] != null)
            {
                remainingCards.Add(pileManager.BoardCards[i]);
                pileManager.BoardCards[i] = null;
            }
        }

        int emptySlotsCount = 25 - remainingCards.Count;
        List<Coroutine> anims = new List<Coroutine>();

        bool playedShiftSound = false;
        bool willShiftCards = false;

        // СДВИГ СТАРЫХ КАРТ
        for (int i = 0; i < remainingCards.Count; i++)
        {
            int newIdx = emptySlotsCount + i;
            pileManager.BoardCards[newIdx] = remainingCards[i];

            if (move.PreviousBoardState[newIdx] != remainingCards[i])
            {
                willShiftCards = true;

                if (!playedShiftSound && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySound("Card_Deal");
                    playedShiftSound = true;
                }

                anims.Add(StartCoroutine(animationService.AnimateCardLinear(remainingCards[i], pileManager.TableauSlots[newIdx].position, 0.25f, null)));
            }
        }

        if (willShiftCards)
        {
            StartCoroutine(DelayedDropSound(0.25f));
        }

        // РАЗДАЧА НОВЫХ КАРТ
        bool willDealCards = false;
        for (int i = emptySlotsCount - 1; i >= 0; i--)
        {
            if (pileManager.StockCards.Count == 0) break;
            willDealCards = true;

            CardController newCard = pileManager.StockCards[pileManager.StockCards.Count - 1];
            pileManager.StockCards.RemoveAt(pileManager.StockCards.Count - 1);

            pileManager.BoardCards[i] = newCard;
            move.CardsDealtFromStock.Add(newCard);
            pileManager.UpdateShadows();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Deal");

            anims.Add(StartCoroutine(animationService.AnimateCardLinear(newCard, pileManager.TableauSlots[i].position, 0.2f, null)));
            yield return new WaitForSeconds(0.05f);
        }

        if (willDealCards)
        {
            StartCoroutine(DelayedDropSound(0.2f));

            // ---> ДОБАВЛЕНО: Засчитываем использование стока <---
            GameQuestTracker.Instance?.RecordStockDraw();
        }

        foreach (var a in anims) yield return a;

        for (int i = 0; i < 25; i++)
        {
            if (pileManager.BoardCards[i] != null)
                pileManager.BoardCards[i].transform.SetParent(pileManager.TableauSlots[i]);
        }
    }

    private List<CardController> GetAllNeighbors(CardController centerCard)
    {
        List<CardController> neighbors = new List<CardController>();
        int centerIdx = pileManager.GetCardIndex(centerCard);
        if (centerIdx == -1) return neighbors;

        for (int i = 0; i < 25; i++)
        {
            CardController otherCard = pileManager.BoardCards[i];
            if (otherCard == null || otherCard == centerCard) continue;

            if (IsAdjacent(centerIdx, i)) neighbors.Add(otherCard);
        }
        return neighbors;
    }

    private bool IsAdjacent(int idx1, int idx2)
    {
        int r1 = idx1 / 5, c1 = idx1 % 5;
        int r2 = idx2 / 5, c2 = idx2 % 5;
        int rDiff = Mathf.Abs(r1 - r2);
        int cDiff = Mathf.Abs(c1 - c2);

        if (is8Ways) return rDiff <= 1 && cDiff <= 1;
        else return (rDiff == 1 && cDiff == 0) || (rDiff == 0 && cDiff == 1);
    }

    public void OnStockClicked() { }

    public void OnUndoAction()
    {
        if (undoStack.Count == 0 || !_isInputAllowed || isUndoing || isGameWon) return;
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.Undo)) return;
            tutorialManager.AdvanceStep();
        }
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Back");

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
        MonteCarloDataLogger.Instance?.LogUndo();

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // ----------------------

        StartCoroutine(UndoRoutine(undoStack.Pop()));
    }

    private IEnumerator UndoRoutine(MonteCarloMoveRecord move)
    {
        if (defeatRoutine != null) { StopCoroutine(defeatRoutine); defeatRoutine = null; }
        if (hasGameStarted && !isGameWon) isTimerRunning = true;

        isUndoing = true;
        IsInputAllowed = false;
        animationService.ResetAllCardsVisuals(pileManager.BoardCards, pileManager.TableauSlots);
        selectedCard = null;

        if (scoreManager != null) scoreManager.AddPoints(-move.PointsEarned);

        List<Coroutine> anims = new List<Coroutine>();

        if (AudioManager.Instance != null)
        {
            AudioSource whoosh = AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            if (whoosh != null) whoosh.pitch = 1.4f;
        }

        // --- ВОЗВРАТ В КОЛОДУ ---
        bool stockReturned = false;
        for (int i = move.CardsDealtFromStock.Count - 1; i >= 0; i--)
        {
            stockReturned = true;
            var c = move.CardsDealtFromStock[i];
            pileManager.StockCards.Add(c);
            pileManager.UpdateShadows();

            int stockIdx = pileManager.StockCards.Count - 1;
            Vector3 localTarget = new Vector3(deckManager.stockCardOffset.x * stockIdx, deckManager.stockCardOffset.y * stockIdx, 0f);
            Vector3 worldTarget = pileManager.StockRoot.TransformPoint(localTarget);

            anims.Add(StartCoroutine(animationService.AnimateCardLinear(c, worldTarget, 0.2f, null)));
        }
        if (stockReturned)
        {
            StartCoroutine(DelayedDropSound(0.2f));
        }

        pileManager.FoundationCards.Remove(move.Card1);
        pileManager.FoundationCards.Remove(move.Card2);
        pileManager.UpdateShadows();

        // ---> ИСПРАВЛЕННЫЙ АНТИ-ЧИТ (ОТКАТ РАНГОВ И ПАР) <---
        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, -2);
        GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveBoardCard, -2);

        if (move.Card1 != null)
        {
            string rankStr = move.Card1.cardModel.rank.ToString();
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -2, rankStr);
            GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveMonteCarloPair, -1, rankStr);
        }
        // -----------------------------------------------

        // --- ВОЗВРАТ НА ДОСКУ ---
        bool boardReturned = false;
        for (int i = 0; i < 25; i++)
        {
            CardController oldCard = move.PreviousBoardState[i];
            pileManager.BoardCards[i] = oldCard;

            if (oldCard != null)
            {
                if (oldCard == move.Card1 || oldCard == move.Card2)
                {
                    if (oldCard.canvasGroup) oldCard.canvasGroup.interactable = true;
                }

                if (Vector3.Distance(oldCard.transform.position, pileManager.TableauSlots[i].position) > 0.01f)
                {
                    boardReturned = true;
                    anims.Add(StartCoroutine(animationService.AnimateCardLinear(oldCard, pileManager.TableauSlots[i].position, 0.25f, null)));
                }
                else
                {
                    oldCard.transform.SetParent(pileManager.TableauSlots[i], true);
                    oldCard.transform.localPosition = Vector3.zero;
                    animationService.SetShadowFlying(oldCard, false);
                }
            }
        }

        if (boardReturned)
        {
            StartCoroutine(DelayedDropSound(0.25f));
        }

        foreach (var a in anims) yield return a;

        for (int i = 0; i < 25; i++)
        {
            if (pileManager.BoardCards[i] != null)
            {
                pileManager.BoardCards[i].transform.SetParent(pileManager.TableauSlots[i]);
                animationService.SetShadowFlying(pileManager.BoardCards[i], false);
            }
        }
        foreach (var c in pileManager.StockCards)
        {
            c.transform.SetParent(pileManager.StockRoot);
            animationService.SetShadowFlying(c, false);
        }

        pileManager.UpdateShadows();
        isUndoing = false;
        IsInputAllowed = true;
        StartBackgroundSolver();
    }

    public void OnUndoAllAction()
    {
        if (undoStack.Count == 0 || !_isInputAllowed || isUndoing || isGameWon) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Back");

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
        MonteCarloDataLogger.Instance?.LogUndoAll();

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // ----------------------

        StartCoroutine(UndoAllRoutine());
    }

    private IEnumerator UndoAllRoutine()
    {
        if (defeatRoutine != null) { StopCoroutine(defeatRoutine); defeatRoutine = null; }
        if (hasGameStarted && !isGameWon) isTimerRunning = true;

        isUndoing = true;
        IsInputAllowed = false;
        animationService.ResetAllCardsVisuals(pileManager.BoardCards, pileManager.TableauSlots);
        selectedCard = null;

        while (undoStack.Count > 0)
        {
            var move = undoStack.Pop();

            for (int i = move.CardsDealtFromStock.Count - 1; i >= 0; i--)
            {
                var c = move.CardsDealtFromStock[i];
                pileManager.StockCards.Add(c);
                c.transform.SetParent(pileManager.StockRoot);
                int stockIdx = pileManager.StockCards.Count - 1;
                c.transform.localPosition = new Vector3(deckManager.stockCardOffset.x * stockIdx, deckManager.stockCardOffset.y * stockIdx, 0f);
            }

            pileManager.FoundationCards.Remove(move.Card1);
            pileManager.FoundationCards.Remove(move.Card2);

            // ---> ИСПРАВЛЕННЫЙ АНТИ-ЧИТ (ОТКАТ РАНГОВ И ПАР ПРИ ПОЛНОЙ ОТМЕНЕ) <---
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, -2);
            GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveBoardCard, -2);

            if (move.Card1 != null)
            {
                string rankStr = move.Card1.cardModel.rank.ToString();
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -2, rankStr);
                GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveMonteCarloPair, -1, rankStr);
            }
            // -----------------------------------------------------------------

            for (int i = 0; i < 25; i++)
            {
                CardController oldCard = move.PreviousBoardState[i];
                pileManager.BoardCards[i] = oldCard;

                if (oldCard != null)
                {
                    if (oldCard == move.Card1 || oldCard == move.Card2)
                    {
                        if (oldCard.canvasGroup) oldCard.canvasGroup.interactable = true;
                    }
                    oldCard.transform.SetParent(pileManager.TableauSlots[i]);
                    oldCard.transform.localPosition = Vector3.zero;
                    animationService.SetShadowFlying(oldCard, false);
                }
            }
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");

        if (scoreManager != null) scoreManager.ResetScore();

        pileManager.UpdateShadows();
        isUndoing = false;
        IsInputAllowed = true;
        StartBackgroundSolver();
        yield return null;
    }

    public void CheckGameState()
    {
        if (isGameWon) return;

        bool isBoardEmpty = true;
        foreach (var c in pileManager.BoardCards) if (c != null) { isBoardEmpty = false; break; }

        if (isBoardEmpty && pileManager.StockCards.Count == 0)
        {
            isGameWon = true;
            isTimerRunning = false;
            IsInputAllowed = false;

            int finalMoves = StatisticsManager.Instance != null ? StatisticsManager.Instance.GetCurrentMoves() : 0;
            int finalScore = scoreManager != null ? scoreManager.Score : 0;

            if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameWon(finalScore);
            MonteCarloDataLogger.Instance?.EndSession("Won", gameTimer);
            if (gameUI != null) gameUI.OnGameWon(finalMoves);

            SimpleMetricsTracker.Instance?.TrackLevelWin(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }
        else if (!isBoardEmpty)
        {
            if (!HasValidMove())
            {
                if (defeatRoutine != null) StopCoroutine(defeatRoutine);
                defeatRoutine = StartCoroutine(ShowDefeatRoutine());
            }
        }
    }

    private bool HasValidMove()
    {
        for (int i = 0; i < 25; i++)
        {
            CardController c1 = pileManager.BoardCards[i];
            if (c1 == null) continue;

            for (int j = i + 1; j < 25; j++)
            {
                CardController c2 = pileManager.BoardCards[j];
                if (c2 == null) continue;

                if (c1.cardModel.rank == c2.cardModel.rank && IsAdjacent(i, j))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private IEnumerator ShowDefeatRoutine()
    {
        yield return new WaitForSeconds(1.0f);

        if (!isGameWon && !HasValidMove())
        {
            isTimerRunning = false;
            IsInputAllowed = false;

            MonteCarloDataLogger.Instance?.LogDefeatPanel();

            if (gameUI != null) gameUI.OnGameLost();
        }
    }

    public void OnCardDoubleClicked(CardController card) { OnCardClicked(card); }

    public void RestartGame()
    {
        if (defeatRoutine != null) { StopCoroutine(defeatRoutine); defeatRoutine = null; }

        if (hasGameStarted && !isGameWon)
        {
            bool isBoardEmpty = true;
            foreach (var c in pileManager.BoardCards) if (c != null) { isBoardEmpty = false; break; }
            if (!(isBoardEmpty && pileManager.StockCards.Count == 0))
            {
                if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameAbandoned();

                // ДОБАВИТЬ ЭТУ СТРОКУ:
                SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
            }
        }

        isRestarting = true;
        StopAllCoroutines();
        if (hintSolver != null) hintSolver.CancelSearch();

        // --- ЖЕСТКАЯ ОСТАНОВКА И СБРОС ---
        // Убиваем визуальные корутины прошлой игры, чтобы они не сломали новую раздачу
        if (animationService != null) animationService.StopAllCoroutines();

        // Мгновенно фиксируем карты на доске перед анимацией их падения вниз
        for (int i = 0; i < 25; i++)
        {
            CardController c = pileManager.BoardCards[i];
            if (c != null)
            {
                c.transform.SetParent(pileManager.TableauSlots[i], true);
                c.transform.localPosition = Vector3.zero;
                c.transform.localScale = Vector3.one;
            }
        }
        for (int i = 0; i < pileManager.StockCards.Count; i++)
        {
            CardController c = pileManager.StockCards[i];
            if (c != null)
            {
                c.transform.SetParent(pileManager.StockRoot, true);
                c.transform.localPosition = new Vector3(deckManager.stockCardOffset.x * i, deckManager.stockCardOffset.y * i, 0f);
            }
        }

        IsInputAllowed = false;

        if (exitAnimator != null)
        {
            exitAnimator.PlayRestartSequence(() =>
            {
                ApplySettingsFromMenu();
                InitializeGame(currentDifficulty, currentGameParam);
            });
        }
        else
        {
            ApplySettingsFromMenu();
            InitializeGame(currentDifficulty, currentGameParam);
        }
    }

    public bool IsMatchInProgress() => hasGameStarted && !isGameWon;

    private void OnDestroy()
    {
        if (hasGameStarted && !isGameWon)
        {
            bool isBoardEmpty = true;
            foreach (var c in pileManager.BoardCards) if (c != null) { isBoardEmpty = false; break; }
            if (!(isBoardEmpty && pileManager.StockCards.Count == 0))
            {
                MonteCarloDataLogger.Instance?.EndSession("Abandoned", gameTimer);
                if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameAbandoned();

                // ДОБАВИТЬ ЭТУ СТРОКУ:
                SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
            }
        }
    }
    public void StartBackgroundSolver()
    {
        if (backgroundSolverCoroutine != null) StopCoroutine(backgroundSolverCoroutine);
        if (hintSolver != null) hintSolver.CancelSearch();
        cachedHintPath = null;
        backgroundSolverCoroutine = StartCoroutine(BackgroundSolverRoutine());
    }

    private IEnumerator BackgroundSolverRoutine()
    {
        // Ждем пока анимации перетаскивания и коллапса не закончатся
        while (animationService != null && animationService.dragLayer != null && animationService.dragLayer.childCount > 0)
            yield return null;

        yield return new WaitForSeconds(0.1f);

        if (isGameWon) yield break;

        bool solverFinished = false;
        if (hintSolver == null) hintSolver = gameObject.AddComponent<MonteCarloHintSolver>();

        hintSolver.FindPath(pileManager, is8Ways, path => {
            cachedHintPath = path;
            solverFinished = true;
        });

        while (!solverFinished) yield return null;
        backgroundSolverCoroutine = null;
    }

    // Заменяем заглушку RequestHint
    public void RequestHint(System.Action onWaitStart, System.Action<bool> onHintResult)
    {
        if (isGameWon || !IsInputAllowed) { onHintResult?.Invoke(false); return; }
        StartCoroutine(HintRoutine(onWaitStart, onHintResult));
    }

    private IEnumerator HintRoutine(System.Action onWaitStart, System.Action<bool> onResult)
    {
        IsInputAllowed = false;
        if (selectedCard != null) DeselectCardSmoothly();

        if (cachedHintPath == null || cachedHintPath.Count == 0) StartBackgroundSolver();

        if (backgroundSolverCoroutine != null)
        {
            onWaitStart?.Invoke();
            while (backgroundSolverCoroutine != null) yield return null;
        }

        if (cachedHintPath != null && cachedHintPath.Count > 0)
        {
            var nextMove = cachedHintPath[0];

            // ИСПРАВЛЕНИЕ: Мы полностью сбрасываем кэш! 
            // Больше никаких слепых шагов в будущее. Каждый ход рассчитывается на свежей доске.
            cachedHintPath.Clear();

            isExecutingHint = true;
            CardController c1 = pileManager.BoardCards[nextMove.Idx1];
            CardController c2 = pileManager.BoardCards[nextMove.Idx2];

            yield return new WaitForSeconds(0.1f); // Ждем снятия выделения

            // Запускаем стандартную механику кликов Монте Карло
            yield return StartCoroutine(HandleCardInteractionRoutine(c1, c2));

            isExecutingHint = false;

            // Запускаем перерасчет подсказки уже на сдвинутой доске
            StartBackgroundSolver();

            onResult?.Invoke(true);
        }
        else
        {
            IsInputAllowed = true;
            onResult?.Invoke(false);
        }
    }
}