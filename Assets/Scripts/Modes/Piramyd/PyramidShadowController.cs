using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Shadow))]
public class PyramidShadowController : MonoBehaviour
{
    public enum ShadowState { Resting, Selected, Flying }

    private Shadow shadow;

    [Header("Shadow Settings")]
    public Vector2 restingDistance = new Vector2(1f, -1f);
    public Vector2 selectedDistance = new Vector2(5f, -5f);
    public Vector2 flyingDistance = new Vector2(5f, -5f);

    [Range(0f, 1f)] public float restingAlpha = 0.4f;
    [Range(0f, 1f)] public float flyingAlpha = 65f / 255f;

    public float transitionSpeed = 15f;

    private ShadowState currentState = ShadowState.Resting;
    private Vector2 currentLogicalDistance;
    private float currentAlpha;

    private void Awake()
    {
        // Уничтожаем базовый контроллер теней, чтобы скрипты не дрались друг с другом
        var oldController = GetComponent<CardShadowController>();
        if (oldController != null)
        {
            Destroy(oldController);
        }

        shadow = GetComponent<Shadow>();
        currentLogicalDistance = restingDistance;
        currentAlpha = restingAlpha;
        ApplyShadow(currentLogicalDistance, currentAlpha);
    }

    public void SetState(ShadowState state)
    {
        currentState = state;
    }

    private void Update()
    {
        if (shadow == null) return;

        // 1. Целевые значения
        Vector2 targetDistance = restingDistance;
        float targetAlpha = restingAlpha;

        if (currentState == ShadowState.Selected)
        {
            targetDistance = selectedDistance;
            targetAlpha = flyingAlpha;
        }
        else if (currentState == ShadowState.Flying)
        {
            targetDistance = flyingDistance;
            targetAlpha = flyingAlpha;
        }

        // 2. Плавная интерполяция
        currentLogicalDistance = Vector2.Lerp(currentLogicalDistance, targetDistance, Time.deltaTime * transitionSpeed);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * transitionSpeed);

        Vector2 appliedDistance = currentLogicalDistance;

        // 3. Компенсация вращения карты (чтобы тень всегда падала вниз-вправо)
        if (currentState == ShadowState.Flying)
        {
            float angle = -transform.eulerAngles.z * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            appliedDistance = new Vector2(
                currentLogicalDistance.x * cos - currentLogicalDistance.y * sin,
                currentLogicalDistance.x * sin + currentLogicalDistance.y * cos
            );
        }

        // 4. Применение
        ApplyShadow(appliedDistance, currentAlpha);
    }

    private void ApplyShadow(Vector2 dist, float alpha)
    {
        shadow.effectDistance = dist;
        Color c = shadow.effectColor;
        c.a = alpha;
        shadow.effectColor = c;
    }
}