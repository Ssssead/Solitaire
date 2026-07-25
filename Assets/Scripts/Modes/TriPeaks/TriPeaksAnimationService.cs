using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TriPeaksAnimationService : MonoBehaviour
{
    [Header("Settings")]
    public float stockGap = 5f;

    public IEnumerator AnimateMoveCard(CardController card, Transform targetTransform, float duration, bool targetFaceUp, System.Action onComplete, Vector3 localOffset = default)
    {
        // --- ИНТЕГРАЦИЯ ТЕНИ (СТАРТ) ---
        var shadow = card.GetComponent<TriPeaksCardShadow>();
        if (shadow != null) shadow.SetState(TriPeaksCardShadow.ShadowState.Flying);

        Canvas canvas = card.GetComponentInParent<Canvas>();
        if (canvas != null) card.transform.SetParent(canvas.transform);
        else card.transform.SetParent(card.transform.root);

        card.transform.SetAsLastSibling();

        Vector3 startPos = card.transform.position;
        // Переводим желаемый локальный отступ в мировые координаты для полета
        Vector3 endPos = targetTransform.TransformPoint(localOffset);
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
        // Финально применяем точный отступ
        card.transform.localPosition = localOffset;
        card.transform.localScale = Vector3.one;
        card.transform.localRotation = Quaternion.identity;

        if (cardData != null) cardData.SetFaceUp(targetFaceUp, false);

        // --- ИНТЕГРАЦИЯ ТЕНИ (ФИНИШ) ---
        if (shadow != null) shadow.SetState(TriPeaksCardShadow.ShadowState.Resting);

        onComplete?.Invoke();
    }
    public IEnumerator AnimateBallisticMoveCard(CardController card, Transform targetTransform, float speed, bool targetFaceUp, System.Action onComplete, Vector3 localOffset = default)
    {
        // --- ИНТЕГРАЦИЯ ТЕНИ (СТАРТ) ---
        var shadow = card.GetComponent<TriPeaksCardShadow>();
        if (shadow != null) shadow.SetState(TriPeaksCardShadow.ShadowState.Flying);

        Canvas canvas = card.GetComponentInParent<Canvas>();
        if (canvas != null) card.transform.SetParent(canvas.transform);
        else card.transform.SetParent(card.transform.root);

        card.transform.SetAsLastSibling();

        Vector3 startPos = card.transform.position;
        Vector3 endPos = targetTransform.TransformPoint(localOffset);
        card.transform.rotation = Quaternion.identity;

        var cardData = card.GetComponent<CardData>();
        bool startFaceUp = cardData != null && cardData.IsFaceUp();
        bool needsFlip = (startFaceUp != targetFaceUp);
        bool flipTriggered = false;

        float elapsed = 0f;

        // 1. Расстояние по прямой
        float distance = Vector3.Distance(startPos, endPos);

        // 2. Высота параболы
        float arcHeight = 10f + (distance * 0.15f);

        // 3. Вычисляем длину самой дуги (прямая + прыжок вверх и вниз)
        float pathLength = distance + (arcHeight * 2f);

        // 4. Время считается от ПОЛНОГО пути карты
        float duration = pathLength / speed;
        if (duration <= 0.05f) duration = 0.05f; // Минимальная защита

        // 5. Угол зависит от времени
        float maxRotation = duration * 120f; // 120 градусов в секунду
        maxRotation = Mathf.Clamp(maxRotation, 5f, 35f);

        if (startPos.x < endPos.x) maxRotation = Mathf.Abs(maxRotation);
        else maxRotation = -Mathf.Abs(maxRotation);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Баллистическая дуга
            Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
            float parabola = 4f * arcHeight * t * (1f - t);
            currentPos.y += parabola;

            card.transform.position = currentPos;

            // Вращение
            float currentRotZ = 0f;
            if (t <= 0.75f)
            {
                float tRot = t / 0.75f;
                currentRotZ = Mathf.Lerp(0f, maxRotation, Mathf.Sin(tRot * Mathf.PI * 0.5f));
            }
            else
            {
                float tRot = (t - 0.75f) / 0.25f;
                currentRotZ = Mathf.SmoothStep(maxRotation, 0f, tRot);
            }
            card.transform.localRotation = Quaternion.Euler(0f, 0f, currentRotZ);

            // Переворот
            if (needsFlip)
            {
                float scaleX = Mathf.Abs(2f * t - 1f);
                card.transform.localScale = new Vector3(scaleX, 1f, 1f);

                if (t >= 0.5f && !flipTriggered && cardData != null)
                {
                    cardData.SetFaceUp(targetFaceUp, false);
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

        // Жестко фиксируем позицию и угол в конце
        card.transform.position = endPos;
        card.transform.SetParent(targetTransform);
        card.transform.localPosition = localOffset;
        card.transform.localScale = Vector3.one;
        card.transform.localRotation = Quaternion.identity;

        if (cardData != null) cardData.SetFaceUp(targetFaceUp, false);

        // --- ИНТЕГРАЦИЯ ТЕНИ (ФИНИШ) ---
        if (shadow != null) shadow.SetState(TriPeaksCardShadow.ShadowState.Resting);

        onComplete?.Invoke();
    }
    public IEnumerator AnimateStockShift(TriPeaksStockPile stockPile, float duration)
    {
        List<Transform> cardsInStock = new List<Transform>();
        foreach (Transform child in stockPile.transform)
        {
            // --- ИСПРАВЛЕНИЕ: Берем только настоящие карты, игнорируем объект тени! ---
            if (child.gameObject.activeSelf && child.GetComponent<CardController>() != null)
            {
                cardsInStock.Add(child);
            }
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

    public IEnumerator AnimateRoundClear(TriPeaksPileManager pileManager, Canvas rootCanvas, float duration)
    {
        // 1. ПЕРЕХОД ИЗ СТОКА В СБРОС
        if (!pileManager.Stock.IsEmpty)
        {
            List<CardController> leftovers = new List<CardController>();
            foreach (Transform t in pileManager.Stock.transform)
            {
                var c = t.GetComponent<CardController>();
                if (c) leftovers.Add(c);
            }

            // <--- ИСПРАВЛЕНИЕ 1: Переворачиваем список --->
            // Теперь мы берем карты с конца списка (то есть с верхушки колоды)
            leftovers.Reverse();

            foreach (var c in leftovers)
            {
                pileManager.Stock.RemoveCard(c);

                Vector3 offset = pileManager.Waste.GetTargetLocalPositionForNextCard();
                pileManager.Waste.AddCard(c);

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySound("Card_Flip");

                Vector3 targetWorld = pileManager.Waste.transform.TransformPoint(offset);

                // <--- ИСПРАВЛЕНИЕ 2: Золотая середина скорости --->
                // Полет карты: 0.2f (быстро, но глаз успевает заметить дугу)
                StartCoroutine(FastFlyTo(c, targetWorld, 0.2f, pileManager.Waste.transform, offset));

                // Задержка между выстрелами: 0.04f (быстрый пулеметный шелест)
                yield return new WaitForSeconds(0.04f);
            }

            yield return new WaitForSeconds(0.2f);
        }

        // 2. ВСЯ СТОПКА УЛЕТАЕТ ЗА ЭКРАН

        if (AudioManager.Instance != null)
        {
            AudioSource whoosh = AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            if (whoosh != null) whoosh.pitch = 1.6f;
        }

        List<CardController> allCards = new List<CardController>();
        foreach (Transform t in pileManager.Waste.transform)
        {
            var c = t.GetComponent<CardController>();
            if (c) allCards.Add(c);
        }

        float screenWidth = 2000f;
        if (rootCanvas != null) screenWidth = rootCanvas.GetComponent<RectTransform>().rect.width;

        float flyOutDuration = 1.1f;

        // Запускаем полет всех карт АБСОЛЮТНО ОДНОВРЕМЕННО (в одном кадре)
        foreach (var c in allCards)
        {
            Vector3 targetPos = c.transform.position + (Vector3.right * (screenWidth * 1.5f));
            StartCoroutine(FlyAndDestroy(c, targetPos, flyOutDuration));

            // УБРАНО: yield return new WaitForSeconds(0.015f);
        }

        // Ждем, пока завершится полет всей стопки, прежде чем начать новый раунд
        yield return new WaitForSeconds(flyOutDuration + 0.1f);
    }

    // НОВЫЙ МЕТОД: Влет всех карт и их раскрытие
    public IEnumerator AnimateDeckArrivalAndExpand(
        List<CardController> cards,
        Transform stockTransform,
        float screenWidth,
        int finalStockCount,
        System.Func<bool> skipCheck,
        float flightGap = 1f,
        float finalGap = 5f)
    {
        int count = cards.Count;
        if (count == 0) yield break;

        float duration = 1.1f; // Одноэтапная динамичная анимация

        // Вычисляем индекс верхней карты стока (чтобы она легла ровно в 0)
        int stockTopIndex = Mathf.Max(0, finalStockCount - 1);

        // Стартовая позиция базы колоды (глубоко за левым краем)
        float startDeckBaseX = -screenWidth - 800f;

        // Рассчитываем финальные целевые позиции для всех карт
        float[] targetX = new float[count];
        for (int i = 0; i < count; i++)
        {
            targetX[i] = (i - stockTopIndex) * finalGap;
        }

        // Чтобы самая верхняя карта достигла своей цели,
        // база летящей колоды должна пролететь дальше
        float endDeckBaseX = targetX[count - 1] - (count - 1) * flightGap;

        // 1. Ставим все карты за экран в плотную стопку
        for (int i = 0; i < count; i++)
        {
            cards[i].gameObject.SetActive(true);
            cards[i].transform.localPosition = new Vector3(startDeckBaseX + i * flightGap, 0, 0);
            cards[i].transform.localRotation = Quaternion.identity;
            cards[i].transform.localScale = Vector3.one;
            cards[i].transform.SetAsLastSibling();
        }

        // 2. Фаза полета и сброса
        float elapsed = 0f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Whoosh_In");
        while (elapsed < duration)
        {
            float speed = (skipCheck != null && skipCheck()) ? 15f : 1f;
            elapsed += Time.deltaTime * speed;

            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            // Текущая позиция базы летящей колоды
            float currentDeckBaseX = Mathf.Lerp(startDeckBaseX, endDeckBaseX, t);

            for (int i = 0; i < count; i++)
            {
                // Где карта должна быть сейчас, если бы летела в плотной стопке
                float flyingX = currentDeckBaseX + i * flightGap;

                // ЛОГИКА СБРОСА: Карта двигается вместе с колодой, но как только
                // доезжает до своей финальной позиции - "застревает" на ней.
                float clampedX = Mathf.Min(flyingX, targetX[i]);

                cards[i].transform.localPosition = new Vector3(clampedX, 0, 0);
            }
            yield return null;
        }

        // 3. Гарантируем точные финальные координаты в конце анимации
        for (int i = 0; i < count; i++)
        {
            cards[i].transform.localPosition = new Vector3(targetX[i], 0, 0);
        }
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");
    }

    private IEnumerator FastFlyTo(CardController card, Vector3 targetWorld, float duration, Transform finalParent, Vector3 localOffset = default)
    {
        Vector3 start = card.transform.position;
        float e = 0f;
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
            // Применяем точный отступ вместо старого Vector3.zero
            card.transform.localPosition = localOffset;
        }
    }

    private IEnumerator FlyAndDestroy(CardController card, Vector3 targetWorld, float duration)
    {
        if (card == null) yield break;

        // --- ИНТЕГРАЦИЯ ТЕНИ (СТАРТ) ---
        var shadow = card.GetComponent<TriPeaksCardShadow>();
        if (shadow != null) shadow.SetState(TriPeaksCardShadow.ShadowState.Flying);

        card.transform.SetParent(card.transform.root);
        Vector3 start = card.transform.position;
        float e = 0f;

        while (e < duration)
        {
            e += Time.deltaTime;
            if (card == null) yield break;
            float t = e / duration;
            t = t * t;
            card.transform.position = Vector3.Lerp(start, targetWorld, t);
            yield return null;
        }

        if (card != null) Destroy(card.gameObject);
    }
    public IEnumerator AnimateShakeError(CardController card)
    {
        float duration = 0.35f;
        float elapsed = 0f;
        float maxAngle = 12f; // На сколько градусов отклоняется карта

        // <--- ЗВУК 2: ГЛУХОЙ СТУК И ШУРШАНИЕ --->
        AudioSource shakeSource = null;
        float baseShakeVolume = 1f;

        if (AudioManager.Instance != null)
        {
           // AudioManager.Instance.PlaySound("Card_Error"); // Глухой отказ
            shakeSource = AudioManager.Instance.PlaySound("Card_Shake");
            if (shakeSource != null) baseShakeVolume = shakeSource.volume;
        }

        // Сбрасываем вращение на случай, если игрок быстро кликает несколько раз
        card.transform.localRotation = Quaternion.identity;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float phase = 1f - t; // Затухание от 1 до 0

            // Формула затухающей синусоиды
            float angle = Mathf.Sin(t * Mathf.PI * 6f) * maxAngle * phase;
            card.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            // --- ДИНАМИЧЕСКАЯ ГРОМКОСТЬ (ПАТТЕРН Б) ---
            if (shakeSource != null && shakeSource.isPlaying)
            {
                // Производная (скорость вращения) для громкости
                float velocityFactor = Mathf.Abs(Mathf.Cos(t * Mathf.PI * 6f));
                shakeSource.volume = baseShakeVolume * velocityFactor * phase;
            }

            yield return null;
        }

        // Жестко выравниваем карту в конце
        card.transform.localRotation = Quaternion.identity;

        // Останавливаем шуршание
        if (shakeSource != null && shakeSource.isPlaying)
        {
            shakeSource.Stop();
            shakeSource.volume = baseShakeVolume;
        }
    }
}