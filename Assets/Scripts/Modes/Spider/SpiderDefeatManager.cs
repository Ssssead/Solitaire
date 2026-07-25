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

        // 1. Освобождение пустой колонки - это всегда полезно, так как дает пустой слот для любых манипуляций
        if (sourceIdx == 0 && targetPile.cards.Count > 0) return true;

        // Перемещение всей колонки с пустого слота на пустой слот - бессмысленно
        if (sourceIdx == 0 && targetPile.cards.Count == 0) return false;

        if (sourceIdx > 0)
        {
            var cardBelow = sourcePile.cards[sourceIdx - 1];

            // 2. Открытие рубашки скрытой карты - всегда продвигает игру
            if (!cardBelow.GetComponent<CardData>().IsFaceUp()) return true;

            // 3. Шило на мыло (бессмысленный перенос между одинаковыми картами)
            if (targetPile.cards.Count > 0)
            {
                var targetTop = targetPile.cards[targetPile.cards.Count - 1];
                // Если переносим карту на точно такую же по рангу и масти (например, с 5 Пик на 5 Пик)
                if (targetTop.cardModel.suit == cardBelow.cardModel.suit &&
                    targetTop.cardModel.rank == cardBelow.cardModel.rank)
                {
                    return false;
                }
            }
        }

        // 4. ВО ВСЕХ ОСТАЛЬНЫХ СЛУЧАЯХ ход МОЖЕТ быть частью сложной многоходовочки.
        // Перенос собранной части масти на пустой слот, перенос на другую масть для освобождения нужной карты и т.д.
        return true;
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