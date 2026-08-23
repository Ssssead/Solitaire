using UnityEngine;
using System.Collections;
using System;

public class SettingsPanelAnimator : MonoBehaviour
{
    [Header("Animation Durations")]
    public float landscapeDuration = 0.4f; // Должно совпадать с длительностью полета карт
    public float portraitDuration = 0.4f; // В вертикальном быстрее
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Off-Screen Offsets (Смещение влево)")]
    public float landscapeXOffset = -1500f;
    public float portraitXOffset = -2500f; // В вертикальном улетает дальше

    private RectTransform rectTransform;
    private Vector2 onScreenPos; // Позиция "В центре"
    private Vector2 landscapeOffScreenPos;
    private Vector2 portraitOffScreenPos;

    private Coroutine currentRoutine;
    private bool isPanelOpen = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // Запоминаем ту позицию, где панель стоит в редакторе (это будет конечная точка "Открыто")
        onScreenPos = rectTransform.anchoredPosition;

        // Фиксируем точки вылета за экран
        landscapeOffScreenPos = new Vector2(landscapeXOffset, onScreenPos.y);
        portraitOffScreenPos = new Vector2(portraitXOffset, onScreenPos.y);
    }

    // Вспомогательные методы для получения текущих параметров в зависимости от ориентации
    private bool IsPortrait() => Screen.width < Screen.height;
    private float CurrentDuration => IsPortrait() ? portraitDuration : landscapeDuration;
    private Vector2 CurrentOffScreenPos => IsPortrait() ? portraitOffScreenPos : landscapeOffScreenPos;

    /// <summary>
    /// Просто открыть панель (выезд слева)
    /// </summary>
    public void AnimateOpen()
    {
        gameObject.SetActive(true);
        isPanelOpen = true;

        if (currentRoutine != null) StopCoroutine(currentRoutine);

        float duration = CurrentDuration;
        Vector2 offScreenPos = CurrentOffScreenPos;

        // Ставим в позицию "за кадром" и запускаем анимацию "в кадр"
        rectTransform.anchoredPosition = offScreenPos;
        currentRoutine = StartCoroutine(MoveRoutine(offScreenPos, onScreenPos, duration));
    }

    /// <summary>
    /// Закрыть панель (уезд влево)
    /// </summary>
    public void AnimateClose()
    {
        isPanelOpen = false;
        if (currentRoutine != null) StopCoroutine(currentRoutine);

        float duration = CurrentDuration;
        Vector2 offScreenPos = CurrentOffScreenPos;

        // Едем из текущей позиции "за кадр"
        currentRoutine = StartCoroutine(MoveRoutine(rectTransform.anchoredPosition, offScreenPos, duration, () =>
        {
            gameObject.SetActive(false); // Выключаем объект после анимации
        }));
    }

    /// <summary>
    /// СМЕНА ИГРЫ: Уехать влево -> Обновить данные -> Выехать слева
    /// </summary>
    /// <param name="onUpdateContent">Метод обновления UI (название игры, кнопки)</param>
    public void AnimateSwitch(Action onUpdateContent)
    {
        gameObject.SetActive(true);
        isPanelOpen = true;

        if (currentRoutine != null) StopCoroutine(currentRoutine);

        // Делим текущее время: 50% на выезд, 50% на въезд.
        float halfDuration = CurrentDuration / 2f;
        Vector2 offScreenPos = CurrentOffScreenPos;

        currentRoutine = StartCoroutine(SwitchRoutine(halfDuration, offScreenPos, onUpdateContent));
    }

    private IEnumerator SwitchRoutine(float halfDuration, Vector2 offScreenPos, Action onUpdateContent)
    {
        // 1. Уезжаем влево
        yield return MoveRoutine(rectTransform.anchoredPosition, offScreenPos, halfDuration);

        // 2. Пока мы за кадром — обновляем текст/кнопки
        onUpdateContent?.Invoke();

        // 3. Выезжаем обратно (слева направо)
        yield return MoveRoutine(offScreenPos, onScreenPos, halfDuration);
    }

    private IEnumerator MoveRoutine(Vector2 start, Vector2 end, float time, Action onComplete = null)
    {
        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / time;

            // Кривая плавности
            float curveT = motionCurve.Evaluate(t);

            rectTransform.anchoredPosition = Vector2.Lerp(start, end, curveT);
            yield return null;
        }
        rectTransform.anchoredPosition = end;
        onComplete?.Invoke();
    }

    public bool IsOpen() => isPanelOpen;
}