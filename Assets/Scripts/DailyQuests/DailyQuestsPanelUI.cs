using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DailyQuestsPanelUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Перетащите сюда 6 плашек заданий из иерархии")]
    public QuestUIItem[] questItems = new QuestUIItem[6];

    [Header("Top Bar UI")]
    public TMP_Text dateText;
    public TMP_Text timerText;
    public Button prevDayButton;
    public Button nextDayButton;
    public GameObject premiumLockIcon;

    [Header("Statistics UI")]
    [Tooltip("Текст для отображения серии выполненных заданий")]
    public TMP_Text questsInARowText;
    [Tooltip("Текст для отображения общего количества идеальных дней (6/6)")]
    public TMP_Text perfectDaysTotalText;

    [Header("Reward UI")]
    public Button claimRewardButton;
    public TMP_Text claimRewardText;
    public GameObject rewardsPanel;

    [System.Serializable]
    public struct GameIconMapping
    {
        public QuestCategory category;
        public Sprite icon;
    }

    [Header("Icons Dictionary")]
    public List<GameIconMapping> gameIcons;

    private DateTime todayDate;
    private DateTime viewedDate;
    private string cachedTimerFormat = "{0}h {1:D2}m {2:D2}s";

    // ---> НОВОЕ: Оптимизация сборщика мусора для таймера <---
    private int lastSecond = -1;

    private void OnEnable()
    {
        if (QuestManager.Instance == null) return;

        QuestManager.Instance.SyncCurrentQuestsWithStatistics();

        todayDate = QuestManager.Instance.GetMoscowTime().Date;

        if (!string.IsNullOrEmpty(QuestManager.Instance.SelectedDateKey) &&
            DateTime.TryParseExact(QuestManager.Instance.SelectedDateKey, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime savedDate))
        {
            if (savedDate <= todayDate && savedDate >= todayDate.AddDays(-4))
            {
                viewedDate = savedDate;
            }
            else
            {
                viewedDate = todayDate;
            }
        }
        else
        {
            viewedDate = todayDate;
        }

        UpdateTimerFormatCache();
        UpdateTopBar();
        LoadQuestsForDate(viewedDate, false);
    }

    private void UpdateTimerFormatCache()
    {
        string currentLang = "en";
        if (LocalizationManager.instance != null)
        {
            currentLang = LocalizationManager.instance.CurrentLanguage.ToLower();
        }

        switch (currentLang)
        {
            case "ru": cachedTimerFormat = "{0}ч {1:D2}м {2:D2}с"; break;
            case "tr": cachedTimerFormat = "{0}sa {1:D2}dk {2:D2}sn"; break;
            default: cachedTimerFormat = "{0}h {1:D2}m {2:D2}s"; break;
        }
    }

    private void Update()
    {
        if (QuestManager.Instance == null || timerText == null) return;
        if (!timerText.gameObject.activeInHierarchy) return;

        DateTime now = QuestManager.Instance.GetMoscowTime();
        TimeSpan timeRemaining;

        if (viewedDate.Date == todayDate)
        {
            // Для "Сегодня" показываем время до обновления квестов (до полуночи)
            DateTime nextReset = now.Date.AddDays(1);
            timeRemaining = nextReset - now;
        }
        else
        {
            // Для прошлых дней показываем время до исчезновения из архива (сгорания)
            int safeDays = QuestManager.Instance.HasPremium ? 5 : 1;
            DateTime expirationDate = viewedDate.Date.AddDays(safeDays);
            timeRemaining = expirationDate - now;
        }

        if (timeRemaining.TotalSeconds <= 0)
        {
            // Проверка, чтобы не спамить обновление текста, если оно уже 0
            if (lastSecond != 0)
            {
                timerText.text = string.Format(cachedTimerFormat, 0, 0, 0);
                lastSecond = 0;

                QuestManager.Instance.SetSelectedDate(now.Date.ToString("yyyy-MM-dd"));
                OnEnable();
            }
        }
        else
        {
            // ---> НОВОЕ: Обновляем строку только когда реально поменялась секунда <---
            if (timeRemaining.Seconds != lastSecond)
            {
                lastSecond = timeRemaining.Seconds;
                int hours = (int)timeRemaining.TotalHours;
                timerText.text = string.Format(cachedTimerFormat, hours, timeRemaining.Minutes, timeRemaining.Seconds);
            }
        }
    }

    #region Button Clicks

    public void OnPrevDayClicked()
    {
        if (!QuestManager.Instance.HasPremium) return;

        viewedDate = viewedDate.AddDays(-1);
        UpdateTopBar();
        LoadQuestsForDate(viewedDate, true);
    }

    public void OnNextDayClicked()
    {
        viewedDate = viewedDate.AddDays(1);
        UpdateTopBar();
        LoadQuestsForDate(viewedDate, true);
    }

    public void OnClaimRewardClicked()
    {
        QuestManager.Instance.ClaimDailyReward(QuestManager.Instance.SelectedDateKey);

        if (rewardsPanel != null)
        {
            var distPanel = rewardsPanel.GetComponent<RewardDistributionPanelUI>();
            if (distPanel != null)
            {
                rewardsPanel.SetActive(true);
                distPanel.OpenPanel(QuestManager.Instance.SelectedDateKey, this);
            }
        }
    }

    public void RefreshUI()
    {
        LoadQuestsForDate(viewedDate, false);
        UpdateStatsUI();
    }

    #endregion

    private void UpdateTopBar()
    {
        if (prevDayButton == null || nextDayButton == null || dateText == null) return;

        nextDayButton.interactable = (viewedDate < todayDate);

        bool hasPremium = QuestManager.Instance.HasPremium;
        int maxDaysBack = 4;

        if (premiumLockIcon != null) premiumLockIcon.SetActive(!hasPremium);

        DateTime oldestAllowedDate = todayDate.AddDays(-maxDaysBack);
        prevDayButton.interactable = hasPremium && (viewedDate > oldestAllowedDate);

        QuestManager.Instance.SetSelectedDate(viewedDate.ToString("yyyy-MM-dd"));

        string monthKey = "Month_" + viewedDate.Month;
        string localizedMonth = LocalizationManager.instance?.GetLocalizedValue(monthKey) ?? "";

        if (string.IsNullOrEmpty(localizedMonth) || localizedMonth == monthKey || localizedMonth == "Localized text not found")
        {
            localizedMonth = viewedDate.ToString("MMMM");
        }

        dateText.text = $"{viewedDate.Day} {localizedMonth.ToUpper()}";

        if (timerText != null) timerText.gameObject.SetActive(true);

        UpdateStatsUI();
    }

    private void UpdateStatsUI()
    {
        if (QuestManager.Instance == null) return;

        if (questsInARowText != null)
            questsInARowText.text = QuestManager.Instance.saveData.questsInARowCurrent.ToString();

        if (perfectDaysTotalText != null)
            perfectDaysTotalText.text = QuestManager.Instance.saveData.perfectDaysTotal.ToString();
    }

    private void LoadQuestsForDate(DateTime date, bool playAnimation)
    {
        string dateKey = date.ToString("yyyy-MM-dd");
        DailyQuestRecord record = QuestManager.Instance.saveData.archive.FirstOrDefault(r => r.dateKey == dateKey);

        if (record != null && record.quests.Count > 0)
        {
            DisplayQuests(record.quests, playAnimation);
            UpdateRewardButton(record);
        }
        else if (date == todayDate)
        {
            QuestManager.Instance.InitializeForToday();
            QuestManager.Instance.SyncCurrentQuestsWithStatistics();
            record = QuestManager.Instance.saveData.archive.FirstOrDefault(r => r.dateKey == dateKey);

            if (record != null)
            {
                DisplayQuests(record.quests, playAnimation);
                UpdateRewardButton(record);
            }
            else ClearQuestsUI();
        }
        else
        {
            ClearQuestsUI();
        }
    }

    private void UpdateRewardButton(DailyQuestRecord record)
    {
        if (claimRewardButton == null || claimRewardText == null) return;

        if (record == null)
        {
            claimRewardText.text = "0/6";
            claimRewardButton.interactable = false;
            return;
        }

        int completed = record.CompletedCount;

        if (completed < 6)
        {
            claimRewardText.text = $"{completed}/6";
            claimRewardButton.interactable = false;
        }
        else
        {
            string locText = LocalizationManager.instance?.GetLocalizedValue("Claim_Reward") ?? "ЗАБРАТЬ НАГРАДУ";
            if (locText == "Claim_Reward" || locText == "Localized text not found" || string.IsNullOrEmpty(locText))
                locText = "ЗАБРАТЬ НАГРАДУ";

            claimRewardText.text = locText.ToUpper();
            claimRewardButton.interactable = true;
        }
    }

    private void ClearQuestsUI()
    {
        StopAllCoroutines();
        foreach (var item in questItems)
        {
            if (item != null && item.gameObject.activeSelf)
                StartCoroutine(HideItemRoutine(item));
        }
        UpdateRewardButton(null);
    }

    private void DisplayQuests(List<QuestInstance> questsToDisplay, bool playAnimation)
    {
        foreach (var q in questsToDisplay)
        {
            if (q.template == null)
            {
                q.template = QuestManager.Instance.allQuestTemplates.FirstOrDefault(t => t.questId == q.questId);
            }
        }

        var easyQuests = questsToDisplay.Where(q => q.template != null && q.template.difficulty == QuestDifficulty.Easy)
                                        .OrderBy(q => GetCategoryPriority(q.template.category)).ToList();

        var mediumQuests = questsToDisplay.Where(q => q.template != null && q.template.difficulty == QuestDifficulty.Medium)
                                          .OrderBy(q => GetCategoryPriority(q.template.category)).ToList();

        var hardQuests = questsToDisplay.Where(q => q.template != null && q.template.difficulty == QuestDifficulty.Hard)
                                        .OrderBy(q => GetCategoryPriority(q.template.category)).ToList();

        int eIdx = 0, mIdx = 0, hIdx = 0;

        StopAllCoroutines();

        for (int i = 0; i < questItems.Length; i++)
        {
            var item = questItems[i];
            if (item == null) continue;

            QuestInstance questToAssign = null;

            if (item.fixedDifficulty == QuestDifficulty.Easy && eIdx < easyQuests.Count)
                questToAssign = easyQuests[eIdx++];
            else if (item.fixedDifficulty == QuestDifficulty.Medium && mIdx < mediumQuests.Count)
                questToAssign = mediumQuests[mIdx++];
            else if (item.fixedDifficulty == QuestDifficulty.Hard && hIdx < hardQuests.Count)
                questToAssign = hardQuests[hIdx++];

            if (questToAssign != null)
            {
                Sprite icon = GetIconForCategory(questToAssign.template.category);

                if (playAnimation)
                {
                    StartCoroutine(FlipItemRoutine(item, questToAssign, icon, i * 0.05f));
                }
                else
                {
                    item.Setup(questToAssign, icon);
                    item.gameObject.SetActive(true);
                    item.GetComponent<RectTransform>().localRotation = Quaternion.identity;
                }
            }
            else
            {
                if (playAnimation)
                    StartCoroutine(HideItemRoutine(item));
                else
                    item.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator FlipItemRoutine(QuestUIItem item, QuestInstance quest, Sprite icon, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);

        RectTransform rt = item.GetComponent<RectTransform>();
        float halfDuration = 0.12f;

        if (!item.gameObject.activeSelf)
        {
            item.Setup(quest, icon);
            rt.localRotation = Quaternion.Euler(90, 0, 0);
            item.gameObject.SetActive(true);

            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / halfDuration;
                rt.localRotation = Quaternion.Euler(Mathf.Lerp(90, 0, t), 0, 0);
                yield return null;
            }
            rt.localRotation = Quaternion.identity;
        }
        else
        {
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / halfDuration;
                rt.localRotation = Quaternion.Euler(Mathf.Lerp(0, 90, t), 0, 0);
                yield return null;
            }

            item.Setup(quest, icon);
            rt.localRotation = Quaternion.Euler(270, 0, 0);

            t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / halfDuration;
                rt.localRotation = Quaternion.Euler(Mathf.Lerp(270, 360, t), 0, 0);
                yield return null;
            }
            rt.localRotation = Quaternion.identity;
        }
    }

    private IEnumerator HideItemRoutine(QuestUIItem item)
    {
        if (!item.gameObject.activeSelf) yield break;

        RectTransform rt = item.GetComponent<RectTransform>();
        float t = 0;

        while (t < 1f)
        {
            t += Time.deltaTime / 0.12f;
            rt.localRotation = Quaternion.Euler(Mathf.Lerp(0, 90, t), 0, 0);
            yield return null;
        }

        item.gameObject.SetActive(false);
        rt.localRotation = Quaternion.identity;
    }

    private Sprite GetIconForCategory(QuestCategory cat)
    {
        foreach (var mapping in gameIcons)
        {
            if (mapping.category == cat) return mapping.icon;
        }
        return null;
    }

    private int GetCategoryPriority(QuestCategory category)
    {
        switch (category)
        {
            case QuestCategory.General: return 0;
            case QuestCategory.Klondike: return 1;
            case QuestCategory.Spider: return 2;
            case QuestCategory.FreeCell: return 3;
            case QuestCategory.Pyramid: return 4;
            case QuestCategory.TriPeaks: return 5;
            case QuestCategory.Yukon: return 6;
            case QuestCategory.MonteCarlo: return 7;
            case QuestCategory.Sultan: return 8;
            case QuestCategory.Octagon: return 9;
            case QuestCategory.Montana: return 10;
            default: return 99;
        }
    }
}