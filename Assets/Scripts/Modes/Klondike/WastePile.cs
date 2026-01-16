using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WastePile : MonoBehaviour, ICardContainer
{
    private List<CardController> cards = new List<CardController>();
    private KlondikeModeManager manager;
    private RectTransform rect;

    [Header("Layout Settings")]
    [SerializeField] private float xStep = 35f;
    [SerializeField] private float zStep = 0.01f;

    [Header("Animation")]
    [SerializeField] private float layoutAnimDuration = 0.24f;
    [SerializeField] private AnimationCurve layoutAnimCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // --- ДОБАВЛЕНО: Система слотов ---
    private RectTransform[] slots;
    private const int SLOT_COUNT = 3;
    // ---------------------------------

    private Coroutine layoutCoroutine = null;

    public Transform Transform => transform;

    public void Initialize(KlondikeModeManager m, RectTransform tf)
    {
        manager = m;
        rect = tf ?? GetComponent<RectTransform>();

        // Создаем физические слоты при инициализации
        CreateSlots();
    }

    // Метод создания слотов (защита от скачков при Drag)
    private void CreateSlots()
    {
        if (slots != null && slots.Length == SLOT_COUNT && slots[0] != null) return;

        slots = new RectTransform[SLOT_COUNT];
        string[] names = { "Slot_0", "Slot_1", "Slot_2" };

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            // Ищем или создаем
            Transform existing = rect.Find(names[i]);
            if (existing != null)
            {
                slots[i] = existing as RectTransform;
            }
            else
            {
                GameObject slotObj = new GameObject(names[i], typeof(RectTransform));
                slotObj.transform.SetParent(rect, false);
                slots[i] = slotObj.GetComponent<RectTransform>();
            }

            // Настраиваем позицию слота
            slots[i].anchorMin = new Vector2(0, 0.5f);
            slots[i].anchorMax = new Vector2(0, 0.5f);
            slots[i].pivot = new Vector2(0, 0.5f);
            slots[i].anchoredPosition = new Vector2(i * xStep, 0); // 0, 35, 70
            slots[i].sizeDelta = new Vector2(100, 140); // Размер примерный, не влияет на логику
        }
    }

    public void Clear()
    {
        foreach (var card in cards)
        {
            if (card != null && card.gameObject != null) Destroy(card.gameObject);
        }
        cards.Clear();
        // Слоты не удаляем, они нужны всегда
    }

    public Vector2 GetAnchoredPositionForFutureIndex(int futureTotalCount, int cardIndex)
    {
        // Возвращаем позицию СЛОТА, в который попадет карта
        int shift = Mathf.Max(0, futureTotalCount - 3);
        int slotIndex = Mathf.Clamp(cardIndex - shift, 0, 2);

        // Это координата слота относительно WastePile
        return new Vector2(slotIndex * xStep, 0f);
    }

    public void AddCard(CardController card, bool faceUp)
    {
        if (card == null) return;
        cards.Add(card); // Сначала добавляем в список логики

        // Устанавливаем данные
        var cardData = card.GetComponent<CardData>();
        if (cardData != null) cardData.SetFaceUp(faceUp, animate: false);

        // Распределяем по слотам
        StartLayoutAnimation();
        UpdateInteractivity();
    }

    public void AddCardsBatch(List<CardController> cardsToAdd, bool faceUp)
    {
        if (cardsToAdd == null || cardsToAdd.Count == 0) return;
        if (layoutCoroutine != null) { StopCoroutine(layoutCoroutine); layoutCoroutine = null; }

        foreach (var card in cardsToAdd)
        {
            if (card == null) continue;
            cards.Add(card);
            var cardData = card.GetComponent<CardData>();
            if (cardData != null) cardData.SetFaceUp(faceUp, animate: false);
        }

        StartLayoutAnimation();
        UpdateInteractivity();
    }

    public void OnCardArrivedFromStock(CardController card, bool faceUp)
    {
        if (card == null) return;

        // Важно: пока не меняем родителя, анимация сделает это плавно
        cards.Add(card);

        var cardData = card.GetComponent<CardData>();
        if (cardData != null) cardData.SetFaceUp(faceUp);
        if (card.canvasGroup != null) card.canvasGroup.blocksRaycasts = false;

        StartLayoutAnimation();
    }

    #region ICardContainer Implementation

    public bool CanAccept(CardController card) => false;
    public void OnCardIncoming(CardController card) { }
    public bool IsEmpty() => cards.Count == 0;

    public CardController GetTopCard() => cards.Count > 0 ? cards[cards.Count - 1] : null;

    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        int n = cards.Count + 1;
        int index = n - 1;
        int shift = Mathf.Max(0, n - 3);
        int slot = Mathf.Clamp(index - shift, 0, 2);
        return new Vector2(slot * xStep, 0f);
    }

    public void AcceptCard(CardController card) => OnCardArrivedFromStock(card, true);

    public void RemoveCardsSilent(int count)
    {
        if (count <= 0 || count > cards.Count) return;
        int startIndex = cards.Count - count;
        cards.RemoveRange(startIndex, count);
    }

    #endregion

    #region Card Operations

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

    public List<CardController> TakeAll()
    {
        var copy = new List<CardController>(cards);
        cards.Clear();
        UpdateInteractivity();
        if (layoutCoroutine != null) { StopCoroutine(layoutCoroutine); layoutCoroutine = null; }
        return copy;
    }

    public bool ContainsCard(CardController card) => cards.Contains(card);
    public int Count => cards.Count;

    #endregion

    #region Layout Animation

    private void StartLayoutAnimation()
    {
        if (layoutCoroutine != null) StopCoroutine(layoutCoroutine);
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

        // Если слоты по какой-то причине не созданы (например, в редакторе), создаем
        if (slots == null || slots.Length == 0 || slots[0] == null) CreateSlots();

        // 1. Подготовка структур данных для анимации
        // Мы будем анимировать перемещение из текущей позиции в (0,0) внутри целевого слота
        Vector2[] startLocalPositions = new Vector2[n];
        RectTransform[] targetSlots = new RectTransform[n];

        int shift = Mathf.Max(0, n - 3);

        for (int i = 0; i < n; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            // Определяем целевой слот (0, 1 или 2)
            int slotIndex = Mathf.Clamp(i - shift, 0, 2);
            // Если карта "ушла в историю" (i < shift), она все равно визуально в 0-м слоте (под низом)
            targetSlots[i] = slots[slotIndex];

            // МЕНЯЕМ РОДИТЕЛЯ С СОХРАНЕНИЕМ ПОЗИЦИИ
            if (card.rectTransform.parent != targetSlots[i])
            {
                // true = worldPositionStays. Карта остается там где была визуально, но координаты меняются.
                card.rectTransform.SetParent(targetSlots[i], true);
            }

            // Запоминаем текущую локальную позицию (с которой начнется анимация)
            startLocalPositions[i] = card.rectTransform.anchoredPosition;

            // Z-Order: чем больше i, тем выше карта
            card.rectTransform.SetAsLastSibling();
        }

        // 2. Анимация
        float elapsed = 0f;
        while (elapsed < layoutAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / layoutAnimDuration);
            float eased = layoutAnimCurve.Evaluate(t);

            for (int i = 0; i < n; i++)
            {
                if (cards[i] == null) continue;

                // Цель всегда (0,0) внутри слота
                Vector2 targetLocal = Vector2.zero;

                // Лерп от стартовой локальной позиции к 0
                cards[i].rectTransform.anchoredPosition = Vector2.LerpUnclamped(startLocalPositions[i], targetLocal, eased);

                // Z глубина
                Vector3 localPos3 = cards[i].rectTransform.localPosition;
                localPos3.z = i * zStep;
                cards[i].rectTransform.localPosition = localPos3;
            }
            yield return null;
        }

        // 3. Финал (жесткая привязка к 0)
        for (int i = 0; i < n; i++)
        {
            if (cards[i] == null) continue;
            cards[i].rectTransform.anchoredPosition = Vector2.zero; // Идеальный 0,0 в слоте

            Vector3 localPos3 = cards[i].rectTransform.localPosition;
            localPos3.z = i * zStep;
            cards[i].rectTransform.localPosition = localPos3;
        }

        UpdateInteractivity();
        layoutCoroutine = null;
    }

    public void UpdateLayout()
    {
        StartLayoutAnimation();
    }
    #endregion

    #region Interactivity

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