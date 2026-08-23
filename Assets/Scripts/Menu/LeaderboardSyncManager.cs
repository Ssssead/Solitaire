using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YG;

public class LeaderboardSyncManager : MonoBehaviour
{
    public static LeaderboardSyncManager Instance;

    // Очередь для плавной отправки (чтобы не превысить лимиты Яндекса)
    private Queue<KeyValuePair<string, int>> syncQueue = new Queue<KeyValuePair<string, int>>();
    private bool isSyncing = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Делаем объект бессмертным при переходах между сценами
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Запускаем проверку всех лидербордов при старте игры
        StartCoroutine(InitialSyncRoutine());
    }

    private IEnumerator InitialSyncRoutine()
    {
        // Ждем, пока проснется плагин Яндекса и наш менеджер статистики
        while (!YG2.isSDKEnabled || StatisticsManager.Instance == null)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // Даем статистике еще секунду на загрузку облачных сохранений
        yield return new WaitForSeconds(1f);

        // Проверка авторизации: если игрок гость, Яндексу лидерборды не нужны, прерываем синхронизацию
        if (!YG2.player.auth)
        {
            Debug.Log("[LB Sync] Игрок не авторизован. Синхронизация лидербордов при запуске отменена.");
            yield break; // Останавливаем корутину
        }

        // 1. Проверяем Глобальный уровень
        StatData globalData = StatisticsManager.Instance.GetGlobalStats();
        if (globalData != null)
        {
            CheckAndEnqueue("GlobalLVL", globalData.currentLevel);
        }

        // 2. Проверяем уровни всех пасьянсов
        foreach (var entry in StatisticsManager.Instance.GetAllEntriesRaw())
        {
            // Ищем ключи вроде Klondike_Global, Spider_Global и т.д.
            if (entry.key != "Global" && entry.key.EndsWith("_Global"))
            {
                // Достаем реальные данные (StatData) по ключу
                StatData data = StatisticsManager.Instance.stats.GetData(entry.key);

                if (data != null)
                {
                    string gameName = entry.key.Replace("_Global", "");
                    CheckAndEnqueue(gameName + "LVL", data.currentLevel);
                }
            }
        }
    }

    // Этот метод вызывается из StatisticsManager при победе или выполнении квеста
    public void SendLevelIfChanged(string lbName, int level)
    {
        CheckAndEnqueue(lbName, level);
    }

    private void CheckAndEnqueue(string lbName, int level)
    {
        // Дополнительная защита: не ставим в очередь, если нет авторизации
        if (!YG2.player.auth) return;

        // Получаем уникальный ID пользователя (например, "uid-12345")
        string playerId = YG2.player.id;

        // Создаем ключ вида: LastSentLB_uid-12345_GlobalLVL
        string prefsKey = $"LastSentLB_{playerId}_{lbName}";

        // Узнаем, какой уровень мы отправляли в последний раз для этого аккаунта (по умолчанию 0)
        int lastSentLevel = PlayerPrefs.GetInt(prefsKey, 0);

        // --- ИСПРАВЛЕНИЕ: Заменили '>' на '!=' ---
        // Если текущий уровень НЕ РАВЕН тому, что мы уже отправляли (позволяет понижать уровень)
        if (level != lastSentLevel)
        {
            // Добавляем в очередь на отправку
            syncQueue.Enqueue(new KeyValuePair<string, int>(lbName, level));

            // Если курьер (корутина) сейчас спит — будим его
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
            // Убеждаемся, что плагин готов и игрок все еще авторизован перед отправкой
            if (YG2.isSDKEnabled && YG2.player.auth)
            {
                var task = syncQueue.Dequeue();
                string lbName = task.Key;
                int level = task.Value;

                // 1. Отправляем новый уровень на сервера Яндекса
                YG2.SetLeaderboard(lbName, level);

                // 2. Сбрасываем кэш ИМЕННО ЭТОЙ таблицы в плагине! 
                // При следующем открытии этой вкладки плагин не найдет её в памяти и скачает актуальную.
                LeaderboardYG.ResetCache(lbName);

                // 3. ЗАПОМИНАЕМ с привязкой к ID аккаунта, что уровень успешно отправлен
                string prefsKey = $"LastSentLB_{YG2.player.id}_{lbName}";
                PlayerPrefs.SetInt(prefsKey, level);
                PlayerPrefs.Save();

                // Для удобства отладки выводим ник игрока в консоль
                Debug.Log($"[LB Sync] Успешно отправлено: {lbName} = {level} для игрока {YG2.player.name}");

                // 4. Ждем 2 секунды перед отправкой следующего, чтобы не разозлить защиту от спама
                yield return new WaitForSeconds(2f);
            }
            else
            {
                // Если нет связи с Яндексом (или слетела авторизация), просто ждем
                yield return new WaitForSeconds(2f);
            }
        }

        isSyncing = false; // Очередь пуста, курьер засыпает
    }
}