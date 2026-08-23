// CardFactory.cs
using System;
using System.Collections;
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

    // --- ИЗМЕНЕНИЯ ЗДЕСЬ: Две базы и хитрое свойство ---
    [Tooltip("Базовая база данных спрайтов (Base)")]
    public CardSpriteDatabase baseSpriteDb;

    [Tooltip("Премиум база данных спрайтов (Premium)")]
    public CardSpriteDatabase premiumSpriteDb;

    [Tooltip("Третья база данных спрайтов")]
    public CardSpriteDatabase thirdSpriteDb;

    // Это свойство заменяет старую переменную. Оно вернет нужную базу, 
    // и FoundationPile.cs больше не будет выдавать ошибку.
    public CardSpriteDatabase spriteDb
    {
        get
        {
            int selectedIndex = PlayerPrefs.GetInt("SelectedDeckStyle", 0);
            if (selectedIndex == 1 && premiumSpriteDb != null) return premiumSpriteDb;
            if (selectedIndex == 2 && thirdSpriteDb != null) return thirdSpriteDb;
            return baseSpriteDb;
        }
    }
    // ---------------------------------------------------

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
    private Vector2 lastScreenSize;
    private Coroutine resizeCoroutine;
    private void Reset()
    {
        if (rootCanvas == null) rootCanvas = FindObjectOfType<Canvas>();
    }
    private void Start()
    {
        // Запоминаем изначальное разрешение при старте
        lastScreenSize = new Vector2(Screen.width, Screen.height);
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
    private void Update()
    {
        // Если разрешение экрана изменилось по ходу игры
        if (Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
        {
            lastScreenSize = new Vector2(Screen.width, Screen.height);

            // Если корутина уже запущена — останавливаем её, чтобы не было дублей
            if (resizeCoroutine != null)
            {
                StopCoroutine(resizeCoroutine);
            }
            // Запускаем отложенное обновление размеров
            resizeCoroutine = StartCoroutine(DelayedResizeCards());
        }
    }
    private IEnumerator DelayedResizeCards()
    {
        // Ждем два кадра. За это время Unity 100% успеет пересчитать Grid Layout Group
        // и referenceSlot получит правильную актуальную ширину.
        yield return null;
        yield return null;

        // Принудительно обновляем канвас на всякий случай
        if (rootCanvas != null)
        {
            Canvas.ForceUpdateCanvases();
        }

        UpdateAllCardsSize();
        resizeCoroutine = null;
    }

    /// <summary>
    /// Метод пересчитывает размеры и применяет их ко всем существующим картам
    /// </summary>
    public void UpdateAllCardsSize()
    {
        if (!autoResizeCards) return; // Если авто-сайз выключен, ничего не делаем

        float targetWidth = defaultCardSize.x;
        Vector2 finalSize = defaultCardSize;

        // 1. Заново высчитываем нужную ширину, ориентируясь на referenceSlot
        if (referenceSlot != null && referenceSlot.rect.width > 10)
        {
            targetWidth = referenceSlot.rect.width;
        }
        else if (rootCanvas != null)
        {
            RectTransform parentRect = rootCanvas.GetComponent<RectTransform>();
            if (parentRect != null && parentRect.rect.width > 10 && parentRect.rect.width < Screen.width * 0.5f)
            {
                targetWidth = parentRect.rect.width;
            }
        }

        // 2. Рассчитываем итоговый размер с сохранением пропорций
        if (defaultCardSize.y > 0 && defaultCardSize.x > 0)
        {
            float aspectRatio = defaultCardSize.x / defaultCardSize.y;
            finalSize = new Vector2(targetWidth, targetWidth / aspectRatio);
        }

        // 3. Проходимся по списку ВСЕХ созданных карт и обновляем их размер
        foreach (var card in createdCards)
        {
            if (card != null && card.rectTransform != null)
            {
                card.rectTransform.sizeDelta = finalSize;
            }
        }
    }
    /// <summary>
    /// Создаёт визуальный GameObject карты с заданной моделью.
    /// </summary>
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

        Vector2 finalSize = defaultCardSize;

        if (autoResizeCards)
        {
            float targetWidth = finalSize.x;

            if (referenceSlot != null && referenceSlot.rect.width > 10)
            {
                targetWidth = referenceSlot.rect.width;
            }
            else if (parent != null)
            {
                RectTransform parentRect = parent.GetComponent<RectTransform>();
                if (parentRect != null && parentRect.rect.width > 10 && parentRect.rect.width < Screen.width * 0.5f)
                {
                    targetWidth = parentRect.rect.width;
                }
            }

            if (defaultCardSize.y > 0 && defaultCardSize.x > 0)
            {
                float aspectRatio = defaultCardSize.x / defaultCardSize.y;
                finalSize = new Vector2(targetWidth, targetWidth / aspectRatio);
            }
        }

        GameObject cardObj = Instantiate(cardPrefab, parent, false);
        cardObj.name = $"Card_{model.suit}_{model.rank}";

        if (!cardObj.activeSelf)
        {
            cardObj.SetActive(true);
        }

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = cardObj.AddComponent<RectTransform>();
        }

        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = finalSize;

        if (rect.localScale != Vector3.one)
        {
            rect.localScale = Vector3.one;
        }

        CardData cardData = cardObj.GetComponent<CardData>();
        if (cardData == null)
        {
            cardData = cardObj.AddComponent<CardData>();
        }

        // --- ИЗМЕНЕНИЯ ЗДЕСЬ ---
        Sprite cardSprite = null;
        if (spriteDb != null)
        {
            cardSprite = spriteDb.GetSprite(model.suit, model.rank);
            // Устанавливаем актуальную рубашку из выбранной БД
            cardData.backSprite = spriteDb.GetCurrentBackSprite();
        }

        cardData.SetModel(model, cardSprite);

        CardController cardController = cardObj.GetComponent<CardController>();
        if (cardController == null)
        {
            cardController = cardObj.AddComponent<CardController>();
        }

        cardController.cardModel = model;
        cardController.canvas = rootCanvas;

        if (cardControllerTemplate != null)
        {
            CopyCardControllerSettings(cardControllerTemplate, cardController);
        }

        CanvasGroup canvasGroup = cardObj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = cardObj.AddComponent<CanvasGroup>();
        }

        if (canvasGroup.alpha <= 0f) canvasGroup.alpha = 1f;

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

        createdCards.Add(cardController);

        return cardController;
    }

    private void CopyCardControllerSettings(CardController source, CardController target)
    {
        if (source == null || target == null) return;
        target.canvas = source.canvas ?? rootCanvas;
    }

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

    public List<CardModel> CreateShuffledDeck(int seed = -1)
    {
        List<CardModel> deck = CreateFullDeck();
        System.Random rng = (seed >= 0) ? new System.Random(seed) : new System.Random();

        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            CardModel temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }

        return deck;
    }

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

    public int GetCreatedCardCount()
    {
        createdCards.RemoveAll(card => card == null);
        return createdCards.Count;
    }

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

    private void OnDestroy()
    {
        createdCards.Clear();
    }

#if UNITY_EDITOR
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