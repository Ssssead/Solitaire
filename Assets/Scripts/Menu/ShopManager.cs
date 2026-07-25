using UnityEngine;
using UnityEngine.UI;
using YG;

public class ShopManager : MonoBehaviour
{
    [Header("ID покупок (строго как в консоли!)")]
    public string premiumId = "Premium";
    public string premium2Id = "Premium2"; // <--- НОВЫЙ ID СО СКИДКОЙ
    public string noAdsId = "NoADS";

    [Header("Кнопки интерфейса")]
    public Button premiumButton;
    public Button noAdsButton;
    public LocalizedText premiumDescText; // Сюда перетащим текст описания Премиума
    [Header("Тексты статуса (Купить / Куплено)")]
    public LocalizedText premiumStatusText;
    public LocalizedText noAdsStatusText;

    [Header("Объекты Цены (скрываются после покупки)")]
    public GameObject premiumPriceObj;
    public GameObject noAdsPriceObj;

    [Header("Иконки валюты (скрываются после покупки)")]
    public GameObject premiumCurrencyIcon;
    public GameObject noAdsCurrencyIcon;

    private void OnEnable()
    {
        YG2.onPurchaseSuccess += HandlePurchaseSuccess;
        YG2.onGetPayments += RefreshShopUI;
        RefreshShopUI();
    }

    private void OnDisable()
    {
        YG2.onPurchaseSuccess -= HandlePurchaseSuccess;
        YG2.onGetPayments -= RefreshShopUI;
    }

    public void BuyPremium()
    {
        if (StatisticsManager.Instance != null && StatisticsManager.Instance.IsUserPremium) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        // Определяем, какой именно Премиум продавать (зависит от того, куплена ли реклама)
        string targetId = (StatisticsManager.Instance != null && StatisticsManager.Instance.IsAdsDisabled) ? premium2Id : premiumId;

        YG2.BuyPayments(targetId);
    }

    public void BuyNoAds()
    {
        if (StatisticsManager.Instance != null &&
           (StatisticsManager.Instance.IsAdsDisabled || StatisticsManager.Instance.IsUserPremium)) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        YG2.BuyPayments(noAdsId);
    }

    private void HandlePurchaseSuccess(string purchasedId)
    {
        Debug.Log($"[ShopManager] Покупка успешно завершена: {purchasedId}. Обновляем UI магазина.");
        RefreshShopUI();
    }

    public void RefreshShopUI()
    {
        if (StatisticsManager.Instance == null) return;

        bool hasPremium = StatisticsManager.Instance.IsUserPremium;
        bool hasNoAds = StatisticsManager.Instance.IsAdsDisabled;

        // --- Блокировка Премиума (или применение скидки) ---
        if (premiumButton != null)
        {
            premiumButton.interactable = !hasPremium;
            var py = premiumButton.GetComponent<PurchaseYG>();

            if (hasPremium)
            {
                if (premiumStatusText != null)
                {
                    premiumStatusText.key = "Shop_Purchased";
                    premiumStatusText.UpdateText();
                }
                if (premiumPriceObj != null) premiumPriceObj.SetActive(false);
                if (premiumCurrencyIcon != null) premiumCurrencyIcon.SetActive(false);
                if (py != null) py.enabled = false;
            }
            else
            {
                // ТОВАР ЕЩЕ НЕ КУПЛЕН:
                string currentPremiumId = hasNoAds ? premium2Id : premiumId;

                // ---> НОВОЕ: МЕНЯЕМ ОПИСАНИЕ <---
                if (premiumDescText != null)
                {
                    premiumDescText.key = hasNoAds ? "Shop_Premium2_Desc" : "Shop_Premium_Desc";
                    premiumDescText.UpdateText();
                }
                // ---------------------------------
                // Подменяем ID в скрипте Яндекса и заставляем его обновить текст цены
                if (py != null)
                {
                    py.id = currentPremiumId;

                    // Если данные о товарах от Яндекса уже загружены - мгновенно обновляем UI
                    if (YG2.purchases != null && YG2.purchases.Length > 0)
                    {
                        var purchaseData = YG2.PurchaseByID(currentPremiumId);
                        if (purchaseData != null) py.UpdateEntries(purchaseData);
                    }

                    if (!py.enabled) py.enabled = true;
                }

                if (premiumStatusText != null)
                {
                    premiumStatusText.key = "Shop_Buy";
                    premiumStatusText.UpdateText();
                }
                if (premiumPriceObj != null) premiumPriceObj.SetActive(true);
                if (premiumCurrencyIcon != null) premiumCurrencyIcon.SetActive(true);
            }
        }

        // --- Блокировка Отключения рекламы ---
        if (noAdsButton != null)
        {
            bool shouldBlockNoAds = hasNoAds || hasPremium;
            noAdsButton.interactable = !shouldBlockNoAds;
            var py = noAdsButton.GetComponent<PurchaseYG>();

            if (shouldBlockNoAds)
            {
                if (noAdsStatusText != null)
                {
                    noAdsStatusText.key = "Shop_Purchased";
                    noAdsStatusText.UpdateText();
                }
                if (noAdsPriceObj != null) noAdsPriceObj.SetActive(false);
                if (noAdsCurrencyIcon != null) noAdsCurrencyIcon.SetActive(false);
                if (py != null) py.enabled = false;
            }
            else
            {
                if (noAdsStatusText != null)
                {
                    noAdsStatusText.key = "Shop_Buy";
                    noAdsStatusText.UpdateText();
                }
                if (noAdsPriceObj != null) noAdsPriceObj.SetActive(true);
                if (noAdsCurrencyIcon != null) noAdsCurrencyIcon.SetActive(true);
                if (py != null && !py.enabled) py.enabled = true;
            }
        }
    }
/*
    // МЕТОДЫ СБРОСА ДЛЯ ТЕСТОВ
    public void OnResetStatsClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.ResetAllStatistics();
        if (MenuController.Instance != null) MenuController.Instance.OnCloseOverlayClicked();
    }

    public void OnResetPurchasesClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
      
        RefreshShopUI();
    }
*/
}