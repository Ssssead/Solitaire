using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class YukonAvailabilitySolver
{
    public class AvailabilityReport
    {
        public int AverageMME;   // Среднее кол-во ходов для вскрытия (Minimum Moves to Expose)
        public int Bottlenecks;  // "Узкие горлышки" (карты, запертые в мертвые петли или требующие >3 ходов)
        public int EasyTargets;  // Колонки, которые можно вскрыть за 1 простой ход
    }

    public static AvailabilityReport EvaluateDeal(Deal deal, int variantParam)
    {
        int totalMME = 0;
        int bottlenecks = 0;
        int easyTargets = 0;
        int hiddenCols = 0;

        for (int col = 0; col < 7; col++)
        {
            if (deal.tableau[col].Count == 0) continue;

            // Находим границу между закрытыми и открытыми картами
            int faceUpStartIndex = -1;
            for (int r = 0; r < deal.tableau[col].Count; r++)
            {
                if (deal.tableau[col][r].FaceUp)
                {
                    faceUpStartIndex = r;
                    break;
                }
            }

            // Если в колонке есть закрытые карты, оцениваем сложность их вскрытия
            if (faceUpStartIndex > 0)
            {
                hiddenCols++;
                CardModel coverCard = deal.tableau[col][faceUpStartIndex].Card;

                var paths = FindPathsToMove(deal, coverCard, variantParam, new HashSet<CardModel>());

                if (paths.Count == 0)
                {
                    totalMME += 10; // Штраф за мертвый тупик
                    bottlenecks++;
                }
                else
                {
                    int minMME = paths.Min();
                    totalMME += minMME;

                    if (minMME == 1) easyTargets++;
                    else if (minMME >= 3) bottlenecks++;
                }
            }
        }

        return new AvailabilityReport
        {
            AverageMME = hiddenCols > 0 ? totalMME / hiddenCols : 0,
            Bottlenecks = bottlenecks,
            EasyTargets = easyTargets
        };
    }

    // Рекурсивный поиск: сколько ходов нужно, чтобы освободить карту
    private static List<int> FindPathsToMove(Deal deal, CardModel cardToMove, int variantParam, HashSet<CardModel> visited)
    {
        List<int> pathMMEs = new List<int>();

        if (visited.Contains(cardToMove)) return pathMMEs; // Защита от бесконечных циклов
        visited.Add(cardToMove);

        if (cardToMove.rank == 13) // Королю нужна пустая колонка
        {
            int emptyCols = deal.tableau.Count(c => c.Count == 0);
            if (emptyCols > 0) pathMMEs.Add(1);
            else pathMMEs.Add(5); // Если пустых нет, достать Короля тяжело
            return pathMMEs;
        }

        int targetRank = cardToMove.rank + 1;

        for (int c = 0; c < 7; c++)
        {
            var pile = deal.tableau[c];
            for (int r = 0; r < pile.Count; r++)
            {
                var potentialTarget = pile[r].Card;
                if (potentialTarget.rank == targetRank && IsValidSuitMatch(cardToMove, potentialTarget, variantParam))
                {
                    if (pile[r].FaceUp)
                    {
                        if (r == pile.Count - 1)
                        {
                            // Цель полностью свободна на дне стопки (1 ход)
                            pathMMEs.Add(1);
                        }
                        else
                        {
                            // Цель накрыта другой картой. Чтобы добраться до цели, надо переложить эту "крышку".
                            var blockingCard = pile[r + 1].Card;
                            var recursivePaths = FindPathsToMove(deal, blockingCard, variantParam, new HashSet<CardModel>(visited));

                            if (recursivePaths.Count > 0)
                            {
                                pathMMEs.Add(1 + recursivePaths.Min());
                            }
                        }
                    }
                    else
                    {
                        // Целевая карта лежит рубашкой вверх (Очень тяжело)
                        pathMMEs.Add(6);
                    }
                }
            }
        }

        return pathMMEs;
    }

    private static bool IsValidSuitMatch(CardModel movingCard, CardModel targetCard, int variantParam)
    {
        if (variantParam == 1) return movingCard.suit == targetCard.suit;

        bool isMovingRed = (movingCard.suit == Suit.Diamonds || movingCard.suit == Suit.Hearts);
        bool isTargetRed = (targetCard.suit == Suit.Diamonds || targetCard.suit == Suit.Hearts);
        return isMovingRed != isTargetRed;
    }
}