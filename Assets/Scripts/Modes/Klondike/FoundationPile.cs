// FoundationPile.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Фундаментная стопка (Foundation) для пасьянса Косынка.
/// Принимает карты одной масти от Туза (1) до Короля (13) по возрастанию.
/// </summary>
public class FoundationPile : MonoBehaviour, ICardContainer
{
    private List<CardController> cards = new List<CardController>();
    private KlondikeModeManager manager;
    private RectTransform rect;
    private AnimationService animationService;

    [Header("Visual Feedback")]
    [SerializeField] private bool showAcceptHighlight = false;

    public Transform Transform => transform;

    public void Initialize(KlondikeModeManager m, RectTransform tf)
    {
        manager = m;
        rect = tf ?? GetComponent<RectTransform>();

        // Получаем AnimationService для Z-сортировки
        if (manager != null)
        {
            animationService = manager.animationService ?? manager.GetComponent<AnimationService>();
        }
        if (animationService == null)
        {
            animationService = FindObjectOfType<AnimationService>();
        }
    }

    public void Clear()
    {
        // Уничтожаем визуальные объекты карт при очистке
        foreach (var card in cards)
        {
            if (card != null && card.gameObject != null)
            {
                Destroy(card.gameObject);
            }
        }
        cards.Clear();
    }

    /// <summary>
    /// Проверяет, может ли эта foundation-стопка принять карту.
    /// Правила: первая карта должна быть тузом, следующие - той же масти и на 1 ранг выше.
    /// </summary>
    public bool CanAccept(CardController card)
    {
        if (card == null) return false;

        var cardData = card.GetComponent<CardData>();
        if (cardData == null || !cardData.IsFaceUp()) return false;

        // Если foundation пуст - принимаем только туза (rank = 1)
        if (cards.Count == 0)
        {
            return cardData.model.rank == 1;
        }

        // Если не пуст - проверяем масть и ранг
        var topCard = cards[cards.Count - 1];
        var topData = topCard.GetComponent<CardData>();

        if (topData == null) return false;

        // Должна быть та же масть и ранг на 1 больше
        return (topData.model.suit == cardData.model.suit) &&
               (cardData.model.rank == topData.model.rank + 1);
    }

    public void OnCardIncoming(CardController card)
    {
        if (showAcceptHighlight && CanAccept(card))
        {
            // Здесь можно добавить визуальную подсветку
            // Например, изменить цвет фона или показать контур
        }
    }

