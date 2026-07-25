using System;
using System.Collections;
using UnityEngine;

public class OctagonAnimationService : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private OctagonDeckManager deckManager;

    private void Start()
    {
        deckManager = FindObjectOfType<OctagonDeckManager>();
    }

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

            // --- НОВОЕ: Проверяем, включен ли пропуск (skip), и ускоряем полет в 15 раз ---
            float speed = (deckManager != null && deckManager.isSkippingIntro) ? 15f : 1f;
            elapsed += Time.deltaTime * speed;

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

    public IEnumerator AnimateShake(CardController card)
    {
        if (card == null) yield break;

        // <--- ПОДГОТОВКА ЗВУКА --->
        AudioSource scrapeSource = null;
        float originalVolume = 1f;

        if (AudioManager.Instance != null)
        {
            scrapeSource = AudioManager.Instance.PlaySound("Card_Shake");
            if (scrapeSource != null)
            {
                originalVolume = scrapeSource.volume; // Запоминаем дефолтную громкость из настроек
            }
        }

        Vector3 startPosition = card.rectTransform.anchoredPosition;
        float shakeDuration = 0.25f;
        float shakeAmplitude = 4f;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float phase = Mathf.Sin(elapsed * 40f) * (1f - elapsed / shakeDuration);

            // <--- ДИНАМИЧЕСКАЯ ГРОМКОСТЬ ОТ СКОРОСТИ --->
            if (scrapeSource != null && scrapeSource.isPlaying)
            {
                // Abs(Cos) дает пульсацию от 0 до 1 синхронно с движением карты
                float speedMultiplier = Mathf.Abs(Mathf.Cos(elapsed * 60f));

                // Не уводим звук в абсолютный ноль (0.1f), чтобы не было "рваного" обрыва
                float dynamicVolume = Mathf.Lerp(0.1f, 1f, speedMultiplier);

                // Плавно глушим общий звук к самому концу анимации
                float generalFade = 1f - (elapsed / shakeDuration);

                // Применяем финальную громкость
                scrapeSource.volume = originalVolume * dynamicVolume * generalFade;
            }

            // Движение карты (Синус)
            float offsetX = Mathf.Sin(elapsed * 60f) * shakeAmplitude * phase;
            card.rectTransform.anchoredPosition = startPosition + new Vector3(offsetX, 0f, 0f);

            yield return null;
        }

        // Возвращаем в исходную позицию
        card.rectTransform.anchoredPosition = startPosition;

        // <--- ОСТАНОВКА И СБРОС ЗВУКА --->
        if (scrapeSource != null)
        {
            scrapeSource.Stop(); // Жестко рубим длинный хвост файла
            scrapeSource.volume = originalVolume; // ВАЖНО: возвращаем громкость, иначе пул сломается!
        }
    }
}