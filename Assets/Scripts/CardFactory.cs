// CardFactory.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Фабрика для создания карт в Unity UI.
/// Создаёт визуальные GameObject карт с компонентами CardController и CardData.
/// </summary>
public class CardFactory : MonoBehaviour
{
    [Header("Prefabs & Resources")]
    [Tooltip("Префаб карты (должен содержать Image и RectTransform)")]
    public GameObject cardPrefab;

    [Tooltip("База данных спрайтов карт")]
    public CardSpriteDatabase spriteDb;

    [Tooltip("Canvas для отрисовки карт")]
    public Canvas rootCanvas;

    [Tooltip("Шаблон CardController для копирования настроек (опционально)")]
    public CardController cardControllerTemplate;

    [Header("Responsive Sizing")]
    [Tooltip("Перетащите сюда любой пустой слот (например, первый Дом или слот Колоды).")]
    public RectTransform referenceSlot;

    [Tooltip("Автоматически подстраивать размер карт под ширину referenceSlot, сохраняя пропорции")]
    public bool autoResizeCards = true;

    [Header("Card Settings")]
    [Tooltip("Размер карты по умолчанию (укажите идеальный размер, например 85 на 125, для сохранения пропорций)")]
    public Vector2 defaultCardSize = new Vector2(85f, 125f);

    // Кэш созданных карт для отладки/управления
    private List<CardController> createdCards = new List<CardController>();

    private void Reset()
    {
        if (rootCanvas == null) rootCanvas = FindObjectOfType<Canvas>();
    }

    private void Awake()
    {
        if (cardPrefab == null) Debug.LogError("[CardFactory] cardPrefab is not assigned!");
        if (rootCanvas == null)
        {
            rootCanvas = FindObjectOfType<Canvas>();
            if (rootCanvas == null) Debug.LogError("[CardFactory] rootCanvas not found!");
        }
    }

    /// <summary>
    /// Создаёт визуальный GameObject карты с заданной моделью.
    /// </summary>
    /// <param name="model">Модель карты (масть и ранг)</param>
    /// <param name="parent">Родительский Transform для карты</param>
    /// <param name="anchoredPos">Начальная anchored position</param>
    /// <returns>CardController созданной карты</returns>
    public CardController CreateCard(CardModel model, Transform parent, Vector2 anchoredPos)
    {
        if (cardPrefab == null)
        {
            Debug.LogError("[CardFactory] Cannot create card: cardPrefab is null!");
            return null;
        }

        if (parent == null)
        {
            Debug.LogWarning("[CardFactory] Parent is null, using rootCanvas as parent.");
            parent = rootCanvas != null ? rootCanvas.transform : transform;
        }

        // --- ДИНАМИЧЕСКИЙ РАСЧЕТ РАЗМЕРА С СОХРАНЕНИЕМ ПРОПОРЦИЙ ---
        // Используем локальную переменную finalSize, чтобы не сломать эталонные пропорции в defaultCardSize
        Vector2 finalSize = defaultCardSize;

        if (autoResizeCards)
        {
            float targetWidth = finalSize.x;

            // 1. Приоритет: берем ТОЛЬКО ширину у специально заданного слота-шаблона
            if (referenceSlot != null && referenceSlot.rect.width > 10)
            {
                targetWidth = referenceSlot.rect.width;
            }
            // 2. Если шаблона нет, пытаемся взять ширину у родителя (куда спавним карту)
            else if (parent != null)
            {
                RectTransform parentRect = parent.GetComponent<RectTransform>();
                // Защита: чтобы не скопировать ширину всего экрана
                if (parentRect != null && parentRect.rect.width > 10 && parentRect.rect.width < Screen.width * 0.5f)
                {
                    targetWidth = parentRect.rect.width;
                }
            }

            // Математически высчитываем идеальную высоту на основе ширины слота
            // Сохраняем пропорции, которые вы задали в инспекторе (например, 85 / 125)
            if (defaultCardSize.y > 0 && defaultCardSize.x > 0)
            {
                float aspectRatio = defaultCardSize.x / defaultCardSize.y;
                finalSize = new Vector2(targetWidth, targetWidth / aspectRatio);
            }
        }
        // -----------------------------------------------------------

        // Создаём экземпляр карты
        GameObject cardObj = Instantiate(cardPrefab, parent, false);
        cardObj.name = $"Card_{model.suit}_{model.rank}";

        // Проверяем что объект активен
        if (!cardObj.activeSelf)
        {
            cardObj.SetActive(true);
        }

        // Настраиваем RectTransform
        RectTransform rect = cardObj.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = cardObj.AddComponent<RectTransform>();
        }

        rect.anchoredPosition = anchoredPos;

        // Устанавливаем рассчитанный размер (идеальные пропорции!)
        rect.sizeDelta = finalSize;

        // КРИТИЧНО: убедимся что scale правильный
        if (rect.localScale != Vector3.one)
        {
            rect.localScale = Vector3.one;
        }

