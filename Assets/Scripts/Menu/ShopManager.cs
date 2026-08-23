using UnityEngine;
using UnityEngine.UI;
using YG;

public class ShopManager : MonoBehaviour
{
    [Header("ID покупок (строго как в консоли!)")]
    public string premiumId = "Premium";
    public string premium2Id = "Premium2";
    public string noAdsId = "NoADS";

    [System.Serializable]
    public class ShopUIGroup
    {
        [Header("Кнопки интерфейса")]
        public Button premiumButton;
        public Button noAdsButton;
        public LocalizedText premiumDescText;

        [Header("Тексты статуса (Купить / Куплено)")]
        public LocalizedText premiumStatusText;
        public LocalizedText noAdsStatusText;

        [Header("Объекты Цены (скрываются после покупки)")]
        public GameObject premiumPriceObj;
        public GameObject noAdsPriceObj;

        [Header("Иконки валюты (скрываются после покупки)")]
        public GameObject premiumCurrencyIcon;
        public GameObject noAdsCurrencyIcon;
    }

    [Header("UI Магазина (Dual Orientation)")]
    public ShopUIGroup landscapeUI;
    public ShopUIGroup portraitUI;

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

        UpdateGroupUI(landscapeUI, hasPremium, hasNoAds);
        UpdateGroupUI(portraitUI, hasPremium, hasNoAds);
    }

    private void UpdateGroupUI(ShopUIGroup group, bool hasPremium, bool hasNoAds)
    {
        if (group == null) return;

        // --- Блокировка Премиума (или применение скидки) ---
        if (group.premiumButton != null)
        {
            group.premiumButton.interactable = !hasPremium;
            var py = group.premiumButton.GetComponent<PurchaseYG>();

            if (hasPremium)
            {
                if (group.premiumStatusText != null) { group.premiumStatusText.key = "Shop_Purchased"; group.premiumStatusText.UpdateText(); }
                if (group.premiumPriceObj != null) group.premiumPriceObj.SetActive(false);
                if (group.premiumCurrencyIcon != null) group.premiumCurrencyIcon.SetActive(false);
                if (py != null) py.enabled = false;
            }
            else
            {
                string currentPremiumId = hasNoAds ? premium2Id : premiumId;

                if (group.premiumDescText != null)
                {
                    group.premiumDescText.key = hasNoAds ? "Shop_Premium2_Desc" : "Shop_Premium_Desc";
                    group.premiumDescText.UpdateText();
                }

                if (py != null)
                {
                    py.id = currentPremiumId;
                    if (YG2.purchases != null && YG2.purchases.Length > 0)
                    {
                        var purchaseData = YG2.PurchaseByID(currentPremiumId);
                        if (purchaseData != null) py.UpdateEntries(purchaseData);
                    }
                    if (!py.enabled) py.enabled = true;
                }

                if (group.premiumStatusText != null) { group.premiumStatusText.key = "Shop_Buy"; group.premiumStatusText.UpdateText(); }
                if (group.premiumPriceObj != null) group.premiumPriceObj.SetActive(true);
                if (group.premiumCurrencyIcon != null) group.premiumCurrencyIcon.SetActive(true);
            }
        }

        // --- Блокировка Отключения рекламы ---
        if (group.noAdsButton != null)
        {
            bool shouldBlockNoAds = hasNoAds || hasPremium;
            group.noAdsButton.interactable = !shouldBlockNoAds;
            var py = group.noAdsButton.GetComponent<PurchaseYG>();

            if (shouldBlockNoAds)
            {
                if (group.noAdsStatusText != null) { group.noAdsStatusText.key = "Shop_Purchased"; group.noAdsStatusText.UpdateText(); }
                if (group.noAdsPriceObj != null) group.noAdsPriceObj.SetActive(false);
                if (group.noAdsCurrencyIcon != null) group.noAdsCurrencyIcon.SetActive(false);
                if (py != null) py.enabled = false;
            }
            else
            {
                if (group.noAdsStatusText != null) { group.noAdsStatusText.key = "Shop_Buy"; group.noAdsStatusText.UpdateText(); }
                if (group.noAdsPriceObj != null) group.noAdsPriceObj.SetActive(true);
                if (group.noAdsCurrencyIcon != null) group.noAdsCurrencyIcon.SetActive(true);
                if (py != null && !py.enabled) py.enabled = true;
            }
        }
    }
}