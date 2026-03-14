using System.Collections.Generic;
using UnityEngine;

public class MonteCarloPileManager : PileManager
{
    [Header("Monte Carlo Slots")]
    public Transform StockRoot;
    public Transform FoundationRoot;
    public List<Transform> TableauSlots = new List<Transform>(25);

    public CardController[] BoardCards = new CardController[25];
    public List<CardController> StockCards = new List<CardController>();
    public List<CardController> FoundationCards = new List<CardController>();

    public int GetCardIndex(CardController card)
    {
        for (int i = 0; i < 25; i++)
        {
            if (BoardCards[i] == card) return i;
        }
        return -1;
    }

    public void ClearAll()
    {
        for (int i = 0; i < 25; i++) BoardCards[i] = null;
        StockCards.Clear();
        FoundationCards.Clear();
    }

    public void UpdateShadows()
    {
        foreach (var card in BoardCards)
        {
            if (card != null)
            {
                var sh = card.GetComponent<CardShadowController>();
                if (sh) sh.SetShadowVisible(true);
            }
        }

        for (int i = 0; i < StockCards.Count; i++)
        {
            var sh = StockCards[i].GetComponent<CardShadowController>();
            if (sh) sh.SetShadowVisible(i == 0);
        }

        for (int i = 0; i < FoundationCards.Count; i++)
        {
            var sh = FoundationCards[i].GetComponent<CardShadowController>();
            if (sh) sh.SetShadowVisible(i == 0);
        }
    }

    // --- Для интро анимации ---
    public void SetAllSlotsAlpha(float alpha)
    {
        SetAlpha(StockRoot, alpha);
        SetAlpha(FoundationRoot, alpha);
        foreach (var slot in TableauSlots) SetAlpha(slot, alpha);
    }

    private void SetAlpha(Transform t, float alpha)
    {
        if (t == null) return;
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = alpha;
    }

    // --- СОВМЕСТИМОСТЬ С SCENE EXIT ANIMATOR ---
    public override List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();

        // SceneExitAnimator ищет ICardContainer, берет с них CanvasGroup и плавно их скрывает.
        // Добавляем фиктивные отключенные контейнеры, чтобы аниматор их "увидел".

        foreach (var slot in TableauSlots)
        {
            if (slot == null) continue;
            var container = slot.GetComponent<TableauPile>();
            if (container == null)
            {
                container = slot.gameObject.AddComponent<TableauPile>();
                container.enabled = false; // Отключаем, чтобы не работала логика Косынки
            }
            list.Add(container);
        }

        if (StockRoot != null)
        {
            var stock = StockRoot.GetComponent<StockPile>();
            if (stock == null)
            {
                stock = StockRoot.gameObject.AddComponent<StockPile>();
                stock.enabled = false;
            }
            list.Add(stock);
        }

        if (FoundationRoot != null)
        {
            var found = FoundationRoot.GetComponent<FoundationPile>();
            if (found == null)
            {
                found = FoundationRoot.gameObject.AddComponent<FoundationPile>();
                found.enabled = false;
            }
            list.Add(found);
        }

        return list;
    }
}