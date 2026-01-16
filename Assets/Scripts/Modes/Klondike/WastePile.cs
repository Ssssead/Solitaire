// WastePile.cs [FIXED UNDO JUMP + YOUR STRUCTURE]
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Стопка Waste (открытые карты из Stock).
/// Отображает до 3 карт с небольшим смещением, анимирует layout.
/// </summary>
public class WastePile : MonoBehaviour, ICardContainer
{
    private List<CardController> cards = new List<CardController>();
    private KlondikeModeManager manager;
    private RectTransform rect;

    // --- НОВОЕ: Три фиксированных слота ---
    private RectTransform[] slots;
    // --------------------------------------

    [Header("Layout Settings")]
    [SerializeField] private float xStep = 35f;             // Горизонтальное смещение между видимыми картами
    [SerializeField] private float zStep = 0.01f;           // Z-шаг для глубины

    [Header("Animation")]
    [SerializeField] private float layoutAnimDuration = 0.24f;
    [SerializeField] private AnimationCurve layoutAnimCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine layoutCoroutine = null;

    public Transform Transform => transform;

    /// <summary>
    /// Инициализация WastePile.
    /// </summary>
    public void Initialize(KlondikeModeManager m, RectTransform tf)
    {
        manager = m;
        rect = tf ?? GetComponent<RectTransform>();

        // Генерируем слоты при инициализации
        GenerateSlots();
    }

    private void GenerateSlots()
    {
        if (slots != null && slots.Length == 3 && slots[0] != null) return;

        slots = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject slotObj = new GameObject($"WasteSlot_{i}");
            slotObj.transform.SetParent(rect, false);

            RectTransform rt = slotObj.AddComponent<RectTransform>();

            // Настройка слотов (как мы обсуждали)
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Позиция слота соответствует смещению карты в старой логике
            rt.anchoredPosition = new Vector2((i * xStep) + 50f, 0);

            slots[i] = rt;
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
    }

    /// <summary>
    /// Добавляет карту в Waste (обычный метод).
    /// </summary>
    public void AddCard(CardController card, bool faceUp)
    {
        if (card == null) return;

        // --- ИСПРАВЛЕНИЕ СКАЧКА ПРИ UNDO ---
        // 1. Вычисляем, в какой слот должна попасть карта ПРЯМО СЕЙЧАС.
        // Если это Undo, карта должна сразу оказаться в Slot2 (или последнем), а не в центре.

        // Предсказываем индекс: текущее кол-во + 1 (эта карта будет последней)
        int predictedCount = cards.Count + 1;
        int targetSlotIndex = GetTargetSlotIndex(predictedCount - 1, predictedCount);

        if (slots == null || slots.Length == 0) GenerateSlots();
        RectTransform targetSlot = slots[targetSlotIndex];

        // 2. Сразу меняем родителя на целевой слот
        if (card.rectTransform.parent != targetSlot)
        {
            card.rectTransform.SetParent(targetSlot, true);
        }
        // -----------------------------------

        // Коррекция Z (локальная)
        Vector3 localPos = card.rectTransform.localPosition;
        localPos.z = (cards.Count + 1) * 0.01f;
        card.rectTransform.localPosition = localPos;

        card.rectTransform.SetAsLastSibling();

        cards.Add(card);

        var cardData = card.GetComponent<CardData>();
        if (cardData != null)
        {
            cardData.SetFaceUp(faceUp, animate: false);
        }

        // Анимация расставит карты по местам (с учетом gap)
        StartLayoutAnimation();

        UpdateInteractivity();
    }

