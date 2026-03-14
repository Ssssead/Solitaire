using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MontanaIntroController : MonoBehaviour, IIntroController
{
    [Header("Core Reference")]
    public MontanaModeManager modeManager;

    [Header("Top UI Elements (Выезжают СВЕРХУ)")]
    public RectTransform topPanelMain;
    public RectTransform topExtraButton;

    [Header("Bottom UI Elements (Выезжают СНИЗУ)")]
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float slotsFadeDuration = 0.5f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    // Скрытые и начальные позиции
    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;

    private Vector2 topExtraButtonStartPos;
    private Vector2 topExtraButtonHiddenPos;

    private List<Vector2> bottomButtonsStartPos = new List<Vector2>();
    private List<Vector2> bottomButtonsHiddenPos = new List<Vector2>();

    private void Awake()
    {
        if (modeManager == null) modeManager = FindObjectOfType<MontanaModeManager>();

        // Принудительно заставляем Unity просчитать все Layout-ы перед сохранением позиций
        Canvas.ForceUpdateCanvases();
        SaveInitialPositions();

        // Прячем UI еще до первого кадра
        PrepareIntro(modeManager.isRestarting);
    }

    private void SaveInitialPositions()
    {
        // Сохраняем позиции и высчитываем точку ЗА экраном (СВЕРХУ)
        if (topPanelMain != null)
        {
            topPanelStartPos = topPanelMain.anchoredPosition;
            topPanelHiddenPos = topPanelStartPos + new Vector2(0, 300f);
        }

        if (topExtraButton != null)
        {
            topExtraButtonStartPos = topExtraButton.anchoredPosition;
            topExtraButtonHiddenPos = topExtraButtonStartPos + new Vector2(0, 300f);
        }

        // Сохраняем позиции кнопок и высчитываем точку ЗА экраном (СНИЗУ)
        bottomButtonsStartPos.Clear();
        bottomButtonsHiddenPos.Clear();
        foreach (var btn in bottomButtons)
        {
            if (btn != null)
            {
                bottomButtonsStartPos.Add(btn.anchoredPosition);
                bottomButtonsHiddenPos.Add(btn.anchoredPosition + new Vector2(0, -300f));
            }
        }
    }

    public void PrepareIntro(bool isRestarting)
    {
        if (modeManager != null) modeManager.IsInputAllowed = false;

        if (isRestarting)
        {
            // ПРИ РЕСТАРТЕ: Сразу ставим всё на финальные (видимые) позиции
            if (topPanelMain != null) topPanelMain.anchoredPosition = topPanelStartPos;
            if (topExtraButton != null) topExtraButton.anchoredPosition = topExtraButtonStartPos;

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = bottomButtonsStartPos[i];
            }
            SetSlotsAlpha(1f); // Слоты сразу видимы
        }
        else
        {
            // ПРИ ПЕРВОМ ЗАПУСКЕ: Физически переносим элементы за пределы экрана
            if (topPanelMain != null) topPanelMain.anchoredPosition = topPanelHiddenPos;
            if (topExtraButton != null) topExtraButton.anchoredPosition = topExtraButtonHiddenPos;

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = bottomButtonsHiddenPos[i];
            }
            SetSlotsAlpha(0f); // Слоты прозрачны
        }
    }

    public IEnumerator PlayIntroSequence(bool isRestarting)
    {
        // Если это рестарт, полностью пропускаем блок анимации UI
        if (isRestarting)
        {
            yield break;
        }

        // 1. Плавное проявление 56 слотов на столе
        yield return StartCoroutine(FadeInSlots(slotsFadeDuration));

        // 2. Анимация въезда СВЕРХУ (панель и доп. кнопка одновременно)
        if (topPanelMain != null)
            StartCoroutine(AnimateUIElement(topPanelMain, topPanelHiddenPos, topPanelStartPos, uiSlideDuration));

        if (topExtraButton != null)
            StartCoroutine(AnimateUIElement(topExtraButton, topExtraButtonHiddenPos, topExtraButtonStartPos, uiSlideDuration));

        // 3. Анимация въезда СНИЗУ (каждая кнопка по очереди с микро-задержкой)
        for (int i = 0; i < bottomButtons.Count; i++)
        {
            if (bottomButtons[i] != null)
            {
                StartCoroutine(AnimateUIElement(bottomButtons[i], bottomButtonsHiddenPos[i], bottomButtonsStartPos[i], uiSlideDuration));
                yield return new WaitForSeconds(buttonStaggerDelay);
            }
        }

        yield return new WaitForSeconds(uiSlideDuration);
    }

    // Универсальный метод перемещения UI
    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = t * t * (3f - 2f * t); // Мягкое торможение в конце (SmoothStep)

            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, easedT);
            yield return null;
        }
        if (target != null) target.anchoredPosition = to;
    }

    // --- Логика прозрачности ТОЛЬКО для пустых слотов на доске ---
    private void SetSlotsAlpha(float alpha)
    {
        if (modeManager != null && modeManager.pileManager != null)
        {
            foreach (var slot in modeManager.pileManager.Slots)
            {
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg == null) cg = slot.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = alpha;
            }
        }
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        List<CanvasGroup> groups = new List<CanvasGroup>();

        if (modeManager != null && modeManager.pileManager != null)
        {
            foreach (var slot in modeManager.pileManager.Slots)
            {
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg != null) groups.Add(cg);
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            foreach (var cg in groups) if (cg != null) cg.alpha = elapsed / duration;
            yield return null;
        }
        foreach (var cg in groups) if (cg != null) cg.alpha = 1f;
    }

    // --- Интерфейс IIntroController (Для совместимости с MenuController) ---
    public List<RectTransform> GetTopUIElements()
    {
        List<RectTransform> list = new List<RectTransform>();
        if (topPanelMain != null) list.Add(topPanelMain);
        if (topExtraButton != null) list.Add(topExtraButton);
        return list;
    }

    public List<RectTransform> GetBottomUIElements()
    {
        List<RectTransform> list = new List<RectTransform>();
        foreach (var b in bottomButtons) if (b != null) list.Add(b);
        return list;
    }
}