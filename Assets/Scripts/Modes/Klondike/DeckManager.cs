// DeckManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [Header("Generation Settings")]
    public Difficulty difficulty = Difficulty.Medium;
    public BaseGenerator generator;

    [Header("Settings")]
    [Tooltip("Seed для детерминированной тасовки. -1 = случайный")]
    public int shuffleSeed = -1;
    [Tooltip("Общее время анимации раздачи (сек)")]
    public float dealingTotalDuration = 1.8f;

    [Header("References")]
    public KlondikeModeManager mode;
    public CardFactory cardFactory;
    public PileManager pileManager;
    public UndoManager undoManager;

    public RectTransform dragLayer;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    [Header("Stalemate Logic")]
    public int maxPassiveRecycles = 2;
    private int passiveRecycleCount = 0;
    private bool hasMadeMoveThisCycle = false;

    public bool IsStalemateReached => passiveRecycleCount >= maxPassiveRecycles && !hasMadeMoveThisCycle;

    private Stack<CardModel> deck;

    // Флаг, указывающий, что идет процесс раздачи или генерации
    private bool isDealing = false;

    public void Initialize(KlondikeModeManager km, CardFactory cf = null, PileManager pm = null)
    {
        mode = km ?? mode;
        cardFactory = cf ?? cardFactory ?? mode?.cardFactory;
        pileManager = pm ?? pileManager ?? mode?.pileManager;
        undoManager = mode?.undoManager ?? GetComponent<UndoManager>() ?? FindObjectOfType<UndoManager>();

        if (generator == null) generator = FindObjectOfType<BaseGenerator>();
        if (mode != null && dragLayer == null) dragLayer = mode.DragLayer;

        LogDebug($"Initialized. Generator found: {generator != null}");
    }

    public void RestartGame()
    {
        // 1. Принудительно останавливаем все текущие анимации
        StopAllCoroutines();

        // 2. СБРОС ФЛАГА: Самое важное исправление.
        // Раз мы перезапускаем игру, мы гарантируем, что старая раздача прервана.
        // Это чинит проблему "сломанного" Stock.
        isDealing = false;

        ResetStalemate();

        if (mode != null && mode.undoManager != null) mode.undoManager.ResetHistory();

        ClearAllPiles();
        if (cardFactory != null) cardFactory.DestroyAllCards();

        if (mode != null)
        {
            mode.StartNewGame();
            mode.IsInputAllowed = true;
        }

        // 3. Запускаем раздачу
        DealInitial();
    }

    public void ResetStalemate()
    {
        passiveRecycleCount = 0;
    }

    public void DealInitial()
    {
        // --- ЗАЩИТА ОТ ДВОЙНОГО ВЫЗОВА ---
        // Если флаг isDealing уже стоит, значит раздача уже запущена (например, двойной клик).
        // Мы просто выходим, предотвращая наложение карт.
        if (isDealing)
        {
            LogDebug("DealInitial ignored: already dealing.");
            return;
        }

        // Ставим блокировку. Она снимется только в конце анимации (AnimateOpeningDeal) 
        // или при принудительном RestartGame().
        isDealing = true;

        // Блокируем ввод игрока
        if (mode != null) mode.IsInputAllowed = false;

        ClearAllPiles();

        int drawCount = 1;
        if (mode != null) drawCount = (int)mode.stockDealMode;

        // ВАРИАНТ 1: КЭШ
        if (DealCacheSystem.Instance != null && generator != null)
        {
            Deal cachedDeal = DealCacheSystem.Instance.GetDeal(generator.GameType, difficulty, drawCount);
            if (cachedDeal != null && IsDealContentValid(cachedDeal))
            {
                ApplyDeal(cachedDeal);
                return;
            }
        }

        // ВАРИАНТ 2: ГЕНЕРАЦИЯ
        if (generator != null)
        {
            StartCoroutine(generator.GenerateDeal(difficulty, drawCount, OnDealGenerated));
        }
        else
        {
            LogDebug("No generator found. Using Random Shuffle (Instant Deal).");
            DealRandom();
        }
    }

    private bool IsDealContentValid(Deal deal)
    {
        if (deal == null) return false;
        int cardCount = 0;
        if (deal.stock != null) cardCount += deal.stock.Count;
        if (deal.tableau != null)
        {
            foreach (var col in deal.tableau)
                if (col != null) cardCount += col.Count;
        }
        return cardCount > 10;
    }

    private void OnDealGenerated(Deal deal, DealMetrics metrics)
    {
        if (deal != null)
        {
            ApplyDeal(deal);
        }
        else
        {
            DealRandom();
        }
    }

    public void OnProductiveMoveMade()
    {
        passiveRecycleCount = 0;
        hasMadeMoveThisCycle = true;
    }

    public void DrawFromStock()
    {
        // Если идет раздача - игнорируем клики.
        // Благодаря фиксу в RestartGame, этот флаг теперь корректно сбрасывается.
        if (isDealing) return;
        if (undoManager != null && undoManager.IsUndoing) return;

        if (pileManager == null) return;
        var stock = pileManager.StockPile;
        var waste = pileManager.WastePile;
        if (stock == null || waste == null) return;

        if (stock.IsEmpty())
        {
            RecycleWasteToStock();
            return;
        }

        int cardsToDraw = (mode != null) ? (int)mode.stockDealMode : 1;

        List<CardController> movedCards = new List<CardController>();
        List<Transform> parents = new List<Transform>();
        List<Vector3> positions = new List<Vector3>();
        List<int> siblings = new List<int>();

        for (int i = 0; i < cardsToDraw; i++)
        {
            var card = stock.PopTop();
            if (card == null) break;

            movedCards.Add(card);
            parents.Add(stock.transform);
            positions.Add(Vector3.zero);
            siblings.Add(-1);

            card.rectTransform.SetParent(waste.transform, true);
            var cardData = card.GetComponent<CardData>();
            if (cardData != null) cardData.SetFaceUp(true, animate: true);
            if (card.canvasGroup != null) card.canvasGroup.blocksRaycasts = false;

            waste.OnCardArrivedFromStock(card, true);
        }

        if (movedCards.Count > 0 && undoManager != null)
        {
            undoManager.RecordMove(movedCards, stock, waste, parents, positions, siblings);
        }

        var anim = mode?.AnimationService;
        if (anim != null)
        {
            anim.ReorderContainerZ(stock.transform);
            anim.ReorderContainerZ(waste.transform);
        }
        Canvas.ForceUpdateCanvases();
        mode?.CheckGameState();
    }

    private void RecycleWasteToStock()
    {
        // Здесь проверку isDealing можно убрать или оставить.
        // Так как DrawFromStock уже проверил isDealing, сюда мы попадем только если колода свободна.

        if (pileManager == null) return;
        var stock = pileManager.StockPile;
        var waste = pileManager.WastePile;
        if (stock == null || waste == null) return;

        var wasteCards = waste.TakeAll();
        if (wasteCards == null || wasteCards.Count == 0) return;

        // Логика пата
        if (hasMadeMoveThisCycle)
        {
            passiveRecycleCount = 0;
        }
        else
        {
            bool moveOnTable = false;
            var defeatMgr = mode?.defeatManager ?? FindObjectOfType<DefeatManager>();
            if (defeatMgr != null && defeatMgr.HasAnyProductiveMoveOnTable()) moveOnTable = true;

            if (moveOnTable) passiveRecycleCount = 0;
            else passiveRecycleCount++;
        }
        hasMadeMoveThisCycle = false;

        // Undo запись
        mode.RegisterMoveAndStartIfNeeded();
        List<CardController> movedCards = new List<CardController>();
        List<Transform> parents = new List<Transform>();
        List<Vector3> positions = new List<Vector3>();
        List<int> siblings = new List<int>();

        for (int i = wasteCards.Count - 1; i >= 0; i--)
        {
            var card = wasteCards[i];
            movedCards.Add(card);
            parents.Add(waste.transform);
            positions.Add(card.rectTransform.anchoredPosition);
            siblings.Add(-1);
        }

        if (undoManager != null)
        {
            undoManager.RecordMove(movedCards, waste, stock, parents, positions, siblings);
        }

        StartCoroutine(AnimateRecycleRoutine(wasteCards, stock));
    }

    private IEnumerator AnimateRecycleRoutine(List<CardController> cardsToRecycle, StockPile stock)
    {
        // Включаем флаг анимации, чтобы нельзя было кликать пока карты летят
        isDealing = true;

        RectTransform layer = dragLayer ?? (mode?.RootCanvas?.transform as RectTransform);
        Vector3 targetWorldPos = stock.transform.position;
        float totalTime = 0.6f;
        float durationPerCard = Mathf.Clamp(totalTime / Mathf.Max(1, cardsToRecycle.Count), 0.02f, 0.2f);

        for (int i = cardsToRecycle.Count - 1; i >= 0; i--)
        {
            var card = cardsToRecycle[i];
            if (card == null) continue;
            if (layer != null) { card.rectTransform.SetParent(layer, true); card.rectTransform.SetAsLastSibling(); }
            if (card.canvasGroup != null) card.canvasGroup.blocksRaycasts = false;

            var cardData = card.GetComponent<CardData>();
            if (cardData != null) { cardData.flipDuration = durationPerCard * 0.9f; cardData.SetFaceUp(false, animate: true); }

            Vector3 startPos = card.rectTransform.position;
            float elapsed = 0f;
            while (elapsed < durationPerCard) { elapsed += Time.unscaledDeltaTime; card.rectTransform.position = Vector3.Lerp(startPos, targetWorldPos, elapsed / durationPerCard); yield return null; }

            card.rectTransform.position = targetWorldPos;
            card.rectTransform.SetParent(stock.transform, false);
            stock.AddCard(card, false);
            if (cardData != null) cardData.flipDuration = 0.2f;
        }
        mode?.AnimationService?.ReorderContainerZ(stock.transform);
        Canvas.ForceUpdateCanvases();

        // СНИМАЕМ ФЛАГ: теперь можно снова кликать
        isDealing = false;

        mode?.CheckGameState();
    }

    private void ApplyDeal(Deal deal)
    {
        if (pileManager == null || cardFactory == null)
        {
            isDealing = false; // Сброс, если ошибка
            return;
        }

        // Блокируем ввод
        if (mode != null) mode.IsInputAllowed = false;
        isDealing = true; // Гарантируем блокировку

        List<List<CardController>> pilesCardsToDeal = new List<List<CardController>>();
        for (int i = 0; i < 7; i++) pilesCardsToDeal.Add(new List<CardController>());

        // Создаем карты для Tableau в Stock
        for (int i = 0; i < 7; i++)
        {
            List<CardInstance> columnData = deal.tableau[i];
            foreach (var cardInst in columnData)
            {
                CardModel model = new CardModel(cardInst.Card.suit, cardInst.Card.rank);
                CardController card = cardFactory.CreateCard(model, pileManager.StockPile.transform, Vector2.zero);

                if (card != null)
                {
                    var data = card.GetComponent<CardData>();
                    data.SetFaceUp(false, animate: false);
                    if (card.canvasGroup) card.canvasGroup.blocksRaycasts = false;
                    pilesCardsToDeal[i].Add(card);
                    mode.RegisterCardEvents(card);
                }
            }
        }

        mode?.AnimationService?.ReorderContainerZ(pileManager.StockPile.transform);

        // Карты для Stock
        var stockList = new List<CardInstance>(deal.stock);
        stockList.Reverse();

        foreach (var cardInst in stockList)
        {
            CardModel model = new CardModel(cardInst.Card.suit, cardInst.Card.rank);
            CardController card = cardFactory.CreateCard(model, pileManager.StockPile.transform, Vector2.zero);
            if (card != null)
            {
                pileManager.StockPile.AddCard(card, false);
                mode.RegisterCardEvents(card);
            }
        }
        mode?.AnimationService?.ReorderContainerZ(pileManager.StockPile.transform);

        StartCoroutine(AnimateOpeningDeal(pilesCardsToDeal));
    }

    private IEnumerator AnimateOpeningDeal(List<List<CardController>> pilesCards)
    {
        int totalCardsToDeal = 28;
        float delayPerCard = dealingTotalDuration / (totalCardsToDeal + 2);
        float moveDuration = 0.25f;

        AnimationService animService = mode?.AnimationService;
        RectTransform flyLayer = dragLayer ?? (mode?.RootCanvas?.transform as RectTransform);
        float dealingGap = 10f;

        for (int row = 0; row < 7; row++)
        {
            for (int col = row; col < 7; col++)
            {
                if (col >= pilesCards.Count || row >= pilesCards[col].Count) continue;

                CardController card = pilesCards[col][row];
                TableauPile targetPile = pileManager.GetTableau(col);

                if (card != null && targetPile != null)
                {
                    if (flyLayer != null)
                    {
                        card.rectTransform.SetParent(flyLayer, true);
                        card.rectTransform.SetAsLastSibling();
                    }

                    Vector2 anchoredPos = new Vector2(0f, -row * dealingGap);
                    Vector3 worldPos = Vector3.zero;

                    if (animService != null) worldPos = animService.AnchoredToWorldPosition(targetPile.transform as RectTransform, anchoredPos);
                    else worldPos = targetPile.transform.position + (Vector3.down * (row * dealingGap * mode.RootCanvas.scaleFactor));

                    bool shouldFlip = (row == col);
                    StartCoroutine(MoveCardRoutine(card, worldPos, moveDuration, targetPile, shouldFlip));
                }
                yield return new WaitForSeconds(delayPerCard);
            }
        }

        yield return new WaitForSeconds(moveDuration);

        foreach (var pile in pileManager.Tableau)
        {
            if (pile != null) pile.ForceUpdateFromTransform();
        }

        // ВАЖНО: Разблокируем колоду и ввод
        isDealing = false;
        if (mode != null) mode.IsInputAllowed = true;
        LogDebug("Animated Deal Complete.");
    }

    private IEnumerator MoveCardRoutine(CardController card, Vector3 targetPos, float duration, TableauPile targetPile, bool endStateFaceUp)
    {
        Vector3 startPos = card.rectTransform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            card.rectTransform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        card.rectTransform.position = targetPos;
        targetPile.AddCard(card, endStateFaceUp);

        if (endStateFaceUp)
        {
            var data = card.GetComponent<CardData>();
            if (data != null) data.SetFaceUp(true, animate: true);
        }

        if (card.canvasGroup)
        {
            card.canvasGroup.blocksRaycasts = true;
        }
    }

    private void DealRandom()
    {
        // ... (Код рандома) ...
        // Если используете DealRandom, не забудьте в конце тоже снять флаги:
        if (mode != null) mode.IsInputAllowed = true;
        isDealing = false;
    }
    public void LoadDeal(Deal deal)
    {
        if (isDealing) return;

        // Ставим блокировку, как при обычной раздаче
        isDealing = true;
        if (mode != null) mode.IsInputAllowed = false;

        // Очищаем стол перед загрузкой
        ClearAllPiles();

        // Используем существующую логику создания карт
        ApplyDeal(deal);
    }
    private void ClearAllPiles()
    {
        if (pileManager == null) return;
        pileManager.ClearAllPiles();
    }

    public int GetRemainingCardCount()
    {
        if (pileManager?.StockPile != null) return pileManager.StockPile.GetCardCount();
        return deck?.Count ?? 0;
    }

    private void LogDebug(string message)
    {
        if (showDebugLogs) Debug.Log($"[DeckManager] {message}");
    }
}