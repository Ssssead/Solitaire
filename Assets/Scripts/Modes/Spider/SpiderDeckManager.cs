using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpiderDeckManager : MonoBehaviour
{
    [Header("Settings")]
    public CardFactory cardFactory;
    public SpiderPileManager pileManager;
    public SpiderModeManager modeManager;
    public UndoManager undoManager;
    public Deal CurrentDeal { get; private set; }

    [Header("Error Feedback")]
    [Tooltip("Объект стрелочки (UI Image), который будет появляться при ошибке раздачи")]
    public GameObject emptyPileArrow;
    private bool isErrorAnimating = false;
    public bool isSkippingIntro = false;
    public bool isDealing = false;
    private List<CardController> deckCards = new List<CardController>();

    private void Update()
    {
        if (isDealing && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
        {
            isSkippingIntro = true;
        }
    }
    public void CreateAndDeal(int suitsCount, Difficulty difficulty)
    {
       
        cardFactory.DestroyAllCards();
        deckCards.Clear();

        if (DealCacheSystem.Instance != null)
        {
            Deal cachedDeal = DealCacheSystem.Instance.GetDeal(GameType.Spider, difficulty, suitsCount);

            if (cachedDeal != null)
            {
                CurrentDeal = cachedDeal;
                var models = ReconstructDeckFromDeal(cachedDeal);
                Debug.Log($"[Spider] Loaded Deal from Cache ({suitsCount} suits, {difficulty})");
                SpawnCardsAndDeal(models);
                return;
            }
        }
    }

    private void SpawnCardsAndDeal(List<CardModel> models)
    {
        foreach (var model in models)
        {
            CardController card = cardFactory.CreateCard(model, modeManager.dragLayer, Vector2.zero);
            FindObjectOfType<DragManager>()?.RegisterCardEvents(card);

            card.GetComponent<CardData>().SetFaceUp(false, false);
            if (card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = false;
                card.canvasGroup.interactable = false;
            }
            deckCards.Add(card);
        }

        if (modeManager.introController != null)
        {
            bool skipUI = modeManager.isRestarting;
            modeManager.isRestarting = false;

            Vector3 targetPos = pileManager.StockPile.transform.position;
            float canvasScale = modeManager.rootCanvas.transform.localScale.x;
            Vector3 offScreenPos = targetPos + new Vector3(1500f * canvasScale, 0, 0);

            foreach (var card in deckCards)
            {
                card.transform.position = offScreenPos;
            }

            modeManager.introController.SetupIntro(skipUI);
            StartCoroutine(modeManager.introController.PlayIntro(skipUI));
        }
        else
        {
            StartCoroutine(PlayIntroDeckArrival(0f));
        }
    }

    public IEnumerator PlayIntroDeckArrival(float duration)
    {
        isSkippingIntro = false;
        isDealing = true; // Открываем возможность пропуска
        modeManager.IsInputAllowed = false;

        Vector3 targetPos = pileManager.StockPile.transform.position;
        float canvasScale = modeManager.rootCanvas.transform.localScale.x;
        Vector3 offScreenPos = targetPos + new Vector3(1500f * canvasScale, 0, 0);

        if (duration > 0f)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Whoosh_In");

            float elapsed = 0f;
            AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

            while (elapsed < duration)
            {
                float speed = isSkippingIntro ? 15f : 1f; // Ускорение
                elapsed += Time.deltaTime * speed;
                float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));

                foreach (var card in deckCards)
                {
                    card.transform.position = Vector3.Lerp(offScreenPos, targetPos, t);
                }
                yield return null;
            }
        }

        foreach (var card in deckCards)
        {
            card.transform.SetParent(pileManager.StockPile.transform, false);
            card.transform.localScale = Vector3.one;
        }

        pileManager.StockPile.ForceRecalculateLayout();

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");

        // Заменили yield return new WaitForSeconds(0.1f);
        float wait1 = 0f;
        while (wait1 < 0.1f)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            wait1 += Time.deltaTime * speed;
            yield return null;
        }

        yield return StartCoroutine(DealInitialLayout());
    }

    private List<CardModel> ReconstructDeckFromDeal(Deal deal)
    {
        List<CardModel> list = new List<CardModel>();

        int maxRow = 0;
        foreach (var col in deal.tableau) if (col.Count > maxRow) maxRow = col.Count;

        for (int row = 0; row < maxRow; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                if (row < deal.tableau[col].Count)
                    list.Add(deal.tableau[col][row].Card);
            }
        }

        var stockList = deal.stock.ToList();
        stockList.Reverse();
        foreach (var c in stockList) list.Add(c.Card);

        return list;
    }

    private IEnumerator DealInitialLayout()
    {
        modeManager.IsInputAllowed = false;
        int dealtCount = 0;
        int[] cardsPerCol = { 6, 6, 6, 6, 5, 5, 5, 5, 5, 5 };

        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                if (row >= cardsPerCol[col]) continue;
                if (dealtCount >= deckCards.Count) break;

                var card = deckCards[dealtCount++];
                var targetPile = pileManager.TableauPiles[col];
                bool faceUp = (row == cardsPerCol[col] - 1);

                MoveCardToPile(card, targetPile, faceUp, recordUndo: false);

                if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

                // Заменили yield return new WaitForSeconds(0.02f);
                float wait2 = 0f;
                while (wait2 < 0.02f)
                {
                    float speed = isSkippingIntro ? 15f : 1f;
                    wait2 += Time.deltaTime * speed;
                    yield return null;
                }
            }
        }

        // Заменили yield return new WaitForSeconds(0.4f);
        float wait3 = 0f;
        while (wait3 < 0.4f)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            wait3 += Time.deltaTime * speed;
            yield return null;
        }

        // Открываем ввод - это позволит слотам принять наши отступы
        modeManager.IsInputAllowed = true;
        isDealing = false; // Раздача полностью окончена

        if (modeManager != null)
        {
            modeManager.UpdateTableauLayouts();
        }
    }

    public void TryDealRow()
    {
        if (!modeManager.IsInputAllowed) return;

        var stockPile = pileManager.StockPile;
        if (stockPile.cards.Count < 10) return;

        SpiderTableauPile firstEmptyPile = null;
        foreach (var pile in pileManager.TableauPiles)
        {
            if (pile.cards.Count == 0)
            {
                firstEmptyPile = pile;
                break;
            }
        }

        if (firstEmptyPile != null)
        {
            if (!isErrorAnimating)
            {
                StartCoroutine(PlayErrorFeedback(firstEmptyPile));
            }
            return;
        }

        modeManager.OnStockClicked();

        List<CardController> cardsToDeal = new List<CardController>();
        int totalInStock = stockPile.cards.Count;

        // --- ИЗМЕНЕНИЕ 1: Собираем карты СВЕРХУ ВНИЗ (от верхней карты к десятой сверху) ---
        for (int i = totalInStock - 1; i >= totalInStock - 10; i--)
        {
            cardsToDeal.Add(stockPile.cards[i]);
        }

        bool willBeEmpty = (totalInStock <= 10);
        modeManager.UpdateTableauLayouts(willBeEmpty);

        StartCoroutine(DealRowRoutine(cardsToDeal));
    }

    private IEnumerator PlayErrorFeedback(SpiderTableauPile emptyPile)
    {
        isErrorAnimating = true;

        // <--- 1. ЗВУК ПРЕДУПРЕЖДЕНИЯ (ОШИБКИ) --->
        if (AudioManager.Instance != null)
        {
            // Здесь отлично подойдет тот же звук, что и при попытке потянуть заблокированный ряд, 
            // либо специальный "UI_Warning", привлекающий внимание к стрелочке.
            AudioManager.Instance.PlaySound("Card_Error");
        }

        StartCoroutine(ShakeStockPile());

        if (emptyPileArrow != null)
        {
            emptyPileArrow.SetActive(true);

            Transform targetParent = emptyPile.emptySlotArrowAnchor != null ? emptyPile.emptySlotArrowAnchor : emptyPile.transform;
            emptyPileArrow.transform.SetParent(targetParent, false);

            emptyPileArrow.transform.localPosition = Vector3.zero;

            float duration = 1.0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float yOffset = Mathf.Sin(elapsed * Mathf.PI * 4f) * 20f;
                emptyPileArrow.transform.localPosition = new Vector3(0, yOffset, 0);
                yield return null;
            }

            emptyPileArrow.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }

        isErrorAnimating = false;
    }

    private IEnumerator ShakeStockPile()
    {
        if (pileManager.StockPile == null) yield break;

        Transform stockTransform = pileManager.StockPile.transform;
        Vector3 originalPos = stockTransform.localPosition;
        float duration = 0.4f;
        float elapsed = 0f;

        // <--- 2. ПОДГОТОВКА ЗВУКА ТРЯСКИ КОЛОДЫ --->
        AudioSource shakeSource = null;
        float baseVolume = 1f;
        if (AudioManager.Instance != null)
        {
            shakeSource = AudioManager.Instance.PlaySound("Card_Shake");
            if (shakeSource != null) baseVolume = shakeSource.volume;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;

            // Визуальная тряска (добавлено плавное затухание амплитуды)
            float speedMultiplier = 40f;
            float xOffset = Mathf.Sin(elapsed * speedMultiplier) * 10f * (1f - percent);
            stockTransform.localPosition = originalPos + new Vector3(xOffset, 0, 0);

            // <--- ДИНАМИЧЕСКАЯ ГРОМКОСТЬ ОТ СКОРОСТИ --->
            if (shakeSource != null)
            {
                float movementSpeed = Mathf.Abs(Mathf.Cos(elapsed * speedMultiplier));
                shakeSource.volume = baseVolume * (1f - percent) * movementSpeed;
            }

            yield return null;
        }

        stockTransform.localPosition = originalPos;

        // <--- СБРОС ЗВУКА --->
        if (shakeSource != null)
        {
            shakeSource.Stop();
            shakeSource.volume = baseVolume; // Возвращаем исходные настройки
        }
    }

    private IEnumerator DealRowRoutine(List<CardController> cardsToDeal)
    {
        modeManager.IsInputAllowed = false;
        string batchID = System.Guid.NewGuid().ToString();

        if (undoManager != null)
        {
            // --- ИЗМЕНЕНИЕ 2: Записываем ходы от 0-го слота к 9-му. 
            // При отмене они будут возвращаться с 9-го по 0-й, что идеально восстановит иерархию (SiblingIndex) ---
            for (int i = 0; i < 10; i++)
            {
                var card = cardsToDeal[i];
                var pile = pileManager.TableauPiles[i];

                undoManager.RecordMove(
                    new List<CardController> { card },
                    pileManager.StockPile,
                    pile,
                    new List<Transform> { card.transform.parent },
                    new List<Vector3> { card.rectTransform.anchoredPosition },
                    new List<int> { card.transform.GetSiblingIndex() },
                    batchID,
                    isRapidUndo: true
                );
            }
        }

        for (int i = 0; i < 10; i++)
        {
            var card = cardsToDeal[i];
            var pile = pileManager.TableauPiles[i];

            MoveCardToPile(card, pile, true, recordUndo: false, groupID: batchID);

            // <--- ЗВУК: ШЕЛЕСТ ДОПОЛНИТЕЛЬНОЙ РАЗДАЧИ --->
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(0.4f);

        modeManager.IsInputAllowed = true;

        if (modeManager != null)
        {
            modeManager.CheckGameState();
            modeManager.UpdateTableauLayouts();
        }
    }

    private void MoveCardToPile(CardController card, SpiderTableauPile targetPile, bool faceUp, bool recordUndo, string groupID = null)
    {
        if (recordUndo && undoManager != null)
        {
            undoManager.RecordMove(
                new List<CardController> { card },
                pileManager.StockPile,
                targetPile,
                new List<Transform> { card.transform.parent },
                new List<Vector3> { card.rectTransform.anchoredPosition },
                new List<int> { card.transform.GetSiblingIndex() },
                groupID,
                isRapidUndo: true
            );
        }

        card.transform.SetParent(targetPile.transform);
        if (card.canvasGroup != null)
        {
            card.canvasGroup.blocksRaycasts = true;
            card.canvasGroup.interactable = true;
        }
        targetPile.AddCard(card, faceUp);
        targetPile.ForceRecalculateLayout();
    }

    public void RestartGame()
    {
        int suits = GameSettings.SpiderSuitCount;
        Difficulty diff = GameSettings.CurrentDifficulty;
        if (suits != 1 && suits != 2 && suits != 4) suits = 1;

        CreateAndDeal(suits, diff);
    }
}