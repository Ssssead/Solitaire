using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuestInstance
{
    public string questId;
    public int targetValue;
    public int currentProgress;
    public bool isCompleted;
    public int conditionValue;

    public List<string> trackedEvents = new List<string>();

    [NonSerialized] public DailyQuestData template;

    // ---> днаюбкемн дкъ WEBGL (IL2CPP) <---
    public QuestInstance() { }

    public QuestInstance(DailyQuestData data)
    {
        template = data;
        questId = data.questId;
        currentProgress = 0;
        isCompleted = false;
        trackedEvents = new List<string>();

        bool isThresholdQuest =
            data.actionType == QuestActionType.PlayTimeUnder ||
            data.actionType == QuestActionType.WinWithMoreThanXMoves ||
            data.actionType == QuestActionType.WinWithMaxUndos ||
            data.actionType == QuestActionType.WinWithMaxFreeCells ||
            data.actionType == QuestActionType.WinWithRemainingStock ||
            data.actionType == QuestActionType.WinWithRemainingShuffles ||
            data.actionType == QuestActionType.WinWithMaxStockDraws;

        if (isThresholdQuest)
        {
            if (data.actionType == QuestActionType.PlayTimeUnder)
            {
                int minSteps = Mathf.CeilToInt(data.minTargetValue / 5f);
                int maxSteps = Mathf.FloorToInt(data.maxTargetValue / 5f);

                if (minSteps > maxSteps)
                    conditionValue = data.minTargetValue;
                else
                    conditionValue = UnityEngine.Random.Range(minSteps, maxSteps + 1) * 5;
            }
            else
            {
                conditionValue = UnityEngine.Random.Range(data.minTargetValue, data.maxTargetValue + 1);
            }

            targetValue = 1;
        }
        else
        {
            targetValue = UnityEngine.Random.Range(data.minTargetValue, data.maxTargetValue + 1);
            conditionValue = 0;
        }
    }

    public string GetDescriptionForUI()
    {
        string currentLang = LocalizationManager.instance.CurrentLanguage;
        string localizedTemplate = "";

        int formatValue = (conditionValue > 0 && targetValue == 1) ? conditionValue : targetValue;

        if (template.actionType == QuestActionType.SpecificRanksInFoundation)
        {
            formatValue = 1;
        }

        if (currentLang == "ru")
        {
            string suffix = GetRussianPluralSuffix(formatValue);
            string fullKey = template.questId + "_Desc_" + suffix;
            localizedTemplate = LocalizationManager.instance.GetLocalizedValue(fullKey);
        }
        else
        {
            string fullKey = template.questId + "_Desc";
            localizedTemplate = LocalizationManager.instance.GetLocalizedValue(fullKey);
        }

        return string.Format(localizedTemplate, formatValue);
    }

    private string GetRussianPluralSuffix(int number)
    {
        int n = Mathf.Abs(number) % 100;
        int n10 = n % 10;
        if (n >= 11 && n <= 19) return "5";
        if (n10 == 1) return "1";
        if (n10 >= 2 && n10 <= 4) return "2";
        return "5";
    }

    public void AddProgress(int amount, string eventId = "")
    {
        if (isCompleted) return;

        if (template.actionType == QuestActionType.PlayDifferentGames)
        {
            if (trackedEvents.Contains(eventId)) return;
            trackedEvents.Add(eventId);
        }

        currentProgress += amount;

        if (currentProgress < 0) currentProgress = 0;

        if (currentProgress >= targetValue)
        {
            currentProgress = targetValue;
            isCompleted = true;
        }
    }
}