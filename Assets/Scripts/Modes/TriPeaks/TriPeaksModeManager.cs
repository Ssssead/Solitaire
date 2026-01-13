using UnityEngine;
using UnityEngine.EventSystems;
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
    public float dealFlyDuration = 0.2f;
    public float totalDealDuration = 1.0f;
    public float stockToWasteDuration = 0.2f;
    public float stockShiftDuration = 0.2f;
    public float tableauToWasteDuration = 0.3f;

    // Пауза перед тем, как игрок сможет кликать дальше (пока переворачиваются карты)
    public float flipWaitDelay = 0.3f;

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

    public bool IsInputAllowed
    {
        get => _isInputAllowed;
        set => _isInputAllowed = value;
    }

    private void Start()
    {
        if (animationService == null) animationService = GetComponent<TriPeaksAnimationService>();
        if (animationService == null) animationService = gameObject.AddComponent<TriPeaksAnimationService>();
        InitializeMode();
    }

    public void InitializeMode()
    {
        currentDifficulty = GameSettings.CurrentDifficulty;
        totalRounds = GameSettings.RoundsCount;
        if (totalRounds < 1) totalRounds = 1;
        currentRound = 1;
        RestartGameInternal(true);
    }

    public void RestartGame()
    {
        if (_hasGameStarted && !_isGameWon && StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameAbandoned();

        currentDifficulty = GameSettings.CurrentDifficulty;
        totalRounds = GameSettings.RoundsCount;
        currentRound = 1;
        RestartGameInternal(true);
    }

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

        if (pileManager == null || pileManager.TableauPiles == null || pileManager.TableauPiles.Count == 0) yield break;

        Deal deal = null;
        if (DealCacheSystem.Instance != null)
        {
            float timeout = 3f;
            while (deal == null && timeout > 0)
            {
                deal = DealCacheSystem.Instance.GetDeal(GameType.TriPeaks, currentDifficulty, totalRounds);
                if (deal == null) yield return null;
                timeout -= Time.deltaTime;
            }
        }

        if (deal == null)
        {
            _isInputAllowed = true;
            yield break;
        }

        // 1. Создание Табло
        List<CardController> tableauCards = new List<CardController>();
        int tableauIndex = 0;
        foreach (var cardList in deal.tableau)
        {
            if (tableauIndex >= pileManager.TableauPiles.Count) break;
            if (cardList == null || cardList.Count == 0) { tableauIndex++; continue; }

            CardController card = cardFactory.CreateCard(cardList[0].Card, pileManager.Stock.transform, Vector2.zero);
            if (card is TriPeaksCardController triCard) triCard.Configure(cardList[0].Card);

            var cardData = card.GetComponent<CardData>();
            if (cardData != null) cardData.SetFaceUp(false, true); // true = мгновенно закрыть
            card.transform.localRotation = Quaternion.identity;

            card.OnClicked += OnCardClicked;
            tableauCards.Add(card);
            tableauIndex++;
        }

        // 2. Создание Стока
        List<CardInstance> stockSource = new List<CardInstance>(deal.stock);
        stockSource.Reverse();

        CardController initialWasteCard = null;
        if (stockSource.Count > 0)
        {
            CardInstance wInst = stockSource[stockSource.Count - 1];
            stockSource.RemoveAt(stockSource.Count - 1);

            initialWasteCard = cardFactory.CreateCard(wInst.Card, pileManager.Stock.transform, Vector2.zero);
            if (initialWasteCard is TriPeaksCardController wTri) wTri.Configure(wInst.Card);

            var wd = initialWasteCard.GetComponent<CardData>();
            if (wd != null) wd.SetFaceUp(false, true);
            initialWasteCard.transform.localRotation = Quaternion.identity;
        }

        int stockCount = stockSource.Count;
        for (int i = 0; i < stockCount; i++)
        {
            float targetX = (i - (stockCount - 1)) * (animationService ? animationService.stockGap : 5f);
            Vector2 pos = new Vector2(targetX, 0f);

            CardController card = cardFactory.CreateCard(stockSource[i].Card, pileManager.Stock.transform, pos);
            if (card is TriPeaksCardController t) t.Configure(stockSource[i].Card);

            var cd = card.GetComponent<CardData>();
            if (cd != null) cd.SetFaceUp(false, true);
            card.transform.localRotation = Quaternion.identity;

            pileManager.Stock.AddCard(card);
            card.gameObject.SetActive(true);
            card.OnClicked += OnCardClicked;
        }

        pileManager.Stock.UpdateVisuals();
        yield return new WaitForSeconds(0.1f);

        // 3. Анимация раздачи
        float delayPerCard = totalDealDuration / (tableauCards.Count + 1);

        for (int i = 0; i < tableauCards.Count; i++)
        {
            CardController card = tableauCards[i];
            TriPeaksTableauPile targetSlot = pileManager.TableauPiles[i];
            targetSlot.AddCard(card); // Логическая привязка

            bool flyFaceUp = (i >= 18); // Нижний ряд летит открытым
            StartCoroutine(animationService.AnimateMoveCard(card, targetSlot.transform, dealFlyDuration, flyFaceUp, null));
            yield return new WaitForSeconds(delayPerCard);
        }

        if (initialWasteCard != null)
        {
            pileManager.Waste.AddCard(initialWasteCard);
            yield return StartCoroutine(animationService.AnimateMoveCard(initialWasteCard, pileManager.Waste.transform, dealFlyDuration, true, null));
        }

        yield return new WaitForSeconds(dealFlyDuration + 0.1f);
        ForceEnableInputOnCards();

        if (currentRound == 1 && StatisticsManager.Instance)
            StatisticsManager.Instance.OnGameStarted("TriPeaks", currentDifficulty, $"{totalRounds}Rounds");

        _isInputAllowed = true;
    }

    private void ForceEnableInputOnCards()
    {
        var allCards = FindObjectsOfType<CardController>();
        foreach (var card in allCards)
        {
            var cg = card.GetComponent<CanvasGroup>();
            if (cg != null) { cg.blocksRaycasts = true; cg.interactable = true; }
            var img = card.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }
    }

    // --- Interaction ---

    public void OnCardClicked(CardController card)
    {
        if (!_isInputAllowed) return;
        if (!_hasGameStarted) _hasGameStarted = true;

        if (pileManager.Stock.Contains(card))
        {
            OnStockClicked();
            return;
        }

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

    public void OnStockClicked()
    {
        if (!_isInputAllowed) return;
        DrawFromStock(true);
    }

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

        if (animationService != null)
            StartCoroutine(animationService.AnimateStockShift(pileManager.Stock, stockShiftDuration));

        // Открываем карту (с анимацией), пока она летит или перед вылетом
        // Но для стока обычно она переворачивается в полете. 
        // TriPeaksAnimationService.AnimateMoveCard делает это.

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

        // 1. Полет карты в Waste
        yield return StartCoroutine(animationService.AnimateMoveCard(card, target.Transform, tableauToWasteDuration, true, () =>
        {
            if (target is TriPeaksWastePile wTarget) wTarget.AddCard(card);
        }));

        // 2. Проверка и открытие новых карт
        if (source is TriPeaksTableauPile)
        {
            if (scoreManager) scoreManager.AddStreakScore();

            // Запускаем обновление и ждем завершения анимаций
            yield return StartCoroutine(UpdateTableauFacesRoutine());
        }

        _isInputAllowed = true;
        CheckGameState();
    }

    private IEnumerator UpdateTableauFacesRoutine()
    {
        bool anyFlipStarted = false;

        // Проходим по всем слотам
        foreach (var slot in pileManager.TableauPiles)
        {
            // Карта должна быть в слоте и НЕ быть заблокирована
            // (RemoveCard уже убрал верхнюю карту логически, поэтому IsBlocked сработает корректно для нижних)
            if (slot.HasCard && !slot.IsBlocked())
            {
                var cardData = slot.CurrentCard.GetComponent<CardData>();

                // Если карта все еще закрыта - открываем
                if (cardData != null && !cardData.IsFaceUp())
                {
                    // ВАЖНО: false = с анимацией. 
                    // CardData сама сделает красивый скейл-эффект.
                    cardData.SetFaceUp(true, false);
                    anyFlipStarted = true;
                }
            }
        }

        // Если была хоть одна анимация, даем время зрителю насладиться
        if (anyFlipStarted)
        {
            yield return new WaitForSeconds(flipWaitDelay);
        }
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
        if (pileManager.TableauPiles.All(p => !p.HasCard))
        {
            StartCoroutine(RoundWonRoutine());
        }
    }

    private IEnumerator RoundWonRoutine()
    {
        _isInputAllowed = false;
        if (scoreManager) scoreManager.AddScore(1000 * currentRound);
        yield return new WaitForSeconds(1.0f);

        if (currentRound < totalRounds)
        {
            currentRound++;
            RestartGameInternal(false);
        }
        else
        {
            _isGameWon = true;
            if (StatisticsManager.Instance) StatisticsManager.Instance.OnGameWon(scoreManager ? scoreManager.CurrentScore : 0);
            if (gameUI) gameUI.OnGameWon();
        }
    }

    public void OnUndoAction() { /* TODO */ }

    public ICardContainer FindNearestContainer(CardController c, Vector2 p, float d) => null;
    public bool OnDropToBoard(CardController c, Vector2 p) => false;
    public void OnCardLongPressed(CardController c) { }
    public void OnCardDroppedToContainer(CardController c, ICardContainer t) { }
    public void OnKeyboardPick(CardController c) { }
    public void OnCardDoubleClicked(CardController c) { OnCardClicked(c); }
}