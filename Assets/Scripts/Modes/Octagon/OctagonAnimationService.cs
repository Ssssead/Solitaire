using System;
using System.Collections;
using UnityEngine;

public class OctagonAnimationService : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public IEnumerator AnimateMoveCard(
        CardController card,
        Transform targetTransform,
        Vector3 targetLocalPos,
        float duration,
        bool targetFaceUp,
        Action onComplete)
    {
        if (card == null) yield break;

        Canvas canvas = card.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            card.transform.SetParent(canvas.transform, true);
        }
        card.transform.SetAsLastSibling();

        Vector3 startPos = card.transform.position;
        Quaternion startRot = card.transform.rotation;

        var data = card.GetComponent<CardData>();
        bool needFlip = (data != null && data.IsFaceUp() != targetFaceUp);
        bool flippedHalfway = false;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (card == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = moveCurve.Evaluate(t);

            if (targetTransform != null)
            {
                Vector3 targetWorldPos = targetTransform.TransformPoint(targetLocalPos);
                card.transform.position = Vector3.Lerp(startPos, targetWorldPos, curvedT);
            }
            card.transform.rotation = Quaternion.Lerp(startRot, Quaternion.identity, curvedT);

            if (needFlip)
            {
                float scaleX = 1f;
                if (t < 0.5f) scaleX = 1f - (t * 2f);
                else
                {
                    if (!flippedHalfway) { data.SetFaceUp(targetFaceUp, false); flippedHalfway = true; }
                    scaleX = (t - 0.5f) * 2f;
                }
                float popScale = 1.0f + (Mathf.Sin(t * Mathf.PI) * 0.1f);
                card.transform.localScale = new Vector3(scaleX * popScale, popScale, 1f);
            }
            else
            {
                card.transform.localScale = Vector3.one;
            }
            yield return null;
        }

        if (card != null)
        {
            if (targetTransform != null)
            {
                card.transform.SetParent(targetTransform);
                card.transform.localPosition = targetLocalPos;
            }
            card.transform.rotation = Quaternion.identity;
            card.transform.localScale = Vector3.one;
            if (!needFlip && data != null && data.IsFaceUp() != targetFaceUp) data.SetFaceUp(targetFaceUp, false);

            var cg = card.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;

            onComplete?.Invoke();
        }
    }

    // --- ќЅЌќ¬Ћ≈ЌЌјя “–я— ј ---
    public IEnumerator AnimateShake(CardController card)
    {
        if (card == null) yield break;

        Vector3 originalPos = card.rectTransform.anchoredPosition;

        // ¬ернули быстрый тайминг (как было)
        float duration = 0.25f;

        // ”меньшили силу тр€ски (было 10f, стало 4f) - "менее интенсивно"
        float magnitude = 4f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;

            // „астота 8 (быстра€ вибраци€)
            float offset = Mathf.Sin(percent * Mathf.PI * 8) * magnitude * (1f - percent);

            card.rectTransform.anchoredPosition = originalPos + new Vector3(offset, 0, 0);
            yield return null;
        }

        card.rectTransform.anchoredPosition = originalPos;
    }
}