using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

public class YukonTableauPile : TableauPile
{
    private YukonModeManager yukonMode;

    private void Start()
    {
        yukonMode = FindObjectOfType<YukonModeManager>();

        var animService = FindObjectOfType<AnimationService>();
        var type = typeof(TableauPile);

        var fieldAnim = type.GetField("animationService", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldAnim != null && animService != null) fieldAnim.SetValue(this, animService);

        // --- ИСПРАВЛЕНИЕ ЗАЗОРА: Передаем RectTransform в базовый класс ---
        // Если rect = null, TableauPile считает, что места мало, и сжимает карты.
        var fieldRect = type.GetField("rect", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldRect != null) fieldRect.SetValue(this, GetComponent<RectTransform>());
        // ------------------------------------------------------------------

        if (GetComponent<CanvasGroup>() == null) gameObject.AddComponent<CanvasGroup>();

        var fieldLocked = type.GetField("isLayoutLocked", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldLocked != null) fieldLocked.SetValue(this, false);
    }

    public override bool CanAccept(CardController card)
    {
        if (card == null) return false;

        if (cards.Count == 0) return card.cardModel.rank == 13;

        CardController topCard = cards[cards.Count - 1];

        if (topCard.cardModel.rank != card.cardModel.rank + 1) return false;

        if (yukonMode != null && yukonMode.CurrentVariant == YukonVariant.Russian)
        {
            return topCard.cardModel.suit == card.cardModel.suit;
        }
        else
        {
            return IsRed(topCard.cardModel) != IsRed(card.cardModel);
        }
    }

    public override void AcceptCard(CardController card)
    {
        List<CardController> allMovedCards = new List<CardController>();
        allMovedCards.Add(card);

        foreach (Transform child in card.transform)
        {
            var cc = child.GetComponent<CardController>();
            if (cc != null) allMovedCards.Add(cc);
        }

        foreach (var c in allMovedCards)
        {
            bool isFaceUp = true;
            if (c is YukonCardController ycc)
            {
                isFaceUp = ycc.IsFaceUp;
            }
            else
            {
                var data = c.GetComponent<CardData>();
                if (data != null) isFaceUp = data.IsFaceUp();
            }

            base.AddCard(c, isFaceUp);
        }

        StartLayoutAnimationPublic();
    }

    public Vector3 GetNextCardWorldPosition()
    {
        if (cards.Count == 0) return transform.position;

        CardController last = cards[cards.Count - 1];

        bool isFaceUp = faceUp.Count > cards.Count - 1 ? faceUp[cards.Count - 1] : true;
        float gap = isFaceUp ? 35f : 10f;

        Vector3 lastPos = last.transform.position;
        float scaleY = transform.lossyScale.y;

        return new Vector3(lastPos.x, lastPos.y - (gap * scaleY), lastPos.z - 0.01f);
    }

    private bool IsRed(CardModel model)
    {
        return model.suit == Suit.Diamonds || model.suit == Suit.Hearts;
    }
}