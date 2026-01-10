using System.Collections.Generic;

public class PyramidMoveRecord
{
    public enum MoveType
    {
        Deal,           // Обычная раздача (Stock -> Waste)
        RemovePair,     // Удаление пары
        RemoveKing,     // Удаление короля
        Recycle         // Пересдача (Waste -> Stock)
    }

    public MoveType Type;

    // Для Deal
    public CardController DealtCard;

    // Для Remove
    public List<RemovedCardInfo> RemovedCards = new List<RemovedCardInfo>();

    // Для Recycle (список карт, которые вернулись в колоду)
    public List<CardController> RecycledCards = new List<CardController>();

    public struct RemovedCardInfo
    {
        public CardController Card;
        public PyramidTableauSlot SourceSlot;
        public bool WasInStock;
        public bool WasInWaste;
    }
}