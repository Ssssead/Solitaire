using UnityEngine.EventSystems;

public class MonteCarloCardController : CardController
{
    // Заглушаем методы DragManager, чтобы карты нельзя было перетаскивать
    public override void OnBeginDrag(PointerEventData eventData) { }
    public override void OnDrag(PointerEventData eventData) { }
    public override void OnEndDrag(PointerEventData eventData) { }
}