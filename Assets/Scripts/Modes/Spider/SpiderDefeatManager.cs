using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiderDefeatManager : MonoBehaviour
{
    private SpiderPileManager pileManager;
    private GameUIController gameUI;
    private SpiderModeManager modeManager;

    [Header("Settings")]
    [SerializeField] private float defeatDelay = 1.0f; // Задержка перед показом поражения

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

        // 1. Если в колоде еще есть карты — мы живы
        if (pileManager.StockPile.GetCardCount() > 0)
        {
            StopDefeatTimer();
            return;
        }

        // 2. Если есть полезные ходы — мы живы
        if (HasAnyUsefulMove())
        {
            StopDefeatTimer();
            return;
        }

        // 3. Если сток пуст и ходов нет — запускаем таймер поражения
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

        // Финальная проверка перед смертью (вдруг игрок успел что-то сделать)
        if (pileManager.StockPile.GetCardCount() == 0 && !HasAnyUsefulMove())
        {
            Debug.Log("Spider Defeat: No moves left.");
            if (gameUI != null) gameUI.OnGameLost();
        }
        pendingDefeatCoroutine = null;
    }

    // --- ЛОГИКА ПОЛЕЗНОСТИ ХОДОВ ---

    private bool HasAnyUsefulMove()
    {
        // Проходим по всем колонкам
        for (int i = 0; i < 10; i++)
        {
            SpiderTableauPile sourcePile = pileManager.TableauPiles[i];
            if (sourcePile.cards.Count == 0) continue;

            // Получаем список карт, которые можно взять (одной масти по порядку)
            List<CardController> movableSequence = GetMovableSequence(sourcePile);
            if (movableSequence.Count == 0) continue;

            // Пытаемся примерить эту пачку ко всем остальным колонкам
            foreach (var cardToMove in movableSequence)
            {
                for (int j = 0; j < 10; j++)
                {
                    if (i == j) continue;

                    SpiderTableauPile targetPile = pileManager.TableauPiles[j];

                    // Технически ход возможен?
                    if (targetPile.CanAccept(cardToMove))
                    {
                        // Проверяем, ПОЛЕЗЕН ли он
                        if (IsMoveUseful(sourcePile, cardToMove, targetPile))
                        {
                            return true; // Нашли хотя бы один полезный ход
                        }
                    }
                }
            }
        }
        return false;
    }

    private bool IsMoveUseful(SpiderTableauPile sourcePile, CardController cardToMove, SpiderTableauPile targetPile)
    {
        // 1. Если мы освобождаем пустую ячейку (под картой ничего нет)
        // В Пауке пустая ячейка всегда полезна.
        int cardIndex = sourcePile.cards.IndexOf(cardToMove);
        if (cardIndex == 0) return true;

        // Смотрим, что лежит ПОД картой, которую мы хотим убрать
        CardController cardBelow = sourcePile.cards[cardIndex - 1];
        CardData dataBelow = cardBelow.GetComponent<CardData>();

        // 2. Если карта снизу ЗАКРЫТА — ход полезен (мы её откроем)
        if (!dataBelow.IsFaceUp()) return true;

        // 3. Если карта снизу ОТКРЫТА — проверяем на "бесполезный цикл"
        // Пример: переносим 3H с 4D на другую 4D. Это бесполезно, если 4D снизу открыта.

        // Получаем, на что мы кладем (Топ целевой стопки или пустота)
        if (targetPile.cards.Count == 0) return true; // Перенос на пустое место полезен

        CardController targetTop = targetPile.cards[targetPile.cards.Count - 1];

        // Сравниваем Ранг карты ПОД нами и Ранг карты, НА которую кладем.
        // В Пауке масть основания не важна для валидности, важен только Ранг.
        // Если Ранги совпадают, мы просто перекладываем "шило на мыло".
        if (cardBelow.cardModel.rank == targetTop.cardModel.rank)
        {
            return false; // Бесполезный ход
        }

        return true;
    }

    private List<CardController> GetMovableSequence(SpiderTableauPile pile)
    {
        List<CardController> result = new List<CardController>();
        if (pile.cards.Count == 0) return result;

        int lastIdx = pile.cards.Count - 1;
        result.Add(pile.cards[lastIdx]);

        for (int i = lastIdx - 1; i >= 0; i--)
        {
            var current = pile.cards[i];
            var next = pile.cards[i + 1];

            if (!current.GetComponent<CardData>().IsFaceUp()) break;

            // В Пауке тащить можно только ОДНОМАСТНЫЕ последовательности
            bool suitOk = current.cardModel.suit == next.cardModel.suit;
            bool rankOk = current.cardModel.rank == next.cardModel.rank + 1;

            if (suitOk && rankOk) result.Add(current);
            else break;
        }
        return result; // Возвращает в порядке с конца (K, Q...), нам для итерации подойдет
    }
}