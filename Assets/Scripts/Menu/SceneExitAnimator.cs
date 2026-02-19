using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SceneExitAnimator : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform topPanel;
    public RectTransform[] bottomButtons;

    [Header("Game References")]
    public PileManager pileManager;

    [Header("Animation Settings")]
    [Tooltip("Сила притяжения (чем больше минус, тем быстрее разгон вниз)")]
    public float gravity = -1500f; // Было -3000, уменьшили для плавности
    [Tooltip("Максимальная скорость вращения карт")]
    public float maxRotationSpeed = 100f; // Было 200, уменьшили чтобы не мельтешили

    // Класс данных для падающей карты
    private class FallingCard
    {
        public Transform transform;
        public Vector3 velocity;
        public float rotationDir;
        public float delay; // Задержка для эффекта "волны" (опционально)
    }

    public void PlayExitSequence(System.Action onComplete)
    {
        StartCoroutine(ExitRoutine(onComplete));
    }

    // --- НОВЫЙ МЕТОД (ПЕРЕЗАПУСК) ---
    public void PlayRestartSequence(System.Action onComplete)
    {
        StartCoroutine(RestartRoutine(onComplete));
    }
    private IEnumerator RestartRoutine(System.Action onComplete)
    {
        // ВНИМАНИЕ: Мы НЕ убираем UI и НЕ прячем слоты.
        // Только роняем карты.

        // 1. Запускаем падение карт
        // destroyAfter = true, так как сцена не перезагружается, мусор надо убрать
        yield return StartCoroutine(DropAllCards(destroyAfter: true));

        // 2. Небольшая пауза перед прилетом новой колоды
        yield return new WaitForSeconds(0.5f);

        onComplete?.Invoke();
    }

    private IEnumerator ExitRoutine(System.Action onComplete)
    {
        // 1. Убираем интерфейс
        StartCoroutine(AnimateHUDOut());
        // 2. Слоты
        StartCoroutine(FadeOutSlots());
        // 3. Карты
        yield return StartCoroutine(DropAllCards(destroyAfter: false)); // При выходе сцену все равно уничтожат

        yield return new WaitForSeconds(0.2f);
        onComplete?.Invoke();
    }

    private IEnumerator AnimateHUDOut()
    {
        float duration = 0.5f;
        float elapsed = 0f;

        Vector2 topStart = topPanel != null ? topPanel.anchoredPosition : Vector2.zero;
        Vector2 topTarget = topStart + new Vector2(0, 300f);

        Vector2[] btnStarts = new Vector2[bottomButtons.Length];
        Vector2[] btnTargets = new Vector2[bottomButtons.Length];

        for (int i = 0; i < bottomButtons.Length; i++)
        {
            if (bottomButtons[i] != null)
            {
                btnStarts[i] = bottomButtons[i].anchoredPosition;
                btnTargets[i] = btnStarts[i] + new Vector2(0, -300f);
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float tEase = t * t * t;

            if (topPanel) topPanel.anchoredPosition = Vector2.Lerp(topStart, topTarget, tEase);

            for (int i = 0; i < bottomButtons.Length; i++)
            {
                if (bottomButtons[i] != null)
                    bottomButtons[i].anchoredPosition = Vector2.Lerp(btnStarts[i], btnTargets[i], tEase);
            }
            yield return null;
        }
    }

    private IEnumerator FadeOutSlots()
    {
        List<Image> slotImages = new List<Image>();

        if (pileManager != null)
        {
            void CollectImages(Transform root)
            {
                if (root == null) return;
                var img = root.GetComponent<Image>();
                if (img != null) slotImages.Add(img);

                foreach (Transform child in root)
                {
                    if (child.GetComponent<CardController>() == null)
                    {
                        var childImg = child.GetComponent<Image>();
                        if (childImg != null) slotImages.Add(childImg);
                    }
                }
            }

            if (pileManager.StockPile) CollectImages(pileManager.StockPile.transform);
            if (pileManager.WastePile) CollectImages(pileManager.WastePile.transform);
            foreach (var p in pileManager.Foundations) CollectImages(p.transform);
            foreach (var p in pileManager.Tableau) CollectImages(p.transform);
        }

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);

            foreach (var img in slotImages)
            {
                if (img == null) continue;
                Color c = img.color;
                c.a = alpha;
                img.color = c;
            }
            yield return null;
        }
    }

    private IEnumerator DropAllCards(bool destroyAfter)
    {
        CardController[] allCards = FindObjectsOfType<CardController>();

        if (allCards.Length == 0) yield break;

        List<FallingCard> fallingCards = new List<FallingCard>();

        foreach (var card in allCards)
        {
            if (card == null) continue;

            card.enabled = false;
            var group = card.GetComponent<CanvasGroup>();
            if (group) group.blocksRaycasts = false;

            FallingCard fc = new FallingCard();
            fc.transform = card.transform;

            float driftX = Random.Range(-10f, 10f);
            float initialSpeedDown = Random.Range(-10f, -50f);
            fc.velocity = new Vector3(driftX, initialSpeedDown, 0);
            fc.rotationDir = Random.Range(-1f, 1f) * (maxRotationSpeed * 0.5f);
            fc.delay = Random.Range(0f, 0.15f);

            fallingCards.Add(fc);
        }

        float duration = 2.0f; // Даем время упасть
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            foreach (var fc in fallingCards)
            {
                if (fc.transform == null) continue;

                if (fc.delay > 0)
                {
                    fc.delay -= dt;
                    continue;
                }

                fc.velocity.y += gravity * dt;
                fc.transform.position += fc.velocity * dt;
                fc.transform.Rotate(0, 0, fc.rotationDir * dt);
            }
            yield return null;
        }

        // --- ВАЖНО ДЛЯ ПЕРЕЗАПУСКА ---
        if (destroyAfter)
        {
            foreach (var fc in fallingCards)
            {
                if (fc.transform != null) Destroy(fc.transform.gameObject);
            }
        }
    }
}