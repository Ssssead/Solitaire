// TableauPile.cs [FINAL: Dynamic Squishing + Strict Gaps]
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TableauPile : MonoBehaviour, ICardContainer
{
    public List<CardController> cards = new List<CardController>();
    public List<bool> faceUp = new List<bool>();

    private KlondikeModeManager manager;
    private RectTransform rect;
    private AnimationService animationService;
    private bool isAnimatingCard = false;
    private bool isLayoutLocked = false;
    public bool IsLocked => isLayoutLocked;

    [Header("Layout Settings")]
    [SerializeField] private float faceDownGap = 6f;        // Отступ для закрытых карт (плотный)
    [SerializeField] private float faceUpGapMax = 35f;      // Максимальный (красивый) отступ для открытых
    [SerializeField] private float faceUpGapMin = 15f;      // Минимальный отступ (при сильном сжатии)
    [SerializeField] private float firstOpenGap = 10f;      // Доп. отступ перед первой открытой картой

    // Динамический отступ, который мы будем вычислять
    private float currentFaceUpGap = 35f;

    // Лимиты
    public float maxHeight = 450f;   // Максимальная высота стопки (подстройте под свой экран)
    public float cardHeight = 160f;  // Высота одной карты

    [Header("Animation")]
    [SerializeField] private float layoutAnimDuration = 0.18f;
    [SerializeField] private AnimationCurve layoutAnimCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine layoutCoroutine = null;

    public Transform Transform => transform;

    public void Initialize(KlondikeModeManager m, RectTransform tf, float gap)
    {
        manager = m;
        rect = tf ?? GetComponent<RectTransform>();

        if (manager != null)
        {
            animationService = manager.animationService ?? manager.GetComponent<AnimationService>();
        }
        if (animationService == null) animationService = FindObjectOfType<AnimationService>();
    }

    public void Clear()
    {
        foreach (var card in cards)
        {
            if (card != null && card.gameObject != null) Destroy(card.gameObject);
        }
        cards.Clear();
        faceUp.Clear();

        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
            layoutCoroutine = null;
        }
        isLayoutLocked = false;
        isAnimatingCard = false;
    }

    // --- ГЛАВНОЕ ИСПРАВЛЕНИЕ: ДИНАМИЧЕСКИЙ РАСЧЕТ ОТСТУПА ---
    private void ComputeFaceUpGap()
    {
        int faceUpCount = 0;
        int faceDownCount = 0;

        foreach (bool isOpen in faceUp)
        {
            if (isOpen) faceUpCount++;
            else faceDownCount++;
        }

        // По умолчанию хотим максимальный красивый отступ
        currentFaceUpGap = faceUpGapMax;

        // Если открытых карт больше 1, проверяем, влезаем ли мы в maxHeight
        if (faceUpCount > 1)
        {
            // Высота, занятая закрытыми картами
            float heightTakenByFaceDown = faceDownCount * faceDownGap;

            // Добавляем firstOpenGap, если есть закрытые карты
            if (faceDownCount > 0) heightTakenByFaceDown += firstOpenGap;

            // Какая высота нужна открытым картам при МАКСИМАЛЬНОМ отступе?
            // (N-1) * gap + высота последней карты
            float neededFaceUpHeight = ((faceUpCount - 1) * faceUpGapMax) + cardHeight;
            float totalNeeded = heightTakenByFaceDown + neededFaceUpHeight;

            // Если не влезаем -> СЖИМАЕМ
            if (totalNeeded > maxHeight)
            {
                // Сколько места осталось чисто для промежутков между открытыми картами?
                float availableSpaceForGaps = maxHeight - cardHeight - heightTakenByFaceDown;

                // Делим на количество промежутков
                currentFaceUpGap = availableSpaceForGaps / (faceUpCount - 1);

                // Не даем сжаться сильнее минимума (чтобы карты не слиплись)
                currentFaceUpGap = Mathf.Max(currentFaceUpGap, faceUpGapMin);
            }
        }
    }

    public virtual void AddCard(CardController card, bool faceUpFlag)
    {
        if (card == null) return;
        if (card.rectTransform.parent != transform) card.rectTransform.SetParent(transform, true);

        cards.Add(card);
        faceUp.Add(faceUpFlag);

        var cardData = card.GetComponent<CardData>();
        if (cardData != null) cardData.SetFaceUp(faceUpFlag, animate: false);

        card.rectTransform.SetAsLastSibling();

        // Сначала пересчитываем, потом анимируем
        ForceRecalculateLayout();
    }

    public virtual void AddCardsBatch(List<CardController> cardsToAdd, bool faceUpFlag)
    {
        if (cardsToAdd == null || cardsToAdd.Count == 0) return;

        isLayoutLocked = false;
        if (layoutCoroutine != null) StopCoroutine(layoutCoroutine);

        for (int i = 0; i < cardsToAdd.Count; i++)
        {
            var card = cardsToAdd[i];
            if (card == null) continue;
            if (card.rectTransform.parent != transform) card.rectTransform.SetParent(transform, true);

            cards.Add(card);
            faceUp.Add(faceUpFlag);

            var cardData = card.GetComponent<CardData>();
            if (cardData != null) cardData.SetFaceUp(faceUpFlag, animate: false);

            card.rectTransform.SetAsLastSibling();
        }
        ForceRecalculateLayout();
    }

    public virtual void ForceRecalculateLayout()
    {
        // Сначала вычисляем, какой должен быть отступ
        ComputeFaceUpGap();
        // Разблокируем на всякий случай
        isLayoutLocked = false;
        // Запускаем анимацию на новые позиции
        StartLayoutAnimation();
    }

    public void StartLayoutAnimationPublic() => ForceRecalculateLayout();

    public bool HasHiddenCards() => faceUp.Contains(false);

    // --- ICardContainer ---

    public virtual bool CanAccept(CardController card)
    {
        if (card == null) return false;
        var cardData = card.GetComponent<CardData>();
        if (cardData == null || !cardData.IsFaceUp()) return false;

        if (cards.Count == 0) return cardData.model.rank == 13; // King

        var topCard = cards[cards.Count - 1];
        var topData = topCard.GetComponent<CardData>();
        if (topData == null) return false;
        if (!faceUp[faceUp.Count - 1]) return false;

        bool topRed = (topData.model.suit == Suit.Hearts || topData.model.suit == Suit.Diamonds);
        bool cardRed = (cardData.model.suit == Suit.Hearts || cardData.model.suit == Suit.Diamonds);

        return (topRed != cardRed) && (cardData.model.rank == topData.model.rank - 1);
    }

    public void OnCardIncoming(CardController card) { }
    public void AcceptCard(CardController card) => AddCard(card, true);
    public bool IsEmpty() => cards.Count == 0;
    public CardController GetTopCard() => cards.Count == 0 ? null : cards[cards.Count - 1];

    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        // Считаем отступ, как если бы карта уже была там
        // (Для точности можно временно увеличить count в расчетах, но обычно достаточно текущего)
        ComputeFaceUpGap();

        if (cards.Count == 0) return Vector2.zero;

        List<Vector2> targets = CalculateTargetAnchors();
        // Позиция для НОВОЙ карты будет после последней существующей
        Vector2 lastPos = targets[targets.Count - 1];

        // Новая карта всегда FaceUp, значит добавляем currentFaceUpGap
        // Исключение: если последняя карта была FaceDown
        bool lastWasOpen = faceUp[faceUp.Count - 1];
        float gapToAdd = lastWasOpen ? currentFaceUpGap : (faceDownGap + firstOpenGap);

        // Упрощение: используем логику CalculateTargetAnchors для N+1
        // Но проще взять последнюю точку и прибавить отступ
        return new Vector2(0f, lastPos.y - gapToAdd);
    }

    // --- Sequence Operations ---

    public int IndexOfCard(CardController card) => cards.IndexOf(card);

    public virtual List<CardController> GetFaceUpSequenceFrom(int idx)
    {
        var sequence = new List<CardController>();
        if (idx < 0 || idx >= cards.Count) return sequence;
        if (!faceUp[idx]) return sequence;

        for (int i = idx; i < cards.Count; i++)
        {
            if (!faceUp[i]) break;
            sequence.Add(cards[i]);
        }
        return sequence;
    }

    public List<CardController> RemoveSequenceFrom(int idx)
    {
        var seq = new List<CardController>();
        if (idx < 0 || idx >= cards.Count) return seq;

        int count = cards.Count - idx;
        seq.AddRange(cards.GetRange(idx, count));

        cards.RemoveRange(idx, count);
        faceUp.RemoveRange(idx, count);

        ForceRecalculateLayout();
        return seq;
    }

    public void RemoveCardsSilent(int count)
    {
        if (count <= 0 || count > cards.Count) return;
        int startIndex = cards.Count - count;
        cards.RemoveRange(startIndex, count);
        faceUp.RemoveRange(startIndex, count);
    }

    public bool CheckAndFlipTop()
    {
        if (cards.Count == 0) return false;
        int topIdx = cards.Count - 1;

        if (!faceUp[topIdx])
        {
            faceUp[topIdx] = true;
            cards[topIdx].GetComponent<CardData>()?.SetFaceUp(true);
            if (manager != null) manager.CheckGameState();
            ForceRecalculateLayout();
            return true;
        }
        return false;
    }

    public void ForceFlipFaceDown(int index, bool immediate = false)
    {
        if (index < 0 || index >= cards.Count) return;
        faceUp[index] = false;
        var cardData = cards[index].GetComponent<CardData>();
        if (cardData != null) cardData.SetFaceUp(false, animate: !immediate);
        ForceRecalculateLayout();
    }

    public void FlipTopIfNeeded()
    {
        if (cards.Count == 0) return;
        int topIdx = cards.Count - 1;
        if (!faceUp[topIdx])
        {
            faceUp[topIdx] = true;
            cards[topIdx].GetComponent<CardData>()?.SetFaceUp(true);
            if (manager != null) manager.CheckGameState();
        }
        ForceRecalculateLayout();
    }

    public List<CardController> GetAllCards() => new List<CardController>(cards);

    public List<CardController> GetFaceUpCards()
    {
        List<CardController> result = new List<CardController>();
        for (int i = 0; i < cards.Count; i++)
        {
            if (i < faceUp.Count && faceUp[i] && cards[i] != null)
                result.Add(cards[i]);
        }
        return result;
    }
    public int Count => cards.Count;

    /// <summary>
    /// Принудительно разблокирует лейаут (вызывается извне, например из DragManager).
    /// </summary>
    public void UnlockLayout()
    {
        isLayoutLocked = false;
    }

    // --- Helpers ---

    private bool HasClosedCardsBeforeFirstOpen()
    {
        foreach (bool isOpen in faceUp)
        {
            if (!isOpen) return true;
            if (isOpen) return false;
        }
        return false;
    }

    public void ForceUpdateFromTransform()
    {
        // Re-sync logical lists with Transform children
        var newCards = new List<CardController>();
        var newFaceUp = new List<bool>();

        for (int i = 0; i < transform.childCount; i++)
        {
            var cardCtrl = transform.GetChild(i).GetComponent<CardController>();
            if (cardCtrl != null)
            {
                newCards.Add(cardCtrl);
                var cardData = cardCtrl.GetComponent<CardData>();
                newFaceUp.Add(cardData != null && cardData.IsFaceUp());
            }
        }

        cards = newCards;
        faceUp = newFaceUp;
        ForceRecalculateLayout();
    }

    // --- ANIMATION ---

    public void SetAnimatingCard(bool animating)
    {
        isAnimatingCard = animating;
        isLayoutLocked = animating;

        // --- НОВОЕ: Физически отключаем клики по картам в этой стопке ---
        // Если идет анимация (animating = true), то blocksRaycasts = false.
        // Когда анимация закончилась, включаем обратно.
        foreach (var card in cards)
        {
            if (card != null && card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = !animating;
            }
        }
    }

    public void StartLayoutAnimation()
    {
        if (isLayoutLocked) return;
        if (layoutCoroutine != null) StopCoroutine(layoutCoroutine);
        layoutCoroutine = StartCoroutine(AnimateLayoutCoroutine());
    }

    private IEnumerator AnimateLayoutCoroutine()
    {
        if (isLayoutLocked) yield break;
        if (cards.Count == 0)
        {
            animationService?.ReorderContainerZ(transform);
            yield break;
        }

        // 1. Вычисляем целевые точки (с учетом нового gap)
        List<Vector2> targets = CalculateTargetAnchors();

        // 2. Запоминаем старт
        List<Vector2> starts = new List<Vector2>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) { starts.Add(Vector2.zero); continue; }

            // Если карта чужая (drag), не анимируем ее
            if (card.rectTransform.parent != transform)
            {
                starts.Add(card.rectTransform.anchoredPosition);
                continue;
            }

            starts.Add(card.rectTransform.anchoredPosition);
            card.rectTransform.SetAsLastSibling();
        }

        // 3. Анимация
        float elapsed = 0f;
        while (elapsed < layoutAnimDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(1e-6f, layoutAnimDuration));
            float eased = layoutAnimCurve.Evaluate(t);

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].rectTransform.parent == transform)
                {
                    cards[i].rectTransform.anchoredPosition = Vector2.LerpUnclamped(starts[i], targets[i], eased);
                }
            }
            yield return null;
        }

        // 4. Финал
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && cards[i].rectTransform.parent == transform)
                cards[i].rectTransform.anchoredPosition = targets[i];
        }
        animationService?.ReorderContainerZ(transform);
        layoutCoroutine = null;
    }

    private List<Vector2> CalculateTargetAnchors()
    {
        // На всякий случай обновляем gap еще раз, хотя он должен быть уже рассчитан
        ComputeFaceUpGap();

        List<Vector2> targets = new List<Vector2>(cards.Count);
        float accum = 0f;
        bool prevWasOpen = false;
        bool hasClosedBeforeFirstOpen = HasClosedCardsBeforeFirstOpen();

        for (int i = 0; i < cards.Count; i++)
        {
            float offset = 0f;

            if (!faceUp[i])
            {
                // Закрытая карта
                offset = accum;
                accum += faceDownGap;
                prevWasOpen = false;
            }
            else
            {
                // Открытая карта
                if (!prevWasOpen)
                {
                    // Первая открытая после закрытых
                    offset = hasClosedBeforeFirstOpen ? accum + firstOpenGap : accum;
                    prevWasOpen = true;
                    accum = offset + currentFaceUpGap;
                }
                else
                {
                    // Последующие открытые
                    offset = accum;
                    accum += currentFaceUpGap;
                }
            }
            targets.Add(new Vector2(0f, -offset));
        }
        return targets;
    }
}