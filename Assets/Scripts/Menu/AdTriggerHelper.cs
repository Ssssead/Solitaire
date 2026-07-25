using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Обязательно для кликов по UI без кнопок

// Добавляем интерфейс IPointerClickHandler
public class AdTriggerHelper : MonoBehaviour, IPointerClickHandler
{
    private void Start()
    {
        // 1. Если на объекте ЕСТЬ компонент Button, подписываемся на него
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(TriggerAd);
        }
    }

    // 2. Сработает для любых UI-объектов (Image, Text) в Canvas, даже БЕЗ компонента Button
    public void OnPointerClick(PointerEventData eventData)
    {
        // Проверяем, что кнопки нет, чтобы не вызывать рекламу дважды
        // (если кнопка есть, сработает подписка из Start)
        if (GetComponent<Button>() == null)
        {
            TriggerAd();
        }
    }

    // 3. Сработает, если скрипт висит на обычном 2D/3D объекте с коллайдером вне Canvas
    private void OnMouseUpAsButton()
    {
        TriggerAd();
    }

    // Сам вызов рекламы
    private void TriggerAd()
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.TryShowInterstitial();
        }
        else
        {
            Debug.LogWarning("[AdTriggerHelper] AdManager не найден на сцене!");
        }
    }
}