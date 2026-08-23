using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class RewardDistributionPanelUI : MonoBehaviour
{
    [Header("Animation Settings")]
    public float animationDuration = 0.25f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("UI Elements")]
    public TMP_Text descriptionText;
    public TMP_Text remainingBoostersText;
    public Button acceptButton;
    public Button closeButton;

    [Header("10 Game Cards")]
    public List<RewardCardUI> cards;

    private CanvasGroup canvasGroup;
    private string currentDateKey;
    private DailyQuestsPanelUI parentPanel;
    public int AvailableBank { get; private set; }
    public int GamesPerBooster { get; private set; }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        transform.localScale = Vector3.one * 0.8f;
    }

    public void OpenPanel(string dateKey, DailyQuestsPanelUI parent)
    {
        currentDateKey = dateKey;
        parentPanel = parent;

        AvailableBank = QuestManager.Instance.saveData.availableXpTickets;
        GamesPerBooster = QuestManager.Instance.HasPremium ? 6 : 3;

        string descKey = "Reward_Desc";
        string locDesc = LocalizationManager.instance?.GetLocalizedValue(descKey) ??
            "Распределите награду! Каждый вложенный бустер дает <color=#ffbb00>x2 опыта</color> на <color=#ffbb00>следующие {0} игр</color>.";
        descriptionText.text = string.Format(locDesc, GamesPerBooster);

        foreach (var card in cards) card.Init(this);

        UpdateAllCardsUI();

        if (!gameObject.activeSelf)
        {
            // ---> ЗВУК ОТКРЫТИЯ ПАНЕЛИ <---
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Panel_Slide_In");

            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(ShowPanelRoutine());
        }
    }

    private IEnumerator ShowPanelRoutine()
    {
        float elapsed = 0;

        canvasGroup.alpha = 0;
        transform.localScale = Vector3.one * 0.8f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = easeCurve.Evaluate(elapsed / animationDuration);

            canvasGroup.alpha = Mathf.Lerp(0, 1, t);
            transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, t);
            yield return null;
        }

        canvasGroup.alpha = 1;
        transform.localScale = Vector3.one;
    }

    public void OnCloseClicked()
    {
        if (!gameObject.activeInHierarchy) return;

        // ---> ЗВУК КЛИКА <---
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        StopAllCoroutines();
        StartCoroutine(HidePanelRoutine());
    }

    public void OnAcceptClicked()
    {
        if (!gameObject.activeInHierarchy) return;

        // ---> ЗВУК КЛИКА <---
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

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
        // ---> ЗВУК ЗАКРЫТИЯ ПАНЕЛИ <---
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Panel_Slide_Out");

        float elapsed = 0;
        float startAlpha = canvasGroup.alpha;
        Vector3 startScale = transform.localScale;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = easeCurve.Evaluate(elapsed / animationDuration);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.8f, t);
            yield return null;
        }

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