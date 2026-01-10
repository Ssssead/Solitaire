using UnityEngine;
using UnityEngine.UI;

public class FreeCellPile : MonoBehaviour, ICardContainer
{
    // Храним ссылку на текущую карту явно (как Foundation хранит список)
    // Это надежнее, чем каждый раз перебирать детей
    private CardController _currentCard;

    public Transform Transform => transform;
    public bool IsEmpty => _currentCard == null;

    private void Start()
    {
        // При старте проверяем, есть ли уже карта внутри (например, при загрузке)
        RefreshState();
    }

    // Этот метод Unity вызывает АВТОМАТИЧЕСКИ, когда карта добавляется или убирается из слота.
    // Это гарантирует, что переменная _currentCard всегда актуальна.
    private void OnTransformChildrenChanged()
    {
        RefreshState();
    }

    private void RefreshState()
    {
        _currentCard = null;

        // Ищем активную карту среди детей
        foreach (Transform child in transform)
        {
            // Игнорируем выключенные объекты (тени, плейсхолдеры)
            if (!child.gameObject.activeSelf) continue;

            CardController card = child.GetComponent<CardController>();
            if (card != null)
            {
                _currentCard = card;
                break; // Нашли карту — слот занят
            }
        }
    }

    // --- ICardContainer Implementation ---

    public bool CanAccept(CardController card)
    {
        // 1. Если слот уже занят — отказ
        if (_currentCard != null)
        {
            // Важный нюанс: если мы тащим карту, которая УЖЕ в этом слоте (подняли и опустили)
            // мы должны разрешить ей "вернуться", но CanAccept обычно вызывается для чужих карт.
            if (_currentCard == card) return true;

            return false;
        }

        // 2. Если слот пуст — принимаем ЛЮБУЮ карту
        return true;
    }

    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        // Логика как в FoundationPile:
        // 1. Ставим родителем этот слот
        card.rectTransform.SetParent(transform, false); // false = сохранить local scale/rotation

        // 2. Центруем
        card.rectTransform.anchoredPosition = Vector2.zero;

        // 3. Обновляем ссылку (хотя OnTransformChildrenChanged тоже сработает)
        _currentCard = card;

        // 4. Гарантируем, что карта сверху (на случай фона)
        card.transform.SetAsLastSibling();
    }

    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        return Vector2.zero;
    }

    public void OnCardIncoming(CardController card)
    {
        // Здесь можно добавить подсветку, как в Foundation
    }
}