using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class RewardDistributionPanelUI : MonoBehaviour
{
    [Header("Animation Settings")]
    public float animationDuration = 0.3f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("UI Elements")]
    public TMP_Text descriptionText;
    public TMP_Text remainingBoostersText;
    public Button acceptButton;
    public Button closeButton; // Ссылка на крестик

    [Header("10 Game Cards")]
    public List<RewardCardUI> cards;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 hiddenPosition;
    private Vector2 visiblePosition = Vector2.zero;

    private string currentDateKey;
    private DailyQuestsPanelUI parentPanel;
    public int AvailableBank { get; private set; }
    public int GamesPerBooster { get; private set; }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Устанавливаем стартовую позицию далеко слева
        float panelWidth = rectTransform.rect.width;
        hiddenPosition = new Vector2(-panelWidth - 500f, 0);
        rectTransform.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0;

        // Отключаем при запуске сцены
        gameObject.SetActive(false);

        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
    }

    public void OpenPanel(string dateKey, DailyQuestsPanelUI parent)
    {
        currentDateKey = dateKey;
        parentPanel = parent;

        AvailableBank = QuestManager.Instance.saveData.availableXpTickets;
        GamesPerBooster = QuestManager.Instance.HasPremium ? 6 : 3;

        // Локализация описания
        string descKey = "Reward_Desc";
        string locDesc = LocalizationManager.instance?.GetLocalizedValue(descKey) ??
            "Распределите награду! Каждый вложенный бустер дает <color=#ffbb00>x2 опыта</color> на <color=#ffbb00>следующие {0} игр</color>.";
        descriptionText.text = string.Format(locDesc, GamesPerBooster);

        foreach (var card in cards) card.Init(this);

        UpdateAllCardsUI();

        // ---> ФИКС 1: Анимация запускается ТОЛЬКО если панель была закрыта <---
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(ShowPanelRoutine());
        }
        else
        {
            // Если панель уже на экране, просто гарантируем её правильное положение и альфу
            rectTransform.anchoredPosition = visiblePosition;
            canvasGroup.alpha = 1f;
        }
    }

    private IEnumerator ShowPanelRoutine()
    {
        float elapsed = 0;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = easeCurve.Evaluate(elapsed / animationDuration);

            rectTransform.anchoredPosition = Vector2.Lerp(hiddenPosition, visiblePosition, t);
            canvasGroup.alpha = Mathf.Lerp(0, 1, t);
            yield return null;
        }

        rectTransform.anchoredPosition = visiblePosition;
        canvasGroup.alpha = 1;
    }

    public void OnCloseClicked()
    {
        // ---> ФИКС 2: Защита от вызова Coroutine на выключенном объекте <---
        if (!gameObject.activeInHierarchy) return;

        StopAllCoroutines();
        StartCoroutine(HidePanelRoutine());
    }

    public void OnAcceptClicked()
    {
        if (!gameObject.activeInHierarchy) return;

        Dictionary<QuestCategory, int> distribution = new Dictionary<QuestCategory, int>();
        foreach (var card in cards)
        {
            if (card.PendingBoosters > 0)
                distribution.Add(card.category, card.PendingBoosters);
        }

        QuestManager.Instance.ApplyDistributedRewards(currentDateKey, distribution);

        StopAllCoroutines();
        StartCoroutine(HidePanelRoutine());

        if (parentPanel != null) parentPanel.RefreshUI();
    }

    private IEnumerator HidePanelRoutine()
    {
        float elapsed = 0;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = easeCurve.Evaluate(elapsed / animationDuration);

            rectTransform.anchoredPosition = Vector2.Lerp(visiblePosition, hiddenPosition, t);
            canvasGroup.alpha = Mathf.Lerp(1, 0, t);
            yield return null;
        }

        rectTransform.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0;
        gameObject.SetActive(false);
    }

    // --- ЛОГИКА БАНКА ---
    public bool TryAddBooster()
    {
        if (AvailableBank > 0) { AvailableBank--; UpdateAllCardsUI(); return true; }
        return false;
    }

    public void RemoveBooster() { AvailableBank++; UpdateAllCardsUI(); }

    public void UpdateAllCardsUI()
    {
        string availKey = "Reward_Available";
        string locAvail = LocalizationManager.instance?.GetLocalizedValue(availKey) ?? "Доступно: {0}";
        remainingBoostersText.text = string.Format(locAvail, AvailableBank);

        int totalPending = 0;
        foreach (var card in cards) totalPending += card.PendingBoosters;

        acceptButton.interactable = (totalPending > 0);
        foreach (var card in cards) card.RefreshUI(AvailableBank > 0);
    }
}