using UnityEngine;
using System.Collections;
using System;

[RequireComponent(typeof(CanvasGroup))] // ������������� ������� CanvasGroup, ���� ��� ���
public class SettingsPanelAnimator : MonoBehaviour
{
    [Header("Animation Durations")]
    public float landscapeDuration = 0.4f;
    public float portraitDuration = 0.4f;
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Off-Screen Offsets (�������� �����)")]
    public float landscapeXOffset = -1500f;
    public float portraitXOffset = -2500f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup; // �����: ������ �� CanvasGroup
    private Vector2 onScreenPos;
    private Vector2 landscapeOffScreenPos;
    private Vector2 portraitOffScreenPos;

    private Coroutine currentRoutine;
    private bool isPanelOpen = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>(); // �����: �������� ���������

        // �� ��������� ������ ������, �� ������� � ��������
        SetPanelVisible(false);

        onScreenPos = rectTransform.anchoredPosition;
        landscapeOffScreenPos = new Vector2(landscapeXOffset, onScreenPos.y);
        portraitOffScreenPos = new Vector2(portraitXOffset, onScreenPos.y);
    }

    private bool IsPortrait() => Screen.width < Screen.height;
    private float CurrentDuration => IsPortrait() ? portraitDuration : landscapeDuration;
    private Vector2 CurrentOffScreenPos => IsPortrait() ? portraitOffScreenPos : landscapeOffScreenPos;

    // �����: ����� ��� ���������� ���������� ��� SetActive
    private void SetPanelVisible(bool isVisible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = isVisible ? 1f : 0f;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    public void AnimateOpen()
    {
        gameObject.SetActive(true); // <--- ������ ��� ������
        SetPanelVisible(true);
        isPanelOpen = true;

        if (currentRoutine != null) StopCoroutine(currentRoutine);

        float duration = CurrentDuration;
        Vector2 offScreenPos = CurrentOffScreenPos;

        rectTransform.anchoredPosition = offScreenPos;
        currentRoutine = StartCoroutine(MoveRoutine(offScreenPos, onScreenPos, duration));
    }

    public void AnimateClose()
    {
        isPanelOpen = false;
        if (currentRoutine != null) StopCoroutine(currentRoutine);

        float duration = CurrentDuration;
        Vector2 offScreenPos = CurrentOffScreenPos;

        currentRoutine = StartCoroutine(MoveRoutine(rectTransform.anchoredPosition, offScreenPos, duration, () =>
        {
            // �����������: ������ ������ ����� CanvasGroup
            SetPanelVisible(false);
        }));
    }

    public void AnimateSwitch(Action onUpdateContent)
    {
        gameObject.SetActive(true); // <--- � ���� ���� ������
        SetPanelVisible(true);
        isPanelOpen = true;

        if (currentRoutine != null) StopCoroutine(currentRoutine);

        float halfDuration = CurrentDuration / 2f;
        Vector2 offScreenPos = CurrentOffScreenPos;

        currentRoutine = StartCoroutine(SwitchRoutine(halfDuration, offScreenPos, onUpdateContent));
    }

    private IEnumerator SwitchRoutine(float halfDuration, Vector2 offScreenPos, Action onUpdateContent)
    {
        yield return MoveRoutine(rectTransform.anchoredPosition, offScreenPos, halfDuration);
        onUpdateContent?.Invoke();
        yield return MoveRoutine(offScreenPos, onScreenPos, halfDuration);
    }

    private IEnumerator MoveRoutine(Vector2 start, Vector2 end, float time, Action onComplete = null)
    {
        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / time;
            float curveT = motionCurve.Evaluate(t);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(start, end, curveT); // �����������: LerpUnclamped �������
            yield return null;
        }
        rectTransform.anchoredPosition = end;
        onComplete?.Invoke();
    }

    public bool IsOpen() => isPanelOpen;

    // Используется при смене ориентации: панель нужно скрыть МГНОВЕННО (она всё равно
    // тут же откроется заново в другой ориентации через AnimateOpen), но без этого метода
    // isPanelOpen оставался бы true навсегда на той стороне, которую выключили raw SetActive(false),
    // и следующий IsSettingsPanelOpen() ошибочно решал бы, что настройки ещё открыты.
    public void ForceClose()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        isPanelOpen = false;
        SetPanelVisible(false);
    }
}