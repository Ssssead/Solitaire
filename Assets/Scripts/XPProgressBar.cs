using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class XPProgressBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Image previewFillImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text xpText;

    [Header("Visuals (Tiers)")]
    // Заменили List<Sprite> на List<Color>
    [SerializeField] private List<Color> tierColors;
    [SerializeField] private Image targetImageToChange;

    private Coroutine animationCoroutine;
    private Coroutine previewAnimationCoroutine;
    private Image TargetGraphic => targetImageToChange != null ? targetImageToChange : fillImage;

    public void ShowPreviewXP(int currentXP, int addedXP, int targetXP)
    {
        if (targetXP <= 0) targetXP = 1;
        if (previewFillImage == null || fillImage == null) return;

        previewFillImage.type = fillImage.type;
        previewFillImage.fillMethod = fillImage.fillMethod;

        // Берем цвет текущего тира и делаем его полупрозрачным для превью
        Color c = TargetGraphic.color;
        c.a = 0.6f;
        previewFillImage.color = c;

        float startFill = fillImage.fillAmount;
        float targetFill = (float)(currentXP + addedXP) / targetXP;
        if (targetFill > 1.0f) targetFill = 1.0f;

        if (previewAnimationCoroutine != null) StopCoroutine(previewAnimationCoroutine);

        if (previewFillImage.fillAmount < startFill || previewFillImage.fillAmount > targetFill)
        {
            previewFillImage.fillAmount = startFill;
        }

        previewAnimationCoroutine = StartCoroutine(AnimatePreviewRoutine(targetFill, 0.3f));
    }

    public void HidePreviewXP()
    {
        if (previewFillImage == null || fillImage == null) return;

        float targetFill = fillImage.fillAmount;

        if (previewAnimationCoroutine != null) StopCoroutine(previewAnimationCoroutine);
        previewAnimationCoroutine = StartCoroutine(AnimatePreviewRoutine(targetFill, 0.3f));
    }

    private IEnumerator AnimatePreviewRoutine(float targetFill, float duration)
    {
        float startFill = previewFillImage.fillAmount;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            previewFillImage.fillAmount = Mathf.Lerp(startFill, targetFill, t);
            yield return null;
        }
        previewFillImage.fillAmount = targetFill;
    }

    public void UpdateBar(int level, int currentXP, int targetXP)
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        if (previewAnimationCoroutine != null) StopCoroutine(previewAnimationCoroutine);

        UpdateVisuals(level);
        float ratio = (targetXP > 0 ? (float)currentXP / targetXP : 0);

        SetUI(level, currentXP, targetXP, ratio);

        if (previewFillImage != null)
        {
            previewFillImage.fillAmount = ratio;
        }
    }

    public void AnimateBar(int level, int startXP, int endXP, int targetXP, float delay = 0.5f)
    {
        if (!gameObject.activeInHierarchy)
        {
            UpdateBar(level, endXP, targetXP);
            return;
        }
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        UpdateVisuals(level);
        animationCoroutine = StartCoroutine(AnimateFillRoutine(level, startXP, endXP, targetXP, delay));
    }

    public void AnimateLevelUp(int oldLevel, int oldXP, int oldTarget, int newLevel, int newXP, int newTarget, float delay = 0.5f)
    {
        if (!gameObject.activeInHierarchy)
        {
            UpdateBar(newLevel, newXP, newTarget);
            return;
        }
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        UpdateVisuals(oldLevel);
        animationCoroutine = StartCoroutine(LevelUpRoutine(oldLevel, oldXP, oldTarget, newLevel, newXP, newTarget, delay));
    }

    // Измененный метод: меняет Image.color вместо Image.sprite
    private void UpdateVisuals(int level)
    {
        if (tierColors == null || tierColors.Count == 0) return;
        if (TargetGraphic == null) return;

        int tierIndex = (level / 10) % tierColors.Count;
        TargetGraphic.color = tierColors[tierIndex];

        // Синхронизируем цвет у preview бара
        if (previewFillImage != null)
        {
            Color c = tierColors[tierIndex];
            c.a = 0.6f;
            previewFillImage.color = c;
        }
    }

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
        float oldStartFill = (float)oldXP / oldTarget;
        SetUI(oldLevel, oldXP, oldTarget, oldStartFill);
        yield return new WaitForSeconds(delay);
        yield return LerpFill(oldStartFill, 1.0f, 0.5f);
        SetUI(oldLevel, oldTarget, oldTarget, 1.0f);
        yield return new WaitForSeconds(0.2f);
        UpdateVisuals(newLevel);
        SetUI(newLevel, 0, newTarget, 0f);
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
            if (previewFillImage != null) previewFillImage.fillAmount = Mathf.SmoothStep(from, to, t);
            yield return null;
        }
        if (fillImage != null) fillImage.fillAmount = to;
        if (previewFillImage != null) previewFillImage.fillAmount = to;
    }

    private void SetUI(int level, int xp, int target, float fill)
    {
        if (levelText != null) levelText.text = level.ToString();
        if (xpText != null) xpText.text = $"{xp} / {target} XP";
        if (fillImage != null) fillImage.fillAmount = fill;
    }
}