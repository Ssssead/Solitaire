using UnityEngine;
using System.Collections;
using System;

public class SettingsPanelAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    public float animationDuration = 0.4f; // Должно совпадать с длительностью полета карт
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Positions")]
    [Tooltip("Если 0, то смещение будет вычислено автоматически по ширине экрана")]
    public float offScreenXOffset = 0f;

    private RectTransform rectTransform;
    private Vector2 onScreenPos; // Позиция "В центре" (или где вы поставили в редакторе)
    private Vector2 offScreenPos; // Позиция "Слева за экраном"

    private Coroutine currentRoutine;
    private bool isPanelOpen = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // Запоминаем ту позицию, где панель стоит в редакторе (это будет конечная точка "Открыто")
        onScreenPos = rectTransform.anchoredPosition;

        // Вычисляем позицию за экраном (слева)
        // Если вы не задали вручную, берем ширину экрана с запасом
        float width = rectTransform.rect.width;
        if (offScreenXOffset == 0)
        {
            // Сдвигаем влево на ширину панели + немного запаса
            offScreenXOffset = -width - 100f;

            // Если анкоры по центру, возможно, нужно сдвигать сильнее (на пол-экрана)
            // Для надежности сдвинем сильно влево
            if (offScreenXOffset > -1000) offScreenXOffset = -1500f;
        }

        offScreenPos = new Vector2(offScreenXOffset, onScreenPos.y);
    }

    /// <summary>
    /// Просто открыть панель (выезд слева)
    /// </summary>
    public void AnimateOpen()
    {
        gameObject.SetActive(true);
        isPanelOpen = true;

        if (currentRoutine != null) StopCoroutine(currentRoutine);

        // Ставим в позицию "за кадром" и запускаем анимацию "в кадр"
        rectTransform.anchoredPosition = offScreenPos;
        currentRoutine = StartCoroutine(MoveRoutine(offScreenPos, onScreenPos, animationDuration));
    }

    /// <summary>
    /// Закрыть панель (уезд влево)
    /// </summary>
    public void AnimateClose()
    {
        isPanelOpen = false;
        if (currentRoutine != null) StopCoroutine(currentRoutine);

        // Едем из текущей позиции "за кадр"
        currentRoutine = StartCoroutine(MoveRoutine(rectTransform.anchoredPosition, offScreenPos, animationDuration, () =>
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

        // Поскольку нам нужно успеть за 0.4 секунды (время карт), 
        // делим время: 50% на выезд, 50% на въезд.
        float halfDuration = animationDuration / 2f;

        currentRoutine = StartCoroutine(SwitchRoutine(halfDuration, onUpdateContent));
    }

    private IEnumerator SwitchRoutine(float duration, Action onUpdateContent)
    {
        // 1. Уезжаем влево
        yield return MoveRoutine(rectTransform.anchoredPosition, offScreenPos, duration);

        // 2. Пока мы за кадром — обновляем текст/кнопки
        onUpdateContent?.Invoke();

        // 3. Выезжаем обратно (слева направо)
        yield return MoveRoutine(offScreenPos, onScreenPos, duration);
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