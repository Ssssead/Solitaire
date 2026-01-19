using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OctagonDeckManager : MonoBehaviour
{
    [Header("References")]
    public OctagonModeManager modeManager;
    public OctagonPileManager pileManager;
    public CardFactory cardFactory;

    [Header("Settings")]
    [SerializeField] private float dealDelay = 0.05f;

    public void ApplyDeal(Deal deal)
    {
        StartCoroutine(DealRoutine(deal));
    }

    private IEnumerator DealRoutine(Deal deal)
    {
        ClearBoard();
        SpawnFixedAces();

        // --- РАЗДАЧА В TABLEAU ---
        int groupsCount = Mathf.Min(4, deal.tableau.Count);

        for (int g = 0; g < groupsCount; g++)
        {
            var groupData = deal.tableau[g]; // Список карт (из JSON)
            var targetGroup = pileManager.TableauGroups[g];

            // В JSON 0-й элемент - это НИЖНЯЯ карта (Slot 4), 
            // Последний элемент - это ВЕРХНЯЯ карта (Slot 0).
            // Но в слотах у нас Slot 0 - это верх.
            // Значит JSON[0] -> Slot 4, JSON[1] -> Slot 3...

            int maxSlots = targetGroup.Slots.Count; // 5

            for (int i = 0; i < groupData.Count; i++)
            {
                if (i >= maxSlots) break;

                // Вычисляем целевой слот: (5 - 1) - 0 = 4. 
                int slotIndex = (maxSlots - 1) - i;
                if (slotIndex < 0) break;

                var cardInstance = groupData[i];
                var slot = targetGroup.Slots[slotIndex];

                // Создаем карту
                CardController card = CreateOctagonCard(cardInstance.Card, cardInstance.FaceUp);

                if (card != null)
                {
                    card.transform.SetParent(slot.transform);
                    card.rectTransform.anchoredPosition = Vector2.zero;
                }
                yield return new WaitForSeconds(dealDelay);
            }
        }

        // --- STOCK ---
        foreach (var cardInstance in deal.stock)
        {
            CardController card = CreateOctagonCard(cardInstance.Card, false);
            if (card != null) pileManager.StockPile.AddCard(card);
        }

        modeManager.IsInputAllowed = true;
    }

    private void SpawnFixedAces()
    {
        if (pileManager.FoundationPiles.Count < 8) return;
        CreateAce(Suit.Spades, pileManager.FoundationPiles[0]);
        CreateAce(Suit.Spades, pileManager.FoundationPiles[1]);
        CreateAce(Suit.Clubs, pileManager.FoundationPiles[2]);
        CreateAce(Suit.Clubs, pileManager.FoundationPiles[3]);
        CreateAce(Suit.Diamonds, pileManager.FoundationPiles[4]);
        CreateAce(Suit.Diamonds, pileManager.FoundationPiles[5]);
        CreateAce(Suit.Hearts, pileManager.FoundationPiles[6]);
        CreateAce(Suit.Hearts, pileManager.FoundationPiles[7]);
    }

    private void CreateAce(Suit suit, ICardContainer container)
    {
        var card = CreateOctagonCard(new CardModel(suit, 1), true);
        if (card != null) container.AcceptCard(card);
    }

    public CardController CreateOctagonCard(CardModel model, bool faceUp)
    {
        if (cardFactory == null) return null;
        var cardObj = cardFactory.CreateCard(model, modeManager.DragLayer, Vector2.zero);
        if (cardObj == null) return null;

        var oldCtrl = cardObj.GetComponent<CardController>();
        if (oldCtrl != null && !(oldCtrl is OctagonCardController)) DestroyImmediate(oldCtrl);

        var newCtrl = cardObj.GetComponent<OctagonCardController>();
        if (newCtrl == null) newCtrl = cardObj.gameObject.AddComponent<OctagonCardController>();

        newCtrl.cardModel = model;
        newCtrl.canvas = modeManager.RootCanvas;
        newCtrl.CardmodeManager = modeManager;

        var data = cardObj.GetComponent<CardData>();
        if (data != null) data.SetFaceUp(faceUp, true);

        return newCtrl;
    }

    public void ClearBoard()
    {
        foreach (Transform t in pileManager.StockPile.transform) Destroy(t.gameObject);
        foreach (Transform t in pileManager.WastePile.transform) Destroy(t.gameObject);
        foreach (var p in pileManager.FoundationPiles) foreach (Transform t in p.transform) Destroy(t.gameObject);
        foreach (var group in pileManager.TableauGroups)
        {
            foreach (var slot in group.Slots) foreach (Transform t in slot.transform) Destroy(t.gameObject);
        }
    }
}