using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic; // Нужно для List

public class XPProgressBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text xpText;

    [Header("Visuals (Tiers)")]
    [Tooltip("Сюда положите 10 спрайтов (цветов) для этого режима. 0-й для ур. 0-9, 1-й для 10-19 и т.д.")]
    [SerializeField] private List<Sprite> tierSprites;

    // Если нужно менять не fillImage, а, например, рамку или фон, привяжите это сюда. 
    // Если null, будем менять sprite у fillImage.
    [SerializeField] private Image targetImageToChange;

    private Coroutine animationCoroutine;

    // Свойство для удобного доступа к нужному Image
    private Image TargetGraphic => targetImageToChange != null ? targetImageToChange : fillImage;

    // Мгновенное обновление (для Меню)
    public void UpdateBar(int level, int currentXP, int targetXP)
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);

        // Сразу ставим правильный визуал для текущего уровня
        UpdateVisuals(level);

        SetUI(level, currentXP, targetXP, (targetXP > 0 ? (float)currentXP / targetXP : 0));
    }

    // Обычная анимация (без повышения уровня)
    public void AnimateBar(int level, int startXP, int endXP, int targetXP, float delay = 0.5f)
    {
        if (!gameObject.activeInHierarchy)
        {
            UpdateBar(level, endXP, targetXP);
            return;
        }

        if (animationCoroutine != null) StopCoroutine(animationCoroutine);

        // Перед анимацией убеждаемся, что визуал соответствует уровню
        UpdateVisuals(level);

        animationCoroutine = StartCoroutine(AnimateFillRoutine(level, startXP, endXP, targetXP, delay));
    }

    // --- Анимация повышения уровня ---
    public void AnimateLevelUp(int oldLevel, int oldXP, int oldTarget, int newLevel, int newXP, int newTarget, float delay = 0.5f)
    {
        if (!gameObject.activeInHierarchy)
        {
            UpdateBar(newLevel, newXP, newTarget);
            return;
        }

        if (animationCoroutine != null) StopCoroutine(animationCoroutine);

        // Стартуем с визуалом СТАРОГО уровня
        UpdateVisuals(oldLevel);

        animationCoroutine = StartCoroutine(LevelUpRoutine(oldLevel, oldXP, oldTarget, newLevel, newXP, newTarget, delay));
    }

    // --- Логика смены цвета/спрайта ---
    private void UpdateVisuals(int level)
    {
        if (tierSprites == null || tierSprites.Count == 0) return;
        if (TargetGraphic == null) return;

        // Формула цикла: каждые 10 уровней меняем индекс.
        // Если спрайтов 10, то % 10 обеспечит цикл (0..9)
        int tierIndex = (level / 10) % tierSprites.Count;

        if (tierSprites[tierIndex] != null)
        {
            TargetGraphic.sprite = tierSprites[tierIndex];
        }
    }

    // --- Корутины ---

    private IEnumerator AnimateFillRoutine(int level, int startXP, int endXP, int targetXP, float delay)
    {
        float startFill = (targetXP > 0) ? (float)startXP / targetXP : 0f;
        float targetFill = (targetXP > 0) ? (float)endXP / targetXP : 0f;

        SetUI(level, startXP, targetXP, startFill);
        yield return new WaitForSeconds(delay);
        yield return LerpFill(startFill, targetFill, 1.0f);
        SetUI(level, endXP, targetXP, targetFill);
    }

    private IEnumerator LevelUpRoutine(int oldLevel, int oldXP, int oldTarget, int newLevel, int newXP, int newTarget, float delay)
    {
        // ЭТАП 1: Показываем старый уровень (Визуал старого уровня уже установлен при старте)
        float oldStartFill = (float)oldXP / oldTarget;
        SetUI(oldLevel, oldXP, oldTarget, oldStartFill);

        yield return new WaitForSeconds(delay);

        // Анимация до 100%
        yield return LerpFill(oldStartFill, 1.0f, 0.5f);
        SetUI(oldLevel, oldTarget, oldTarget, 1.0f);

        yield return new WaitForSeconds(0.2f); // Пауза на пике

        // ЭТАП 2: СМЕНА УРОВНЯ И ВИЗУАЛА
        // Здесь мы меняем картинку на новую, пока бар пустой
        UpdateVisuals(newLevel);

        SetUI(newLevel, 0, newTarget, 0f); // Сброс в 0

        // ЭТАП 3: Заполнение нового бара (уже новым цветом)
        float newTargetFill = (float)newXP / newTarget;
        yield return LerpFill(0f, newTargetFill, 0.5f);

        SetUI(newLevel, newXP, newTarget, newTargetFill);
    }

    private IEnumerator LerpFill(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (fillImage != null) fillImage.fillAmount = Mathf.SmoothStep(from, to, t);
            yield return null;
        }
        if (fillImage != null) fillImage.fillAmount = to;
    }

    private void SetUI(int level, int xp, int target, float fill)
    {
        if (levelText != null) levelText.text = level.ToString();
        if (xpText != null) xpText.text = $"{xp} / {target} XP";
        if (fillImage != null) fillImage.fillAmount = fill;
    }
}