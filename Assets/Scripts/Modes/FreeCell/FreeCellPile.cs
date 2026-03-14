using UnityEngine;
using UnityEngine.UI;

public class FreeCellPile : MonoBehaviour, ICardContainer
{
    private CardController _currentCard;

    public Transform Transform => transform;
    public bool IsEmpty => _currentCard == null;

    private void Start() { RefreshState(); }
    private void OnTransformChildrenChanged() { RefreshState(); }

    private void RefreshState()
    {
        _currentCard = null;
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;
            CardController card = child.GetComponent<CardController>();
            if (card != null) { _currentCard = card; break; }
        }
    }

    public bool CanAccept(CardController card)
    {
        var modeManager = FindObjectOfType<FreeCellModeManager>();
        if (modeManager != null && modeManager.CurrentDragCount > 1)
        {
            return false;
        }

        if (_currentCard != null)
        {
            if (_currentCard == card) return true;
            return false;
        }
        return true;
    }

    // --- ИСПРАВЛЕННЫЙ МЕТОД ---
    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        // 1. Обновляем логическую ссылку
        _currentCard = card;

        // 2. Удочеряем карту слоту, но ВАЖНО: сохраняем мировую позицию (true)!
        // Мы удалили принудительное обнуление координат (anchoredPosition = Vector2.zero).
        // Теперь скрипт анимации не будет перебиваться и плавно доставит карту в центр ячейки.
        card.rectTransform.SetParent(transform, true);

        // 3. Рисуем поверх
        card.transform.SetAsLastSibling();
    }

    public Vector2 GetDropAnchoredPosition(CardController card) { return Vector2.zero; }
    public void OnCardIncoming(CardController card) { }
}