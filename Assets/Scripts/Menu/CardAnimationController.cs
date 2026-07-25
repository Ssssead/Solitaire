using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CardAnimationController : MonoBehaviour
{
    [System.Serializable]
    public class CardEntry
    {
        public GameType type;
        public RectTransform rect;
        [HideInInspector] public RectTransform homePlaceholder;
        [HideInInspector] public Vector3 initialScale;
        [HideInInspector] public CardHoverEffect hoverEffect;
        [HideInInspector] public Button buttonComp;
    }

    [Header("Configuration")]
    public List<CardEntry> allCards;

    [Header("Positions")]
    public RectTransform previewAnchor;
    public List<RectTransform> bottomSlots;

    [Header("Animation Settings")]
    public float animationDuration = 0.4f;
    public Vector3 selectedScale = new Vector3(1.2f, 1.2f, 1f);
    public Vector3 bottomScale = new Vector3(0.7f, 0.7f, 1f);
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Rotation Settings")]
    [Tooltip("Максимальный угол случайного наклона (например, 3 градуса)")]
    public float randomRotationRange = 3f;

    private void Awake()
    {
        foreach (var card in allCards)
        {
            if (card.rect != null)
            {
                // Запоминаем оригинальные масштаб (например, 0.9) и поворот карты
                Vector3 originalScale = card.rect.localScale;
                Quaternion originalRot = card.rect.localRotation;

                // 1. Создаем пустышку, которая будет держать место в сетке
                GameObject phObj = new GameObject(card.rect.name + "_Home");
                RectTransform phRect = phObj.AddComponent<RectTransform>();

                phRect.SetParent(card.rect.parent, false);
                phRect.SetSiblingIndex(card.rect.GetSiblingIndex());

                // Копируем базовые параметры
                phRect.anchorMin = card.rect.anchorMin;
                phRect.anchorMax = card.rect.anchorMax;
                phRect.pivot = card.rect.pivot;
                phRect.sizeDelta = card.rect.sizeDelta;
                phRect.anchoredPosition = card.rect.anchoredPosition;

                // Пустышка должна быть "чистой", чтобы сетка (Layout Group) не сходила с ума
                phRect.localScale = Vector3.one;
                phRect.localRotation = Quaternion.identity;

                // 2. Забираем правила Layout у карты и отдаем пустышке
                LayoutElement le = card.rect.GetComponent<LayoutElement>();
                if (le != null)
                {
                    LayoutElement phLe = phObj.AddComponent<LayoutElement>();
                    phLe.ignoreLayout = le.ignoreLayout;
                    phLe.minWidth = le.minWidth;
                    phLe.minHeight = le.minHeight;
                    phLe.preferredWidth = le.preferredWidth;
                    phLe.preferredHeight = le.preferredHeight;
                    phLe.flexibleWidth = le.flexibleWidth;
                    phLe.flexibleHeight = le.flexibleHeight;
                    phLe.layoutPriority = le.layoutPriority;

                    le.enabled = false;
                }

                // 3. Забираем AspectRatioFitter
                AspectRatioFitter fitter = card.rect.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                {
                    AspectRatioFitter phFitter = phObj.AddComponent<AspectRatioFitter>();
                    phFitter.aspectMode = fitter.aspectMode;
                    phFitter.aspectRatio = fitter.aspectRatio;

                    fitter.enabled = false;
                }

                card.homePlaceholder = phRect;

                // 4. Помещаем саму карту внутрь её пустышки
                card.rect.SetParent(phRect, true);
                SetAsStretchChild(card.rect);

                // 5. ВОЗВРАЩАЕМ карте её родной вид (scale 0.9)
                card.rect.localScale = originalScale;
                card.rect.localRotation = originalRot;

                // И сохраняем его в память для ResetGrid
                card.initialScale = originalScale;
            }
        }
    }

    private void Start()
    {
        foreach (var card in allCards)
        {
            if (card.rect != null)
            {
                card.hoverEffect = card.rect.GetComponent<CardHoverEffect>();
                card.buttonComp = card.rect.GetComponent<Button>();
            }
        }
    }

    private void SetAsStretchChild(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private void SetAsFixedCenterAnchor(RectTransform rt, Vector2 currentAbsoluteSize)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = currentAbsoluteSize;
    }

    private void SetAllHovers(bool state)
    {
        foreach (var card in allCards)
        {
            if (card.hoverEffect != null)
            {
                card.hoverEffect.SetHoverEnabled(state);
                card.hoverEffect.enabled = state;
            }

            if (card.rect != null)
            {
                var tooltip = card.rect.GetComponent<ButtonHoverTooltip>();
                if (tooltip != null) tooltip.enabled = state;
            }

            if (card.buttonComp != null)
            {
                card.buttonComp.interactable = state;
            }
        }
    }

    public void RefreshAllCards()
    {
        foreach (var entry in allCards)
        {
            RefreshCardVisuals(entry.rect);
        }
    }

    public void RefreshCardVisuals(RectTransform card)
    {
        if (card == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);

        var texts = card.GetComponentsInChildren<TMP_Text>();
        foreach (var t in texts)
        {
            t.SetAllDirty();
            t.ForceMeshUpdate();
        }
    }

    public void SelectCard(GameType selectedType)
    {
        StopAllCoroutines();
        SetAllHovers(false);
        StartCoroutine(SelectCardRoutine(selectedType));
    }

    private IEnumerator SelectCardRoutine(GameType selectedType)
    {
        int bottomSlotIndex = 0;
        List<Coroutine> activeAnims = new List<Coroutine>();
        CardEntry selectedCardEntry = null;

        foreach (var card in allCards)
        {
            if (card.type != selectedType && card.hoverEffect != null)
            {
                card.hoverEffect.SetSelectedMode(false);
            }
        }

        foreach (var card in allCards)
        {
            if (card.rect == null) continue;

            Vector3 startPos = card.rect.position;
            Vector2 startSize = card.rect.rect.size;
            Quaternion startRot = card.rect.localRotation;
            Vector3 startScale = card.rect.localScale;

            RectTransform targetSlot = null;
            Vector3 destScale = Vector3.one;
            Quaternion destRot = Quaternion.identity;

            if (card.type == selectedType)
            {
                selectedCardEntry = card;
                targetSlot = previewAnchor;
                destScale = selectedScale;
            }
            else
            {
                if (bottomSlotIndex < bottomSlots.Count)
                {
                    targetSlot = bottomSlots[bottomSlotIndex];
                    destScale = bottomScale;
                    float randomZ = Random.Range(-randomRotationRange, randomRotationRange);
                    destRot = Quaternion.Euler(0, 0, randomZ);
                    bottomSlotIndex++;
                }
            }

            if (targetSlot != null)
            {
                card.rect.SetParent(targetSlot, true);
                SetAsFixedCenterAnchor(card.rect, startSize);
                card.rect.position = startPos;

                activeAnims.Add(StartCoroutine(AnimateToSlot(
                    card, startPos, targetSlot, startScale, destScale, startRot, destRot, startSize
                )));
            }
        }

        foreach (var c in activeAnims) yield return c;
        foreach (var card in allCards) RefreshCardVisuals(card.rect);
        SetAllHovers(true);

        if (selectedCardEntry != null)
        {
            if (selectedCardEntry.buttonComp != null) selectedCardEntry.buttonComp.interactable = false;
            if (selectedCardEntry.hoverEffect != null) selectedCardEntry.hoverEffect.SetSelectedMode(true);
        }
    }

    private IEnumerator AnimateToSlot(CardEntry card, Vector3 startPos, RectTransform destSlot, Vector3 startScale, Vector3 destScale, Quaternion startRot, Quaternion destRot, Vector2 startSize)
    {
        float elapsed = 0f;
        RectTransform target = card.rect;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveT = motionCurve.Evaluate(t);

            Vector3 currentDestPos = destSlot != null ? destSlot.position : startPos;
            Vector2 correctSize = card.homePlaceholder.rect.size;

            target.position = Vector3.Lerp(startPos, currentDestPos, curveT);
            target.localScale = Vector3.Lerp(startScale, destScale, curveT);
            target.localRotation = Quaternion.Lerp(startRot, destRot, curveT);
            target.sizeDelta = Vector2.Lerp(startSize, correctSize, curveT);

            yield return null;
        }

        if (destSlot != null) target.position = destSlot.position;
        target.localScale = destScale;
        target.localRotation = destRot;
        target.sizeDelta = card.homePlaceholder.rect.size;
    }

    public void ResetGrid()
    {
        StopAllCoroutines();
        SetAllHovers(false);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

        foreach (var card in allCards)
        {
            if (card.hoverEffect != null) card.hoverEffect.SetSelectedMode(false);
            if (card.buttonComp != null) card.buttonComp.interactable = true;
        }

        StartCoroutine(ResetGridRoutine());
    }

    private IEnumerator ResetGridRoutine()
    {
        List<Coroutine> activeAnims = new List<Coroutine>();

        foreach (var card in allCards)
        {
            if (card.rect != null)
            {
                Vector3 startPos = card.rect.position;
                Vector3 startScale = card.rect.localScale;
                Quaternion startRot = card.rect.localRotation;
                Vector2 startSize = card.rect.rect.size;

                card.rect.SetParent(card.homePlaceholder, true);
                SetAsFixedCenterAnchor(card.rect, startSize);
                card.rect.position = startPos;

                float randomZ = Random.Range(-randomRotationRange, randomRotationRange);
                Quaternion randomRot = Quaternion.Euler(0, 0, randomZ);

                activeAnims.Add(StartCoroutine(AnimateHome(
                    card, startPos, startScale, card.initialScale, startRot, randomRot, startSize
                )));
            }
        }

        foreach (var c in activeAnims) yield return c;
        foreach (var card in allCards) RefreshCardVisuals(card.rect);
        RefreshAllCards();
        SetAllHovers(true);
    }

    private IEnumerator AnimateHome(CardEntry card, Vector3 startPos, Vector3 startScale, Vector3 destScale, Quaternion startRot, Quaternion destRot, Vector2 startSize)
    {
        float elapsed = 0f;
        RectTransform target = card.rect;
        RectTransform placeholder = card.homePlaceholder;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveT = motionCurve.Evaluate(t);

            Vector2 currentPlaceholderSize = placeholder.rect.size;

            target.position = Vector3.Lerp(startPos, placeholder.position, curveT);
            target.localScale = Vector3.Lerp(startScale, destScale, curveT);
            target.localRotation = Quaternion.Lerp(startRot, destRot, curveT);

            target.sizeDelta = Vector2.Lerp(startSize, currentPlaceholderSize, curveT);

            yield return null;
        }

        target.position = placeholder.position;
        target.localScale = destScale;
        target.localRotation = destRot;

        SetAsStretchChild(target);
    }

    public void SetHomePosition(RectTransform cardRect, Vector2 pos) { }
}