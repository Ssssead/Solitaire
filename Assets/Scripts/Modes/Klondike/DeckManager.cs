using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [Header("Generation Settings")]
    public Difficulty difficulty = Difficulty.Medium;
    public BaseGenerator generator;

    [Header("Settings")]
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

    [Header("Intro Animation")]
    public Transform offScreenSpawnPoint;
    private List<List<CardController>> pendingTableauDeals;
    public bool IsRecycling { get; private set; } = false;

    [Header("Stalemate Logic")]
    public int maxPassiveRecycles = 2;
    private int passiveRecycleCount = 0;
    private bool hasMadeMoveThisCycle = false;

    public bool IsStalemateReached => passiveRecycleCount >= maxPassiveRecycles && !hasMadeMoveThisCycle;

    // Флаг, указывающий, что идет процесс раздачи или генерации
    // Сделал публичным свойством, чтобы KlondikeModeManager мог читать его
    public bool isDealing = false;
    public bool IsDealing => isDealing;

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

        // 2. СБРОС ФЛАГА
        isDealing = false;
        IsRecycling = false;

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

    // --- СТРОГАЯ ЛОГИКА РАЗДАЧИ ---
    public void DealInitial()
    {
        if (isDealing)
        {
            LogDebug("DealInitial ignored: already dealing.");
            return;
        }

        isDealing = true;
        if (mode != null) mode.IsInputAllowed = false;

        ClearAllPiles();

        int drawCount = (mode != null) ? (int)mode.stockDealMode : 1;

        // 1. ПЫТАЕМСЯ ВЗЯТЬ ИЗ КЭША
        if (DealCacheSystem.Instance != null && generator != null)
        {
            // Важно: берем GameType из генератора или явно Klondike, чтобы не перепутать с Sultan
            GameType gType = generator.GameType;
            Deal cachedDeal = DealCacheSystem.Instance.GetDeal(gType, difficulty, drawCount);

            if (cachedDeal != null)
            {
                LogDebug("Deal found in Cache! Applying...");
                ApplyDeal(cachedDeal, animate: true);
                return;
            }
            else
            {
                LogDebug("Cache empty. Requesting new generation...");
            }
        }

        // 2. ЕСЛИ В КЭШЕ ПУСТО -> ГЕНЕРИРУЕМ (Но не рандом!)
        if (generator != null)
        {
            StartCoroutine(generator.GenerateDeal(difficulty, drawCount, OnDealGenerated));
        }
        else
        {
            // КРИТИЧЕСКАЯ ОШИБКА: Нет ни кэша, ни генератора.
            Debug.LogError("[DeckManager] FATAL: No Generator and No Cache! Cannot deal smart game.");
            isDealing = false;
            if (mode != null) mode.IsInputAllowed = true;
        }
    }

    private void OnDealGenerated(Deal deal, DealMetrics metrics)
    {
        if (deal != null)
        {
            ApplyDeal(deal, animate: true);
        }
        else
        {
            Debug.LogError("[DeckManager] Generator failed to produce a deal. Game cannot start.");
            isDealing = false;
            if (mode != null) mode.IsInputAllowed = true;
        }
    }

    // --- ИНТРО ЛОГИКА ---
    public IEnumerator PlayIntroDeckArrival(float duration)
    {
        // 1. Создаем карты (скрыто, в StockPile)
        PrepareRealCardsForIntro();

        if (pileManager == null || pileManager.StockPile == null) yield break;
        var stock = pileManager.StockPile;

        // 2. Собираем карты для анимации
        List<Transform> allCards = new List<Transform>();
        foreach (Transform child in stock.transform)
        {
            if (child.GetComponent<CardController>()) allCards.Add(child);
        }

        if (allCards.Count == 0) yield break;

        // 3. Анимация полета колоды из-за экрана
        Vector3 spawnPos = offScreenSpawnPoint != null ? offScreenSpawnPoint.position : stock.transform.position - new Vector3(1500, 0, 0);

        Vector3[] startPositions = new Vector3[allCards.Count];
        Vector3[] targetPositions = new Vector3[allCards.Count];

        for (int i = 0; i < allCards.Count; i++)
        {
            startPositions[i] = spawnPos;
            // Каждая карта летит на свое место (с учетом GetWorldPositionForIndex из StockPile)
            targetPositions[i] = stock.GetWorldPositionForIndex(i);
            allCards[i].position = startPositions[i];
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            for (int i = 0; i < allCards.Count; i++)
            {
                allCards[i].position = Vector3.Lerp(startPositions[i], targetPositions[i], t);
            }
            yield return null;
        }

        // Финализация позиций
        for (int i = 0; i < allCards.Count; i++) allCards[i].position = targetPositions[i];

        mode?.AnimationService?.ReorderContainerZ(stock.transform);

        // 4. Запускаем раздачу на стол
        if (pendingTableauDeals != null)
        {
            yield return StartCoroutine(AnimateOpeningDeal(pendingTableauDeals));
            pendingTableauDeals = null;
        }
    }

    private void PrepareRealCardsForIntro()
    {
        isDealing = true;
        if (mode != null) mode.IsInputAllowed = false;
        ClearAllPiles();

        int drawCount = (mode != null) ? (int)mode.stockDealMode : 1;
        Deal deal = null;

        // Пытаемся взять синхронно из кэша
        if (DealCacheSystem.Instance != null && generator != null)
        {
            deal = DealCacheSystem.Instance.GetDeal(generator.GameType, difficulty, drawCount);
        }

        if (deal != null)
        {
            // Создаем карты, но БЕЗ запуска анимации (animate: false)
            ApplyDeal(deal, animate: false);
        }
        else
        {
            Debug.LogError("[DeckManager] Intro failed: Cache is empty. Cannot start intro.");
            isDealing = false;
        }
    }

    // --- ПРИМЕНЕНИЕ РАСКЛАДА (Визуальная логика) ---
    private void ApplyDeal(Deal deal, bool animate)
    {
        if (pileManager == null || cardFactory == null)
        {
            isDealing = false;
            return;
        }

        // Подготавливаем списки
        pendingTableauDeals = new List<List<CardController>>();
        for (int i = 0; i < 7; i++) pendingTableauDeals.Add(new List<CardController>());

        var stock = pileManager.StockPile;

        // --- ШАГ 1: Создаем карты STOCK (ОСТАТОК) ---
        // Эти карты лежат в основании.
        var stockList = new List<CardInstance>(deal.stock);
        stockList.Reverse(); // Обычно нужно развернуть, если в JSON порядок Top->Bottom

        foreach (var cardInst in stockList)
        {
            CardModel model = new CardModel(cardInst.Card.suit, cardInst.Card.rank);
            CardController card = cardFactory.CreateCard(model, stock.transform, Vector2.zero);
            if (card != null)
            {
                stock.AddCard(card, false); // AddCard сам выставит смещение
                mode.RegisterCardEvents(card);
            }
        }

        mode?.AnimationService?.ReorderContainerZ(stock.transform);

        // --- ШАГ 2: Определяем точку вылета (Вершина стопки) ---
        Vector3 launchPos = stock.transform.position;
        int currentStockCount = stock.GetCardCount();
        if (currentStockCount > 0)
        {
            launchPos = stock.GetWorldPositionForIndex(currentStockCount - 1);
        }

        // --- ШАГ 3: Создаем карты TABLEAU (В ОБРАТНОМ ПОРЯДКЕ) ---
        // Идем с 6-го ряда до 0-го. Карта 0-го ряда (первая вылетающая) создается ПОСЛЕДНЕЙ,
        // поэтому она будет лежать ПОВЕРХ всех (Last Sibling).

        for (int row = 6; row >= 0; row--)
        {
            for (int col = 6; col >= row; col--)
            {
                List<CardInstance> columnData = deal.tableau[col];
                if (row >= columnData.Count) continue;

                var cardInst = columnData[row];
                CardModel model = new CardModel(cardInst.Card.suit, cardInst.Card.rank);

                CardController card = cardFactory.CreateCard(model, stock.transform, Vector2.zero);

                if (card != null)
                {
                    // Ставим ВСЕ карты в одну точку (верхушка стока)
                    card.transform.position = launchPos;
                    // Чуть приподнимаем по Z (к камере), чтобы не мерцало
                    card.transform.localPosition -= new Vector3(0, 0, 0.05f);

                    // Гарантируем отрисовку поверх всего
                    card.rectTransform.SetAsLastSibling();

                    var data = card.GetComponent<CardData>();
                    data.SetFaceUp(false, animate: false);
                    if (card.canvasGroup) card.canvasGroup.blocksRaycasts = false;

                    pendingTableauDeals[col].Add(card);
                    mode.RegisterCardEvents(card);
                }
            }
        }

        // Разворачиваем списки обратно, чтобы анимация шла [Card0, Card1...]
        foreach (var list in pendingTableauDeals)
        {
            list.Reverse();
        }

        // Запускаем анимацию разлета
        if (animate)
        {
            StartCoroutine(AnimateOpeningDeal(pendingTableauDeals));
            pendingTableauDeals = null; // Очищаем ссылку, т.к. использовали
        }
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

                    // Вычисляем цель
                    Vector3 worldPos = Vector3.zero;
                    Vector2 anchoredTarget = new Vector2(0f, -row * dealingGap);

                    if (animService != null)
                        worldPos = animService.AnchoredToWorldPosition(targetPile.transform as RectTransform, anchoredTarget);
                    else
                        worldPos = targetPile.transform.position + (Vector3.down * (row * dealingGap * mode.RootCanvas.scaleFactor));

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

    // --- ЛОГИКА КОЛОДЫ (Draw / Recycle) ---
    public void LoadDeal(Deal deal)
    {
        if (isDealing) return;
        isDealing = true;
        if (mode != null) mode.IsInputAllowed = false;
        ClearAllPiles();
        ApplyDeal(deal, animate: true);
    }

    public void DrawFromStock()
    {
        if (isDealing || IsRecycling) return;
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
        IsRecycling = true;
        isDealing = true;

        if (pileManager == null) return;
        var stock = pileManager.StockPile;
        var waste = pileManager.WastePile;
        if (stock == null || waste == null) return;

        var wasteCards = waste.TakeAll();
        if (wasteCards == null || wasteCards.Count == 0)
        {
            IsRecycling = false;
            isDealing = false;
            return;
        }

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
        isDealing = true;
        IsRecycling = true;

        if (mode != null) mode.IsInputAllowed = false;

        RectTransform layer = dragLayer ?? (mode?.RootCanvas?.transform as RectTransform);
        Vector3 targetBasePos = stock.transform.position;

        float totalTime = 0.5f;
        float durationPerCard = Mathf.Clamp(totalTime / Mathf.Max(1, cardsToRecycle.Count), 0.02f, 0.15f);

        for (int i = cardsToRecycle.Count - 1; i >= 0; i--)
        {
            var card = cardsToRecycle[i];
            if (card == null) continue;

            if (layer != null)
            {
                card.rectTransform.SetParent(layer, true);
                card.rectTransform.SetAsLastSibling();
            }
            if (card.canvasGroup != null) card.canvasGroup.blocksRaycasts = false;

            var cardData = card.GetComponent<CardData>();
            if (cardData != null)
            {
                cardData.flipDuration = durationPerCard * 0.9f;
                cardData.SetFaceUp(false, animate: true);
            }

            Vector3 startPos = card.rectTransform.position;
            float elapsed = 0f;
            while (elapsed < durationPerCard)
            {
                elapsed += Time.unscaledDeltaTime;
                card.rectTransform.position = Vector3.Lerp(startPos, targetBasePos, elapsed / durationPerCard);
                yield return null;
            }

            card.rectTransform.position = targetBasePos;
            card.rectTransform.SetParent(stock.transform, false);

            // AddCard сам расставит смещения
            stock.AddCard(card, false);

            if (cardData != null) cardData.flipDuration = 0.2f;
        }

        mode?.AnimationService?.ReorderContainerZ(stock.transform);
        Canvas.ForceUpdateCanvases();

        isDealing = false;
        IsRecycling = false;

        if (mode != null) mode.IsInputAllowed = true;

        mode?.CheckGameState();
    }

    public void OnProductiveMoveMade()
    {
        passiveRecycleCount = 0;
        hasMadeMoveThisCycle = true;
    }

    private void ClearAllPiles()
    {
        if (pileManager == null) return;
        pileManager.ClearAllPiles();
    }

    public int GetRemainingCardCount()
    {
        if (pileManager?.StockPile != null) return pileManager.StockPile.GetCardCount();
        return 0;
    }

    private void LogDebug(string message)
    {
        if (showDebugLogs) Debug.Log($"[DeckManager] {message}");
    }
}