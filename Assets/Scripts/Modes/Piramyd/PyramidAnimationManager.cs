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

    // --- 1. СТАРТОВАЯ РАЗДАЧА (Для нового PyramidModeManager, если понадобится) ---
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
        PrepareCardForFlight(card);

        var info = card.GetComponent<CardInfoStorage>();
        Transform targetSlot = info != null ? info.LinkedSlot : deckManager.wasteRoot;

        yield return StartCoroutine(LerpRoutine(card, targetSlot.position, duration));

        if (card != null)
        {
            card.transform.SetParent(targetSlot);
            card.transform.localPosition = Vector3.zero;
            card.transform.localScale = Vector3.one;
            card.transform.localRotation = Quaternion.identity;
        }
    }

    // --- 2. МЕТОДЫ ДЛЯ СТАРОГО КОДА (MoveCardAnimation) ---
    // Это тот самый метод, который ищет ваш PyramidModeManager
    public IEnumerator MoveCardAnimation(CardController card, Vector3 targetWorldPos, float duration)
    {
        if (card == null) yield break;
        PrepareCardForFlight(card);

        yield return StartCoroutine(LerpRoutine(card, targetWorldPos, duration));

        if (card != null)
        {
            card.transform.position = targetWorldPos;
            card.transform.localScale = Vector3.one;
        }
    }

    // --- 3. МЕТОДЫ ДЛЯ СТАРОГО КОДА (AnimateCardBallistic - перегрузка) ---
    // Старый код вызывает этот метод без onComplete
    public IEnumerator AnimateCardBallistic(CardController card, Transform foundation, float duration)
    {
        yield return StartCoroutine(AnimateRemoveBallistic(card, foundation, null));
    }

    // --- 4. МЕТОДЫ ДЛЯ НОВОГО КОДА (MoveCardLinear, MoveCardToStockAndDisable) ---

    public IEnumerator MoveCardLinear(CardController card, Vector3 targetWorldPos, float duration, System.Action onComplete = null)
    {
        yield return StartCoroutine(MoveCardAnimation(card, targetWorldPos, duration));
        onComplete?.Invoke();
    }

    public IEnumerator MoveCardToStockAndDisable(CardController card, Vector3 targetPos, float duration)
    {
        if (card == null) yield break;
        PrepareCardForFlight(card);

        yield return StartCoroutine(LerpRoutine(card, targetPos, duration));

        if (card != null)
        {
            card.transform.position = targetPos;
            if (deckManager != null && deckManager.stockRoot != null)
            {
                card.transform.SetParent(deckManager.stockRoot);
                card.transform.localPosition = Vector3.zero;
            }
        }
    }

    // --- 5. CORE LOGIC ---

    public IEnumerator AnimateRemoveBallistic(CardController card, Transform target, System.Action onComplete = null)
    {
        if (card == null) yield break;

        PrepareCardForFlight(card);
        if (card.canvasGroup) card.canvasGroup.interactable = false;

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

        // ВАЖНО: Старый код ожидает, что карта будет скрыта в конце
        if (card != null)
        {
            card.transform.position = endPos;
            card.transform.SetParent(target);
            card.gameObject.SetActive(false);
        }

        onComplete?.Invoke();
    }

    public IEnumerator ReturnCardFromFoundation(CardController card, Vector3 startPos, Vector3 endPos, Transform finalParent, float duration)
    {
        card.gameObject.SetActive(true);
        PrepareCardForFlight(card);
        card.transform.position = startPos;

        yield return StartCoroutine(LerpRoutine(card, endPos, duration));

        if (card != null && finalParent != null)
        {
            card.transform.SetParent(finalParent);
            card.transform.localPosition = Vector3.zero;
            if (finalParent == deckManager.wasteRoot || finalParent == deckManager.stockRoot)
                card.transform.SetAsLastSibling();
            if (card.canvasGroup) card.canvasGroup.interactable = true;
        }
    }

    // --- Helpers ---
    private void PrepareCardForFlight(CardController card)
    {
        card.transform.SetParent(dragLayerRect, true);
        card.transform.localScale = Vector3.one;
        card.transform.localRotation = Quaternion.identity;

        var data = card.GetComponent<CardData>();
        if (data)
        {
            data.SetFaceUp(true);
            if (data.image) data.image.color = Color.white;
        }
    }

    private IEnumerator LerpRoutine(CardController card, Vector3 targetWorldPos, float duration)
    {
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
        if (card != null) card.transform.position = targetWorldPos;
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