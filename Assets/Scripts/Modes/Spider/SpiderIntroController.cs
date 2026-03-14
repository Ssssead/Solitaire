using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpiderIntroController : MonoBehaviour, IIntroController
{
    [Header("References")]
    public SpiderModeManager modeManager;
    public RectTransform topPanel;
    public List<RectTransform> bottomButtons;

    [Header("Animation Settings")]
    public float startDelay = 0.5f;
    public float slotsFadeDuration = 0.8f;
    public float deckFlyDuration = 1.0f;
    public float uiSlideDuration = 0.5f;
    public float buttonStaggerDelay = 0.1f;

    private Vector2 topPanelStartPos;
    private Vector2 topPanelHiddenPos;
    private List<Vector2> buttonsStartPos = new List<Vector2>();
    private List<Vector2> buttonsHiddenPos = new List<Vector2>();

    public List<RectTransform> GetTopUIElements() => new List<RectTransform> { topPanel };
    public List<RectTransform> GetBottomUIElements() => bottomButtons;

    private void Awake()
    {
        if (modeManager == null) modeManager = GetComponent<SpiderModeManager>();
    }

    public void SetupIntro(bool skipUI)
    {
        Canvas.ForceUpdateCanvases();

        // Запоминаем оригинальные позиции UI
        if (topPanel != null && topPanelStartPos == Vector2.zero)
        {
            topPanelStartPos = topPanel.anchoredPosition;
            topPanelHiddenPos = topPanelStartPos + new Vector2(0, 300f);
        }

        if (buttonsStartPos.Count == 0)
        {
            foreach (var btn in bottomButtons)
            {
                if (btn != null)
                {
                    buttonsStartPos.Add(btn.anchoredPosition);
                    buttonsHiddenPos.Add(btn.anchoredPosition + new Vector2(0, -300f));
                }
            }
        }

        // Если это первый запуск, прячем UI и прозрачность слотов
        if (!skipUI)
        {
            if (topPanel != null) topPanel.anchoredPosition = topPanelHiddenPos;
            for (int i = 0; i < bottomButtons.Count; i++)
                if (bottomButtons[i] != null) bottomButtons[i].anchoredPosition = buttonsHiddenPos[i];

            SetSlotsAlpha(0f);
        }
    }

    public IEnumerator PlayIntro(bool skipUI)
    {
        // 1 ФАЗА: Появление слотов и выезд UI (пропускаем при рестарте)
        if (!skipUI)
        {
            yield return new WaitForSeconds(startDelay);

            StartCoroutine(FadeInSlots(slotsFadeDuration));

            if (topPanel != null)
                StartCoroutine(AnimateUIElement(topPanel, topPanelHiddenPos, topPanelStartPos, uiSlideDuration));

            for (int i = 0; i < bottomButtons.Count; i++)
            {
                if (bottomButtons[i] != null)
                {
                    StartCoroutine(AnimateUIElement(bottomButtons[i], buttonsHiddenPos[i], buttonsStartPos[i], uiSlideDuration));
                    yield return new WaitForSeconds(buttonStaggerDelay);
                }
            }

            // Ждем окончания UI анимации
            yield return new WaitForSeconds(uiSlideDuration);
        }
        else
        {
            // При перезапуске гарантируем, что слоты 100% видимы
            SetSlotsAlpha(1f);
        }

        // 2 ФАЗА: Полет колоды снизу вверх (выполняется всегда)
        if (modeManager != null && modeManager.deckManager != null)
        {
            yield return StartCoroutine(modeManager.deckManager.PlayIntroDeckArrival(deckFlyDuration));
        }
    }

    private IEnumerator AnimateUIElement(RectTransform target, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (target != null) target.anchoredPosition = Vector2.Lerp(from, to, curve.Evaluate(elapsed / duration));
            yield return null;
        }
        if (target != null) target.anchoredPosition = to;
    }

    private IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetSlotsAlpha(elapsed / duration);
            yield return null;
        }
        SetSlotsAlpha(1f);
    }

    private void SetSlotsAlpha(float alpha)
    {
        if (modeManager == null || modeManager.pileManager == null) return;

        foreach (var t in modeManager.pileManager.TableauPiles)
        {
            if (t != null)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg) cg.alpha = alpha;
            }
        }
        foreach (var f in modeManager.pileManager.FoundationPiles)
        {
            if (f != null)
            {
                var cg = f.GetComponent<CanvasGroup>();
                if (cg) cg.alpha = alpha;
            }
        }

        // Скрываем подложку стока, чтобы она не светилась до прилета карт
        if (modeManager.pileManager.StockPile != null)
        {
            var cg = modeManager.pileManager.StockPile.GetComponent<CanvasGroup>();
            if (cg) cg.alpha = alpha;
        }
    }
}