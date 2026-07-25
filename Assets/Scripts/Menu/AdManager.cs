using System;
using UnityEngine;
using YG;

public class AdManager : MonoBehaviour
{
    public static AdManager Instance;

    [Header("Настройки Interstitial (Обычная реклама)")]
    [Tooltip("Время между обычными рекламами (рекомендуется от 65 сек)")]
    public float interstitialCooldown = 65f;

    [Tooltip("Отсрочка обычной рекламы после того, как игрок посмотрел рекламу за вознаграждение")]
    public float cooldownAfterRewarded = 120f;

    private float interstitialTimer = 0f;

    [Header("Настройки Rewarded (Реклама за награду)")]
    [Tooltip("Сколько секунд игрок сможет использовать бонусы бесплатно после одного просмотра рекламы")]
    public float freeRewardDuration = 60f;
    private float freeRewardTimer = 0f;

    // ВНИМАНИЕ: В YG2 ID награды теперь string (строка)!
    public static event Action<string> OnRewardEarned;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
            Destroy(gameObject);
    }

    private void OnEnable()
    {
        // Подписываемся на события PluginYG2
        YG2.onCloseInterAdv += OnInterstitialClosed;
        YG2.onCloseRewardedAdv += OnRewardedClosed;
        YG2.onRewardAdv += OnYandexRewardGranted;
    }

    private void OnDisable()
    {
        // Отписываемся, чтобы избежать утечек памяти
        YG2.onCloseInterAdv -= OnInterstitialClosed;
        YG2.onCloseRewardedAdv -= OnRewardedClosed;
        YG2.onRewardAdv -= OnYandexRewardGranted;
    }

    private void Update()
    {
        // Таймер обычной рекламы
        if (interstitialTimer > 0)
            interstitialTimer -= Time.deltaTime;

        // Таймер бесплатного окна (grace period)
        if (freeRewardTimer > 0)
            freeRewardTimer -= Time.deltaTime;
    }

    // --- ПРОВЕРКА ПОКУПОК ---

    private bool AreAdsDisabled()
    {
        // Проверяем флаги покупок из StatisticsManager
        if (StatisticsManager.Instance != null)
            return StatisticsManager.Instance.IsUserPremium || StatisticsManager.Instance.IsAdsDisabled;

        return false;
    }

    // --- ВЫЗОВ РЕКЛАМЫ ---

    /// <summary>
    /// Вызов обычной межстраничной рекламы
    /// </summary>
    public void TryShowInterstitial()
    {
        // 1. Если реклама отключена (куплен Премиум или NoAds) — выходим
        if (AreAdsDisabled()) return;

        // 2. Показываем рекламу, если наш таймер истек и внутренний таймер YG2 тоже готов
        if (interstitialTimer <= 0 && YG2.isTimerAdvCompleted)
        {
            YG2.InterstitialAdvShow();
        }
    }

    /// <summary>
    /// Вызов рекламы за вознаграждение (Отмена хода, Отмена всех ходов)
    /// </summary>
    public void ShowRewarded(string id) // В YG2 параметр стал string!
    {
        // Если реклама отключена ИЛИ действует льготный период
        if (AreAdsDisabled() || freeRewardTimer > 0)
        {
            Debug.Log($"[AdManager] Выдача бесплатной награды {id}");
            GrantReward(id); // Выдаем награду мгновенно!
        }
        else
        {
            // Показываем видеорекламу
            YG2.RewardedAdvShow(id);
        }
    }

    // --- КОЛБЭКИ ОТ ЯНДЕКСА (PLUGIN YG2) ---

    private void OnInterstitialClosed()
    {
        // Игрок закрыл обычную рекламу — запускаем стандартный таймер
        interstitialTimer = interstitialCooldown;
    }

    private void OnRewardedClosed()
    {
        // Откладываем показ обычной рекламы в нашем скрипте
        interstitialTimer = cooldownAfterRewarded;

        // Подстраховка: Просим сам плагин YG2 пропустить следующий показ Interstitial, 
        // чтобы встроенный таймер плагина не выкинул рекламу внезапно
        YG2.SkipNextInterAdCall();
    }

    private void OnYandexRewardGranted(string id)
    {
        // Игрок честно посмотрел рекламу.
        // 1. Запускаем льготный период
        freeRewardTimer = freeRewardDuration;

        // 2. Выдаем саму награду
        GrantReward(id);
    }

    // --- ВНУТРЕННЯЯ ЛОГИКА ---

    private void GrantReward(string id)
    {
        // Вызываем событие, на которое подписаны другие скрипты
        OnRewardEarned?.Invoke(id);
    }

    /// <summary>
    /// Для UI: Метод, чтобы знать, нужно ли показывать значок телевизора на кнопках
    /// </summary>
    public bool IsRewardFree()
    {
        // Если куплено отключение рекламы или премиум — действие БЕСПЛАТНО всегда
        if (AreAdsDisabled()) return true;

        // Иначе проверяем обычный таймер льготного периода
        return freeRewardTimer > 0;
    }
    // --- УПРАВЛЕНИЕ STICKY БАННЕРОМ ---
    public void UpdateStickyAd()
    {
        bool showSticky = true;

        if (StatisticsManager.Instance != null)
        {
            // Если есть премиум ИЛИ куплено отключение рекламы — прячем (false)
            showSticky = !(StatisticsManager.Instance.IsUserPremium || StatisticsManager.Instance.IsAdsDisabled);
        }

        // Отправляем команду в Яндекс
        YG2.StickyAdActivity(showSticky);
    }
}