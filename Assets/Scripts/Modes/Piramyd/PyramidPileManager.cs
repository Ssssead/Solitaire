using System.Collections.Generic;
using UnityEngine;

public class PyramidPileManager : MonoBehaviour
{
    public PyramidStockPile Stock;
    public PyramidWastePile Waste;
    public List<PyramidTableauSlot> TableauSlots = new List<PyramidTableauSlot>();

    public void Initialize(List<Transform> rows)
    {
        TableauSlots.Clear();
        Dictionary<string, PyramidTableauSlot> map = new Dictionary<string, PyramidTableauSlot>();

        for (int r = 0; r < rows.Count; r++)
        {
            Transform rowTr = rows[r];
            for (int c = 0; c < rowTr.childCount; c++)
            {
                var slotObj = rowTr.GetChild(c);
                var slot = slotObj.GetComponent<PyramidTableauSlot>();
                if (slot == null) slot = slotObj.gameObject.AddComponent<PyramidTableauSlot>();

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

    public void UpdateLocks()
    {
        // 1. Пирамида
        foreach (var slot in TableauSlots)
        {
            if (slot.Card != null)
            {
                bool blocked = slot.IsBlocked();
                SetInteractable(slot.Card, !blocked);
            }
        }

        // 2. Сток
        Stock.UpdateInteractability(); // Важно вызывать обновление стока

        // 3. Waste (активна только верхняя)
        var wasteCards = Waste.GetCards();
        for (int i = 0; i < wasteCards.Count; i++)
        {
            bool isTop = (i == wasteCards.Count - 1);
            SetInteractable(wasteCards[i], isTop);
        }
    }

    public void RemoveCardFromSystem(CardController card)
    {
        // 1. Проверяем пирамиду
        foreach (var slot in TableauSlots)
        {
            if (slot.Card == card)
            {
                slot.Card = null;
                return; // Карта найдена и убрана ссылкой из слота
            }
        }

        // 2. Проверяем Сток (ИСПРАВЛЕНИЕ: удаляем из списка Stock, чтобы не было "фантомов")
        if (Stock.HasCard(card))
        {
            Stock.Remove(card);
            return;
        }

        // 3. Проверяем Waste
        if (Waste.HasCard(card))
        {
            Waste.Remove(card);
            return;
        }
    }

    public bool IsPyramidCleared()
    {
        foreach (var slot in TableauSlots)
            if (slot.Card != null) return false;
        return true;
    }

    private void SetInteractable(CardController cc, bool val)
    {
        if (cc.canvasGroup)
        {
            cc.canvasGroup.interactable = val;
            cc.canvasGroup.blocksRaycasts = val;
        }
        var cd = cc.GetComponent<CardData>();
        if (cd && cd.image) cd.image.color = val ? Color.white : new Color(0.6f, 0.6f, 0.6f);
    }
}