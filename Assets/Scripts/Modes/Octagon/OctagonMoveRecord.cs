using System.Collections.Generic;
using UnityEngine;

public class OctagonMoveRecord
{
    public struct SubMove
    {
        public CardController Card;
        public ICardContainer Source;
        public ICardContainer Target;
        public bool WasFaceUp;
        public bool WasRaycastBlocked;
    }

    public List<SubMove> SubMoves = new List<SubMove>();

    // --- НОВОЕ ПОЛЕ: Карта, которая открылась в \"Source\" после ухода верхней карты ---
    public CardController RevealedCard;

    public void AddMove(CardController card, ICardContainer source, ICardContainer target, bool wasFaceUp)
    {
        var cg = card.GetComponent<CanvasGroup>();
        SubMoves.Add(new SubMove
        {
            Card = card,
            Source = source,
            Target = target,
            WasFaceUp = wasFaceUp,
            WasRaycastBlocked = cg != null && cg.blocksRaycasts
        });
    }

    public bool IsEmpty => SubMoves.Count == 0;
}