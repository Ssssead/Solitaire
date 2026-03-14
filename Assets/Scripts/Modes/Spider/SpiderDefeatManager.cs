using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiderDefeatManager : MonoBehaviour
{
    private SpiderPileManager pileManager;
    private GameUIController gameUI;
    private SpiderModeManager modeManager;

    [Header("Settings")]
    [SerializeField] private float defeatDelay = 1.0f;

    [Header("Undo Grace")]
    [SerializeField] private float undoGracePeriod = 2.0f;
    private float ignoreChecksUntil = 0f;

    private Coroutine pendingDefeatCoroutine;

    public void Initialize(SpiderPileManager pm, GameUIController ui, SpiderModeManager mm)
    {
        pileManager = pm;
        gameUI = ui;
        modeManager = mm;
    }

    public void OnUndo()
    {
        StopDefeatTimer();
        ignoreChecksUntil = Time.time + undoGracePeriod;
    }

    public void CheckDefeatCondition()
    {
        if (Time.time < ignoreChecksUntil) return;

        // --- ЗАЩИТА 1: Если сейчас летит стопка в дом, мы точно не проиграли ---
        if (modeManager != null && modeManager.ActiveFoundationAnimations > 0)
        {
            StopDefeatTimer();
            return;
        }

        // --- ЗАЩИТА 2: Если есть сток - играем дальше ---
        if (pileManager.StockPile.GetCardCount() > 0)
        {
            StopDefeatTimer();
            return;
        }

        // --- ЗАЩИТА 3: Если весь стол пустой (это победа, а не поражение) ---
        if (IsTableauCompletelyEmpty())
        {
            StopDefeatTimer();
            return;
        }

        // --- ЗАЩИТА 4: Если есть хоть один полезный ход - играем дальше ---
        if (HasAnyUsefulMove())
        {
            StopDefeatTimer();
            return;
        }

        // Тупик
        if (pendingDefeatCoroutine == null)
        {
            pendingDefeatCoroutine = StartCoroutine(DefeatRoutine());
        }
    }

    private void StopDefeatTimer()
    {
        if (pendingDefeatCoroutine != null)
        {
            StopCoroutine(pendingDefeatCoroutine);
            pendingDefeatCoroutine = null;
        }
    }

    private IEnumerator DefeatRoutine()
    {
        yield return new WaitForSeconds(defeatDelay);

        // Повторная финальная проверка перед тем как показать экран
        if (pileManager.StockPile.GetCardCount() == 0 && !HasAnyUsefulMove() && !IsTableauCompletelyEmpty() && modeManager.ActiveFoundationAnimations == 0)
        {
            Debug.Log("Spider Defeat: True mathematical dead-end reached.");
            if (gameUI != null) gameUI.OnGameLost();
        }
        pendingDefeatCoroutine = null;
    }

    private bool IsTableauCompletelyEmpty()
    {
        for (int i = 0; i < 10; i++)
        {
            if (pileManager.TableauPiles[i].cards.Count > 0) return false;
        }
        return true;
    }

    // --- УМНЫЙ АНАЛИЗ ХОДОВ ---

    private bool HasAnyUsefulMove()
    {
        for (int i = 0; i < 10; i++)
        {
            var sourcePile = pileManager.TableauPiles[i];

            // Получаем ВСЕ возможные комбинации (всю стопку и любые её части)
            var sequences = GetAllMovableSequences(sourcePile);

            foreach (var seq in sequences)
            {
                var topOfSeq = seq[0];
                int sourceIdx = sourcePile.cards.IndexOf(topOfSeq);

                for (int j = 0; j < 10; j++)
                {
                    if (i == j) continue;
                    var targetPile = pileManager.TableauPiles[j];

                    if (targetPile.CanAccept(topOfSeq))
                    {
                        if (IsMoveProductive(sourcePile, sourceIdx, targetPile, seq))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private bool IsMoveProductive(SpiderTableauPile sourcePile, int sourceIdx, SpiderTableauPile targetPile, List<CardController> draggedSeq)
    {
        var topOfSeq = draggedSeq[0];

        // 1. Освобождение пустой колонки всегда полезно
        if (sourceIdx == 0) return true;

        var cardBelow = sourcePile.cards[sourceIdx - 1];

        // 2. Открытие рубашки всегда полезно
        if (!cardBelow.GetComponent<CardData>().IsFaceUp()) return true;

        bool isCurrentMatch = cardBelow.cardModel.suit == topOfSeq.cardModel.suit;

        if (targetPile.cards.Count > 0)
        {
            var targetTop = targetPile.cards[targetPile.cards.Count - 1];
            bool isTargetMatch = targetTop.cardModel.suit == topOfSeq.cardModel.suit;

            // 3. Сборка масти (была разная, стала одинаковая)
            if (isTargetMatch && !isCurrentMatch) return true;

            // Разрывать масть ради другой масти нет смысла
            if (isCurrentMatch && !isTargetMatch) return false;

            // Перекладывание "Шило на мыло" (та же масть и тот же ранг)
            if (targetTop.cardModel.suit == cardBelow.cardModel.suit &&
                targetTop.cardModel.rank == cardBelow.cardModel.rank)
            {
                return false;
            }
        }
        else
        {
            // 4. Перенос на пустую ячейку (полезно для мусора, вредно для собранной масти)
            if (isCurrentMatch) return false;
            return true;
        }

        // --- 5. МНОГОХОДОВКА (Перенос мусора на мусор) ---
        // Ищем любую косвенную выгоду на столе
        for (int k = 0; k < 10; k++)
        {
            var p = pileManager.TableauPiles[k];
            if (p == sourcePile || p == targetPile) continue;

            // Если на столе есть пустая колонка, игра не может быть проиграна
            // Пустая колонка дает место для любых маневров
            if (p.cards.Count == 0) return true;

            var pTop = p.cards[p.cards.Count - 1];

            // Выгода А: Сможет ли освободившаяся карта лечь по масти куда-то еще?
            if (pTop.cardModel.suit == cardBelow.cardModel.suit &&
                pTop.cardModel.rank == cardBelow.cardModel.rank + 1) return true;

            var otherSeqs = GetAllMovableSequences(p);
            foreach (var oSeq in otherSeqs)
            {
                var oTop = oSeq[0];

                // Выгода Б: Сможет ли кто-то лечь по масти на освободившуюся карту?
                if (oTop.cardModel.suit == cardBelow.cardModel.suit &&
                    oTop.cardModel.rank == cardBelow.cardModel.rank - 1) return true;

                // Выгода В: Сможет ли перемещенная стопка принять на себя карту по масти?
                var bottomOfDraggedSeq = draggedSeq[draggedSeq.Count - 1];
                if (oTop.cardModel.suit == bottomOfDraggedSeq.cardModel.suit &&
                    oTop.cardModel.rank == bottomOfDraggedSeq.cardModel.rank - 1) return true;
            }
        }

        return false; // Это абсолютный тупик
    }

    // --- Находит все доступные для отрыва части цепочки ---
    private List<List<CardController>> GetAllMovableSequences(SpiderTableauPile pile)
    {
        List<List<CardController>> sequences = new List<List<CardController>>();
        if (pile.cards.Count == 0) return sequences;

        int lastIdx = pile.cards.Count - 1;

        List<CardController> currentSeq = new List<CardController>();
        currentSeq.Add(pile.cards[lastIdx]);
        sequences.Add(new List<CardController>(currentSeq));

        for (int i = lastIdx - 1; i >= 0; i--)
        {
            var current = pile.cards[i];
            var prev = pile.cards[i + 1];

            if (!current.GetComponent<CardData>().IsFaceUp()) break;

            if (current.cardModel.suit == prev.cardModel.suit &&
                current.cardModel.rank == prev.cardModel.rank + 1)
            {
                currentSeq.Insert(0, current);
                sequences.Add(new List<CardController>(currentSeq));
            }
            else break;
        }

        return sequences;
    }
}