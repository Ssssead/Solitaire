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
        [HideInInspector] public Vector2 initialPos;
        // initialRot удален, так как мы генерируем его случайно каждый раз
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

    public void SetHomePosition(RectTransform cardRect, Vector2 pos)
    {
        foreach (var entry in allCards)
        {
            if (entry.rect == cardRect)
            {
                entry.initialPos = pos;
                return;
            }
        }
    }

    // Метод CaptureCurrentRotationsAsHome удален, он больше не нужен

    private void SetAllHovers(bool state)
    {
        foreach (var card in allCards)
        {
            if (card.hoverEffect != null)
                card.hoverEffect.SetHoverEnabled(state);
        }
    }

    private void ResetRenderPriority()
    {
        foreach (var card in allCards)
        {
            if (card.hoverEffect != null)
                card.hoverEffect.SetPriorityRender(false);
        }
    }

    private void RefreshCardVisuals(RectTransform card)
    {
        if (card == null) return;
        Vector2 pos = card.anchoredPosition;
        card.anchoredPosition = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));

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
        ResetRenderPriority();

        StartCoroutine(SelectCardRoutine(selectedType));
    }

    private IEnumerator SelectCardRoutine(GameType selectedType)
    {
        int bottomSlotIndex = 0;
        List<Coroutine> activeAnims = new List<Coroutine>();

        CardEntry selectedCardEntry = null;

        foreach (var card in allCards)
        {
            if (card.rect == null) continue;

            if (card.type == selectedType)
            {
                selectedCardEntry = card;
                // Выбранная карта -> Превью -> Ровно (0 градусов)
                activeAnims.Add(StartCoroutine(AnimateRoutine(
                    card.rect,
                    previewAnchor.anchoredPosition,
                    selectedScale,
                    Quaternion.identity
                )));
            }
            else
            {
                if (bottomSlotIndex < bottomSlots.Count)
                {
                    Vector2 targetPos = bottomSlots[bottomSlotIndex].anchoredPosition;

                    // ГЕНЕРИРУЕМ НОВЫЙ СЛУЧАЙНЫЙ НАКЛОН ПРИ СПУСКЕ ВНИЗ
                    float randomZ = Random.Range(-randomRotationRange, randomRotationRange);
                    Quaternion randomRot = Quaternion.Euler(0, 0, randomZ);

                    activeAnims.Add(StartCoroutine(AnimateRoutine(
                        card.rect,
                        targetPos,
                        bottomScale,
                        randomRot
                    )));
                    bottomSlotIndex++;
                }

                if (card.buttonComp != null) card.buttonComp.interactable = true;
                if (card.hoverEffect != null) card.hoverEffect.SetSelectedMode(false);
            }
        }

        foreach (var c in activeAnims) yield return c;

        foreach (var card in allCards) RefreshCardVisuals(card.rect);

        SetAllHovers(true);

        if (selectedCardEntry != null)
        {
            if (selectedCardEntry.buttonComp != null)
                selectedCardEntry.buttonComp.interactable = false;

            if (selectedCardEntry.hoverEffect != null)
            {
                selectedCardEntry.hoverEffect.SetPriorityRender(true);
                selectedCardEntry.hoverEffect.SetSelectedMode(true);
            }
        }
    }

    public void ResetGrid()
    {
        StopAllCoroutines();
        SetAllHovers(false);
        ResetRenderPriority();

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
                // ГЕНЕРИРУЕМ НОВЫЙ СЛУЧАЙНЫЙ НАКЛОН ПРИ ВОЗВРАТЕ ДОМОЙ
                float randomZ = Random.Range(-randomRotationRange, randomRotationRange);
                Quaternion randomRot = Quaternion.Euler(0, 0, randomZ);

                activeAnims.Add(StartCoroutine(AnimateRoutine(
                    card.rect,
                    card.initialPos,
                    Vector3.one,
                    randomRot
                )));
            }
        }

        foreach (var c in activeAnims) yield return c;

        foreach (var card in allCards) RefreshCardVisuals(card.rect);

        SetAllHovers(true);
    }

    private IEnumerator AnimateRoutine(RectTransform target, Vector2 destPos, Vector3 destScale, Quaternion destRot)
    {
        Vector2 startPos = target.anchoredPosition;
        Vector3 startScale = target.localScale;
        Quaternion startRot = target.localRotation;

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float curveT = motionCurve.Evaluate(t);

            target.anchoredPosition = Vector2.Lerp(startPos, destPos, curveT);
            target.localScale = Vector3.Lerp(startScale, destScale, curveT);
            target.localRotation = Quaternion.Lerp(startRot, destRot, curveT);

            yield return null;
        }

        target.anchoredPosition = destPos;
        target.localScale = destScale;
        target.localRotation = destRot;
    }
}