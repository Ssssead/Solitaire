using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TriPeaksCardController : CardController, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private TriPeaksModeManager _modeManager;

    private void Start()
    {
        _modeManager = FindObjectOfType<TriPeaksModeManager>();

        // 1. ИЗОЛЯЦИЯ ОТ СЛОТА (Самое важное для анимации)
        // Добавляем компонент, который говорит слоту: "Не управляй моими размерами"
        LayoutElement le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        // 2. Страховка скорости анимации
        var data = GetComponent<CardData>();
        if (data != null && data.flipDuration < 0.1f)
        {
            data.flipDuration = 0.25f;
        }
    }

    public void Configure(CardModel model)
    {
        this.cardModel = model;
        this.name = $"Card_{model.suit}_{model.rank}";

        var dataComponent = GetComponent<CardData>();
        if (dataComponent != null)
        {
            dataComponent.model = model;
            if (dataComponent.flipDuration < 0.1f) dataComponent.flipDuration = 0.25f;
        }
        UpdateVisualState();
    }

    public void UpdateVisualState() { }

    // Отключаем перетаскивание (как вы просили)
    public new void OnBeginDrag(PointerEventData eventData) { }
    public new void OnDrag(PointerEventData eventData) { }
    public new void OnEndDrag(PointerEventData eventData) { }
}