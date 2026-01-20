using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class OctagonStockPile : MonoBehaviour, ICardContainer, IPointerClickHandler
{
    private OctagonModeManager _manager;
    public int CardCount => transform.childCount;

    private void Start()
    {
        _manager = FindObjectOfType<OctagonModeManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_manager != null && _manager.IsInputAllowed)
        {
            _manager.OnStockClicked();
        }
    }

    public CardController PeekTopCard()
    {
        if (transform.childCount == 0) return null;
        return transform.GetChild(transform.childCount - 1).GetComponent<CardController>();
    }

    public List<CardController> DrawCards(int count)
    {
        List<CardController> drawn = new List<CardController>();
        for (int i = 0; i < count; i++)
        {
            if (transform.childCount == 0) break;
            
            Transform t = transform.GetChild(transform.childCount - 1);
            CardController c = t.GetComponent<CardController>();
            if (c != null)
            {
                drawn.Add(c);
                // ВАЖНО: Мы НЕ делаем SetParent(null) здесь.
                // Карта остается ребенком Stock, пока AnimationService не заберет её.
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
    public void AcceptCard(CardController card) { }
    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
}