using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerProfilePanel : MonoBehaviour
{
    [Header("UI Элементы панели")]
    public TMP_Text playerTitleText;   // Текст звания под именем

    [Header("Кнопки и окна")]
    public Button openTitlesButton;
    public GameObject titlesPanelObj;

    private void Start()
    {
        if (openTitlesButton != null)
            openTitlesButton.onClick.AddListener(OpenTitlesPanel);

        // Подписываемся на события званий
        if (TitleManager.Instance != null)
            TitleManager.Instance.OnTitleChanged += UpdateTitleText;

        // Подписываемся на смену языка, чтобы звание сразу переводилось
        LocalizationManager.OnLocalizationLoaded += UpdateTitleText;

        UpdateTitleText();
    }

    private void OnDestroy()
    {
        if (TitleManager.Instance != null)
            TitleManager.Instance.OnTitleChanged -= UpdateTitleText;

        LocalizationManager.OnLocalizationLoaded -= UpdateTitleText;
    }

    private void UpdateTitleText()
    {
        if (TitleManager.Instance == null) return;

        string titleKey = TitleManager.Instance.GetCurrentTitleKey();

        // Проверяем, готов ли локализатор
        if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
        {
            playerTitleText.text = LocalizationManager.instance.GetLocalizedValue(titleKey);
        }
        else
        {
            playerTitleText.text = "Loading...";
        }
    }

    private void OpenTitlesPanel()
    {
        if (titlesPanelObj != null)
        {
            var selectionPanel = titlesPanelObj.GetComponent<TitleSelectionPanel>();
            if (selectionPanel != null)
            {
                // Запускаем открытие через специальный метод с анимацией
                selectionPanel.OpenPanel();
            }
            else
            {
                titlesPanelObj.SetActive(true);
            }
        }
    }
}