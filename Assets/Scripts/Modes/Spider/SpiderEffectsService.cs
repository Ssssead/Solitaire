using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpiderEffectsService : MonoBehaviour
{
    public static SpiderEffectsService Instance { get; private set; }

    [Header("Shake Settings")]
    public float duration = 0.3f;
    public float magnitude = 10f;
    public float speed = 50f;

    // Ссылка на менеджер режима (чтобы блокировать ввод)
    private SpiderModeManager modeManager;

    // Флаг, чтобы не запускать тряску поверх тряски
    public bool IsShaking { get; private set; } = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Находим менеджер игры
        modeManager = FindObjectOfType<SpiderModeManager>();
    }

    public void Shake(List<CardController> cards)
    {
        if (cards == null || cards.Count == 0) return;

        // Если уже трясется — выходим, чтобы не накладывать эффекты
        if (IsShaking) return;

        StartCoroutine(ShakeRoutine(cards));
    }

    private IEnumerator ShakeRoutine(List<CardController> cards)
    {
        IsShaking = true;

        // --- 1. БЛОКИРУЕМ ВВОД ГЛОБАЛЬНО ---
        // DragManager увидит это и не даст взять карту
        if (modeManager != null) modeManager.IsInputAllowed = false;

        Dictionary<RectTransform, float> originalX = new Dictionary<RectTransform, float>();
        foreach (var c in cards)
            if (c) originalX[c.rectTransform] = c.rectTransform.anchoredPosition.x;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;
            float offset = Mathf.Sin(elapsed * speed) * magnitude * (1f - percent);

            foreach (var c in cards)
            {
                if (c == null) continue;

                if (originalX.ContainsKey(c.rectTransform))
                {
                    Vector2 pos = c.rectTransform.anchoredPosition;
                    pos.x = originalX[c.rectTransform] + offset;
                    c.rectTransform.anchoredPosition = pos;
                }
            }
            yield return null;
        }

        // Возвращаем на место (гарантированно выравниваем)
        foreach (var c in cards)
        {
            if (c != null && originalX.ContainsKey(c.rectTransform))
            {
                Vector2 pos = c.rectTransform.anchoredPosition;
                pos.x = originalX[c.rectTransform];
                c.rectTransform.anchoredPosition = pos;
            }
        }

        // --- 2. РАЗБЛОКИРУЕМ ВВОД ---
        if (modeManager != null) modeManager.IsInputAllowed = true;

        IsShaking = false;
    }
}