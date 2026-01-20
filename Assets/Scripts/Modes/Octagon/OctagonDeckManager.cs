using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OctagonDeckManager : MonoBehaviour
{
    [Header("References")]
    public OctagonModeManager modeManager;
    public OctagonPileManager pileManager;
    public CardFactory cardFactory;
    public OctagonAnimationService animService;

    [Header("Animation Settings")]
    [SerializeField] private float deckEntryDuration = 0.7f;
    [SerializeField] private float totalDealTime = 2.0f;

    private void Start()
    {
        if (animService == null) animService = FindObjectOfType<OctagonAnimationService>();
    }

    public void ApplyDeal(Deal deal)
    {
        StartCoroutine(DealRoutine(deal));
    }

    private IEnumerator DealRoutine(Deal deal)
    {
        modeManager.IsInputAllowed = false;
        ClearBoard();

        // --- 1. Логическая подготовка очереди ---
        Queue<DealTask> dealQueue = new Queue<DealTask>();

        // ИСПРАВЛЕНИЕ: Жесткий порядок мастей для Foundation
        // 0,1: Spades | 2,3: Hearts | 4,5: Clubs | 6,7: Diamonds
        CardModel[] aces = new CardModel[8] {
            new CardModel(Suit.Spades, 1), new CardModel(Suit.Spades, 1),     // Slot 0, 1
            new CardModel(Suit.Hearts, 1), new CardModel(Suit.Hearts, 1),     // Slot 2, 3
            new CardModel(Suit.Clubs, 1), new CardModel(Suit.Clubs, 1),       // Slot 4, 5
            new CardModel(Suit.Diamonds, 1), new CardModel(Suit.Diamonds, 1)  // Slot 6, 7
        };

        int aceIndex = 0;
        int groupsCount = Mathf.Min(4, deal.tableau.Count);

        for (int g = 0; g < groupsCount; g++)
        {
            // A. Tableau (Группа)
            var groupData = deal.tableau[g];
            var targetGroup = pileManager.TableauGroups[g];
            int maxSlots = targetGroup.Slots.Count;

            for (int i = 0; i < groupData.Count && i < maxSlots; i++)
            {
                int slotIndex = (maxSlots - 1) - i;
                var cardData = groupData[i];
                var targetSlot = targetGroup.Slots[slotIndex];
                bool flip = (slotIndex == 0);
                dealQueue.Enqueue(new DealTask { Model = cardData.Card, Target = targetSlot, FlipOnArrival = flip });
            }

            // B. Foundation (Два туза следующей по списку масти)
            if (aceIndex < 8)
            {
                dealQueue.Enqueue(new DealTask
                {
                    Model = aces[aceIndex],
                    Target = pileManager.FoundationPiles[aceIndex],
                    FlipOnArrival = true
                });
                aceIndex++;
            }
            if (aceIndex < 8)
            {
                dealQueue.Enqueue(new DealTask
                {
                    Model = aces[aceIndex],
                    Target = pileManager.FoundationPiles[aceIndex],
                    FlipOnArrival = true
                });
                aceIndex++;
            }
        }

        // C. Stock
        List<CardModel> stockModels = new List<CardModel>();
        foreach (var c in deal.stock) stockModels.Add(c.Card);


        // --- 2. Создание карт ---
        List<CardController> allCreatedCards = new List<CardController>();

        // 2.1 Карты стока
        foreach (var m in stockModels)
        {
            var c = CreateCardAtStock(m);
            pileManager.StockPile.AddCard(c);
            allCreatedCards.Add(c);
        }

        // 2.2 Карты раздачи
        var tasksArray = dealQueue.ToArray();
        for (int i = tasksArray.Length - 1; i >= 0; i--)
        {
            var task = tasksArray[i];
            task.CardInstance = CreateCardAtStock(task.Model);
            pileManager.StockPile.AddCard(task.CardInstance);
            allCreatedCards.Add(task.CardInstance);
        }


        // --- 3. Анимация влета колоды ---
        float canvasWidth = modeManager.RootCanvas.GetComponent<RectTransform>().rect.width;
        Vector2 offScreenPos = new Vector2(-canvasWidth * 1.2f, 0);

        foreach (var card in allCreatedCards)
        {
            card.rectTransform.anchoredPosition = offScreenPos;
            card.transform.localScale = Vector3.one;
        }

        if (animService != null)
        {
            foreach (var card in allCreatedCards)
            {
                StartCoroutine(animService.AnimateMoveCard(
                    card,
                    pileManager.StockPile.transform,
                    Vector3.zero,
                    deckEntryDuration,
                    false,
                    () =>
                    {
                        var cg = card.GetComponent<CanvasGroup>();
                        if (cg) cg.blocksRaycasts = false;
                    }
                ));
            }
        }
        else
        {
            foreach (var card in allCreatedCards) card.rectTransform.anchoredPosition = Vector2.zero;
        }

        yield return new WaitForSeconds(deckEntryDuration + 0.1f);


        // --- 4. Анимация раздачи по столам ---
        float interval = totalDealTime / Mathf.Max(1, dealQueue.Count);
        interval = Mathf.Clamp(interval, 0.03f, 0.1f);
        float flightDuration = 0.35f;

        while (dealQueue.Count > 0)
        {
            var task = dealQueue.Dequeue();

            if (animService != null)
            {
                StartCoroutine(animService.AnimateMoveCard(
                    task.CardInstance,
                    task.Target.Transform,
                    Vector3.zero,
                    flightDuration,
                    task.FlipOnArrival,
                    () =>
                    {
                        task.Target.AcceptCard(task.CardInstance);
                        var cg = task.CardInstance.GetComponent<CanvasGroup>();
                        if (cg) cg.blocksRaycasts = true;
                    }
                ));
            }
            else
            {
                task.Target.AcceptCard(task.CardInstance);
            }

            yield return new WaitForSeconds(interval);
        }

        yield return new WaitForSeconds(flightDuration);
        modeManager.IsInputAllowed = true;
    }

    private class DealTask
    {
        public CardModel Model;
        public ICardContainer Target;
        public bool FlipOnArrival;
        public CardController CardInstance;
    }

    private CardController CreateCardAtStock(CardModel model)
    {
        if (cardFactory == null) return null;

        var cardObj = cardFactory.CreateCard(model, pileManager.StockPile.transform, Vector2.zero);
        if (cardObj == null) return null;

        var oldCtrl = cardObj.GetComponent<CardController>();
        if (oldCtrl != null && !(oldCtrl is OctagonCardController)) DestroyImmediate(oldCtrl);

        var newCtrl = cardObj.GetComponent<OctagonCardController>();
        if (newCtrl == null) newCtrl = cardObj.gameObject.AddComponent<OctagonCardController>();

        newCtrl.cardModel = model;
        newCtrl.canvas = modeManager.RootCanvas;
        newCtrl.CardmodeManager = modeManager;

        var cg = newCtrl.GetComponent<CanvasGroup>();
        if (cg) cg.blocksRaycasts = false;

        var data = cardObj.GetComponent<CardData>();
        if (data != null) data.SetFaceUp(false, true);

        newCtrl.transform.localScale = Vector3.one;

        return newCtrl;
    }

    public void ClearBoard()
    {
        foreach (Transform t in pileManager.StockPile.transform) Destroy(t.gameObject);
        foreach (Transform t in pileManager.WastePile.transform) Destroy(t.gameObject);
        foreach (var p in pileManager.FoundationPiles) foreach (Transform t in p.transform) Destroy(t.gameObject);
        foreach (var group in pileManager.TableauGroups)
            foreach (var slot in group.Slots) foreach (Transform t in slot.transform) Destroy(t.gameObject);
    }
}