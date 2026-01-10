using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PyramidDeckManager : MonoBehaviour
{
    public PyramidModeManager modeManager;
    public PyramidPileManager pileManager;
    public CardFactory cardFactory;

    public List<Transform> rowParents; // Row1...Row7
    public Transform stockRoot;
    public Transform wasteRoot;

    private void Awake()
    {
        if (!cardFactory) cardFactory = FindObjectOfType<CardFactory>();

        // Инициализация PileManager компонентами
        if (pileManager.Stock == null) pileManager.Stock = stockRoot.gameObject.AddComponent<PyramidStockPile>();
        if (pileManager.Waste == null) pileManager.Waste = wasteRoot.gameObject.AddComponent<PyramidWastePile>();

        pileManager.Initialize(rowParents);
    }

    public void InstantiateDeal(Deal deal)
    {
        ClearBoard();

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
                    var cardObj = cardFactory.CreateCard(instance.Card, slot.transform, Vector2.zero);
                    cardObj.CardmodeManager = modeManager;
                    cardObj.OnClicked += modeManager.OnCardClicked;

                    var data = cardObj.GetComponent<CardData>();
                    // Используем данные из JSON/Генератора
                    if (data) data.SetFaceUp(instance.FaceUp, false);

                    slot.Card = cardObj;
                }
            }
        }

        // 2. Сток
        // Инвертируем список, чтобы визуально верхняя карта была последней (сверху)
        var stockList = deal.stock.ToList();
        stockList.Reverse();

        foreach (var instance in stockList)
        {
            var cardObj = cardFactory.CreateCard(instance.Card, stockRoot, Vector2.zero);
            cardObj.CardmodeManager = modeManager;

            // Важный момент: подписываемся на клик, который обрабатывает ModeManager
            cardObj.OnClicked += modeManager.OnStockClicked;

            var data = cardObj.GetComponent<CardData>();
            // ИСПРАВЛЕНИЕ: Используем instance.FaceUp вместо false
            if (data) data.SetFaceUp(instance.FaceUp, false);

            pileManager.Stock.Add(cardObj);
        }

        pileManager.UpdateLocks();
    }

    public void ClearBoard()
    {
        foreach (var slot in pileManager.TableauSlots)
        {
            if (slot.Card) Destroy(slot.Card.gameObject);
            slot.Card = null;
        }
        foreach (Transform t in stockRoot) Destroy(t.gameObject);
        pileManager.Stock.Clear();

        foreach (Transform t in wasteRoot) Destroy(t.gameObject);
        pileManager.Waste.Clear();
    }
}