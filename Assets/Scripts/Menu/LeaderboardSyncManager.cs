using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YG;

public class LeaderboardSyncManager : MonoBehaviour
{
    public static LeaderboardSyncManager Instance;

    private Queue<KeyValuePair<string, int>> syncQueue = new Queue<KeyValuePair<string, int>>();
    private bool isSyncing = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(InitialSyncRoutine());
    }

    private IEnumerator InitialSyncRoutine()
    {
        while (!YG2.isSDKEnabled || StatisticsManager.Instance == null)
        {
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(1f);

        // ---> НОВОЕ: Проверка авторизации <---
        // Если игрок не авторизован (false), Яндексу лидерборды не нужны, прерываем синхронизацию
        if (!YG2.player.auth)
        {
            Debug.Log("[LB Sync] Игрок не авторизован. Синхронизация лидербордов отменена.");
            yield break; // Останавливаем корутину
        }

        StatData globalData = StatisticsManager.Instance.GetGlobalStats();
        if (globalData != null)
        {
            CheckAndEnqueue("GlobalLVL", globalData.currentLevel);
        }

        foreach (var entry in StatisticsManager.Instance.GetAllEntriesRaw())
        {
            if (entry.key != "Global" && entry.key.EndsWith("_Global"))
            {
                StatData data = StatisticsManager.Instance.stats.GetData(entry.key);

                if (data != null)
                {
                    string gameName = entry.key.Replace("_Global", "");
                    CheckAndEnqueue(gameName + "LVL", data.currentLevel);
                }
            }
        }
    }

    public void SendLevelIfChanged(string lbName, int level)
    {
        CheckAndEnqueue(lbName, level);
    }

    private void CheckAndEnqueue(string lbName, int level)
    {
        // Дополнительная защита: не ставим в очередь, если нет авторизации
        if (!YG2.player.auth) return;

        // ---> НОВОЕ: Уникальный ключ для каждого аккаунта! <---
        // Получаем ID пользователя (например, "uid-12345")
        string playerId = YG2.player.id;

        // Создаем ключ вида: LastSentLB_uid-12345_GlobalLVL
        string prefsKey = $"LastSentLB_{playerId}_{lbName}";

        int lastSentLevel = PlayerPrefs.GetInt(prefsKey, 0);

        if (level > lastSentLevel)
        {
            syncQueue.Enqueue(new KeyValuePair<string, int>(lbName, level));

            if (!isSyncing)
            {
                StartCoroutine(ProcessQueueRoutine());
            }
        }
    }

    private IEnumerator ProcessQueueRoutine()
    {
        isSyncing = true;

        while (syncQueue.Count > 0)
        {
            if (YG2.isSDKEnabled && YG2.player.auth)
            {
                var task = syncQueue.Dequeue();
                string lbName = task.Key;
                int level = task.Value;

                // 1. Отправляем в Яндекс
                YG2.SetLeaderboard(lbName, level);

                // ---> НОВОЕ: Сбрасываем кэш ИМЕННО ЭТОЙ таблицы! <---
                // При следующем клике на эту вкладку, плагин не найдет её в памяти и скачает заново.
                LeaderboardYG.ResetCache(lbName);

                // 2. ЗАПОМИНАЕМ с привязкой к ID аккаунта
                string prefsKey = $"LastSentLB_{YG2.player.id}_{lbName}";
                PlayerPrefs.SetInt(prefsKey, level);
                PlayerPrefs.Save();

                Debug.Log($"[LB Sync] Отправлено: {lbName} = {level} для игрока {YG2.player.name}");

                yield return new WaitForSeconds(2f);
            }
            else
            {
                yield return new WaitForSeconds(2f);
            }
        }

        isSyncing = false;
    }
}