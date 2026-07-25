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

    [HideInInspector] public bool isSkippingIntro = false;
    [HideInInspector] public bool isDealing = false;

    private void Start()
    {
        if (animService == null) animService = FindObjectOfType<OctagonAnimationService>();
    }

    private void Update()
    {
        if (isDealing && !modeManager.IsInputAllowed && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
        {
            isSkippingIntro = true;
        }
    }

    private IEnumerator SkippableWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime * (isSkippingIntro ? 15f : 1f);
            yield return null;
        }
    }

    public void ApplyDeal(Deal deal)
    {
        if (deal == null) return;

        // Перехватываем расклад для обучения
        if (GameSettings.IsTutorialMode)
        {
            StartCoroutine(TutorialAnimateDealRoutine(deal));
            return;
        }

        // Обычная раздача
        StartCoroutine(DealRoutine(deal));
    }
    

    // Метод, который вызывает ModeManager для старта обучения
    public void StartTutorialDeal(Deal deal)
    {
        ClearBoard();
        StartCoroutine(TutorialAnimateDealRoutine(deal));
    }

    private IEnumerator TutorialAnimateDealRoutine(Deal deal)
    {
        isDealing = true;
        modeManager.IsInputAllowed = false;

        float flyDuration = 0.5f;
        Vector3 spawnOffset = new Vector3(0f, 1500f, 0f);

        // 1. Прилет карт в Дома
        if (deal.foundations != null && pileManager.FoundationPiles != null)
        {
            int maxFoundations = Mathf.Min(deal.foundations.Count, pileManager.FoundationPiles.Count);
            for (int f = 0; f < maxFoundations; f++)
            {
                if (deal.foundations[f] == null || pileManager.FoundationPiles[f] == null) continue;

                foreach (var model in deal.foundations[f])
                {
                    var targetPile = pileManager.FoundationPiles[f];
                    var cardObj = cardFactory.CreateCard(model, targetPile.transform, Vector2.zero);
                    var ctrl = cardObj.GetComponent<OctagonCardController>() ?? cardObj.gameObject.AddComponent<OctagonCardController>();
                    ctrl.cardModel = model;
                    ctrl.canvas = modeManager.RootCanvas;
                    ctrl.CardmodeManager = modeManager;

                    cardObj.GetComponent<CardData>().SetFaceUp(true, false);

                    cardObj.transform.localPosition = spawnOffset;
                    StartCoroutine(animService.AnimateMoveCard(ctrl, targetPile.transform, Vector3.zero, flyDuration, true, () =>
                    {
                        targetPile.AcceptCard(ctrl);
                    }));
                }
            }
        }

        // 2. Прилет карт на Стол (По строго заданным вашим нижним слотам)
        if (deal.tableau != null && pileManager.TableauGroups != null)
        {
            int maxGroups = Mathf.Min(deal.tableau.Count, pileManager.TableauGroups.Count);
            for (int g = 0; g < maxGroups; g++)
            {
                if (deal.tableau[g] == null || pileManager.TableauGroups[g] == null) continue;

                var sampleList = deal.tableau[g];
                var group = pileManager.TableauGroups[g];
                if (group.Slots == null) continue;

                for (int i = 0; i < sampleList.Count; i++)
                {
                    if (sampleList[i] == null) continue;

                    var cardInstance = sampleList[i];

                    // Направляем карты СТРОГО в нужные слоты
                    OctagonTableauSlot slot = null;
                    if (g == 0 && group.Slots.Count > 4) slot = group.Slots[4];      // J♣ в Слот 4
                    else if (g == 1 && group.Slots.Count > 4) slot = group.Slots[4]; // K♣ в Слот 4
                    else if (g == 2 && group.Slots.Count > 4) slot = group.Slots[4]; // K♠ в Слот 4
                    else if (g == 3 && group.Slots.Count > 4)
                    {
                        if (i == 0) slot = group.Slots[3]; // J♦ в Слот 3
                        else if (i == 1) slot = group.Slots[4]; // K♥ в Слот 4
                    }

                    if (slot == null) continue;

                    var cardObj = cardFactory.CreateCard(cardInstance.Card, slot.transform, Vector2.zero);
                    var ctrl = cardObj.GetComponent<OctagonCardController>() ?? cardObj.gameObject.AddComponent<OctagonCardController>();
                    ctrl.cardModel = cardInstance.Card;
                    ctrl.canvas = modeManager.RootCanvas;
                    ctrl.CardmodeManager = modeManager;

                    // Рубашкой вверх кладем ТОЛЬКО Короля Червей (Группа 3, 2-я карта)
                    bool initialFaceUp = cardInstance.FaceUp;
                    if (g == 3 && i == 1) initialFaceUp = false;

                    cardObj.GetComponent<CardData>().SetFaceUp(initialFaceUp, false);

                    cardObj.transform.localPosition = spawnOffset;
                    Vector3 targetLocalPos = Vector3.zero;

                    StartCoroutine(animService.AnimateMoveCard(ctrl, slot.transform, targetLocalPos, flyDuration, initialFaceUp, () =>
                    {
                        slot.AcceptCard(ctrl);
                        slot.UpdateLayout();
                    }));
                }
            }
        }

        // 3. Заполнение колоды
        if (deal.stock != null)
        {
            foreach (var cardInstance in deal.stock)
            {
                if (cardInstance != null) CreateCardAtStock(cardInstance.Card);
            }
        }

        yield return new WaitForSeconds(flyDuration + 0.05f);

        if (pileManager.TableauGroups != null)
        {
            foreach (var g in pileManager.TableauGroups)
            {
                if (g != null) g.UpdateTopCardState();
            }
        }

        isDealing = false;
        modeManager.IsInputAllowed = true;

        // ЗАПУСК ОБУЧЕНИЯ (Как только все карты прилетели на свои места)
        if (modeManager.tutorialManager != null)
        {
            modeManager.tutorialManager.StartTutorial();
        }
    }

    private IEnumerator DealRoutine(Deal deal)
    {
        isSkippingIntro = false; // <--- Сброс перед раздачей
        isDealing = true;
        modeManager.IsInputAllowed = false;
        ClearBoard();

        Queue<DealTask> dealQueue = new Queue<DealTask>();

        CardModel[] aces = new CardModel[8] {
            new CardModel(Suit.Spades, 1), new CardModel(Suit.Spades, 1),
            new CardModel(Suit.Hearts, 1), new CardModel(Suit.Hearts, 1),
            new CardModel(Suit.Clubs, 1), new CardModel(Suit.Clubs, 1),
            new CardModel(Suit.Diamonds, 1), new CardModel(Suit.Diamonds, 1)
        };

        int aceIndex = 0;
        int groupsCount = Mathf.Min(4, deal.tableau.Count);

        for (int g = 0; g < groupsCount; g++)
        {
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

            if (aceIndex < 8)
            {
                dealQueue.Enqueue(new DealTask { Model = aces[aceIndex], Target = pileManager.FoundationPiles[aceIndex], FlipOnArrival = true });
                aceIndex++;
            }
            if (aceIndex < 8)
            {
                dealQueue.Enqueue(new DealTask { Model = aces[aceIndex], Target = pileManager.FoundationPiles[aceIndex], FlipOnArrival = true });
                aceIndex++;
            }
        }

        List<CardModel> stockModels = new List<CardModel>();
        foreach (var c in deal.stock) stockModels.Add(c.Card);

        List<CardController> allCreatedCards = new List<CardController>();

        foreach (var m in stockModels)
        {
            var c = CreateCardAtStock(m);
            pileManager.StockPile.AddCard(c);
            allCreatedCards.Add(c);
        }

        var tasksArray = dealQueue.ToArray();
        for (int i = tasksArray.Length - 1; i >= 0; i--)
        {
            var task = tasksArray[i];
            task.CardInstance = CreateCardAtStock(task.Model);
            pileManager.StockPile.AddCard(task.CardInstance);
            allCreatedCards.Add(task.CardInstance);
        }

        float canvasWidth = modeManager.RootCanvas.GetComponent<RectTransform>().rect.width;
        Vector2 offScreenPos = new Vector2(-canvasWidth * 1.2f, 0);

        foreach (var card in allCreatedCards)
        {
            card.rectTransform.anchoredPosition = offScreenPos;
            card.transform.localScale = Vector3.one;
        }

        // 1. Влетание всей колоды целиком
        if (animService != null)
        {
            if (AudioManager.Instance != null && !isSkippingIntro)
            {
                AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            }

            for (int i = 0; i < allCreatedCards.Count; i++)
            {
                var card = allCreatedCards[i];

                Vector3 targetLocalPos = new Vector3(
                    i * pileManager.StockPile.offsetX,
                    i * pileManager.StockPile.offsetY,
                    0f
                );

                StartCoroutine(animService.AnimateMoveCard(
                    card,
                    pileManager.StockPile.transform,
                    targetLocalPos,
                    deckEntryDuration,
                    false,
                    () =>
                    {
                        card.transform.SetParent(pileManager.StockPile.transform, true);
                        card.transform.SetAsLastSibling();

                        var cg = card.GetComponent<CanvasGroup>();
                        if (cg) cg.blocksRaycasts = true;
                    }
                ));
            }
        }
        else
        {
            foreach (var card in allCreatedCards) card.rectTransform.anchoredPosition = Vector2.zero;
        }

        yield return StartCoroutine(SkippableWait(deckEntryDuration + 0.1f));

        float interval = totalDealTime / Mathf.Max(1, dealQueue.Count);
        interval = Mathf.Clamp(interval, 0.03f, 0.1f);
        float flightDuration = 0.35f;

        // 2. Раздача карт по столам 
        while (dealQueue.Count > 0)
        {
            var task = dealQueue.Dequeue();

            if (animService != null)
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySound("Card_Deal");
                }

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

            yield return StartCoroutine(SkippableWait(interval));
        }

        yield return StartCoroutine(SkippableWait(flightDuration));

        modeManager.IsInputAllowed = true;
        isSkippingIntro = false;
        isDealing = false;
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
        if (cg) cg.blocksRaycasts = true;

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