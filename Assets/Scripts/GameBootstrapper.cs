using UnityEngine;
using System.Collections;
using UnityEngine.AddressableAssets;
/*
public class GameBootstrapper : MonoBehaviour
{
    // Флаг и событие для старта анимации
    public static bool IsHeavyLoaded { get; private set; } = false;
    public static event System.Action OnHeavySystemsLoaded;

    [Header("Priority 0 (Frame 1)")]
    public AudioManager audioManager;
    public LocalizationManager localizationManager;

    [Header("Priority 1 (Heavy Data)")]
    public StatisticsManager statisticsManager;
    public DealCacheSystem dealCacheSystem;

    [Header("Priority 2 (Lightweight / Background)")]
    public GlobalSaveManager globalSaveManager;
    public AdManager adManager;
    public QuestManager questManager;

    private void Start()
    {
        StartCoroutine(InitializationRoutine());
    }
    private IEnumerator BackgroundDownloadRoutine()
    {
        // 1. Самое важное скачиваем параллельно: Стандартную колоду и популярные игры (Tier 1)
        // Колоду не ждем (без yield), пускаем параллельно с первой группой сцен
        Addressables.DownloadDependenciesAsync("Deck_Standard");

        var tier1Op = Addressables.DownloadDependenciesAsync("GameScenes_Tier1");
        yield return tier1Op; // Ждем, пока скачается первая тройка игр

        // 2. Игры второго приоритета (Tier 2)
        var tier2Op = Addressables.DownloadDependenciesAsync("GameScenes_Tier2");
        yield return tier2Op; // Ждем завершения

        // 3. Игры третьего приоритета (Tier 3)
        var tier3Op = Addressables.DownloadDependenciesAsync("GameScenes_Tier3");
        yield return tier3Op;

        // 4. Премиум колоды качаем в самом конце, чтобы не забивать канал
        if (statisticsManager != null && statisticsManager.IsUserPremium)
        {
            Addressables.DownloadDependenciesAsync("Decks_Premium");
        }

        Debug.Log("[Bootstrapper] Последовательная фоновая загрузка всех сцен завершена!");
    }
    private IEnumerator InitializationRoutine()
    {
        // === 1. БАЗА (Выполняется мгновенно) ===
        if (audioManager) audioManager.Initialize();
        if (localizationManager) localizationManager.Initialize();
        yield return null;

        // === 2. ТЯЖЕЛЫЕ СИСТЕМЫ ===
        // Анимация еще не началась. Движок может смело "подвисать" 
        // на парсинге огромных JSON-файлов статистики и кэша.
        if (statisticsManager) yield return StartCoroutine(statisticsManager.InitializeAsync());
        if (dealCacheSystem) yield return StartCoroutine(dealCacheSystem.InitializeAsync());

        // === 3. СТАРТ АНИМАЦИИ ===
        // Тяжелые данные в памяти. Даем команду UI начинать полет карт.
        IsHeavyLoaded = true;
        OnHeavySystemsLoaded?.Invoke();

        // Отдаем кадр движку, чтобы анимация сдвинулась с места
        yield return null;

        // === 4. ЛЕГКИЕ СИСТЕМЫ ===
        // Карты уже летят на стол со стабильными 60 FPS. 
        // Неспеша, размазывая по кадрам, грузим остаток.
        if (globalSaveManager) globalSaveManager.Initialize();
        yield return null;

        if (adManager) adManager.Initialize();
        yield return null;

        if (questManager) yield return StartCoroutine(questManager.InitializeAsync());

        // === 5. ФОНОВАЯ ДОЗАГРУЗКА АССЕТОВ ===
        // Запускаем корутину последовательной фоновой загрузки
        StartCoroutine(BackgroundDownloadRoutine());

        Debug.Log("[Bootstrapper] Все системы загружены. Фоновое скачивание текстур начато.");
    } // Конец InitializationRoutine
}*/