using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MoveRecord
{
    public List<CardController> cards = new List<CardController>();
    public ICardContainer sourceContainer;
    public ICardContainer targetContainer;
    public List<Transform> sourceParents = new List<Transform>();
    public List<Vector3> sourceLocalPositions = new List<Vector3>();
    public List<int> sourceSiblingIndices = new List<int>();
    public List<bool> sourceFaceUpStates = new List<bool>();
    public bool sourceCardWasFlipped = false;
    public int flippedCardIndex = -1;

    // Новые поля для Spider
    public string groupID = null;
    public bool isRapidUndo = false; 

    public MoveRecord() { }

    public MoveRecord(List<CardController> movedCards,
                      ICardContainer source,
                      ICardContainer target,
                      List<Transform> parents = null,
                      List<Vector3> positions = null,
                      List<int> siblings = null)
    {
        if (movedCards != null) cards = new List<CardController>(movedCards);
        sourceContainer = source;
        targetContainer = target;
        if (parents != null) sourceParents = new List<Transform>(parents);
        if (positions != null) sourceLocalPositions = new List<Vector3>(positions);
        if (siblings != null) sourceSiblingIndices = new List<int>(siblings);
    }

    public void SaveFaceUpStates()
    {
        sourceFaceUpStates.Clear();
        foreach (var card in cards)
        {
            if (card != null)
            {
                var cardData = card.GetComponent<CardData>();
                sourceFaceUpStates.Add(cardData != null && cardData.IsFaceUp());
            }
            else sourceFaceUpStates.Add(false);
        }
    }

    public void RestoreFaceUpStates()
    {
        for (int i = 0; i < cards.Count && i < sourceFaceUpStates.Count; i++)
        {
            var card = cards[i];
            if (card != null)
            {
                var cardData = card.GetComponent<CardData>();
                if (cardData != null) cardData.SetFaceUp(sourceFaceUpStates[i], animate: false);
            }
        }
    }
    
    public override string ToString() => $"MoveRecord: {cards.Count} cards";
}