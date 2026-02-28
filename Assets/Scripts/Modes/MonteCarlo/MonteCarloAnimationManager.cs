using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonteCarloAnimationManager : MonoBehaviour
{
    public RectTransform dragLayer;
    public float moveAnimDuration = 0.25f;
    public float arcHeight = 50f;

    // Перелет пары в Foundation
    public IEnumerator AnimatePairToFoundation(CardController c1, CardController c2, Transform target, Action onComplete)
    {
        if (c1.canvasGroup) c1.canvasGroup.interactable = false;
        if (c2.canvasGroup) c2.canvasGroup.interactable = false;

        c1.transform.SetParent(dragLayer, true);
        c2.transform.SetParent(dragLayer, true);

        Coroutine r1 = StartCoroutine(ArcFlight(c1, target.position, moveAnimDuration));
        Coroutine r2 = StartCoroutine(ArcFlight(c2, target.position, moveAnimDuration));

        yield return r1;
        yield return r2;

        c1.transform.SetParent(target);
        c2.transform.SetParent(target);

        onComplete?.Invoke();
    }

    // Линейный полет для сдвига (Consolidate)
    public IEnumerator AnimateCardLinear(CardController card, Vector3 targetPos, float duration)
    {
        if (card == null) yield break;
        card.transform.SetParent(dragLayer, true);

        Vector3 startPos = card.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            card.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        card.transform.position = targetPos;
    }

    private IEnumerator ArcFlight(CardController card, Vector3 endPos, float duration)
    {
        Vector3 startPos = card.transform.position;
        Vector3 midPoint = (startPos + endPos) / 2f + Vector3.up * arcHeight;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Кривая Безье для дуги
            Vector3 pos = Mathf.Pow(1 - t, 2) * startPos + 2 * (1 - t) * t * midPoint + Mathf.Pow(t, 2) * endPos;
            card.transform.position = pos;
            elapsed += Time.deltaTime;
            yield return null;
        }
        card.transform.position = endPos;
    }
}