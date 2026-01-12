using UnityEngine;
using UnityEngine.EventSystems;

public class TriPeaksCardController : CardController, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // TriPeaks - игра на кликах. Мы намеренно "скрываем" (new) реализацию Drag-интерфейсов,
    // чтобы Unity EventSystem не пыталась таскать карту.

    public new void OnBeginDrag(PointerEventData eventData)
    {
        // Drag запрещен. Можно добавить визуальный эффект (например, легкое покачивание "нельзя взять")
    }

    public new void OnDrag(PointerEventData eventData)
    {
        // Пусто
    }

    public new void OnEndDrag(PointerEventData eventData)
    {
        // Пусто
    }

    // Обработка клика остается в базовом классе (CardController.OnPointerClick), 
    // который вызывает событие OnClicked.
}