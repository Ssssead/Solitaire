using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class OctagonStockPile : MonoBehaviour, ICardContainer, IPointerClickHandler
{
    private OctagonModeManager _mode;

    [Header("3D Stacking Effect")]
    [Tooltip("Горизонтальный отступ между картами (положительный - вправо, отрицательный - влево)")]
    public float offsetX = 2f;
    [Tooltip("Вертикальный отступ между картами (положительный - вверх, отрицательный - вниз)")]
    public float offsetY = -2f;

    // --- ВЕРНУЛИ ВАШИ ОРИГИНАЛЬНЫЕ СВОЙСТВА И МЕТОДЫ ---
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

    public CardController PopTopCard()
    {
        if (transform.childCount == 0) return null;

        // Перебираем элементы сверху вниз
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform t = transform.GetChild(i);
            CardController c = t.GetComponent<CardController>();

            if (c != null)
            {
                if (_mode != null && _mode.RootCanvas != null)
                    c.transform.SetParent(_mode.RootCanvas.transform, true);
                else
                    c.transform.SetParent(null, true);

                return c;
            }
        }

        return null;
    }

    public List<CardController> DrawCards(int count)
    {
        List<CardController> drawn = new List<CardController>();
        for (int i = 0; i < count; i++)
        {
            int index = transform.childCount - 1 - i;
            if (index < 0) break;

            Transform t = transform.GetChild(index);
            CardController c = t.GetComponent<CardController>();
            if (c != null) drawn.Add(c);
        }
        return drawn;
    }

    // --- ОБНОВЛЕННЫЙ ADDCARD С 3D-ОТСТУПОМ ---
    public void AddCard(CardController card)
    {
        if (card == null) return;

        card.transform.SetParent(transform, true);
        card.transform.SetAsLastSibling();

        int cardIndex = transform.childCount - 1;
        if (cardIndex < 0) cardIndex = 0;

        card.transform.localPosition = new Vector3(cardIndex * offsetX, cardIndex * offsetY, 0f);
        card.transform.localRotation = Quaternion.identity;

        var data = card.GetComponent<CardData>();
        if (data) data.SetFaceUp(false, false);

        var cg = card.GetComponent<CanvasGroup>();
        // ---> ИСПРАВЛЕНО: Меняем false на true, чтобы карты ловили клики <---
        if (cg) cg.blocksRaycasts = true;
    }

    // --- ИНТЕРФЕЙС ICARDCONTAINER ---
    public Transform Transform => transform;
    public bool CanAccept(CardController card) => false;
    public void AcceptCard(CardController card) => AddCard(card);
    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
}