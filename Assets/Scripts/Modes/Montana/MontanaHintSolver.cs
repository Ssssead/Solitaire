using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct MontanaHintMove
{
    public bool IsReshuffle;
    public CardController Card;
    public MontanaSlot SourceSlot;
    public MontanaSlot TargetSlot;
}

public class MontanaHintSolver : MonoBehaviour
{
    public IEnumerator FindPathRoutine(MontanaModeManager mode, Action<MontanaHintMove?> onComplete)
    {
        var pileManager = mode.pileManager;
        var puzzleManager = mode.puzzleManager;

        List<MontanaHintMove> possibleMoves = new List<MontanaHintMove>();
        List<CardController> unlockedCards = new List<CardController>();

        // ИСПРАВЛЕНИЕ 1: Собираем абсолютно все карты, игнорируя IsLockedCard.
        // Это позволит подсказке "вытаскивать" карты из неправильно собранных (но заблокированных) 
        // рядов игрока и переносить их на правильные места по замыслу Оракула.
        foreach (var slot in pileManager.Slots)
        {
            var card = slot.GetTopCard();
            if (card != null)
            {
                unlockedCards.Add(card);
            }
        }

        // Собираем все легальные ходы
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 14; c++)
            {
                var slot = pileManager.GetSlot(r, c);
                if (slot.GetTopCard() == null)
                {
                    if (c == 0)
                    {
                        int reqSuit = puzzleManager.RowSuitRequirements[r];
                        foreach (var card in unlockedCards)
                            if (card.cardModel.rank == 1 && (int)card.cardModel.suit == reqSuit)
                                possibleMoves.Add(new MontanaHintMove { IsReshuffle = false, Card = card, SourceSlot = card.GetComponentInParent<MontanaSlot>(), TargetSlot = slot });
                    }
                    else
                    {
                        var leftCard = pileManager.GetSlot(r, c - 1).GetTopCard();
                        if (leftCard != null && leftCard.cardModel.rank != 13)
                        {
                            Suit reqSuit = leftCard.cardModel.suit;
                            int reqRank = leftCard.cardModel.rank + 1;
                            foreach (var card in unlockedCards)
                                if (card.cardModel.suit == reqSuit && card.cardModel.rank == reqRank)
                                    possibleMoves.Add(new MontanaHintMove { IsReshuffle = false, Card = card, SourceSlot = card.GetComponentInParent<MontanaSlot>(), TargetSlot = slot });
                        }
                    }
                }
            }
        }

        yield return null;

        if (possibleMoves.Count > 0)
        {
            int[] currentBoard = GetIntBoard(pileManager);

            // ИСПРАВЛЕНИЕ 2: Инициализируем bestC отрицательным числом, 
            // так как новый score может уходить в минус при c = 0.
            int bestC = -999999;
            MontanaHintMove bestMove = possibleMoves[0];

            foreach (var move in possibleMoves)
            {
                int[] testBoard = (int[])currentBoard.Clone();
                int sIdx = move.SourceSlot.Col * 4 + move.SourceSlot.Row;
                int tIdx = move.TargetSlot.Col * 4 + move.TargetSlot.Row;

                testBoard[tIdx] = testBoard[sIdx];
                testBoard[sIdx] = 0;

                int c = puzzleManager.SimulateFast(testBoard);

                // ИСПРАВЛЕНИЕ 3: Тайбрейкер Оракула.
                // Оракул (SimulateFast и Генератор) всегда заполняет пустые слоты слева направо, сверху вниз.
                // Вычитая индекс (tIdx), мы отдаем приоритет слотам, находящимся левее и выше,
                // заставляя подсказку строго следовать гарантированному пути.
                int score = (c * 10000) - tIdx;

                if (score > bestC)
                {
                    bestC = score;
                    bestMove = move;
                }
            }

            onComplete?.Invoke(bestMove);
        }
        else if (mode.CurrentReshufflesLeft > 0)
        {
            onComplete?.Invoke(new MontanaHintMove { IsReshuffle = true });
        }
        else
        {
            onComplete?.Invoke(null);
        }
    }

    private int[] GetIntBoard(MontanaPileManager pileManager)
    {
        int[] board = new int[56];
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 14; c++)
            {
                var card = pileManager.GetSlot(r, c).GetTopCard();
                board[c * 4 + r] = card != null ? (int)card.cardModel.suit * 13 + card.cardModel.rank : 0;
            }
        }
        return board;
    }
}