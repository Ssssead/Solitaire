using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TriPeaksAnimationService : MonoBehaviour
{
    [Header("Settings")]
    public float stockGap = 5f;

    // Анимация полета
    public IEnumerator AnimateMoveCard(CardController card, Transform targetTransform, float duration, bool targetFaceUp, System.Action onComplete)
    {
        Canvas canvas = card.GetComponentInParent<Canvas>();
        if (canvas != null) card.transform.SetParent(canvas.transform);
        else card.transform.SetParent(card.transform.root);

        card.transform.SetAsLastSibling();

        Vector3 startPos = card.transform.position;
        Vector3 endPos = targetTransform.position;
        card.transform.rotation = Quaternion.identity;

        var cardData = card.GetComponent<CardData>();
        bool startFaceUp = cardData != null && cardData.IsFaceUp();
        bool needsFlip = (startFaceUp != targetFaceUp);
        bool flipTriggered = false;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tMove = Mathf.SmoothStep(0f, 1f, t);

            card.transform.position = Vector3.Lerp(startPos, endPos, tMove);

            // Если нужно перевернуть в полете
            if (needsFlip)
            {
                // Сжимаем и разжимаем сами
                float scaleX = Mathf.Abs(2f * t - 1f);
                card.transform.localScale = new Vector3(scaleX, 1f, 1f);

                // Момент смены спрайта (середина пути)
                if (t >= 0.5f && !flipTriggered && cardData != null)
                {
                    // ВАЖНО: false = МГНОВЕННО.
                    // Мы не хотим, чтобы CardData запускала свою корутину, 
                    // так как мы сами меняем scale прямо здесь.
                    cardData.SetFaceUp(targetFaceUp, false);

                    // Восстанавливаем scale, так как SetFaceUp(instant) сбрасывает его в 1
                    card.transform.localScale = new Vector3(scaleX, 1f, 1f);

                    flipTriggered = true;
                }
            }
            else
            {
                card.transform.localScale = Vector3.one;
            }

            yield return null;
        }

        // Финал
        card.transform.position = endPos;
        card.transform.SetParent(targetTransform);
        card.transform.localPosition = Vector3.zero;
        card.transform.localScale = Vector3.one;
        card.transform.localRotation = Quaternion.identity;

        // Финальная синхронизация (мгновенно)
        if (cardData != null) cardData.SetFaceUp(targetFaceUp, false);

        onComplete?.Invoke();
    }

    public IEnumerator AnimateFlip(CardController card, float duration)
    {
        // Этот метод больше не используется ModeManager-ом для игровых карт,
        // так как мы перешли на нативную анимацию CardData.
        // Оставляю его пустым или для совместимости.
        yield break;
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
                cardsInStock[i].localPosition = endPositions[i];
        }
    }
}