using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;

public class MenuExitController : MonoBehaviour
{
    [Header("Dependencies")]
    public CardAnimationController cardAnimator;
    public RectTransform settingsPanelRect; // Панель настроек
    public List<RectTransform> topUiElements; // Верхние кнопки и панель игрока

    [Header("Phase 1: Falling Cards")]
    public float fallDuration = 0.5f;
    public float delayBetweenFalls = 0.05f;
    public float fallDistance = 1500f; // Насколько вниз падают карты
    public float fallRotation = 30f;   // Поворот влево (Z +30)

    // ИСПРАВЛЕНИЕ: Инициализируем обычной линейной кривой
    public AnimationCurve fallCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Phase 1: Top UI Fly Up")]
    public float topUiFlyDuration = 0.5f;
    public float topUiDistance = 500f;
    public AnimationCurve topUiCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Phase 2: Exit (Panel & Card)")]
    public float exitMoveDuration = 0.6f;
    public float exitDistanceX = 1500f; // Насколько далеко разлетаются

    // ИСПРАВЛЕНИЕ: Инициализируем обычной линейной кривой
    public AnimationCurve exitCurve = AnimationCurve.Linear(0, 0, 1, 1);

    /// <summary>
    /// Запускает всю последовательность выхода
    /// </summary>
    public void PlayExitAnimation(GameType selectedGame, Action onComplete)
    {
        StartCoroutine(ExitSequence(selectedGame, onComplete));
    }

    private IEnumerator ExitSequence(GameType selectedGame, Action onComplete)
    {
        // 1. Выключаем взаимодействие
        DisableAllInteractions();

        // 2. Сортируем карты: находим выбранную и список остальных
        RectTransform selectedCardRect = null;
        List<RectTransform> cardsToFall = new List<RectTransform>();

        // Берем список из аниматора
        foreach (var entry in cardAnimator.allCards)
        {
            if (entry.rect == null) continue;

            if (entry.type == selectedGame)
            {
                selectedCardRect = entry.rect;
            }
            else
            {
                cardsToFall.Add(entry.rect);
            }
        }

        // --- ФАЗА 1: Падение карт и улет верха ---

        // Запускаем улет верхнего UI
        foreach (var ui in topUiElements)
        {
            if (ui != null) StartCoroutine(MoveUiRoutine(ui, new Vector2(0, topUiDistance), topUiFlyDuration, topUiCurve));
        }

        // Запускаем падение карт по очереди
        foreach (var card in cardsToFall)
        {
            StartCoroutine(FallCardRoutine(card));
            yield return new WaitForSeconds(delayBetweenFalls);
        }

        // Ждем пока упадут карты
        yield return new WaitForSeconds(fallDuration);


        // --- ФАЗА 2: Разлет Панели и Выбранной карты ---

        // Панель настроек летит ВЛЕВО
        if (settingsPanelRect != null)
        {
            StartCoroutine(MoveUiRoutine(settingsPanelRect, new Vector2(-exitDistanceX, 0), exitMoveDuration, exitCurve));
        }

        // Выбранная карта летит ВПРАВО
        if (selectedCardRect != null)
        {
            StartCoroutine(MoveUiRoutine(selectedCardRect, new Vector2(exitDistanceX, 0), exitMoveDuration, exitCurve));
        }

        // Ждем окончания разлета
        yield return new WaitForSeconds(exitMoveDuration);

        // --- ФИНАЛ: Загрузка сцены ---
        onComplete?.Invoke();
    }

    // Логика падения карты (Вниз + Поворот влево)
    private IEnumerator FallCardRoutine(RectTransform card)
    {
        Vector2 startPos = card.anchoredPosition;
        Vector2 targetPos = startPos - new Vector2(0, fallDistance);

        Quaternion startRot = card.localRotation;
        // Поворот влево (против часовой) = положительный Z
        Quaternion targetRot = startRot * Quaternion.Euler(0, 0, fallRotation);

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallDuration;

            // Используем LerpUnclamped, чтобы кривая могла выходить за пределы 0..1 (для эффекта подскока)
            float curveT = fallCurve.Evaluate(t);

            card.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, curveT);
            card.localRotation = Quaternion.LerpUnclamped(startRot, targetRot, curveT);

            yield return null;
        }
        card.anchoredPosition = targetPos;
    }

    // Логика простого перемещения (для UI и разлета)
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

    private void DisableAllInteractions()
    {
        // Выключаем ховеры у всех карт
        var hovers = FindObjectsOfType<CardHoverEffect>();
        foreach (var h in hovers) h.SetHoverEnabled(false);
    }
}