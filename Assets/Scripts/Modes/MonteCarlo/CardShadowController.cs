using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Shadow))]
public class CardShadowController : MonoBehaviour
{
    private Shadow shadow;

    [Header("Shadow Settings")]
    public Vector2 restingDistance = new Vector2(1f, -1f);
    public Vector2 flyingDistance = new Vector2(5f, -5f);

    [Range(0f, 1f)] public float restingAlpha = 0.4f; // Стандартная прозрачность (около 100/255)
    [Range(0f, 1f)] public float flyingAlpha = 65f / 255f; // Точно 65, как вы просили

    public float transitionSpeed = 15f;

    private bool isFlying = false;

    private void Awake()
    {
        shadow = GetComponent<Shadow>();
        shadow.effectDistance = restingDistance;
        SetAlpha(restingAlpha);
    }

    public void SetFlying(bool flying)
    {
        if (isFlying == flying) return;
        isFlying = flying;
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine());
    }

    public void SetShadowVisible(bool visible)
    {
        if (shadow == null) shadow = GetComponent<Shadow>();
        shadow.enabled = visible;
    }

    private void SetAlpha(float alpha)
    {
        Color c = shadow.effectColor;
        c.a = alpha;
        shadow.effectColor = c;
    }

    private IEnumerator TransitionRoutine()
    {
        Vector2 targetDistance = isFlying ? flyingDistance : restingDistance;
        float targetAlpha = isFlying ? flyingAlpha : restingAlpha;

        while (true)
        {
            shadow.effectDistance = Vector2.Lerp(shadow.effectDistance, targetDistance, Time.deltaTime * transitionSpeed);

            Color c = shadow.effectColor;
            c.a = Mathf.Lerp(c.a, targetAlpha, Time.deltaTime * transitionSpeed);
            shadow.effectColor = c;

            if (Vector2.Distance(shadow.effectDistance, targetDistance) < 0.05f && Mathf.Abs(c.a - targetAlpha) < 0.01f)
            {
                shadow.effectDistance = targetDistance;
                SetAlpha(targetAlpha);
                break;
            }
            yield return null;
        }
    }
}