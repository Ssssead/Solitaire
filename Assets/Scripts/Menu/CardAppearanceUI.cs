using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CardAppearanceUI : MonoBehaviour
{
    [Header("UI Ссылки")]
    public MenuController menuController;
    public DeckSelectionUI deckSelectionUI;

    [Header("Базы данных спрайтов")]
    public CardSpriteDatabase baseSpriteDb;
    public CardSpriteDatabase premiumSpriteDb;
    public CardSpriteDatabase thirdSpriteDb;

    [Header("Карты на сцене (13 лиц)")]
    public CardData[] faceUpCards = new CardData[13];

    [Header("Настройки анимации и масштаба карт")]
    public float flipSpeed = 0.15f;
    public float maxRandomRotation = 10f;

    public static event System.Action<int> OnSuitChangedGlobal;
    public static event System.Action<bool> OnRussianLettersChangedGlobal;

    [Header("Настройки Hover (Наведение)")]
    public float hoverScaleMultiplier = 1.05f;
    public float hoverSpeed = 15f;
    private Dictionary<Transform, Coroutine> hoverCoroutines = new Dictionary<Transform, Coroutine>();

    [Header("Кнопки выбора мастей")]
    public Button[] suitButtons = new Button[4];
    public Image[] suitIcons = new Image[4];

    [Header("Визуал кнопок мастей")]
    public Color activeBtnColor = new Color(1f, 0.8f, 0.2f);
    public Color defaultBtnColor = new Color(0.55f, 0.37f, 0.22f);
    public Color activeIconColor = Color.black;
    public Color defaultIconColor = new Color(0.7f, 0.7f, 0.7f);
    public float activeSuitScale = 1.15f;
    public float suitAnimSpeed = 15f;
    private Coroutine[] suitAnimCoroutines = new Coroutine[4];

    [Header("Русские буквы")]
    public GameObject russianLettersButtonObj;
    public TMP_Text russianLettersText;

    private Suit currentSuit = Suit.Spades;
    public Suit CurrentSuit => currentSuit;

    private Coroutine flipCoroutine;
    private float baseCardScale = 0.3f;

    // Переменные для отслеживания ориентации/разрешения
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

    private void Awake()
    {
        if (faceUpCards.Length > 0 && faceUpCards[0] != null)
        {
            baseCardScale = faceUpCards[0].transform.localScale.x;
        }
        else
        {
            baseCardScale = 0.35f;
        }
    }

    private void Start()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
    }

    private void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            SyncStateFromPrefs();
        }
    }

    private void OnEnable()
    {
        CheckRussianLanguageVisibility();
        LocalizationManager.OnLocalizationLoaded += CheckRussianLanguageVisibility;
        OnSuitChangedGlobal += HandleSuitChanged;
        OnRussianLettersChangedGlobal += HandleRussianLettersChanged;

        SyncStateFromPrefs();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLocalizationLoaded -= CheckRussianLanguageVisibility;
        OnSuitChangedGlobal -= HandleSuitChanged;
        OnRussianLettersChangedGlobal -= HandleRussianLettersChanged;
    }

    public void SyncStateFromPrefs()
    {
        currentSuit = (Suit)PlayerPrefs.GetInt("SelectedSuit", 0);
        SyncSymbolModeWithLanguage();

        CardData activeCard = faceUpCards[0];
        if (activeCard != null && flipCoroutine != null)
        {
            activeCard.StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        SetupInitialCards();
        UpdateSuitButtons((int)currentSuit, true);
    }

    private void SetupInitialCards()
    {
        CardSpriteDatabase db = ActiveSpriteDb;
        if (db == null) return;
        db.BuildCache();

        for (int i = 0; i < faceUpCards.Length; i++)
        {
            if (faceUpCards[i] == null) continue;
            int rank = i + 1;
            CardModel model = new CardModel(currentSuit, rank);
            Sprite face = db.GetSprite(model.suit, model.rank);

            faceUpCards[i].SetModel(model, face);
            faceUpCards[i].SetFaceUp(true, false);

            faceUpCards[i].transform.localScale = new Vector3(baseCardScale, baseCardScale, 1f);

            if (faceUpCards[i].transform.localEulerAngles.z == 0)
            {
                float randomRotationZ = Random.Range(-maxRandomRotation, maxRandomRotation);
                faceUpCards[i].transform.localRotation = Quaternion.Euler(0, 0, randomRotationZ);
            }

            SetupHoverEvents(faceUpCards[i]);
        }
    }

    private void CheckRussianLanguageVisibility()
    {
        if (russianLettersButtonObj == null) return;
        string currentLang = "en";
        if (LocalizationManager.instance != null) currentLang = LocalizationManager.instance.CurrentLanguage;
        else currentLang = PlayerPrefs.GetString("SelectedLanguage", "en");

        bool isRu = (currentLang == "ru" || currentLang == "russian");

        if (isRu)
        {
            russianLettersButtonObj.SetActive(true);
            UpdateRussianButtonText();
        }
        else
        {
            russianLettersButtonObj.SetActive(false);
            if (PlayerPrefs.GetInt("UseRussianSymbols", 0) == 1)
            {
                PlayerPrefs.SetInt("UseRussianSymbols", 0);
                PlayerPrefs.Save();
                SyncSymbolModeWithLanguage();
                SetupInitialCards();
            }
        }
    }

    private void SyncSymbolModeWithLanguage()
    {
        bool isRusSymbols = PlayerPrefs.GetInt("UseRussianSymbols", 0) == 1;
        if (baseSpriteDb != null) baseSpriteDb.SetSymbolMode(isRusSymbols);
        if (premiumSpriteDb != null) premiumSpriteDb.SetSymbolMode(isRusSymbols);
        if (thirdSpriteDb != null) thirdSpriteDb.SetSymbolMode(isRusSymbols);
    }

    private void UpdateRussianButtonText()
    {
        if (russianLettersText == null) return;
        bool isOn = PlayerPrefs.GetInt("UseRussianSymbols", 0) == 1;
        russianLettersText.text = isOn ? "Русские буквы: вкл" : "Русские буквы: выкл";
    }

    public void ToggleRussianLetters()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Flip");

        bool isCurrentlyRus = PlayerPrefs.GetInt("UseRussianSymbols", 0) == 1;
        bool newRusState = !isCurrentlyRus;

        PlayerPrefs.SetInt("UseRussianSymbols", newRusState ? 1 : 0);
        PlayerPrefs.Save();

        OnRussianLettersChangedGlobal?.Invoke(newRusState);
        ApplyRussianLettersChange();
    }

    private void HandleRussianLettersChanged(bool isRus)
    {
        ApplyRussianLettersChange();
    }

    private void ApplyRussianLettersChange()
    {
        SyncSymbolModeWithLanguage();
        UpdateRussianButtonText();

        CardData activeCard = faceUpCards[0];
        if (activeCard != null && flipCoroutine != null)
        {
            activeCard.StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        if (gameObject.activeInHierarchy && activeCard != null)
        {
            int[] cardsToFlip = new int[] { 0, 10, 11 };
            flipCoroutine = activeCard.StartCoroutine(FlipSpecificCardsRoutine(cardsToFlip));
        }
        else
        {
            SetupInitialCards();
        }
    }

    private IEnumerator FlipSpecificCardsRoutine(int[] indicesToFlip)
    {
        float elapsed = 0f;
        while (elapsed < flipSpeed)
        {
            elapsed += Time.deltaTime;
            float scaleX = Mathf.Lerp(baseCardScale, 0f, elapsed / flipSpeed);
            foreach (int i in indicesToFlip)
                if (i < faceUpCards.Length && faceUpCards[i] != null)
                    faceUpCards[i].transform.localScale = new Vector3(scaleX, baseCardScale, 1f);
            yield return null;
        }

        CardSpriteDatabase db = ActiveSpriteDb;
        foreach (int i in indicesToFlip)
        {
            if (i < faceUpCards.Length && faceUpCards[i] != null)
            {
                int rank = i + 1;
                CardModel newModel = new CardModel(currentSuit, rank);
                Sprite newFace = db.GetSprite(newModel.suit, newModel.rank);
                faceUpCards[i].SetModel(newModel, newFace);
                if (faceUpCards[i].image != null) faceUpCards[i].image.sprite = newFace;
            }
        }

        elapsed = 0f;
        while (elapsed < flipSpeed)
        {
            elapsed += Time.deltaTime;
            float scaleX = Mathf.Lerp(0f, baseCardScale, elapsed / flipSpeed);
            foreach (int i in indicesToFlip)
                if (i < faceUpCards.Length && faceUpCards[i] != null)
                    faceUpCards[i].transform.localScale = new Vector3(scaleX, baseCardScale, 1f);
            yield return null;
        }

        foreach (int i in indicesToFlip)
            if (i < faceUpCards.Length && faceUpCards[i] != null)
                faceUpCards[i].transform.localScale = new Vector3(baseCardScale, baseCardScale, 1f);
        flipCoroutine = null;
    }

    public void SetSuit(int suitIndex)
    {
        if (currentSuit == (Suit)suitIndex) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Flip");

        // СОХРАНЯЕМ ВЫБОР МАСТИ В ПАМЯТЬ
        PlayerPrefs.SetInt("SelectedSuit", suitIndex);
        PlayerPrefs.Save();

        OnSuitChangedGlobal?.Invoke(suitIndex);
        ApplySuitChange(suitIndex);
    }

    private void HandleSuitChanged(int suitIndex)
    {
        if (currentSuit == (Suit)suitIndex) return;
        ApplySuitChange(suitIndex);
    }

    private void ApplySuitChange(int suitIndex)
    {
        currentSuit = (Suit)suitIndex;
        UpdateSuitButtons(suitIndex, !gameObject.activeInHierarchy);

        if (deckSelectionUI != null) deckSelectionUI.UpdatePreviewCards(!gameObject.activeInHierarchy);

        CardData activeCard = faceUpCards[0];
        if (activeCard != null && flipCoroutine != null)
        {
            activeCard.StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        if (gameObject.activeInHierarchy && activeCard != null)
        {
            flipCoroutine = activeCard.StartCoroutine(FlipToNewSuitRoutine());
        }
        else
        {
            SetupInitialCards();
        }
    }

    public void ChangeDeckStyle(bool instant = false)
    {
        SyncSymbolModeWithLanguage();

        CardData activeCard = faceUpCards[0];
        if (activeCard == null) return;

        if (flipCoroutine != null)
        {
            activeCard.StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        if (instant)
        {
            SetupInitialCards();
        }
        else
        {
            // Здесь мы используем саму карту, так как она всегда видна на столе!
            flipCoroutine = activeCard.StartCoroutine(FlipToNewSuitRoutine());
        }
    }

    private void UpdateSuitButtons(int activeIndex, bool instant)
    {
        for (int i = 0; i < suitButtons.Length; i++)
        {
            if (suitButtons[i] == null) continue;

            bool isActive = (i == activeIndex);
            float targetScaleF = isActive ? activeSuitScale : 1f;
            Color targetBtnColor = isActive ? activeBtnColor : defaultBtnColor;
            Color targetIconColor = isActive ? activeIconColor : defaultIconColor;

            suitButtons[i].transition = Selectable.Transition.None;

            Image btnImage = suitButtons[i].GetComponent<Image>();
            Image iconImage = (suitIcons.Length > i) ? suitIcons[i] : null;

            if (instant)
            {
                suitButtons[i].transform.localScale = new Vector3(targetScaleF, targetScaleF, 1f);
                if (btnImage != null) btnImage.color = targetBtnColor;
                if (iconImage != null) iconImage.color = targetIconColor;
            }
            else
            {
                if (suitAnimCoroutines[i] != null) StopCoroutine(suitAnimCoroutines[i]);
                suitAnimCoroutines[i] = StartCoroutine(AnimateSuitButton(suitButtons[i].transform, btnImage, iconImage, targetScaleF, targetBtnColor, targetIconColor));
            }
        }
    }

    private IEnumerator AnimateSuitButton(Transform target, Image btnImage, Image iconImage, float targetScaleF, Color targetBtnColor, Color targetIconColor)
    {
        Vector3 targetScale = new Vector3(targetScaleF, targetScaleF, 1f);

        while (Vector3.Distance(target.localScale, targetScale) > 0.001f)
        {
            float step = Time.unscaledDeltaTime * suitAnimSpeed;
            target.localScale = Vector3.Lerp(target.localScale, targetScale, step);

            if (btnImage != null) btnImage.color = Color.Lerp(btnImage.color, targetBtnColor, step);
            if (iconImage != null) iconImage.color = Color.Lerp(iconImage.color, targetIconColor, step);

            yield return null;
        }

        target.localScale = targetScale;
        if (btnImage != null) btnImage.color = targetBtnColor;
        if (iconImage != null) iconImage.color = targetIconColor;
    }

    private IEnumerator FlipToNewSuitRoutine()
    {
        float elapsed = 0f;
        while (elapsed < flipSpeed)
        {
            elapsed += Time.deltaTime;
            float scaleX = Mathf.Lerp(baseCardScale, 0f, elapsed / flipSpeed);
            foreach (var card in faceUpCards)
                if (card != null) card.transform.localScale = new Vector3(scaleX, baseCardScale, 1f);
            yield return null;
        }

        CardSpriteDatabase db = ActiveSpriteDb;
        for (int i = 0; i < faceUpCards.Length; i++)
        {
            if (faceUpCards[i] == null) continue;
            int rank = i + 1;
            CardModel newModel = new CardModel(currentSuit, rank);
            Sprite newFace = db.GetSprite(newModel.suit, newModel.rank);
            faceUpCards[i].SetModel(newModel, newFace);
            if (faceUpCards[i].image != null) faceUpCards[i].image.sprite = newFace;
        }

        elapsed = 0f;
        while (elapsed < flipSpeed)
        {
            elapsed += Time.deltaTime;
            float scaleX = Mathf.Lerp(0f, baseCardScale, elapsed / flipSpeed);
            foreach (var card in faceUpCards)
                if (card != null) card.transform.localScale = new Vector3(scaleX, baseCardScale, 1f);
            yield return null;
        }

        foreach (var card in faceUpCards)
            if (card != null) card.transform.localScale = new Vector3(baseCardScale, baseCardScale, 1f);
        flipCoroutine = null;
    }

    private void SetupHoverEvents(CardData cardData)
    {
        GameObject cardObj = cardData.gameObject;
        Button btn = cardObj.GetComponent<Button>();
        if (btn == null)
        {
            btn = cardObj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
        }

        EventTrigger trigger = cardObj.GetComponent<EventTrigger>() ?? cardObj.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) => StartHover(cardData, true));
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener((_) => StartHover(cardData, false));
        trigger.triggers.Add(exit);
    }

    private void StartHover(CardData cardData, bool isEnter)
    {
        if (flipCoroutine != null) return;
        Transform target = cardData.transform;
        if (hoverCoroutines.ContainsKey(target) && hoverCoroutines[target] != null)
            cardData.StopCoroutine(hoverCoroutines[target]);
        float targetScaleF = isEnter ? baseCardScale * hoverScaleMultiplier : baseCardScale;
        hoverCoroutines[target] = cardData.StartCoroutine(HoverRoutine(target, targetScaleF));
    }

    private IEnumerator HoverRoutine(Transform target, float targetScaleF)
    {
        Vector3 targetScale = new Vector3(targetScaleF, targetScaleF, 1f);
        while (Vector3.Distance(target.localScale, targetScale) > 0.001f)
        {
            if (flipCoroutine != null) yield break;
            target.localScale = Vector3.Lerp(target.localScale, targetScale, Time.unscaledDeltaTime * hoverSpeed);
            yield return null;
        }
        target.localScale = targetScale;
    }

    public void OnAcceptClicked()
    {
        if (menuController != null) menuController.CloseCardAppearance();
    }
}