    // --- МЕТОД, КОТОРОГО НЕ ХВАТАЛО UNDOMANAGER'У ---
    public void AddCardsBatch(List<CardController> cardsToAdd, bool faceUp)
    {
        if (cardsToAdd == null || cardsToAdd.Count == 0) return;

        // Останавливаем текущую анимацию, чтобы не было конфликтов
        if (layoutCoroutine != null) { StopCoroutine(layoutCoroutine); layoutCoroutine = null; }

        if (slots == null || slots.Length == 0) GenerateSlots();

        int startCount = cards.Count;
        int totalNewCount = startCount + cardsToAdd.Count;

        for (int i = 0; i < cardsToAdd.Count; i++)
        {
            var card = cardsToAdd[i];
            if (card == null) continue;

            // --- ИСПРАВЛЕНИЕ СКАЧКА ---
            // Вычисляем слот для каждой карты в пачке
            int currentIndex = startCount + i;
            int slotIndex = GetTargetSlotIndex(currentIndex, totalNewCount);
            RectTransform targetSlot = slots[slotIndex];

            if (card.rectTransform.parent != targetSlot)
            {
                card.rectTransform.SetParent(targetSlot, true);
            }
            // --------------------------

            // Z коррекция
            Vector3 localPos = card.rectTransform.localPosition;
            localPos.z = (currentIndex + 1) * zStep;
            card.rectTransform.localPosition = localPos;

            card.rectTransform.SetAsLastSibling();
            cards.Add(card);

            var cardData = card.GetComponent<CardData>();
            if (cardData != null) cardData.SetFaceUp(faceUp, animate: false);
        }

        // Запускаем анимацию один раз для всех
        StartLayoutAnimation();
        UpdateInteractivity();
    }
    // ------------------------------------------------

    /// <summary>
    /// Добавляет карту из Stock с анимацией layout.
    /// </summary>
    public void OnCardArrivedFromStock(CardController card, bool faceUp)
    {
        // Используем AddCard, так как там теперь правильная логика слотов
        AddCard(card, faceUp);
    }

    // --- ВСПОМОГАТЕЛЬНЫЙ МЕТОД ДЛЯ ВЫЧИСЛЕНИЯ СЛОТА ---
    private int GetTargetSlotIndex(int cardIndex, int totalCount)
    {
        if (totalCount <= 0) return 0;

        // Логика "Скользящего окна" (как в анимации)
        // Показываем последние 3 карты

        // Если карт мало (<= 3):
        // 0 -> Slot0
        // 1 -> Slot1
        // 2 -> Slot2

        // Если карт много (> 3):
        // (N-3) -> Slot0
        // (N-2) -> Slot1
        // (N-1) -> Slot2

        int shift = Mathf.Max(0, totalCount - 3);
        int slotIndex = Mathf.Clamp(cardIndex - shift, 0, 2);

        // Защита от переполнения (если слотов меньше 3)
        return Mathf.Min(slotIndex, slots.Length - 1);
    }
    // --------------------------------------------------

    #region ICardContainer Implementation

    // --- ДОБАВЛЕНЫ МЕТОДЫ ИНТЕРФЕЙСА (Они нужны DragManager) ---
    public bool ContainsCard(CardController card) => cards.Contains(card);
    public void OnCardIncoming(CardController card) { } // Заглушка
    // ------------------------------------------------------------

    /// <summary>
    /// Waste не принимает карты через drag & drop (только через Stock).
    /// </summary>
    public bool CanAccept(CardController card) => false;

    public bool IsEmpty() => cards.Count == 0;
    public CardController GetTopCard()
    {
        if (Count == 0) return null;
        return cards.Count > 0 ? cards[cards.Count - 1] : null;
    }

