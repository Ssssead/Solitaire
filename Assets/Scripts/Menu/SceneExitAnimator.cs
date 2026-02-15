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

    private IEnumerator ExitRoutine(System.Action onComplete)
    {
        // 1. Убираем интерфейс
        StartCoroutine(AnimateHUDOut());

        // 2. Запускаем исчезновение слотов
        StartCoroutine(FadeOutSlots());

        // 3. Запускаем скатывание карт
        yield return StartCoroutine(DropAllCards());

        // 4. Пауза
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

    private IEnumerator DropAllCards()
    {
        CardController[] allCards = FindObjectsOfType<CardController>();

        if (allCards.Length == 0) yield break;

        List<FallingCard> fallingCards = new List<FallingCard>();

        foreach (var card in allCards)
        {
            if (card == null) continue;

            // Отключаем логику и Raycast
            card.enabled = false;
            var group = card.GetComponent<CanvasGroup>();
            if (group) group.blocksRaycasts = false;

            FallingCard fc = new FallingCard();
            fc.transform = card.transform;

            // --- НАСТРОЙКИ ДЛЯ "РОВНОГО СКАТЫВАНИЯ" ---

            // 1. Позиция по X: Уменьшаем разброс до минимума.
            // Было (-50, 50), ставим (-10, 10). 
            // Карты будут ехать почти строго вниз.
            float driftX = Random.Range(-10f, 10f);

            // 2. Позиция по Y: Чуть-чуть толкаем вниз на старте
            float initialSpeedDown = Random.Range(-10f, -50f);

            fc.velocity = new Vector3(driftX, initialSpeedDown, 0);

            // 3. Вращение: Еще медленнее, чтобы не отвлекало
            fc.rotationDir = Random.Range(-1f, 1f) * (maxRotationSpeed * 0.5f);

            // 4. Задержка
            fc.delay = Random.Range(0f, 0.15f);

            fallingCards.Add(fc);
        }

        // Увеличим время жизни, так как гравитация слабая (-100), 
        // карты могут не успеть уехать за экран за 2 секунды.
        float duration = 4.0f;
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

                // Применяем вашу гравитацию (-100)
                fc.velocity.y += gravity * dt;

                fc.transform.position += fc.velocity * dt;
                fc.transform.Rotate(0, 0, fc.rotationDir * dt);
            }
            yield return null;
        }
    }
}