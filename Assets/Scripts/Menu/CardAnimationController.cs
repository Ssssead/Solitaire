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

    // Состояние одной карты на время перехода ориентации.
    private struct CardTransitionState
    {
        public Vector2 startAnchored;
        public Vector2 startSize;
        public Vector2 targetSize;
        public Vector3 startScale;
        public bool needSizeAnim;
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

    // Переиспользуемые буферы вместо new List/Dictionary на каждый клик.
    // activeAnimsBuffer общий для SelectCardRoutine и ResetGridRoutine — они никогда не выполняются
    // одновременно, так как оба вызывают StopAllCoroutines() перед стартом.
    private readonly List<Coroutine> activeAnimsBuffer = new List<Coroutine>();
    private readonly Dictionary<CardEntry, CardTransitionState> transitionStates = new Dictionary<CardEntry, CardTransitionState>();

    private void Awake()
    {
        isPortrait = Screen.width < Screen.height;

        // Вызов Canvas.ForceUpdateCanvases() здесь можно удалить, 
        // так как в Awake он бесполезен для неинициализированных LayoutGroup.

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
                    // ВАЖНО: Временно привязываем карточку к краям родителя (Stretch).
                    // Это позволит CanvasScaler и встроенным LayoutGroup правильно рассчитать её размер на 1-м кадре.
                    SetAsStretchChild(card.rect);
                }

                card.rect.localRotation = originalRot;
            }
        }
    }
    private void SetAsStretchChild(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private IEnumerator Start()
    {
        // Ждем один кадр, чтобы Unity завершил все просчеты интерфейса
        yield return null;
        Canvas.ForceUpdateCanvases();

        foreach (var card in allCards)
        {
            if (card.rect != null)
            {
                card.hoverEffect = card.rect.GetComponent<CardHoverEffect>();
                card.buttonComp = card.rect.GetComponent<Button>();


                // МЫ УДАЛИЛИ ОТСЮДА ВЫЗОВ SetAsStretchChild(card.rect);
                // Карты уже привязаны к слотам в Awake(), а MenuIntroController
                // уже спрятал их за экран. Если дергать якоря здесь, анимация сломается.

                // Применяем масштаб
                card.rect.localScale = isPortrait ? portraitGridScale : card.initialScale;
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

    // Переводит мировую позицию произвольного RectTransform в anchoredPosition ТЕКУЩЕГО родителя карты,
    // не становясь при этом его ребёнком. Используется только для вычисления цели анимации —
    // сам родитель карты (card.rect.parent) при этом не меняется.
    private Vector2 WorldPositionToLocalAnchored(RectTransform card, Vector3 worldPos)
    {
        Vector3 savedPos = card.position;
        card.position = worldPos;
        Vector2 result = card.anchoredPosition;
        card.position = savedPos;
        return result;
    }

    private void HandleOrientationChange()
    {
        if (orientationRoutine != null) StopCoroutine(orientationRoutine);
        orientationRoutine = StartCoroutine(TransitionOrientationRoutine());
    }

    // Смена ориентации остаётся единственным местом, где карта реально меняет родителя —
    // это редкое событие (поворот экрана), а не горячий путь клика, поэтому трогать не стали.
    private IEnumerator TransitionOrientationRoutine()
    {
        SetAllHovers(false);
        yield return null;
        Canvas.ForceUpdateCanvases();
        float elapsed = 0f;
        transitionStates.Clear();
        int unselectedSlotIndex = 0;

        foreach (var card in allCards)
        {
            RectTransform targetParent = GetTargetParentForCard(card, ref unselectedSlotIndex);
            if (targetParent == null) continue;

            Vector3 startWorldPos = card.rect.position;
            card.rect.SetParent(targetParent, false);

            // ИСПОЛЬЗУЕМ SetAsStretchChild ВМЕСТО SetAsFixedCenterAnchor
            SetAsStretchChild(card.rect);

            card.rect.position = startWorldPos;
            Vector2 startAnchored = card.rect.anchoredPosition;
            Vector2 startSize = card.rect.sizeDelta;

            // Целевой размер теперь всегда равен размеру родителя (0, 0), так как карточка растянута (offset)
            Vector2 targetSize = Vector2.zero;

            Vector3 startScale = card.rect.localScale;

            transitionStates[card] = new CardTransitionState
            {
                startAnchored = startAnchored,
                startSize = startSize,
                targetSize = targetSize,
                startScale = startScale,
                // Анимация размера нужна, если текущий offsetDelta не равен нулю
                needSizeAnim = (startSize - targetSize).sqrMagnitude > 0.01f
            };
        }

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveT = motionCurve.Evaluate(t);

            foreach (var card in allCards)
            {
                if (!transitionStates.TryGetValue(card, out var state)) continue;
                RectTransform rt = card.rect;

                rt.anchoredPosition = Vector2.LerpUnclamped(state.startAnchored, Vector2.zero, curveT);

                if (state.needSizeAnim)
                {
                    // Анимируем sizeDelta (который теперь представляет offset) к нулю
                    rt.sizeDelta = Vector2.LerpUnclamped(state.startSize, state.targetSize, curveT);
                }

                Vector3 targetScale = GetTargetScaleForCard(card);
                rt.localScale = Vector3.LerpUnclamped(state.startScale, targetScale, curveT);
            }
            yield return null;
        }

        foreach (var card in allCards)
        {
            // В конце анимации гарантируем, что карточка идеально растянута по родителю
            SetAsStretchChild(card.rect);
            RefreshCardVisuals(card.rect);
        }

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

        activeAnimsBuffer.Clear();
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

            // Никакого SetParent и никакой смены якорей здесь больше нет: родитель карты
            // не менялся с Awake (или с последнего поворота экрана), поэтому текущее
            // anchoredPosition уже корректно в системе координат этого родителя.
            Vector2 startAnchored = card.rect.anchoredPosition;
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
                // Слот используется только как источник координат — читаем его мировую позицию
                // и конвертируем в anchoredPosition текущего (неизменного) родителя карты.
                Vector2 destAnchored = WorldPositionToLocalAnchored(card.rect, targetSlot.position);

                activeAnimsBuffer.Add(StartCoroutine(AnimateToSlot(
                    card, startAnchored, destAnchored, startScale, destScale, startRot, destRot
                )));
            }
        }

        foreach (var c in activeAnimsBuffer) yield return c;
        foreach (var card in allCards) RefreshCardVisuals(card.rect);
        SetAllHovers(true);

        if (selectedCardEntry != null)
        {
            if (selectedCardEntry.buttonComp != null) selectedCardEntry.buttonComp.interactable = false;
            if (selectedCardEntry.hoverEffect != null) selectedCardEntry.hoverEffect.SetSelectedMode(true);
        }
    }

    private IEnumerator AnimateToSlot(CardEntry card, Vector2 startAnchored, Vector2 destAnchored, Vector3 startScale, Vector3 destScale, Quaternion startRot, Quaternion destRot)
    {
        float elapsed = 0f;
        RectTransform target = card.rect;

        // sizeDelta здесь больше не анимируется вообще: она равна GetHomeSize(card) с момента
        // Awake и не меняется ни при выборе карты, ни при возврате в сетку — весь визуальный
        // масштаб (превью крупнее, нижний ряд мельче) даёт только localScale.
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveT = motionCurve.Evaluate(t);

            target.anchoredPosition = Vector2.Lerp(startAnchored, destAnchored, curveT);
            target.localScale = Vector3.Lerp(startScale, destScale, curveT);
            target.localRotation = Quaternion.Lerp(startRot, destRot, curveT);

            yield return null;
        }

        target.anchoredPosition = destAnchored;
        target.localScale = destScale;
        target.localRotation = destRot;
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
        activeAnimsBuffer.Clear();

        foreach (var card in allCards)
        {
            if (card.rect != null)
            {
                Vector2 startAnchored = card.rect.anchoredPosition;
                Vector3 startScale = card.rect.localScale;
                Quaternion startRot = card.rect.localRotation;

                RectTransform targetHome = isPortrait ? card.portraitHome : card.landscapeHome;

                Vector2 destAnchored = targetHome != null
                    ? WorldPositionToLocalAnchored(card.rect, targetHome.position)
                    : Vector2.zero;

                float randomZ = Random.Range(-randomRotationRange, randomRotationRange);
                Quaternion randomRot = Quaternion.Euler(0, 0, randomZ);

                Vector3 destScale = isPortrait ? portraitGridScale : card.initialScale;

                activeAnimsBuffer.Add(StartCoroutine(AnimateHome(
                    card, startAnchored, destAnchored, startScale, destScale, startRot, randomRot
                )));
            }
        }

        foreach (var c in activeAnimsBuffer) yield return c;
        foreach (var card in allCards) RefreshCardVisuals(card.rect);
        RefreshAllCards();
        SetAllHovers(true);
    }

    private IEnumerator AnimateHome(CardEntry card, Vector2 startAnchored, Vector2 destAnchored, Vector3 startScale, Vector3 destScale, Quaternion startRot, Quaternion destRot)
    {
        float elapsed = 0f;
        RectTransform target = card.rect;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveT = motionCurve.Evaluate(t);

            target.anchoredPosition = Vector2.Lerp(startAnchored, destAnchored, curveT);
            target.localScale = Vector3.Lerp(startScale, destScale, curveT);
            target.localRotation = Quaternion.Lerp(startRot, destRot, curveT);

            yield return null;
        }

        target.anchoredPosition = destAnchored;
        target.localScale = destScale;
        target.localRotation = destRot;

        // ВАЖНО: Возвращаем привязку по краям после окончания анимации
        SetAsStretchChild(target);
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
        // ОСТАВЛЯЕМ ПУСТЫМ! Unity сам перестраивает UI в конце кадра.
        // Вызов ForceRebuildLayoutImmediate и ForceMeshUpdate здесь убивает FPS.
    }
}