    public void ForceUpdateFromTransform()
    {
        cards.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            var cardCtrl = child.GetComponent<CardController>();
            if (cardCtrl != null)
            {
                cards.Add(cardCtrl);
                var cardData = cardCtrl.GetComponent<CardData>();
                if (cardData != null)
                {
                    cardData.SetFaceUp(true, animate: false);
                }
            }
        }
        animationService?.ReorderContainerZ(transform);
    }


    /// <summary>
    /// Возвращает anchored position для карты, которая будет помещена в foundation.
    /// В foundation все карты накладываются друг на друга в центре.
    /// </summary>
    public Vector2 GetDropAnchoredPosition(CardController card)
    {
        return Vector2.zero; // Все карты в центре foundation
    }

    /// <summary>
    /// Принимает карту в foundation-стопку.
    /// </summary>
    public void AcceptCard(CardController card)
    {
        if (card == null) return;

        // Физика
        card.rectTransform.SetParent(transform, false);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.rectTransform.SetAsLastSibling();

        // Визуал
        var cardData = card.GetComponent<CardData>();
        if (cardData != null)
        {
            cardData.SetFaceUp(true, animate: false);
            if (manager?.cardFactory?.spriteDb != null)
            {
                var s = manager.cardFactory.spriteDb.GetSprite(cardData.model.suit, cardData.model.rank);
                if (s != null && cardData.image != null) cardData.image.sprite = s;
            }
        }

        // Логика
        if (!cards.Contains(card)) cards.Add(card);

        // КРИТИЧНОЕ ИСПРАВЛЕНИЕ: Обновляем интерактивность ВСЕХ карт
        UpdateInteractivity();

        animationService?.ReorderContainerZ(transform);
    }

    /// <summary>
    /// Логически добавляет карту в список, не меняя её позиции.
    /// Используется для AutoWin, чтобы следующая карта знала, что место уже занято.
    /// </summary>
    public void ReserveCard(CardController card)
    {
        if (card != null && !cards.Contains(card))
        {
            cards.Add(card);
            // Визуально карта еще летит, но логически она уже сверху стопки.
            // AcceptCard, вызванный в конце анимации, проигнорирует дублирование благодаря проверке Contains.
        }
    }
    /// <summary>
    /// Проверяет, завершена ли эта foundation-стопка (все 13 карт от туза до короля).
    /// </summary>
    public bool IsComplete() => (cards.Count == 13);

    /// <summary>
    /// Проверяет, принадлежит ли карта этой foundation-стопке.
    /// </summary>
    public bool ContainsCard(CardController card)
    {
        if (card == null) return false;

        // Проверяем по внутреннему списку
        bool inList = cards.Contains(card);

        // И по физическому родителю (для безопасности)
        bool isPhysicalChild = card.transform.parent == transform;

        return inList && isPhysicalChild; // оба условия должны быть выполнены
    }
    // Добавляем метод для принудительного удаления карты
    public void ForceRemove(CardController card)
    {
        if (card == null) return;

        if (cards.Contains(card)) cards.Remove(card);
        else cards.RemoveAll(c => c == card);

        // КРИТИЧНО: Обновляем состояние оставшихся карт
        UpdateInteractivity();

        // У удаленной карты включаем raycast
        if (card.canvasGroup != null)
        {
            card.canvasGroup.blocksRaycasts = true;
            card.canvasGroup.interactable = true;
        }

        animationService?.ReorderContainerZ(transform);
    }

    /// <summary>
    /// Главный фикс: проходит по всем картам и отключает Raycast у всех, кроме верхней.
    /// Это физически запрещает игроку схватить карту, которая лежит под другой.
    /// </summary>
    private void UpdateInteractivity()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;

            bool isTop = (i == cards.Count - 1);

            if (c.canvasGroup != null)
            {
                c.canvasGroup.blocksRaycasts = isTop;
                c.canvasGroup.interactable = isTop;
            }
        }
    }



    /// <summary>
    /// Проверяет, является ли карта верхней в этой стопке.
    /// </summary>

    public bool IsCardOnTop(CardController card)
    {
        if (card == null || cards.Count == 0) return false;

        // Проверяем, что карта в списке и она верхняя
        bool isTopInList = cards[cards.Count - 1] == card;

        // И что она физически находится в Foundation
        bool isPhysicalChild = card.transform.parent == transform;

        return isTopInList && isPhysicalChild;
    }

    /// <summary>
    /// Удаляет и возвращает верхнюю карту foundation-стопки (для undo или перемещения обратно в tableau).
    /// </summary>
    public CardController PopTop()
    {
        if (cards.Count == 0) return null;

        int lastIndex = cards.Count - 1;
        CardController topCard = cards[lastIndex];
        cards.RemoveAt(lastIndex);

        // КРИТИЧНО: При снятии карты нужно включить интерактивность у новой верхней карты
        UpdateInteractivity();

        // У снятой карты включаем raycast (чтобы её можно было нести)
        if (topCard.canvasGroup != null)
        {
            topCard.canvasGroup.blocksRaycasts = true;
            topCard.canvasGroup.interactable = true;
        }

        animationService?.ReorderContainerZ(transform);
        return topCard;
    }
    public bool CanRemoveCard(CardController card)
    {
        if (card == null || cards.Count == 0) return false;
        // Можно вынуть только верхнюю карту
        return cards[cards.Count - 1] == card;
    }


    /// <summary>
    /// Возвращает верхнюю карту без удаления (для проверок).
    /// </summary>
    public CardController GetTopCard()
    {
        if (cards.Count == 0) return null;
        return cards[cards.Count - 1];
    }

    /// <summary>
    /// Возвращает количество карт в стопке.
    /// </summary>
    public int Count => cards.Count;

    /// <summary>
    /// Возвращает масть этой foundation-стопки (если есть хотя бы одна карта).
    /// </summary>
    public Suit? GetSuit()
    {
        if (cards.Count == 0) return null;

        var topData = cards[cards.Count - 1].GetComponent<CardData>();
        if (topData == null) return null;

        return topData.model.suit;
    }
}