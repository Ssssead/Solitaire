using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class GameIconTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [Tooltip("Ссылка на главный скрипт плашки (лежит на родителе)")]
    public QuestUIItem mainQuestItem;

    [Tooltip("Сама панель тултипа, которая должна появляться")]
    public GameObject tooltipPanel;

    [Tooltip("Текст внутри панели тултипа")]
    public TMP_Text tooltipText;

    private void Start()
    {
        // Прячем тултип при старте игры
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (mainQuestItem == null || tooltipPanel == null || tooltipText == null) return;

        // Запрашиваем перевод у плашки
        string gameName = mainQuestItem.GetLocalizedGameName();

        // Если это не общее задание (General), показываем тултип
        if (!string.IsNullOrEmpty(gameName))
        {
            tooltipText.text = gameName;
            tooltipPanel.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Прячем тултип, когда убрали мышку
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    private void OnDisable()
    {
        // Защита: прячем тултип, если сама плашка исчезла/выключилась
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }
}