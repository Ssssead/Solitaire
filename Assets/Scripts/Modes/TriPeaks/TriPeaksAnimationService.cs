using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class TriPeaksAnimationService : MonoBehaviour
{
    [Header("Settings")]
    public float stockGap = 5f;

    // --- ПЛАВНЫЙ ПЕРЕВОРОТ НА МЕСТЕ ---
    public IEnumerator AnimateFlip(CardController card, float duration)
    {
        var cardData = card.GetComponent<CardData>();
        if (cardData == null) yield break;

        float halfDuration = duration / 2f;
        float elapsed = 0f;

        // 1. Сжатие (1 -> 0)
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            // SmoothStep для мягкости
            float scaleX = Mathf.SmoothStep(1f, 0f, t);

            card.transform.localScale = new Vector3(scaleX, 1f, 1f);
            card.transform.localPosition = Vector3.zero; // Держим в центре
            yield return null;
        }

        // 2. Смена спрайта (КРИТИЧЕСКАЯ СЕКЦИЯ)
        // CardData.SetFaceUp(..., true) внутри себя делает scale = 1.
        // Мы должны это перекрыть в том же кадре.
        card.transform.localScale = Vector3.zero;
        cardData.SetFaceUp(true, true);
        card.transform.localScale = Vector3.zero; // Гасим рывок

        // 3. Расширение (0 -> 1)
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float scaleX = Mathf.SmoothStep(0f, 1f, t);

            card.transform.localScale = new Vector3(scaleX, 1f, 1f);
            card.transform.localPosition = Vector3.zero;
            yield return null;
        }

        // 4. Финальный сброс
        card.transform.localScale = Vector3.one;
        card.transform.localPosition = Vector3.zero;
    }

    // --- Анимация полета (аналогичная защита от рывка) ---
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

            if (needsFlip)
            {
                // Синусоида 0 -> 1 -> 0, инвертируем в scale 1 -> 0 -> 1
                float scaleX = 1f - Mathf.Sin(t * Mathf.PI);
                card.transform.localScale = new Vector3(Mathf.Abs(scaleX), 1f, 1f);

                if (t >= 0.5f && !flipTriggered && cardData != null)
                {
                    // Защита от рывка при полете
                    cardData.SetFaceUp(targetFaceUp, true);

                    // Пересчитываем Scale для текущего момента t, чтобы он не стал 1.0
                    float currentScaleX = 1f - Mathf.Sin(t * Mathf.PI);
                    card.transform.localScale = new Vector3(Mathf.Abs(currentScaleX), 1f, 1f);

                    flipTriggered = true;
                }
            }
            else
            {
                card.transform.localScale = Vector3.one;
            }

            yield return null;
        }

        // Фиксация в конце
        card.transform.position = endPos;
        card.transform.SetParent(targetTransform);
        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;

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
                cardsInStock[i].localPosition = endPositions[i];
        }
    }
}