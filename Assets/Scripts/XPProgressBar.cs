using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class XPProgressBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text xpText;

    private Coroutine animationCoroutine;

    // Мгновенное обновление (для Меню)
    public void UpdateBar(int level, int currentXP, int targetXP)
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);

        SetUI(level, currentXP, targetXP, (targetXP > 0 ? (float)currentXP / targetXP : 0));
    }

    // Обычная анимация (без повышения уровня)
    public void AnimateBar(int level, int startXP, int endXP, int targetXP, float delay = 0.5f)
    {
        // ЗАЩИТА: Если объект выключен, просто ставим финальные значения без анимации
        if (!gameObject.activeInHierarchy)
        {
            UpdateBar(level, endXP, targetXP);
            return;
        }

        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateFillRoutine(level, startXP, endXP, targetXP, delay));
    }

    // --- НОВОЕ: Анимация повышения уровня ---
    public void AnimateLevelUp(int oldLevel, int oldXP, int oldTarget, int newLevel, int newXP, int newTarget, float delay = 0.5f)
    {
        // ЗАЩИТА
        if (!gameObject.activeInHierarchy)
        {
            UpdateBar(newLevel, newXP, newTarget);
            return;
        }

        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(LevelUpRoutine(oldLevel, oldXP, oldTarget, newLevel, newXP, newTarget, delay));
    }

    // --- Корутины ---

    private IEnumerator AnimateFillRoutine(int level, int startXP, int endXP, int targetXP, float delay)
    {
        float startFill = (targetXP > 0) ? (float)startXP / targetXP : 0f;
        float targetFill = (targetXP > 0) ? (float)endXP / targetXP : 0f;

        // Начальное состояние
        SetUI(level, startXP, targetXP, startFill);
        yield return new WaitForSeconds(delay);

        // Анимация
        yield return LerpFill(startFill, targetFill, 1.0f);

        // Финализация (на всякий случай)
        SetUI(level, endXP, targetXP, targetFill);
    }

    private IEnumerator LevelUpRoutine(int oldLevel, int oldXP, int oldTarget, int newLevel, int newXP, int newTarget, float delay)
    {
        // ЭТАП 1: Показываем старый уровень и заполняем до 100%
        float oldStartFill = (float)oldXP / oldTarget;
        SetUI(oldLevel, oldXP, oldTarget, oldStartFill);

        yield return new WaitForSeconds(delay);

        // Анимация до полного бара
        yield return LerpFill(oldStartFill, 1.0f, 0.5f); // 0.5 сек на заполнение
        SetUI(oldLevel, oldTarget, oldTarget, 1.0f); // Показываем 1000/1000

        yield return new WaitForSeconds(0.2f); // Маленькая пауза перед "хлопком"

        // ЭТАП 2: Смена уровня, сброс бара в 0
        SetUI(newLevel, 0, newTarget, 0f);

        // ЭТАП 3: Заполнение нового бара
        float newTargetFill = (float)newXP / newTarget;
        yield return LerpFill(0f, newTargetFill, 0.5f); // 0.5 сек на заполнение нового

        // Финал
        SetUI(newLevel, newXP, newTarget, newTargetFill);
    }

    // Хелпер для плавной смены fillAmount
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

    // Хелпер для установки текстов и картинки
    private void SetUI(int level, int xp, int target, float fill)
    {
        if (levelText != null) levelText.text = level.ToString();
        if (xpText != null) xpText.text = $"{xp} / {target} XP";
        if (fillImage != null) fillImage.fillAmount = fill;
    }
}