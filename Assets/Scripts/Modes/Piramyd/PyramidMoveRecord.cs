using System.Collections.Generic;

public class PyramidMoveRecord
{
    public enum MoveType
    {
        Deal,
        RemovePair,
        RemoveKing,
        Recycle
    }

    public MoveType Type;

    public CardController DealtCard;
    public List<CardController> RecycledCards = new List<CardController>();
    public List<RemovedCardInfo> RemovedCards = new List<RemovedCardInfo>();

    public struct RemovedCardInfo
    {
        public CardController Card;
        public PyramidTableauSlot SourceSlot;
        public bool WasInStock;
        public bool WasInWaste;

        // Новое поле: запоминаем, в какой фундамент улетела карта
        public bool WentToLeftFoundation;
    }
}