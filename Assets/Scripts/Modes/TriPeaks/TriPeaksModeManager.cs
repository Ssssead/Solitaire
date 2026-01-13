using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TriPeaksModeManager : MonoBehaviour, IModeManager, ICardGameMode
{
    [Header("Core References")]
    public CardFactory cardFactory;
    public Canvas rootCanvas;
    public RectTransform dragLayer;
    public GameUIController gameUI;

    [Header("Services")]
    public TriPeaksPileManager pileManager;
    public TriPeaksScoreManager scoreManager;
    public UndoManager undoManager;
    public TriPeaksAnimationService animationService;
    public DragManager dragManager;

    [Header("Animation Settings")]
    public float dealFlyDuration = 0.35f;
    public float totalDealDuration = 1.8f;
    public float stockToWasteDuration = 0.2f;
    public float stockShiftDuration = 0.2f;
    public float tableauToWasteDuration = 0.3f;

    // Пауза, чтобы игрок успел увидеть анимацию переворота
    public float flipWaitDelay = 0.35f;

    private Difficulty currentDifficulty = Difficulty.Easy;
    private int currentRound = 1;
    private int totalRounds = 1;
    private bool _isInputAllowed = true;
    private bool _hasGameStarted = false;
    private bool _isGameWon = false;

    public string GameName => "TriPeaks";
    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => null;
    public PileManager PileManager => null;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public bool IsInputAllowed { get => _isInputAllowed; set => _isInputAllowed = value; }

    private void Start()
    {
        if (animationService == null) animationService = GetComponent<TriPeaksAnimationService>();
        if (animationService == null) animationService = gameObject.AddComponent<TriPeaksAnimationService>();
        InitializeMode();
    }

    public void InitializeMode() { RestartGameInternal(true); }
    public void RestartGame() { RestartGameInternal(true); }

    private void RestartGameInternal(bool fullReset)
    {
        StopAllCoroutines();
        _hasGameStarted = false;
        _isInputAllowed = true;
        _isGameWon = false;

        if (fullReset && scoreManager != null) scoreManager.ResetScore();
        if (pileManager != null) pileManager.ClearAll();
        if (undoManager != null) undoManager.ResetHistory();

        StartCoroutine(SetupRoundRoutine());
    }

    private IEnumerator SetupRoundRoutine()
    {
        _isInputAllowed = false;
        if (pileManager == null) yield break;

        Deal deal = null;
        if (DealCacheSystem.Instance != null) deal = DealCacheSystem.Instance.GetDeal(GameType.TriPeaks, currentDifficulty, totalRounds);
        if (deal == null) { _isInputAllowed = true; yield break; }

        List<CardController> tableauCards = new List<CardController>();
        int tableauIndex = 0;
        foreach (var cardList in deal.tableau)
        {
            if (tableauIndex >= pileManager.TableauPiles.Count) break;
            if (cardList == null || cardList.Count == 0) { tableauIndex++; continue; }

            // Создаем карту (Фабрика сама назначит спрайты)
            CardController card = cardFactory.CreateCard(cardList[0].Card, pileManager.Stock.transform, Vector2.zero);

            // Настраиваем: сразу закрыта, без анимации
            var cardData = card.GetComponent<CardData>();
            if (cardData != null) cardData.SetFaceUp(false, false);

            card.OnClicked += OnCardClicked;
            tableauCards.Add(card);
            tableauIndex++;
        }

        List<CardInstance> stockSource = new List<CardInstance>(deal.stock);
        stockSource.Reverse();

        CardController initialWasteCard = null;
        if (stockSource.Count > 0)
        {
            CardInstance wInst = stockSource[stockSource.Count - 1];
            stockSource.RemoveAt(stockSource.Count - 1);
            initialWasteCard = cardFactory.CreateCard(wInst.Card, pileManager.Stock.transform, Vector2.zero);
            var wd = initialWasteCard.GetComponent<CardData>();
            if (wd != null) wd.SetFaceUp(false, false);
        }

        int stockCount = stockSource.Count;
        for (int i = 0; i < stockCount; i++)
        {
            float targetX = (i - (stockCount - 1)) * (animationService ? animationService.stockGap : 5f);
            CardController card = cardFactory.CreateCard(stockSource[i].Card, pileManager.Stock.transform, new Vector2(targetX, 0));
            var cd = card.GetComponent<CardData>();
            if (cd != null) cd.SetFaceUp(false, false);

            pileManager.Stock.AddCard(card);
            card.gameObject.SetActive(true);
            card.OnClicked += OnCardClicked;
        }

        pileManager.Stock.UpdateVisuals();
        yield return new WaitForSeconds(0.1f);

        // --- Анимация Раздачи ---
        float delayPerCard = totalDealDuration / (tableauCards.Count + 1);
        for (int i = 0; i < tableauCards.Count; i++)
        {
            CardController card = tableauCards[i];
            TriPeaksTableauPile targetSlot = pileManager.TableauPiles[i];

            // Карты нижнего ряда (18-27) переворачиваются в полете
            bool flyFaceUp = (i >= 18);

            StartCoroutine(animationService.AnimateMoveCard(card, targetSlot.transform, dealFlyDuration, flyFaceUp, () =>
            {
                // Привязываем к слоту ТОЛЬКО после прилета
                targetSlot.AddCard(card);
            }));

            yield return new WaitForSeconds(delayPerCard);
        }

        if (initialWasteCard != null)
        {
            yield return StartCoroutine(animationService.AnimateMoveCard(initialWasteCard, pileManager.Waste.transform, dealFlyDuration, true, () =>
            {
                pileManager.Waste.AddCard(initialWasteCard);
            }));
        }

        yield return new WaitForSeconds(dealFlyDuration + 0.1f);
        ForceEnableInput();

        if (currentRound == 1 && StatisticsManager.Instance)
            StatisticsManager.Instance.OnGameStarted("TriPeaks", currentDifficulty, $"{totalRounds}Rounds");
        _isInputAllowed = true;
    }

    private void ForceEnableInput()
    {
        var allCards = FindObjectsOfType<CardController>();
        foreach (var c in allCards)
        {
            if (c.canvasGroup) { c.canvasGroup.blocksRaycasts = true; c.canvasGroup.interactable = true; }
            var img = c.GetComponent<Image>();
            if (img) img.raycastTarget = true;
        }
    }

    public void OnCardClicked(CardController card)
    {
        if (!_isInputAllowed) return;
        if (!_hasGameStarted) _hasGameStarted = true;
        if (pileManager.Stock.Contains(card)) { OnStockClicked(); return; }

        TriPeaksTableauPile slot = pileManager.FindSlotWithCard(card);
        if (slot != null)
        {
            if (slot.IsBlocked()) return;

            CardController wasteTop = pileManager.Waste.TopCard;
            if (wasteTop == null || CheckMatch(card.cardModel, wasteTop.cardModel))
            {
                StartCoroutine(MoveToWasteRoutine(card, slot, pileManager.Waste));
            }
        }
    }

    public void OnStockClicked() { if (!_isInputAllowed) return; DrawFromStock(true); }

    private void DrawFromStock(bool recordUndo)
    {
        if (pileManager.Stock.IsEmpty) return;
        CardController card = pileManager.Stock.DrawCard();
        if (card == null) return;
        StartCoroutine(DrawFromStockSequence(card, recordUndo));
    }

    private IEnumerator DrawFromStockSequence(CardController card, bool recordUndo)
    {
        _isInputAllowed = false;
        pileManager.Stock.RemoveCard(card);
        if (animationService != null) StartCoroutine(animationService.AnimateStockShift(pileManager.Stock, stockShiftDuration));

        yield return StartCoroutine(animationService.AnimateMoveCard(card, pileManager.Waste.transform, stockToWasteDuration, true, () =>
        {
            pileManager.Waste.AddCard(card);
        }));

        if (scoreManager) scoreManager.ResetStreak();
        _isInputAllowed = true;
        CheckGameState();
    }

    private IEnumerator MoveToWasteRoutine(CardController card, ICardContainer source, ICardContainer target)
    {
        _isInputAllowed = false;
        if (source is TriPeaksTableauPile tSource) tSource.RemoveCard(card);

        yield return StartCoroutine(animationService.AnimateMoveCard(card, target.Transform, tableauToWasteDuration, true, () =>
        {
            if (target is TriPeaksWastePile wTarget) wTarget.AddCard(card);
        }));

        if (source is TriPeaksTableauPile)
        {
            if (scoreManager) scoreManager.AddStreakScore();
            // Запускаем обновление (переворот) разблокированных карт
            yield return StartCoroutine(UpdateTableauFacesRoutine());
        }

        _isInputAllowed = true;
        CheckGameState();
    }

    // --- ИСПРАВЛЕНО: Правильный вызов анимации переворота ---
    private IEnumerator UpdateTableauFacesRoutine()
    {
        bool anyFlip = false;
        foreach (var slot in pileManager.TableauPiles)
        {
            if (slot.HasCard && !slot.IsBlocked())
            {
                var cardData = slot.CurrentCard.GetComponent<CardData>();
                if (cardData != null && !cardData.IsFaceUp())
                {
                    // ВТОРОЙ ПАРАМЕТР TRUE = С АНИМАЦИЕЙ!
                    // Это чинит мгновенное переключение спрайта.
                    // CardData сама запустит плавную смену scale и спрайта.
                    cardData.SetFaceUp(true, true);
                    anyFlip = true;
                }
            }
        }

        if (anyFlip) yield return new WaitForSeconds(flipWaitDelay);
    }

    private bool CheckMatch(CardModel a, CardModel b)
    {
        int r1 = a.rank;
        int r2 = b.rank;
        if (Mathf.Abs(r1 - r2) == 1) return true;
        if ((r1 == 13 && r2 == 1) || (r1 == 1 && r2 == 13)) return true;
        return false;
    }

    public void CheckGameState()
    {
        if (pileManager.TableauPiles.All(p => !p.HasCard)) StartCoroutine(RoundWonRoutine());
    }

    private IEnumerator RoundWonRoutine()
    {
        _isInputAllowed = false;
        if (scoreManager) scoreManager.AddScore(1000 * currentRound);
        yield return new WaitForSeconds(1.0f);
        if (currentRound < totalRounds) { currentRound++; RestartGameInternal(false); }
        else { _isGameWon = true; if (gameUI) gameUI.OnGameWon(); }
    }

    public void OnUndoAction() { }
    public ICardContainer FindNearestContainer(CardController c, Vector2 p, float d) => null;
    public bool OnDropToBoard(CardController c, Vector2 p) => false;
    public void OnCardLongPressed(CardController c) { }
    public void OnCardDroppedToContainer(CardController c, ICardContainer t) { }
    public void OnKeyboardPick(CardController c) { }
    public void OnCardDoubleClicked(CardController c) { OnCardClicked(c); }
}