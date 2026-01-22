using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OctagonModeManager : MonoBehaviour, ICardGameMode, IModeManager
{
    [Header("Managers")]
    [SerializeField] private OctagonDeckManager deckManager;
    [SerializeField] private OctagonPileManager pileManager;
    [SerializeField] private GameUIController gameUI;
    [SerializeField] private OctagonAnimationService animationService;

    [Header("UI Controls")]
    [SerializeField] private Button undoButton;
    [SerializeField] private Button undoAllButton;

    [Header("Setup")]
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private Canvas rootCanvas;

    [Header("Game Rules")]
    [SerializeField] private int maxRecycles = 2;
    private int recyclesUsed = 0;
    private bool _isGameFinished = false;
    private Difficulty currentDifficulty;

    private Stack<OctagonMoveRecord> undoStack = new Stack<OctagonMoveRecord>();
    private bool isUndoing = false;

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

    private IEnumerator Start()
    {
        if (deckManager == null) deckManager = GetComponent<OctagonDeckManager>();
        if (pileManager == null) pileManager = GetComponent<OctagonPileManager>();

        if (animationService == null)
        {
            animationService = GetComponent<OctagonAnimationService>();
            if (animationService == null) animationService = gameObject.AddComponent<OctagonAnimationService>();
        }

        if (undoButton) undoButton.onClick.AddListener(OnUndoAction);
        if (undoAllButton) undoAllButton.onClick.AddListener(OnUndoAllAction);

        yield return null;

        if (DealCacheSystem.Instance != null)
        {
            Deal deal = DealCacheSystem.Instance.GetDeal(GameType.Octagon, currentDifficulty, 0);
            if (deal != null)
            {
                recyclesUsed = 0;
                _isGameFinished = false;
                deckManager.ApplyDeal(deal);
                undoStack.Clear();
            }
        }
    }

    private void Update()
    {
        if (undoButton) undoButton.interactable = undoStack.Count > 0 && !isUndoing;
        if (undoAllButton) undoAllButton.interactable = undoStack.Count > 0 && !isUndoing;
    }

    // --- ACTIONS ---

    public void OnStockClicked()
    {
        if (!IsInputAllowed || _isGameFinished || isUndoing) return;

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
        }
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed || _isGameFinished || isUndoing) return;

        var data = card.GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) return;

        if (!IsCardMovable(card))
        {
            StartCoroutine(animationService.AnimateShake(card));
            return;
        }

        foreach (var f in pileManager.FoundationPiles)
        {
            if (f.CanAccept(card))
            {
                StartCoroutine(AnimateAutoMove(card, f));
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
                        StartCoroutine(AnimateAutoMove(card, slot));
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
        OctagonMoveRecord record = new OctagonMoveRecord();

        var octCard = card as OctagonCardController;
        ICardContainer source = octCard != null ? octCard.SourceContainer : null;

        bool wasFaceUp = true;
        record.AddMove(card, source, container, wasFaceUp);

        // Передаем карту (которая УЖЕ перемещена в иерархии), но метод справится
        CheckRevealCardUnder(source, record, card);

        if (source is OctagonTableauSlot slot && slot.Group != null)
        {
            slot.Group.UpdateTopCardState();
        }

        StartCoroutine(CheckRefillAndFinalizeMove(record));
    }

    // --- FIX: Улучшенная проверка карты под низом ---
    private void CheckRevealCardUnder(ICardContainer source, OctagonMoveRecord record, CardController movingCard)
    {
        // Проверяем, является ли источник слотом табло
        if (source is OctagonTableauSlot sourceSlot && sourceSlot.Group != null)
        {
            // Нам нужно найти новую верхнюю карту в ГРУППЕ (а не просто в пустом слоте)
            var group = sourceSlot.Group;

            CardController candidateToReveal = null;

            // Перебираем все слоты группы сверху вниз (0 -> Count)
            for (int i = 0; i < group.Slots.Count; i++)
            {
                var slot = group.Slots[i];
                var cardInSlot = slot.GetTopCard();

                if (cardInSlot != null)
                {
                    // Если мы нашли саму переносимую карту (случай AutoMove, она еще не улетела)
                    // то пропускаем её и ищем следующую
                    if (cardInSlot == movingCard) continue;

                    // Нашли первую карту, которая не является movingCard. Это и есть карта под низом.
                    candidateToReveal = cardInSlot;
                    break;
                }
            }

            // Если карта найдена и она закрыта -> записываем её
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

    private IEnumerator CheckRefillAndFinalizeMove(OctagonMoveRecord currentRecord)
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

        undoStack.Push(currentRecord);
        IsInputAllowed = true;
        CheckGameState();
    }

    private IEnumerator RefillGroupRoutine(OctagonTableauGroup group, OctagonMoveRecord record)
    {
        List<(CardController card, ICardContainer source)> moveList = new List<(CardController, ICardContainer)>();
        int needed = 5;

        while (moveList.Count < needed && pileManager.StockPile.CardCount > 0)
        {
            var c = pileManager.StockPile.PopTopCard();
            if (c) moveList.Add((c, pileManager.StockPile));
        }
        while (moveList.Count < needed && pileManager.WastePile.CardCount > 0)
        {
            var c = pileManager.WastePile.PopBottomCard();
            if (c) moveList.Add((c, pileManager.WastePile));
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

                StartCoroutine(animationService.AnimateMoveCard(
                    card, targetSlot.Transform, Vector3.zero, duration, isLastOne,
                    () =>
                    {
                        targetSlot.AcceptCard(card);
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
        if (isUndoing || undoStack.Count == 0 || !IsInputAllowed) return;
        StartCoroutine(UndoRoutine(false)); // false = с анимацией
    }

    // --- FIX: Добавлен параметр immediate для мгновенной отмены ---
    private IEnumerator UndoRoutine(bool immediate)
    {
        isUndoing = true;
        if (!immediate) IsInputAllowed = false;

        OctagonMoveRecord record = undoStack.Pop();

        // --- FIX: Уменьшение счетчика пересдач ---
        // Если запись содержит перемещение из Waste в Stock, значит мы отменяем Recycle
        if (record.SubMoves.Count > 0)
        {
            var firstMove = record.SubMoves[0];
            // Сравниваем ссылки на объекты куч
            if (firstMove.Source == pileManager.WastePile && firstMove.Target == pileManager.StockPile)
            {
                recyclesUsed = Mathf.Max(0, recyclesUsed - 1);
                // Debug.Log("Recycle undone. Used: " + recyclesUsed);
            }
        }
        // -----------------------------------------

        // 1. Восстанавливаем перевернутую карту
        if (record.RevealedCard != null)
        {
            var data = record.RevealedCard.GetComponent<CardData>();
            if (data != null)
            {
                data.SetFaceUp(false, !immediate);
            }
        }

        // Параметры скорости
        float undoMoveDuration = 0.25f;
        float undoInterval = 0.08f;

        // 2. Возвращаем карты
        for (int i = record.SubMoves.Count - 1; i >= 0; i--)
        {
            var move = record.SubMoves[i];

            CardController card = move.Card;
            ICardContainer targetContainer = move.Source;

            if (targetContainer == null || card == null) continue;

            bool targetFaceUp = move.WasFaceUp;
            if (targetContainer is OctagonStockPile) targetFaceUp = false;
            else if (targetContainer is OctagonWastePile) targetFaceUp = true;

            if (immediate)
            {
                card.transform.SetParent(targetContainer.Transform);
                card.rectTransform.anchoredPosition = Vector2.zero;
                card.transform.localRotation = Quaternion.identity;

                targetContainer.AcceptCard(card);

                if (targetContainer is OctagonTableauSlot ts) ts.UpdateLayout();
                else if (targetContainer is OctagonWastePile wp) wp.UpdateLayout();

                var data = card.GetComponent<CardData>();
                if (data) data.SetFaceUp(targetFaceUp, false);

                var cg = card.GetComponent<CanvasGroup>();
                if (cg) cg.blocksRaycasts = move.WasRaycastBlocked;
            }
            else
            {
                StartCoroutine(animationService.AnimateMoveCard(
                    card,
                    targetContainer.Transform,
                    Vector3.zero,
                    undoMoveDuration,
                    targetFaceUp,
                    () =>
                    {
                        targetContainer.AcceptCard(card);
                        var cg = card.GetComponent<CanvasGroup>();
                        if (cg) cg.blocksRaycasts = move.WasRaycastBlocked;

                        if (targetContainer is OctagonWastePile wp) wp.UpdateLayout();
                        if (targetContainer is OctagonTableauSlot ts) ts.UpdateLayout();
                    }
                ));

                yield return new WaitForSeconds(undoInterval);
            }
        }

        if (!immediate)
        {
            yield return new WaitForSeconds(undoMoveDuration);
        }

        foreach (var g in pileManager.TableauGroups) g.UpdateTopCardState();

        if (!immediate) IsInputAllowed = true;
        isUndoing = false;
    }

    public void OnUndoAllAction()
    {
        if (isUndoing || undoStack.Count == 0 || !IsInputAllowed) return;
        StartCoroutine(UndoAllRoutine());
    }

    // --- FIX: Мгновенная отмена всего ---
    private IEnumerator UndoAllRoutine()
    {
        IsInputAllowed = false;
        isUndoing = true; // Блокируем UI вручную, т.к. update проверяет это поле

        while (undoStack.Count > 0)
        {
            // Вызываем с параметром immediate = true
            yield return StartCoroutine(UndoRoutine(true));
        }

        isUndoing = false;
        IsInputAllowed = true;
    }

    // --- АНИМАЦИИ ---

    private IEnumerator AnimateStockToWaste()
    {
        IsInputAllowed = false;
        var card = pileManager.StockPile.PopTopCard();
        if (card != null)
        {
            OctagonMoveRecord record = new OctagonMoveRecord();
            record.AddMove(card, pileManager.StockPile, pileManager.WastePile, false);
            undoStack.Push(record);

            yield return StartCoroutine(animationService.AnimateMoveCard(
                card,
                pileManager.WastePile.transform,
                Vector3.zero,
                0.25f,
                true,
                () => { pileManager.WastePile.AddCard(card); CheckGameState(); }
            ));
        }
        IsInputAllowed = true;
    }

    private IEnumerator AnimateRecycle()
    {
        IsInputAllowed = false;
        OctagonMoveRecord record = new OctagonMoveRecord();
        var cards = new List<CardController>(pileManager.WastePile.GetComponentsInChildren<CardController>());

        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var card = cards[i];
            record.AddMove(card, pileManager.WastePile, pileManager.StockPile, true);
            StartCoroutine(animationService.AnimateMoveCard(
                card, pileManager.StockPile.transform, Vector3.zero, 0.15f, false,
                () => { pileManager.StockPile.AddCard(card); }
            ));
            yield return new WaitForSeconds(0.02f);
        }
        undoStack.Push(record);
        yield return new WaitForSeconds(0.3f);
        pileManager.WastePile.UpdateLayout();
        IsInputAllowed = true;
        CheckGameState();
    }

    private IEnumerator AnimateAutoMove(CardController card, ICardContainer target)
    {
        IsInputAllowed = false;

        // --- FIX: Явное определение источника для AutoMove ---
        ICardContainer source = card.GetComponentInParent<ICardContainer>();

        OctagonMoveRecord record = new OctagonMoveRecord();
        bool wasFaceUp = true;
        record.AddMove(card, source, target, wasFaceUp);

        // --- FIX: Передаем movingCard для точной проверки ---
        CheckRevealCardUnder(source, record, card);

        yield return StartCoroutine(animationService.AnimateMoveCard(
            card, target.Transform, Vector3.zero, 0.2f, true,
            () =>
            {
                target.AcceptCard(card);

                if (source is OctagonTableauSlot slot && slot.Group != null)
                {
                    slot.Group.UpdateTopCardState();
                }

                StartCoroutine(CheckRefillAndFinalizeMove(record));
            }
        ));
    }

    // --- REQUIRED INTERFACE METHODS ---
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
        foreach (var f in pileManager.FoundationPiles) { if (f.GetTopCard()?.cardModel.rank == 13) k++; }
        if (k == 8) { _isGameFinished = true; if (gameUI) gameUI.OnGameWon(0); }
    }
    public bool OnDropToBoard(CardController c, Vector2 p) => false;
    public void OnUndoActionDummy() { }
    public void RestartGame() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void OnCardClicked(CardController c) { }
    public void OnCardDoubleClicked(CardController c, bool b) { OnCardDoubleClicked(c); }
    public void OnCardLongPressed(CardController c) { }
    public void OnKeyboardPick(CardController c) { }
}