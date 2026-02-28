using System.Collections.Generic;
using UnityEngine;

public class MonteCarloPileManager : MonoBehaviour
{
    [Header("Slots")]
    public Transform StockRoot;
    public Transform FoundationRoot;
    public List<Transform> TableauSlots = new List<Transform>(25);

    public CardController[] BoardCards = new CardController[25];
    public List<CardController> StockCards = new List<CardController>();
    public List<CardController> FoundationCards = new List<CardController>();

    public int GetCardIndex(CardController card)
    {
        for (int i = 0; i < 25; i++)
        {
            if (BoardCards[i] == card) return i;
        }
        return -1;
    }

    public void ClearAll()
    {
        for (int i = 0; i < 25; i++) BoardCards[i] = null;
        StockCards.Clear();
        FoundationCards.Clear();
    }

    // --- НОВЫЙ МЕТОД: Контроль наложения теней ---
    public void UpdateShadows()
    {
        // 1. Карты на столе: тень есть у всех
        foreach (var card in BoardCards)
        {
            if (card != null)
            {
                var sh = card.GetComponent<CardShadowController>();
                if (sh) sh.SetShadowVisible(true);
            }
        }

        // 2. Колода (Stock): тень ТОЛЬКО у самой нижней карты (индекс 0)
        for (int i = 0; i < StockCards.Count; i++)
        {
            var sh = StockCards[i].GetComponent<CardShadowController>();
            if (sh) sh.SetShadowVisible(i == 0);
        }

        // 3. Фундамент (Foundation): тень ТОЛЬКО у самой нижней карты (индекс 0)
        for (int i = 0; i < FoundationCards.Count; i++)
        {
            var sh = FoundationCards[i].GetComponent<CardShadowController>();
            if (sh) sh.SetShadowVisible(i == 0);
        }
    }
}