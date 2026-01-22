using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class WasteShadow : MonoBehaviour
{
    [Header("Settings")]
    public Sprite shadowSprite; // Спрайт фона карты (9-slice)

    [Header("Dimensions")]
    public float cardWidth = 85f;   // Ширина карты
    public float cardHeight = 125f; // Высота карты
    public float slotGap = 35f;     // Отступ (как вы просили)

    [Header("Shadow Appearance")]
    public Vector2 offset = new Vector2(0.1f, -0.1f);
    [Range(0f, 1f)] public float alpha = 0.4f;

    // Внутренние переменные
    private GameObject shadowObj;
    private RectTransform shadowRect;
    private Image shadowImg;
    private CanvasGroup canvasGroup;

    // Список найденных слотов
    private List<Transform> detectedSlots = new List<Transform>();
    private bool initialized = false;

    private void LateUpdate()
    {
        // 1. Автоматический поиск слотов, если они еще не найдены
        if (!initialized || detectedSlots.Count == 0)
        {
            TryFindSlots();
            if (!initialized) return; // Если все еще не нашли, ждем следующего кадра
        }

        // 2. Считаем сколько карт сейчас реально лежит в сбросе
        int activeCards = CountActiveCards();

        // Если карт нет - прячем тень
        if (activeCards == 0)
        {
            if (shadowObj != null && shadowObj.activeSelf) shadowObj.SetActive(false);
            return;
        }

        // 3. Создаем объект тени, если его нет
        if (shadowObj == null) CreateShadow();
        if (!shadowObj.activeSelf) shadowObj.SetActive(true);

        // 4. РАСЧЕТ РАЗМЕРА ТЕНИ
        // 1 карта = cardWidth
        // 2 карты = cardWidth + 35
        // 3 карты = cardWidth + 35 + 35
        float totalWidth = cardWidth + ((activeCards - 1) * slotGap);

        // Применяем размер
        shadowRect.sizeDelta = new Vector2(totalWidth, cardHeight);

        // 5. Позиционирование
        // Тень должна начинаться там же, где первый слот (Slot_0)
        if (detectedSlots.Count > 0 && detectedSlots[0] != null)
        {
            // Привязываемся к локальной позиции первого слота + смещение тени
            Vector2 basePos = detectedSlots[0].localPosition;

            // Сдвигаем тень так, чтобы она росла вправо от первого слота
            // Учитываем Pivot (0, 0.5)
            shadowRect.localPosition = basePos + offset;
        }

        // Гарантируем, что тень под картами (индекс 0)
        if (shadowRect.GetSiblingIndex() != 0) shadowRect.SetAsFirstSibling();
    }

    private void TryFindSlots()
    {
        detectedSlots.Clear();
        // Ищем детей с именами "Slot_0", "Slot_1", и т.д.
        // Так как этот скрипт висит на Slot0 (родителе), мы ищем среди своих детей.
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Slot_"))
            {
                detectedSlots.Add(child);
            }
        }

        // Сортируем по имени, чтобы Slot_0 был первым
        detectedSlots.Sort((a, b) => string.Compare(a.name, b.name));

        if (detectedSlots.Count > 0)
        {
            initialized = true;
        }
    }

    private int CountActiveCards()
    {
        int count = 0;
        foreach (var slot in detectedSlots)
        {
            if (slot == null) continue;

            // Проверяем детей слота.
            // В Slot_X должны лежать карты. Игнорируем саму тень, если она вдруг туда попала.
            // (Хотя тень мы создаем в родителе Slot0, так что в слотах только карты)
            if (slot.childCount > 0)
            {
                count++;
            }
        }
        // Ограничиваем счетчик кол-вом слотов (обычно 3), 
        // так как тень не должна расти бесконечно, если логика WastePile это не поддерживает
        return Mathf.Clamp(count, 0, detectedSlots.Count);
    }

    private void CreateShadow()
    {
        shadowObj = new GameObject("Waste_Unified_Shadow");
        shadowRect = shadowObj.AddComponent<RectTransform>();

        // Делаем тень дочерним объектом Slot0 (где висит этот скрипт)
        shadowObj.transform.SetParent(transform, false);

        // Настройка Pivot: (0, 0.5) - левый край, центр по вертикали
        // Это важно, чтобы при увеличении ширины тень росла вправо
        shadowRect.pivot = new Vector2(0f, 0.5f);
        shadowRect.anchorMin = new Vector2(0f, 0.5f);
        shadowRect.anchorMax = new Vector2(0f, 0.5f);

        shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = shadowSprite;
        shadowImg.color = Color.black;
        shadowImg.type = Image.Type.Sliced; // Чтобы углы не искажались
        shadowImg.raycastTarget = false;

        // Корректировка пикселей (Pixel Perfect)
        if (shadowSprite != null && cardWidth > 0)
        {
            float ratio = shadowSprite.rect.width / cardWidth;
            shadowImg.pixelsPerUnitMultiplier = ratio;
        }

        canvasGroup = shadowObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = alpha;
        canvasGroup.blocksRaycasts = false;

        shadowObj.transform.SetAsFirstSibling();
    }
}