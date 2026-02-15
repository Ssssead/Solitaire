using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameIntroController : MonoBehaviour
{
    [Header("References")]
    public KlondikeModeManager modeManager;
    public RectTransform topPanel;

    // [NEW] Список кнопок вместо одной панели
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.5f;
    public float slotsFadeDuration = 0.8f;
    public float deckFlyDuration = 1.2f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f; // Задержка между вылетом кнопок

    // Начальные позиции
    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<KlondikeModeManager>();
    }

    public void PrepareIntro()
    {
        // 1. Скрываем слоты
        modeManager.PileManager.SetAllSlotsAlpha(0f);

        // 2. Скрываем Верхнюю панель
        if (topPanel != null)
        {
            topPanelStartPos = topPanel.anchoredPosition;
            topPanelHiddenPos = topPanelStartPos + new Vector2(0, 200f); // Вверх
            topPanel.anchoredPosition = topPanelHiddenPos;
        }

        // 3. Скрываем кнопки (каждую отдельно)
        buttonsStartPos.Clear();
        buttonsHiddenPos.Clear();

        foreach (var btn in bottomButtons)
        {
            if (btn != null)
            {
                Vector2 start = btn.anchoredPosition;
                buttonsStartPos.Add(start);

                // Сдвигаем вниз за экран
                Vector2 hidden = start - new Vector2(0, 200f);
                buttonsHiddenPos.Add(hidden);

                btn.anchoredPosition = hidden;
            }
        }
    }

    public void PlayIntroSequence()
    {
        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        // 1. Проявление слотов
        StartCoroutine(modeManager.PileManager.FadeInSlots(slotsFadeDuration));

        // 2. Вылет верхней панели
        if (topPanel != null)
            StartCoroutine(AnimateUIElement(topPanel, topPanelHiddenPos, topPanelStartPos, uiSlideDuration));

        // 3. Вылет кнопок "лесенкой"
        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
            {
                StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                yield return new WaitForSeconds(buttonStaggerDelay);
            }
        }

        // 4. Полет колоды (НАСТОЯЩИХ КАРТ)
        // Мы просим DeckManager подготовить карты где-то за экраном, а потом притянуть их
        yield return StartCoroutine(modeManager.deckManager.PlayIntroDeckArrival(deckFlyDuration));
    }

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        if (target == null) yield break;
        float elapsed = 0f;
        // Используем BackOut для эффекта "пружинки" при остановке
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            target.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            yield return null;
        }
        target.anchoredPosition = to;
    }
}