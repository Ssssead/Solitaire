using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Наследуемся от базового PileManager, чтобы SceneExitAnimator мог с ним работать
public class TriPeaksPileManager : PileManager
{
    [Header("Piles")]
    public TriPeaksStockPile Stock;
    public TriPeaksWastePile Waste;
    public List<TriPeaksTableauPile> TableauPiles; // Назначить в инспекторе (28 шт)

    public void ClearAll()
    {
        Stock.Clear();
        Waste.Clear();
        foreach (var p in TableauPiles) p.Clear();
    }

    public TriPeaksTableauPile FindSlotWithCard(CardController card)
    {
        foreach (var p in TableauPiles)
        {
            if (p.CurrentCard == card) return p;
        }
        return null;
    }

    // Метод для контроллера интро (проявление слота при старте)
    public void SetWasteSlotAlpha(float alpha)
    {
        if (Waste != null)
        {
            var img = Waste.GetComponent<Image>();
            if (img != null)
            {
                var color = img.color;
                color.a = alpha;
                img.color = color;
            }
        }
    }

    // --- ПЕРЕОПРЕДЕЛЕНИЕ ДЛЯ SCENE EXIT ANIMATOR ---
    // Этот метод вызывается аниматором при выходе из сцены для затухания слотов
    public override List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();

        if (Stock != null) list.Add(Stock);
        if (Waste != null) list.Add(Waste);

        if (TableauPiles != null)
        {
            foreach (var slot in TableauPiles)
            {
                list.Add(slot);
            }
        }

        return list;
    }
}