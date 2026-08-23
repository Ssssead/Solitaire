using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;

public class MenuExitController : MonoBehaviour
{
    [Header("Dependencies")]
    public CardAnimationController cardAnimator;

    [Header("Settings Panels (Dual Orientation)")]
    public RectTransform landscapeSettingsPanel;
    public RectTransform portraitSettingsPanel;

    [Header("Top UI Elements (Fly Up)")]
    public List<RectTransform> topUiElements;

    [Header("Conditional Elements (Fly Up or Left)")]
    public RectTransform leaderboardBtn;
    public RectTransform statsBtn;

    [Header("Phase 1: Falling Cards (Smooth)")]
    public float fallDuration = 0.7f;
    public float delayBetweenFalls = 0.08f;
    public float fallDistance = 1500f;
    public float fallRotation = 30f;
    public AnimationCurve fallCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Phase 1: Top UI Fly Up")]
    public float topUiFlyDuration = 0.6f;
    public float topUiDistance = 1500f; // Увеличили с 500 до 1500, чтобы точно улетали за экран
    public AnimationCurve topUiCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Phase 2: Settings Panel Exit")]
    public float landscapeExitDuration = 0.8f;
    public float portraitExitDuration = 0.5f; // Сделали быстрее
    public float landscapeExitDistanceX = 1500f;
    public float portraitExitDistanceX = 2500f; // Сделали дальше
    public AnimationCurve exitCurveSettingsPanel = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Phase 3: Selected Card Complex Sequence")]
    [Tooltip("Создай пустой UI-объект в центре экрана и перетащи его сюда!")]
    public RectTransform centerMarker;

    [Tooltip("Время полета в центр (увеличивается Scale + 360 поворот)")]
    public float moveToCenterDuration = 1.0f;

    [Tooltip("Пауза в центре (карта покачивается)")]
    public float pauseInCenterDuration = 0.4f;
    [Tooltip("Угол покачивания в центре (градусы)")]
    public float wobbleAngle = 5f;
    [Tooltip("Скорость покачивания в центре")]
    public float wobbleSpeed = 15f;

    [Tooltip("Время вылета из центра (уменьшается Scale)")]
    public float flyOutOfCenterDuration = 0.8f;
    [Tooltip("Пик увеличения масштаба (будет достигнут ровно в центре экрана)")]
    public float maxScaleMultiplier = 1.3f;
    [Tooltip("Поворот карты в самом конце полета влево (за экраном)")]
    public float finalExitRotation = 10f;

    public void PlayExitAnimation(GameType selectedGame, Action onComplete)
    {
        StartCoroutine(ExitSequence(selectedGame, onComplete));
    }

    private IEnumerator ExitSequence(GameType selectedGame, Action onComplete)
    {
        RectTransform selectedCardRect = null;
        List<RectTransform> cardsToFall = new List<RectTransform>();

        if (cardAnimator != null && cardAnimator.allCards != null)
        {
            foreach (var entry in cardAnimator.allCards)
            {
                if (entry.rect == null) continue;
                if (entry.type == selectedGame) selectedCardRect = entry.rect;
                else cardsToFall.Add(entry.rect);
            }
        }

        DisableAllInteractions(selectedCardRect);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySoundWithAutoFade("Table_Sweep", fallDuration, 0.2f);

        // --- ЛОГИКА АНИМАЦИИ ИНТЕРФЕЙСА В ЗАВИСИМОСТИ ОТ ОРИЕНТАЦИИ ---
        bool isPortrait = Screen.width < Screen.height;
        RectTransform activeSettingsPanel = isPortrait ? portraitSettingsPanel : landscapeSettingsPanel;

        // Вычисляем дистанцию и время "на лету"
        float currentExitDistance = isPortrait ? portraitExitDistanceX : landscapeExitDistanceX;
        float currentExitDuration = isPortrait ? portraitExitDuration : landscapeExitDuration;

        // 1. Двигаем обычный верхний UI ВВЕРХ
        if (topUiElements != null)
        {
            foreach (var ui in topUiElements)
            {
                if (ui != null && ui != leaderboardBtn && ui != statsBtn)
                {
                    StartCoroutine(MoveUiRoutine(ui, new Vector2(0, topUiDistance), topUiFlyDuration, topUiCurve));
                }
            }
        }

        // 2. Двигаем активную панель настроек ВЛЕВО
        if (activeSettingsPanel != null && activeSettingsPanel.gameObject.activeInHierarchy)
        {
            StartCoroutine(MoveUiRoutine(activeSettingsPanel, new Vector2(-currentExitDistance, 0), currentExitDuration, exitCurveSettingsPanel));
        }

        // 3. Двигаем Статистику и Лидерборд (Влево если Портрет, Вверх если Ландшафт)
        Vector2 dynamicExitOffset = isPortrait ? new Vector2(-currentExitDistance, 0) : new Vector2(0, topUiDistance);
        float dynamicExitDuration = isPortrait ? currentExitDuration : topUiFlyDuration;
        AnimationCurve dynamicExitCurve = isPortrait ? exitCurveSettingsPanel : topUiCurve;

        if (leaderboardBtn != null) StartCoroutine(MoveUiRoutine(leaderboardBtn, dynamicExitOffset, dynamicExitDuration, dynamicExitCurve));
        if (statsBtn != null) StartCoroutine(MoveUiRoutine(statsBtn, dynamicExitOffset, dynamicExitDuration, dynamicExitCurve));

        // --- ПАДЕНИЕ ЛИШНИХ КАРТ ---
        float maxFallDelay = 0f;
        foreach (var card in cardsToFall)
        {
            StartCoroutine(FallCardRoutine(card, maxFallDelay));
            maxFallDelay += delayBetweenFalls;
        }

        // --- АНИМАЦИЯ ВЫБРАННОЙ КАРТЫ (передаем правильную дистанцию) ---
        if (selectedCardRect != null)
        {
            StartCoroutine(AnimateSelectedCardComplexSequence(selectedCardRect, currentExitDistance));
        }

        float fallFinishTime = maxFallDelay + fallDuration;
        float selectedAnimTotalTime = moveToCenterDuration + pauseInCenterDuration + flyOutOfCenterDuration;
        float maxWaitTime = Mathf.Max(topUiFlyDuration, currentExitDuration, fallFinishTime, selectedAnimTotalTime);

        yield return new WaitForSeconds(maxWaitTime);
        onComplete?.Invoke();
    }

