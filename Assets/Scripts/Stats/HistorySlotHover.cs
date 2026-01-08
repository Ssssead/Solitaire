using UnityEngine;
using UnityEngine.EventSystems;

public class HistorySlotHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Перечисление типов слотов
    public enum SlotType
    {
        Difficulty, // Обычная сложность (Easy/Medium/Hard) -> НИЧЕГО не показываем
        GameGlobal, // Глобальная статистика игры (Klondike Global) -> Показываем GameInfoTooltip
        AppGlobal   // Общая статистика приложения (Total) -> Показываем другую панель (в будущем)
    }

    private GameHistoryEntry myData;
    private SlotType myType = SlotType.Difficulty; // По умолчанию - сложность (без панели)

    // Обновленный метод настройки: теперь принимает и тип слота
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
                // Для глобальной статистики игры показываем текущий тултип
                if (GameInfoTooltip.Instance != null)
                {
                    GameInfoTooltip.Instance.ShowTooltip(myData);
                }
                break;

            case SlotType.AppGlobal:
                // TODO: Здесь будет вызов вашей новой панели (AppGlobalTooltip)
                // Пока можно оставить пусто или вывести лог
                // if (AppGlobalTooltip.Instance != null) AppGlobalTooltip.Instance.Show(myData);
                Debug.Log("Show App Global Tooltip (Coming Soon)");
                break;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Скрываем все возможные тултипы
        if (GameInfoTooltip.Instance != null)
        {
            GameInfoTooltip.Instance.HideTooltip();
        }

        // TODO: Скрыть AppGlobalTooltip, когда он будет готов
    }
}