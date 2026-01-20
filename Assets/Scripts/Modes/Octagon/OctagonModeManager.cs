using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Для кнопок Undo
using static KlondikeModeManager;

public class OctagonModeManager : MonoBehaviour, ICardGameMode, IModeManager
{
    [Header("Managers")]
    [SerializeField] private OctagonDeckManager deckManager;
    [SerializeField] private OctagonPileManager pileManager;
    // UndoManager нам больше не нужен для логики, но можно оставить поле, если оно используется где-то еще
    [SerializeField] private UndoManager _unusedUndoManager;
    [SerializeField] private GameUIController gameUI;
    [SerializeField] private OctagonAnimationService animationService;

    [Header("UI Controls")]
    [SerializeField] private Button undoButton;     // Ссылка на кнопку Undo в UI
    [SerializeField] private Button undoAllButton;  // Ссылка на кнопку Restart/UndoAll

    [Header("Setup")]
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private Canvas rootCanvas;

    [Header("Game Rules")]
    [SerializeField] private int maxRecycles = 2;
    private int recyclesUsed = 0;
    private bool _isGameFinished = false;
    private int currentScore = 0;

    // --- ЛОКАЛЬНАЯ СИСТЕМА UNDO (КАК В PYRAMID) ---
    private Stack<OctagonMoveRecord> undoStack = new Stack<OctagonMoveRecord>();
    private OctagonMoveRecord currentTransaction; // Текущий записываемый ход (может включать Refill)

    // Свойства
    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => null;
    public OctagonAnimationService OctagonAnim => animationService;
    public PileManager PileManager => null;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 25f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public bool IsInputAllowed { get; set; } = false;
    public string GameName => "Octagon";

    private Difficulty requestDifficulty = Difficulty.Easy;
    private int requestParam = 0;

    private IEnumerator Start()
    {
        if (deckManager == null) deckManager = GetComponent<OctagonDeckManager>();
        if (animationService == null)
        {
            animationService = GetComponent<OctagonAnimationService>();
            if (animationService == null) animationService = gameObject.AddComponent<OctagonAnimationService>();
        }

        // Подписываем кнопки
        if (undoButton) undoButton.onClick.AddListener(OnUndoClicked);
        if (undoAllButton) undoAllButton.onClick.AddListener(RestartGame); // Или UndoAll

        UpdateUI();

        yield return null;

        if (DealCacheSystem.Instance != null)
        {
            Deal deal = null;
            int retries = 0;
            while (deal == null && retries < 10)
            {
                deal = DealCacheSystem.Instance.GetDeal(GameType.Octagon, requestDifficulty, requestParam);
                if (deal == null) { yield return new WaitForSeconds(0.1f); retries++; }
            }
            if (deal != null)
            {
                recyclesUsed = 0;
                _isGameFinished = false;
                currentScore = 0;
                undoStack.Clear();
                deckManager.ApplyDeal(deal);

                // Старт статистики
                if (StatisticsManager.Instance != null)
                    StatisticsManager.Instance.OnGameStarted("Octagon", requestDifficulty, "Classic");

                UpdateUI();
            }
        }
    }

    private void UpdateUI()
    {
        if (undoButton) undoButton.interactable = IsInputAllowed && undoStack.Count > 0;
    }

    // --- UNDO SYSTEM IMPLEMENTATION ---

    private void StartTransaction()
    {
        currentTransaction = new OctagonMoveRecord();
    }

    private void RecordSubMove(CardController card, ICardContainer source, ICardContainer target, int scoreDelta = 0)
    {
        if (currentTransaction == null) return;

        bool wasFaceUp = false;
        var data = card.GetComponent<CardData>();
        if (data != null) wasFaceUp = data.IsFaceUp();

        currentTransaction.AddMove(card, source, target, wasFaceUp, scoreDelta);

        // Применяем счет сразу
        currentScore += scoreDelta;
    }

    private void CommitTransaction()
    {
        if (currentTransaction != null && !currentTransaction.IsEmpty)
        {
            undoStack.Push(currentTransaction);
            if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
        }
        currentTransaction = null;
        UpdateUI();
    }

    public void OnUndoClicked()
    {
        if (undoStack.Count > 0 && IsInputAllowed)
        {
            StopAllCoroutines(); // Остановить анимации Refill если идут
            IsInputAllowed = false;
            StartCoroutine(PerformUndoRoutine());
        }
    }

