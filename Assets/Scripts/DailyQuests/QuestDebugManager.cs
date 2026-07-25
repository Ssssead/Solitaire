using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class QuestDebugManager : MonoBehaviour
{
    [Header("State")]
    public bool isDebugEnabled = false;

    [Header("UI Toggle")]
    public Button toggleButton;
    public TMP_Text toggleText;

    [Tooltip("Кнопка для полного сброса заданий за сегодняшний день")]
    public Button resetTodayButton;

    [Tooltip("Кнопка для стирания истории игр (чтобы задания не выполнялись задним числом)")]
    public Button clearStatsButton; // <--- НОВАЯ КНОПКА

    [Header("Debug Panel")]
    public GameObject debugPanel;
    public Transform easyContainer;
    public Transform mediumContainer;
    public Transform hardContainer;

    public GameObject questButtonPrefab;

    [Header("Data")]
    public List<DailyQuestData> allQuestTemplates;

    private QuestUIItem selectedSlot;

    private void Start()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(ToggleDebug);
        if (resetTodayButton != null) resetTodayButton.onClick.AddListener(ResetTodayQuests);

        

        UpdateToggleUI();
        if (debugPanel != null) debugPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isDebugEnabled) return;

        if (Input.GetMouseButtonDown(1))
        {
            PointerEventData ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);

            foreach (var hit in results)
            {
                QuestUIItem item = hit.gameObject.GetComponentInParent<QuestUIItem>();
                if (item != null)
                {
                    OpenPanelForSlot(item);
                    break;
                }
            }
        }
    }

    private void ToggleDebug()
    {
        isDebugEnabled = !isDebugEnabled;
        UpdateToggleUI();
        if (!isDebugEnabled && debugPanel != null) debugPanel.SetActive(false);
    }

    private void UpdateToggleUI()
    {
        if (toggleText != null)
            toggleText.text = isDebugEnabled ? "Отладка включена" : "Отладка выключена";

        if (resetTodayButton != null) resetTodayButton.gameObject.SetActive(isDebugEnabled);
        if (clearStatsButton != null) clearStatsButton.gameObject.SetActive(isDebugEnabled);
    }

    // ==========================================
    // НОВЫЙ МЕТОД: ОЧИСТКА ИСТОРИИ ИГР
    // ==========================================
   

    private void ResetTodayQuests()
    {
        if (QuestManager.Instance == null) return;

        string todayKey = QuestManager.Instance.GetMoscowTime().Date.ToString("yyyy-MM-dd");
        var archive = QuestManager.Instance.saveData.archive;

        var record = archive.FirstOrDefault(r => r.dateKey == todayKey);
        if (record != null)
        {
            archive.Remove(record);
        }

        QuestManager.Instance.InitializeForToday();
        QuestManager.Instance.SaveData();

        var panelUI = FindObjectOfType<DailyQuestsPanelUI>();
        if (panelUI != null)
        {
            panelUI.gameObject.SetActive(false);
            panelUI.gameObject.SetActive(true);
        }

        if (debugPanel != null) debugPanel.SetActive(false);
        Debug.Log("[QuestDebug] Задания на сегодня успешно сброшены!");
    }

    private void OpenPanelForSlot(QuestUIItem slot)
    {
        selectedSlot = slot;
        debugPanel.SetActive(true);
        ClearContainers();

        if (easyContainer != null) easyContainer.gameObject.SetActive(slot.fixedDifficulty == QuestDifficulty.Easy);
        if (mediumContainer != null) mediumContainer.gameObject.SetActive(slot.fixedDifficulty == QuestDifficulty.Medium);
        if (hardContainer != null) hardContainer.gameObject.SetActive(slot.fixedDifficulty == QuestDifficulty.Hard);
    }

    public void SelectCategory(string categoryName)
    {
        ClearContainers();
        if (string.IsNullOrEmpty(categoryName) || selectedSlot == null) return;

        QuestCategory category = (QuestCategory)Enum.Parse(typeof(QuestCategory), categoryName);
        var filtered = allQuestTemplates.Where(q => q.category == category).ToList();

        foreach (var template in filtered)
        {
            if (template.difficulty != selectedSlot.fixedDifficulty)
                continue;

            Transform targetContainer = null;
            switch (template.difficulty)
            {
                case QuestDifficulty.Easy: targetContainer = easyContainer; break;
                case QuestDifficulty.Medium: targetContainer = mediumContainer; break;
                case QuestDifficulty.Hard: targetContainer = hardContainer; break;
            }

            if (targetContainer != null)
            {
                GameObject btnObj = Instantiate(questButtonPrefab, targetContainer);

                string titleKey = template.questId + "_Title";
                string localizedTitle = LocalizationManager.instance?.GetLocalizedValue(titleKey) ?? "";

                if (string.IsNullOrEmpty(localizedTitle) || localizedTitle == titleKey || localizedTitle == "Localized text not found")
                {
                    localizedTitle = string.IsNullOrEmpty(template.questName) ? template.name : template.questName;
                }

                btnObj.GetComponentInChildren<TMP_Text>().text = localizedTitle.ToUpper();

                var capturedTemplate = template;
                btnObj.GetComponent<Button>().onClick.AddListener(() => AssignQuestToSlot(capturedTemplate));
            }
        }
    }

    private void ClearContainers()
    {
        if (easyContainer != null) foreach (Transform child in easyContainer) Destroy(child.gameObject);
        if (mediumContainer != null) foreach (Transform child in mediumContainer) Destroy(child.gameObject);
        if (hardContainer != null) foreach (Transform child in hardContainer) Destroy(child.gameObject);
    }

    private void AssignQuestToSlot(DailyQuestData template)
    {
        if (selectedSlot == null || selectedSlot.ActiveQuest == null) return;

        QuestInstance oldQuest = selectedSlot.ActiveQuest;
        string todayKey = QuestManager.Instance.GetMoscowTime().Date.ToString("yyyy-MM-dd");
        DailyQuestRecord record = QuestManager.Instance.saveData.archive.FirstOrDefault(r => r.dateKey == todayKey);

        if (record != null)
        {
            int index = record.quests.IndexOf(oldQuest);
            if (index >= 0)
            {
                QuestInstance newQuest = new QuestInstance(template);
                record.quests[index] = newQuest;
                QuestManager.Instance.SaveData();

                var panelUI = FindObjectOfType<DailyQuestsPanelUI>();
                if (panelUI != null)
                {
                    panelUI.gameObject.SetActive(false);
                    panelUI.gameObject.SetActive(true);
                }
            }
        }
        debugPanel.SetActive(false);
    }
}