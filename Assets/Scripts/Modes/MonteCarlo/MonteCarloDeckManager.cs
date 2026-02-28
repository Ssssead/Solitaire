using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MonteCarloDeckManager : MonoBehaviour
{
    public MonteCarloModeManager modeManager;
    public MonteCarloPileManager pileManager;
    public CardFactory cardFactory;

    [Header("Stock Visual Settings")]
    public Vector2 stockCardOffset = new Vector2(2f, 0f);

    public void InstantiateDeal(Deal deal)
    {
        ClearBoard();

        for (int i = 0; i < 25; i++)
        {
            if (i < deal.tableau.Count && deal.tableau[i].Count > 0)
            {
                var instance = deal.tableau[i][0];
                var cardObj = cardFactory.CreateCard(instance.Card, pileManager.TableauSlots[i], Vector2.zero);
                SetupCard(cardObj);
                pileManager.BoardCards[i] = cardObj;
            }
        }

        var stockList = deal.stock.ToList();
        stockList.Reverse();
        for (int i = 0; i < stockList.Count; i++)
        {
            var instance = stockList[i];
            var cardObj = cardFactory.CreateCard(instance.Card, pileManager.StockRoot, Vector2.zero);
            SetupCard(cardObj);
            cardObj.GetComponent<CardData>()?.SetFaceUp(true, false);
            pileManager.StockCards.Add(cardObj);

            cardObj.transform.localPosition = new Vector3(stockCardOffset.x * i, stockCardOffset.y * i, 0f);
        }

        // НОВОЕ: Обновляем тени после расстановки
        pileManager.UpdateShadows();
    }

    private void SetupCard(CardController cardObj)
    {
        cardObj.CardmodeManager = modeManager;
        cardObj.OnClicked += modeManager.OnCardClicked;
        var data = cardObj.GetComponent<CardData>();
        if (data)
        {
            data.SetFaceUp(true, false);
            if (data.image) data.image.color = Color.white;
        }

        // НОВОЕ: Добавляем компонент тени, если его нет
        var shadow = cardObj.GetComponent<CardShadowController>();
        if (shadow == null) shadow = cardObj.gameObject.AddComponent<CardShadowController>();
    }

    public void ClearBoard()
    {
        pileManager.ClearAll();
        if (cardFactory != null) cardFactory.DestroyAllCards();
    }
}