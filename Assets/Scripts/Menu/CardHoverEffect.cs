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
    private bool isInteractionAllowed = true;
    private bool isSelectedMode = false;

    private float swingTimer;
    private float bobTimer;

    private void Awake()
    {
        if (targetButton == null) targetButton = GetComponent<Button>();
    }

    // --- Управление режимами ---

    public void SetHoverEnabled(bool isEnabled)
    {
        isInteractionAllowed = isEnabled;

        // Расширенная проверка: глушим эффект, если есть корутина ИЛИ если карточка считает себя isHovering
        if (!isEnabled && (isHovering || activeCoroutine != null) && !isSelectedMode)
        {
            ForceStop();
        }
    }

    public void SetSelectedMode(bool selected)
    {
        isSelectedMode = selected;

        if (selected)
        {
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);

            targetHoverPos = transform.localPosition;
            swingTimer = 0f;
            bobTimer = 0f;
            isHovering = true;

            activeCoroutine = StartCoroutine(IdleHoverRoutine());
        }
        else
        {
            isHovering = false;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);

            activeCoroutine = null;
            transform.localRotation = Quaternion.identity;
        }
    }

    private void ForceStop()
    {
        isHovering = false;

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        // ---> КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ 2: Мгновенно возвращаем не только масштаб, 
        // но и ПОЗИЦИЮ с ВРАЩЕНИЕМ. Это дает CardAnimationController'у идеально 
        // "чистую" карту перед началом перестроения.
        if (baseScale != Vector3.zero)
        {
            transform.localScale = baseScale;
            transform.localPosition = basePos;
            transform.localRotation = baseRot;
        }
    }

    // --- События мыши ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInteractionAllowed || isHovering || isSelectedMode) return;

        bool isButtonActive = (targetButton == null || targetButton.interactable);
        if (!isButtonActive && !workEvenIfDisabled) return;

        if (activeCoroutine == null)
        {
            baseScale = transform.localScale;
            basePos = transform.localPosition;
            baseRot = transform.localRotation;

            targetHoverScale = baseScale * hoverScaleMult;
            targetHoverPos = basePos + new Vector3(0, hoverYOffset, 0);

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Hover");
        }

        isHovering = true;
        swingTimer = 0f;
        bobTimer = 0f;

        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        activeCoroutine = StartCoroutine(TransitionInRoutine());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
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
        while (isHovering)
        {
            swingTimer += Time.deltaTime * swingSpeed;
            bobTimer += Time.deltaTime * bobSpeed;

            float zAngle = Mathf.Sin(swingTimer) * swingAngle;
            float yOffset = Mathf.Sin(bobTimer) * bobAmount;

            transform.localRotation = Quaternion.Euler(0, 0, zAngle);
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

        activeCoroutine = null;
    }
}