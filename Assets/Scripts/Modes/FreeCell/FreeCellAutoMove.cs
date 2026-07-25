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
    [SerializeField] private float delayBetweenMoves = 0.15f; // Начальная скорость
    [SerializeField] private bool smartMove = true;

    private bool isAutoMoving = false;

    // --- НОВОЕ: Виртуальный счетчик домов для мгновенной логики ---
    private Dictionary<Suit, int> virtualFoundationRanks = new Dictionary<Suit, int>();

    public void OnAutoMoveButtonClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        // --- ИНТЕГРАЦИЯ ТУТОРИАЛА ---
        if (modeManager != null && modeManager.Tutorial != null)
        {
            var tut = modeManager.Tutorial as FreeCellTutorialManager;
            if (tut != null && tut.IsTutorialActive)
            {
                // Передаем запрос в туториал. Менеджер сам запустит полет карт по сценарию!
                tut.IsActionAllowed(TutorialActionType.ClickAuto);
                return;
            }
        }
        // -----------------------------

        // Блокируем запуск в обычной игре, если ввод запрещен
        if (isAutoMoving || modeManager == null || !modeManager.IsInputAllowed) return;

        StartCoroutine(AutoMoveRoutine());
    }

    private IEnumerator AutoMoveRoutine()
    {
        isAutoMoving = true;
        modeManager.IsInputAllowed = false; // Жесткая блокировка стола на время полета

        // Инициализируем виртуальные дома текущим физическим состоянием стола
        InitVirtualFoundations();

        bool cardMoved;
        int movedInRow = 0;

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
                    movedInRow++;
                    yield return new WaitForSeconds(CalculateDynamicDelay(movedInRow));
                    break;
                }
            }

            if (cardMoved) continue;

            // 2. Проверяем Tableau (Столбцы)
            foreach (var tab in pileManager.Tableau)
            {
                if (tab.cards.Count == 0) continue;

                var card = tab.cards[tab.cards.Count - 1];
                if (card != null && TryMoveToFoundation(card, tab))
                {
                    cardMoved = true;
                    movedInRow++;
                    yield return new WaitForSeconds(CalculateDynamicDelay(movedInRow));
                    break;
                }
            }

        } while (cardMoved);

        modeManager.IsInputAllowed = true; // Разблокируем стол
        modeManager.CheckGameState();
        isAutoMoving = false;
    }

    private float CalculateDynamicDelay(int count)
    {
        if (count <= 5) return delayBetweenMoves;
        float reduction = (count - 5) * 0.02f;
        return Mathf.Max(0.04f, delayBetweenMoves - reduction);
    }

    private void InitVirtualFoundations()
    {
        virtualFoundationRanks.Clear();
        // Заполняем базовые значения (ноль для всех мастей)
        virtualFoundationRanks[Suit.Hearts] = 0;
        virtualFoundationRanks[Suit.Diamonds] = 0;
        virtualFoundationRanks[Suit.Spades] = 0;
        virtualFoundationRanks[Suit.Clubs] = 0;

        // Считываем реальное положение на столе ПЕРЕД началом лавины автосбора
        foreach (var f in pileManager.Foundations)
        {
            if (f.Count > 0)
            {
                var topCard = f.GetTopCard();
                if (topCard != null)
                {
                    virtualFoundationRanks[topCard.cardModel.suit] = topCard.cardModel.rank;
                }
            }
        }
    }

    private bool TryMoveToFoundation(CardController card, ICardContainer sourceContainer)
    {
        foreach (var foundation in pileManager.Foundations)
        {
            if (foundation.CanAccept(card))
            {
                if (smartMove && !IsSafeToAutoMove(card))
                {
                    continue;
                }

                PerformMove(card, sourceContainer, foundation);
                return true;
            }
        }
        return false;
    }

    private void PerformMove(CardController card, ICardContainer source, FoundationPile target)
    {
        var prevParent = card.transform.parent;
        var prevSib = card.transform.GetSiblingIndex();
        Vector3 prevPos = card.transform.localPosition;

        // <--- ГЛАВНОЕ ИСПРАВЛЕНИЕ: ОЧИСТКА ХВОСТОВ ОТ СВОБОДНОЙ ЯЧЕЙКИ --->
        // 1. Убиваем анимацию центрирования ячейки, если она еще не закончилась
        card.StopAllCoroutines();

        // 2. Удаляем временный Canvas, который мог остаться от прерванной анимации
        Canvas tempCanvas = card.GetComponent<Canvas>();
        if (tempCanvas != null) Destroy(tempCanvas);

        // 3. Гарантируем, что карта снова кликабельна
        if (card.canvasGroup != null) card.canvasGroup.blocksRaycasts = true;
        // ------------------------------------------------------------------

        if (source is TableauPile tab)
        {
            int idx = tab.IndexOfCard(card);
            if (idx != -1) tab.RemoveSequenceFrom(idx);
        }

        if (modeManager != null && modeManager.DragLayer != null)
        {
            card.rectTransform.SetParent(modeManager.DragLayer, true);
            card.rectTransform.SetAsLastSibling();
        }

        target.ReserveCard(card);
        card.ForceSnapToContainer(target);

        virtualFoundationRanks[card.cardModel.suit] = card.cardModel.rank;

        if (modeManager != null) modeManager.OnMoveMade();
        if (scoreManager != null) scoreManager.OnCardMove(source, target);

        if (undoManager != null)
        {
            undoManager.RecordMove(
                new List<CardController> { card },
                source, target,
                new List<Transform> { prevParent },
                new List<Vector3> { prevPos },
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

        // --- 1. СТАНДАРТНАЯ ПРОВЕРКА ЧЕРЕЗ МГНОВЕННЫЕ ВИРТУАЛЬНЫЕ ДОМА ---
        if (isRed)
        {
            // Красная карта безопасна, если обе черные масти имеют ранг не ниже (наш - 1)
            if (virtualFoundationRanks[Suit.Spades] < rank - 1 ||
                virtualFoundationRanks[Suit.Clubs] < rank - 1)
            {
                safeByStandardRules = false;
            }
        }
        else
        {
            // Черная карта безопасна, если обе красные масти имеют ранг не ниже (наш - 1)
            if (virtualFoundationRanks[Suit.Hearts] < rank - 1 ||
                virtualFoundationRanks[Suit.Diamonds] < rank - 1)
            {
                safeByStandardRules = false;
            }
        }

        if (safeByStandardRules) return true;

        // --- 2. ЛОГИКА LOOKAHEAD (Прогляд вглубь) ---
        if (card.transform.parent != null)
        {
            var tableau = card.transform.parent.GetComponent<TableauPile>();

            if (tableau != null && tableau.cards.Count >= 2)
            {
                var cardBelow = tableau.cards[tableau.cards.Count - 2];

                foreach (var f in pileManager.Foundations)
                {
                    if (f.CanAccept(cardBelow)) return true;
                }
            }
        }

        return false;
    }
}