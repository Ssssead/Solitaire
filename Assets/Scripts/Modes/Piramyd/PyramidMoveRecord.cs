using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// --- ОПРЕДЕЛЕНИЕ СТРУКТУРЫ ХОДА (Для Undo) ---
public class PyramidMoveRecord
{
    public enum MoveType { Deal, Recycle, RemovePair, RemoveKing }
    public MoveType Type;

    // Для Deal
    public CardController DealtCard;

    // Для Recycle
    public List<CardController> RecycledCards;

    // Для Remove
    public List<RemovedCardInfo> RemovedCards = new List<RemovedCardInfo>();

    // --- НОВОЕ: Для отмены очков ---
    public int ScoreGained;
    public List<int> ClearedRows = new List<int>(); // Список рядов, очищенных этим ходом

    public class RemovedCardInfo
    {
        public CardController Card;
        public PyramidTableauSlot SourceSlot;
        public bool WasInStock;
        public bool WasInWaste;
        public bool WentToLeftFoundation;
    }
}