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
        if (modeManager != null) modeManager.IsInputAllowed = false;

        // <--- ПОДГОТОВКА ДИНАМИЧЕСКОГО ЗВУКА --->
        AudioSource shakeSource = null;
        float baseVolume = 1f;
        if (AudioManager.Instance != null)
        {
            // Получаем источник звука, чтобы управлять им в реальном времени
            shakeSource = AudioManager.Instance.PlaySound("Card_Shake");
            if (shakeSource != null) baseVolume = shakeSource.volume;
        }

        Dictionary<RectTransform, float> originalX = new Dictionary<RectTransform, float>();
        foreach (var c in cards)
            if (c) originalX[c.rectTransform] = c.rectTransform.anchoredPosition.x;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;

            // Математика визуальной тряски
            float offset = Mathf.Sin(elapsed * speed) * magnitude * (1f - percent);

            // <--- ИЗМЕНЕНИЕ ГРОМКОСТИ ОТ СКОРОСТИ --->
            if (shakeSource != null)
            {
                // 1. (1f - percent) плавно затухает звук к концу анимации
                // 2. Mathf.Abs(Mathf.Cos(elapsed * speed)) делает звук громче в центре рывка 
                //    и тише в крайних точках амплитуды (эффект трения картона)
                float movementSpeed = Mathf.Abs(Mathf.Cos(elapsed * speed));
                shakeSource.volume = baseVolume * (1f - percent) * movementSpeed;
            }

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

        // <--- ОСТАНАВЛИВАЕМ ЗВУК И СБРАСЫВАЕМ НАСТРОЙКИ --->
        if (shakeSource != null)
        {
            shakeSource.Stop();
            shakeSource.volume = baseVolume; // Возвращаем исходную громкость в пул AudioManager
        }

        // --- 2. РАЗБЛОКИРУЕМ ВВОД ---
        if (modeManager != null) modeManager.IsInputAllowed = true;

        IsShaking = false;
    }
}