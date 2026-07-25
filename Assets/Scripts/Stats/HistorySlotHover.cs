using UnityEngine;
using UnityEngine.EventSystems;

public class HistorySlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Перечисление типов слотов
    public enum SlotType
    {
        Difficulty, // Обычная сложность (Easy/Medium/Hard) -> НИЧЕГО не показываем
        GameGlobal, // Глобальная статистика конкретной игры (Klondike Global)
        AppGlobal   // Общая статистика приложения (Total) 
    }

    private GameHistoryEntry myData;
    private SlotType myType = SlotType.Difficulty;

    // Метод настройки: принимает и данные, и тип слота
    public void Setup(GameHistoryEntry data, SlotType type)
    {
        myData = data;
        myType = type;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Если данных нет, ничего не показываем
        if (myData == null) return;

        // Логика в зависимости от типа
        switch (myType)
        {
            case SlotType.Difficulty:
                // Для сложностей ничего не делаем (панель не нужна)
                break;

            case SlotType.GameGlobal:
            case SlotType.AppGlobal:
                // Передаем и данные, и ТИП слота, чтобы тултип сам решил, что показывать!
                if (GameInfoTooltip.Instance != null)
                {
                    GameInfoTooltip.Instance.ShowTooltip(myData, myType);
                }
                break;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Скрываем тултип
        if (GameInfoTooltip.Instance != null)
        {
            GameInfoTooltip.Instance.HideTooltip();
        }
    }
}