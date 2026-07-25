using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PyramidDeckManager : MonoBehaviour
{
    public PyramidModeManager modeManager;
    public PyramidPileManager pileManager;
    public CardFactory cardFactory;
    public PyramidAnimationManager animManager;

    [Header("Tableau & Piles")]
    public List<Transform> rowParents;
    public Transform stockRoot;
    public Transform wasteRoot;

    [Header("Foundations")]
    public Transform leftFoundation;
    public Transform rightFoundation;

    private void Awake()
    {
        if (!cardFactory) cardFactory = FindObjectOfType<CardFactory>();
        if (!animManager) animManager = FindObjectOfType<PyramidAnimationManager>();

        if (pileManager.Stock == null) pileManager.Stock = stockRoot.gameObject.AddComponent<PyramidStockPile>();
        if (pileManager.Waste == null) pileManager.Waste = wasteRoot.gameObject.AddComponent<PyramidWastePile>();

        pileManager.Initialize(rowParents);
    }

    public List<CardController> InstantiateDeal(Deal deal)
    {
        ClearBoard();
        List<CardController> cardsToAnimate = new List<CardController>();

        // 1. Пирамида
        for (int r = 0; r < deal.tableau.Count; r++)
        {
            var rowData = deal.tableau[r];
            for (int c = 0; c < rowData.Count; c++)
            {
                var instance = rowData[c];
                var slot = pileManager.TableauSlots.Find(s => s.Row == r && s.Col == c);

                if (slot)
                {
                    var cardObj = cardFactory.CreateCard(instance.Card, stockRoot, Vector2.zero);
                    cardObj.CardmodeManager = modeManager;
                    cardObj.OnClicked += modeManager.OnCardClicked;

                    var data = cardObj.GetComponent<CardData>();
                    if (data)
                    {
                        data.SetFaceUp(true, false);
                        if (data.image) data.image.color = Color.white;
                    }

                    slot.Card = cardObj;
                    var info = cardObj.gameObject.AddComponent<CardInfoStorage>();
                    info.LinkedSlot = slot.transform;

                    cardsToAnimate.Add(cardObj);
                }
            }
        }

        // 2. Сток
        var stockList = deal.stock.ToList();
        stockList.Reverse();

        foreach (var instance in stockList)
        {
            var cardObj = cardFactory.CreateCard(instance.Card, stockRoot, Vector2.zero);
            cardObj.CardmodeManager = modeManager;
            cardObj.OnClicked += modeManager.OnStockClicked;

            var data = cardObj.GetComponent<CardData>();
            if (data)
            {
                data.SetFaceUp(instance.FaceUp, false);
                if (data.image) data.image.color = Color.white;
            }

            pileManager.Stock.Add(cardObj);
        }

        return cardsToAnimate;
    }

    public void ClearBoard()
    {
        foreach (var slot in pileManager.TableauSlots) { if (slot.Card) Destroy(slot.Card.gameObject); slot.Card = null; }
        foreach (Transform t in stockRoot) Destroy(t.gameObject);
        pileManager.Stock.Clear();
        foreach (Transform t in wasteRoot) Destroy(t.gameObject);
        pileManager.Waste.Clear();
        if (leftFoundation) foreach (Transform t in leftFoundation) Destroy(t.gameObject);
        if (rightFoundation) foreach (Transform t in rightFoundation) Destroy(t.gameObject);
    }
}