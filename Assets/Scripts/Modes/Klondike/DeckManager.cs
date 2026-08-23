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
    public bool isSkippingIntro = false;
    public bool forceInstantSkip = false; // [NEW] Экстренный пропуск

    [Header("Intro Animation")]
    public Transform landscapeSpawnPoint;
    public Transform portraitSpawnPoint;

    private List<List<CardController>> pendingTableauDeals;
    public bool IsRecycling { get; private set; } = false;

    [Header("Stalemate Logic")]
    public int maxPassiveRecycles = 2;
    private int passiveRecycleCount = 0;
    private bool hasMadeMoveThisCycle = false;

    public bool IsStalemateReached => passiveRecycleCount >= maxPassiveRecycles && !hasMadeMoveThisCycle;

    public bool isDealing = false;
    public bool IsDealing => isDealing;

    private bool lastPortraitState; // [NEW] Для отслеживания поворота

    public void Initialize(KlondikeModeManager km, CardFactory cf = null, PileManager pm = null)
    {
        mode = km ?? mode;
        cardFactory = cf ?? cardFactory ?? mode?.cardFactory;
        pileManager = pm ?? pileManager ?? mode?.pileManager;
        undoManager = mode?.undoManager ?? GetComponent<UndoManager>() ?? FindObjectOfType<UndoManager>();

        if (generator == null) generator = FindObjectOfType<BaseGenerator>();
        if (mode != null && dragLayer == null) dragLayer = mode.DragLayer;

        if (GameLayoutManager.Instance != null) lastPortraitState = GameLayoutManager.Instance.IsPortrait;

        LogDebug($"Initialized. Generator found: {generator != null}");
    }

    private void Update()
    {
        // Обычный пропуск по клику
        if ((isDealing || IsRecycling) && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
        {
            isSkippingIntro = true;
        }

        // [NEW] Экстренный пропуск при повороте экрана
        if (GameLayoutManager.Instance != null)
        {
            bool currentPortrait = GameLayoutManager.Instance.IsPortrait;
            if (currentPortrait != lastPortraitState)
            {
                lastPortraitState = currentPortrait;
                if (isDealing || IsRecycling)
                {
                    isSkippingIntro = true;
                    forceInstantSkip = true;
                    Debug.Log("[DeckManager] Смена ориентации во время анимации! Пропускаем анимацию.");
                }
            }
        }
    }

    public void RestartGame()
    {
        StopAllCoroutines();
        isDealing = false;
        IsRecycling = false;
        isSkippingIntro = false;
        forceInstantSkip = false;

        ResetStalemate();
        if (mode != null && mode.undoManager != null) mode.undoManager.ResetHistory();
        ClearAllPiles();
        if (cardFactory != null) cardFactory.DestroyAllCards();

        if (mode != null)
        {
            mode.StartNewGame();
            mode.IsInputAllowed = true;
        }
        DealInitial();
    }

    public void ResetStalemate()
    {
        passiveRecycleCount = 0;
    }

    public void DealInitial()
    {
        if (isDealing) return;

        isSkippingIntro = false;
        forceInstantSkip = false;
        isDealing = true;
        if (mode != null) mode.IsInputAllowed = false;

        ClearAllPiles();
        int drawCount = (mode != null) ? (int)mode.stockDealMode : 1;

        if (DealCacheSystem.Instance != null && generator != null)
        {
            GameType gType = generator.GameType;
            Deal cachedDeal = DealCacheSystem.Instance.GetDeal(gType, difficulty, drawCount);

            if (cachedDeal != null)
            {
                ApplyDeal(cachedDeal, animate: true);
                return;
            }
        }

        if (generator != null) StartCoroutine(generator.GenerateDeal(difficulty, drawCount, OnDealGenerated));
        else
        {
            isDealing = false;
            if (mode != null) mode.IsInputAllowed = true;
        }
    }

    private void OnDealGenerated(Deal deal, DealMetrics metrics)
    {
        if (deal != null) ApplyDeal(deal, animate: true);
        else
        {
            isDealing = false;
            if (mode != null) mode.IsInputAllowed = true;
        }
    }

    public IEnumerator PlayIntroDeckArrival(float duration)
    {
        isSkippingIntro = false;
        forceInstantSkip = false;
        PrepareRealCardsForIntro();

        if (pileManager == null || pileManager.StockPile == null) yield break;
        var stock = pileManager.StockPile;

        List<Transform> allCards = new List<Transform>();
        foreach (Transform child in stock.transform)
        {
            if (child.GetComponent<CardController>()) allCards.Add(child);
        }

        if (allCards.Count == 0) yield break;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", duration, 0.2f);

        bool isPortrait = GameLayoutManager.Instance != null && GameLayoutManager.Instance.IsPortrait;
        Transform activeSpawnPoint = isPortrait ? portraitSpawnPoint : landscapeSpawnPoint;

        Vector3 fallbackOffset = isPortrait ? new Vector3(0, -1500, 0) : new Vector3(1500, 0, 0);
        Vector3 spawnPos = activeSpawnPoint != null ? activeSpawnPoint.position : stock.transform.position - fallbackOffset;

        Vector3[] startPositions = new Vector3[allCards.Count];
        Vector3[] targetPositions = new Vector3[allCards.Count];

        for (int i = 0; i < allCards.Count; i++)
        {
            startPositions[i] = spawnPos;
            targetPositions[i] = stock.GetWorldPositionForIndex(i);
            allCards[i].position = startPositions[i];
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (forceInstantSkip) break; // [NEW] Прерываем анимацию

            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < allCards.Count; i++)
            {
                allCards[i].position = Vector3.Lerp(startPositions[i], targetPositions[i], t);
            }
            yield return null;
        }

        // [NEW] Запрашиваем новые координаты на случай, если они изменились из-за поворота
        for (int i = 0; i < allCards.Count; i++)
        {
            allCards[i].position = stock.GetWorldPositionForIndex(i);
        }

        mode?.AnimationService?.ReorderContainerZ(stock.transform);

        if (pendingTableauDeals != null)
        {
            yield return StartCoroutine(AnimateOpeningDeal(pendingTableauDeals));
            pendingTableauDeals = null;
        }
    }

    private void PrepareRealCardsForIntro()
    {
        isDealing = true;
        isSkippingIntro = false;
        forceInstantSkip = false;
        if (mode != null) mode.IsInputAllowed = false;
        ClearAllPiles();

        int drawCount = (mode != null) ? (int)mode.stockDealMode : 1;
        Deal deal = null;

        if (DealCacheSystem.Instance != null && generator != null)
            deal = DealCacheSystem.Instance.GetDeal(generator.GameType, difficulty, drawCount);

        if (deal != null) ApplyDeal(deal, animate: false);
        else isDealing = false;
    }

    private void ApplyDeal(Deal deal, bool animate)
    {
        if (pileManager == null || cardFactory == null)
        {
            isDealing = false;
            return;
        }

        pendingTableauDeals = new List<List<CardController>>();
        for (int i = 0; i < 7; i++) pendingTableauDeals.Add(new List<CardController>());

        var stock = pileManager.StockPile;
        var stockList = new List<CardInstance>(deal.stock);
        stockList.Reverse();

        foreach (var cardInst in stockList)
        {
            CardModel model = new CardModel(cardInst.Card.suit, cardInst.Card.rank);
            CardController card = cardFactory.CreateCard(model, stock.transform, Vector2.zero);
            if (card != null)
            {
                stock.AddCard(card, false);
                mode.RegisterCardEvents(card);
            }
        }

        mode?.AnimationService?.ReorderContainerZ(stock.transform);

        Vector3 launchPos = stock.transform.position;
        int currentStockCount = stock.GetCardCount();
        if (currentStockCount > 0)
        {
            launchPos = stock.GetWorldPositionForIndex(currentStockCount - 1);
        }

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
                    card.transform.position = launchPos;
                    card.transform.localPosition -= new Vector3(0, 0, 0.05f);

                    card.rectTransform.SetAsLastSibling();

                    var data = card.GetComponent<CardData>();
                    data.SetFaceUp(false, animate: false);
                    if (card.canvasGroup) card.canvasGroup.blocksRaycasts = false;

                    pendingTableauDeals[col].Add(card);
                    mode.RegisterCardEvents(card);
                }
            }
        }

        foreach (var list in pendingTableauDeals)
        {
            list.Reverse();
        }

        if (animate)
        {
            StartCoroutine(AnimateOpeningDeal(pendingTableauDeals));
            pendingTableauDeals = null;
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

                    Vector3 worldPos = Vector3.zero;
                    Vector2 anchoredTarget = new Vector2(0f, -row * dealingGap);

                    if (animService != null)
                        worldPos = animService.AnchoredToWorldPosition(targetPile.transform as RectTransform, anchoredTarget);
                    else
                        worldPos = targetPile.transform.position + (Vector3.down * (row * dealingGap * mode.RootCanvas.scaleFactor));

                    bool shouldFlip = (row == col);

                    if (AudioManager.Instance != null && !forceInstantSkip)
                        AudioManager.Instance.PlaySound("Card_Deal");

                    StartCoroutine(MoveCardRoutine(card, worldPos, moveDuration, targetPile, shouldFlip));
                }

                float waitTimer = 0f;
                while (waitTimer < delayPerCard)
                {
                    if (forceInstantSkip) break; // [NEW] Прерываем паузы
                    float speed = isSkippingIntro ? 15f : 1f;
                    waitTimer += Time.deltaTime * speed;
                    yield return null;
                }
            }
        }

        float finalTimer = 0f;
        while (finalTimer < moveDuration)
        {
            if (forceInstantSkip) break; // [NEW] Прерываем финальную паузу
            float speed = isSkippingIntro ? 15f : 1f;
            finalTimer += Time.deltaTime * speed;
            yield return null;
        }

        foreach (var pile in pileManager.Tableau)
        {
            if (pile != null) pile.ForceUpdateFromTransform();
        }

        isDealing = false;
        forceInstantSkip = false;
        if (mode != null) mode.IsInputAllowed = true;
    }

    private IEnumerator MoveCardRoutine(CardController card, Vector3 targetPos, float duration, TableauPile targetPile, bool endStateFaceUp)
    {
        Vector3 startPos = card.rectTransform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (forceInstantSkip) break; // [NEW] Прерываем полет
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            card.rectTransform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        // [NEW] Ставим карту на актуальную позицию стопки (защита от устаревших координат)
        card.rectTransform.position = targetPile.transform.position;
        targetPile.AddCard(card, endStateFaceUp);

        if (AudioManager.Instance != null && !forceInstantSkip)
            AudioManager.Instance.PlaySound("Card_Drop_Success");

        if (endStateFaceUp)
        {
            var data = card.GetComponent<CardData>();
            // Если экстренно пропустили - разворачиваем мгновенно
            if (data != null) data.SetFaceUp(true, animate: !forceInstantSkip);
        }

        if (card.canvasGroup) card.canvasGroup.blocksRaycasts = true;
    }

    public void LoadDeal(Deal deal)
    {
        if (isDealing) return;
        isSkippingIntro = false;
        forceInstantSkip = false;
        isDealing = true;
        if (mode != null) mode.IsInputAllowed = false;
        ClearAllPiles();
        ApplyDeal(deal, animate: true);
    }

    public void DrawFromStock()
    {
        if ((isDealing || IsRecycling) && pileManager != null && pileManager.StockPile != null && pileManager.StockPile.IsEmpty())
        {
            isDealing = false;
            IsRecycling = false;
        }

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
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Deal");
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
        isSkippingIntro = false;
        forceInstantSkip = false;

        if (pileManager == null)
        {
            IsRecycling = false; isDealing = false;
            return;
        }

        var stock = pileManager.StockPile;
        var waste = pileManager.WastePile;
        if (stock == null || waste == null)
        {
            IsRecycling = false; isDealing = false;
            return;
        }

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
            try
            {
                var defeatMgr = mode?.defeatManager ?? FindObjectOfType<DefeatManager>();
                if (defeatMgr != null && defeatMgr.HasAnyProductiveMoveOnTable()) moveOnTable = true;
            }
            catch (System.Exception e) { Debug.LogWarning($"[DeckManager] Ошибка DefeatManager: {e.Message}"); }

            if (moveOnTable) passiveRecycleCount = 0;
            else passiveRecycleCount++;
        }
        hasMadeMoveThisCycle = false;

        if (mode != null) mode.RegisterMoveAndStartIfNeeded();

        if (mode != null && mode.scoreManager is KlondikeScoreManager kScore)
        {
            kScore.OnDeckRecycled();
        }

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

        try
        {
            StartCoroutine(AnimateRecycleRoutine(wasteCards, stock));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DeckManager] Ошибка запуска корутины возврата: {e.Message}");
            isDealing = false; IsRecycling = false;
        }
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
            if (AudioManager.Instance != null && !forceInstantSkip)
                AudioManager.Instance.PlaySound("Card_Deal");
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
                // Мгновенный переворот, если экстренный пропуск
                cardData.SetFaceUp(false, animate: !forceInstantSkip);
            }

            Vector3 startPos = card.rectTransform.position;
            float elapsed = 0f;
            while (elapsed < durationPerCard)
            {
                if (forceInstantSkip) break; // [NEW] Экстренный пропуск
                float speed = isSkippingIntro ? 15f : 1f;
                elapsed += Time.unscaledDeltaTime * speed;
                card.rectTransform.position = Vector3.Lerp(startPos, targetBasePos, Mathf.Clamp01(elapsed / durationPerCard));
                yield return null;
            }

            // [NEW] Принудительно забираем актуальную позицию слота
            card.rectTransform.position = stock.transform.position;
            card.rectTransform.SetParent(stock.transform, true);

            stock.AddCard(card, false);

            if (cardData != null) cardData.flipDuration = 0.2f;
        }

        mode?.AnimationService?.ReorderContainerZ(stock.transform);
        Canvas.ForceUpdateCanvases();

        isDealing = false;
        IsRecycling = false;
        isSkippingIntro = false;
        forceInstantSkip = false;

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