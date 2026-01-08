// WastePile.cs
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

        // --- ИСПРАВЛЕНИЕ: true чтобы сохранить позицию после полета Undo ---
        if (card.rectTransform.parent != transform)
        {
            card.rectTransform.SetParent(transform, true);
        }

        // --- УДАЛЕНО: card.rectTransform.anchoredPosition = Vector2.zero; ---
        // Мы не сбрасываем позицию, так как карта уже прилетела на нужное место (или близко к нему).
        // StartLayoutAnimation ниже все поправит.

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

    public void AddCardsBatch(List<CardController> cardsToAdd, bool faceUp)
    {
        if (cardsToAdd == null || cardsToAdd.Count == 0) return;

        // Останавливаем текущую анимацию, чтобы не было конфликтов
        if (layoutCoroutine != null) { StopCoroutine(layoutCoroutine); layoutCoroutine = null; }

        foreach (var card in cardsToAdd)
        {
            if (card == null) continue;

            if (card.rectTransform.parent != transform) card.rectTransform.SetParent(transform, true);

            // Z коррекция
            Vector3 localPos = card.rectTransform.localPosition;
            localPos.z = (cards.Count + 1) * zStep;
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

    /// <summary>
    /// Добавляет карту из Stock с анимацией layout.
    /// </summary>
    public void OnCardArrivedFromStock(CardController card, bool faceUp)
    {
        if (card == null) return;

        // Сохраняем мировую позицию при смене parent
        card.rectTransform.SetParent(transform, true);

        var cardData = card.GetComponent<CardData>();
        if (cardData != null)
        {
            cardData.SetFaceUp(faceUp);
        }

        if (card.canvasGroup != null)
        {
            card.canvasGroup.blocksRaycasts = false;
        }

        cards.Add(card);

        // Запускаем анимацию перестроения
        StartLayoutAnimation();
    }

    #region ICardContainer Implementation

    /// <summary>
    /// Waste не принимает карты через drag & drop (только через Stock).
    /// </summary>
    public bool CanAccept(CardController card) => false;

    public void OnCardIncoming(CardController card) { }

    public bool IsEmpty() => cards.Count == 0;
    public CardController GetTopCard()
    {
        if (Count == 0) return null;
        // WastePile наследуется от BasePile? Или имеет свой список?
        // Обычно это cards[cards.Count-1]
        return cards.Count > 0 ? cards[cards.Count - 1] : null;
    }

    /// <summary>
    /// Возвращает позицию для новой верхней карты.
    /// </summary>
    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        int n = cards.Count + 1;  // Как будто карта уже добавлена
        int index = n - 1;
        int shift = Mathf.Max(0, n - 3);  // Показываем максимум 3 карты
        int slot = Mathf.Clamp(index - shift, 0, 2);
        float x = slot * xStep;
        return new Vector2(x, 0f);
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
    /// Проверяет, является ли карта верхней в Waste.
    /// </summary>
    public bool ContainsCard(CardController card)
    {
        if (cards.Count == 0) return false;
        return cards[cards.Count - 1] == card;
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

        // Вычисляем целевые позиции (показываем максимум 3 карты справа)
        Vector2[] targetPositions = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            int shift = Mathf.Max(0, n - 3);  // Смещение для показа последних 3
            int slot = Mathf.Clamp(i - shift, 0, 2);
            float x = slot * xStep;
            targetPositions[i] = new Vector2(x, 0f);
        }

        // Сохраняем стартовые позиции
        Vector2[] startPositions = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            var card = cards[i];
            if (card == null)
            {
                startPositions[i] = Vector2.zero;
                continue;
            }

            // Приводим карту к этому transform
            if (card.rectTransform.parent != transform)
            {
                card.rectTransform.SetParent(transform, true);
            }

            startPositions[i] = card.rectTransform.anchoredPosition;
            card.rectTransform.SetSiblingIndex(i);
        }

        // Анимация
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

                Vector2 pos = Vector2.LerpUnclamped(startPositions[i], targetPositions[i], eased);
                card.rectTransform.anchoredPosition = pos;

                // Обновляем Z для глубины
                Vector3 localPos = card.rectTransform.localPosition;
                localPos.z = i * zStep;
                card.rectTransform.localPosition = localPos;
            }

            yield return null;
        }

        // Финальная установка точных позиций
        for (int i = 0; i < n; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            card.rectTransform.anchoredPosition = targetPositions[i];

            Vector3 localPos = card.rectTransform.localPosition;
            localPos.z = i * zStep;
            card.rectTransform.localPosition = localPos;
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