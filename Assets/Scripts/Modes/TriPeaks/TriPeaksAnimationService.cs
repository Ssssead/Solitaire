using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TriPeaksAnimationService : MonoBehaviour
{
    [Header("Settings")]
    public float stockGap = 5f;

    // --- СТАНДАРТНАЯ АНИМАЦИЯ (Оставляем как было, чтобы не ломать скейл) ---
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
                float scaleX = Mathf.Abs(2f * t - 1f);
                card.transform.localScale = new Vector3(scaleX, 1f, 1f);

                if (t >= 0.5f && !flipTriggered && cardData != null)
                {
                    cardData.SetFaceUp(targetFaceUp, false);
                    // Восстанавливаем scale, так как мгновенная смена может сбросить его
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

        card.transform.position = endPos;
        card.transform.SetParent(targetTransform);
        card.transform.localPosition = Vector3.zero;
        card.transform.localScale = Vector3.one;
        card.transform.localRotation = Quaternion.identity;

        if (cardData != null) cardData.SetFaceUp(targetFaceUp, false);

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

    // --- НОВЫЕ МЕТОДЫ (ИСПРАВЛЕННЫЕ) ---

    // 1. Очистка стола (карты летят стеной вправо)
    public IEnumerator AnimateRoundClear(TriPeaksPileManager pileManager, Canvas rootCanvas, float duration)
    {
        // Сначала быстро перекидываем Stock в Waste (визуально)
        if (!pileManager.Stock.IsEmpty)
        {
            // Берем только трансформы, чтобы не нарушать логику стопки раньше времени
            List<CardController> leftovers = new List<CardController>();
            foreach (Transform t in pileManager.Stock.transform)
            {
                var c = t.GetComponent<CardController>();
                if (c) leftovers.Add(c);
            }

            foreach (var c in leftovers)
            {
                // Микро-анимация перелета в Waste
                StartCoroutine(FastFlyTo(c, pileManager.Waste.transform.position, 0.2f, pileManager.Waste.transform));
                yield return new WaitForSeconds(0.03f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        // Теперь все карты в Waste (или визуально там). Отправляем их вправо.
        List<CardController> allCards = new List<CardController>();
        foreach (Transform t in pileManager.Waste.transform)
        {
            var c = t.GetComponent<CardController>();
            if (c) allCards.Add(c);
        }

        // Вычисляем точку за правым краем экрана
        float screenWidth = 2000f;
        if (rootCanvas != null) screenWidth = rootCanvas.GetComponent<RectTransform>().rect.width;
        Vector3 targetPos = pileManager.Waste.transform.position + Vector3.right * (screenWidth * 1.5f);

        // ЗАПУСКАЕМ ВСЕ РАЗОМ (без yield внутри цикла)
        foreach (var c in allCards)
        {
            StartCoroutine(FlyAndDestroy(c, targetPos, 0.6f));
        }

        // Ждем пока улетят
        yield return new WaitForSeconds(0.7f);
    }

    // 2. Влет нового стока (паровозиком слева)
    public IEnumerator AnimateStockEntry(List<CardController> cards, TriPeaksStockPile stockPile, Canvas rootCanvas, float speedFactor)
    {
        float screenWidth = 2000f;
        if (rootCanvas != null) screenWidth = rootCanvas.GetComponent<RectTransform>().rect.width;

        // Стартовая точка: далеко слева
        // Используем локальные координаты относительно StockPile для надежности
        float startOffsetLocal = -(screenWidth / 1.5f + 500f);

        int count = cards.Count;
        float[] targetXPositions = new float[count];
        bool[] arrived = new bool[count];

        // Инициализация позиций
        for (int i = 0; i < count; i++)
        {
            CardController c = cards[i];
            c.gameObject.SetActive(true);

            // Кладем в StockPile сразу, чтобы локальные координаты работали корректно
            stockPile.AddCard(c);

            // Целевая позиция X (по формуле стока)
            targetXPositions[i] = (i - (count - 1)) * stockGap;

            // Ставим в начало (слева)
            c.transform.localPosition = new Vector3(targetXPositions[i] + startOffsetLocal, 0, 0);
            c.transform.localRotation = Quaternion.identity;
            c.transform.localScale = Vector3.one;

            arrived[i] = false;
        }

        bool allArrived = false;
        // Скорость в пикселях/сек (подбирается экспериментально)
        float speed = 2500f * speedFactor;
        float timeOut = 5.0f; // Защита от зависания

        while (!allArrived && timeOut > 0)
        {
            allArrived = true;
            float dt = Time.deltaTime;
            timeOut -= dt;

            for (int i = 0; i < count; i++)
            {
                if (arrived[i]) continue;

                CardController c = cards[i];
                Vector3 pos = c.transform.localPosition;
                float targetX = targetXPositions[i];

                // Двигаем вправо
                pos.x += speed * dt;

                // Если перелетели цель -> фиксируем
                if (pos.x >= targetX)
                {
                    pos.x = targetX;
                    arrived[i] = true;
                }
                else
                {
                    allArrived = false; // Кто-то еще летит
                }

                c.transform.localPosition = pos;
            }
            yield return null;
        }

        stockPile.UpdateVisuals();
    }

    // --- Helpers ---

    private IEnumerator FastFlyTo(CardController card, Vector3 targetWorld, float duration, Transform finalParent)
    {
        Vector3 start = card.transform.position;
        float e = 0f;

        // Сразу открываем, чтобы красиво летела
        var cd = card.GetComponent<CardData>();
        if (cd) cd.SetFaceUp(true, false);

        while (e < duration)
        {
            e += Time.deltaTime;
            if (card == null) yield break;
            card.transform.position = Vector3.Lerp(start, targetWorld, e / duration);
            yield return null;
        }
        if (card != null)
        {
            card.transform.SetParent(finalParent);
            card.transform.localPosition = Vector3.zero;
        }
    }

    private IEnumerator FlyAndDestroy(CardController card, Vector3 targetWorld, float duration)
    {
        if (card == null) yield break;

        // Отцепляем от родителя, чтобы летела плавно
        card.transform.SetParent(card.transform.root);
        Vector3 start = card.transform.position;
        float e = 0f;

        while (e < duration)
        {
            e += Time.deltaTime;
            if (card == null) yield break;
            float t = e / duration;
            t = t * t; // Ускорение
            card.transform.position = Vector3.Lerp(start, targetWorld, t);
            yield return null;
        }
        if (card != null) Destroy(card.gameObject);
    }
}