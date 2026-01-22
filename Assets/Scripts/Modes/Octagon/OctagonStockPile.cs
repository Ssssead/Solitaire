using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class OctagonStockPile : MonoBehaviour, ICardContainer, IPointerClickHandler
{
    private OctagonModeManager _mode;
    public int CardCount => transform.childCount;

    private void Start()
    {
        _mode = FindObjectOfType<OctagonModeManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_mode != null && _mode.IsInputAllowed)
        {
            _mode.OnStockClicked();
        }
    }

    public CardController PeekTopCard()
    {
        if (transform.childCount == 0) return null;
        return transform.GetChild(transform.childCount - 1).GetComponent<CardController>();
    }

    // --- НОВЫЙ МЕТОД: Взять и удалить из иерархии (для Refill) ---
    public CardController PopTopCard()
    {
        if (transform.childCount == 0) return null;

        Transform t = transform.GetChild(transform.childCount - 1);
        CardController c = t.GetComponent<CardController>();

        if (c != null)
        {
            // Отсоединяем, чтобы следующий вызов взял следующую карту
            // Но оставляем мировые координаты для анимации
            c.transform.SetParent(_mode.RootCanvas.transform);
        }
        return c;
    }

    // Старый метод DrawCards можно оставить или удалить, он больше не используется в новом Refill
    public List<CardController> DrawCards(int count)
    {
        List<CardController> drawn = new List<CardController>();

        // --- ИСПРАВЛЕНИЕ: Берем карты со смещением ---
        for (int i = 0; i < count; i++)
        {
            // Нам нужно брать с конца, но с отступом i
            // i=0 -> Самая верхняя (индекс Count-1)
            // i=1 -> Предпоследняя (индекс Count-2)
            int index = transform.childCount - 1 - i;

            // Если карт не хватает, прерываем
            if (index < 0) break;

            Transform t = transform.GetChild(index);
            CardController c = t.GetComponent<CardController>();
            if (c != null)
            {
                drawn.Add(c);
            }
        }
        return drawn;
    }

    public void AddCard(CardController card)
    {
        card.transform.SetParent(transform);
        card.rectTransform.anchoredPosition = Vector2.zero;

        var data = card.GetComponent<CardData>();
        if (data) data.SetFaceUp(false, false);

        var cg = card.GetComponent<CanvasGroup>();
        if (cg) cg.blocksRaycasts = false;
    }

    public Transform Transform => transform;
    public bool CanAccept(CardController card) => false;
    public void AcceptCard(CardController card) => AddCard(card);
    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
}