using System.Collections.Generic;
using UnityEngine;

public class PyramidWastePile : MonoBehaviour
{
    private List<CardController> cards = new List<CardController>();

    public void Add(CardController c)
    {
        c.transform.SetParent(transform);
        c.transform.localPosition = Vector3.zero;
        c.transform.SetAsLastSibling();
        cards.Add(c);
    }

    public void Remove(CardController c)
    {
        if (cards.Contains(c)) cards.Remove(c);
    }

    public List<CardController> DrawAll()
    {
        var temp = new List<CardController>(cards);
        cards.Clear();
        return temp;
    }

    public CardController TopCard() => cards.Count > 0 ? cards[cards.Count - 1] : null;
    public bool HasCard(CardController c) => cards.Contains(c);
    public List<CardController> GetCards() => cards;
    public void Clear() => cards.Clear();
}