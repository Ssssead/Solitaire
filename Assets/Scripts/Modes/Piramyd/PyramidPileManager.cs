using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// --- ОБЕРТКА ДЛЯ SCENE EXIT ANIMATOR (чтобы не сломался выход из сцены) ---
public class PyramidContainerWrapper : MonoBehaviour, ICardContainer
{
    public Transform Transform => transform;
    public bool CanAccept(CardController card) => false;
    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
    public void AcceptCard(CardController card) { }
}

// --- НАСЛЕДУЕМСЯ ОТ PileManager ---
public class PyramidPileManager : PileManager
{
    public PyramidStockPile Stock;
    public PyramidWastePile Waste;
    public List<PyramidTableauSlot> TableauSlots = new List<PyramidTableauSlot>();

    // Флаги очищенных рядов (0 - верхушка, 6 - низ)
    private bool[] rowClearedFlags = new bool[7];

    // --- ПЕРЕОПРЕДЕЛЕНИЕ ДЛЯ SCENE EXIT ANIMATOR ---
    public override List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();

        void AddWrapper(Component comp)
        {
            if (comp == null) return;
            var wrapper = comp.GetComponent<PyramidContainerWrapper>();
            if (wrapper == null) wrapper = comp.gameObject.AddComponent<PyramidContainerWrapper>();
            list.Add(wrapper);
        }

        AddWrapper(Stock);
        AddWrapper(Waste);
        foreach (var slot in TableauSlots) AddWrapper(slot);

        var deckManager = FindObjectOfType<PyramidDeckManager>();
        if (deckManager != null)
        {
            AddWrapper(deckManager.stockRoot);
            AddWrapper(deckManager.wasteRoot);
            AddWrapper(deckManager.leftFoundation);
            AddWrapper(deckManager.rightFoundation);
        }

        return list;
    }

    public void Initialize(List<Transform> rows)
    {
        TableauSlots.Clear();
        ResetRowFlags();

        Dictionary<string, PyramidTableauSlot> map = new Dictionary<string, PyramidTableauSlot>();

        for (int r = 0; r < rows.Count; r++)
        {
            Transform rowTr = rows[r];
            for (int c = 0; c < rowTr.childCount; c++)
            {
                var slotObj = rowTr.GetChild(c);
                var slot = slotObj.GetComponent<PyramidTableauSlot>();
                if (slot == null) slot = slotObj.gameObject.AddComponent<PyramidTableauSlot>();

                var slotImage = slotObj.GetComponent<Image>();
                if (slotImage != null) slotImage.raycastTarget = false;

                slot.Row = r;
                slot.Col = c;
                slot.Card = null;

                TableauSlots.Add(slot);
                map[$"{r}_{c}"] = slot;
            }
        }

        foreach (var slot in TableauSlots)
        {
            string leftKey = $"{slot.Row + 1}_{slot.Col}";
            string rightKey = $"{slot.Row + 1}_{slot.Col + 1}";

            if (map.ContainsKey(leftKey)) slot.LeftChild = map[leftKey];
            if (map.ContainsKey(rightKey)) slot.RightChild = map[rightKey];
        }
    }

    public void ResetRowFlags()
    {
        for (int i = 0; i < rowClearedFlags.Length; i++)
            rowClearedFlags[i] = false;
    }

    public List<int> CheckForNewClearedRows()
    {
        List<int> justClearedRows = new List<int>();

        for (int r = 0; r < 7; r++)
        {
            if (rowClearedFlags[r]) continue;

            bool isRowEmpty = true;
            foreach (var slot in TableauSlots)
            {
                if (slot.Row == r && slot.Card != null)
                {
                    isRowEmpty = false;
                    break;
                }
            }

            if (isRowEmpty)
            {
                rowClearedFlags[r] = true;
                justClearedRows.Add(r);
            }
        }
        return justClearedRows;
    }

    public void RestoreRowFlag(int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < rowClearedFlags.Length)
        {
            rowClearedFlags[rowIndex] = false;
        }
    }

    public void UpdateLocks()
    {
        foreach (var slot in TableauSlots)
        {
            if (slot.Card != null)
            {
                bool blocked = slot.IsBlocked();
                if (slot.Card.canvasGroup)
                {
                    slot.Card.canvasGroup.interactable = !blocked;
                    slot.Card.canvasGroup.blocksRaycasts = true;
                }

                var data = slot.Card.GetComponent<CardData>();
                if (data && data.image) data.image.color = Color.white;

                // --- НОВОЕ: В пирамиде у всех карт всегда есть тень ---
                var shadow = slot.Card.GetComponent<UnityEngine.UI.Shadow>();
                if (shadow != null) shadow.enabled = true;
            }
        }

        Stock.UpdateInteractability();

        var wasteCards = Waste.GetCards();
        for (int i = 0; i < wasteCards.Count; i++)
        {
            bool isTop = (i == wasteCards.Count - 1);
            CardController card = wasteCards[i];

            if (card.canvasGroup)
            {
                card.canvasGroup.interactable = isTop;
                card.canvasGroup.blocksRaycasts = true;
            }

            var data = card.GetComponent<CardData>();
            if (data && data.image) data.image.color = Color.white;

            // --- НОВОЕ: Тень только у самой нижней карты сброса (индекс 0) ---
            var shadow = card.GetComponent<UnityEngine.UI.Shadow>();
            if (shadow != null) shadow.enabled = (i == 0);
        }
    }

    public bool HasValidMove()
    {
        List<CardController> availableTableau = new List<CardController>();
        foreach (var slot in TableauSlots)
        {
            if (slot.Card != null && !slot.IsBlocked())
            {
                if (slot.Card.cardModel.rank == 13) return true;
                availableTableau.Add(slot.Card);
            }
        }
        for (int i = 0; i < availableTableau.Count; i++)
        {
            for (int j = i + 1; j < availableTableau.Count; j++)
            {
                if (availableTableau[i].cardModel.rank + availableTableau[j].cardModel.rank == 13) return true;
            }
        }
        CardController topWaste = Waste.TopCard();
        if (topWaste != null)
        {
            if (topWaste.cardModel.rank == 13) return true;
            foreach (var tCard in availableTableau)
            {
                if (tCard.cardModel.rank + topWaste.cardModel.rank == 13) return true;
            }
        }
        return false;
    }

    public void RemoveCardFromSystem(CardController card)
    {
        foreach (var slot in TableauSlots)
        {
            if (slot.Card == card)
            {
                slot.Card = null;
                return;
            }
        }
        if (Stock.HasCard(card)) Stock.Remove(card);
        if (Waste.HasCard(card)) Waste.Remove(card);
    }

    public bool IsPyramidCleared()
    {
        foreach (var slot in TableauSlots) if (slot.Card != null) return false;
        return true;
    }
}