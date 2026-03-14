using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

[RequireComponent(typeof(CanvasGroup))]
public class SpiderTableauPile : TableauPile
{
    [Header("Spider References")]
    public SpiderModeManager spiderMode;

    [Header("EXPLICIT Dynamic Boundaries")]
    [Tooltip("ОБЫЧНАЯ граница (Пустышка ВНИЗУ, когда слоту ничего не мешает)")]
    public Transform normalBoundary;
    [Tooltip("СЖАТАЯ граница (Пустышка ВВЕРХУ, когда есть Сток или Фундамент)")]
    public Transform compressedBoundary;

    [Header("Spider Dynamic Gaps")]
    public float faceUpActiveMinGap = 15f;
    public float faceUpInactiveMinGap = 8f;
    public float faceUpMaxGap = 35f;

    private CanvasGroup _pileCanvasGroup;
    private bool isAssembling = false;
    private bool _isCompressed = false;
    private Coroutine _spiderLayout;
    private FieldInfo _baseLayoutField;

    [Header("Feedback Anchors")]
    [Tooltip("Пустышка прямо под слотом для стрелочки (когда слот пуст)")]
    public Transform emptySlotArrowAnchor;

    void Start()
    {
        _pileCanvasGroup = GetComponent<CanvasGroup>();
        if (_pileCanvasGroup == null) _pileCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (normalBoundary != null) bottomBoundary = normalBoundary;

        // Перехватчик базовой корутины
        _baseLayoutField = typeof(TableauPile).GetField("layoutCoroutine", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private void Update()
    {
        if (_baseLayoutField != null && _baseLayoutField.GetValue(this) is Coroutine baseAnim)
        {
            StopCoroutine(baseAnim);
            _baseLayoutField.SetValue(this, null);
            ForceRecalculateLayout();
        }
    }

    public void SetLayoutCompressed(bool isCompressed)
    {
        if (normalBoundary == null || compressedBoundary == null) return;

        if (_isCompressed != isCompressed)
        {
            _isCompressed = isCompressed;
            bottomBoundary = isCompressed ? compressedBoundary : normalBoundary;

            ForceRecalculateLayout();
            StopCoroutine(RetryRecalculate());
            StartCoroutine(RetryRecalculate());
        }
    }

    private IEnumerator RetryRecalculate()
    {
        yield return new WaitForSeconds(0.4f);
        ForceRecalculateLayout();
    }

    public override void ForceRecalculateLayout()
    {
        if (_spiderLayout != null) StopCoroutine(_spiderLayout);
        _spiderLayout = StartCoroutine(SpiderLayoutRoutine());
    }

    private List<Vector2> CalculateSpiderAnchors()
    {
        if (cards.Count == 0) return new List<Vector2>();

        int activeCount = 1;
        if (faceUp.Count > 0 && faceUp[faceUp.Count - 1])
        {
            for (int i = cards.Count - 2; i >= 0; i--)
            {
                if (!faceUp[i]) break;
                if (cards[i] != null && cards[i + 1] != null &&
                    cards[i].cardModel.suit == cards[i + 1].cardModel.suit &&
                    cards[i].cardModel.rank == cards[i + 1].cardModel.rank + 1)
                    activeCount++;
                else break;
            }
        }
        else activeCount = 0;

        int upCount = 0, downCount = 0;
        foreach (bool u in faceUp) { if (u) upCount++; else downCount++; }

        float gapDown = 6f, gapFirst = 12f;
        float area = 800f;

        // --- ТОЧНАЯ ФОРМУЛА ИЗ БАЗОВОГО TABLEAUPILE.CS ---
        if (bottomBoundary != null)
        {
            float worldHeightAvailable = transform.position.y - bottomBoundary.position.y;
            if (worldHeightAvailable < 0) worldHeightAvailable = 0;

            float currentScale = transform.lossyScale.y;
            if (currentScale < 0.0001f) currentScale = 1f;

            area = (worldHeightAvailable / currentScale) - 10f; // 10f = bottom margin
        }

        float ch = 125f;
        float pivotY = 0.5f;
        if (cards.Count > 0 && cards[0] != null)
        {
            ch = cards[0].rectTransform.rect.height;
            pivotY = cards[0].rectTransform.pivot.y;
        }

        // Вычитаем только расстояние от пивота до низа карты (иначе граница уползет вверх!)
        area -= (ch * pivotY);
        // ---------------------------------------------------

        float used = (downCount * gapDown) + (downCount > 0 && upCount > 0 ? gapFirst : 0);
        float free = area - used;

        int actGaps = Mathf.Max(0, activeCount - 1);
        int inactGaps = upCount - activeCount;

        float gAct = faceUpMaxGap, gInact = faceUpMaxGap;
        if (actGaps + inactGaps > 0)
        {
            float uni = free / (actGaps + inactGaps);
            if (uni >= faceUpActiveMinGap)
            {
                gAct = gInact = Mathf.Min(uni, faceUpMaxGap);
            }
            else
            {
                gAct = faceUpActiveMinGap;
                if (inactGaps > 0)
                    gInact = Mathf.Clamp((free - actGaps * gAct) / inactGaps, faceUpInactiveMinGap, faceUpMaxGap);
            }
        }

        List<Vector2> targets = new List<Vector2>(cards.Count);
        float y = 0f;
        bool prevUp = false;

        for (int i = 0; i < cards.Count; i++)
        {
            float offset = 0f;
            if (!faceUp[i])
            {
                offset = y;
                y += gapDown;
                prevUp = false;
            }
            else
            {
                if (!prevUp)
                {
                    offset = (downCount > 0) ? y + gapFirst : y;
                    prevUp = true;
                }
                else offset = y;

                float nextGap = (i >= cards.Count - activeCount) ? gAct : gInact;
                y = offset + nextGap;
            }
            targets.Add(new Vector2(0, -offset));
        }

        return targets;
    }

    private IEnumerator SpiderLayoutRoutine()
    {
        if (cards.Count == 0) yield break;

        List<Vector2> targets = CalculateSpiderAnchors();

        float dur = 0.18f, time = 0f;
        List<Vector2> starts = new List<Vector2>(cards.Count);
        foreach (var c in cards) starts.Add(c != null ? c.rectTransform.anchoredPosition : Vector2.zero);

        while (time < dur)
        {
            time += Time.unscaledDeltaTime;
            float t = time / dur;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] && cards[i].transform.parent == transform)
                    cards[i].rectTransform.anchoredPosition = Vector2.Lerp(starts[i], targets[i], t);
            }
            yield return null;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] && cards[i].transform.parent == transform)
                cards[i].rectTransform.anchoredPosition = targets[i];
        }
    }

    public override Vector2 GetDropAnchoredPosition(CardController card)
    {
        if (cards.Count == 0) return Vector2.zero;
        if (card == null) return base.GetDropAnchoredPosition(card);

        cards.Add(card);
        faceUp.Add(true);

        List<Vector2> targets = CalculateSpiderAnchors();
        Vector2 dropPos = targets[targets.Count - 1];

        cards.RemoveAt(cards.Count - 1);
        faceUp.RemoveAt(faceUp.Count - 1);

        return dropPos;
    }

    public override bool CanAccept(CardController card)
    {
        if (_pileCanvasGroup != null && !_pileCanvasGroup.blocksRaycasts) return false;
        if (card == null) return false;
        if (cards.Count == 0) return true;
        return cards[cards.Count - 1].cardModel.rank == card.cardModel.rank + 1;
    }

    private void CheckSuit()
    {
        if (isAssembling) return;
        if (spiderMode && spiderMode.undoManager && spiderMode.undoManager.IsUndoing) return;
        if (cards.Count < 13) return;
        var last = cards[cards.Count - 1];
        if (last.cardModel.rank != 1) return;

        Suit s = last.cardModel.suit;
        int r = 1;
        int start = -1;
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var c = cards[i];
            if (c.cardModel.suit == s && c.cardModel.rank == r && c.GetComponent<CardData>().IsFaceUp())
            {
                if (r == 13) { start = i; break; }
                r++;
            }
            else break;
        }

        if (start != -1)
        {
            isAssembling = true;
            StartCoroutine(MoveSequenceToFoundation(start));
        }
    }

    private IEnumerator MoveSequenceToFoundation(int startIndex)
    {
        var foundation = spiderMode.GetNextEmptyFoundation();
        if (foundation == null) { isAssembling = false; yield break; }

        if (_pileCanvasGroup != null) _pileCanvasGroup.blocksRaycasts = false;
        if (spiderMode != null) spiderMode.ActiveFoundationAnimations++;
        if (spiderMode != null) spiderMode.OnRowCompleted();

        spiderMode.UpdateTableauLayouts();

        yield return new WaitForSeconds(0.5f);

        List<CardController> sequence = RemoveSequenceFrom(startIndex);

        ForceRecalculateLayout();

        if (spiderMode.DragLayer)
        {
            foreach (var c in sequence)
            {
                c.transform.SetParent(spiderMode.DragLayer, true);
                c.transform.SetAsLastSibling();
            }
        }

        string batchID = System.Guid.NewGuid().ToString();
        if (spiderMode.undoManager) spiderMode.undoManager.TagLastMove(batchID);

        List<Transform> parents = new List<Transform>();
        List<Vector3> positions = new List<Vector3>();
        List<int> siblings = new List<int>();
        foreach (var c in sequence)
        {
            parents.Add(transform);
            positions.Add(Vector3.zero);
            siblings.Add(0);
        }

        if (spiderMode.undoManager)
            spiderMode.undoManager.RecordMove(sequence, this, foundation, parents, positions, siblings, batchID);

        float flyDuration = 0.4f;
        float staggerDelay = 0.05f;

        for (int i = sequence.Count - 1; i >= 0; i--)
        {
            var card = sequence[i];
            if (card.canvasGroup) card.canvasGroup.blocksRaycasts = false;
            StartCoroutine(AnimateCardToFoundation(card, foundation, flyDuration));
            yield return new WaitForSeconds(staggerDelay);
        }

        yield return new WaitForSeconds(flyDuration);

        if (spiderMode != null) spiderMode.ActiveFoundationAnimations--;
        if (_pileCanvasGroup != null) _pileCanvasGroup.blocksRaycasts = true;

        FlipTopIfNeeded();
        foundation.SetCompleted(sequence[0].cardModel.suit);

        isAssembling = false;
        spiderMode.CheckGameState();
    }

    private IEnumerator AnimateCardToFoundation(CardController card, SpiderFoundationPile foundation, float duration)
    {
        if (spiderMode.DragLayer) card.transform.SetParent(spiderMode.DragLayer);
        card.transform.SetAsLastSibling();

        Vector3 startPos = card.transform.position;
        Vector3 targetPos = foundation.transform.position;
        float t = 0;

        while (t < duration)
        {
            t += Time.deltaTime;
            card.transform.position = Vector3.Lerp(startPos, targetPos, t / duration);
            yield return null;
        }

        card.ForceSnapToContainer(foundation);
        if (card.canvasGroup) card.canvasGroup.blocksRaycasts = true;
    }

    public override void AddCard(CardController card, bool faceUp)
    {
        base.AddCard(card, faceUp);
        CheckSuit();
    }

    public override void AddCardsBatch(List<CardController> cardsToAdd, bool faceUp)
    {
        base.AddCardsBatch(cardsToAdd, faceUp);
        CheckSuit();
    }
}