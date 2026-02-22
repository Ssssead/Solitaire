using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SceneExitAnimator : MonoBehaviour
{
    [Header("Game References")]
    public PileManager pileManager;

    [Header("Animation Settings")]
    [Tooltip("Сила притяжения (чем больше минус, тем быстрее разгон вниз)")]
    public float gravity = -1500f;
    [Tooltip("Максимальная скорость вращения карт")]
    public float maxRotationSpeed = 100f;

    private IIntroController introController;

    private class FallingCard
    {
        public Transform transform;
        public Vector3 velocity;
        public float rotationDir;
        public float delay;
    }

    private void Awake()
    {
        introController = GetComponent<IIntroController>();
        if (introController == null)
        {
            foreach (var comp in FindObjectsOfType<MonoBehaviour>())
            {
                if (comp is IIntroController ic) { introController = ic; break; }
            }
        }
    }

    public void PlayExitSequence(System.Action onComplete)
    {
        StartCoroutine(AnimateOutRoutine(onComplete, true)); // isExit = true
    }

    public void PlayRestartSequence(System.Action onComplete)
    {
        StartCoroutine(AnimateOutRoutine(onComplete, false)); // isExit = false
    }

    private IEnumerator AnimateOutRoutine(System.Action onComplete, bool isExit)
    {
        // 1. ИСЧЕЗНОВЕНИЕ ИНТЕРФЕЙСА И СЛОТОВ ТОЛЬКО ПРИ ВЫХОДЕ В МЕНЮ
        if (isExit)
        {
            if (introController != null)
            {
                foreach (var el in introController.GetTopUIElements())
                    if (el != null) StartCoroutine(AnimateUIElement(el, el.anchoredPosition, el.anchoredPosition + new Vector2(0, 300f), 0.4f));

                foreach (var el in introController.GetBottomUIElements())
                    if (el != null) StartCoroutine(AnimateUIElement(el, el.anchoredPosition, el.anchoredPosition + new Vector2(0, -300f), 0.4f));
            }

            // Прячем слоты ТОЛЬКО при выходе в меню
            if (pileManager != null)
            {
                foreach (var container in pileManager.GetAllContainers())
                {
                    var mono = container as MonoBehaviour;
                    if (mono != null)
                    {
                        var cg = mono.GetComponent<CanvasGroup>();
                        if (cg == null) cg = mono.gameObject.AddComponent<CanvasGroup>();
                        StartCoroutine(FadeCanvasGroup(cg, cg.alpha, 0f, 0.4f));
                    }
                }
            }
        }

        // 2. ПАДЕНИЕ КАРТ ПРОИСХОДИТ ВСЕГДА (и при рестарте, и при выходе)
        List<FallingCard> fallingCards = new List<FallingCard>();
        CardController[] allCards = FindObjectsOfType<CardController>();

        foreach (var card in allCards)
        {
            var group = card.GetComponent<CanvasGroup>();
            if (group) group.blocksRaycasts = false;

            Canvas parentCanvas = card.GetComponentInParent<Canvas>();
            if (parentCanvas != null) card.transform.SetParent(parentCanvas.transform, true);

            FallingCard fc = new FallingCard();
            fc.transform = card.transform;

            float driftX = Random.Range(-10f, 10f);
            float initialSpeedDown = Random.Range(-10f, -50f);
            fc.velocity = new Vector3(driftX, initialSpeedDown, 0);
            fc.rotationDir = Random.Range(-1f, 1f) * (maxRotationSpeed * 0.5f);
            fc.delay = Random.Range(0f, 0.15f);

            fallingCards.Add(fc);
        }

        // Даем картам время упасть
        float duration = 0.9f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            foreach (var fc in fallingCards)
            {
                if (fc.transform == null) continue;
                if (fc.delay > 0) { fc.delay -= dt; continue; }

                fc.velocity.y += gravity * dt;
                fc.transform.position += fc.velocity * dt;
                fc.transform.Rotate(0, 0, fc.rotationDir * dt);
            }
            yield return null;
        }

        onComplete?.Invoke();
    }

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(elapsed / duration));
            yield return null;
        }
        if (target != null) target.anchoredPosition = to;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (cg != null) cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        if (cg != null) cg.alpha = end;
    }
}