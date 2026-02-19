using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

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
    public TriPeaksAnimationService animationService;
    public DragManager dragManager;

    [Header("UI Buttons")]
    public Button undoButton;
    public Button undoAllButton;

    [Header("HUD")]
    public TMP_Text movesText;

    [Header("Animation Settings")]
    public float dealFlyDuration = 0.3f;
    public float totalDealDuration = 1.5f;
    public float stockToWasteDuration = 0.25f;
    public float stockShiftDuration = 0.2f;
    public float tableauToWasteDuration = 0.3f;
    public float tableauFlipDuration = 0.25f;
    public float flipWaitDelay = 0.3f;
    public float undoMoveDuration = 0.2f;
    public float undoAllMoveDuration = 0.08f;

    private Difficulty currentDifficulty = Difficulty.Easy;
    private int currentRound = 1;
    private int totalRounds = 1;

    // Флаги состояний
    private bool _isInputAllowed = true;
    private bool _isSetupRunning = false;
    private bool _isUndoing = false;
    private bool hasGameStarted = false;

    // Флаги статистики
    private bool _hasGameStarted = false;
    private bool _isGameEnded = false;
    private bool _isGameWon = false;

    private CardModel _logicTopCardModel;
    private Stack<TriPeaksMoveRecord> _undoStack = new Stack<TriPeaksMoveRecord>();

    public string GameName => "TriPeaks";
    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => null;
    public PileManager PileManager => null;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public GameType GameType => GameType.TriPeaks;

    public bool IsInputAllowed
    {
        get => _isInputAllowed && !_isSetupRunning && !_isUndoing && !_isGameEnded;
        set => _isInputAllowed = value;
    }
    public bool IsMatchInProgress()
    {
        return hasGameStarted;
    }
    private void Start()
    {
        if (animationService == null) animationService = GetComponent<TriPeaksAnimationService>();
        if (animationService == null) animationService = gameObject.AddComponent<TriPeaksAnimationService>();

        if (undoButton != null) undoButton.onClick.AddListener(OnUndoAction);
        if (undoAllButton != null) undoAllButton.onClick.AddListener(OnUndoAllAction);

        // Загрузка настроек
        currentDifficulty = GameSettings.CurrentDifficulty;
        totalRounds = GameSettings.RoundsCount;
        if (totalRounds < 1) totalRounds = 1;

        InitializeMode();
    }

    private void Update()
    {
        bool canInteract = IsInputAllowed;
        // Если игра проиграна, но есть история, разрешаем нажимать Undo (кнопки должны быть активны)
        if (_isGameEnded && !_isGameWon && _undoStack.Count > 0)
        {
            canInteract = true;
        }

        bool hasHistory = _undoStack.Count > 0;

        if (undoButton != null) undoButton.interactable = canInteract && hasHistory;
        if (undoAllButton != null) undoAllButton.interactable = canInteract && hasHistory;

        if (!_isGameEnded && movesText != null && StatisticsManager.Instance != null)
        {
            movesText.text = StatisticsManager.Instance.GetCurrentMoves().ToString();
        }
    }

    // --- ИНТЕГРАЦИЯ СТАТИСТИКИ ---
    private void RegisterActivity()
    {
        if (!_hasGameStarted)
        {
            _hasGameStarted = true;
            if (StatisticsManager.Instance != null)
            {
                StatisticsManager.Instance.OnGameStarted("TriPeaks", GameSettings.CurrentDifficulty, $"{totalRounds}Rounds");
            }
        }

        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.RegisterMove();
        }
    }

    private void OnDestroy()
    {
        if (_hasGameStarted && !_isGameWon)
        {
            if (StatisticsManager.Instance != null)
            {
                StatisticsManager.Instance.OnGameAbandoned();
            }
        }
    }

    public void InitializeMode() { RestartGameInternal(true); }

    public void RestartGame()
    {
        if (_hasGameStarted && !_isGameWon)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }

        currentDifficulty = GameSettings.CurrentDifficulty;
        RestartGameInternal(true);
    }

    private void RestartGameInternal(bool fullReset)
    {
        StopAllCoroutines();

        if (fullReset)
        {
            _hasGameStarted = false;
            currentRound = 1;
            if (scoreManager != null) scoreManager.ResetScore();
        }

        _isGameEnded = false;
        _isGameWon = false;
        _isInputAllowed = true;
        _isSetupRunning = false;
        _isUndoing = false;
        _undoStack.Clear();

        if (pileManager != null) pileManager.ClearAll();

        StartCoroutine(SetupRoundRoutine());
    }

    private IEnumerator SetupRoundRoutine()
    {
        _isSetupRunning = true;
        if (pileManager == null) yield break;

        Deal deal = null;
        if (DealCacheSystem.Instance != null)
            deal = DealCacheSystem.Instance.GetDeal(GameType.TriPeaks, currentDifficulty, totalRounds);

        if (deal == null) { _isSetupRunning = false; yield break; }

        List<CardController> tableauCards = new List<CardController>();
        int tableauIndex = 0;
        foreach (var cardList in deal.tableau)
        {
            if (tableauIndex >= pileManager.TableauPiles.Count) break;
            if (cardList == null || cardList.Count == 0) { tableauIndex++; continue; }

            CardController card = cardFactory.CreateCard(cardList[0].Card, pileManager.Stock.transform, Vector2.zero);
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
            float targetX = (i - (stockCount - 1)) * (animationService ? pileManager.Stock.Gap : 5f);
            CardController card = cardFactory.CreateCard(stockSource[i].Card, pileManager.Stock.transform, new Vector2(targetX, 0));
            var cd = card.GetComponent<CardData>();
            if (cd != null) cd.SetFaceUp(false, false);

            pileManager.Stock.AddCard(card);
            card.gameObject.SetActive(true);
            card.OnClicked += OnCardClicked;
        }

        pileManager.Stock.UpdateVisuals();
        yield return new WaitForSeconds(0.1f);

        float delayPerCard = totalDealDuration / (tableauCards.Count + 1);
        for (int i = 0; i < tableauCards.Count; i++)
        {
            CardController card = tableauCards[i];
            TriPeaksTableauPile targetSlot = pileManager.TableauPiles[i];
            bool flyFaceUp = (i >= 18);

            StartCoroutine(animationService.AnimateMoveCard(card, targetSlot.transform, dealFlyDuration, flyFaceUp, () =>
            {
                targetSlot.AddCard(card);
            }));

            yield return new WaitForSeconds(delayPerCard);
        }

        if (initialWasteCard != null)
        {
            _logicTopCardModel = initialWasteCard.cardModel;
            yield return StartCoroutine(animationService.AnimateMoveCard(initialWasteCard, pileManager.Waste.transform, dealFlyDuration, true, () =>
            {
                pileManager.Waste.AddCard(initialWasteCard);
            }));
        }

        yield return new WaitForSeconds(dealFlyDuration + 0.1f);
        ForceEnableInput();

        _isSetupRunning = false;
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
        if (!IsInputAllowed) return;

        if (pileManager.Stock.Contains(card)) { OnStockClicked(); return; }

        TriPeaksTableauPile slot = pileManager.FindSlotWithCard(card);
        if (slot != null)
        {
            if (slot.IsBlocked()) return;

            if (CheckMatch(card.cardModel, _logicTopCardModel))
            {
                StartCoroutine(MoveToWasteRoutine(card, slot, pileManager.Waste));
            }
        }
    }

    public void OnStockClicked()
    {
        if (!IsInputAllowed) return;
        DrawFromStock();
    }

    private void DrawFromStock()
    {
        if (pileManager.Stock.IsEmpty) return;
        CardController card = pileManager.Stock.DrawCard();
        if (card == null) return;
        StartCoroutine(DrawFromStockSequence(card));
    }

    private IEnumerator DrawFromStockSequence(CardController card)
    {
        RegisterActivity();

        _logicTopCardModel = card.cardModel;

        var record = new TriPeaksMoveRecord
        {
            MovedCard = card,
            IsFromStock = true,
            PreviousStreak = scoreManager ? scoreManager.CurrentStreak : 0,
            PointsEarned = 0
        };
        _undoStack.Push(record);

        pileManager.Stock.RemoveCard(card);
        if (animationService != null) StartCoroutine(animationService.AnimateStockShift(pileManager.Stock, stockShiftDuration));

        yield return StartCoroutine(animationService.AnimateMoveCard(card, pileManager.Waste.transform, stockToWasteDuration, true, () =>
        {
            pileManager.Waste.AddCard(card);
        }));

        if (scoreManager) scoreManager.ResetStreak();
        CheckGameState();
    }

    private IEnumerator MoveToWasteRoutine(CardController card, ICardContainer source, ICardContainer target)
    {
        RegisterActivity();

        _logicTopCardModel = card.cardModel;

        var record = new TriPeaksMoveRecord
        {
            MovedCard = card,
            IsFromStock = false,
            SourcePile = source as TriPeaksTableauPile,
            PreviousStreak = scoreManager ? scoreManager.CurrentStreak : 0
        };

        if (scoreManager)
        {
            int points = scoreManager.BasePoints + (scoreManager.CurrentStreak + 1) * scoreManager.StreakBonus;
            record.PointsEarned = points;
        }
        _undoStack.Push(record);

        if (source is TriPeaksTableauPile tSource) tSource.RemoveCard(card);

        Coroutine moveRoutine = StartCoroutine(animationService.AnimateMoveCard(card, target.Transform, tableauToWasteDuration, true, () =>
        {
            if (target is TriPeaksWastePile wTarget) wTarget.AddCard(card);
        }));

        if (source is TriPeaksTableauPile)
        {
            if (scoreManager) scoreManager.AddStreakScore();
            StartCoroutine(UpdateTableauFacesRoutine(record));
        }

        yield return moveRoutine;
        CheckGameState();
    }

    private IEnumerator UpdateTableauFacesRoutine(TriPeaksMoveRecord record)
    {
        bool anyFlip = false;
        foreach (var slot in pileManager.TableauPiles)
        {
            if (slot.HasCard && !slot.IsBlocked())
            {
                var cardData = slot.CurrentCard.GetComponent<CardData>();
                if (cardData != null && !cardData.IsFaceUp())
                {
                    cardData.SetFaceUp(true, true);
                    if (record != null) record.FlipList.Add(slot);
                    anyFlip = true;
                }
            }
        }
        if (anyFlip) yield return new WaitForSeconds(flipWaitDelay);
    }

    // --- UNDO ---

    public void OnUndoAction()
    {
        // Разрешаем Undo, если игра проиграна (Defeat), чтобы игрок мог спастись
        bool isDefeatState = _isGameEnded && !_isGameWon;
        if (!IsInputAllowed && !isDefeatState) return;
        if (_undoStack.Count == 0) return;

        // Если отменяем поражение -> возвращаем игру в активное состояние
        _isGameEnded = false;

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        StartCoroutine(UndoLastMoveRoutine());
    }

    private void ApplyUndoLogic(TriPeaksMoveRecord record)
    {
        if (scoreManager)
            scoreManager.RestoreScoreAndStreak(record.PointsEarned, record.PreviousStreak);

        if (record.FlipList != null && record.FlipList.Count > 0)
        {
            foreach (var slot in record.FlipList)
            {
                if (slot.CurrentCard != null)
                {
                    var data = slot.CurrentCard.GetComponent<CardData>();
                    if (data != null) data.SetFaceUp(false, true);
                }
            }
        }
    }

    private IEnumerator UndoLastMoveRoutine()
    {
        _isUndoing = true;

        TriPeaksMoveRecord record = _undoStack.Pop();
        ApplyUndoLogic(record);

        if (record.FlipList.Count > 0) yield return new WaitForSeconds(0.15f);

        CardController card = record.MovedCard;
        pileManager.Waste.RemoveCard(card);

        if (record.IsFromStock)
        {
            var cd = card.GetComponent<CardData>();
            if (cd != null) cd.SetFaceUp(false, true);

            yield return StartCoroutine(animationService.AnimateMoveCard(card, pileManager.Stock.transform, undoMoveDuration, false, () =>
            {
                pileManager.Stock.AddCard(card);
                if (animationService != null)
                    StartCoroutine(animationService.AnimateStockShift(pileManager.Stock, stockShiftDuration));
                else
                    pileManager.Stock.UpdateVisuals();
            }));
        }
        else
        {
            TriPeaksTableauPile targetSlot = record.SourcePile;
            yield return StartCoroutine(animationService.AnimateMoveCard(card, targetSlot.transform, undoMoveDuration, true, () =>
            {
                targetSlot.AddCard(card);
            }));
        }

        CardController top = pileManager.Waste.TopCard;
        if (top != null) _logicTopCardModel = top.cardModel;

        _isUndoing = false;
    }

    public void OnUndoAllAction()
    {
        // Разрешаем Undo All при поражении
        bool isDefeatState = _isGameEnded && !_isGameWon;
        if (!IsInputAllowed && !isDefeatState) return;
        if (_undoStack.Count == 0) return;

        // --- ИСПРАВЛЕНИЕ: Добавляем +1 к ходам за нажатие кнопки (независимо от кол-ва карт) ---
        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();
        // ---------------------------------------------------------------------------------------

        // Сбрасываем флаг поражения
        _isGameEnded = false;

        StartCoroutine(UndoAllRoutine());
    }

    private IEnumerator UndoAllRoutine()
    {
        _isUndoing = true;

        while (_undoStack.Count > 0)
        {
            TriPeaksMoveRecord record = _undoStack.Pop();
            ApplyUndoLogic(record);

            CardController card = record.MovedCard;
            pileManager.Waste.RemoveCard(card);

            if (record.IsFromStock)
            {
                var cd = card.GetComponent<CardData>();
                if (cd != null) cd.SetFaceUp(false, true);

                StartCoroutine(animationService.AnimateMoveCard(card, pileManager.Stock.transform, undoAllMoveDuration, false, () =>
                {
                    pileManager.Stock.AddCard(card);
                    pileManager.Stock.UpdateVisuals();
                }));
            }
            else
            {
                TriPeaksTableauPile targetSlot = record.SourcePile;
                StartCoroutine(animationService.AnimateMoveCard(card, targetSlot.transform, undoAllMoveDuration, true, () =>
                {
                    targetSlot.AddCard(card);
                }));
            }

            yield return new WaitForSeconds(0.06f);
        }

        yield return new WaitForSeconds(undoAllMoveDuration + 0.1f);

        pileManager.Stock.UpdateVisuals();

        CardController top = pileManager.Waste.TopCard;
        if (top != null) _logicTopCardModel = top.cardModel;

        _isUndoing = false;
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
        // 1. Проверка на ПОБЕДУ (Табло пустое)
        if (pileManager.TableauPiles.All(p => !p.HasCard))
        {
            StartCoroutine(RoundWonRoutine());
            return;
        }

        // --- 2. ИСПРАВЛЕНИЕ: Проверка на ПОРАЖЕНИЕ ---
        // Условие: Сток пуст И ни одна открытая карта в табло не подходит к сбросу
        if (pileManager.Stock.IsEmpty)
        {
            bool anyMovePossible = false;

            foreach (var slot in pileManager.TableauPiles)
            {
                // Проверяем только слоты с картами
                if (slot.HasCard)
                {
                    // Проверяем только открытые (не заблокированные) карты
                    if (!slot.IsBlocked())
                    {
                        // Проверяем совпадение с текущей картой сброса
                        if (CheckMatch(slot.CurrentCard.cardModel, _logicTopCardModel))
                        {
                            anyMovePossible = true;
                            break; // Нашли хотя бы один ход, игра не окончена
                        }
                    }
                }
            }

            // Если ходов нет -> Поражение
            if (!anyMovePossible)
            {
                StartCoroutine(GameLostRoutine());
            }
        }
        // --------------------------------------------
    }

    private IEnumerator GameLostRoutine()
    {
        if (_isGameEnded) yield break;

        _isInputAllowed = false;
        _isGameEnded = true; // Блокируем игру, но не ставим _isGameWon

        yield return new WaitForSeconds(1.0f);

        // Отправляем данные о прерванной игре (поражении)
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }

        if (gameUI) gameUI.OnGameLost();
    }

    private IEnumerator RoundWonRoutine()
    {
        _isInputAllowed = false;

        if (scoreManager) scoreManager.AddScore(1000 * currentRound);

        yield return new WaitForSeconds(0.5f);

        if (currentRound < totalRounds)
        {
            if (animationService != null)
            {
                yield return StartCoroutine(animationService.AnimateRoundClear(pileManager, rootCanvas, 0.1f));
            }

            currentRound++;
            _isSetupRunning = true;
            _isUndoing = false;
            _undoStack.Clear();
            pileManager.ClearAll();

            StartCoroutine(NextRoundSequence());
        }
        else
        {
            int finalMoves = 0;
            if (StatisticsManager.Instance != null)
                finalMoves = StatisticsManager.Instance.GetCurrentMoves();

            _isGameEnded = true;
            _isGameWon = true;

            if (StatisticsManager.Instance != null)
            {
                try
                {
                    StatisticsManager.Instance.OnGameWon(scoreManager ? scoreManager.CurrentScore : 0);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[TriPeaks] Ignored stats error: " + e.Message);
                }
            }

            if (gameUI) gameUI.OnGameWon(finalMoves);
        }
    }

    private IEnumerator NextRoundSequence()
    {
        Deal deal = null;
        if (DealCacheSystem.Instance != null)
            deal = DealCacheSystem.Instance.GetDeal(GameType.TriPeaks, currentDifficulty, totalRounds);

        if (deal == null) { _isSetupRunning = false; _isInputAllowed = true; yield break; }

        List<CardController> tableauCards = new List<CardController>();
        int tableauIndex = 0;
        foreach (var cardList in deal.tableau)
        {
            if (tableauIndex >= pileManager.TableauPiles.Count) break;
            if (cardList == null || cardList.Count == 0) { tableauIndex++; continue; }

            CardController card = cardFactory.CreateCard(cardList[0].Card, pileManager.Stock.transform, Vector2.zero);
            var cardData = card.GetComponent<CardData>();
            if (cardData != null) cardData.SetFaceUp(false, false);

            card.OnClicked += OnCardClicked;
            card.gameObject.SetActive(false);
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

        List<CardController> stockCardsForAnim = new List<CardController>();
        for (int i = 0; i < stockSource.Count; i++)
        {
            CardController card = cardFactory.CreateCard(stockSource[i].Card, pileManager.Stock.transform, Vector2.zero);
            var cd = card.GetComponent<CardData>();
            if (cd != null) cd.SetFaceUp(false, false);
            card.OnClicked += OnCardClicked;
            stockCardsForAnim.Add(card);
        }

        if (initialWasteCard != null) stockCardsForAnim.Add(initialWasteCard);

        if (animationService != null)
        {
            yield return StartCoroutine(animationService.AnimateStockEntry(stockCardsForAnim, pileManager.Stock, rootCanvas, 1.2f));
        }
        else
        {
            foreach (var c in stockCardsForAnim) pileManager.Stock.AddCard(c);
            pileManager.Stock.UpdateVisuals();
        }

        if (initialWasteCard != null)
        {
            pileManager.Stock.RemoveCard(initialWasteCard);
            _logicTopCardModel = initialWasteCard.cardModel;

            yield return StartCoroutine(animationService.AnimateMoveCard(initialWasteCard, pileManager.Waste.transform, dealFlyDuration, true, () =>
            {
                pileManager.Waste.AddCard(initialWasteCard);
            }));
        }

        float delayPerCard = totalDealDuration / (tableauCards.Count + 1);
        for (int i = 0; i < tableauCards.Count; i++)
        {
            CardController card = tableauCards[i];
            card.gameObject.SetActive(true);

            TriPeaksTableauPile targetSlot = pileManager.TableauPiles[i];
            bool flyFaceUp = (i >= 18);

            StartCoroutine(animationService.AnimateMoveCard(card, targetSlot.transform, dealFlyDuration, flyFaceUp, () =>
            {
                targetSlot.AddCard(card);
            }));
            yield return new WaitForSeconds(delayPerCard);
        }

        yield return new WaitForSeconds(dealFlyDuration + 0.1f);

        ForceEnableInput();

        pileManager.Stock.UpdateVisuals();

        if (pileManager.Waste.TopCard != null)
            _logicTopCardModel = pileManager.Waste.TopCard.cardModel;
        else if (initialWasteCard != null)
            _logicTopCardModel = initialWasteCard.cardModel;

        _isSetupRunning = false;
        _isInputAllowed = true; // Включаем управление
    }

    public ICardContainer FindNearestContainer(CardController c, Vector2 p, float d) => null;
    public bool OnDropToBoard(CardController c, Vector2 p) => false;
    public void OnCardLongPressed(CardController c) { }
    public void OnCardDroppedToContainer(CardController c, ICardContainer t) { }
    public void OnKeyboardPick(CardController c) { }
    public void OnCardDoubleClicked(CardController c) { OnCardClicked(c); }
}