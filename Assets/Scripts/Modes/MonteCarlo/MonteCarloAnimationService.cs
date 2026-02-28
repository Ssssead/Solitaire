using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MonteCarloAnimationService : MonoBehaviour
{
    [Header("Movement Settings")]
    public RectTransform dragLayer;
    public float moveAnimDuration = 0.65f;
    public float arcHeight = 50f;

    [Header("Visual Settings")]
    [SerializeField] private Color dimmedColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("Selection Animation")]
    public float hoverScaleMult = 1.1f;
    public float hoverYOffset = 0f;
    public float transitionSpeed = 10f;
    public float swingAngle = 2.5f;
    public float swingSpeed = 3f;
    public float bobAmount = 5f;
    public float bobSpeed = 2f;

    private Dictionary<CardController, Coroutine> activeAnimations = new Dictionary<CardController, Coroutine>();
    private Dictionary<CardController, Coroutine> activeColorFades = new Dictionary<CardController, Coroutine>();

    private CardController heroCard1;
    private CardController heroCard2;

    // --- ”œ–¿¬À≈Õ»≈ “≈ÕﬂÃ» ---
    public void SetShadowFlying(CardController card, bool flying)
    {
        if (card == null) return;
        var sh = card.GetComponent<CardShadowController>();
        if (sh) sh.SetFlying(flying);
    }

    public void SetShadowVisible(CardController card, bool visible)
    {
        if (card == null) return;
        var sh = card.GetComponent<CardShadowController>();
        if (sh) sh.SetShadowVisible(visible);
    }
    // -------------------------

    public void SetHeroes(CardController c1, CardController c2 = null)
    {
        heroCard1 = c1;
        heroCard2 = c2;
        BringHeroesToFront();
    }

    private void BringHeroesToFront()
    {
        if (dragLayer == null) return;
        if (heroCard2 != null && heroCard2.transform.parent == dragLayer) heroCard2.transform.SetAsLastSibling();
        if (heroCard1 != null && heroCard1.transform.parent == dragLayer) heroCard1.transform.SetAsLastSibling();
    }

    public void HighlightSelectedAndDimOthers(CardController selectedCard, List<CardController> whiteList, CardController[] allBoardCards, List<Transform> slots, CardController previousCard = null)
    {
        ResetAllCardsVisuals(allBoardCards, slots, selectedCard, previousCard);

        if (selectedCard == null) return;

        if (activeColorFades.ContainsKey(selectedCard) && activeColorFades[selectedCard] != null)
        {
            StopCoroutine(activeColorFades[selectedCard]);
            activeColorFades[selectedCard] = null;
        }
        var data = selectedCard.GetComponent<CardData>();
        if (data && data.image) data.image.color = Color.white;

        if (dragLayer != null) selectedCard.transform.SetParent(dragLayer, true);

        SetHeroes(selectedCard, null);

        if (activeAnimations.ContainsKey(selectedCard) && activeAnimations[selectedCard] != null)
            StopCoroutine(activeAnimations[selectedCard]);

        SetShadowFlying(selectedCard, true);
        activeAnimations[selectedCard] = StartCoroutine(CardHoverRoutine(selectedCard));

        foreach (var card in allBoardCards)
        {
            if (card == null) continue;
            if (card != selectedCard && !whiteList.Contains(card)) DimCard(card, true);
        }
    }

    public void HighlightTwoCards(CardController c1, CardController c2)
    {
        if (dragLayer != null)
        {
            c1.transform.SetParent(dragLayer, true);
            c2.transform.SetParent(dragLayer, true);
        }

        SetHeroes(c1, c2);

        if (activeAnimations.ContainsKey(c1) && activeAnimations[c1] != null) StopCoroutine(activeAnimations[c1]);
        if (activeAnimations.ContainsKey(c2) && activeAnimations[c2] != null) StopCoroutine(activeAnimations[c2]);

        SetShadowFlying(c1, true);
        SetShadowFlying(c2, true);

        activeAnimations[c1] = StartCoroutine(CardHoverRoutine(c1));
        activeAnimations[c2] = StartCoroutine(CardHoverRoutine(c2));

        if (activeColorFades.ContainsKey(c1) && activeColorFades[c1] != null) StopCoroutine(activeColorFades[c1]);
        if (activeColorFades.ContainsKey(c2) && activeColorFades[c2] != null) StopCoroutine(activeColorFades[c2]);

        var data1 = c1.GetComponent<CardData>(); if (data1 && data1.image) data1.image.color = Color.white;
        var data2 = c2.GetComponent<CardData>(); if (data2 && data2.image) data2.image.color = Color.white;
    }

    public void ResetAllCardsVisuals(CardController[] allBoardCards, List<Transform> slots, params CardController[] skipCards)
    {
        List<CardController> skips = new List<CardController>();
        if (skipCards != null)
        {
            foreach (var c in skipCards) if (c != null) skips.Add(c);
        }

        for (int i = 0; i < allBoardCards.Length; i++)
        {
            var card = allBoardCards[i];
            if (card == null || skips.Contains(card)) continue;

            if (activeAnimations.ContainsKey(card) && activeAnimations[card] != null)
            {
                StopCoroutine(activeAnimations[card]);
                activeAnimations[card] = null;
            }

            if (activeColorFades.ContainsKey(card) && activeColorFades[card] != null)
            {
                StopCoroutine(activeColorFades[card]);
                activeColorFades[card] = null;
            }

            if (slots != null && slots.Count > i) card.transform.SetParent(slots[i], true);

            card.transform.localScale = Vector3.one;
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;

            SetShadowFlying(card, false);
            var data = card.GetComponent<CardData>();
            if (data && data.image) data.image.color = Color.white;
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

    private void DimCard(CardController card, bool dim)
    {
        var data = card.GetComponent<CardData>();
        if (data && data.image)
        {
            if (activeColorFades.ContainsKey(card) && activeColorFades[card] != null) StopCoroutine(activeColorFades[card]);
            activeColorFades[card] = StartCoroutine(ColorFadeRoutine(data.image, dim ? dimmedColor : Color.white, 0.2f));
        }
    }

    private IEnumerator ColorFadeRoutine(Image img, Color targetColor, float duration)
    {
        if (img == null) yield break;
        Color startColor = img.color; float elapsed = 0f;
        while (elapsed < duration)
        {
            if (img == null) yield break;
            elapsed += Time.deltaTime;
            img.color = Color.Lerp(startColor, targetColor, elapsed / duration);
            yield return null;
        }
        if (img != null) img.color = targetColor;
    }

    public IEnumerator SmoothReturnToSlot(CardController card, Transform slot, float duration)
    {
        if (card == null || slot == null) yield break;
        if (activeAnimations.ContainsKey(card) && activeAnimations[card] != null) StopCoroutine(activeAnimations[card]);

        SetShadowFlying(card, true);

        Vector3 startPos = card.transform.position;
        Vector3 startScale = card.transform.localScale;
        Quaternion startRot = card.transform.localRotation;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            card.transform.position = Vector3.Lerp(startPos, slot.position, t);
            card.transform.localScale = Vector3.Lerp(startScale, Vector3.one, t);
            card.transform.localRotation = Quaternion.Lerp(startRot, Quaternion.identity, t);
            yield return null;
        }

        card.transform.SetParent(slot, true);
        card.transform.position = slot.position;
        card.transform.localScale = Vector3.one;
        card.transform.localRotation = Quaternion.identity;

        SetShadowFlying(card, false);
    }

    public IEnumerator FlyToCard(CardController flyingCard, CardController targetCard, float duration)
    {
        if (flyingCard == null || targetCard == null) yield break;
        if (activeAnimations.ContainsKey(flyingCard) && activeAnimations[flyingCard] != null) StopCoroutine(activeAnimations[flyingCard]);

        if (dragLayer) flyingCard.transform.SetParent(dragLayer, true);
        BringHeroesToFront();

        SetShadowFlying(flyingCard, true);

        Vector3 startPos = flyingCard.transform.position;
        Vector3 startScale = flyingCard.transform.localScale;
        Quaternion startRot = flyingCard.transform.localRotation;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            flyingCard.transform.position = Vector3.Lerp(startPos, targetCard.transform.position, t);
            flyingCard.transform.localScale = Vector3.Lerp(startScale, Vector3.one, t);
            flyingCard.transform.localRotation = Quaternion.Lerp(startRot, Quaternion.identity, t);
            yield return null;
        }
        flyingCard.transform.position = targetCard.transform.position;
    }

    public IEnumerator AnimatePairToFoundationWithRotation(CardController c1, CardController c2, Transform target, Action onComplete)
    {
        if (c1.canvasGroup) c1.canvasGroup.interactable = false;
        if (c2.canvasGroup) c2.canvasGroup.interactable = false;

        if (dragLayer)
        {
            c1.transform.SetParent(dragLayer, true);
            c2.transform.SetParent(dragLayer, true);
        }

        SetHeroes(c1, c2);

        SetShadowVisible(c1, false);
        SetShadowVisible(c2, true);
        SetShadowFlying(c2, true);

        if (activeAnimations.ContainsKey(c1) && activeAnimations[c1] != null) StopCoroutine(activeAnimations[c1]);
        if (activeAnimations.ContainsKey(c2) && activeAnimations[c2] != null) StopCoroutine(activeAnimations[c2]);

        Coroutine r1 = StartCoroutine(LinearFlightWithRotation(c1, target.position, moveAnimDuration));
        Coroutine r2 = StartCoroutine(LinearFlightWithRotation(c2, target.position, moveAnimDuration));

        yield return r1;
        yield return r2;

        c1.transform.SetParent(target); c2.transform.SetParent(target);
        c1.transform.localRotation = Quaternion.identity; c2.transform.localRotation = Quaternion.identity;
        c1.transform.localScale = Vector3.one; c2.transform.localScale = Vector3.one;

        // --- »—œ–¿¬À≈Õ»≈: √‡‡ÌÚËÓ‚‡ÌÌ˚È Ò·ÓÒ ÎÂÚˇ˘ÂÈ ÚÂÌË ‰Îˇ Œ¡≈»’ Í‡Ú ---
        SetShadowFlying(c1, false);
        SetShadowFlying(c2, false);
        // ----------------------------------------------------------------------

        SetHeroes(null, null);
        onComplete?.Invoke();
    }

    private IEnumerator LinearFlightWithRotation(CardController card, Vector3 endPos, float duration)
    {
        Vector3 startPos = card.transform.position;
        float elapsed = 0f;

        float randomRotSpeed = UnityEngine.Random.Range(360f, 720f) * (UnityEngine.Random.value > 0.5f ? 1 : -1);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            card.transform.position = Vector3.Lerp(startPos, endPos, t);
            card.transform.Rotate(0, 0, randomRotSpeed * Time.deltaTime);
            card.transform.localScale = Vector3.Lerp(card.transform.localScale, Vector3.one, t);

            elapsed += Time.deltaTime;
            yield return null;
        }
        card.transform.position = endPos;
        card.transform.localRotation = Quaternion.identity;
    }

    public IEnumerator AnimateCardLinear(CardController card, Vector3 targetPos, float duration)
    {
        if (card == null) yield break;
        if (dragLayer) card.transform.SetParent(dragLayer, true);

        BringHeroesToFront();
        SetShadowFlying(card, true);

        Vector3 startPos = card.transform.position; float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            card.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        card.transform.position = targetPos;
        SetShadowFlying(card, false);
    }
}