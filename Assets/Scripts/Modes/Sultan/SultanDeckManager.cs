using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SultanDeckManager : MonoBehaviour
{
    [Header("References")]
    public SultanModeManager modeManager;
    public SultanPileManager pileManager;
    public CardFactory cardFactory;
    private SultanAnimationService _animService;

    [Header("Intro Animation")]
    public Transform offScreenSpawnPoint;

    [Header("Rules")]
    public int maxRecycles = 2;
    private int currentRecycles = 0;

    public bool HasRecyclesRemaining => currentRecycles < maxRecycles;
    public Difficulty currentDifficulty;

    public bool isDealing = false;
    public bool isSkippingIntro = false;

    public void Initialize(SultanModeManager mode, CardFactory factory, SultanPileManager piles)
    {
        modeManager = mode;
        cardFactory = factory;
        pileManager = piles;

        _animService = GetComponent<SultanAnimationService>();
        if (_animService == null) Debug.LogError("❌ [SultanDeckManager] Не найден SultanAnimationService!");
        else _animService.Initialize(mode);
    }

    private void Update()
    {
        if (isDealing && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
        {
            isSkippingIntro = true;
        }
    }

    private IEnumerator SkippableWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            yield return null;
        }
    }

    public void DealInitial()
    {
        currentRecycles = 0;
        StartCoroutine(DealRoutine());
    }

    private IEnumerator DealRoutine()
    {
        isDealing = true;
        isSkippingIntro = false;
        modeManager.IsInputAllowed = false;

        var intro = GetComponent<SultanIntroController>();
        if (intro != null) intro.PrepareIntro(modeManager.isRestarting);

        Deal deal = null;
        if (DealCacheSystem.Instance != null)
        {
            deal = DealCacheSystem.Instance.GetDeal(modeManager.GameType, currentDifficulty, 0);
        }

        // --- ГЛАВНЫЙ БАГФИКС СУЛТАНА ---
        // Если расклад пришел из кэша, в нем нет домов (DealCacheSystem их не сохраняет).
        // Мы вручную реконструируем 9 базовых карт!
        if (deal != null && (deal.foundations == null || deal.foundations.Count < 9 || deal.foundations[0].Count == 0))
        {
            deal.foundations = new List<List<CardModel>>();
            for (int i = 0; i < 9; i++) deal.foundations.Add(new List<CardModel>());

            deal.foundations[0].Add(new CardModel(Suit.Diamonds, 13));
            deal.foundations[1].Add(new CardModel(Suit.Hearts, 1));
            deal.foundations[2].Add(new CardModel(Suit.Diamonds, 13));
            deal.foundations[3].Add(new CardModel(Suit.Clubs, 13));
            deal.foundations[4].Add(new CardModel(Suit.Hearts, 13));
            deal.foundations[5].Add(new CardModel(Suit.Clubs, 13));
            deal.foundations[6].Add(new CardModel(Suit.Spades, 13));
            deal.foundations[7].Add(new CardModel(Suit.Hearts, 13));
            deal.foundations[8].Add(new CardModel(Suit.Spades, 13));
        }

        if (deal != null)
        {
            deal.tableau.RemoveAll(list => list == null || list.Count == 0);
        }

        bool isDealValid = deal != null && deal.tableau.Count >= 6 && deal.foundations.Count >= 9;

        // Если кэш оказался реально пуст
        if (!isDealValid)
        {
            var tempGenerator = gameObject.AddComponent<SultanGenerator>();

            // ИСПРАВЛЕНИЕ: Убрали жесткий хардкод Difficulty.Medium!
            yield return StartCoroutine(tempGenerator.GenerateDeal(currentDifficulty, 0, (d, metrics) => { deal = d; }));
            Destroy(tempGenerator);

            if (deal != null) deal.tableau.RemoveAll(list => list == null || list.Count == 0);
        }

        if (intro != null) yield return StartCoroutine(intro.AnimateUIAndSlots(modeManager.isRestarting));

        Vector3 spawnPos = offScreenSpawnPoint != null ? offScreenSpawnPoint.position : new Vector3(0, -2000, 0);

        var stockArray = deal.stock.ToArray();
        List<CardController> stockCards = new List<CardController>();

        for (int i = stockArray.Length - 1; i >= 0; i--)
        {
            var card = SpawnCard(stockArray[i].Card, modeManager.DragLayer, false);
            stockCards.Add(card);
        }

        pileManager.StockPile.ClearWithoutUpdate();
        foreach (var c in stockCards) pileManager.StockPile.AddCard(c, false);
        pileManager.StockPile.UpdateOffsets();

        List<Vector3> targetPositions = new List<Vector3>();
        foreach (var c in stockCards)
        {
            targetPositions.Add(c.transform.position);
            c.transform.SetParent(modeManager.DragLayer, true);
            c.transform.position = spawnPos;
        }

        if (AudioManager.Instance != null && stockCards.Count > 0)
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", 0.45f, 0.2f);

        float stockDuration = 0.45f;
        float elapsed = 0f;
        while (elapsed < stockDuration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = elapsed / stockDuration;
            float curvedT = t * t * (3f - 2f * t);

            for (int i = 0; i < stockCards.Count; i++)
            {
                stockCards[i].transform.position = Vector3.Lerp(spawnPos, targetPositions[i], curvedT);
            }
            yield return null;
        }

        pileManager.StockPile.ClearWithoutUpdate();
        foreach (var c in stockCards) pileManager.StockPile.AddCard(c, false);
        pileManager.StockPile.UpdateOffsets();

        yield return StartCoroutine(SkippableWait(0.1f));

        Vector3 topStockPos = pileManager.StockPile.transform.position;
        if (pileManager.StockPile.transform.childCount > 0)
        {
            topStockPos = pileManager.StockPile.transform.GetChild(pileManager.StockPile.transform.childCount - 1).position;
        }

        int fIndex = 0;
        for (int i = 0; i < 9; i++)
        {
            bool isCenter = (i == 4);
            ICardContainer targetPile = isCenter ? (ICardContainer)pileManager.CenterPile : pileManager.Foundations[fIndex];

            var card = SpawnCard(deal.foundations[i][0], modeManager.DragLayer, false);
            card.transform.position = topStockPos;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

            StartCoroutine(FlyCardRoutine(card, targetPile, 0.25f, true));

            if (!isCenter) fIndex++;
            yield return StartCoroutine(SkippableWait(0.06f));
        }

        for (int i = 0; i < 6; i++)
        {
            var card = SpawnCard(deal.tableau[i][0].Card, modeManager.DragLayer, false);
            card.transform.position = topStockPos;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

            StartCoroutine(FlyCardRoutine(card, pileManager.Reserves[i], 0.25f, true));
            yield return StartCoroutine(SkippableWait(0.06f));
        }

        yield return StartCoroutine(SkippableWait(0.3f));

        isDealing = false;
        isSkippingIntro = false;
        modeManager.isRestarting = false;
        modeManager.IsInputAllowed = true;
        modeManager.CheckGameState();
    }

    private IEnumerator FlyCardRoutine(CardController card, ICardContainer targetPile, float duration, bool flipFaceUp)
    {
        Vector3 startPos = card.transform.position;
        Vector3 targetPos = targetPile.Transform.position;
        float elapsed = 0f;
        bool flipped = false;

        var data = card.GetComponent<CardData>();

        while (elapsed < duration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = t * t * (3f - 2f * t);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, curvedT);
            currentPos.z -= 1f;
            card.transform.position = currentPos;

            if (flipFaceUp && t > 0.5f && !flipped)
            {
                if (data != null) data.SetFaceUp(true, true);
                flipped = true;
            }

            yield return null;
        }

        card.transform.position = targetPos;
        if (flipFaceUp && !flipped && data != null) data.SetFaceUp(true, false);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");

        targetPile.AcceptCard(card);
    }

    private CardController SpawnCard(CardModel model, Transform parent, bool faceUp)
    {
        var cardObj = cardFactory.CreateCard(model, parent, Vector2.zero);

        var oldCtrl = cardObj.GetComponent<CardController>();
        if (oldCtrl != null && !(oldCtrl is SultanCardController)) DestroyImmediate(oldCtrl);

        var newCtrl = cardObj.GetComponent<SultanCardController>();
        if (newCtrl == null) newCtrl = cardObj.gameObject.AddComponent<SultanCardController>();

        newCtrl.cardModel = model;
        newCtrl.canvas = modeManager.RootCanvas;
        newCtrl.CardmodeManager = modeManager;

        var data = cardObj.GetComponent<CardData>();
        if (data != null) data.SetFaceUp(faceUp, false);

        newCtrl.transform.localScale = Vector3.one;
        return newCtrl;
    }

    public void DrawFromStock()
    {
        if (isDealing) return;

        int stockCount = pileManager.StockPile.transform.childCount;

        if (stockCount > 0)
        {
            var cardTr = pileManager.StockPile.transform.GetChild(stockCount - 1);
            var card = cardTr.GetComponent<CardController>();

            if (modeManager.undoManager != null)
            {
                List<CardController> movedCards = new List<CardController> { card };
                List<Transform> parents = new List<Transform> { pileManager.StockPile.transform };
                List<Vector3> positions = new List<Vector3> { card.transform.localPosition };
                List<int> siblings = new List<int> { card.transform.GetSiblingIndex() };

                modeManager.undoManager.RecordMove(movedCards, pileManager.StockPile, pileManager.WastePile, parents, positions, siblings);
            }

            if (modeManager.scoreManager != null) modeManager.scoreManager.BreakStreak();

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

            _animService.AnimateStockToWaste(card, pileManager.WastePile);
        }
        else if (currentRecycles < maxRecycles)
        {
            RecycleWasteToStock();
        }
    }

    private void RecycleWasteToStock()
    {
        currentRecycles++;

        if (modeManager.scoreManager != null)
        {
            modeManager.scoreManager.OnDeckRecycled();
            modeManager.RegisterMoveAndStartIfNeeded();
        }

        int count = pileManager.WastePile.transform.childCount;

        List<CardController> movedCards = new List<CardController>();
        List<Transform> parents = new List<Transform>();
        List<Vector3> positions = new List<Vector3>();
        List<int> siblings = new List<int>();

        for (int i = count - 1; i >= 0; i--)
        {
            Transform t = pileManager.WastePile.transform.GetChild(i);
            CardController c = t.GetComponent<CardController>();

            movedCards.Add(c);
            parents.Add(pileManager.WastePile.transform);
            positions.Add(t.localPosition);
            siblings.Add(t.GetSiblingIndex());
        }

        if (modeManager.undoManager != null)
        {
            modeManager.undoManager.RecordMove(movedCards, pileManager.WastePile, pileManager.StockPile, parents, positions, siblings);
        }

        StartCoroutine(AnimateRecycleRoutine(movedCards));
    }

    private IEnumerator AnimateRecycleRoutine(List<CardController> cardsToRecycle)
    {
        isDealing = true;
        isSkippingIntro = false;
        modeManager.IsInputAllowed = false;

        float flightDuration = 0.15f;
        float staggerDelay = 0.015f;

        for (int i = 0; i < cardsToRecycle.Count; i++)
        {
            var card = cardsToRecycle[i];
            if (card == null) continue;

            StartCoroutine(RecycleSingleCardRoutine(card, flightDuration, i * staggerDelay));
        }

        float totalTime = flightDuration + (cardsToRecycle.Count * staggerDelay);
        yield return StartCoroutine(SkippableWait(totalTime));

        if (pileManager.StockPile != null) pileManager.StockPile.UpdateOffsets();

        isDealing = false;
        isSkippingIntro = false;
        modeManager.IsInputAllowed = true;
        modeManager.CheckGameState();
    }

    private IEnumerator RecycleSingleCardRoutine(CardController card, float duration, float delay)
    {
        if (delay > 0) yield return StartCoroutine(SkippableWait(delay));

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

        card.transform.SetParent(modeManager.DragLayer, true);
        card.transform.SetAsLastSibling();

        var cardData = card.GetComponent<CardData>();
        if (cardData != null)
        {
            cardData.SetFaceUp(false, false);
        }

        Vector3 startPos = card.transform.position;
        Vector3 targetPos = pileManager.StockPile.transform.position;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            card.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        card.transform.position = targetPos;
        pileManager.StockPile.AddCard(card, false);
        pileManager.StockPile.UpdateOffsets();
    }
}