using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(100)]
public class GameLayoutManager : MonoBehaviour
{
    public static GameLayoutManager Instance;

    [System.Serializable]
    public class UIElement
    {
        public RectTransform target;
        public RectTransform landscapeAnchor;
        public RectTransform portraitAnchor;
    }

    [System.Serializable]
    public class SlotElement
    {
        public RectTransform targetSlot;
        public RectTransform landscapeAnchor;
        public RectTransform portraitAnchor;
        public Transform landscapeBoundary;
        public Transform portraitBoundary;
    }

    [Header("Игровые слоты (каждый отдельно)")]
    public List<SlotElement> slots = new List<SlotElement>();

    [Header("UI Элементы (Панель инфо, кнопки)")]
    public List<UIElement> uiElements = new List<UIElement>();

    [Header("Включать только в Альбомной")]
    public List<GameObject> landscapeOnlyObjects = new List<GameObject>();

    [Header("Включать только в Портретной")]
    public List<GameObject> portraitOnlyObjects = new List<GameObject>();

    public bool IsPortrait { get; private set; }
    private int lastWidth;
    private int lastHeight;

    private void Awake()
    {
        Instance = this;
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        IsPortrait = Screen.width < Screen.height;

        ApplyLayout(IsPortrait);
    }

    private void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            bool checkPortrait = Screen.width < Screen.height;

            if (checkPortrait != IsPortrait)
            {
                IsPortrait = checkPortrait;
                ApplyLayout(IsPortrait);
            }
        }
    }

    public void ApplyLayout(bool portrait)
    {
        // 1. Включаем нужные панели
        foreach (var obj in landscapeOnlyObjects) if (obj != null) obj.SetActive(!portrait);
        foreach (var obj in portraitOnlyObjects) if (obj != null) obj.SetActive(portrait);

        // 2. Перемещаем UI Элементы
        foreach (var el in uiElements)
        {
            if (el.target == null) continue;
            RectTransform newParent = portrait ? el.portraitAnchor : el.landscapeAnchor;
            ReparentAndStretch(el.target, newParent);
        }

        // 3. Перемещаем Слоты
        foreach (var slot in slots)
        {
            if (slot.targetSlot == null) continue;
            RectTransform newParent = portrait ? slot.portraitAnchor : slot.landscapeAnchor;
            ReparentAndStretchSlot(slot.targetSlot, newParent);
        }

        // === КРИТИЧНЫЙ ФИКС ===
        // Принудительно заставляем Unity пересчитать все мировые позиции ДО перерасчета отступов
        Canvas.ForceUpdateCanvases();

        // 4. Даем команду слотам пересчитать раскладку карт
        foreach (var slot in slots)
        {
            if (slot.targetSlot == null) continue;

            var tableau = slot.targetSlot.GetComponent<TableauPile>();
            if (tableau != null)
            {
                Transform boundary = portrait ? slot.portraitBoundary : slot.landscapeBoundary;
                if (boundary != null) tableau.bottomBoundary = boundary;

                // Мгновенно расставляем карты, чтобы они не летали при повороте телефона
                tableau.ForceRebuildLayout();
            }

            var waste = slot.targetSlot.GetComponent<WastePile>();
            if (waste != null) waste.ForceLayoutImmediate();
        }

        foreach (var comp in FindObjectsOfType<MonoBehaviour>())
        {
            if (comp is IIntroController intro)
            {
                intro.UpdateSavedPositions();
                break;
            }
        }
    }

    private void ReparentAndStretch(RectTransform target, RectTransform newParent)
    {
        if (target == null || newParent == null) return;
        target.SetParent(newParent, false);
        target.anchorMin = Vector2.zero; target.anchorMax = Vector2.one;
        target.offsetMin = Vector2.zero; target.offsetMax = Vector2.zero;
        target.anchoredPosition = Vector2.zero;
        target.pivot = new Vector2(0.5f, 0.5f);
        target.localScale = Vector3.one;
        target.localPosition = new Vector3(target.localPosition.x, target.localPosition.y, 0f);
    }

    private void ReparentAndStretchSlot(RectTransform target, RectTransform newParent)
    {
        if (target == null || newParent == null) return;

        var fitter = target.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        if (fitter != null)
        {
            fitter.enabled = false;
            Destroy(fitter);
        }

        target.SetParent(newParent, false);
        target.anchorMin = Vector2.zero; target.anchorMax = Vector2.one;
        target.offsetMin = Vector2.zero; target.offsetMax = Vector2.zero;
        target.anchoredPosition = Vector2.zero;
        target.pivot = new Vector2(0.5f, 0.5f);
        target.localScale = Vector3.one;
        target.localPosition = new Vector3(target.localPosition.x, target.localPosition.y, 0f);
    }
}