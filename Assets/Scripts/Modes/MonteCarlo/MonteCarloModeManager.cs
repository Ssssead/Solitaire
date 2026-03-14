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

    [Header("UI & HUD")]
    public TMP_Text movesText;
    public TMP_Text scoreText;
    public TMP_Text timeText;

    [Header("UI Buttons")]
    public Button undoButton;
    public Button undoAllButton;

    [Header("Intro Animation")]
    public bool playIntroOnStart = true;
    public MonteCarloIntroController introController;

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
    private bool isGameWon = false;

    private float gameTimer = 0f;
    private bool isTimerRunning = false;

    // --- ICardGameMode Implementation ---
    public string GameName => "MonteCarlo";
    public GameType GameType => GameType.MonteCarlo;
    public bool IsInputAllowed { get; set; } = true;
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
            undoAllButton.onClick.RemoveAllListeners();
            undoAllButton.onClick.AddListener(OnUndoAllAction);
        }
    }

    private void Update()
    {
        bool canInteract = IsInputAllowed && !isUndoing && !isGameWon;
        bool hasHistory = undoStack.Count > 0;

        if (undoButton != null) undoButton.interactable = canInteract && hasHistory;
        if (undoAllButton != null) undoAllButton.interactable = canInteract && hasHistory;

        if (isTimerRunning && !isGameWon) gameTimer += Time.deltaTime;

        if (timeText != null)
        {
            System.TimeSpan t = System.TimeSpan.FromSeconds(gameTimer);
            timeText.text = t.ToString(@"m\:ss");
        }

        if (movesText != null && StatisticsManager.Instance != null)
            movesText.text = StatisticsManager.Instance.GetCurrentMoves().ToString();

        if (scoreText != null && scoreManager != null)
            scoreText.text = scoreManager.Score.ToString();
    }

    public void InitializeGame(Difficulty diff, int param)
    {
        currentDifficulty = diff;
        currentGameParam = param;
        IsInputAllowed = false;
        isUndoing = false;
        isGameWon = false;

        if (DealCacheSystem.Instance != null)
        {
            Deal deal = DealCacheSystem.Instance.GetDeal(GameType, currentDifficulty, currentGameParam);
            if (deal != null) StartGame(deal);
            else Debug.LogError("[MonteCarlo] Получен пустой расклад!");
        }
    }

    public void StartGame(Deal deal)
    {
        undoStack.Clear();
        selectedCard = null;
        hasGameStarted = false;
        isGameWon = false;

        gameTimer = 0f;
        isTimerRunning = false;
        if (scoreManager != null) scoreManager.ResetScore();

        StartCoroutine(IntroSequenceRoutine(deal));
    }

    private IEnumerator IntroSequenceRoutine(Deal deal)
    {
        IsInputAllowed = false;

        if (playIntroOnStart && introController != null)
        {
            introController.PrepareIntro(isRestarting);
            deckManager.InstantiateDeal(deal, isIntro: true);
            yield return StartCoroutine(introController.PlayIntroSequence(isRestarting));
        }
        else
        {
            deckManager.InstantiateDeal(deal, isIntro: false);
        }

        pileManager.UpdateShadows();
        isRestarting = false;
        IsInputAllowed = true;
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed || isUndoing || isGameWon) return;
        if (pileManager.StockCards.Contains(card)) return;

        int clickedIdx = pileManager.GetCardIndex(card);
        if (clickedIdx == -1) return;

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true;

            string variantName = is8Ways ? "8Ways" : "4Ways";
            if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameStarted("MonteCarlo", currentDifficulty, variantName);
        }

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
        move.Card1 = c1;
        move.Card2 = c2;
        move.PreviousBoardState = (CardController[])pileManager.BoardCards.Clone();

        pileManager.BoardCards[idx1] = null;
        pileManager.BoardCards[idx2] = null;
        pileManager.FoundationCards.Add(c1);
        pileManager.FoundationCards.Add(c2);
        pileManager.UpdateShadows();

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();

        if (scoreManager != null)
        {
            move.PointsEarned = scoreManager.CalculateAndAddMatchScore(idx1, idx2);
        }

        animationService.HighlightTwoCards(c1, c2);

        Coroutine collapseTask = StartCoroutine(CollapseBoardRoutine(move));

        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(animationService.AnimatePairToFoundationWithRotation(c1, c2, pileManager.FoundationRoot, null));
        yield return collapseTask;

        animationService.ResetAllCardsVisuals(pileManager.BoardCards, pileManager.TableauSlots);
        pileManager.UpdateShadows();

        undoStack.Push(move);
        IsInputAllowed = true;
        CheckGameState();
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

        for (int i = 0; i < remainingCards.Count; i++)
        {
            int newIdx = emptySlotsCount + i;
            pileManager.BoardCards[newIdx] = remainingCards[i];

            if (move.PreviousBoardState[newIdx] != remainingCards[i])
            {
                anims.Add(StartCoroutine(animationService.AnimateCardLinear(remainingCards[i], pileManager.TableauSlots[newIdx].position, 0.25f)));
            }
        }

        for (int i = emptySlotsCount - 1; i >= 0; i--)
        {
            if (pileManager.StockCards.Count == 0) break;

            CardController newCard = pileManager.StockCards[pileManager.StockCards.Count - 1];
            pileManager.StockCards.RemoveAt(pileManager.StockCards.Count - 1);

            pileManager.BoardCards[i] = newCard;
            move.CardsDealtFromStock.Add(newCard);
            pileManager.UpdateShadows();

            anims.Add(StartCoroutine(animationService.AnimateCardLinear(newCard, pileManager.TableauSlots[i].position, 0.2f)));
            yield return new WaitForSeconds(0.05f);
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
        if (undoStack.Count == 0 || !IsInputAllowed || isUndoing || isGameWon) return;
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
        StartCoroutine(UndoRoutine(undoStack.Pop()));
    }

    private IEnumerator UndoRoutine(MonteCarloMoveRecord move)
    {
        isUndoing = true;
        IsInputAllowed = false;
        animationService.ResetAllCardsVisuals(pileManager.BoardCards, pileManager.TableauSlots);
        selectedCard = null;

        if (scoreManager != null) scoreManager.AddPoints(-move.PointsEarned);

        List<Coroutine> anims = new List<Coroutine>();

        for (int i = move.CardsDealtFromStock.Count - 1; i >= 0; i--)
        {
            var c = move.CardsDealtFromStock[i];
            pileManager.StockCards.Add(c);
            pileManager.UpdateShadows();

            int stockIdx = pileManager.StockCards.Count - 1;
            Vector3 localTarget = new Vector3(deckManager.stockCardOffset.x * stockIdx, deckManager.stockCardOffset.y * stockIdx, 0f);
            Vector3 worldTarget = pileManager.StockRoot.TransformPoint(localTarget);

            anims.Add(StartCoroutine(animationService.AnimateCardLinear(c, worldTarget, 0.2f)));
        }

        pileManager.FoundationCards.Remove(move.Card1);
        pileManager.FoundationCards.Remove(move.Card2);
        pileManager.UpdateShadows();

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

                // ИСПРАВЛЕНИЕ: Включаем полет только если карта сдвинулась с места
                if (Vector3.Distance(oldCard.transform.position, pileManager.TableauSlots[i].position) > 0.01f)
                {
                    anims.Add(StartCoroutine(animationService.AnimateCardLinear(oldCard, pileManager.TableauSlots[i].position, 0.25f)));
                }
                else
                {
                    // Иначе просто привязываем к слоту и жестко выключаем тень
                    oldCard.transform.SetParent(pileManager.TableauSlots[i], true);
                    oldCard.transform.localPosition = Vector3.zero;
                    animationService.SetShadowFlying(oldCard, false);
                }
            }
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
    }

    public void OnUndoAllAction()
    {
        if (undoStack.Count == 0 || !IsInputAllowed || isUndoing || isGameWon) return;
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
        StartCoroutine(UndoAllRoutine());
    }

    private IEnumerator UndoAllRoutine()
    {
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

        if (scoreManager != null) scoreManager.ResetScore();

        pileManager.UpdateShadows();
        isUndoing = false;
        IsInputAllowed = true;
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
            if (gameUI != null) gameUI.OnGameWon(finalMoves);
        }
    }

    public void OnCardDoubleClicked(CardController card) { OnCardClicked(card); }

    public void RestartGame()
    {
        if (hasGameStarted && !isGameWon)
        {
            bool isBoardEmpty = true;
            foreach (var c in pileManager.BoardCards) if (c != null) { isBoardEmpty = false; break; }
            if (!(isBoardEmpty && pileManager.StockCards.Count == 0))
            {
                if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameAbandoned();
            }
        }

        isRestarting = true;
        StopAllCoroutines();
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
                if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameAbandoned();
            }
        }
    }
}