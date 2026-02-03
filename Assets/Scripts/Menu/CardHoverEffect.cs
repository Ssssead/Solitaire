using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Transition Settings")]
    public float hoverScaleMult = 1.1f;
    public float hoverYOffset = 20f;
    public float transitionSpeed = 10f;

    [Header("Idle Swing Settings")]
    public float swingAngle = 2.5f;
    public float swingSpeed = 3f;
    public float bobAmount = 5f;
    public float bobSpeed = 2f;

    [Header("Requirements")]
    public Button targetButton;
    public bool workEvenIfDisabled = true;

    // Внутренние переменные
    private Vector3 baseScale;
    private Vector3 basePos;
    private Quaternion baseRot;
    private Vector3 targetHoverPos;
    private Vector3 targetHoverScale;

    private Coroutine activeCoroutine;
    private bool isHovering = false;
    private bool isInteractionAllowed = true; // Разрешен ли ховер вообще
    private bool isSelectedMode = false;      // <--- НОВЫЙ ФЛАГ: Карта выбрана

    private float swingTimer;
    private float bobTimer;
    private Canvas overrideCanvas;

    private void Awake()
    {
        if (targetButton == null) targetButton = GetComponent<Button>();
    }

    // --- Управление режимами ---

    public void SetHoverEnabled(bool isEnabled)
    {
        isInteractionAllowed = isEnabled;
        if (!isEnabled && isHovering && !isSelectedMode)
        {
            ForceStop();
        }
    }

    /// <summary>
    /// Включает режим "Выбрана": карта плавает сама по себе, мышь игнорируется.
    /// </summary>
    public void SetSelectedMode(bool selected)
    {
        isSelectedMode = selected;

        if (selected)
        {
            // 1. Останавливаем любые текущие анимации
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);

            // 2. Запоминаем текущую позицию (это уже позиция в Preview Anchor) как центр колебаний
            targetHoverPos = transform.localPosition;

            // 3. Сбрасываем таймеры, чтобы движение было плавным
            swingTimer = 0f;
            bobTimer = 0f;

            // 4. Запускаем вечное покачивание
            isHovering = true;
            activeCoroutine = StartCoroutine(IdleHoverRoutine());
        }
        else
        {
            // Выключаем режим
            isHovering = false;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);

            // Сбрасываем вращение в ноль, чтобы карта вернулась ровной
            transform.localRotation = Quaternion.identity;
        }
    }

    private void ForceStop()
    {
        isHovering = false;
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
    }

    // --- Приоритет отрисовки (Sorting Order) ---
    public void SetPriorityRender(bool enable)
    {
        if (overrideCanvas == null)
        {
            overrideCanvas = GetComponent<Canvas>();
            if (overrideCanvas == null) overrideCanvas = gameObject.AddComponent<Canvas>();
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        }

        if (enable)
        {
            overrideCanvas.overrideSorting = true;
            overrideCanvas.sortingOrder = 100;
        }
        else
        {
            overrideCanvas.overrideSorting = false;
            overrideCanvas.sortingOrder = 0;
        }
    }

    // --- События мыши ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Если запрещено, или уже наведена, или В РЕЖИМЕ SELECTED - игнорируем мышь
        if (!isInteractionAllowed || isHovering || isSelectedMode) return;

        bool isButtonActive = (targetButton == null || targetButton.interactable);
        if (!isButtonActive && !workEvenIfDisabled) return;

        isHovering = true;

        baseScale = transform.localScale;
        basePos = transform.localPosition;
        baseRot = transform.localRotation;

        targetHoverScale = baseScale * hoverScaleMult;
        targetHoverPos = basePos + new Vector3(0, hoverYOffset, 0);

        swingTimer = 0f;
        bobTimer = 0f;

        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(TransitionInRoutine());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Если карта выбрана (SelectedMode), мышь на нее не влияет
        if (isSelectedMode) return;

        if (!isInteractionAllowed || !isHovering) return;
        isHovering = false;

        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(TransitionOutRoutine());
    }

    // --- Корутины ---

    private IEnumerator TransitionInRoutine()
    {
        float t = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;
        Quaternion targetRot = Quaternion.identity;

        while (t < 1f)
        {
            t += Time.deltaTime * transitionSpeed;
            transform.localScale = Vector3.Lerp(startScale, targetHoverScale, t);
            transform.localPosition = Vector3.Lerp(startPos, targetHoverPos, t);
            transform.localRotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }

        transform.localScale = targetHoverScale;
        transform.localPosition = targetHoverPos;
        transform.localRotation = targetRot;

        activeCoroutine = StartCoroutine(IdleHoverRoutine());
    }

    private IEnumerator IdleHoverRoutine()
    {
        // Бесконечный цикл покачивания
        while (isHovering)
        {
            swingTimer += Time.deltaTime * swingSpeed;
            bobTimer += Time.deltaTime * bobSpeed;

            float zAngle = Mathf.Sin(swingTimer) * swingAngle;
            float yOffset = Mathf.Sin(bobTimer) * bobAmount;

            transform.localRotation = Quaternion.Euler(0, 0, zAngle);
            // Колеблемся вокруг targetHoverPos
            transform.localPosition = targetHoverPos + new Vector3(0, yOffset, 0);

            yield return null;
        }
    }

    private IEnumerator TransitionOutRoutine()
    {
        float t = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;

        while (t < 1f)
        {
            t += Time.deltaTime * transitionSpeed;
            transform.localScale = Vector3.Lerp(startScale, baseScale, t);
            transform.localPosition = Vector3.Lerp(startPos, basePos, t);
            transform.localRotation = Quaternion.Lerp(startRot, baseRot, t);
            yield return null;
        }

        transform.localScale = baseScale;
        transform.localPosition = basePos;
        transform.localRotation = baseRot;
    }
}