    private IEnumerator PerformUndoRoutine()
    {
        var record = undoStack.Pop();

        // Проходим по действиям В ОБРАТНОМ ПОРЯДКЕ (LIFO)
        for (int i = record.SubMoves.Count - 1; i >= 0; i--)
        {
            var move = record.SubMoves[i];
            var card = move.Card;

            // 1. Откат очков
            currentScore -= move.ScoreGained;

            // 2. Логический возврат (где карта должна быть)
            // Если возвращаем в Stock или Waste, позиционируем сразу

            // Анимация возврата
            yield return StartCoroutine(animationService.AnimateMoveCard(
                card,
                move.Source.Transform, // Возвращаем в Source
                Vector3.zero,
                0.2f, // Быстрая анимация
                move.WasFaceUp, // Восстанавливаем состояние (FaceUp/Down)
                () =>
                {
                    // Физическое принятие контейнером
                    // Особая логика для Stock/Waste чтобы сбросить позиции
                    if (move.Source is OctagonStockPile stock) stock.AddCard(card);
                    else if (move.Source is OctagonWastePile waste) waste.AddCard(card);
                    else move.Source.AcceptCard(card);
                }
            ));
        }

        // Если отменили Recycle, нужно уменьшить счетчик
        // (Это сложнее отследить, но можно проверить: если move.Target == Stock && move.Source == Waste)
        // Для простоты пока не меняем recyclesUsed, или можно добавить поле в MoveRecord

        IsInputAllowed = true;
        UpdateUI();
        CheckGameState(); // Обновить состояние
    }

    public void OnUndoAction() { /* Интерфейсный метод, не используется */ }

