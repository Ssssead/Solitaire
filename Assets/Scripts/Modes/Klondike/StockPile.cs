// StockPile.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static KlondikeModeManager;

/// <summary>
/// Стопка Stock (колода закрытых карт).
/// При клике берёт карту и перемещает в Waste.
/// Поддерживает рециркуляцию Waste обратно в Stock.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class StockPile : MonoBehaviour, ICardContainer, IPointerClickHandler
{
    private List<CardController> cards = new List<CardController>();
    private KlondikeModeManager manager;
    private RectTransform rect;
    private AnimationService animationService;

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    [Header("Settings")]
    public StockDealMode dealMode = StockDealMode.Draw1; // Настройка в инспекторе

    // Добавляем ссылку на режим
    private StockDealMode currentDealMode;

    public Transform Transform => transform;

    /// <summary>
    /// Инициализация StockPile.
    /// </summary>
    public void Initialize(KlondikeModeManager m, RectTransform tf)
    {
        manager = m;
        rect = tf ?? GetComponent<RectTransform>();

        if (rect == null)
        {
            rect = transform as RectTransform;
            if (rect == null)
            {
                Debug.LogError($"[StockPile] Cannot get RectTransform for {gameObject.name}");
            }
        }

        // Получаем настройку из KlondikeModeManager
        if (manager != null)
        {
            currentDealMode = manager.stockDealMode; // Используем настройку из режима
            animationService = manager.animationService ?? manager.GetComponent<AnimationService>();
        }
        else
        {
            currentDealMode = dealMode; // Используем локальную настройку
        }

        if (animationService == null)
        {
            animationService = FindObjectOfType<AnimationService>();
        }

        // Убеждаемся что есть Graphic для перехвата кликов
        EnsureGraphicForRaycast();

        LogDebug($"Initialized. Deal mode: {currentDealMode}, rect={rect?.name ?? "null"}, animationService={animationService != null}");
    }

    /// <summary>
    /// Обеспечивает наличие UI Graphic для перехвата кликов на пустой слот.
    /// </summary>
    private void EnsureGraphicForRaycast()
    {
        var graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            // Добавляем прозрачный Image
            var image = gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);  // Прозрачный
            image.raycastTarget = true;
            LogDebug("Added transparent Image for raycast");
        }
        else
        {
            // Убеждаемся что raycastTarget включён
            graphic.raycastTarget = true;
        }
    }

    public void Clear()
    {
        // Уничтожаем визуальные объекты карт
        foreach (var card in cards)
        {
            if (card != null && card.gameObject != null)
            {
                Destroy(card.gameObject);
            }
        }

        cards.Clear();
        LogDebug("Cleared all cards");
    }

    /// <summary>
    /// Добавляет карту в Stock.
    /// </summary>
    public void AddCard(CardController card, bool faceUp)
    {
        if (card == null) return;

        if (card.rectTransform.parent != transform)
        {
            card.rectTransform.SetParent(transform, false);
        }

        card.rectTransform.anchoredPosition = Vector2.zero;
        card.rectTransform.SetAsLastSibling();

        cards.Add(card);

        var cardData = card.GetComponent<CardData>();
        if (cardData != null)
        {
            // animate: false здесь нормально, так как карта уже перевернулась во время полета
            // Этот вызов просто гарантирует финальное состояние
            cardData.SetFaceUp(faceUp, animate: false);
        }

        animationService?.ReorderContainerZ(transform);
    }

    #region ICardContainer Implementation

    /// <summary>
    /// Stock не принимает карты через drag & drop.
    /// </summary>
    public bool CanAccept(CardController card) => false;

    public void OnCardIncoming(CardController card) { }

    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;

    /// <summary>
    /// Stock не принимает карты напрямую (только через AddCard).
    /// </summary>
    public void AcceptCard(CardController card)
    {
        // Не используется, но реализуем для полноты интерфейса
        LogDebug($"AcceptCard called for {card?.name}, adding as faceDown");
        AddCard(card, faceUp: false);
    }

    public bool IsEmpty() => cards.Count == 0;

    /// <summary>
    /// Удаляет и возвращает верхнюю карту из Stock.
    /// </summary>
    public CardController PopTop()
    {
        if (cards.Count == 0) return null;

        int lastIndex = cards.Count - 1;
        var topCard = cards[lastIndex];
        cards.RemoveAt(lastIndex);

        LogDebug($"Popped card {topCard?.name}. Remaining: {cards.Count}");
        return topCard;
    }

    /// <summary>
    /// Проверяет, принадлежит ли карта этой стопке.
    /// </summary>
    public bool ContainsCard(CardController card)
    {
        if (card == null) return false;

        // Карта должна быть и в списке логики, И физически быть дочерним объектом
        return cards.Contains(card) && card.transform.parent == transform;
    }

    /// <summary>
    /// Возвращает количество карт в Stock.
    /// </summary>
    public int GetCardCount() => cards.Count;

    #endregion

    #region Click Handling

    /// <summary>
    /// Обрабатывает клик по Stock (берёт карту в Waste или рециркулирует).
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 1. Проверки
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

        // 2. ДЕЛЕГИРОВАНИЕ: Передаем управление в DragManager
        if (manager != null)
        {
            var deckManager = manager.deckManager ?? manager.GetComponent< DeckManager> ();
            if (deckManager != null)
            {
                // Вызываем метод DrawFromStock в DragManager.
                // В DragManager этот метод уже содержит логику: "Если Stock пуст -> RecycleWasteToStock".
                // А RecycleWasteToStock в DragManager уже содержит запись Undo.
                deckManager.DrawFromStock();
                return;
            }
        }

        // 3. Fallback (если вдруг менеджера нет, что вряд ли)
        // Этот код сработает только в крайнем случае
        if (cards.Count == 0)
        {
            RecycleWasteToStock(); // Внутренний метод без Undo
        }
        else
        {
            DrawCardsFromStock((int)currentDealMode); // Внутренний метод без Undo
        }
    }
    /// <summary>
    /// Берёт указанное количество карт из Stock в Waste.
    /// </summary>
    private void DrawCardsFromStock(int count)
    {
        if (manager == null)
        {
            Debug.LogWarning("[StockPile] Cannot draw: manager is null");
            return;
        }

        var pileManager = manager.pileManager ?? manager.GetComponent<PileManager>();
        if (pileManager == null || pileManager.WastePile == null)
        {
            Debug.LogWarning("[StockPile] Cannot draw: pileManager or WastePile is null");
            return;
        }

        var wastePile = pileManager.WastePile;

        // Берём указанное количество карт
        int cardsToDraw = Mathf.Min(count, cards.Count);

        for (int i = 0; i < cardsToDraw; i++)
        {
            var card = PopTop();
            if (card == null) break;

            // Переворачиваем лицом вверх
            var cardData = card.GetComponent<CardData>();
            if (cardData != null)
            {
                cardData.SetFaceUp(true, animate: true); // с анимацией переворота
            }

            // Добавляем в Waste
            wastePile.AddCard(card, faceUp: true);
        }

        // Обновляем визуализацию
        var animService = manager?.animationService;
        animService?.ReorderContainerZ(transform);
        animService?.ReorderContainerZ(wastePile.transform);
        Canvas.ForceUpdateCanvases();

        LogDebug($"Drew {cardsToDraw} card(s) from Stock to Waste. Stock now has {cards.Count} cards.");
    }


    /// <summary>
    /// Рециркулирует все карты из Waste обратно в Stock.
    /// </summary>
    private void RecycleWasteToStock()
    {
        if (manager == null)
        {
            Debug.LogWarning("[StockPile] Cannot recycle: manager is null");
            return;
        }

        var pileManager = manager.pileManager ?? manager.GetComponent<PileManager>();
        if (pileManager == null || pileManager.WastePile == null)
        {
            Debug.LogWarning("[StockPile] Cannot recycle: pileManager or WastePile is null");
            return;
        }

        var wastePile = pileManager.WastePile;

        // Забираем все карты из Waste
        var wasteCards = wastePile.TakeAll();

        if (wasteCards == null || wasteCards.Count == 0)
        {
            LogDebug("Waste is empty, nothing to recycle");
            return;
        }

        LogDebug($"Recycling {wasteCards.Count} cards from Waste to Stock");

        // ЗАПУСКАЕМ АНИМАЦИЮ ПЕРЕЛИСТЫВАНИЯ
        StartCoroutine(AnimateCardFlipRecycle(wasteCards));
    }

    /// <summary>
    /// Анимирует перелистывание карт из Waste в Stock по одной.
    /// </summary>
    private System.Collections.IEnumerator AnimateCardFlipRecycle(List<CardController> wasteCards)
    {
        if (wasteCards == null || wasteCards.Count == 0) yield break;

        // Получаем dragLayer
        Canvas canvasToUse = manager?.rootCanvas ?? GetComponentInParent<Canvas>();
        RectTransform dragLayer = canvasToUse?.transform as RectTransform;

        if (dragLayer == null) yield break;

        // Получаем целевую позицию Stock
        Vector3 targetWorldPos = transform.position;

        // ОБРАТНЫЙ ПОРЯДОК: последняя карта из waste становится первой в stock (LIFO)
        for (int i = wasteCards.Count - 1; i >= 0; i--)
        {
            var card = wasteCards[i];
            if (card == null) continue;

            // Устанавливаем высокий Z-индекс
            Vector3 currentLocalPos = card.rectTransform.localPosition;
            card.rectTransform.localPosition = new Vector3(currentLocalPos.x, currentLocalPos.y, 999f);

            // Сохраняем стартовую позицию
            Vector3 startWorldPos = card.rectTransform.position;

            // Перемещаем в dragLayer
            card.rectTransform.SetParent(dragLayer, true);
            card.rectTransform.position = startWorldPos;

            // ДЛИТЕЛЬНОСТЬ АНИМАЦИИ ОДНОЙ КАРТЫ
            float singleCardDuration = 0.5f / wasteCards.Count; // общее время 0.5с / количество карт

            // АНИМАЦИЯ ПЕРЕЛЕТА ОДНОЙ КАРТЫ
            float elapsed = 0f;
            while (elapsed < singleCardDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / singleCardDuration;

                // Плавная интерполяция
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                card.rectTransform.position = Vector3.Lerp(startWorldPos, targetWorldPos, smoothT);

                yield return null;
            }

            // ФИНАЛЬНАЯ ПОЗИЦИЯ
            card.rectTransform.position = targetWorldPos;

            // ПЕРЕВОРАЧИВАЕМ КАРТУ РУБАШКОЙ ВНИЗ
            var cardData = card.GetComponent<CardData>();
            if (cardData != null)
            {
                cardData.SetFaceUp(false, animate: false);
            }

            // Перемещаем в Stock
            card.rectTransform.SetParent(transform, false);
            AddCard(card, faceUp: false);

            // НЕБОЛЬШАЯ ЗАДЕРЖКА МЕЖДУ КАРТАМИ ДЛЯ ЭФФЕКТА ПЕРЕЛИСТЫВАНИЯ
            yield return new WaitForSeconds(0.02f);
        }

        // Обновляем визуализацию
        animationService?.ReorderContainerZ(transform);
        Canvas.ForceUpdateCanvases();
        manager?.CheckGameState();
        LogDebug($"Recycled {wasteCards.Count} cards. Stock now has {cards.Count} cards");
    }

    /// <summary>
    /// Пытается взять карту из Stock (через DragManager/Manager).
    /// </summary>
    private void TryDrawFromStock()
    {
        if (manager == null)
        {
            Debug.LogWarning("[StockPile] Cannot draw: manager is null");
            return;
        }

        // Пытаемся вызвать метод через DragManager
        var dragManager = manager.dragManager ?? manager.GetComponent<DragManager>();
        if (dragManager != null)
        {
            // DragManager должен иметь метод для обработки клика на Stock
            dragManager.OnCardClicked(null);  // null = клик на Stock, не на конкретную карту
            LogDebug("Called DragManager.OnCardClicked for stock draw");
            return;
        }

        // Fallback: прямой вызов через рефлексию (если DragManager недоступен)
        Debug.LogWarning("[StockPile] DragManager not found, cannot process stock click");
    }

    #endregion

    #region Debug

    private void LogDebug(string message)
    {
        if (debug)
        {
            Debug.Log($"[StockPile:{gameObject.name}] {message}");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Show Card Count")]
    private void DebugShowCount()
    {
        Debug.Log($"[StockPile] Cards in stock: {cards.Count}");
    }

    [ContextMenu("Debug: Simulate Click")]
    private void DebugSimulateClick()
    {
        OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
    }
#endif

    #endregion
}