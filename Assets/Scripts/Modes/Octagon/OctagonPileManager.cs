using System.Collections.Generic;
using UnityEngine;

// 1. Наследуем от базового PileManager (как во FreeCell)
public class OctagonPileManager : PileManager
{
    public OctagonStockPile StockPile;
    public OctagonWastePile WastePile;

    [Header("Groups")]
    public List<OctagonTableauGroup> TableauGroups;

    public List<OctagonFoundationPile> FoundationPiles;

    // 2. Переопределяем метод для SceneExitAnimator
    public override List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();

        if (StockPile != null) list.Add(StockPile);
        if (WastePile != null) list.Add(WastePile);
        if (FoundationPiles != null) list.AddRange(FoundationPiles);

        if (TableauGroups != null)
        {
            foreach (var group in TableauGroups)
            {
                if (group != null)
                {
                    // Добавляем саму группу (чтобы аниматор растворил фиолетовый фон)
                    list.Add(group);

                    // Добавляем сами слоты
                    if (group.Slots != null) list.AddRange(group.Slots);
                }
            }
        }

        return list;
    }

    #region Intro Visual Control

    public void SetAllSlotsAlpha(float alpha)
    {
        foreach (var f in FoundationPiles) SetGroupAlpha(f.transform, alpha);

        foreach (var group in TableauGroups)
        {
            SetGroupAlpha(group.transform, alpha);
            foreach (var slot in group.Slots) SetGroupAlpha(slot.transform, alpha);
        }

        if (StockPile != null) SetGroupAlpha(StockPile.transform, alpha);
        if (WastePile != null) SetGroupAlpha(WastePile.transform, alpha);
    }

    private void SetGroupAlpha(Transform targetTransform, float alpha)
    {
        if (targetTransform == null) return;

        CanvasGroup cg = targetTransform.GetComponent<CanvasGroup>();
        if (cg == null) cg = targetTransform.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = alpha;
    }

    #endregion
}