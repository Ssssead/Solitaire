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

    [Header("Премиум блокировка")]
    public GameObject premiumLockPanel;

    public float animSpeed = 15f;

    public static event System.Action<int> OnDeckStyleChangedGlobal;

    private float baseActiveScale = 0.3f;
    private float baseInactiveScale = 0.25f;

    private int activeIndex;
    private Coroutine[] scaleCoroutines;

    // Переменные для отслеживания ориентации/разрешения
    private int lastWidth;
    private int lastHeight;

    private void Awake()
    {
        float maxScale = 0f;
        float minScale = 10f;
        foreach (var btn in deckButtons)
        {
            if (btn == null) continue;
            float s = btn.localScale.x;
            if (s > maxScale) maxScale = s;
            if (s < minScale) minScale = s;
        }
        if (maxScale <= minScale) { maxScale = 0.3f; minScale = 0.25f; }
        baseActiveScale = maxScale;
        baseInactiveScale = minScale;

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
        CheckPremiumStatus();
        OnDeckStyleChangedGlobal += SyncDeckStyle;
        SyncStateFromPrefs();
    }

    private void OnDisable()
    {
        OnDeckStyleChangedGlobal -= SyncDeckStyle;
    }

    public void SyncStateFromPrefs()
    {
        activeIndex = PlayerPrefs.GetInt("SelectedDeckStyle", 0);
        for (int i = 0; i < deckButtons.Length; i++)
        {
            if (deckButtons[i] == null) continue;
            float targetS = (i == activeIndex) ? baseActiveScale : baseInactiveScale;
            deckButtons[i].localScale = new Vector3(targetS, targetS, 1f);
        }
        UpdatePreviewCards(true);

        // ДОБАВЛЕНО: Жестко синхронизируем зависимые панели при включении
        if (mainAppearanceUI != null) mainAppearanceUI.ChangeDeckStyle(true);
        if (backSelectionUI != null) backSelectionUI.RefreshDeckSprites(true);
    }

    public void UpdatePreviewCards(bool instant)
    {
        if (mainAppearanceUI == null) return;

        if (instant || !gameObject.activeInHierarchy) ApplyPreviewSprites();
        else StartCoroutine(FlipPreviewCardsRoutine());
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
        float flipHalfSpeed = mainAppearanceUI != null ? mainAppearanceUI.flipSpeed : 0.15f;
        float elapsed = 0f;
        while (elapsed < flipHalfSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flipHalfSpeed;
            for (int i = 0; i < deckButtons.Length; i++)
            {
                if (deckButtons[i] == null) continue;
                float currentScale = (i == activeIndex) ? baseActiveScale : baseInactiveScale;
                deckButtons[i].localScale = new Vector3(Mathf.Lerp(currentScale, 0f, t), currentScale, 1f);
            }
            yield return null;
        }

        ApplyPreviewSprites();

        elapsed = 0f;
        while (elapsed < flipHalfSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flipHalfSpeed;
            for (int i = 0; i < deckButtons.Length; i++)
            {
                if (deckButtons[i] == null) continue;
                float currentScale = (i == activeIndex) ? baseActiveScale : baseInactiveScale;
                deckButtons[i].localScale = new Vector3(Mathf.Lerp(0f, currentScale, t), currentScale, 1f);
            }
            yield return null;
        }

        for (int i = 0; i < deckButtons.Length; i++)
        {
            if (deckButtons[i] == null) continue;
            float currentScale = (i == activeIndex) ? baseActiveScale : baseInactiveScale;
            deckButtons[i].localScale = new Vector3(currentScale, currentScale, 1f);
        }
    }

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

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Flip");

        activeIndex = index;
        PlayerPrefs.SetInt("SelectedDeckStyle", activeIndex);
        PlayerPrefs.Save();

        OnDeckStyleChangedGlobal?.Invoke(activeIndex);

        // ЖЕСТКО запускаем анимацию на картах стола, передавая false
        if (mainAppearanceUI != null) mainAppearanceUI.ChangeDeckStyle(false);
        if (backSelectionUI != null) backSelectionUI.RefreshDeckSprites(false);

        AnimateScales();
    }

    private void SyncDeckStyle(int newIndex)
    {
        if (activeIndex == newIndex) return;
        activeIndex = newIndex;

        UpdatePreviewCards(true);
        AnimateScales();

        // А вот для невидимой фоновой панели передаем true (мгновенно), чтобы она подготовилась
        if (mainAppearanceUI != null) mainAppearanceUI.ChangeDeckStyle(true);
        if (backSelectionUI != null) backSelectionUI.RefreshDeckSprites(true);
    }

    private void AnimateScales()
    {
        for (int i = 0; i < deckButtons.Length; i++)
        {
            if (deckButtons[i] == null) continue;
            float targetS = (i == activeIndex) ? baseActiveScale : baseInactiveScale;
            if (scaleCoroutines[i] != null) StopCoroutine(scaleCoroutines[i]);

            if (gameObject.activeInHierarchy) scaleCoroutines[i] = StartCoroutine(ScaleRoutine(deckButtons[i], targetS));
            else deckButtons[i].localScale = new Vector3(targetS, targetS, 1f);
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