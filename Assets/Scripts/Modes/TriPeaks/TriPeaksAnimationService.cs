using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TriPeaksAnimationService : MonoBehaviour
{
    [Header("Settings")]
    public float stockGap = 5f;

    // Анимация перемещения карты (с 2D-эффектом переворота во время полета)
    public IEnumerator AnimateMoveCard(CardController card, Transform targetTransform, float duration, bool targetFaceUp, System.Action onComplete)
    {
        Canvas canvas = card.GetComponentInParent<Canvas>();
        if (canvas != null) card.transform.SetParent(canvas.transform);
        else card.transform.SetParent(card.transform.root);

        card.transform.SetAsLastSibling();

        Vector3 startPos = card.transform.position;
        Vector3 endPos = targetTransform.position;

        var cardData = card.GetComponent<CardData>();
        bool startFaceUp = cardData != null && cardData.IsFaceUp();
        bool needsFlip = (startFaceUp != targetFaceUp);
        bool flipTriggered = false;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tMove = Mathf.SmoothStep(0f, 1f, t); // Плавное движение

            card.transform.position = Vector3.Lerp(startPos, endPos, tMove);

            // Если нужно перевернуть ВО ВРЕМЯ полета, делаем это вручную
            if (needsFlip)
            {
                float scaleX = Mathf.Abs(2f * t - 1f);
                card.transform.localScale = new Vector3(scaleX, 1f, 1f);

                if (t >= 0.5f && !flipTriggered && cardData != null)
                {
                    cardData.SetFaceUp(targetFaceUp, true); // Мгновенно меняем спрайт
                    flipTriggered = true;
                }
            }
            else
            {
                // Если переворота нет, просто держим масштаб
                card.transform.localScale = Vector3.one;
            }

            yield return null;
        }

        card.transform.position = endPos;
        card.transform.SetParent(targetTransform);
        card.transform.localPosition = Vector3.zero;

        // Восстанавливаем скейл и поворот
        card.transform.localScale = Vector3.one;
        card.transform.localRotation = Quaternion.identity;

        if (cardData != null) cardData.SetFaceUp(targetFaceUp, true);

        onComplete?.Invoke();
    }

    public IEnumerator AnimateStockShift(TriPeaksStockPile stockPile, float duration)
    {
        List<Transform> cardsInStock = new List<Transform>();
        foreach (Transform child in stockPile.transform)
        {
            if (child.gameObject.activeSelf) cardsInStock.Add(child);
        }

        int count = cardsInStock.Count;
        if (count == 0) yield break;

        List<Vector3> startPositions = new List<Vector3>();
        List<Vector3> endPositions = new List<Vector3>();

        for (int i = 0; i < count; i++)
        {
            startPositions.Add(cardsInStock[i].localPosition);
            float targetX = (i - (count - 1)) * stockGap;
            endPositions.Add(new Vector3(targetX, 0, 0));
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < count; i++)
            {
                if (cardsInStock[i] != null)
                    cardsInStock[i].localPosition = Vector3.Lerp(startPositions[i], endPositions[i], t);
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (cardsInStock[i] != null)
            {
                cardsInStock[i].localPosition = endPositions[i];
                cardsInStock[i].localScale = Vector3.one;
            }
        }
    }
}