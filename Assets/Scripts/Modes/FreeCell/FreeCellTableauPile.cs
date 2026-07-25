using UnityEngine;
using System.Reflection;

public class FreeCellTableauPile : TableauPile
{
    private FreeCellModeManager modeManager;

    private void Start()
    {
        modeManager = FindObjectOfType<FreeCellModeManager>();

        var animService = FindObjectOfType<AnimationService>();
        var type = typeof(TableauPile);

        var fieldAnim = type.GetField("animationService", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldAnim != null && animService != null) fieldAnim.SetValue(this, animService);

        if (GetComponent<CanvasGroup>() == null) gameObject.AddComponent<CanvasGroup>();

        var fieldLocked = type.GetField("isLayoutLocked", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldLocked != null) fieldLocked.SetValue(this, false);
    }

    public override bool CanAccept(CardController card)
    {
        if (card == null) return false;

        // 1. Проверяем физические правила игры (цвет и ранг)
        bool validByRules = false;
        if (cards.Count == 0)
        {
            validByRules = true;
        }
        else
        {
            CardController topCard = cards[cards.Count - 1];
            bool isColorDifferent = IsRed(topCard) != IsRed(card);
            bool isRankCorrect = topCard.cardModel.rank == card.cardModel.rank + 1;
            validByRules = isColorDifferent && isRankCorrect;
        }

        // Если масть или ранг не подходят - сразу отказываем
        if (!validByRules) return false;

        // 2. Правила соблюдены! Теперь проверяем ЛИМИТ ПЕРЕНОСА
        if (modeManager != null && modeManager.CurrentDragCount > 1)
        {
            bool isEmptyColumn = (cards.Count == 0);
            int limit = modeManager.GetMaxDragSequenceSize(isEmptyColumn);

            if (modeManager.CurrentDragCount > limit)
            {
                // Лимит превышен! Ставим флаг для скриптов перетаскивания и отказываем.
                modeManager.JustFailedDueToLimit = true;
                return false;
            }
        }

        // --- ИСПРАВЛЕНИЕ ---
        // Если мы дошли до сюда, значит конкретно этот столбец готов принять карты.
        // Очищаем ложный флаг ошибки, который мог остаться от соседнего пустого столбца!
        if (modeManager != null) modeManager.JustFailedDueToLimit = false;
        // -------------------

        return true;
    }

    private bool IsRed(CardController c)
    {
        return c.cardModel.suit == Suit.Diamonds || c.cardModel.suit == Suit.Hearts;
    }
}