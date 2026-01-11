using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PyramidPileManager : MonoBehaviour
{
    public PyramidStockPile Stock;
    public PyramidWastePile Waste;
    public List<PyramidTableauSlot> TableauSlots = new List<PyramidTableauSlot>();

    // Флаги очищенных рядов (0 - верхушка, 6 - низ)
    private bool[] rowClearedFlags = new bool[7];

    private Dictionary<CardController, Coroutine> colorRoutines = new Dictionary<CardController, Coroutine>();

    public void Initialize(List<Transform> rows)
    {
        TableauSlots.Clear();
        ResetRowFlags(); // Используем метод сброса

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

    // --- НОВЫЙ МЕТОД: Сброс флагов рядов (вызывать при новом раунде) ---
    public void ResetRowFlags()
    {
        for (int i = 0; i < rowClearedFlags.Length; i++)
            rowClearedFlags[i] = false;
    }
    // -------------------------------------------------------------------

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
                SetCardColorSmoothly(slot.Card, blocked ? new Color(0.6f, 0.6f, 0.6f) : Color.white);
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

    private void SetCardColorSmoothly(CardController card, Color targetColor)
    {
        var data = card.GetComponent<CardData>();
        if (data == null || data.image == null) return;
        if (IsColorClose(data.image.color, targetColor)) return;

        if (colorRoutines.ContainsKey(card))
        {
            if (colorRoutines[card] != null) StopCoroutine(colorRoutines[card]);
            colorRoutines.Remove(card);
        }
        Coroutine routine = StartCoroutine(FadeColorRoutine(data, targetColor));
        colorRoutines[card] = routine;
    }

    private IEnumerator FadeColorRoutine(CardData data, Color target)
    {
        float duration = 0.3f; float elapsed = 0f; Color start = data.image.color;
        while (elapsed < duration)
        {
            if (data == null || data.image == null) yield break;
            elapsed += Time.deltaTime;
            data.image.color = Color.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        if (data != null && data.image != null) data.image.color = target;
        var controller = data.GetComponent<CardController>();
        if (controller != null && colorRoutines.ContainsKey(controller)) colorRoutines.Remove(controller);
    }

    private bool IsColorClose(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

    public void RemoveCardFromSystem(CardController card)
    {
        foreach (var slot in TableauSlots)
        {
            if (slot.Card == card)
            {
                slot.Card = null;
                if (colorRoutines.ContainsKey(card)) colorRoutines.Remove(card);
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