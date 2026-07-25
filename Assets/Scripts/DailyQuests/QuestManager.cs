using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YG;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Databases")]
    public List<DailyQuestData> allQuestTemplates;

    [Header("Time Settings")]
    private long serverTimeOffsetMs = 0;
    private bool isTimeSynced = false;
    private readonly TimeSpan MOSCOW_OFFSET = TimeSpan.FromHours(3);

    public QuestSaveData saveData;

    private readonly DateTime EPOCH_DATE = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const int MAX_ARCHIVE_DAYS = 6;
    private const int DAILY_QUESTS_COUNT = 6;
    public string SelectedDateKey { get; private set; }

    public bool HasPremium => StatisticsManager.Instance != null && StatisticsManager.Instance.IsUserPremium;
    public static event Action<QuestInstance, int, int> OnQuestProgressNotification;
    private Dictionary<string, int> lastNotifiedProgress = new Dictionary<string, int>();
    private bool cloudDataReceived = false;

    // ==========================================
    // СТРУКТУРА 1-В-1 КАК В STATISTICS MANAGER
    // ==========================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }


    private void Initialize()
    {
        SelectedDateKey = GetMoscowTime().Date.ToString("yyyy-MM-dd");

        // Грузим локальные данные для мгновенного старта (офлайн/редактор/PlayerPrefs)
        LoadDataLocal();

        // Реальные облачные данные придут асинхронно через onGetSDKData.
        // Синхронный вызов LoadDataCloud() здесь УБРАН - YG2.saves пока пуст.
    }

    private void LoadDataLocal()
    {
        string json = PlayerPrefs.GetString("QuestSaveData", "");
        if (string.IsNullOrEmpty(json)) saveData = new QuestSaveData();
        else saveData = JsonUtility.FromJson<QuestSaveData>(json);

        OnDataLoaded();
    }

    public void LoadFromCloud(string cloudJson)
    {
        cloudDataReceived = true;
        if (!string.IsNullOrEmpty(cloudJson))
        {
            QuestSerializer.Deserialize(cloudJson, out saveData);
        }
        OnDataLoaded();
    }
    public string GetCloudData()
    {
        return QuestSerializer.Serialize(saveData);
    }

    private void OnDataLoaded()
    {
        SyncServerTime();
        InitializeForToday();
    }

    public void SaveData()
    {
        string json = JsonUtility.ToJson(saveData, true);
        PlayerPrefs.SetString("QuestSaveData", json);
        PlayerPrefs.Save();

        if (GlobalSaveManager.Instance != null)
        {
            GlobalSaveManager.Instance.MarkAsDirty();
        }
    }

    // ==========================================
    // ЛОГИКА ВРЕМЕНИ И ИНИЦИАЛИЗАЦИИ
    // ==========================================

    private void SyncServerTime()
    {
        long serverMs = YG2.ServerTime();
        if (serverMs > 0)
        {
            long localUnityMs = (long)(Time.unscaledTime * 1000L);
            serverTimeOffsetMs = serverMs - localUnityMs;
            isTimeSynced = true;
        }
    }

    public void SetSelectedDate(string dateKey)
    {
        string todayKey = GetMoscowTime().Date.ToString("yyyy-MM-dd");
        if (!HasPremium && dateKey != todayKey)
        {
            SelectedDateKey = todayKey;
            return;
        }
        SelectedDateKey = dateKey;
    }

    public DateTime GetMoscowTime()
    {
        if (!isTimeSynced) SyncServerTime();

        long currentServerMs;
        if (isTimeSynced) currentServerMs = serverTimeOffsetMs + (long)(Time.unscaledTime * 1000L);
        else currentServerMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        DateTimeOffset utcTime = DateTimeOffset.FromUnixTimeMilliseconds(currentServerMs);
        return utcTime.ToOffset(MOSCOW_OFFSET).DateTime;
    }

    public void InitializeForToday()
    {
        DateTime today = GetMoscowTime().Date;

        bool archiveChanged = EnsureLast5DaysExist(); // теперь возвращает bool
        EvaluateStreaks(today);                        // сам решает, нужен ли SaveData
        CleanUpArchive(today);

        foreach (var record in saveData.archive) RestoreTemplates(record);

        if (archiveChanged) SaveData();
    }

    // ==========================================
    // СТРИКИ И ГЕНЕРАЦИЯ
    // ==========================================

    private void EvaluateStreaks(DateTime today)
    {
        int safeDaysCount = HasPremium ? 5 : 1;
        DateTime maxEvaluatableDate = today.AddDays(-safeDaysCount);

        if (string.IsNullOrEmpty(saveData.lastEvaluatedDateKey))
        {
            saveData.lastEvaluatedDateKey = maxEvaluatableDate.ToString("yyyy-MM-dd");
            return;
        }

        DateTime lastEvalDate = DateTime.ParseExact(saveData.lastEvaluatedDateKey, "yyyy-MM-dd", null);
        if (lastEvalDate >= maxEvaluatableDate) return;

        int daysToEvaluate = (int)(maxEvaluatableDate - lastEvalDate).TotalDays;

        for (int i = 1; i <= daysToEvaluate; i++)
        {
            DateTime checkDate = lastEvalDate.AddDays(i);
            string checkKey = checkDate.ToString("yyyy-MM-dd");

            DailyQuestRecord record = saveData.archive.FirstOrDefault(r => r.dateKey == checkKey);
            int completed = record != null ? record.CompletedCount : 0;

            if (completed >= 1)
            {
                saveData.minorStreakCurrent++;
                saveData.questsInARowCurrent += completed;
            }
            else
            {
                saveData.minorStreakCurrent = 0;
                saveData.questsInARowCurrent = 0;
            }

            if (completed == DAILY_QUESTS_COUNT) saveData.perfectDaysStreakCurrent++;
            else saveData.perfectDaysStreakCurrent = 0;

            if (saveData.minorStreakCurrent > saveData.minorStreakBest) saveData.minorStreakBest = saveData.minorStreakCurrent;
            if (saveData.questsInARowCurrent > saveData.questsInARowBest) saveData.questsInARowBest = saveData.questsInARowCurrent;
            if (saveData.perfectDaysStreakCurrent > saveData.perfectDaysStreakBest) saveData.perfectDaysStreakBest = saveData.perfectDaysStreakCurrent;
        }

        saveData.lastEvaluatedDateKey = maxEvaluatableDate.ToString("yyyy-MM-dd");
        // Вызов SaveData() убран отсюда, так как он делается в InitializeForToday
    }
    public int GetTrueQuestsInARow(DateTime today)
    {
        int streak = saveData.questsInARowCurrent;
        if (string.IsNullOrEmpty(saveData.lastEvaluatedDateKey)) return streak;

        DateTime lastEval = DateTime.ParseExact(saveData.lastEvaluatedDateKey, "yyyy-MM-dd", null);
        int windowDays = (int)(today - lastEval).TotalDays;

        for (int i = 1; i <= windowDays; i++)
        {
            DateTime checkDate = lastEval.AddDays(i);
            var record = saveData.archive.FirstOrDefault(r => r.dateKey == checkDate.ToString("yyyy-MM-dd"));

            if (record != null && record.CompletedCount > 0)
            {
                streak += record.CompletedCount;
            }
            else
            {
                streak = 0; // Дыра! Стрик обрывается
            }
        }
        return streak;
    }

    public int GetTrueDailyStreak(DateTime today)
    {
        int streak = saveData.minorStreakCurrent;
        if (string.IsNullOrEmpty(saveData.lastEvaluatedDateKey)) return streak;

        DateTime lastEval = DateTime.ParseExact(saveData.lastEvaluatedDateKey, "yyyy-MM-dd", null);
        int windowDays = (int)(today - lastEval).TotalDays;

        for (int i = 1; i <= windowDays; i++)
        {
            DateTime checkDate = lastEval.AddDays(i);
            var record = saveData.archive.FirstOrDefault(r => r.dateKey == checkDate.ToString("yyyy-MM-dd"));

            if (record != null && record.CompletedCount >= 1)
            {
                streak++;
            }
            else
            {
                streak = 0; // Дыра! Стрик обрывается
            }
        }
        return streak;
    }

    public int GetTruePerfectDaysStreak(DateTime today)
    {
        int streak = saveData.perfectDaysStreakCurrent;
        if (string.IsNullOrEmpty(saveData.lastEvaluatedDateKey)) return streak;

        DateTime lastEval = DateTime.ParseExact(saveData.lastEvaluatedDateKey, "yyyy-MM-dd", null);
        int windowDays = (int)(today - lastEval).TotalDays;

        for (int i = 1; i <= windowDays; i++)
        {
            DateTime checkDate = lastEval.AddDays(i);
            var record = saveData.archive.FirstOrDefault(r => r.dateKey == checkDate.ToString("yyyy-MM-dd"));

            if (record != null && record.CompletedCount == DAILY_QUESTS_COUNT)
            {
                streak++;
            }
            else
            {
                streak = 0; // Дыра! Стрик обрывается
            }
        }
        return streak;
    }


    private void CleanUpArchive(DateTime today)
    {
        DateTime cutoffDate = today.AddDays(-MAX_ARCHIVE_DAYS + 1);
        saveData.archive.RemoveAll(r => DateTime.ParseExact(r.dateKey, "yyyy-MM-dd", null) < cutoffDate);
    }

    private void RestoreTemplates(DailyQuestRecord record)
    {
        foreach (var instance in record.quests)
        {
            instance.template = allQuestTemplates.FirstOrDefault(t => t.questId == instance.questId);
            if (instance.template != null && instance.conditionValue == 0 && instance.targetValue > 1)
            {
                bool isThresholdQuest =
                    instance.template.actionType == QuestActionType.PlayTimeUnder ||
                    instance.template.actionType == QuestActionType.WinWithMoreThanXMoves ||
                    instance.template.actionType == QuestActionType.WinWithMaxUndos ||
                    instance.template.actionType == QuestActionType.WinWithMaxFreeCells ||
                    instance.template.actionType == QuestActionType.WinWithRemainingStock ||
                    instance.template.actionType == QuestActionType.WinWithRemainingShuffles ||
                    instance.template.actionType == QuestActionType.WinWithMaxStockDraws;

                if (isThresholdQuest)
                {
                    instance.conditionValue = instance.targetValue;
                    instance.targetValue = 1;
                    if (instance.currentProgress >= 1) { instance.currentProgress = 1; instance.isCompleted = true; }
                }
            }
        }
    }

    private Dictionary<QuestCategory, int> CalculateCategoryWeights(DateTime targetDate)
    {
        Dictionary<QuestCategory, int> weights = new Dictionary<QuestCategory, int>();
        List<QuestCategory> allCategories = Enum.GetValues(typeof(QuestCategory)).Cast<QuestCategory>().ToList();

        foreach (var cat in allCategories) { if (cat == QuestCategory.General) continue; weights[cat] = 10; }

        int totalDaysSinceEpoch = (int)(targetDate - EPOCH_DATE).TotalDays;
        for (int d = 0; d < totalDaysSinceEpoch; d++)
        {
            System.Random simRng = new System.Random(d.GetHashCode());
            List<QuestCategory> pool = weights.Keys.ToList();
            List<QuestCategory> pickedForDay = new List<QuestCategory>();

            for (int i = 0; i < 5; i++)
            {
                QuestCategory picked = PickWeighted(pool, weights, simRng);
                pickedForDay.Add(picked);
                pool.Remove(picked);
            }
            foreach (var key in weights.Keys.ToList()) { if (pickedForDay.Contains(key)) weights[key] = 1; else weights[key] += 3; }
        }
        return weights;
    }

    private QuestCategory PickWeighted(List<QuestCategory> available, Dictionary<QuestCategory, int> weights, System.Random rng)
    {
        int totalWeight = available.Sum(c => weights[c]);
        int r = rng.Next(0, totalWeight);
        int current = 0;
        foreach (var cat in available) { current += weights[cat]; if (r < current) return cat; }
        return available.Last();
    }

    private DailyQuestRecord GenerateQuestsForDate(DateTime date)
    {
        DailyQuestRecord record = new DailyQuestRecord { dateKey = date.ToString("yyyy-MM-dd") };
        int dailySeed = (date - EPOCH_DATE).TotalDays.GetHashCode();
        UnityEngine.Random.InitState(dailySeed);
        System.Random sysRng = new System.Random(dailySeed);

        var currentWeights = CalculateCategoryWeights(date);
        List<QuestCategory> availableGames = currentWeights.Keys.ToList();
        List<QuestCategory> selectedGames = new List<QuestCategory>();

        for (int i = 0; i < 5; i++)
        {
            QuestCategory picked = PickWeighted(availableGames, currentWeights, sysRng);
            selectedGames.Add(picked);
            availableGames.Remove(picked);
        }

        List<QuestDifficulty> allTargetDiffs = new List<QuestDifficulty>
        {
            QuestDifficulty.Easy, QuestDifficulty.Easy, QuestDifficulty.Easy,
            QuestDifficulty.Medium, QuestDifficulty.Medium, QuestDifficulty.Hard
        };

        var generalTemplates = allQuestTemplates.Where(t => t.category == QuestCategory.General && t.isAvailableInPool).ToList();
        if (generalTemplates.Count > 0)
        {
            var pickedGeneral = generalTemplates[sysRng.Next(generalTemplates.Count)];
            record.quests.Add(new QuestInstance(pickedGeneral));
            allTargetDiffs.Remove(pickedGeneral.difficulty);
        }
        else allTargetDiffs.Remove(QuestDifficulty.Easy);

        allTargetDiffs = allTargetDiffs.OrderBy(x => sysRng.Next()).ToList();

        for (int i = 0; i < selectedGames.Count; i++)
        {
            QuestCategory cat = selectedGames[i];
            QuestDifficulty reqDiff = allTargetDiffs[i];

            var gameTemplates = allQuestTemplates.Where(t => t.category == cat && t.difficulty == reqDiff && t.isAvailableInPool).ToList();
            if (gameTemplates.Count == 0) gameTemplates = allQuestTemplates.Where(t => t.category != QuestCategory.General && t.difficulty == reqDiff && t.isAvailableInPool).ToList();

            if (gameTemplates.Count > 0)
            {
                var template = gameTemplates[sysRng.Next(gameTemplates.Count)];
                record.quests.Add(new QuestInstance(template));
            }
        }
        return record;
    }

    // ==========================================
    // ОБРАБОТКА ПРОГРЕССА (СИНХРОННОЕ СОХРАНЕНИЕ)
    // ==========================================

    public void ReportEvent(QuestEventContext context)
    {
        if (GameSettings.IsTutorialMode) return;
        bool progressChanged = false;

        foreach (var record in saveData.archive)
        {
            if (record.dateKey != SelectedDateKey) continue;

            foreach (var quest in record.quests)
            {
                if (quest.isCompleted) continue;

                if (DoesContextMatchQuest(context, quest))
                {
                    bool wasCompleted = quest.isCompleted;
                    int sign = Math.Sign(context.value);
                    int amountToAdd = context.value;

                    if (quest.targetValue == 1) amountToAdd = sign;

                    if (quest.template.actionType == QuestActionType.AccumulatePlayTime)
                    {
                        quest.conditionValue += amountToAdd;
                        int minutesToAdd = quest.conditionValue / 60;
                        if (minutesToAdd > 0) { amountToAdd = minutesToAdd; quest.conditionValue -= (minutesToAdd * 60); }
                        else amountToAdd = 0;
                    }
                    else if (quest.template.actionType == QuestActionType.ComboPairsWithoutDraw ||
                        quest.template.actionType == QuestActionType.ComboCardsWithoutDraw ||
                        quest.template.actionType == QuestActionType.ComboGapsWithoutShuffle)
                    {
                        if (context.value > quest.currentProgress) amountToAdd = context.value - quest.currentProgress;
                        else amountToAdd = 0;
                    }
                    else if (quest.template.actionType == QuestActionType.MoveCardSequence) amountToAdd = sign;

                    if (amountToAdd != 0)
                    {
                        int oldProgress = quest.currentProgress;
                        string dictKey = $"{record.dateKey}_{quest.questId}";

                        quest.AddProgress(amountToAdd, context.eventId);
                        int newProgress = quest.currentProgress;
                        progressChanged = true;

                        if (amountToAdd < 0)
                        {
                            if (lastNotifiedProgress.ContainsKey(dictKey) && newProgress < lastNotifiedProgress[dictKey])
                                lastNotifiedProgress[dictKey] = newProgress;
                        }
                        else
                        {
                            if (!lastNotifiedProgress.ContainsKey(dictKey) || oldProgress < lastNotifiedProgress[dictKey])
                                lastNotifiedProgress[dictKey] = oldProgress;

                            bool justCompleted = !wasCompleted && quest.isCompleted;
                            int step = GetNotificationStep(quest);
                            int startProgress = lastNotifiedProgress[dictKey];

                            if (justCompleted || (startProgress / step < newProgress / step))
                            {
                                OnQuestProgressNotification?.Invoke(quest, startProgress, newProgress);
                                lastNotifiedProgress[dictKey] = newProgress;
                            }

                            if (justCompleted)
                            {
                                if (record.CompletedCount == DAILY_QUESTS_COUNT)
                                    saveData.perfectDaysTotal++;

                                DateTime today = GetMoscowTime().Date;

                                // Динамический расчет рекордов при каждом выполнении
                                int currentQ = GetTrueQuestsInARow(today);
                                if (currentQ > saveData.questsInARowBest) saveData.questsInARowBest = currentQ;

                                int currentD = GetTrueDailyStreak(today);
                                if (currentD > saveData.minorStreakBest) saveData.minorStreakBest = currentD;

                                int currentP = GetTruePerfectDaysStreak(today);
                                if (currentP > saveData.perfectDaysStreakBest) saveData.perfectDaysStreakBest = currentP;

                                StatisticsManager.Instance?.RegisterQuestCompleted(
                                    currentQ,
                                    currentD,
                                    quest.template.xpReward
                                );
                            }
                        }
                    }
                }
            }
        }

        if (progressChanged)
        {
            SaveData();
        }
    }

    private int GetNotificationStep(QuestInstance quest)
    {
        if (quest.template.actionType == QuestActionType.ComboPairsWithoutDraw ||
            quest.template.actionType == QuestActionType.ComboCardsWithoutDraw) return 1;

        int target = quest.targetValue;
        if (target <= 10) return 1;
        if (target <= 25) return 5;
        return 10;
    }

    private bool DoesContextMatchQuest(QuestEventContext ctx, QuestInstance quest)
    {
        var template = quest.template;
        if (template.actionType != ctx.actionType) return false;
        if (template.category != QuestCategory.General && template.category != ctx.category) return false;
        if (template.requiredMatchDifficulty != TargetMatchDifficulty.Any && template.requiredMatchDifficulty != ctx.matchDifficulty) return false;

        if (!string.IsNullOrEmpty(template.requiredVariant) && template.requiredVariant != "Any" && template.requiredVariant != "None")
        {
            string[] acceptedVariants = template.requiredVariant.Split(',');
            bool matchFound = false;
            foreach (string v in acceptedVariants) { if (v.Trim() == ctx.variant) { matchFound = true; break; } }
            if (!matchFound) return false;
        }

        if (template.targetRanks != null && template.targetRanks.Count > 0)
        {
            if (int.TryParse(ctx.eventId, out int rankPlayed))
                if (!template.targetRanks.Contains(rankPlayed)) return false;
        }

        int conditionThreshold = template.requiredConditionValue > 0 ? template.requiredConditionValue : quest.conditionValue;

        if (template.actionType == QuestActionType.MoveCardSequence || template.actionType == QuestActionType.ComboPairsWithoutDraw || template.actionType == QuestActionType.ComboCardsWithoutDraw)
            if (ctx.value < conditionThreshold) return false;

        if (template.actionType == QuestActionType.PlayTimeUnder) if (ctx.value > conditionThreshold) return false;
        if (template.actionType == QuestActionType.WinWithMaxUndos) if (ctx.value > conditionThreshold) return false;
        if (template.actionType == QuestActionType.WinWithMoreThanXMoves) if (ctx.value < conditionThreshold) return false;
        if (template.actionType == QuestActionType.WinWithMaxFreeCells) if (ctx.value > conditionThreshold) return false;
        if (template.actionType == QuestActionType.WinWithRemainingStock || template.actionType == QuestActionType.WinWithRemainingShuffles)
            if (ctx.value < conditionThreshold) return false;
        if (template.actionType == QuestActionType.WinWithMaxStockDraws) if (ctx.value > conditionThreshold) return false;

        return true;
    }

    public void ResetMatchSpecificQuests()
    {
        if (saveData == null) return;
        string todayKey = GetMoscowTime().Date.ToString("yyyy-MM-dd");
        var record = saveData.archive.FirstOrDefault(r => r.dateKey == todayKey);
        if (record != null)
        {
            bool changed = false;
            foreach (var q in record.quests)
            {
                if (q.isCompleted) continue;
                if (q.template.actionType == QuestActionType.ClearTableauColumn || q.template.actionType == QuestActionType.MoveSpecificRanks)
                {
                    if (q.currentProgress > 0) { q.currentProgress = 0; changed = true; }
                }
            }
            if (changed) SaveData();
        }
    }

    public void ResetQuestProgressByAction(QuestActionType actionType)
    {
        if (saveData == null) return;
        string todayKey = GetMoscowTime().Date.ToString("yyyy-MM-dd");
        var record = saveData.archive.FirstOrDefault(r => r.dateKey == todayKey);
        if (record != null)
        {
            bool changed = false;
            foreach (var q in record.quests)
            {
                if (!q.isCompleted && q.template.actionType == actionType)
                {
                    if (q.currentProgress > 0) { q.currentProgress = 0; changed = true; }
                }
            }
            if (changed) SaveData();
        }
    }

    public void SyncCurrentQuestsWithStatistics()
    {
        if (StatisticsManager.Instance == null || saveData.archive.Count == 0) return;
        string todayKey = GetMoscowTime().Date.ToString("yyyy-MM-dd");
        DailyQuestRecord todayRecord = saveData.archive.FirstOrDefault(r => r.dateKey == todayKey);
        if (todayRecord == null) return;

        bool changed = false;
        DateTime today = GetMoscowTime().Date;
        var allStats = StatisticsManager.Instance.GetAllEntriesRaw();

        foreach (var quest in todayRecord.quests)
        {
            if (quest.isCompleted) continue;

            if (quest.targetValue == 1)
            {
                if (IsQuestSatisfiedByHistory(quest, allStats, today))
                {
                    quest.currentProgress = 1;
                    quest.isCompleted = true;
                    changed = true;

                    if (todayRecord.CompletedCount == DAILY_QUESTS_COUNT)
                        saveData.perfectDaysTotal++;

                    int currentQ = GetTrueQuestsInARow(today);
                    if (currentQ > saveData.questsInARowBest) saveData.questsInARowBest = currentQ;

                    int currentD = GetTrueDailyStreak(today);
                    if (currentD > saveData.minorStreakBest) saveData.minorStreakBest = currentD;

                    int currentP = GetTruePerfectDaysStreak(today);
                    if (currentP > saveData.perfectDaysStreakBest) saveData.perfectDaysStreakBest = currentP;

                    StatisticsManager.Instance?.RegisterQuestCompleted(currentQ, currentD);
                }
            }
        }
        if (changed) SaveData();
    }

    private bool IsQuestSatisfiedByHistory(QuestInstance quest, List<StatEntry> allStats, DateTime today)
    {
        var template = quest.template;
        foreach (var entry in allStats)
        {
            if (entry.data == null || entry.data.history == null) continue;
            foreach (var h in entry.data.history)
            {
                DateTime playedDate;
                bool parsed = DateTime.TryParse(h.playedAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out playedDate);
                if (!parsed) parsed = DateTime.TryParse(h.playedAt, out playedDate);

                if (parsed && (playedDate.Date == DateTime.Now.Date || playedDate.Date == today.Date))
                {
                    System.Enum.TryParse(h.gameName, out QuestCategory cat);
                    if (template.category != QuestCategory.General && template.category != cat) continue;
                    if (template.requiredMatchDifficulty != TargetMatchDifficulty.Any && !string.IsNullOrEmpty(h.difficulty) && template.requiredMatchDifficulty.ToString() != h.difficulty) continue;

                    if (!string.IsNullOrEmpty(template.requiredVariant) && template.requiredVariant != "Any" && template.requiredVariant != "None")
                    {
                        if (string.IsNullOrEmpty(h.variant)) continue;
                        string[] acceptedVariants = template.requiredVariant.Split(',');
                        bool matchFound = false;
                        foreach (string v in acceptedVariants) { if (v.Trim() == h.variant) { matchFound = true; break; } }
                        if (!matchFound) continue;
                    }

                    if (template.actionType == QuestActionType.WinGame && h.won) return true;
                    if (template.actionType == QuestActionType.CompleteMatch) return true;
                    if (template.actionType == QuestActionType.PlayTimeUnder && h.won)
                    {
                        int conditionThreshold = template.requiredConditionValue > 0 ? template.requiredConditionValue : quest.conditionValue;
                        if (h.time <= conditionThreshold) return true;
                    }
                    if (template.actionType == QuestActionType.WinWithMoreThanXMoves && h.won)
                    {
                        int conditionThreshold = template.requiredConditionValue > 0 ? template.requiredConditionValue : quest.conditionValue;
                        if (h.moves >= conditionThreshold) return true;
                    }
                }
            }
        }
        return false;
    }

    public bool EnsureLast5DaysExist()
    {
        DateTime today = GetMoscowTime().Date;
        bool changed = false;

        for (int i = 4; i >= 0; i--)
        {
            DateTime targetDate = today.AddDays(-i);
            string dateKey = targetDate.ToString("yyyy-MM-dd");
            var record = saveData.archive.Find(r => r.dateKey == dateKey);

            if (record == null)
            {
                saveData.archive.Add(GenerateQuestsForDate(targetDate));
                changed = true;
            }
        }
        return changed;
    }

    // ==========================================
    // БУСТЕРЫ
    // ==========================================

    public void ClaimDailyReward(string dateKey)
    {
        var record = saveData.archive.FirstOrDefault(r => r.dateKey == dateKey);
        if (record != null && record.CompletedCount >= 6 && !record.isRewardClaimed)
        {
            record.isRewardClaimed = true;
            int rewardAmount = HasPremium ? 6 : 3;
            saveData.availableXpTickets += rewardAmount;
            SaveData();
        }
    }

    public void ApplyDistributedRewards(string dateKey, Dictionary<QuestCategory, int> distribution)
    {
        if (saveData.activeXpBuffs == null) saveData.activeXpBuffs = new List<XpBuffRecord>();
        int totalSpent = 0;
        int gamesPerBooster = HasPremium ? 6 : 3;

        foreach (var kvp in distribution)
        {
            if (kvp.Value > 0)
            {
                totalSpent += kvp.Value;
                var buff = saveData.activeXpBuffs.FirstOrDefault(b => b.gameCategory == kvp.Key);
                if (buff == null) { buff = new XpBuffRecord { gameCategory = kvp.Key, remainingWins = 0 }; saveData.activeXpBuffs.Add(buff); }
                buff.remainingWins += (kvp.Value * gamesPerBooster);
            }
        }
        saveData.availableXpTickets -= totalSpent;
        if (saveData.availableXpTickets < 0) saveData.availableXpTickets = 0;
        SaveData();
    }

    public bool ConsumeXpBuffForGame(QuestCategory category)
    {
        if (saveData.activeXpBuffs == null) return false;
        var buff = saveData.activeXpBuffs.FirstOrDefault(b => b.gameCategory == category);
        if (buff != null && buff.remainingWins > 0)
        {
            buff.remainingWins--;
            SaveData();
            return true;
        }
        return false;
    }

    public bool HasActiveXpBuff(QuestCategory category)
    {
        if (saveData == null || saveData.activeXpBuffs == null) return false;
        var buff = saveData.activeXpBuffs.FirstOrDefault(b => b.gameCategory == category);
        return buff != null && buff.remainingWins > 0;
    }
}