using UnityEngine;

public class PyramidTableauSlot : MonoBehaviour
{
    public int Row;
    public int Col;
    public CardController Card;

    public PyramidTableauSlot LeftChild;
    public PyramidTableauSlot RightChild;

    public bool IsBlocked()
    {
        // Заблокирован, если есть карта в ЛЮБОМ из детей
        bool leftHasCard = LeftChild != null && LeftChild.Card != null;
        bool rightHasCard = RightChild != null && RightChild.Card != null;
        return leftHasCard || rightHasCard;
    }
}