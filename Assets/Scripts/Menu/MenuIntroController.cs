using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MenuIntroController : MonoBehaviour
{
    [Header("References")]
    public List<RectTransform> cardObjects;
    public List<RectTransform> topUiElements;

    [Header("Dependencies")]
    public CardAnimationController cardAnimator;

    [Header("Dealing Settings")]
    public float cardFlyDuration = 0.8f;
    public float delayBetweenCards = 0.05f;

    [Tooltip("На сколько пикселей левее от своей финальной точки стартует карта")]
    public float startXOffset = -1200f;

    [Tooltip("На сколько пикселей ВЫШЕ своей финальной точки стартует верхний UI")]
    public float topUiStartOffset = 1000f; // <--- НОВОЕ ПОЛЕ (По умолчанию 1000)

    [Header("Speed Up Settings")]
    [Tooltip("Во сколько раз ускорить анимацию при клике экрана")]
    public float fastForwardMultiplier = 10f;

    [Header("Stack Appearance")]
    [Range(0, 50f)] public float messyPositionJitter = 15f;
    public float startRotation = 45f;
    [Range(0, 5f)] public float finalRandomRotation = 3f;

    [Header("Animation Curves")]
    public AnimationCurve cardFlyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve uiDropCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private List<Vector2> cardsFinalPositions = new List<Vector2>();
    private List<Vector2> topUiFinalPositions = new List<Vector2>();

    private float currentSpeedMultiplier = 1f;
    private bool isAnimating = false;
    private GameObject raycastBlocker;

    // ИСПРАВЛЕНИЕ: Ждем 1 кадр перед стартом, чтобы адаптивный Canvas принял правильный размер
    private void Start() // Убрали IEnumerator!
    {
        // Принудительно заставляем Unity пересчитать все размеры Canvas прямо сейчас, 
        // не дожидаясь следующего кадра!
        Canvas.ForceUpdateCanvases();

        // 1. Запоминаем финальные идеальные позиции
        foreach (var card in cardObjects)
        {
            if (card != null)
            {
                var hover = card.GetComponent<CardHoverEffect>();
                if (hover != null) hover.SetHoverEnabled(false);
                cardsFinalPositions.Add(card.anchoredPosition);
            }
        }

        foreach (var ui in topUiElements)
        {
            if (ui != null) topUiFinalPositions.Add(ui.anchoredPosition);
        }

        // 2. Раскидываем элементы за экран В ТОМ ЖЕ КАДРЕ
        PrepareStacks();

        // 3. Запускаем анимацию
        StartCoroutine(IntroSequenceRoutine());
    }

    private void Update()
    {
        if (isAnimating && Input.GetMouseButtonDown(0))
        {
            currentSpeedMultiplier = fastForwardMultiplier;
        }
    }

    private void PrepareStacks()
    {
        // Прячем верхний UI за экран, используя новую переменную
        foreach (var ui in topUiElements)
        {
            if (ui != null)
            {
                ui.anchoredPosition += new Vector2(0, topUiStartOffset);
            }
        }

        // Прячем карты влево (собирая их в красивые стопки)
        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] == null) continue;

            Vector2 finalPos = cardsFinalPositions[i];
            Vector2 stackPos = new Vector2(finalPos.x + startXOffset, finalPos.y);

            cardObjects[i].anchoredPosition = stackPos + new Vector2(
                Random.Range(-messyPositionJitter, messyPositionJitter),
                Random.Range(-messyPositionJitter, messyPositionJitter));

            cardObjects[i].localRotation = Quaternion.Euler(0, 0, startRotation + Random.Range(-10f, 10f));
        }
    }

    private IEnumerator IntroSequenceRoutine()
    {
        isAnimating = true;
        currentSpeedMultiplier = 1f;

        SetInputBlocker(true);

        yield return StartCoroutine(SmartWait(0.3f));

        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] == null) continue;
            Quaternion targetRot = Quaternion.Euler(0, 0, Random.Range(-finalRandomRotation, finalRandomRotation));

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

            StartCoroutine(AnimateCard(cardObjects[i], cardsFinalPositions[i], targetRot, cardFlyDuration));
            yield return StartCoroutine(SmartWait(delayBetweenCards));
        }

        yield return StartCoroutine(SmartWait(cardFlyDuration));

        if (cardAnimator != null)
        {
            cardAnimator.RefreshAllCards();
        }

        foreach (var ui in topUiElements)
        {
            if (ui != null) StartCoroutine(AnimateUi(ui, topUiFinalPositions[topUiElements.IndexOf(ui)], 0.5f));
            yield return StartCoroutine(SmartWait(0.1f));
        }

        yield return StartCoroutine(SmartWait(0.5f));

        EnableHoverEffects();

        SetInputBlocker(false);
        isAnimating = false;
    }

    private void EnableHoverEffects()
    {
        foreach (var card in cardObjects)
        {
            if (card != null)
            {
                var hover = card.GetComponent<CardHoverEffect>();
                if (hover != null) hover.SetHoverEnabled(true);
            }
        }
    }

    private IEnumerator SmartWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime * currentSpeedMultiplier;
            yield return null;
        }
    }

    private IEnumerator AnimateCard(RectTransform target, Vector2 destPos, Quaternion destRot, float duration)
    {
        Vector2 startPos = target.anchoredPosition;
        Quaternion startRot = target.localRotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime * currentSpeedMultiplier;
            float t = cardFlyCurve.Evaluate(elapsed / duration);
            target.anchoredPosition = Vector2.LerpUnclamped(startPos, destPos, t);
            target.localRotation = Quaternion.Lerp(startRot, destRot, t);
            yield return null;
        }
        target.anchoredPosition = destPos;
        target.localRotation = destRot;
    }

    private IEnumerator AnimateUi(RectTransform target, Vector2 destPos, float duration)
    {
        Vector2 startPos = target.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime * currentSpeedMultiplier;
            target.anchoredPosition = Vector2.Lerp(startPos, destPos, uiDropCurve.Evaluate(elapsed / duration));
            yield return null;
        }
        target.anchoredPosition = destPos;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Drop");
    }

    private void SetInputBlocker(bool active)
    {
        if (active)
        {
            if (raycastBlocker == null)
            {
                raycastBlocker = new GameObject("IntroInputBlocker");
                var rect = raycastBlocker.AddComponent<RectTransform>();

                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null) rect.SetParent(canvas.transform, false);
                else rect.SetParent(transform, false);

                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var img = raycastBlocker.AddComponent<Image>();
                img.color = Color.clear;
            }
            raycastBlocker.transform.SetAsLastSibling();
        }
        else
        {
            if (raycastBlocker != null) Destroy(raycastBlocker);
        }
    }

    private void OnDestroy()
    {
        if (raycastBlocker != null) Destroy(raycastBlocker);
    }
}