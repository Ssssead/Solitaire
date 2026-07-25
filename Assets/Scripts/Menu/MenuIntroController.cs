using UnityEngine;
using UnityEngine.UI; // <--- ДОБАВЛЕНО для создания прозрачной панели (Image)
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
    public float startXOffset = -1200f;

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

    // Внутренние переменные для контроля анимации
    private float currentSpeedMultiplier = 1f;
    private bool isAnimating = false;
    private GameObject raycastBlocker;

    private void Awake()
    {
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
            if (ui != null) topUiFinalPositions.Add(ui.anchoredPosition);
        PrepareStacks();
    }

    private void Start()
    {
        StartCoroutine(IntroSequenceRoutine());
    }

    private void Update()
    {
        // Слушаем клик по экрану (или тап на телефоне)
        if (isAnimating && Input.GetMouseButtonDown(0))
        {
            // Резко ускоряем анимацию
            currentSpeedMultiplier = fastForwardMultiplier;
        }
    }

    private void PrepareStacks()
    {
        foreach (var ui in topUiElements)
            if (ui != null) ui.anchoredPosition += new Vector2(0, 300f);

        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] == null) continue;
            float stackY = (i < 5) ? cardsFinalPositions[0].y : cardsFinalPositions[5].y;
            Vector2 stackPos = new Vector2(startXOffset, stackY);
            cardObjects[i].anchoredPosition = stackPos + new Vector2(Random.Range(-messyPositionJitter, messyPositionJitter), Random.Range(-messyPositionJitter, messyPositionJitter));
            cardObjects[i].localRotation = Quaternion.Euler(0, 0, startRotation + Random.Range(-10f, 10f));
        }
    }

    private IEnumerator IntroSequenceRoutine()
    {
        isAnimating = true;
        currentSpeedMultiplier = 1f;

        // Включаем невидимый щит, чтобы кнопки не реагировали
        SetInputBlocker(true);

        yield return StartCoroutine(SmartWait(0.3f));

        for (int i = 0; i < cardObjects.Count; i++)
        {
            if (cardObjects[i] == null) continue;
            Quaternion targetRot = Quaternion.Euler(0, 0, Random.Range(-finalRandomRotation, finalRandomRotation));

            // <--- ДОБАВИТЬ ЭТО: Звук вылета карты --->
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
            // Убрали звук отсюда!
            if (ui != null) StartCoroutine(AnimateUi(ui, topUiFinalPositions[topUiElements.IndexOf(ui)], 0.5f));
            yield return StartCoroutine(SmartWait(0.1f));
        }

        yield return StartCoroutine(SmartWait(0.5f));

        EnableHoverEffects();

        // Снимаем блокировку, возвращаем интерфейсу жизнь
        SetInputBlocker(false);
        isAnimating = false;
    }

    private void EnableHoverEffects()
    {
        foreach (var card in cardObjects)
            if (card != null)
            {
                var hover = card.GetComponent<CardHoverEffect>();
                if (hover != null) hover.SetHoverEnabled(true);
            }
    }

    // --- Умные корутины, которые умеют ускоряться ---

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
            elapsed += Time.deltaTime * currentSpeedMultiplier; // Применяем ускорение
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
            elapsed += Time.deltaTime * currentSpeedMultiplier; // Применяем ускорение
            target.anchoredPosition = Vector2.Lerp(startPos, destPos, uiDropCurve.Evaluate(elapsed / duration));
            yield return null;
        }
        target.anchoredPosition = destPos;

        // <--- ПЕРЕНЕСЛИ ЗВУК СЮДА --->
        // Теперь звук удара проигрывается ровно в момент приземления элемента
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Drop");
    }

    // --- Логика блокировки интерфейса (Невидимый щит) ---

    private void SetInputBlocker(bool active)
    {
        if (active)
        {
            if (raycastBlocker == null)
            {
                // Создаем пустышку
                raycastBlocker = new GameObject("IntroInputBlocker");
                var rect = raycastBlocker.AddComponent<RectTransform>();

                // Цепляем к Canvas
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null) rect.SetParent(canvas.transform, false);
                else rect.SetParent(transform, false);

                // Растягиваем на весь экран
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                // Делаем её прозрачной, но ловящей клики
                var img = raycastBlocker.AddComponent<Image>();
                img.color = Color.clear;
            }
            // Выдвигаем на самый передний план
            raycastBlocker.transform.SetAsLastSibling();
        }
        else
        {
            // Уничтожаем щит после анимации
            if (raycastBlocker != null) Destroy(raycastBlocker);
        }
    }

    private void OnDestroy()
    {
        // Подчищаем за собой на случай, если сцена была закрыта принудительно во время анимации
        if (raycastBlocker != null) Destroy(raycastBlocker);
    }
}