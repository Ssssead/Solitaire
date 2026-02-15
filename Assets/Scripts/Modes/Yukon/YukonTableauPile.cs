using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

public class YukonTableauPile : TableauPile
{
    private YukonModeManager yukonMode;

    private void Start()
    {
        yukonMode = FindObjectOfType<YukonModeManager>();

        // --- Инициализация базового класса через Reflection ---
        var animService = FindObjectOfType<AnimationService>();
        var type = typeof(TableauPile);

        var fieldAnim = type.GetField("animationService", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldAnim != null && animService != null) fieldAnim.SetValue(this, animService);

        // Гарантируем CanvasGroup
        if (GetComponent<CanvasGroup>() == null) gameObject.AddComponent<CanvasGroup>();

        // Снимаем блокировку лейаута, если она есть
        var fieldLocked = type.GetField("isLayoutLocked", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldLocked != null) fieldLocked.SetValue(this, false);
    }

    public override bool CanAccept(CardController card)
    {
        if (card == null) return false;

        // 1. Пустая стопка принимает только Короля (13)
        if (cards.Count == 0) return card.cardModel.rank == 13;

        CardController topCard = cards[cards.Count - 1];

        // 2. Проверка ранга (на 1 меньше)
        if (topCard.cardModel.rank != card.cardModel.rank + 1) return false;

        // 3. Проверка масти/цвета
        if (yukonMode != null && yukonMode.CurrentVariant == YukonVariant.Russian)
        {
            return topCard.cardModel.suit == card.cardModel.suit;
        }
        else
        {
            return IsRed(topCard.cardModel) != IsRed(card.cardModel);
        }
    }

    // --- ИСПРАВЛЕНИЕ: Корректный прием карт с учетом FaceUp ---
    public override void AcceptCard(CardController card)
    {
        // 1. Собираем все карты, которые участвуют в переносе (сам лидер + его дети)
        List<CardController> allMovedCards = new List<CardController>();
        allMovedCards.Add(card);

        // Дети карты - это прицепленные к ней карты (Yukon stack)
        foreach (Transform child in card.transform)
        {
            var cc = child.GetComponent<CardController>();
            if (cc != null) allMovedCards.Add(cc);
        }

        // 2. Добавляем их в стопку по очереди, сохраняя их состояние FaceUp
        foreach (var c in allMovedCards)
        {
            bool isFaceUp = true;

            // Пытаемся узнать текущее состояние карты
            if (c is YukonCardController ycc)
            {
                isFaceUp = ycc.IsFaceUp;
            }
            else
            {
                var data = c.GetComponent<CardData>();
                if (data != null) isFaceUp = data.IsFaceUp();
            }

            // Вызываем базовый метод AddCard, передавая ПРАВИЛЬНОЕ состояние
            // Это обновит списки cards и faceUp в базовом классе TableauPile
            base.AddCard(c, isFaceUp);
        }

        // 3. Обновляем визуализацию
        StartLayoutAnimationPublic();
    }

    // Метод для анимации полета (используется YukonCardController)
    public Vector3 GetNextCardWorldPosition()
    {
        if (cards.Count == 0) return transform.position;

        CardController last = cards[cards.Count - 1];

        // Берем состояние из базового списка faceUp
        bool isFaceUp = faceUp.Count > cards.Count - 1 ? faceUp[cards.Count - 1] : true;
        float gap = isFaceUp ? 35f : 10f; // Можно вынести в настройки

        Vector3 lastPos = last.transform.position;
        float scaleY = transform.lossyScale.y;

        return new Vector3(lastPos.x, lastPos.y - (gap * scaleY), lastPos.z - 0.01f);
    }

    private bool IsRed(CardModel model)
    {
        return model.suit == Suit.Diamonds || model.suit == Suit.Hearts;
    }
}