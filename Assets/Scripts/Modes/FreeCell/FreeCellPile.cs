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
        var oldCard = _currentCard; // Запоминаем, кто был до обновления
        _currentCard = null;

        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;
            CardController card = child.GetComponent<CardController>();
            if (card != null) { _currentCard = card; break; }
        }

        // ---> ДОБАВИТЬ ЭТО: Отнимаем прогресс, если карту убрали через отмену хода <---
        if (oldCard != null && _currentCard == null)
        {
            var mode = FindObjectOfType<FreeCellModeManager>();
            if (mode != null && mode.undoManager != null && mode.undoManager.IsUndoing)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveToFreeCell, -1);
            }
        }
        // ------------------------------------------------------------------------------
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

    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        // 1. Бронируем слот
        _currentCard = card;

        // 2. СЧИТАЕМ РЕАЛЬНОЕ КОЛИЧЕСТВО КАРТ (ИГНОРИРУЯ СТАРЫЕ ССЫЛКИ)
        var fcManager = FindObjectOfType<FreeCellPileManager>();
        int occupied = 0;
        if (fcManager != null)
        {
            foreach (var fc in fcManager.FreeCells)
            {
                if (fc == this)
                {
                    // Эту ячейку мы только что заняли целевой картой
                    occupied++;
                }
                else
                {
                    // Проверяем физически детей в остальных ячейках
                    bool hasRealCard = false;
                    foreach (Transform child in fc.transform)
                    {
                        if (!child.gameObject.activeSelf) continue;

                        CardController c = child.GetComponent<CardController>();
                        // Если в ячейке реально лежит карта, и это НЕ та карта, которую мы сейчас переносим
                        if (c != null && c != card)
                        {
                            hasRealCard = true;
                            break;
                        }
                    }

                    if (hasRealCard) occupied++;
                }
            }
        }
        Debug.Log($"<color=cyan>[FreeCellPile]</color> Карта {card.name} летит в ячейку. Насчитали занятых: {occupied}");
        GameQuestTracker.Instance?.RecordFreeCellOccupied(occupied);

        // 3. Если карта ещё летит — физику делает SnapRoutine, выходим
        if (card.IsAnimating) return;

        // 4. Физическая привязка (если анимации нет)
        card.rectTransform.SetParent(transform, false);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.SetAsLastSibling();
    }

    public Vector2 GetDropAnchoredPosition(CardController card) { return Vector2.zero; }
    public void OnCardIncoming(CardController card) { }
}