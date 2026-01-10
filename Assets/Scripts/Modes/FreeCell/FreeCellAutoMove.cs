using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FreeCellAutoMove : MonoBehaviour
{
    [Header("References")]
    public FreeCellModeManager modeManager;
    public FreeCellPileManager pileManager;
    public UndoManager undoManager;
    public FreeCellScoreManager scoreManager;

    [Header("Settings")]
    [SerializeField] private float delayBetweenMoves = 0.15f; // Скорость полета карт
    [SerializeField] private bool smartMove = true; // Безопасный режим (как в Windows)

    private bool isAutoMoving = false;

    // Этот метод привяжите к кнопке UI
    public void OnAutoMoveButtonClicked()
    {
        if (isAutoMoving || modeManager == null) return;
        StartCoroutine(AutoMoveRoutine());
    }

    private IEnumerator AutoMoveRoutine()
    {
        isAutoMoving = true;
        bool cardMoved;

        do
        {
            cardMoved = false;

            // 1. Проверяем FreeCells (Свободные ячейки)
            foreach (var cell in pileManager.FreeCells)
            {
                if (cell.IsEmpty) continue;

                var card = cell.GetComponentInChildren<CardController>();
                if (card != null && TryMoveToFoundation(card, cell))
                {
                    cardMoved = true;
                    yield return new WaitForSeconds(delayBetweenMoves);
                    break; // Прерываем цикл, чтобы начать проверку заново
                }
            }

            if (cardMoved) continue;

            // 2. Проверяем Tableau (Столбцы)
            foreach (var tab in pileManager.Tableau)
            {
                if (tab.cards.Count == 0) continue;

                var card = tab.cards[tab.cards.Count - 1]; // Берем верхнюю карту
                if (card != null && TryMoveToFoundation(card, tab))
                {
                    cardMoved = true;
                    yield return new WaitForSeconds(delayBetweenMoves);
                    break;
                }
            }

        } while (cardMoved); // Повторяем, пока находятся карты для переноса

        // Проверяем победу в конце серии ходов
        modeManager.CheckGameState();
        isAutoMoving = false;
    }

    private bool TryMoveToFoundation(CardController card, ICardContainer sourceContainer)
    {
        // Находим подходящий Foundation
        foreach (var foundation in pileManager.Foundations)
        {
            if (foundation.CanAccept(card))
            {
                // --- ПРОВЕРКА БЕЗОПАСНОСТИ (Smart Move) ---
                if (smartMove && !IsSafeToAutoMove(card))
                {
                    continue;
                }
                // -------------------------------------------

                PerformMove(card, sourceContainer, foundation);
                return true;
            }
        }
        return false;
    }

    private void PerformMove(CardController card, ICardContainer source, FoundationPile target)
    {
        // 1. Подготовка данных для Undo
        var prevParent = card.transform.parent;
        var prevSib = card.transform.GetSiblingIndex();

        // 2. Логическое изъятие из источника
        if (source is TableauPile tab)
        {
            int idx = tab.IndexOfCard(card);
            if (idx != -1)
            {
                tab.RemoveSequenceFrom(idx);
            }
        }

        // --- ИСПРАВЛЕНИЕ: МГНОВЕННАЯ РЕЗЕРВАЦИЯ ---
        // Сообщаем Foundation, что карта уже "как бы" там.
        // Теперь CanAccept для 2-ки вернет true, даже если Туз еще в полете.
        target.ReserveCard(card);
        // ------------------------------------------

        // 3. Анимация (Snap) 
        // ForceSnapToContainer вызовет AcceptCard в конце, но ReserveCard это не сломает
        card.ForceSnapToContainer(target);
        if (modeManager != null)
        {
            modeManager.OnMoveMade();
        }

        // 4. Очки
        if (scoreManager != null)
        {
            scoreManager.OnCardMove(source, target);
        }

        // 5. Запись в Undo
        if (undoManager != null)
        {
            undoManager.RecordMove(
                new List<CardController> { card },
                source,
                target,
                new List<Transform> { prevParent },
                new List<Vector3> { Vector3.zero },
                new List<int> { prevSib }
            );
        }
    }

    private bool IsSafeToAutoMove(CardController card)
    {
        int rank = card.cardModel.rank;
        if (rank <= 2) return true; // Тузы и двойки всегда убираем

        bool isRed = (card.cardModel.suit == Suit.Diamonds || card.cardModel.suit == Suit.Hearts);
        bool safeByStandardRules = true;

        // --- 1. СТАНДАРТНАЯ ПРОВЕРКА (SMART MOVE) ---
        // Идея: Не убирать карту X, пока карты (X-1) другого цвета не вышли.
        // Иначе некуда будет класть (X-1).
        foreach (var f in pileManager.Foundations)
        {
            if (f.Count == 0) continue;

            var topCard = f.GetTopCard();
            if (topCard == null) continue;

            bool topIsRed = (topCard.cardModel.suit == Suit.Diamonds || topCard.cardModel.suit == Suit.Hearts);

            // Если масть противоположная (Красная против Черной)
            if (topIsRed != isRed)
            {
                // Проверяем ранг противоположного цвета
                // Если он меньше чем (наш - 1), значит наша карта еще нужна на столе
                if (topCard.cardModel.rank < rank - 1)
                {
                    safeByStandardRules = false;
                    break;
                }
            }
        }

        // Если по стандартам всё ок — разрешаем
        if (safeByStandardRules) return true;


        // --- 2. НОВАЯ ЛОГИКА: "ПРОГЛЯДЫВАНИЕ ВГЛУБЬ" (LOOKAHEAD) ---
        // Если стандарт запрещает, но карта блокирует другую карту,
        // которая СРАЗУ ЖЕ может улететь в дом — разрешаем ход.

        if (card.transform.parent != null)
        {
            var tableau = card.transform.parent.GetComponent<TableauPile>();

            // Проверяем, что карта лежит в столбце (Tableau) и под ней что-то есть
            if (tableau != null && tableau.cards.Count >= 2)
            {
                // card - это верхняя карта (последняя в списке).
                // Нам нужна та, что под ней (предпоследняя).
                var cardBelow = tableau.cards[tableau.cards.Count - 2];

                // Проверяем, готова ли нижняя карта лететь в какой-нибудь Foundation
                foreach (var f in pileManager.Foundations)
                {
                    // CanAccept проверяет масть и ранг. 
                    // Если нижняя карта подходит — значит, текущая карта блокирует прогресс.
                    // Убираем блокировку!
                    if (f.CanAccept(cardBelow))
                    {
                        // Debug.Log($"SmartMove Override: Moving {card.name} because it blocks {cardBelow.name}");
                        return true;
                    }
                }
            }
        }

        return false;
    }
}