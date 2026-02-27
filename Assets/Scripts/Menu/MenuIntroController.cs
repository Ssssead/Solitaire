using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MenuIntroController : MonoBehaviour
{
    [Header("References")]
    public List<RectTransform> cardObjects;
    public List<RectTransform> topUiElements;

    [Header("Dependencies")]
    public CardAnimationController cardAnimator;

    [Header("Dealing Settings")]
    public float cardFlyDuration = 0.8f;
    public float delayBetweenCards = 0.05f;
    public float startXOffset = -1200f;

    [Header("Stack Appearance")]
    [Range(0, 50f)] public float messyPositionJitter = 15f;
    public float startRotation = 45f;
    [Range(0, 5f)] public float finalRandomRotation = 3f;

    [Header("Animation Curves")]
    public AnimationCurve cardFlyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve uiDropCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private List<Vector2> cardsFinalPositions = new List<Vector2>();
    private List<Vector2> topUiFinalPositions = new List<Vector2>();

    private void Awake()
    {
        foreach (var card in cardObjects)
        {
            if (card != null)
            {
                var hover = card.GetComponent<CardHoverEffect>();
                if (hover != null) hover.SetHoverEnabled(false);
                cardsFinalPositions.Add(card.anchoredPosition);
            }
        }
        foreach (var ui in topUiElements)
            if (ui != null) topUiFinalPositions.Add(ui.anchoredPosition);
        PrepareStacks();
    }

    private void Start()
    {
        StartCoroutine(IntroSequenceRoutine());
    }

    private void PrepareStacks()
    {
        foreach (var ui in topUiElements)
            if (ui != null) ui.anchoredPosition += new Vector2(0, 300f);

        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] == null) continue;
            float stackY = (i < 5) ? cardsFinalPositions[0].y : cardsFinalPositions[5].y;
            Vector2 stackPos = new Vector2(startXOffset, stackY);
            cardObjects[i].anchoredPosition = stackPos + new Vector2(Random.Range(-messyPositionJitter, messyPositionJitter), Random.Range(-messyPositionJitter, messyPositionJitter));
            cardObjects[i].localRotation = Quaternion.Euler(0, 0, startRotation + Random.Range(-10f, 10f));
        }
    }

    private IEnumerator IntroSequenceRoutine()
    {
        yield return new WaitForSeconds(0.3f);
        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] == null) continue;
            Quaternion targetRot = Quaternion.Euler(0, 0, Random.Range(-finalRandomRotation, finalRandomRotation));
            StartCoroutine(AnimateCard(cardObjects[i], cardsFinalPositions[i], targetRot, cardFlyDuration));
            yield return new WaitForSeconds(delayBetweenCards);
        }
        yield return new WaitForSeconds(cardFlyDuration);

        // --- НОВОЕ: Принудительное обновление всех карт ПОСЛЕ полета ---
        if (cardAnimator != null)
        {
            cardAnimator.RefreshAllCards();
        }

        foreach (var ui in topUiElements)
        {
            if (ui != null) StartCoroutine(AnimateUi(ui, topUiFinalPositions[topUiElements.IndexOf(ui)], 0.5f));
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(0.5f);
        EnableHoverEffects();
    }

    private void EnableHoverEffects()
    {
        foreach (var card in cardObjects)
            if (card != null)
            {
                var hover = card.GetComponent<CardHoverEffect>();
                if (hover != null) hover.SetHoverEnabled(true);
            }
    }

    private IEnumerator AnimateCard(RectTransform target, Vector2 destPos, Quaternion destRot, float duration)
    {
        Vector2 startPos = target.anchoredPosition;
        Quaternion startRot = target.localRotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = cardFlyCurve.Evaluate(elapsed / duration);
            target.anchoredPosition = Vector2.LerpUnclamped(startPos, destPos, t);
            target.localRotation = Quaternion.Lerp(startRot, destRot, t);
            yield return null;
        }
        target.anchoredPosition = destPos;
        target.localRotation = destRot;
    }

    private IEnumerator AnimateUi(RectTransform target, Vector2 destPos, float duration)
    {
        Vector2 startPos = target.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.anchoredPosition = Vector2.Lerp(startPos, destPos, uiDropCurve.Evaluate(elapsed / duration));
            yield return null;
        }
        target.anchoredPosition = destPos;
    }
}