using UnityEngine;
using UnityEngine.EventSystems;

public class SultanStockPile : MonoBehaviour, ICardContainer, IPointerClickHandler
{
    public Transform Transform => transform;
    private SultanModeManager _mode;

    [Header("Visuals")]
    [Tooltip("Отступ вверх для каждой карты в пикселях RectTransform")]
    public float verticalSpacing = 1.5f; // Небольшой отступ, чтобы не "съесть" место на экране

    public void Initialize(SultanModeManager mode, RectTransform tf)
    {
        _mode = mode;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_mode != null && _mode.IsInputAllowed)
        {
            _mode.OnStockClicked();
        }
    }

    public bool CanAccept(CardController card) => false;
    public void AcceptCard(CardController card) { }

    public void AddCard(CardController card, bool faceUp)
    {
        if (card == null) return;

        card.transform.SetParent(transform);
        card.transform.localRotation = Quaternion.identity;

        var data = card.GetComponent<CardData>();
        if (data != null) data.SetFaceUp(faceUp, false);

        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = false;

        // Пересчитываем отступы для всех карт после добавления новой
        UpdateOffsets();
    }

    // Уникальный метод Султана для очистки без пересчета отступов
    public void ClearWithoutUpdate()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
    }

    // --- КЛЮЧЕВОЙ МЕТОД: РАСЧЕТ ОТСТУПОВ ---
    public void UpdateOffsets()
    {
        int count = transform.childCount;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            RectTransform rt = transform.GetChild(i) as RectTransform;
            if (rt != null)
            {
                // Позиционируем карту по центру, но смещаем вверх (позитивная ось Y)
                // на величину отступа, умноженную на индекс карты.
                rt.anchoredPosition = new Vector2(0, i * verticalSpacing);
            }
        }
    }

    // Переопределяем метод очистки базового ICardContainer
    public void Clear()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        // Не нужно обновлять отступы, так как карт нет
    }

    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
}