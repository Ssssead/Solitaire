using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DeckSelectionUI : MonoBehaviour
{
    [Header("Связи с панелями")]
    public CardAppearanceUI mainAppearanceUI;
    public CardBackSelectionUI backSelectionUI;
    public MenuController menuController;

    [Header("Кнопки колод (3 штуки)")]
    public RectTransform[] deckButtons = new RectTransform[3];

    [Header("Настройки анимации")]
    public float activeScale = 0.3f;
    public float inactiveScale = 0.25f;
    public float animSpeed = 15f;

    [Header("Премиум блокировка")]
    public GameObject premiumLockPanel;

    private int activeIndex;
    private Coroutine[] scaleCoroutines;
    private int lastWidth;
    private int lastHeight;

    private void OnEnable()
    {
        CheckPremiumStatus();
        UpdatePreviewCards(true); // При открытии панели обновляем мгновенно
    }
    private void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            AdjustScaleForResolution(); // Пересчитываем activeScale и inactiveScale
            ApplyDynamicScale();        // Применяем к кнопкам-королям
        }
    }

    private void ApplyDynamicScale()
    {
        for (int i = 0; i < deckButtons.Length; i++)
        {
            if (deckButtons[i] == null) continue;

            // Изменяем масштаб только если кнопка сейчас не анимируется кликом
            if (scaleCoroutines == null || i >= scaleCoroutines.Length || scaleCoroutines[i] == null)
            {
                float currentScale = (i == activeIndex) ? activeScale : inactiveScale;
                deckButtons[i].localScale = new Vector3(currentScale, currentScale, 1f);
            }
        }
    }
    private void Start()
    {
        

        activeIndex = PlayerPrefs.GetInt("SelectedDeckStyle", 0);
        scaleCoroutines = new Coroutine[deckButtons.Length];

        for (int i = 0; i < deckButtons.Length; i++)
        {
            if (deckButtons[i] == null) continue;
            Button btn = deckButtons[i].GetComponent<Button>();
            if (btn == null) btn = deckButtons[i].gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;

            int index = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnDeckClicked(index));

            float targetS = (i == activeIndex) ? activeScale : inactiveScale;
            deckButtons[i].localScale = new Vector3(targetS, targetS, 1f);
        }

        UpdatePreviewCards(true); // При старте сцены тоже обновляем мгновенно
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
            multiplier = Mathf.Min(rawGrow, 1.15f);
        }

        activeScale = 0.3f * multiplier;
        inactiveScale = 0.25f * multiplier;
    }
    // --- ОБНОВЛЕНИЕ КОРОЛЕЙ С АНИМАЦИЕЙ ---
    public void UpdatePreviewCards(bool instant)
    {
        if (mainAppearanceUI == null) return;

        // ЗАЩИТА: Если панель закрыта (выключена), мы не имеем права запускать на ней корутины.
        // Поэтому мы просто меняем спрайты мгновенно (игрок этого все равно не увидит).
        if (instant || !gameObject.activeInHierarchy)
        {
            ApplyPreviewSprites();
        }
        else
        {
            StartCoroutine(FlipPreviewCardsRoutine());
        }
    }

    private void ApplyPreviewSprites()
    {
        Suit currentSuit = mainAppearanceUI.CurrentSuit;
        int kingRank = 13;

        if (deckButtons.Length > 0 && deckButtons[0] != null && mainAppearanceUI.baseSpriteDb != null)
        {
            Image img = deckButtons[0].GetComponent<Image>();
            if (img != null) img.sprite = mainAppearanceUI.baseSpriteDb.GetSprite(currentSuit, kingRank);
        }

        if (deckButtons.Length > 1 && deckButtons[1] != null && mainAppearanceUI.premiumSpriteDb != null)
        {
            Image img = deckButtons[1].GetComponent<Image>();
            if (img != null) img.sprite = mainAppearanceUI.premiumSpriteDb.GetSprite(currentSuit, kingRank);
        }

        if (deckButtons.Length > 2 && deckButtons[2] != null && mainAppearanceUI.thirdSpriteDb != null)
        {
            Image img = deckButtons[2].GetComponent<Image>();
            if (img != null) img.sprite = mainAppearanceUI.thirdSpriteDb.GetSprite(currentSuit, kingRank);
        }
    }

    private IEnumerator FlipPreviewCardsRoutine()
    {
        // Берем скорость переворота из главного скрипта
        float flipHalfSpeed = mainAppearanceUI != null ? mainAppearanceUI.flipSpeed : 0.15f;

        // 1. Сжимаем (эффект переворота)
        float elapsed = 0f;
        while (elapsed < flipHalfSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flipHalfSpeed;
            for (int i = 0; i < deckButtons.Length; i++)
            {
                if (deckButtons[i] == null) continue;
                float currentScale = (i == activeIndex) ? activeScale : inactiveScale;
                deckButtons[i].localScale = new Vector3(Mathf.Lerp(currentScale, 0f, t), currentScale, 1f);
            }
            yield return null;
        }

        // 2. Меняем картинки пока они "тонкие"
        ApplyPreviewSprites();

        // 3. Разжимаем обратно
        elapsed = 0f;
        while (elapsed < flipHalfSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flipHalfSpeed;
            for (int i = 0; i < deckButtons.Length; i++)
            {
                if (deckButtons[i] == null) continue;
                float currentScale = (i == activeIndex) ? activeScale : inactiveScale;
                deckButtons[i].localScale = new Vector3(Mathf.Lerp(0f, currentScale, t), currentScale, 1f);
            }
            yield return null;
        }

        // Страховка финального размера
        for (int i = 0; i < deckButtons.Length; i++)
        {
            if (deckButtons[i] == null) continue;
            float currentScale = (i == activeIndex) ? activeScale : inactiveScale;
            deckButtons[i].localScale = new Vector3(currentScale, currentScale, 1f);
        }
    }

    // --- ОСТАЛЬНЫЕ МЕТОДЫ ---
    private void CheckPremiumStatus()
    {
        bool isPremium = false;
        if (StatisticsManager.Instance != null) isPremium = StatisticsManager.Instance.IsUserPremium;
        else isPremium = PlayerPrefs.GetInt("IsPremiumSaved", 0) == 1;

        if (premiumLockPanel != null) premiumLockPanel.SetActive(!isPremium);
    }

    private void OnDeckClicked(int index)
    {
        if (premiumLockPanel != null && premiumLockPanel.activeSelf) return;
        if (index == activeIndex) return;

        activeIndex = index;
        PlayerPrefs.SetInt("SelectedDeckStyle", activeIndex);
        PlayerPrefs.Save();

        if (mainAppearanceUI != null) mainAppearanceUI.ChangeDeckStyle();
        if (backSelectionUI != null) backSelectionUI.RefreshDeckSprites(false);

        for (int i = 0; i < deckButtons.Length; i++)
        {
            if (deckButtons[i] == null) continue;
            float targetS = (i == activeIndex) ? activeScale : inactiveScale;
            if (scaleCoroutines[i] != null) StopCoroutine(scaleCoroutines[i]);
            scaleCoroutines[i] = StartCoroutine(ScaleRoutine(deckButtons[i], targetS));
        }
    }

    private IEnumerator ScaleRoutine(RectTransform target, float targetScaleF)
    {
        Vector3 targetS = new Vector3(targetScaleF, targetScaleF, 1f);
        while (Vector3.Distance(target.localScale, targetS) > 0.001f)
        {
            target.localScale = Vector3.Lerp(target.localScale, targetS, Time.unscaledDeltaTime * animSpeed);
            yield return null;
        }
        target.localScale = targetS;
    }

    public void ClosePanel()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        if (menuController != null) menuController.CloseDeckSelection();
    }
}