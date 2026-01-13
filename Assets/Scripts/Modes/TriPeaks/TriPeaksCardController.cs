using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TriPeaksCardController : CardController, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private TriPeaksModeManager _modeManager;

    private void Start()
    {
        _modeManager = FindObjectOfType<TriPeaksModeManager>();

        // Очистка от LayoutElement (возвращаемся к нативному поведению как в Klondike)
        LayoutElement le = GetComponent<LayoutElement>();
        if (le != null) Destroy(le);

        // КРИТИЧНО: Страховка времени анимации.
        // Если в префабе стоит 0, анимации не будет. Мы ставим дефолт.
        var data = GetComponent<CardData>();
        if (data != null && data.flipDuration < 0.1f)
        {
            data.flipDuration = 0.22f;
        }
    }

    public void Configure(CardModel model)
    {
        // Просто запоминаем модель.
        // Мы НЕ вызываем data.SetModel(), так как CardFactory уже сделала это
        // и назначила правильный спрайт.
        this.cardModel = model;
        this.name = $"Card_{model.suit}_{model.rank}";

        UpdateVisualState();
    }

    public void UpdateVisualState()
    {
        // Место для затемнения (Tint)
    }

    // Блокировка Drag & Drop (как в Pyramid)
    public new void OnBeginDrag(PointerEventData eventData) { }
    public new void OnDrag(PointerEventData eventData) { }
    public new void OnEndDrag(PointerEventData eventData) { }
}