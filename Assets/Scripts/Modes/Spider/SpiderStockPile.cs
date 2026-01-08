using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class SpiderStockPile : TableauPile, IPointerClickHandler
{
    [Header("Spider Settings")]
    public SpiderDeckManager deckManager;
    public SpiderModeManager spiderMode;

    [Header("Visual Settings")]
    [Tooltip("Отступ между ГРУППАМИ карт (по 10 штук)")]
    public float groupGap = 5f;
    [Tooltip("Сколько карт должно остаться в стоке после начальной раздачи")]
    public int cardsToKeepStacked = 50;

    private void Start()
    {
        RefreshCardsFromHierarchy();
    }

    private void OnTransformChildrenChanged()
    {
        RefreshCardsFromHierarchy();
    }

    private void RefreshCardsFromHierarchy()
    {
        cards.Clear();
        foreach (Transform child in transform)
        {
            var c = child.GetComponent<CardController>();
            if (c != null)
            {
                cards.Add(c);

                // Гарантируем, что карты в колоде закрыты
                var data = c.GetComponent<CardData>();
                if (data != null && data.IsFaceUp())
                {
                    data.SetFaceUp(false, animate: false);
                }
            }
        }
        ForceRecalculateLayout();
    }

    // --- НОВЫЙ МЕТОД ДЛЯ ИСПРАВЛЕНИЯ ОШИБКИ ---
    public int GetCardCount()
    {
        // Возвращаем количество карт в списке (наследуется от TableauPile)
        return cards.Count;
    }
    // ------------------------------------------

    public override void ForceRecalculateLayout()
    {
        int excess = Mathf.Max(0, cards.Count - cardsToKeepStacked);

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card != null)
            {
                Vector2 targetPos;

                if (i < excess)
                {
                    targetPos = Vector2.zero;
                }
                else
                {
                    int stockIndex = i - excess;
                    int groupIndex = stockIndex / 10;
                    float xPos = groupIndex * groupGap;
                    targetPos = new Vector2(xPos, 0);
                }

                card.rectTransform.anchoredPosition = targetPos;
            }
        }
    }

    private void LateUpdate()
    {
        foreach (var card in cards)
        {
            if (card != null && card.canvasGroup != null)
            {
                if (card.canvasGroup.blocksRaycasts)
                {
                    card.canvasGroup.blocksRaycasts = false;
                    card.canvasGroup.interactable = false;
                }
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (spiderMode != null && !spiderMode.IsInputAllowed) return;
        if (cards.Count < 10) return;

        if (deckManager != null)
        {
            deckManager.TryDealRow();
        }
    }

    public override void AddCard(CardController card, bool faceUp)
    {
        if (card != null && card.transform.parent != transform)
        {
            card.transform.SetParent(transform, false);
        }
    }

    public override void AddCardsBatch(List<CardController> cardsToAdd, bool faceUp)
    {
        foreach (var card in cardsToAdd)
        {
            if (card != null && card.transform.parent != transform)
            {
                card.transform.SetParent(transform, false);
            }
        }
    }

    public override bool CanAccept(CardController card) => false;
    public override List<CardController> GetFaceUpSequenceFrom(int index) => new List<CardController>();
}