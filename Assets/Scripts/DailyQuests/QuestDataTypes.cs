using System;
using System.Collections.Generic;

[Serializable]
public class XpBuffRecord
{
    public QuestCategory gameCategory;
    public int remainingWins;

    public XpBuffRecord() { } // <--- днаюбкемн
}

public struct QuestEventContext
{
    public QuestCategory category;
    public QuestActionType actionType;
    public int value;
    public TargetMatchDifficulty matchDifficulty;
    public string variant;
    public int undosUsed;
    public float timeSpentSeconds;
    public string eventId;

    public QuestEventContext(QuestCategory category, QuestActionType actionType, int value = 1)
    {
        this.category = category;
        this.actionType = actionType;
        this.value = value;
        this.matchDifficulty = TargetMatchDifficulty.Any;
        this.variant = "";
        this.undosUsed = 0;
        this.timeSpentSeconds = 0f;
        this.eventId = "";
    }
}

[Serializable]
public class DailyQuestRecord
{
    public string dateKey;
    public List<QuestInstance> quests = new List<QuestInstance>();
    public bool isRewardClaimed = false;

    public DailyQuestRecord() { } // <--- днаюбкемн

    public int CompletedCount
    {
        get
        {
            int count = 0;
            foreach (var q in quests)
            {
                if (q != null && q.isCompleted) count++;
            }
            return count;
        }
    }
}

[System.Serializable]
public class QuestSaveData
{
    public int minorStreakCurrent = 0;
    public int minorStreakBest = 0;
    public int questsInARowCurrent = 0;
    public int questsInARowBest = 0;

    public int perfectDaysTotal = 0;
    public int perfectDaysStreakCurrent = 0;
    public int perfectDaysStreakBest = 0;

    public string lastEvaluatedDateKey = "";
    public List<DailyQuestRecord> archive = new List<DailyQuestRecord>();

    public int availableXpTickets = 0;
    public List<XpBuffRecord> activeXpBuffs = new List<XpBuffRecord>();

    public QuestSaveData() { } // <--- днаюбкемн
}