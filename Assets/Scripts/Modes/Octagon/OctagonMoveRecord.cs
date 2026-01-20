using System.Collections.Generic;
using UnityEngine;

// Аналог PyramidMoveRecord, но адаптированный под цепочки событий Octagon
public class OctagonMoveRecord
{
    // Один атоммарный сдвиг карты
    public struct SubMove
    {
        public CardController Card;
        public ICardContainer Source;
        public ICardContainer Target;
        public bool WasFaceUp; // Состояние до перемещения
        public int ScoreGained; // Очки, полученные за это конкретное действие
    }

    public List<SubMove> SubMoves = new List<SubMove>();

    public void AddMove(CardController card, ICardContainer source, ICardContainer target, bool wasFaceUp, int score)
    {
        SubMoves.Add(new SubMove
        {
            Card = card,
            Source = source,
            Target = target,
            WasFaceUp = wasFaceUp,
            ScoreGained = score
        });
    }

    public bool IsEmpty => SubMoves.Count == 0;
}