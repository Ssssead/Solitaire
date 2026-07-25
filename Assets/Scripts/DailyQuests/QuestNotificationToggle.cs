using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(Button))]
public class QuestNotificationToggle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI References")]
    [Tooltip("Компонент Image самой кнопки")]
    public Image iconImage;

    [Header("Sprites (Уведомления ВКЛ)")]
    public Sprite iconOnNormal;    // Желтый колокольчик (в покое)
    public Sprite iconOnPressed;   // Желтый колокольчик (нажатый)

    [Header("Sprites (Уведомления ВЫКЛ)")]
    public Sprite iconOffNormal;   // Синий колокольчик (в покое)
    public Sprite iconOffPressed;  // Синий колокольчик (нажатый)

    [Header("Tooltip (Всплывающая панель)")]
    public GameObject tooltipPanel;
    public TMP_Text tooltipText;

    [Tooltip("Ключи локализации для текста подсказки")]
    public string textOnKey = "Quest_Notifications_On";
    public string textOffKey = "Quest_Notifications_Off";

    private bool isEnabled = true;
    private bool isPressed = false;
    private const string PREF_KEY = "QuestNotificationsEnabled";

    private void Start()
    {
        if (iconImage == null) iconImage = GetComponent<Image>();

        // Загружаем сохраненное состояние (по умолчанию 1 - включено)
        isEnabled = PlayerPrefs.GetInt(PREF_KEY, 1) == 1;
        UpdateButtonVisuals();

        // Прячем тултип на старте
        if (tooltipPanel != null) tooltipPanel.SetActive(false);

        Button btn = GetComponent<Button>();
        // Отключаем стандартный переход Unity (Color Tint)
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(ToggleNotifications);
    }

    public void ToggleNotifications()
    {
        isEnabled = !isEnabled;

        // Сохраняем настройку
        PlayerPrefs.SetInt(PREF_KEY, isEnabled ? 1 : 0);
        PlayerPrefs.Save();

        UpdateButtonVisuals();

        // Обновляем текст тултипа "на лету", если мышка всё ещё на кнопке
        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            UpdateTooltipText();
        }

        // Воспроизводим звук интерфейса
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");
    }

    private void UpdateButtonVisuals()
    {
        if (iconImage == null) return;

        // Подставляем правильный спрайт в зависимости от состояния
        if (isEnabled)
        {
            iconImage.sprite = isPressed ? iconOnPressed : iconOnNormal;
        }
        else
        {
            iconImage.sprite = isPressed ? iconOffPressed : iconOffNormal;
        }
    }

    private void UpdateTooltipText()
    {
        if (tooltipText != null)
        {
            // Выбираем нужный ключ
            string key = isEnabled ? textOnKey : textOffKey;

            // Получаем текст из локализации
            string localizedStr = LocalizationManager.instance != null ? LocalizationManager.instance.GetLocalizedValue(key) : key;

            // Если локализатор не нашел ключ (возвращает "Localized text not found"), используем заглушку
            if (string.IsNullOrEmpty(localizedStr) || localizedStr.Contains("not found"))
            {
                localizedStr = isEnabled ? "Уведомления включены" : "Уведомления выключены";
            }

            tooltipText.text = localizedStr;
        }
    }

    // --- Обработка нажатия мыши ---
    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        UpdateButtonVisuals();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        UpdateButtonVisuals();
    }

    // --- Обработка наведения мыши (показываем подсказку) ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipPanel != null)
        {
            UpdateTooltipText();
            tooltipPanel.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }
}