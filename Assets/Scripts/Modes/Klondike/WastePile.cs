using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class WastePile : MonoBehaviour, ICardContainer
{
    private List<CardController> cards = new List<CardController>();
    private KlondikeModeManager manager;
    private RectTransform rect;

    [Header("Layout Settings (Landscape)")]
    [FormerlySerializedAs("xStep")]
    [SerializeField] private float xStepLandscape = 35f;

    [Header("Layout Settings (Portrait)")]
    [SerializeField] private float xStepPortrait = 60f;

    [Header("Animation")]
    [SerializeField] private float layoutAnimDuration = 0.24f;
    [SerializeField] private AnimationCurve layoutAnimCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float zStep = 0.01f;

    private float CurrentXStep => (GameLayoutManager.Instance != null && GameLayoutManager.Instance.IsPortrait) ? xStepPortrait : xStepLandscape;

    private RectTransform[] slots;
    private const int SLOT_COUNT = 3;

    private Coroutine layoutCoroutine = null;

    public Transform Transform => transform;

    public void Initialize(KlondikeModeManager m, RectTransform tf)
    {
        manager = m;
        rect = tf ?? GetComponent<RectTransform>();
        CreateSlots();
    }

    private void CreateSlots()
    {
        // --- ФИКС: Защита от NullReferenceException ---
        // Если rect еще не передан менеджером, получаем его сами
        if (rect == null) rect = GetComponent<RectTransform>();
        if (rect == null) return;
        // ----------------------------------------------

        // Защита: проверяем, что все слоты существуют и не были удалены
        if (slots != null && slots.Length == SLOT_COUNT && slots[0] != null) return;

        slots = new RectTransform[SLOT_COUNT];
        string[] names = { "WasteSlot_0", "WasteSlot_1", "WasteSlot_2" };

        for (int i = 0; i < SLOT_COUNT; i++)
        {
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

            slots[i].anchorMin = new Vector2(0, 0.5f);
            slots[i].anchorMax = new Vector2(0, 0.5f);
            slots[i].pivot = new Vector2(0, 0.5f);
            slots[i].anchoredPosition = new Vector2(i * CurrentXStep, 0);
            slots[i].sizeDelta = new Vector2(100, 140);
            slots[i].localScale = Vector3.one;
        }
    }
    private void UpdateSlotPositions()
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].anchoredPosition = new Vector2(i * CurrentXStep, 0);
            }
        }
    }

    public void Clear()
    {
        foreach (var card in cards)
        {
            if (card != null && card.gameObject != null) Destroy(card.gameObject);
        }
        cards.Clear();
    }

    public Vector2 GetAnchoredPositionForFutureIndex(int futureTotalCount, int cardIndex)
    {
        int shift = Mathf.Max(0, futureTotalCount - 3);
        int slotIndex = Mathf.Clamp(cardIndex - shift, 0, 2);
        return new Vector2(slotIndex * CurrentXStep, 0f);
    }

    public void AddCard(CardController card, bool faceUp)
    {
        if (card == null) return;
        cards.Add(card);
        var cardData = card.GetComponent<CardData>();
        if (cardData != null) cardData.SetFaceUp(faceUp, animate: false);
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
        cards.Add(card);
        var cardData = card.GetComponent<CardData>();
        if (cardData != null) cardData.SetFaceUp(faceUp);
        if (card.canvasGroup != null) card.canvasGroup.blocksRaycasts = false;

        StartLayoutAnimation();
    }

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
        return new Vector2(slot * CurrentXStep, 0f);
    }

    public void AcceptCard(CardController card) => OnCardArrivedFromStock(card, true);

    public void RemoveCardsSilent(int count)
    {
        if (count <= 0 || count > cards.Count) return;
        int startIndex = cards.Count - count;
        cards.RemoveRange(startIndex, count);
    }

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

    private void StartLayoutAnimation()
    {
        // --- ФИКС: Если объект выключен (например, при смене ориентации), корутина упадёт. Ставим мгновенно. ---
        if (!gameObject.activeInHierarchy)
        {
            ForceLayoutImmediate();
            return;
        }

        if (layoutCoroutine != null) StopCoroutine(layoutCoroutine);
        layoutCoroutine = StartCoroutine(LayoutAnimationCoroutine());
    }

    // Мгновенная расстановка карт (если анимация не может запуститься)
    public void ForceLayoutImmediate()
    {
        if (slots == null || slots.Length == 0 || slots[0] == null) CreateSlots();
        UpdateSlotPositions();

        int n = cards.Count;
        int shift = Mathf.Max(0, n - 3);

        for (int i = 0; i < n; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            int slotIndex = Mathf.Clamp(i - shift, 0, 2);
            RectTransform targetSlot = slots[slotIndex];

            if (card.rectTransform.parent != targetSlot)
            {
                card.rectTransform.SetParent(targetSlot, true);
            }

            card.rectTransform.anchoredPosition = Vector2.zero;
            Vector3 localPos3 = card.rectTransform.localPosition;
            localPos3.z = i * zStep;
            card.rectTransform.localPosition = localPos3;
            card.rectTransform.SetAsLastSibling();
        }
        UpdateInteractivity();
    }

    private IEnumerator LayoutAnimationCoroutine()
    {
        int n = cards.Count;
        if (n == 0)
        {
            UpdateInteractivity();
            yield break;
        }

        // Защита перед стартом
        if (slots == null || slots.Length == 0 || slots[0] == null) CreateSlots();
        UpdateSlotPositions();

        Vector2[] startLocalPositions = new Vector2[n];
        RectTransform[] targetSlots = new RectTransform[n];
        int shift = Mathf.Max(0, n - 3);

        for (int i = 0; i < n; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            int slotIndex = Mathf.Clamp(i - shift, 0, 2);
            targetSlots[i] = slots[slotIndex];

            // Если карта ещё не в слоте — переносим её
            if (card.rectTransform.parent != targetSlots[i])
            {
                card.rectTransform.SetParent(targetSlots[i], true);
            }

            startLocalPositions[i] = card.rectTransform.anchoredPosition;
            card.rectTransform.SetAsLastSibling();
        }

        float elapsed = 0f;
        while (elapsed < layoutAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / layoutAnimDuration);
            float eased = layoutAnimCurve.Evaluate(t);

            for (int i = 0; i < n; i++)
            {
                if (cards[i] == null) continue;
                cards[i].rectTransform.anchoredPosition = Vector2.LerpUnclamped(startLocalPositions[i], Vector2.zero, eased);

                Vector3 localPos3 = cards[i].rectTransform.localPosition;
                localPos3.z = i * zStep;
                cards[i].rectTransform.localPosition = localPos3;
            }
            yield return null;
        }

        for (int i = 0; i < n; i++)
        {
            if (cards[i] == null) continue;
            cards[i].rectTransform.anchoredPosition = Vector2.zero;
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
}