using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PyramidAnimationManager : MonoBehaviour
{
    [Header("References")]
    public PyramidDeckManager deckManager;
    public RectTransform dragLayerRect;

    [Header("Timings")]
    [SerializeField] private float dealTotalDuration = 1.2f;
    [SerializeField] private float singleCardMoveDuration = 0.3f;
    [SerializeField] private float removeAnimDuration = 0.5f;

    [Header("Ballistic Settings")]
    public float arcHeight = 50f;
    public float startRotationSpeed = 540f;
    public float rotationAcceleration = 720f;

    private void Start()
    {
        if (dragLayerRect != null) dragLayerRect.SetAsLastSibling();
    }

    // --- 1. STARTER DEAL ---
    public IEnumerator PlayDealAnimation(List<CardController> tableauCards)
    {
        float delayPerCard = dealTotalDuration / Mathf.Max(1, tableauCards.Count);
        foreach (var card in tableauCards)
        {
            StartCoroutine(MoveCardToHome(card, singleCardMoveDuration));
            yield return new WaitForSeconds(delayPerCard);
        }
        yield return new WaitForSeconds(singleCardMoveDuration);
        if (deckManager != null && deckManager.pileManager != null)
            deckManager.pileManager.UpdateLocks();
    }

    private IEnumerator MoveCardToHome(CardController card, float duration)
    {
        if (card == null) yield break;

        // Подготовка
        card.transform.SetParent(dragLayerRect, true);
        card.transform.localScale = Vector3.one;

        var data = card.GetComponent<CardData>();
        if (data)
        {
            data.SetFaceUp(true);
            if (data.image) data.image.color = Color.white;
        }

        var info = card.GetComponent<CardInfoStorage>();
        Transform targetSlot = info != null ? info.LinkedSlot : deckManager.wasteRoot;

        Vector3 startPos = card.transform.position;
        Vector3 targetPos = targetSlot.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            card.transform.position = Vector3.Lerp(startPos, targetPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (card != null)
        {
            card.transform.position = targetSlot.position;
            card.transform.SetParent(targetSlot);
            card.transform.localPosition = Vector3.zero;
            card.transform.localScale = Vector3.one;
            card.transform.localRotation = Quaternion.identity;
        }
    }

    // --- 2. MOVEMENT (Deal / Undo / Recycle) ---
    public IEnumerator MoveCardLinear(CardController card, Vector3 targetWorldPos, float duration, System.Action onComplete = null)
    {
        if (card == null) yield break;

        // ВАЖНО: Включаем карту и выносим на DragLayer
        card.gameObject.SetActive(true);
        card.transform.SetParent(dragLayerRect, true);
        card.transform.localScale = Vector3.one;
        card.transform.localRotation = Quaternion.identity;

        var data = card.GetComponent<CardData>();
        if (data)
        {
            data.SetFaceUp(true);
            if (data.image) data.image.color = Color.white;
        }

        Vector3 startPos = card.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (card == null) yield break;
            float t = elapsed / duration;
            card.transform.position = Vector3.Lerp(startPos, targetWorldPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (card != null)
        {
            card.transform.position = targetWorldPos;
            card.transform.localScale = Vector3.one;
        }

        onComplete?.Invoke();
    }

    public IEnumerator MoveCardToStockAndDisable(CardController card, Vector3 targetPos, float duration)
    {
        card.transform.SetParent(dragLayerRect, true);
        card.transform.localScale = Vector3.one;

        var data = card.GetComponent<CardData>();
        if (data)
        {
            data.SetFaceUp(true);
            if (data.image) data.image.color = Color.white;
        }

        float elapsed = 0f;
        Vector3 startPos = card.transform.position;

        while (elapsed < duration)
        {
            card.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        card.transform.position = targetPos;
        if (deckManager != null && deckManager.stockRoot != null)
        {
            card.transform.SetParent(deckManager.stockRoot);
            card.transform.localPosition = Vector3.zero;
        }
    }

    public IEnumerator AnimateRemoveBallistic(CardController card, Transform target, System.Action onComplete = null)
    {
        if (card == null) yield break;

        card.transform.SetParent(dragLayerRect, true);
        if (card.canvasGroup) card.canvasGroup.interactable = false;
        card.transform.localScale = Vector3.one;

        Vector3 startPos = card.transform.position;
        Vector3 endPos = target.position;
        Vector3 midPoint = (startPos + endPos) / 2f + Vector3.up * arcHeight;

        float elapsed = 0f; float curRot = startRotationSpeed;

        while (elapsed < removeAnimDuration)
        {
            if (card == null) yield break;
            float t = elapsed / removeAnimDuration;
            Vector3 pos = Mathf.Pow(1 - t, 2) * startPos + 2 * (1 - t) * t * midPoint + Mathf.Pow(t, 2) * endPos;
            card.transform.position = pos;
            curRot += rotationAcceleration * Time.deltaTime;
            card.transform.Rotate(0, 0, -curRot * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        onComplete?.Invoke();
    }

    public IEnumerator ReturnCardFromFoundation(CardController card, Vector3 startPos, Vector3 endPos, Transform finalParent, float duration)
    {
        card.gameObject.SetActive(true);
        card.transform.SetParent(dragLayerRect, true);
        card.transform.position = startPos;
        card.transform.rotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;

        var data = card.GetComponent<CardData>();
        if (data && data.image) data.image.color = Color.white;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            card.transform.position = Vector3.Lerp(startPos, endPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        card.transform.position = endPos;

        if (finalParent != null)
        {
            card.transform.SetParent(finalParent);
            card.transform.localPosition = Vector3.zero;
            if (finalParent == deckManager.wasteRoot || finalParent == deckManager.stockRoot)
                card.transform.SetAsLastSibling();
            if (card.canvasGroup) card.canvasGroup.interactable = true;
        }
    }

    public IEnumerator PlayNewRoundEntry(Transform stockRoot)
    {
        Vector3 originalPos = stockRoot.position;
        Vector3 startPos = originalPos - Vector3.right * 1500f;
        stockRoot.position = startPos;
        float duration = 0.8f; float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration; t = 1f - Mathf.Pow(1f - t, 3);
            stockRoot.position = Vector3.Lerp(startPos, originalPos, t);
            elapsed += Time.deltaTime; yield return null;
        }
        stockRoot.position = originalPos;
    }

    public IEnumerator ClearRemainingCards(List<CardController> cards, Transform target, float duration = 0.5f)
    {
        foreach (var c in cards)
        {
            if (c == null) continue;
            StartCoroutine(AnimateRemoveBallistic(c, target, () => { if (c) { c.gameObject.SetActive(false); Destroy(c.gameObject); } }));
            yield return new WaitForSeconds(0.05f);
        }
        yield return new WaitForSeconds(duration);
    }
}
public class CardInfoStorage : MonoBehaviour
{
    public Transform LinkedSlot;
}