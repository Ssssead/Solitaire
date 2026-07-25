using System;
using System.Collections.Generic;
using System.Linq;

public static class YukonHumanSolver
{
    // Оценивает расклад с точки зрения "когнитивной вязкости" и ловушек
    // Возвращает "Оценку человеческой сложности" (Perceived Difficulty Score)
    public static int EvaluatePerceivedDifficulty(Deal deal, int variantParam)
    {
        int penaltyScore = 0;

        // 1. Иллюзия доступности: Игрок видит карту, но не замечает, что под ней погребены важные карты.
        int inversionDepth = CalculateInversionDepth(deal);
        penaltyScore += (inversionDepth * 2); // Штраф за общий шум

        // 2. Слепая зона "замурованного Короля"
        int buriedKings = CountBuriedKings(deal);
        penaltyScore += (buriedKings * 300); // Огромный когнитивный штраф за Королей на дне

        // 3. Штраф за "разбивание" собранных стопок (Эффект невозвратных затрат)
        // Человек неохотно перекладывает часть уже собранной последовательности, даже если это путь к победе.
        penaltyScore += CalculateSequenceRigidity(deal, variantParam);

        return penaltyScore;
    }

    // Оценка каждого хода глазами человека (Жадная эвристика)
    public static int ScoreMoveHumanly(Deal d, YukonSolver.MoveCommand move, int variantParam)
    {
        int score = 0;

        if (move.Type == YukonSolver.MoveType.Foundation)
        {
            // Люди любят отправлять карты в дом
            score += 1000;
        }
        else if (move.Type == YukonSolver.MoveType.RevealTableau)
        {
            // Люди любят вскрывать закрытые карты
            score += 800;
        }
        else if (move.Type == YukonSolver.MoveType.MoveTableau)
        {
            var sourceStack = d.tableau[move.FromIdx];
            int startIdx = sourceStack.Count - move.Count;

            // Если перемещение освобождает закрытую карту, которую можно будет вскрыть
            if (startIdx > 0 && !sourceStack[startIdx - 1].FaceUp)
            {
                score += 500;
            }

            // Иллюзия консолидации: перенос большого куска кажется полезным
            score += (move.Count * 50);

            // Штраф сепарации: если мы отрываем карту от уже подходящей ей по масти/цвету, 
            // человек делает это крайне неохотно.
            if (startIdx > 0 && sourceStack[startIdx - 1].FaceUp)
            {
                var cardAbove = sourceStack[startIdx - 1].Card;
                var movingCard = sourceStack[startIdx].Card;

                if (cardAbove.rank == movingCard.rank + 1)
                {
                    bool isMatch = (variantParam == 1) ?
                        (cardAbove.suit == movingCard.suit) :
                        (IsOppositeColor(cardAbove, movingCard));

                    if (isMatch)
                    {
                        score -= 600; // Мощный блок против "разрушения сделанного"
                    }
                }
            }
        }

        return score;
    }

    private static int CalculateInversionDepth(Deal d)
    {
        int depth = 0;
        for (int i = 0; i < 7; i++)
        {
            var pile = d.tableau[i];
            for (int j = 0; j < pile.Count; j++)
            {
                // Ищем только "якоря"
                if (pile[j].Card.rank <= 2) depth += (pile.Count - 1 - j);
            }
        }
        return depth;
    }

    private static int CountBuriedKings(Deal d)
    {
        int buried = 0;
        for (int i = 0; i < 7; i++)
        {
            var pile = d.tableau[i];
            int limit = Math.Min(3, pile.Count);
            for (int j = 0; j < limit; j++)
            {
                if (!pile[j].FaceUp && pile[j].Card.rank == 13) buried++;
            }
        }
        return buried;
    }

    private static int CalculateSequenceRigidity(Deal d, int variantParam)
    {
        int penalty = 0;
        for (int i = 0; i < 7; i++)
        {
            var pile = d.tableau[i];
            int consecutive = 0;

            for (int j = 1; j < pile.Count; j++)
            {
                if (!pile[j - 1].FaceUp || !pile[j].FaceUp) continue;

                var top = pile[j - 1].Card;
                var bottom = pile[j].Card;

                if (top.rank == bottom.rank + 1)
                {
                    bool match = (variantParam == 1) ? (top.suit == bottom.suit) : IsOppositeColor(top, bottom);
                    if (match) consecutive++;
                }
            }
            // Чем длиннее уже собранная открытая цепь, тем сложнее человеку психологически её разобрать
            if (consecutive > 2) penalty += (consecutive * 50);
        }
        return penalty;
    }

    private static bool IsOppositeColor(CardModel a, CardModel b)
    {
        bool rA = (a.suit == Suit.Diamonds || a.suit == Suit.Hearts);
        bool rB = (b.suit == Suit.Diamonds || b.suit == Suit.Hearts);
        return rA != rB;
    }
}