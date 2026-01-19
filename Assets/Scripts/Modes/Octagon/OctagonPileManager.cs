using System.Collections.Generic;
using UnityEngine;

public class OctagonPileManager : MonoBehaviour
{
    public OctagonStockPile StockPile;
    public OctagonWastePile WastePile;

    [Header("Groups")]
    public List<OctagonTableauGroup> TableauGroups; // 4 Группы

    public List<OctagonFoundationPile> FoundationPiles;

    public List<ICardContainer> GetAllContainers()
    {
        // DragManager'у не обязательно знать про этот список, если мы используем свою логику FindNearest
        return null;
    }
}