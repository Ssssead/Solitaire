using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OctagonCardController : CardController
{
    private OctagonModeManager _mode;
    private Transform _originalParent;
    private int _originalIndex;

    private void Start()
    {
        _mode = FindObjectOfType<OctagonModeManager>();

        // Инициализируем компоненты, чтобы избежать ошибок базы
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (_mode != null && !_mode.IsInputAllowed) { eventData.pointerDrag = null; return; }

        // Блокировка Foundation (нельзя брать карты из баз)
        if (transform.parent != null && transform.parent.GetComponent<OctagonFoundationPile>() != null)
        {
            eventData.pointerDrag = null;
            return;
        }
        // Блокировка Stock
        if (transform.parent != null && transform.parent.GetComponent<OctagonStockPile>() != null)
        {
            eventData.pointerDrag = null;
            return;
        }
        // Блокировка закрытых карт
        var data = GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) { eventData.pointerDrag = null; return; }

        // --- НАЧАЛО ПЕРЕТАСКИВАНИЯ ---
        _originalParent = transform.parent;
        _originalIndex = transform.GetSiblingIndex();

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

        // Поднимаем карту на слой DragLayer
        if (_mode != null && _mode.DragLayer != null)
        {
            transform.SetParent(_mode.DragLayer, true);
            transform.SetAsLastSibling();
        }

        // Вызываем базу для совместимости (чтобы работали события, если они есть)
        base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        // Двигаем карту сами, если база не справляется
        if (rectTransform != null && _mode != null && _mode.RootCanvas != null)
        {
            rectTransform.anchoredPosition += eventData.delta / _mode.RootCanvas.scaleFactor;
        }
        else
        {
            base.OnDrag(eventData);
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        // Не вызываем base.OnEndDrag, чтобы полностью контролировать логику завершения
        // base.OnEndDrag(eventData); 

        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        bool success = false;

        if (_mode != null)
        {
            // 1. Спрашиваем у менеджера: "Куда я упал?"
            // Мы передаем 0 в maxDistance, так как логика внутри менеджера использует RectTransformUtility
            ICardContainer target = _mode.FindNearestContainer(this, eventData.position, 0);

            // 2. Если нашли валидный контейнер
            if (target != null)
            {
                // Кладем карту в контейнер
                target.AcceptCard(this);

                // Сообщаем менеджеру о ходе (для проверки победы)
                _mode.OnCardDroppedToContainer(this, target);

                success = true;
            }
        }

        // 3. Если не попали в контейнер — возвращаемся домой
        if (!success)
        {
            ReturnToOriginalParent();
        }
    }

    private void ReturnToOriginalParent()
    {
        if (_originalParent != null)
        {
            transform.SetParent(_originalParent);
            transform.SetSiblingIndex(_originalIndex);

            // Если вернулись в Waste, обновляем раскладку
            var waste = _originalParent.GetComponent<OctagonWastePile>();
            if (waste != null)
            {
                waste.UpdateLayout();
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }
}