        // Настраиваем CardData
        CardData cardData = cardObj.GetComponent<CardData>();
        if (cardData == null)
        {
            cardData = cardObj.AddComponent<CardData>();
        }

        // Получаем спрайт карты
        Sprite cardSprite = null;
        if (spriteDb != null)
        {
            cardSprite = spriteDb.GetSprite(model.suit, model.rank);
        }

        cardData.SetModel(model, cardSprite);

        // Настраиваем CardController
        CardController cardController = cardObj.GetComponent<CardController>();
        if (cardController == null)
        {
            cardController = cardObj.AddComponent<CardController>();
        }

        cardController.cardModel = model;
        cardController.canvas = rootCanvas;

        // Копируем настройки из шаблона, если он есть
        if (cardControllerTemplate != null)
        {
            CopyCardControllerSettings(cardControllerTemplate, cardController);
        }

        // Добавляем CanvasGroup если его нет (нужен для drag & drop)
        CanvasGroup canvasGroup = cardObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = cardObj.AddComponent<CanvasGroup>();
        }

        // Убеждаемся что CanvasGroup видим
        if (canvasGroup.alpha <= 0f) canvasGroup.alpha = 1f;

        // Проверяем Image компонент
        var image = cardObj.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            if (!image.enabled) image.enabled = true;

            if (image.color.a <= 0f)
            {
                var col = image.color;
                col.a = 1f;
                image.color = col;
            }
        }

        // Сохраняем в кэш
        createdCards.Add(cardController);

        return cardController;
    }

    /// <summary>
    /// Копирует настройки из одного CardController в другой.
    /// </summary>
    private void CopyCardControllerSettings(CardController source, CardController target)
    {
        if (source == null || target == null) return;

        // Копируем публичные настройки
        target.canvas = source.canvas ?? rootCanvas;
        // Здесь можно добавить копирование других настроек при необходимости
    }

    /// <summary>
    /// Создаёт полную колоду из 52 карт (не перемешанную).
    /// </summary>
    /// <returns>Список из 52 моделей карт</returns>
    public List<CardModel> CreateFullDeck()
    {
        List<CardModel> deck = new List<CardModel>(52);

        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                deck.Add(new CardModel(suit, rank));
            }
        }

        return deck;
    }

    /// <summary>
    /// Создаёт и перемешивает колоду карт.
    /// </summary>
    /// <param name="seed">Seed для генератора случайных чисел. -1 = случайный seed.</param>
    /// <returns>Перемешанный список из 52 моделей карт</returns>
    public List<CardModel> CreateShuffledDeck(int seed = -1)
    {
        List<CardModel> deck = CreateFullDeck();

        // Создаём генератор случайных чисел
        System.Random rng = (seed >= 0)
            ? new System.Random(seed)
            : new System.Random();

        // Алгоритм Fisher-Yates для перемешивания
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);

            // Меняем местами элементы
            CardModel temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }

        return deck;
    }

    /// <summary>
    /// Уничтожает все созданные карты (для перезапуска игры).
    /// </summary>
    public void DestroyAllCards()
    {
        foreach (var card in createdCards)
        {
            if (card != null && card.gameObject != null)
            {
                Destroy(card.gameObject);
            }
        }

        createdCards.Clear();
    }

    /// <summary>
    /// Возвращает количество созданных карт в сцене.
    /// </summary>
    public int GetCreatedCardCount()
    {
        // Очищаем null-ссылки из списка
        createdCards.RemoveAll(card => card == null);
        return createdCards.Count;
    }

    /// <summary>
    /// Проверяет валидность фабрики (все ли зависимости на месте).
    /// </summary>
    public bool IsValid()
    {
        bool valid = true;

        if (cardPrefab == null)
        {
            Debug.LogError("[CardFactory] Validation failed: cardPrefab is null");
            valid = false;
        }

        if (rootCanvas == null)
        {
            Debug.LogError("[CardFactory] Validation failed: rootCanvas is null");
            valid = false;
        }

        if (spriteDb == null)
        {
            Debug.LogWarning("[CardFactory] Validation warning: spriteDb is null (cards may not display)");
        }

        return valid;
    }

    /// <summary>
    /// Вызывается при уничтожении фабрики - очистка ресурсов.
    /// </summary>
    private void OnDestroy()
    {
        // Очищаем кэш (GameObject'ы будут уничтожены автоматически Unity)
        createdCards.Clear();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Отладочная информация для редактора.
    /// </summary>
    [ContextMenu("Debug: Show Created Cards Count")]
    private void DebugShowCardCount()
    {
        Debug.Log($"[CardFactory] Created cards in scene: {GetCreatedCardCount()}");
    }

    [ContextMenu("Debug: Validate Factory")]
    private void DebugValidate()
    {
        if (IsValid())
        {
            Debug.Log("[CardFactory] Factory is valid and ready to use!");
        }
        else
        {
            Debug.LogError("[CardFactory] Factory validation failed! Check errors above.");
        }
    }
#endif
}