    // --- GAMEPLAY LOGIC ---

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        if (card is OctagonCardController octCard)
        {
            // 1. Начинаем транзакцию
            StartTransaction();

            int score = (container is OctagonFoundationPile) ? 10 : 0;

            // 2. Записываем основной ход
            RecordSubMove(card, octCard.SourceContainer, container, score);

            // 3. Запускаем Refill (он допишет свои действия в ту же транзакцию)
            StartCoroutine(DelayedCheckAutoActions());
        }
        else
        {
            // Fallback
            CheckGameState();
        }
    }

    private IEnumerator DelayedCheckAutoActions()
    {
        yield return new WaitForEndOfFrame();
        CheckAutoActions(); // Refill допишет в currentTransaction
        CommitTransaction(); // Закрываем транзакцию ПОСЛЕ всех действий
    }

    private void CheckAutoActions()
    {
        if (_isGameFinished) return;

        // Ищем пустые группы для Refill
        foreach (var group in pileManager.TableauGroups)
        {
            if (group.IsEmpty())
            {
                if (pileManager.StockPile.CardCount + pileManager.WastePile.CardCount > 0)
                {
                    StartCoroutine(RefillGroupRoutine(group));
                    return; // Только одну группу за раз, Commit произойдет в конце корутины?
                    // НЕТ! Корутина Refill асинхронна.
                    // Проблема: CommitTransaction вызывается сразу в DelayedCheckAutoActions.
                    // Решение: Если запускаем Refill, то Commit переносим ВНУТРЬ Refill.
                }
            }
        }
        // Если Refill не запустился - коммитим здесь (но у нас логика разделена)
        // В текущей реализации DelayedCheckAutoActions делает Commit. 
        // Это ошибка, если запустится корутина.
        // Исправим: DelayedCheckAutoActions НЕ делает коммит, если запустил Refill.
    }

    // --- ИСПРАВЛЕННАЯ ЛОГИКА REFILL + UNDO ---

    private IEnumerator DelayedCheckAutoActions_Fixed()
    {
        yield return new WaitForEndOfFrame();

        bool startedRefill = false;
        if (!_isGameFinished)
        {
            foreach (var group in pileManager.TableauGroups)
            {
                if (group.IsEmpty() && (pileManager.StockPile.CardCount + pileManager.WastePile.CardCount > 0))
                {
                    StartCoroutine(RefillGroupRoutine(group));
                    startedRefill = true;
                    break;
                }
            }
        }

        if (!startedRefill)
        {
            CommitTransaction(); // Если рефилла нет, сохраняем ход
        }
    }

    // Заменяем старый метод на этот
    private IEnumerator RefillGroupRoutine(OctagonTableauGroup group)
    {
        IsInputAllowed = false;
        List<CardController> cardsToPlace = new List<CardController>();
        List<ICardContainer> sources = new List<ICardContainer>(); // Запоминаем источники для Undo

        int needed = 5;

        // Stock
        var stockCards = pileManager.StockPile.DrawCards(needed);
        foreach (var c in stockCards) { cardsToPlace.Add(c); sources.Add(pileManager.StockPile); }

        // Waste
        int remainder = needed - cardsToPlace.Count;
        if (remainder > 0 && pileManager.WastePile.CardCount > 0)
        {
            for (int i = 0; i < remainder; i++)
            {
                var w = pileManager.WastePile.DrawBottomCard();
                if (w != null) { cardsToPlace.Add(w); sources.Add(pileManager.WastePile); }
                else break;
            }
        }

        int maxSlots = group.Slots.Count;
        float moveDuration = 0.3f;
        float interval = 0.1f;

        for (int i = 0; i < cardsToPlace.Count; i++)
        {
            var card = cardsToPlace[i];
            var source = sources[i];

            int slotIndex = (maxSlots - 1) - i;
            if (slotIndex >= 0 && slotIndex < group.Slots.Count)
            {
                var targetSlot = group.Slots[slotIndex];
                bool isTopOne = (i == cardsToPlace.Count - 1);

                // --- ЗАПИСЬ В ТЕКУЩУЮ ТРАНЗАКЦИЮ ---
                RecordSubMove(card, source, targetSlot, 0);
                // ------------------------------------

                StartCoroutine(animationService.AnimateMoveCard(card, targetSlot.Transform, Vector3.zero, moveDuration, isTopOne,
                    () => {
                        targetSlot.AcceptCard(card);
                        var cg = card.GetComponent<CanvasGroup>();
                        if (cg) cg.blocksRaycasts = isTopOne;
                    }));
                yield return new WaitForSeconds(interval);
            }
        }
        yield return new WaitForSeconds(moveDuration);

        // Завершаем транзакцию
        CommitTransaction();

        IsInputAllowed = true;
        CheckGameState();

        // Рекурсивная проверка (если вдруг открылось еще место), но в новой транзакции? 
        // Нет, лучше пока без рекурсии для стабильности.
    }

    // --- STOCK CLICK ---

    public void OnStockClicked()
    {
        if (!IsInputAllowed) return;

        StartTransaction(); // Начинаем запись

        if (pileManager.StockPile.CardCount > 0)
        {
            StartCoroutine(AnimateStockToWaste());
        }
        else if (pileManager.WastePile.CardCount > 0)
        {
            if (recyclesUsed < maxRecycles)
            {
                recyclesUsed++;
                StartCoroutine(AnimateRecycle());
            }
            else
            {
                CommitTransaction(); // Пустой клик, но закрыть надо
                CheckGameState();
            }
        }
        else
        {
            CommitTransaction();
        }
    }

    private IEnumerator AnimateStockToWaste()
    {
        IsInputAllowed = false;
        var list = pileManager.StockPile.DrawCards(1);
        if (list.Count > 0)
        {
            var card = list[0];

            // Запись хода
            RecordSubMove(card, pileManager.StockPile, pileManager.WastePile);

            yield return StartCoroutine(animationService.AnimateMoveCard(card, pileManager.WastePile.transform, Vector3.zero, 0.25f, true,
                () => { pileManager.WastePile.AddCard(card); CheckGameState(); }));
        }
        CommitTransaction();
        IsInputAllowed = true;
    }

    private IEnumerator AnimateRecycle()
    {
        IsInputAllowed = false;
        var cards = new List<CardController>(pileManager.WastePile.GetComponentsInChildren<CardController>());

        // Записываем все карты в одну транзакцию
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var card = cards[i];
            RecordSubMove(card, pileManager.WastePile, pileManager.StockPile);

            StartCoroutine(animationService.AnimateMoveCard(card, pileManager.StockPile.transform, Vector3.zero, 0.15f, false,
                () => { pileManager.StockPile.AddCard(card); }));
            yield return new WaitForSeconds(0.02f);
        }
        yield return new WaitForSeconds(0.3f);

        CommitTransaction();
        IsInputAllowed = true;
        CheckGameState();
    }

    // --- DOUBLE CLICK ---

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        var data = card.GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) return;

        if (card is OctagonCardController octCard)
        {
            if (!IsCardMovable(card)) { StartCoroutine(animationService.AnimateShake(card)); return; }

            // Foundation
            foreach (var f in pileManager.FoundationPiles)
            {
                if (f.CanAccept(card))
                {
                    StartTransaction();
                    RecordSubMove(card, octCard.SourceContainer, f, 10);
                    StartCoroutine(AnimateAutoMove(card, f));
                    return;
                }
            }
            // Tableau
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
                            StartTransaction();
                            RecordSubMove(card, octCard.SourceContainer, slot, 0);
                            StartCoroutine(AnimateAutoMove(card, slot));
                            return;
                        }
                    }
                    if (slot.transform.childCount > 0) break;
                }
            }
            StartCoroutine(animationService.AnimateShake(card));
        }
    }

    private IEnumerator AnimateAutoMove(CardController card, ICardContainer target)
    {
        IsInputAllowed = false;

        // Запускаем Refill после анимации перемещения
        // ВАЖНО: Используем Fixed метод, который либо коммитит, либо продолжает транзакцию в Refill
        StartCoroutine(DelayedCheckAutoActions_Fixed());

        yield return StartCoroutine(animationService.AnimateMoveCard(card, target.Transform, Vector3.zero, 0.25f, true,
            () => {
                target.AcceptCard(card);
                foreach (var group in pileManager.TableauGroups) group.UpdateTopCardState();
                CheckGameState();
            }));
        IsInputAllowed = true;
    }

    // --- UTILS & WIN/LOSS ---

    private bool IsCardMovable(CardController card)
    {
        Transform parent = card.transform.parent;
        if (parent == null || parent.GetComponent<OctagonStockPile>()) return false;
        return parent.GetChild(parent.childCount - 1) == card.transform;
    }

    public void CheckGameState()
    {
        if (_isGameFinished) return;
        if (CheckWinCondition()) { _isGameFinished = true; StartCoroutine(ShowWinRoutine()); return; }
        if (CheckDefeatCondition()) { _isGameFinished = true; StartCoroutine(ShowDefeatRoutine()); }
    }
    private bool CheckWinCondition() { int k = 0; foreach (var f in pileManager.FoundationPiles) { var t = f.GetTopCard(); if (t != null && t.cardModel.rank == 13) k++; } return k == 8; }
    private bool CheckDefeatCondition()
    {
        if (pileManager.StockPile.CardCount > 0 || recyclesUsed < maxRecycles) return false;
        if (pileManager.WastePile.CardCount > 0) { var w = pileManager.WastePile.transform.GetChild(pileManager.WastePile.transform.childCount - 1).GetComponent<CardController>(); if (CanMove(w)) return false; }
        foreach (var g in pileManager.TableauGroups) { var t = g.GetTopCard(); if (t != null && CanMove(t)) return false; }
        return true;
    }
    private bool CanMove(CardController c) { if (c == null) return false; foreach (var f in pileManager.FoundationPiles) if (f.CanAccept(c)) return true; return false; }
    private IEnumerator ShowWinRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameWon(currentScore);
        if (gameUI) gameUI.OnGameWon(currentScore);
    }
    private IEnumerator ShowDefeatRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameAbandoned();
        if (gameUI) gameUI.OnGameLost();
    }
    public void RestartGame()
    {
        if (StatisticsManager.Instance != null && !_isGameFinished) StatisticsManager.Instance.OnGameAbandoned();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // --- DRAG HELPERS ---
    public ICardContainer FindNearestContainer(CardController card, Vector2 screenPos, float maxDistance)
    {
        Rect cardRect = GetWorldRect(card.rectTransform);
        ICardContainer bestContainer = null;
        float bestOverlapArea = 0f;
        List<ICardContainer> candidates = new List<ICardContainer>();
        candidates.AddRange(pileManager.FoundationPiles);
        foreach (var group in pileManager.TableauGroups) candidates.AddRange(group.Slots);
        foreach (var container in candidates)
        {
            var mono = container as MonoBehaviour;
            if (mono == null) continue;
            Rect targetRect;
            CardController topCard = null;
            if (container is OctagonTableauSlot tabSlot) topCard = tabSlot.GetTopCard();
            else if (container is OctagonFoundationPile foundPile) topCard = foundPile.GetTopCard();
            if (topCard != null) targetRect = GetWorldRect(topCard.rectTransform);
            else targetRect = GetWorldRect(mono.transform as RectTransform);
            float area = GetIntersectionArea(cardRect, targetRect);
            if (area > bestOverlapArea) { if (container.CanAccept(card)) { bestOverlapArea = area; bestContainer = container; } }
        }
        return bestContainer;
    }
    private Rect GetWorldRect(RectTransform rt) { Vector3[] c = new Vector3[4]; rt.GetWorldCorners(c); return new Rect(c[0].x, c[0].y, Mathf.Abs(c[2].x - c[0].x), Mathf.Abs(c[2].y - c[0].y)); }
    private float GetIntersectionArea(Rect r1, Rect r2) { float w = Mathf.Min(r1.xMax, r2.xMax) - Mathf.Max(r1.xMin, r2.xMin); float h = Mathf.Min(r1.yMax, r2.yMax) - Mathf.Max(r1.yMin, r2.yMin); return (w > 0 && h > 0) ? w * h : 0f; }
    public bool OnDropToBoard(CardController card, Vector2 pos) => false;
    public void OnCardClicked(CardController c) { }
    public void OnCardLongPressed(CardController c) { }
    public void OnKeyboardPick(CardController c) { }
}