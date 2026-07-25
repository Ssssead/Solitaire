using UnityEngine;
using YG;

public class GlobalSaveManager : MonoBehaviour
{
    public static GlobalSaveManager Instance { get; private set; }

    [Header("Настройки")]
    [Tooltip("Задержка между отправками данных на сервер Яндекса (защита от спама)")]
    [SerializeField] private float saveCooldown = 5f;

    private bool isDirty = false;
    private float lastSaveTime = 0f;

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

    private void OnEnable() => YG2.onGetSDKData += DistributeCloudData;
    private void OnDisable() => YG2.onGetSDKData -= DistributeCloudData;

    private void Start()
    {
        // Если SDK уже инициализировано (или мы в редакторе без плагина), запрашиваем раздачу данных
        if (YG2.isSDKEnabled)
        {
            DistributeCloudData();
        }
    }

    // 1. ПОЛУЧЕНИЕ ДАННЫХ ОТ ЯНДЕКСА И РАЗДАЧА СИСТЕМАМ
    private void DistributeCloudData()
    {
        Debug.Log("[GlobalSaveManager] Данные из облака получены. Раздаю системам...");

        // Синхронизируем базовые покупки с менеджером статистики
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.IsUserPremium = YG2.saves.isPremium;
            StatisticsManager.Instance.IsAdsDisabled = YG2.saves.isAdsDisabled;
            StatisticsManager.Instance.LoadFromCloud(YG2.saves.statsDataJson);
        }

        if (QuestManager.Instance != null)
            QuestManager.Instance.LoadFromCloud(YG2.saves.questDataJson);

        if (DealCacheSystem.Instance != null)
            DealCacheSystem.Instance.LoadFromCloud(YG2.saves.dealCacheJson);

        // ---> ОТКЛЮЧАЕМ/ВКЛЮЧАЕМ STICKY БАННЕР В ЗАВИСИМОСТИ ОТ СОХРАНЕНИЙ <---
        if (AdManager.Instance != null)
        {
            AdManager.Instance.UpdateStickyAd();
        }
    }

    // 2. ВЫЗОВ ИЗ ДРУГИХ СКРИПТОВ ДЛЯ ОТМЕТКИ ИЗМЕНЕНИЙ
    public void MarkAsDirty()
    {
        isDirty = true;
    }

    private void Update()
    {
        // Пакетное сохранение с защитой от спама запросами
        if (isDirty && (Time.realtimeSinceStartup - lastSaveTime > saveCooldown))
        {
            ExecuteCloudSave();
        }
    }

    // 3. СБОР ДАННЫХ СО ВСЕХ СИСТЕМ И ОТПРАВКА ОДНИМ ПАКЕТОМ
    private void ExecuteCloudSave()
    {
        if (!YG2.isSDKEnabled) return;

        isDirty = false;
        lastSaveTime = Time.realtimeSinceStartup;

        // Собираем актуальные данные со всех систем
        if (StatisticsManager.Instance != null)
        {
            YG2.saves.statsDataJson = StatisticsManager.Instance.GetCloudData();
            YG2.saves.isPremium = StatisticsManager.Instance.IsUserPremium;
            YG2.saves.isAdsDisabled = StatisticsManager.Instance.IsAdsDisabled;
        }

        if (QuestManager.Instance != null)
            YG2.saves.questDataJson = QuestManager.Instance.GetCloudData();

        if (DealCacheSystem.Instance != null)
            YG2.saves.dealCacheJson = DealCacheSystem.Instance.GetCloudData();

        // Отправляем ОДИН запрос на сервер
        YG2.SaveProgress();
        Debug.Log("[GlobalSaveManager] Прогресс успешно упакован и отправлен в облако.");
    }

    private void OnApplicationQuit()
    {
        // Принудительно сохраняем при выходе, если остались несохраненные данные
        if (isDirty) ExecuteCloudSave();
    }
}