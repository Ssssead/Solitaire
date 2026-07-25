using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardBackSelectionUI : MonoBehaviour
{
    [Header("Базы данных")]
    public CardSpriteDatabase baseSpriteDb;
    public CardSpriteDatabase premiumSpriteDb;
    public CardSpriteDatabase thirdSpriteDb;

    [Header("Якоря (Места назначения)")]
    public RectTransform tableAnchor;
    public RectTransform[] sideSlots = new RectTransform[7];

    [Header("Сами карты (8 штук)")]
    public CardData[] allBackCards = new CardData[8];

    [Header("Настройки анимации полета")]
    public float flightDuration = 0.35f;
    public AnimationCurve flightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Настройки визуала")]
    public float tableScale = 0.3f;
    public float sideScale = 0.2f;
    public float sideBaseRotation = 90f;
    public float maxRandomRotation = 4f;

    [Header("Настройки Hover (Наведение)")]
    public float hoverScaleMultiplier = 1.05f;
    public float hoverSpeed = 15f;

    private Dictionary<Transform, Coroutine> hoverCoroutines = new Dictionary<Transform, Coroutine>();
    private int activeIndex;
    private Coroutine[] moveCoroutines = new Coroutine[8];
    private Coroutine globalFlipCoroutine; // Для анимации смены колоды
    private int lastWidth;
    private int lastHeight;

    public CardSpriteDatabase ActiveSpriteDb
    {
        get
        {
            int selectedIndex = PlayerPrefs.GetInt("SelectedDeckStyle", 0);
            if (selectedIndex == 1 && premiumSpriteDb != null) return premiumSpriteDb;
            if (selectedIndex == 2 && thirdSpriteDb != null) return thirdSpriteDb;
            return baseSpriteDb;
        }
    }
    private void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            AdjustScaleForResolution(); // Пересчитываем tableScale и sideScale
            ApplyDynamicScale();        // Применяем изменения
        }
    }
    private void ApplyDynamicScale()
    {
        // Обновляем размеры только если сейчас нет глобального переворота колоды
        if (globalFlipCoroutine == null)
        {
            for (int i = 0; i < allBackCards.Length; i++)
            {
                if (allBackCards[i] == null) continue;

                // Если карта сейчас не летит анимацией, плавно правим её масштаб под экран
                if (moveCoroutines[i] == null)
                {
                    float currentScale = (i == activeIndex) ? tableScale : sideScale;
                    allBackCards[i].transform.localScale = new Vector3(currentScale, currentScale, 1f);
                }
            }
        }
    }
    private void Start()
    {
        

        activeIndex = PlayerPrefs.GetInt("SelectedBackIndex", 0);
        for (int i = 0; i < allBackCards.Length; i++)
        {
            if (allBackCards[i] == null) continue;

            Button btn = allBackCards[i].gameObject.GetComponent<Button>();
            if (btn == null) btn = allBackCards[i].gameObject.AddComponent<Button>();

            // Фикс белой прозрачности кнопки (убираем полупрозрачность неактивной карты)
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.disabledColor = Color.white;
            cb.colorMultiplier = 1f;
            btn.colors = cb;

            btn.enabled = true;
            btn.interactable = true;

            CanvasGroup cg = allBackCards[i].GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }

            btn.onClick.RemoveAllListeners();
            int indexToSelect = i;
            btn.onClick.AddListener(() => OnCardClicked(indexToSelect));

            // Активируем отслеживание мыши
            SetupHoverEvents(allBackCards[i], i);
        }

        RefreshDeckSprites(true); // Мгновенная настройка спрайтов при старте
        ArrangeCards(true);       // Мгновенная расстановка при старте
    }
    private void AdjustScaleForResolution()
    {
        float targetAspect = 16f / 9f;
        float currentAspect = (float)Screen.width / Screen.height;
        float multiplier = 1f;

        if (currentAspect > targetAspect)
        {
            multiplier = targetAspect / currentAspect;
        }
        else if (currentAspect < targetAspect)
        {
            float rawGrow = targetAspect / currentAspect;
            multiplier = Mathf.Min(rawGrow, 1.15f); // Тот же безопасный лимит
        }

        tableScale = 0.3f * multiplier;
        sideScale = 0.2f * multiplier;
    }
    // --- ОБНОВЛЕНИЕ РУБАШЕК ПРИ СМЕНЕ КОЛОДЫ ---
    public void RefreshDeckSprites(bool instant)
    {
        CardSpriteDatabase db = ActiveSpriteDb;
        if (db == null) return;
        db.BuildCache();

        if (instant)
        {
            for (int i = 0; i < allBackCards.Length; i++)
            {
                if (allBackCards[i] == null) continue;
                allBackCards[i].UpdateBackVisual(db.backSprites[i]);
                allBackCards[i].SetFaceUp(false, false);
            }
        }
        else
        {
            // ИСПРАВЛЕНИЕ ОШИБКИ INACTIVE GAMEOBJECT:
            // Так как сама панель сейчас может быть выключена, мы просим запустить корутину 
            // ту карту, которая сейчас лежит на столе (она гарантированно активна!)
            CardData activeCard = allBackCards[activeIndex];

            if (globalFlipCoroutine != null) activeCard.StopCoroutine(globalFlipCoroutine);
            globalFlipCoroutine = activeCard.StartCoroutine(FlipAllBacksRoutine(db));
        }
    }

    private IEnumerator FlipAllBacksRoutine(CardSpriteDatabase db)
    {
        float flipHalfSpeed = 0.15f;

        // 1. Сжимаем все карты по оси X (эффект переворота)
        float elapsed = 0f;
        while (elapsed < flipHalfSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flipHalfSpeed;
            for (int i = 0; i < allBackCards.Length; i++)
            {
                if (allBackCards[i] == null) continue;
                float baseS = (i == activeIndex) ? tableScale : sideScale;
                allBackCards[i].transform.localScale = new Vector3(Mathf.Lerp(baseS, 0, t), baseS, 1f);
            }
            yield return null;
        }

        // 2. Меняем спрайты, пока карты "плоские"
        for (int i = 0; i < allBackCards.Length; i++)
        {
            if (allBackCards[i] == null) continue;
            allBackCards[i].UpdateBackVisual(db.backSprites[i]);
        }

        // 3. Разжимаем обратно
        elapsed = 0f;
        while (elapsed < flipHalfSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flipHalfSpeed;
            for (int i = 0; i < allBackCards.Length; i++)
            {
                if (allBackCards[i] == null) continue;
                float baseS = (i == activeIndex) ? tableScale : sideScale;
                allBackCards[i].transform.localScale = new Vector3(Mathf.Lerp(0, baseS, t), baseS, 1f);
            }
            yield return null;
        }

        // Жестко фиксируем финальный размер для надежности
        for (int i = 0; i < allBackCards.Length; i++)
        {
            if (allBackCards[i] == null) continue;
            float baseS = (i == activeIndex) ? tableScale : sideScale;
            allBackCards[i].transform.localScale = new Vector3(baseS, baseS, 1f);
        }

        globalFlipCoroutine = null;
    }

    // --- ЛОГИКА НАВЕДЕНИЯ МЫШИ (HOVER) ---
    private void SetupHoverEvents(CardData cardData, int index)
    {
        GameObject cardObj = cardData.gameObject;
        EventTrigger trigger = cardObj.GetComponent<EventTrigger>() ?? cardObj.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) => StartHover(index, true));
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) => StartHover(index, false));
        trigger.triggers.Add(exit);
    }

    private void StartHover(int index, bool isEnter)
    {
        // Не увеличиваем, если карта летит или сейчас идет анимация смены колоды
        if (moveCoroutines[index] != null || globalFlipCoroutine != null) return;

        CardData card = allBackCards[index];
        Transform target = card.transform;

        if (hoverCoroutines.ContainsKey(target) && hoverCoroutines[target] != null)
            card.StopCoroutine(hoverCoroutines[target]);

        // Определяем базовый масштаб: на столе он больше, в панели меньше
        float baseScale = (index == activeIndex) ? tableScale : sideScale;
        float targetScaleF = isEnter ? baseScale * hoverScaleMultiplier : baseScale;

        // Запускаем корутину на самой карте
        hoverCoroutines[target] = card.StartCoroutine(HoverRoutine(target, targetScaleF, index));
    }

    private IEnumerator HoverRoutine(Transform target, float targetScaleF, int index)
    {
        Vector3 targetScale = new Vector3(targetScaleF, targetScaleF, 1f);
        while (Vector3.Distance(target.localScale, targetScale) > 0.001f)
        {
            // Прерываем увеличение, если игрок кликнул и начался полет или смена колоды
            if (moveCoroutines[index] != null || globalFlipCoroutine != null) yield break;

            target.localScale = Vector3.Lerp(target.localScale, targetScale, Time.unscaledDeltaTime * hoverSpeed);
            yield return null;
        }
        target.localScale = targetScale;
    }

    // --- ЛОГИКА КЛИКА И ПОЛЕТА ---
    public void OnCardClicked(int clickedIndex)
    {
        if (clickedIndex == activeIndex) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("BG_Switch");

        activeIndex = clickedIndex;
        PlayerPrefs.SetInt("SelectedBackIndex", activeIndex);
        PlayerPrefs.Save();

        ArrangeCards(false);
    }

    private void ArrangeCards(bool instant)
    {
        int currentSideSlot = 0;

        for (int i = 0; i < allBackCards.Length; i++)
        {
            if (allBackCards[i] == null) continue;

            Transform targetAnchor = null;
            float targetScaleF;
            Quaternion targetRot;
            bool isTable = (i == activeIndex);

            if (isTable)
            {
                targetAnchor = tableAnchor;
                targetScaleF = tableScale;
                targetRot = Quaternion.Euler(0, 0, Random.Range(-maxRandomRotation, maxRandomRotation));
                allBackCards[i].GetComponent<Button>().interactable = false;
            }
            else
            {
                if (currentSideSlot < sideSlots.Length && sideSlots[currentSideSlot] != null)
                {
                    targetAnchor = sideSlots[currentSideSlot];
                }
                currentSideSlot++;

                targetScaleF = sideScale;
                targetRot = Quaternion.Euler(0, 0, sideBaseRotation + Random.Range(-maxRandomRotation, maxRandomRotation));
                allBackCards[i].GetComponent<Button>().interactable = true;
            }

            Vector3 targetScale = new Vector3(targetScaleF, targetScaleF, 1f);

            if (targetAnchor == null) targetAnchor = transform;

            if (instant)
            {
                allBackCards[i].transform.SetParent(targetAnchor);
                allBackCards[i].transform.localPosition = Vector3.zero;
                allBackCards[i].transform.localScale = targetScale;
                allBackCards[i].transform.localRotation = targetRot;
            }
            else
            {
                Canvas rootCanvas = GetComponentInParent<Canvas>();
                if (rootCanvas != null)
                {
                    allBackCards[i].transform.SetParent(rootCanvas.transform, true);
                    allBackCards[i].transform.SetAsLastSibling();
                }

                if (moveCoroutines[i] != null) StopCoroutine(moveCoroutines[i]);
                moveCoroutines[i] = StartCoroutine(FlyRoutine(allBackCards[i].transform, targetAnchor, targetScale, targetRot, i));
            }
        }
    }

    private IEnumerator FlyRoutine(Transform target, Transform destAnchor, Vector3 destScale, Quaternion destRot, int index)
    {
        Vector3 startPos = target.position;
        Vector3 startScale = target.localScale;
        Quaternion startRot = target.localRotation;

        float elapsed = 0f;
        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;
            float t = flightCurve.Evaluate(elapsed / flightDuration);

            Vector3 currentDestPos = destAnchor != null ? destAnchor.position : startPos;

            target.position = Vector3.LerpUnclamped(startPos, currentDestPos, t);
            target.localScale = Vector3.LerpUnclamped(startScale, destScale, t);
            target.localRotation = Quaternion.LerpUnclamped(startRot, destRot, t);

            yield return null;
        }

        if (destAnchor != null)
        {
            target.SetParent(destAnchor);
            target.localPosition = Vector3.zero;
        }

        target.localScale = destScale;
        target.localRotation = destRot;
        moveCoroutines[index] = null;
    }
}