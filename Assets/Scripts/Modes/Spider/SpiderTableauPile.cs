using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class SpiderTableauPile : TableauPile
{
    [Header("Spider References")]
    public SpiderModeManager spiderMode;

    private CanvasGroup _pileCanvasGroup;

    // --- НАСТРОЙКИ ВЫСОТЫ ---
    private const float DefaultHeight = 450f;
    private const float CompressedHeight = 375f;

    void Start()
    {
        _pileCanvasGroup = GetComponent<CanvasGroup>();
        if (_pileCanvasGroup == null) _pileCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Гарантируем дефолт при старте
       // maxHeight = DefaultHeight;
    }

    // --- НОВЫЙ МЕТОД: Переключение высоты ---
    public void SetLayoutCompressed(bool isCompressed)
    {
        float targetHeight = isCompressed ? CompressedHeight : DefaultHeight;

        // Если высота отличается, меняем и пересчитываем
       // if (Mathf.Abs(maxHeight - targetHeight) > 1f)
        {
      //      maxHeight = targetHeight;
            ForceRecalculateLayout(); // Это встроенный метод TableauPile, он сам всё пересчитает
        }
    }

    // ... (Остальной код: CanAccept, GetFaceUpSequenceFrom, AddCard... оставляем БЕЗ изменений) ...
    // ... (Методы CheckSuit, MoveSequenceToFoundation и т.д. тоже оставляем) ...

    // --- ВАЖНО: Вставьте остальной код вашего скрипта ниже, он не меняется ---

    public override bool CanAccept(CardController card)
    {
        if (_pileCanvasGroup != null && !_pileCanvasGroup.blocksRaycasts) return false;
        if (card == null) return false;
        if (cards.Count == 0) return true;
        return cards[cards.Count - 1].cardModel.rank == card.cardModel.rank + 1;
    }

    public override List<CardController> GetFaceUpSequenceFrom(int requestedIndex)
    {
        if (_pileCanvasGroup != null && !_pileCanvasGroup.blocksRaycasts) return new List<CardController>();
        if (requestedIndex < 0 || requestedIndex >= cards.Count) return new List<CardController>();

        int validStartIndex = cards.Count - 1;
        for (int i = cards.Count - 2; i >= 0; i--)
        {
            var curr = cards[i];
            var next = cards[i + 1];
            bool rankOk = curr.cardModel.rank == next.cardModel.rank + 1;
            bool suitOk = curr.cardModel.suit == next.cardModel.suit;
            bool faceUp = curr.GetComponent<CardData>().IsFaceUp();

            if (rankOk && suitOk && faceUp) validStartIndex = i;
            else break;
        }

        if (requestedIndex < validStartIndex)
        {
            List<CardController> blockers = new List<CardController>();
            for (int i = validStartIndex; i < cards.Count; i++) blockers.Add(cards[i]);
            if (SpiderEffectsService.Instance) SpiderEffectsService.Instance.Shake(blockers);
            return new List<CardController>();
        }

        List<CardController> sequence = new List<CardController>();
        for (int i = requestedIndex; i < cards.Count; i++) sequence.Add(cards[i]);
        return sequence;
    }

    public override void AddCard(CardController card, bool faceUp)
    {
        base.AddCard(card, faceUp);
        CheckSuit();
    }

    public override void AddCardsBatch(List<CardController> c, bool f)
    {
        base.AddCardsBatch(c, f);
        CheckSuit();
    }

    public void SetAnimatingCard(bool isAnimating)
    {
        if (_pileCanvasGroup != null) _pileCanvasGroup.blocksRaycasts = !isAnimating;
    }

    private void CheckSuit()
    {
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

        if (start != -1) StartCoroutine(MoveSequenceToFoundation(start));
    }

    private IEnumerator MoveSequenceToFoundation(int startIndex)
    {
        if (_pileCanvasGroup != null) _pileCanvasGroup.blocksRaycasts = false;
        if (spiderMode != null) spiderMode.ActiveFoundationAnimations++;
        if (spiderMode != null) spiderMode.OnRowCompleted();

        spiderMode.UpdateTableauLayouts();

        yield return new WaitForSeconds(0.5f);

        var foundation = spiderMode.GetNextEmptyFoundation();
        if (foundation == null)
        {
            if (_pileCanvasGroup != null) _pileCanvasGroup.blocksRaycasts = true;
            if (spiderMode != null) spiderMode.ActiveFoundationAnimations--;
            yield break;
        }

        List<CardController> sequence = RemoveSequenceFrom(startIndex);

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
        {
            spiderMode.undoManager.RecordMove(sequence, this, foundation, parents, positions, siblings, batchID);
        }

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
}