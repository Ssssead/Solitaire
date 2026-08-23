using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Header("Spacing - Landscape")]
    [FormerlySerializedAs("faceDownGap")]
    [SerializeField] private float faceDownGapLandscape = 6f;
    [FormerlySerializedAs("faceUpGapMax")]
    [SerializeField] private float faceUpGapMaxLandscape = 35f;
    [FormerlySerializedAs("faceUpGapMin")]
    [SerializeField] private float faceUpGapMinLandscape = 15f;
    [FormerlySerializedAs("firstOpenGap")]
    [SerializeField] private float firstOpenGapLandscape = 12f;

    [Header("Spacing - Portrait")]
    [SerializeField] private float faceDownGapPortrait = 14f;
    [SerializeField] private float faceUpGapMaxPortrait = 60f;
    [SerializeField] private float faceUpGapMinPortrait = 25f;
    [SerializeField] private float firstOpenGapPortrait = 24f;

    [SerializeField] private float bottomPadding = 200f;

    private bool IsPortrait => GameLayoutManager.Instance != null && GameLayoutManager.Instance.IsPortrait;
    private float FaceDownGap => IsPortrait ? faceDownGapPortrait : faceDownGapLandscape;
    private float FaceUpGapMax => IsPortrait ? faceUpGapMaxPortrait : faceUpGapMaxLandscape;
    private float FaceUpGapMin => IsPortrait ? faceUpGapMinPortrait : faceUpGapMinLandscape;
    private float FirstOpenGap => IsPortrait ? firstOpenGapPortrait : firstOpenGapLandscape;

    [Header("Dynamic Limits")]
    public Transform bottomBoundary;
    [SerializeField] private float bottomMargin = 10f;

    private float dynamicMaxHeight;
    private float currentFaceUpGap = 35f;

    public float maxHeight = 450f;
    public float cardHeight = 125f;

    [Header("Animation")]
    [SerializeField] private float layoutAnimDuration = 0.18f;
    [SerializeField] private AnimationCurve layoutAnimCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine layoutCoroutine = null;

    public Transform Transform => transform;

    public void Initialize(KlondikeModeManager m, RectTransform tf, float gap)
    {
        manager = m;
        rect = tf ?? GetComponent<RectTransform>();
        CalculateDynamicHeight();

        if (manager != null) animationService = manager.AnimationService;
        if (animationService == null) animationService = FindObjectOfType<AnimationService>();
    }

    private void Update()
    {
        CalculateDynamicHeight();
    }

    private void CalculateDynamicHeight()
    {
        if (bottomBoundary != null)
        {
            float worldHeightAvailable = transform.position.y - bottomBoundary.position.y;
            if (worldHeightAvailable < 0) worldHeightAvailable = 0;

            float currentScale = transform.lossyScale.y;
            if (currentScale < 0.0001f) currentScale = 1f;

            dynamicMaxHeight = worldHeightAvailable / currentScale;
            dynamicMaxHeight -= bottomMargin;
        }
        else
        {
            Canvas c = manager?.rootCanvas ?? GetComponentInParent<Canvas>();
            if (c != null && rect != null)
            {
                float scaleFactor = c.scaleFactor;
                if (scaleFactor <= 0) scaleFactor = 1f;
                dynamicMaxHeight = transform.position.y / scaleFactor - bottomPadding;
            }
            else
            {
                dynamicMaxHeight = Screen.height * 0.55f;
            }
        }

        if (dynamicMaxHeight < cardHeight * 1.5f) dynamicMaxHeight = cardHeight * 1.5f;
    }

    private void OnDrawGizmos()
    {
        if (bottomBoundary != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, bottomBoundary.position.y, transform.position.z));
            Gizmos.DrawWireSphere(new Vector3(transform.position.x, bottomBoundary.position.y, transform.position.z), 10f);
        }
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

    private void ComputeFaceUpGap()
    {
        int faceUpCount = 0;
        int faceDownCount = 0;
        for (int i = 0; i < faceUp.Count; i++) { if (faceUp[i]) faceUpCount++; else faceDownCount++; }

        currentFaceUpGap = FaceUpGapMax;

        if (faceUpCount > 1)
        {
            float currentCardHeight = cardHeight;
            float pivotY = 0.5f;

            if (cards.Count > 0 && cards[0] != null)
            {
                currentCardHeight = cards[0].rectTransform.rect.height;
                pivotY = cards[0].rectTransform.pivot.y;
            }

            float heightTakenByFaceDown = (faceDownCount * FaceDownGap) + (faceDownCount > 0 ? FirstOpenGap : 0);
            float totalGapOffset = heightTakenByFaceDown + ((faceUpCount - 1) * FaceUpGapMax);
            float neededHeight = totalGapOffset + (currentCardHeight * pivotY);

            if (neededHeight > dynamicMaxHeight)
            {
                float availableForGaps = dynamicMaxHeight - heightTakenByFaceDown - (currentCardHeight * pivotY);
                float newGap = availableForGaps / (faceUpCount - 1);
                currentFaceUpGap = Mathf.Max(newGap, FaceUpGapMin);
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

    public void ForceRebuildLayout()
    {
        if (cards.Count == 0) return;

        ComputeFaceUpGap();

        float currentY = 0;
        bool prevOpen = false;
        bool hasClosed = faceUp.Contains(false);

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) continue;

            if (card.rectTransform.parent != transform) card.rectTransform.SetParent(transform, true);
            card.rectTransform.SetSiblingIndex(i);

            if (card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = true;
                card.canvasGroup.alpha = 1f;
            }

            card.rectTransform.anchoredPosition = new Vector2(0, -currentY);

            var data = card.GetComponent<CardData>();
            if (data != null) data.SetFaceUp(faceUp[i], animate: false);

            if (!faceUp[i])
            {
                currentY += FaceDownGap;
                prevOpen = false;
            }
            else
            {
                if (!prevOpen)
                {
                    currentY += (hasClosed ? FirstOpenGap : 0);
                    prevOpen = true;
                }
                currentY += currentFaceUpGap;
            }
        }
    }

    public virtual void ForceRecalculateLayout()
    {
        ComputeFaceUpGap();
        isLayoutLocked = false;
        StartLayoutAnimation();
    }

    public void StartLayoutAnimationPublic() => ForceRecalculateLayout();
    public bool HasHiddenCards() => faceUp.Contains(false);

    public virtual bool CanAccept(CardController card)
    {
        if (card == null) return false;
        var cardData = card.GetComponent<CardData>();
        if (cardData == null || !cardData.IsFaceUp()) return false;

        if (cards.Count == 0) return cardData.model.rank == 13;

        var topCard = cards[cards.Count - 1];
        var topData = topCard.GetComponent<CardData>();
        if (topData == null) return false;
        if (!faceUp[faceUp.Count - 1]) return false;

        bool topRed = (topData.model.suit == Suit.Hearts || topData.model.suit == Suit.Diamonds);
        bool cardRed = (cardData.model.suit == Suit.Hearts || cardData.model.suit == Suit.Diamonds);

        return (topRed != cardRed) && (cardData.model.rank == topData.model.rank - 1);
    }

    public void OnCardIncoming(CardController card) { }
    public virtual void AcceptCard(CardController card) => AddCard(card, true);
    public bool IsEmpty() => cards.Count == 0;
    public CardController GetTopCard() => cards.Count == 0 ? null : cards[cards.Count - 1];

    public virtual Vector2 GetDropAnchoredPosition(CardController card)
    {
        ComputeFaceUpGap();

        if (cards.Count == 0) return Vector2.zero;

        List<Vector2> targets = CalculateTargetAnchors();
        Vector2 lastPos = targets[targets.Count - 1];

        bool lastWasOpen = faceUp[faceUp.Count - 1];
        float gapToAdd = lastWasOpen ? currentFaceUpGap : (FaceDownGap + FirstOpenGap);

        return new Vector2(0f, lastPos.y - gapToAdd);
    }

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

        if (cards.Count == 0) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn);

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
            GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards);
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

        GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, -1);

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
            GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards);
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

    public void UnlockLayout()
    {
        isLayoutLocked = false;
    }

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

    public void SetAnimatingCard(bool animating)
    {
        isAnimatingCard = animating;
        isLayoutLocked = animating;

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

        List<Vector2> targets = CalculateTargetAnchors();
        List<Vector2> starts = new List<Vector2>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) { starts.Add(Vector2.zero); continue; }

            if (card.rectTransform.parent != transform)
            {
                starts.Add(card.rectTransform.anchoredPosition);
                continue;
            }

            starts.Add(card.rectTransform.anchoredPosition);
            card.rectTransform.SetAsLastSibling();
        }

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
                offset = accum;
                accum += FaceDownGap;
                prevWasOpen = false;
            }
            else
            {
                if (!prevWasOpen)
                {
                    offset = hasClosedBeforeFirstOpen ? accum + FirstOpenGap : accum;
                    prevWasOpen = true;
                    accum = offset + currentFaceUpGap;
                }
                else
                {
                    offset = accum;
                    accum += currentFaceUpGap;
                }
            }
            targets.Add(new Vector2(0f, -offset));
        }
        return targets;
    }
}