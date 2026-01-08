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
    public SpiderLevelGenerator levelGenerator;

    // Список для первичной анимации раздачи
    private List<CardController> deckCards = new List<CardController>();

    public void CreateAndDeal(int suitsCount, Difficulty difficulty)
    {
        // Инициализация генератора если нужно
        if (levelGenerator == null)
            levelGenerator = gameObject.AddComponent<SpiderLevelGenerator>();

        cardFactory.DestroyAllCards();
        deckCards.Clear();

        // 1. Пробуем загрузить из КЭША
        if (DealCacheSystem.Instance != null)
        {
            // Используем переданную сложность для поиска в кэше
            Deal cachedDeal = DealCacheSystem.Instance.GetDeal(GameType.Spider, difficulty, suitsCount);

            if (cachedDeal != null)
            {
                var models = ReconstructDeckFromDeal(cachedDeal);
                Debug.Log($"[Spider] Loaded Deal from Cache ({suitsCount} suits, {difficulty})");
                SpawnCardsAndDeal(models);
                return;
            }
        }

        // 2. Если кэша нет - ГЕНЕРИРУЕМ ЧЕРЕЗ НОВЫЙ СКРИПТ
        StartCoroutine(levelGenerator.GenerateDeal(difficulty, suitsCount, (deal, metrics) =>
        {
            // Преобразуем объект Deal обратно в плоский список моделей для спавна
            var models = ReconstructDeckFromDeal(deal);
            SpawnCardsAndDeal(models);
        }));
    }

    private void SpawnCardsAndDeal(List<CardModel> models)
    {
        foreach (var model in models)
        {
            CardController card = cardFactory.CreateCard(model, pileManager.StockPile.transform, Vector2.zero);
            FindObjectOfType<DragManager>()?.RegisterCardEvents(card);

            // Все карты изначально закрыты
            card.GetComponent<CardData>().SetFaceUp(false, false);
            if (card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = false;
                card.canvasGroup.interactable = false;
            }
            deckCards.Add(card);
        }

        pileManager.StockPile.ForceRecalculateLayout();
        StartCoroutine(DealInitialLayout());
    }

    // Распаковка Deal в плоский список для анимации
    private List<CardModel> ReconstructDeckFromDeal(Deal deal)
    {
        List<CardModel> list = new List<CardModel>();

        // Tableau: Собираем построчно (ряд 0, ряд 1...), чтобы анимация шла слева-направо
        // Проверяем реальную высоту колонок из кэша
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

        // Stock: Карты запаса кладем в конец списка
        var stockList = deal.stock.ToList();
        stockList.Reverse();
        foreach (var c in stockList) list.Add(c.Card);

        return list;
    }

    private IEnumerator DealInitialLayout()
    {
        modeManager.IsInputAllowed = false;

        // Определяем структуру стола по фактическому количеству карт
        // (Обычно это 6-6-6-6-5-5-5-5-5-5, но кэш может дать другое)
        int dealtCount = 0;
        // 54 карты на столе
        int totalTableauCards = 54;

        // Используем стандартную структуру Паука для анимации, если в deckCards достаточно карт
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
                yield return new WaitForSeconds(0.02f);
            }
        }
        modeManager.IsInputAllowed = true;
    }

    // --- ИСПРАВЛЕННАЯ РАЗДАЧА ИЗ КОЛОДЫ ---
    public void TryDealRow()
    {
        if (!modeManager.IsInputAllowed) return;

        var stockPile = pileManager.StockPile;
        if (stockPile.cards.Count < 10) return;

        foreach (var pile in pileManager.TableauPiles)
        {
            if (pile.cards.Count == 0) return;
        }

        // --- ДОБАВИТЬ ЭТУ СТРОКУ ---
        modeManager.OnStockClicked();
        // ---------------------------

        List<CardController> cardsToDeal = new List<CardController>();
        int totalInStock = stockPile.cards.Count;
        for (int i = totalInStock - 10; i < totalInStock; i++)
        {
            cardsToDeal.Add(stockPile.cards[i]);
        }

        StartCoroutine(DealRowRoutine(cardsToDeal));
    }

    private IEnumerator DealRowRoutine(List<CardController> cardsToDeal)
    {
        modeManager.IsInputAllowed = false;
        string batchID = System.Guid.NewGuid().ToString();

        for (int i = 0; i < 10; i++)
        {
            var card = cardsToDeal[i];
            var pile = pileManager.TableauPiles[i];

            MoveCardToPile(card, pile, true, recordUndo: true, groupID: batchID);
            yield return new WaitForSeconds(0.05f);
        }

        modeManager.IsInputAllowed = true;
        modeManager.UpdateTableauLayouts();
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

    // --- Вспомогательные методы ---
    private List<CardModel> GenerateModels(int level)
    {
        List<CardModel> list = new List<CardModel>();
        List<Suit> suitsToUse = new List<Suit>();
        if (level == 1) suitsToUse.Add(Suit.Spades);
        else if (level == 2) { suitsToUse.Add(Suit.Spades); suitsToUse.Add(Suit.Hearts); }
        else { suitsToUse.Add(Suit.Spades); suitsToUse.Add(Suit.Hearts); suitsToUse.Add(Suit.Clubs); suitsToUse.Add(Suit.Diamonds); }

        int setsPerSuit = 8 / suitsToUse.Count;
        foreach (var suit in suitsToUse)
            for (int k = 0; k < setsPerSuit; k++)
                for (int r = 1; r <= 13; r++) list.Add(new CardModel(suit, r));
        return list;
    }

    public void RestartGame()
    {
        int suits = GameSettings.SpiderSuitCount;
        Difficulty diff = GameSettings.CurrentDifficulty; // Берем из настроек

        // Валидация
        if (suits != 1 && suits != 2 && suits != 4) suits = 1;

        CreateAndDeal(suits, diff);
    }

    private void Shuffle<T>(List<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k]; list[k] = list[n]; list[n] = value;
        }
    }
}