    /// <summary>
    /// Возвращает позицию для новой верхней карты.
    /// </summary>
    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        // Возвращаем позицию 3-го слота (или последнего актуального)
        if (slots != null && slots.Length > 2) return slots[2].anchoredPosition;
        return Vector2.zero;
    }

    public void AcceptCard(CardController card)
    {
        OnCardArrivedFromStock(card, true);
    }

    public void RemoveCardsSilent(int count)
    {
        if (count <= 0 || count > cards.Count) return;
        int startIndex = cards.Count - count;
        cards.RemoveRange(startIndex, count);
        // Анимацию здесь НЕ запускаем, её запустит UndoManager в нужный момент
    }

    #endregion

    #region Card Operations

    /// <summary>
    /// Удаляет и возвращает верхнюю карту.
    /// </summary>
    public CardController PopTop()
    {
        if (cards.Count == 0) return null;

        int lastIndex = cards.Count - 1;
        var topCard = cards[lastIndex];
        cards.RemoveAt(lastIndex);

        StartLayoutAnimation();
        UpdateInteractivity();

        return topCard;
    }

    /// <summary>
    /// Забирает все карты из Waste (для рециркуляции в Stock).
    /// </summary>
    public List<CardController> TakeAll()
    {
        var copy = new List<CardController>(cards);
        cards.Clear();
        UpdateInteractivity();

        // Останавливаем анимацию если она идёт
        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
            layoutCoroutine = null;
        }

        return copy;
    }

    /// <summary>
    /// Количество карт в Waste.
    /// </summary>
    public int Count => cards.Count;

    #endregion

    #region Layout Animation

    /// <summary>
    /// Запускает анимацию перестроения layout.
    /// </summary>
    private void StartLayoutAnimation()
    {
        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
        }

        layoutCoroutine = StartCoroutine(LayoutAnimationCoroutine());
    }

    private IEnumerator LayoutAnimationCoroutine()
    {
        int n = cards.Count;

        if (n == 0)
        {
            UpdateInteractivity();
            yield break;
        }

        // --- ИЗМЕНЕННАЯ ЛОГИКА: Цели теперь зависят от слотов (0,0) ---
        // Но чтобы сохранить вашу анимацию, мы просто привязываем карты к слотам
        // и анимируем к localPosition = 0

        // Создаем слоты, если вдруг их нет
        if (slots == null || slots.Length == 0) GenerateSlots();

        for (int i = 0; i < n; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            // Вычисляем, в какой слот должна попасть карта
            int slotIndex = GetTargetSlotIndex(i, n);
            RectTransform targetSlot = slots[slotIndex];

            // 1. Меняем родителя на слот (это убирает скачок при drag!)
            if (card.rectTransform.parent != targetSlot)
            {
                card.rectTransform.SetParent(targetSlot, true); // true = без визуального прыжка
            }

            // 2. Ставим Z-порядок
            card.rectTransform.SetSiblingIndex(i); // Или SetAsLastSibling если в одном слоте
        }

        // Анимация к центру слотов (0,0)
        float elapsed = 0f;
        while (elapsed < layoutAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / layoutAnimDuration);
            float eased = layoutAnimCurve.Evaluate(t);

            for (int i = 0; i < n; i++)
            {
                var card = cards[i];
                if (card == null) continue;

                // Анимируем к 0,0,0 в локальных координатах слота
                // Z учитываем для глубины
                Vector3 targetLocalPos = new Vector3(0, 0, i * zStep);
                card.rectTransform.localPosition = Vector3.Lerp(card.rectTransform.localPosition, targetLocalPos, eased);
            }

            yield return null;
        }

        // Финальная установка точных позиций
        for (int i = 0; i < n; i++)
        {
            var card = cards[i];
            if (card == null) continue;
            card.rectTransform.localPosition = new Vector3(0, 0, i * zStep);
        }

        UpdateInteractivity();
        layoutCoroutine = null;
    }

    // Добавляем этот метод в конец класса или в регион Public API
    public void UpdateLayout()
    {
        StartLayoutAnimation();
    }
    #endregion

    #region Interactivity

    /// <summary>
    /// Обновляет интерактивность карт (только верхняя может быть взята).
    /// </summary>
    private void UpdateInteractivity()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            bool isTop = (i == cards.Count - 1);

            if (card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = isTop;
                card.canvasGroup.interactable = isTop;
            }
        }
    }

    #endregion

#if UNITY_EDITOR
    [ContextMenu("Debug: Show Card Count")]
    private void DebugShowCount()
    {
        Debug.Log($"[WastePile] Cards in waste: {cards.Count}");
    }

    [ContextMenu("Debug: Trigger Layout Animation")]
    private void DebugTriggerAnimation()
    {
        StartLayoutAnimation();
    }
#endif
}