using UnityEngine;
using System.Collections;
using System;

public class YukonAnimationService : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Flip")]
    [SerializeField] private float flipDuration = 0.15f;

    // Перемещение
    public IEnumerator AnimateCard(Transform cardTransform, Vector3 targetWorldPos, Action onComplete)
    {
        Vector3 startPos = cardTransform.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float curveValue = moveCurve.Evaluate(t);

            cardTransform.position = Vector3.Lerp(startPos, targetWorldPos, curveValue);
            yield return null;
        }

        cardTransform.position = targetWorldPos;
        onComplete?.Invoke();
    }

    // --- НОВЫЙ МЕТОД: Переворот карты ---
    public IEnumerator AnimateFlip(CardController card, bool targetFaceUp, Action onMidFlip)
    {
        float halfDuration = flipDuration / 2f;
        float elapsed = 0f;
        Transform t = card.transform;
        Vector3 originalScale = t.localScale;

        // 1. Сжимаем карту до 0 по оси X (поворот на 90 градусов)
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float scaleX = Mathf.Lerp(originalScale.x, 0f, elapsed / halfDuration);
            t.localScale = new Vector3(scaleX, originalScale.y, originalScale.z);
            yield return null;
        }

        // 2. В середине меняем спрайт
        onMidFlip?.Invoke();

        // 3. Разжимаем обратно до 1
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float scaleX = Mathf.Lerp(0f, originalScale.x, elapsed / halfDuration);
            t.localScale = new Vector3(scaleX, originalScale.y, originalScale.z);
            yield return null;
        }

        t.localScale = originalScale;
    }
}