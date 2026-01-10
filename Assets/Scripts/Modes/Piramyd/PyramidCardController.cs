// ¬ PyramidCardController.cs
using UnityEngine.EventSystems;

public class PyramidCardController : CardController, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // явна€ реализаци€ "скрывает" реализацию базы дл€ системы событий Unity

    public new void OnBeginDrag(PointerEventData eventData) { }
    public new void OnDrag(PointerEventData eventData) { }
    public new void OnEndDrag(PointerEventData eventData) { }
}