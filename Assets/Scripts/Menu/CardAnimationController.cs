using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CardAnimationController : MonoBehaviour
{
    public enum MenuMode { Grid, Preview }

    [System.Serializable]
    public class CardEntry
    {
        public GameType type;
        public RectTransform rect;
        public RectTransform portraitHome;

        [HideInInspector] public RectTransform landscapeHome;
        [HideInInspector] public Vector3 initialScale;
        [HideInInspector] public CardHoverEffect hoverEffect;
        [HideInInspector] public Button buttonComp;
    }

    [Header("Configuration")]
    public List<CardEntry> allCards;

    [Header("Shared Anchors - Landscape")]
    public RectTransform landscapePreviewAnchor;
    public List<RectTransform> landscapeBottomSlots;

    [Header("Shared Anchors - Portrait")]
    public RectTransform portraitPreviewAnchor;
    public List<RectTransform> portraitBottomSlots;

    [Header("Animation Settings")]
    public float animationDuration = 0.4f;
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float randomRotationRange = 3f;

    [Header("Landscape Scales")]
    public Vector3 landscapeSelectedScale = new Vector3(1.2f, 1.2f, 1f);
    public Vector3 landscapeBottomScale = new Vector3(0.7f, 0.7f, 1f);

    [Header("Portrait Scales")]
    public Vector3 portraitGridScale = new Vector3(1.6f, 1.6f, 1f);
    public Vector3 portraitSelectedScale = new Vector3(2.5f, 2.5f, 1f);
    public Vector3 portraitBottomScale = new Vector3(1.3f, 1.3f, 1f);

    private bool isPortrait;
    private MenuMode currentMode = MenuMode.Grid;
    private GameType? selectedGame = null;
    private Coroutine orientationRoutine;

    private void Awake()
    {
        isPortrait = Screen.width < Screen.height;

        foreach (var card in allCards)
        {
            if (card.rect != null)
            {
                Vector3 originalScale = card.rect.localScale;
                Quaternion originalRot = card.rect.localRotation;

                GameObject phObj = new GameObject(card.rect.name + "_LandscapeHome");
                RectTransform phRect = phObj.AddComponent<RectTransform>();
                phRect.SetParent(card.rect.parent, false);
                phRect.SetSiblingIndex(card.rect.GetSiblingIndex());

                phRect.anchorMin = card.rect.anchorMin;
                phRect.anchorMax = card.rect.anchorMax;
                phRect.pivot = card.rect.pivot;
                phRect.sizeDelta = card.rect.sizeDelta;
                phRect.anchoredPosition = card.rect.anchoredPosition;
                phRect.localScale = Vector3.one;
                phRect.localRotation = Quaternion.identity;

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

                AspectRatioFitter fitter = card.rect.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                {
                    AspectRatioFitter phFitter = phObj.AddComponent<AspectRatioFitter>();
                    phFitter.aspectMode = fitter.aspectMode;
                    phFitter.aspectRatio = fitter.aspectRatio;
                    fitter.enabled = false;
                }

                card.landscapeHome = phRect;
                card.initialScale = originalScale;

                RectTransform startingTarget = isPortrait ? card.portraitHome : card.landscapeHome;
                if (startingTarget != null)
                {
                    card.rect.SetParent(startingTarget, true);
                    SetAsStretchChild(card.rect);
                }

                card.rect.localScale = isPortrait ? portraitGridScale : originalScale;
                card.rect.localRotation = originalRot;
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

    private void Update()
    {
        bool checkPortrait = Screen.width < Screen.height;
        if (checkPortrait != isPortrait)
        {
            isPortrait = checkPortrait;
            HandleOrientationChange();
        }
    }

    private Vector2 GetHomeSize(CardEntry card)
    {
        RectTransform currentHome = isPortrait ? card.portraitHome : card.landscapeHome;
        return currentHome != null ? currentHome.rect.size : Vector2.zero;
    }

    private Vector3 GetTargetScaleForCard(CardEntry card)
    {
        if (currentMode == MenuMode.Grid)
        {
            return isPortrait ? portraitGridScale : card.initialScale;
        }
        else
        {
            if (selectedGame.HasValue && card.type == selectedGame.Value)
                return isPortrait ? portraitSelectedScale : landscapeSelectedScale;
            else
                return isPortrait ? portraitBottomScale : landscapeBottomScale;
        }
    }

    private void HandleOrientationChange()
    {
        if (orientationRoutine != null) StopCoroutine(orientationRoutine);
        orientationRoutine = StartCoroutine(TransitionOrientationRoutine());
    }

    private IEnumerator TransitionOrientationRoutine()
    {
        // 1. ЖЕЛЕЗОБЕТОННАЯ БЛОКИРОВКА: Сбрасываем все ховеры ДО ТОГО, как считать координаты!
        SetAllHovers(false);

        // 2. Ждем 1 кадр и обновляем Canvas (чтобы карточки физически вернулись на место)
        yield return null;
        Canvas.ForceUpdateCanvases();

        float elapsed = 0f;

        Dictionary<CardEntry, Vector3> startPositions = new Dictionary<CardEntry, Vector3>();
        Dictionary<CardEntry, Vector2> startSizes = new Dictionary<CardEntry, Vector2>();
        Dictionary<CardEntry, Vector3> startScales = new Dictionary<CardEntry, Vector3>();
        Dictionary<CardEntry, RectTransform> targets = new Dictionary<CardEntry, RectTransform>();

        int unselectedSlotIndex = 0;

        foreach (var card in allCards)
        {
            RectTransform targetParent = GetTargetParentForCard(card, ref unselectedSlotIndex);
            targets[card] = targetParent;

            if (targetParent != null)
            {
                // Запоминаем мировую позицию ДО смены родителя (она теперь 100% без искажений от мышки)
                startPositions[card] = card.rect.position;

                card.rect.SetParent(targetParent, true);

                SetAsFixedCenterAnchor(card.rect, card.rect.rect.size);

                // ЗАПОМИНАЕМ ЛОКАЛЬНЫЕ ДАННЫЕ ПОСЛЕ СМЕНЫ РОДИТЕЛЯ
                startSizes[card] = card.rect.sizeDelta;
                startScales[card] = card.rect.localScale;

                // Жестко возвращаем мировую позицию
                card.rect.position = startPositions[card];
            }
        }

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveT = motionCurve.Evaluate(t);

            foreach (var card in allCards)
            {
                RectTransform targetParent = targets[card];
                if (targetParent != null)
                {
                    card.rect.position = Vector3.LerpUnclamped(startPositions[card], targetParent.position, curveT);

                    Vector2 targetSize = (currentMode == MenuMode.Grid) ? targetParent.rect.size : GetHomeSize(card);
                    card.rect.sizeDelta = Vector2.LerpUnclamped(startSizes[card], targetSize, curveT);

                    Vector3 targetScale = GetTargetScaleForCard(card);
                    card.rect.localScale = Vector3.LerpUnclamped(startScales[card], targetScale, curveT);
                }
            }
            yield return null;
        }

        foreach (var card in allCards)
        {
            if (currentMode == MenuMode.Grid) SetAsStretchChild(card.rect);
            else SetAsFixedCenterAnchor(card.rect, GetHomeSize(card));

            RefreshCardVisuals(card.rect);
        }

        // 3. ВОЗВРАЩАЕМ ХОВЕРЫ: Перелет закончен, можно снова водить мышкой
        SetAllHovers(true);
    }

    private RectTransform GetTargetParentForCard(CardEntry card, ref int unselectedSlotIndex)
    {
        if (currentMode == MenuMode.Grid)
        {
            return isPortrait ? card.portraitHome : card.landscapeHome;
        }
        else
        {
            if (selectedGame.HasValue && card.type == selectedGame.Value)
            {
                return isPortrait ? portraitPreviewAnchor : landscapePreviewAnchor;
            }
            else
            {
                var currentBottomSlots = isPortrait ? portraitBottomSlots : landscapeBottomSlots;
                if (unselectedSlotIndex < currentBottomSlots.Count)
                {
                    RectTransform slot = currentBottomSlots[unselectedSlotIndex];
                    unselectedSlotIndex++;
                    return slot;
                }
            }
        }
        return isPortrait ? card.portraitHome : card.landscapeHome;
    }

    public void SelectCard(GameType selectedType)
    {
        StopAllCoroutines();
        SetAllHovers(false);

        currentMode = MenuMode.Preview;
        selectedGame = selectedType;

        StartCoroutine(SelectCardRoutine(selectedType));
    }

    private IEnumerator SelectCardRoutine(GameType selectedType)
    {
        int bottomSlotIndex = 0;
        List<Coroutine> activeAnims = new List<Coroutine>();
        CardEntry selectedCardEntry = null;

        RectTransform currentPreview = isPortrait ? portraitPreviewAnchor : landscapePreviewAnchor;
        List<RectTransform> currentBottom = isPortrait ? portraitBottomSlots : landscapeBottomSlots;

        foreach (var card in allCards)
        {
            if (card.type != selectedType && card.hoverEffect != null)
                card.hoverEffect.SetSelectedMode(false);
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
                targetSlot = currentPreview;
                destScale = isPortrait ? portraitSelectedScale : landscapeSelectedScale;
            }
            else
            {
                if (bottomSlotIndex < currentBottom.Count)
                {
                    targetSlot = currentBottom[bottomSlotIndex];
                    destScale = isPortrait ? portraitBottomScale : landscapeBottomScale;
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
                    card, targetSlot, startPos, startScale, destScale, startRot, destRot, startSize
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

    private IEnumerator AnimateToSlot(CardEntry card, RectTransform destSlot, Vector3 startPos, Vector3 startScale, Vector3 destScale, Quaternion startRot, Quaternion destRot, Vector2 startSize)
    {
        float elapsed = 0f;
        RectTransform target = card.rect;

        Vector2 correctSize = GetHomeSize(card);

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveT = motionCurve.Evaluate(t);

            target.position = Vector3.Lerp(startPos, destSlot.position, curveT);
            target.localScale = Vector3.Lerp(startScale, destScale, curveT);
            target.localRotation = Quaternion.Lerp(startRot, destRot, curveT);

            target.sizeDelta = Vector2.Lerp(startSize, correctSize, curveT);

            yield return null;
        }

        target.position = destSlot.position;
        target.localScale = destScale;
        target.localRotation = destRot;
        target.sizeDelta = correctSize;
    }

    public void ResetGrid()
    {
        StopAllCoroutines();
        SetAllHovers(false);

        currentMode = MenuMode.Grid;
        selectedGame = null;

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

                RectTransform targetHome = isPortrait ? card.portraitHome : card.landscapeHome;

                card.rect.SetParent(targetHome, true);
                SetAsFixedCenterAnchor(card.rect, startSize);
                card.rect.position = startPos;

                float randomZ = Random.Range(-randomRotationRange, randomRotationRange);
                Quaternion randomRot = Quaternion.Euler(0, 0, randomZ);

                Vector3 destScale = isPortrait ? portraitGridScale : card.initialScale;

                activeAnims.Add(StartCoroutine(AnimateHome(
                    card, targetHome, startPos, startScale, destScale, startRot, randomRot, startSize
                )));
            }
        }

        foreach (var c in activeAnims) yield return c;
        foreach (var card in allCards) RefreshCardVisuals(card.rect);
        RefreshAllCards();
        SetAllHovers(true);
    }

    private IEnumerator AnimateHome(CardEntry card, RectTransform destSlot, Vector3 startPos, Vector3 startScale, Vector3 destScale, Quaternion startRot, Quaternion destRot, Vector2 startSize)
    {
        float elapsed = 0f;
        RectTransform target = card.rect;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveT = motionCurve.Evaluate(t);

            target.position = Vector3.Lerp(startPos, destSlot.position, curveT);
            target.localScale = Vector3.Lerp(startScale, destScale, curveT);
            target.localRotation = Quaternion.Lerp(startRot, destRot, curveT);

            target.sizeDelta = Vector2.Lerp(startSize, destSlot.rect.size, curveT);

            yield return null;
        }

        target.position = destSlot.position;
        target.localScale = destScale;
        target.localRotation = destRot;

        SetAsStretchChild(target);
    }

    private void SetAsStretchChild(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
        foreach (var entry in allCards) RefreshCardVisuals(entry.rect);
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
}