    private IEnumerator AnimateSelectedCardComplexSequence(RectTransform card, float targetExitDistanceX)
    {
        var hover = card.GetComponent<CardHoverEffect>();
        if (hover != null) hover.SetSelectedMode(false);

        if (centerMarker == null)
            Debug.LogWarning("ВНИМАНИЕ: centerMarker не назначен в инспекторе! Пожалуйста, перетащи туда пустой объект.");

        Vector3 startPos = card.position;
        Vector3 targetCenterPos = centerMarker != null ? centerMarker.position : startPos;

        Quaternion startRot = card.localRotation;
        Vector3 startScale = card.localScale;
        Vector3 maxScale = startScale * maxScaleMultiplier;

        float elapsed = 0f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", moveToCenterDuration, 0.1f);

        // === ЭТАП 1: ПОЛЕТ В ЦЕНТР ===
        while (elapsed < moveToCenterDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveToCenterDuration;

            float smoothedT = Mathf.SmoothStep(0f, 1f, t);
            card.position = Vector3.Lerp(startPos, targetCenterPos, smoothedT);

            float zRotation = Mathf.Lerp(0, 360, smoothedT);
            card.localRotation = startRot * Quaternion.Euler(0, 0, zRotation);

            card.localScale = Vector3.Lerp(startScale, maxScale, smoothedT);

            yield return null;
        }

        card.position = targetCenterPos;
        card.localRotation = Quaternion.identity;
        card.localScale = maxScale;

        // === ЭТАП 2: ЗАДЕРЖКА В ЦЕНТРЕ (Покачивание) ===
        elapsed = 0f;
        while (elapsed < pauseInCenterDuration)
        {
            elapsed += Time.deltaTime;
            float zWobble = Mathf.Sin(elapsed * wobbleSpeed) * wobbleAngle;
            card.localRotation = Quaternion.Euler(0, 0, zWobble);
            yield return null;
        }

        Quaternion startOutRot = card.localRotation;
        if (AudioManager.Instance != null)
        {
            float fadeTime = 0.3f;
            float delay = flyOutOfCenterDuration - fadeTime;
            if (delay < 0) delay = 0;
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_Out", delay, fadeTime);
        }

        // === ЭТАП 3: ВЫЛЕТ ВЛЕВО (Используем динамическую дистанцию) ===
        float worldExitDistanceX = targetExitDistanceX * card.lossyScale.x;
        Vector3 exitTargetPos = targetCenterPos + new Vector3(-worldExitDistanceX, 0f, 0f);
        Quaternion exitRot = Quaternion.Euler(0, 0, finalExitRotation);

        elapsed = 0f;

        while (elapsed < flyOutOfCenterDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flyOutOfCenterDuration;

            float acceleratedT = t * t;
            card.position = Vector3.LerpUnclamped(targetCenterPos, exitTargetPos, acceleratedT);
            card.localScale = Vector3.Lerp(maxScale, startScale, acceleratedT);
            card.localRotation = Quaternion.Lerp(startOutRot, exitRot, t);

            yield return null;
        }

        card.position = exitTargetPos;
        card.localRotation = exitRot;
        card.localScale = startScale;
        card.gameObject.SetActive(false);
    }

    private IEnumerator FallCardRoutine(RectTransform card, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);

        Vector2 startPos = card.anchoredPosition;
        Vector2 targetPos = startPos - new Vector2(0, fallDistance);

        Quaternion startRot = card.localRotation;
        Quaternion targetRot = startRot * Quaternion.Euler(0, 0, fallRotation);

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallDuration;
            float curveT = fallCurve.Evaluate(t);

            card.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, curveT);
            card.localRotation = Quaternion.LerpUnclamped(startRot, targetRot, curveT);
            yield return null;
        }
        card.anchoredPosition = targetPos;
    }

    private IEnumerator MoveUiRoutine(RectTransform target, Vector2 offset, float duration, AnimationCurve curve)
    {
        Vector2 startPos = target.anchoredPosition;
        Vector2 targetPos = startPos + offset;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curveT = curve.Evaluate(t);

            target.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, curveT);
            yield return null;
        }
        target.anchoredPosition = targetPos;
    }

    private void DisableAllInteractions(RectTransform selectedCard)
    {
        var hovers = FindObjectsOfType<CardHoverEffect>();
        foreach (var h in hovers)
        {
            h.SetHoverEnabled(false);
            if (h.transform as RectTransform != selectedCard)
            {
                h.SetSelectedMode(false);
            }
        }
    }
}