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

    public void Initialize(SultanModeManager mode, CardFactory factory, SultanPileManager piles)
    {
        modeManager = mode;
        cardFactory = factory;
        pileManager = piles;

        _animService = GetComponent<SultanAnimationService>();
        if (_animService == null) Debug.LogError("❌ [SultanDeckManager] Не найден SultanAnimationService!");
        else _animService.Initialize(mode);
    }

    public void DealInitial()
    {
        currentRecycles = 0;
        StartCoroutine(DealRoutine());
    }

    private IEnumerator DealRoutine()
    {
        modeManager.IsInputAllowed = false;

        var intro = GetComponent<SultanIntroController>();
        if (intro != null) intro.PrepareIntro();

        Deal deal = null;

        if (DealCacheSystem.Instance != null)
            deal = DealCacheSystem.Instance.GetDeal(modeManager.GameType, Difficulty.Medium, 0);

        if (deal != null)
        {
            deal.tableau.RemoveAll(list => list == null || list.Count == 0);
            deal.foundations.RemoveAll(list => list == null || list.Count == 0);
        }

        bool isDealValid = deal != null && deal.tableau.Count >= 6 && deal.foundations.Count >= 9;

        if (!isDealValid)
        {
            var tempGenerator = gameObject.AddComponent<SultanGenerator>();
            yield return StartCoroutine(tempGenerator.GenerateDeal(Difficulty.Medium, 0, (d, metrics) => { deal = d; }));
            Destroy(tempGenerator);

            if (deal != null)
            {
                deal.tableau.RemoveAll(list => list == null || list.Count == 0);
                deal.foundations.RemoveAll(list => list == null || list.Count == 0);
            }
        }

        if (intro != null) yield return StartCoroutine(intro.AnimateUIAndSlots());

        Vector3 spawnPos = offScreenSpawnPoint != null ? offScreenSpawnPoint.position : new Vector3(0, -2000, 0);
        Vector3 stockPos = pileManager.StockPile.transform.position;

        var stockArray = deal.stock.ToArray();
        List<CardController> stockCards = new List<CardController>();

        for (int i = stockArray.Length - 1; i >= 0; i--)
        {
            var card = SpawnCard(stockArray[i].Card, modeManager.DragLayer, false);
            card.transform.position = spawnPos;
            stockCards.Add(card);
        }

        float stockDuration = 0.45f;
        float elapsed = 0f;
        while (elapsed < stockDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stockDuration;
            float curvedT = t * t * (3f - 2f * t);

            foreach (var c in stockCards)
                c.transform.position = Vector3.Lerp(spawnPos, stockPos, curvedT);

            yield return null;
        }

        pileManager.StockPile.ClearWithoutUpdate();
        foreach (var c in stockCards) pileManager.StockPile.AddCard(c, false);
        pileManager.StockPile.UpdateOffsets();

        yield return new WaitForSeconds(0.1f);

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

            StartCoroutine(FlyCardRoutine(card, targetPile, 0.25f, true));

            if (!isCenter) fIndex++;
            yield return new WaitForSeconds(0.06f);
        }

        for (int i = 0; i < 6; i++)
        {
            var card = SpawnCard(deal.tableau[i][0].Card, modeManager.DragLayer, false);
            card.transform.position = topStockPos;

            StartCoroutine(FlyCardRoutine(card, pileManager.Reserves[i], 0.25f, true));
            yield return new WaitForSeconds(0.06f);
        }

        yield return new WaitForSeconds(0.3f);
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
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
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

    // --- ⚡ ИСПРАВЛЕННАЯ СИСТЕМА АНИМАЦИИ ПЕРЕСДАЧИ ⚡ ---
    private IEnumerator AnimateRecycleRoutine(List<CardController> cardsToRecycle)
    {
        modeManager.IsInputAllowed = false;

        float flightDuration = 0.15f; // Время полета одной карты
        float staggerDelay = 0.015f;  // Микро-задержка между вылетами (создает эффект "потока")

        // Запускаем полет каждой карты в своей корутине (параллельно!)
        for (int i = 0; i < cardsToRecycle.Count; i++)
        {
            var card = cardsToRecycle[i];
            if (card == null) continue;

            StartCoroutine(RecycleSingleCardRoutine(card, flightDuration, i * staggerDelay));
        }

        // Ждем ровно столько времени, сколько нужно последней карте, чтобы приземлиться
        float totalTime = flightDuration + (cardsToRecycle.Count * staggerDelay);
        yield return new WaitForSeconds(totalTime);

        // На всякий случай обновляем всю лесенку в конце
        if (pileManager.StockPile != null) pileManager.StockPile.UpdateOffsets();

        modeManager.IsInputAllowed = true;
        modeManager.CheckGameState();
    }

    private IEnumerator RecycleSingleCardRoutine(CardController card, float duration, float delay)
    {
        // Ждем своей очереди на вылет
        if (delay > 0) yield return new WaitForSeconds(delay);

        card.transform.SetParent(modeManager.DragLayer, true);
        card.transform.SetAsLastSibling();

        var cardData = card.GetComponent<CardData>();
        if (cardData != null)
        {
            // МГНОВЕННЫЙ переворот (без анимации), чтобы не изменять параметр flipDuration!
            cardData.SetFaceUp(false, false);
        }

        Vector3 startPos = card.transform.position;

        // Летим СТРОГО в базовую точку StockPile. Никаких умножений на scaleFactor!
        Vector3 targetPos = pileManager.StockPile.transform.position;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // SmoothStep

            card.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        card.transform.position = targetPos;

        // Отдаем карту слоту. Он сам установит ее на нужную ступеньку лесенки.
        pileManager.StockPile.AddCard(card, false);
        pileManager.StockPile.UpdateOffsets();
    }
}