using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Shadow))]
public class TriPeaksCardShadow : MonoBehaviour
{
    public enum ShadowState { Resting, Flying }

    private Shadow shadow;

    [Header("Standard Shadow")]
    public Vector2 restingDistance = new Vector2(2f, -2f);
    public Vector2 flyingDistance = new Vector2(10f, -10f); // Тень отдаляется в полете
    [Range(0f, 1f)] public float restingAlpha = 0.4f;
    [Range(0f, 1f)] public float flyingAlpha = 0.25f;

    public float transitionSpeed = 15f;

    private ShadowState currentState = ShadowState.Resting;
    private Vector2 currentLogicalDistance;
    private float currentAlpha;

    private void Awake()
    {
        // Очистка от старых скриптов, чтобы не было конфликтов
        var old1 = GetComponent<CardShadowController>(); if (old1) Destroy(old1);
        var old3 = GetComponent<PyramidShadowController>(); if (old3) Destroy(old3);

        shadow = GetComponent<Shadow>();
        currentLogicalDistance = restingDistance;
        currentAlpha = 0f;
    }

    public void SetState(ShadowState state)
    {
        currentState = state;
    }

    private void Update()
    {
        Vector2 targetDistance = restingDistance;
        float targetAlpha = restingAlpha;

        // --- УМНАЯ АВТО-МАСКИРОВКА ---
        if (currentState == ShadowState.Resting && transform.parent != null)
        {
            string parentName = transform.parent.name;

            // 1. Прячем индивидуальную тень у всех карт в Stock (там работает глобальная тень)
            if (parentName.Contains("Stock"))
            {
                targetAlpha = 0f;
            }
            // 2. Прячем тень у карт в Waste, КРОМЕ самой нижней (первая карта имеет индекс 0)
            else if (parentName.Contains("Waste") && transform.GetSiblingIndex() > 0)
            {
                targetAlpha = 0f;
            }
        }
        else if (currentState == ShadowState.Flying)
        {
            targetDistance = flyingDistance;
            targetAlpha = flyingAlpha;
        }

        // Плавная смена значений
        float dt = Time.deltaTime * transitionSpeed;
        currentLogicalDistance = Vector2.Lerp(currentLogicalDistance, targetDistance, dt);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, dt);

        // Компенсация вращения в полете (тень всегда падает вправо-вниз)
        Vector2 appliedDistance = currentLogicalDistance;
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

        shadow.effectDistance = appliedDistance;
        Color c = shadow.effectColor;
        c.a = currentAlpha;
        shadow.effectColor = c;
        shadow.enabled = (currentAlpha > 0.01f); // Полностью выключаем компонент для оптимизации
    }
}