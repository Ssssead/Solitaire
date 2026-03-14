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

    [Header("Selection Animation")]
    public float hoverScaleMult = 1.1f;
    public float hoverYOffset = 20f;
    public float transitionSpeed = 10f;
    public float swingAngle = 2.5f;
    public float swingSpeed = 3f;
    public float bobAmount = 5f;
    public float bobSpeed = 2f;

    private Dictionary<CardController, Coroutine> activeHoverRoutines = new Dictionary<CardController, Coroutine>();

    private void Start()
    {
        if (dragLayerRect != null) dragLayerRect.SetAsLastSibling();
    }

    private void SetCardShadowState(CardController card, PyramidShadowController.ShadowState state)
    {
        if (card == null) return;

        var shadowCtrl = card.GetComponent<PyramidShadowController>();
        if (shadowCtrl == null)
        {
            shadowCtrl = card.gameObject.AddComponent<PyramidShadowController>();
        }
        shadowCtrl.SetState(state);

        // --- НОВОЕ: Если карта летит или выделена, жестко включаем ей тень ---
        if (state != PyramidShadowController.ShadowState.Resting)
        {
            var shadow = card.GetComponent<UnityEngine.UI.Shadow>();
            if (shadow != null) shadow.enabled = true;
        }
    }

    public void SelectCard(CardController card)
    {
        if (card == null) return;

        if (dragLayerRect) card.transform.SetParent(dragLayerRect, true);
        card.transform.SetAsLastSibling();

        SetCardShadowState(card, PyramidShadowController.ShadowState.Selected);

        if (activeHoverRoutines.TryGetValue(card, out Coroutine r) && r != null)
            StopCoroutine(r);

        activeHoverRoutines[card] = StartCoroutine(CardHoverRoutine(card));
    }

    public void DeselectCard(CardController card, Transform originalSlot, Vector3 targetWorldPos, bool immediate = false)
    {
        if (card == null) return;

        if (activeHoverRoutines.TryGetValue(card, out Coroutine r) && r != null)
        {
            StopCoroutine(r);
            activeHoverRoutines.Remove(card);
        }

        if (immediate)
        {
            card.transform.SetParent(originalSlot, true);
            card.transform.position = targetWorldPos;
            card.transform.localScale = Vector3.one;
            card.transform.localRotation = Quaternion.identity;

            SetCardShadowState(card, PyramidShadowController.ShadowState.Resting);

            if (originalSlot == deckManager.wasteRoot || originalSlot == deckManager.stockRoot)
                card.transform.SetAsLastSibling();

            // --- НОВОЕ: Обновляем стопки, чтобы они спрятали тень, если нужно ---
            if (deckManager != null && deckManager.pileManager != null)
                deckManager.pileManager.UpdateLocks();
        }
        else
        {
            StartCoroutine(SmoothReturnToSlot(card, originalSlot, targetWorldPos, 0.2f));
        }
    }

    private IEnumerator CardHoverRoutine(CardController card)
    {
        Transform t = card.transform;
        Vector3 baseScale = Vector3.one;
        Vector3 basePos = t.localPosition;
        Quaternion baseRot = Quaternion.identity;

        Vector3 targetHoverScale = baseScale * hoverScaleMult;
        Vector3 targetHoverPos = basePos + new Vector3(0, hoverYOffset, 0);

        float progress = 0f;
        Vector3 startScale = t.localScale;
        Vector3 startPos = t.localPosition;
        Quaternion startRot = t.localRotation;

        while (progress < 1f)
        {
            progress += Time.deltaTime * transitionSpeed;
            if (progress > 1f) progress = 1f;

            t.localScale = Vector3.Lerp(startScale, targetHoverScale, progress);
            t.localPosition = Vector3.Lerp(startPos, targetHoverPos, progress);
            t.localRotation = Quaternion.Lerp(startRot, baseRot, progress);
            yield return null;
        }

        float swingTimer = 0f; float bobTimer = 0f;
        while (true)
        {
            swingTimer += Time.deltaTime * swingSpeed;
            bobTimer += Time.deltaTime * bobSpeed;

            t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(swingTimer) * swingAngle);
            t.localPosition = targetHoverPos + new Vector3(0, Mathf.Sin(bobTimer) * bobAmount, 0);
            yield return null;
        }
    }

    public IEnumerator SmoothReturnToSlot(CardController card, Transform slot, Vector3 targetWorldPos, float duration)
    {
        if (card == null || slot == null) yield break;

        SetCardShadowState(card, PyramidShadowController.ShadowState.Flying);

        Vector3 startPos = card.transform.position;
        Vector3 startScale = card.transform.localScale;
        Quaternion startRot = card.transform.localRotation;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (card == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            card.transform.position = Vector3.Lerp(startPos, targetWorldPos, t);
            card.transform.localScale = Vector3.Lerp(startScale, Vector3.one, t);
            card.transform.localRotation = Quaternion.Lerp(startRot, Quaternion.identity, t);
            yield return null;
        }

        if (card != null)
        {
            card.transform.SetParent(slot, true);
            card.transform.position = targetWorldPos;
            card.transform.localScale = Vector3.one;
            card.transform.localRotation = Quaternion.identity;

            SetCardShadowState(card, PyramidShadowController.ShadowState.Resting);

            if (slot == deckManager.wasteRoot || slot == deckManager.stockRoot)
                card.transform.SetAsLastSibling();

            // --- НОВОЕ: Обновляем стопки, чтобы спрятали тени ---
            if (deckManager != null && deckManager.pileManager != null)
                deckManager.pileManager.UpdateLocks();
        }
    }

    public IEnumerator PlayIntroDeckArrival(Transform stockRoot, System.Func<bool> isSkippingFunc = null)
    {
        float stackGap = 4f;
        if (deckManager != null && deckManager.pileManager != null && deckManager.pileManager.Stock != null)
            stackGap = deckManager.pileManager.Stock.stackGap;

        List<Transform> cards = new List<Transform>();
        List<Vector3> targetLocalPos = new List<Vector3>();
        List<CardController> allCards = new List<CardController>();

        foreach (Transform child in stockRoot)
        {
            CardController cc = child.GetComponent<CardController>();
            if (cc != null) allCards.Add(cc);
        }

        foreach (var cc in allCards) { if (deckManager.pileManager.Stock.HasCard(cc)) cc.transform.SetAsLastSibling(); }
        foreach (var cc in allCards) { if (!deckManager.pileManager.Stock.HasCard(cc)) cc.transform.SetAsLastSibling(); }

        int stockIndex = 0;
        int tableauIndex = deckManager.pileManager.Stock.Count;

        List<CardController> stockList = new List<CardController>();
        List<CardController> tableauList = new List<CardController>();

        foreach (var cc in allCards)
        {
            if (deckManager.pileManager.Stock.HasCard(cc)) stockList.Add(cc);
            else tableauList.Add(cc);
        }

        tableauList.Reverse();

        List<CardController> orderedCards = new List<CardController>();
        orderedCards.AddRange(stockList);
        orderedCards.AddRange(tableauList);

        for (int i = 0; i < orderedCards.Count; i++)
        {
            var cc = orderedCards[i];
            cc.transform.SetAsLastSibling();

            // --- НОВОЕ: Тень только у самой нижней карты в полете ---
            var shadow = cc.GetComponent<UnityEngine.UI.Shadow>();
            if (shadow != null) shadow.enabled = (i == 0);

            Vector3 finalPos = new Vector3(i * stackGap, 0, 0);
            targetLocalPos.Add(finalPos);
            cards.Add(cc.transform);

            cc.transform.localPosition = finalPos + Vector3.down * 1500f;
        }

        float duration = 0.8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float speed = (isSkippingFunc != null && isSkippingFunc()) ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3);

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    Vector3 startPos = targetLocalPos[i] + Vector3.down * 1500f;
                    cards[i].localPosition = Vector3.Lerp(startPos, targetLocalPos[i], t);
                }
            }
            yield return null;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null) cards[i].localPosition = targetLocalPos[i];
        }
    }

    public IEnumerator PlayNewRoundEntry(Transform stockRoot)
    {
        float stackGap = 4f;
        if (deckManager != null && deckManager.pileManager != null && deckManager.pileManager.Stock != null)
            stackGap = deckManager.pileManager.Stock.stackGap;

        List<Transform> cards = new List<Transform>();
        List<Vector3> targetLocalPos = new List<Vector3>();
        List<CardController> allCards = new List<CardController>();

        foreach (Transform child in stockRoot)
        {
            CardController cc = child.GetComponent<CardController>();
            if (cc != null) allCards.Add(cc);
        }

        List<CardController> stockList = new List<CardController>();
        List<CardController> tableauList = new List<CardController>();

        foreach (var cc in allCards)
        {
            if (deckManager.pileManager.Stock.HasCard(cc)) stockList.Add(cc);
            else tableauList.Add(cc);
        }

        tableauList.Reverse();

        List<CardController> orderedCards = new List<CardController>();
        orderedCards.AddRange(stockList);
        orderedCards.AddRange(tableauList);

        for (int i = 0; i < orderedCards.Count; i++)
        {
            var cc = orderedCards[i];
            cc.transform.SetAsLastSibling();

            // --- НОВОЕ: Тень только у нижней карты ---
            var shadow = cc.GetComponent<UnityEngine.UI.Shadow>();
            if (shadow != null) shadow.enabled = (i == 0);

            Vector3 finalPos = new Vector3(i * stackGap, 0, 0);
            targetLocalPos.Add(finalPos);
            cards.Add(cc.transform);

            cc.transform.localPosition = finalPos + Vector3.left * 1500f;
        }

        float duration = 0.8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3);

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    Vector3 startPos = targetLocalPos[i] + Vector3.left * 1500f;
                    cards[i].localPosition = Vector3.Lerp(startPos, targetLocalPos[i], t);
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null) cards[i].localPosition = targetLocalPos[i];
        }
    }

    public IEnumerator PlayDealAnimation(List<CardController> tableauCards, System.Func<bool> isSkippingFunc = null)
    {
        float delayPerCard = dealTotalDuration / Mathf.Max(1, tableauCards.Count);
        foreach (var card in tableauCards)
        {
            StartCoroutine(MoveCardToHome(card, singleCardMoveDuration, isSkippingFunc));

            float waitElapsed = 0f;
            while (waitElapsed < delayPerCard)
            {
                float speed = (isSkippingFunc != null && isSkippingFunc()) ? 15f : 1f;
                waitElapsed += Time.deltaTime * speed;
                yield return null;
            }
        }

        float finalWait = 0f;
        while (finalWait < singleCardMoveDuration)
        {
            float speed = (isSkippingFunc != null && isSkippingFunc()) ? 15f : 1f;
            finalWait += Time.deltaTime * speed;
            yield return null;
        }

        if (deckManager != null && deckManager.pileManager != null)
            deckManager.pileManager.UpdateLocks();
    }

    private IEnumerator MoveCardToHome(CardController card, float duration, System.Func<bool> isSkippingFunc = null)
    {
        if (card == null) yield break;
        PrepareCardForFlight(card);

        var info = card.GetComponent<CardInfoStorage>();
        Transform targetSlot = info != null ? info.LinkedSlot : deckManager.wasteRoot;

        yield return StartCoroutine(LerpRoutine(card, targetSlot.position, duration, isSkippingFunc));

        if (card != null)
        {
            card.transform.SetParent(targetSlot);
            card.transform.localPosition = Vector3.zero;
            card.transform.localScale = Vector3.one;
            card.transform.localRotation = Quaternion.identity;

            SetCardShadowState(card, PyramidShadowController.ShadowState.Resting);
        }
    }

    public IEnumerator MoveCardLinear(CardController card, Vector3 targetWorldPos, float duration, System.Action onComplete = null)
    {
        if (card == null) yield break;
        PrepareCardForFlight(card);

        yield return StartCoroutine(LerpRoutine(card, targetWorldPos, duration));

        if (card != null)
        {
            card.transform.position = targetWorldPos;
            card.transform.localScale = Vector3.one;
            SetCardShadowState(card, PyramidShadowController.ShadowState.Resting);
        }
        onComplete?.Invoke();
    }

    public IEnumerator MoveCardToStockAndDisable(CardController card, Vector3 targetPos, float duration)
    {
        if (card == null) yield break;
        PrepareCardForFlight(card);

        yield return StartCoroutine(LerpRoutine(card, targetPos, duration));

        if (card != null)
        {
            if (deckManager != null && deckManager.stockRoot != null)
            {
                card.transform.SetParent(deckManager.stockRoot, true);
            }
            card.transform.position = targetPos;
            SetCardShadowState(card, PyramidShadowController.ShadowState.Resting);
        }
    }

    public IEnumerator AnimateRemoveBallistic(CardController card, Transform target, System.Action onComplete = null)
    {
        if (card == null) yield break;

        if (activeHoverRoutines.TryGetValue(card, out Coroutine r) && r != null)
        {
            StopCoroutine(r);
            activeHoverRoutines.Remove(card);
        }

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

        if (card != null)
        {
            card.transform.position = endPos;
            card.transform.SetParent(target);
            SetCardShadowState(card, PyramidShadowController.ShadowState.Resting);
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
            card.transform.SetParent(finalParent, true);
            card.transform.position = endPos;

            SetCardShadowState(card, PyramidShadowController.ShadowState.Resting);

            if (finalParent == deckManager.wasteRoot || finalParent == deckManager.stockRoot)
                card.transform.SetAsLastSibling();
            if (card.canvasGroup) card.canvasGroup.interactable = true;
        }
    }

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

        SetCardShadowState(card, PyramidShadowController.ShadowState.Flying);
    }

    private IEnumerator LerpRoutine(CardController card, Vector3 targetWorldPos, float duration, System.Func<bool> isSkippingFunc = null)
    {
        Vector3 startPos = card.transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (card == null) yield break;
            float speed = (isSkippingFunc != null && isSkippingFunc()) ? 15f : 1f;
            float t = elapsed / duration;
            card.transform.position = Vector3.Lerp(startPos, targetWorldPos, t);
            elapsed += Time.deltaTime * speed;
            yield return null;
        }
        if (card != null) card.transform.position = targetWorldPos;
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