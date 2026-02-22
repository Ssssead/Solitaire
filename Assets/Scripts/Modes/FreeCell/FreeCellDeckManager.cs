using System.Collections;
using System.Collections.Generic;
using System.Reflection; // Обязательно для принудительного снятия блокировок
using UnityEngine;

public class FreeCellDeckManager : MonoBehaviour
{
    [Header("Settings")]
    public float cardFlyToDeckSpeed = 0.5f;
    public float dealCardSpeed = 0.1f;
    public float delayBetweenCards = 0.05f;

    [Header("References")]
    public FreeCellPileManager pileManager;
    public CardFactory cardFactory;
    public FreeCellModeManager modeManager;

    [Tooltip("Точка на экране, куда влетает начальная колода")]
    public RectTransform deckTargetPoint;

    [Tooltip("Точка за пределами экрана снизу, откуда вылетают карты")]
    public Vector2 offscreenSpawnPoint = new Vector2(0, -2000f);

    private struct DealAction
    {
        public int columnIndex;
        public CardModel model;
    }

    public IEnumerator PlayIntroDeal(Deal deal)
    {
        // 1. Очистка стола
        cardFactory.DestroyAllCards();

        // 2. Формируем список раздачи
        List<DealAction> dealSequence = new List<DealAction>();
        int maxRows = 7;

        for (int row = 0; row < maxRows; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (col < deal.tableau.Count && row < deal.tableau[col].Count)
                {
                    var cData = deal.tableau[col][row];
                    dealSequence.Add(new DealAction
                    {
                        columnIndex = col,
                        model = new CardModel(cData.Card.suit, cData.Card.rank)
                    });
                }
            }
        }

        // 3. Создаем все карты за кадром
        List<CardController> spawnedCards = new List<CardController>();

        for (int i = dealSequence.Count - 1; i >= 0; i--)
        {
            var action = dealSequence[i];
            CardController card = cardFactory.CreateCard(action.model, modeManager.DragLayer, offscreenSpawnPoint);

            card.GetComponent<CardData>().SetFaceUp(true, false);
            spawnedCards.Add(card);
        }

        spawnedCards.Reverse();

        // 4. Влет начальной колоды
        float elapsed = 0f;
        Vector2 targetPos = deckTargetPoint != null ? deckTargetPoint.anchoredPosition : Vector2.zero;

        while (elapsed < cardFlyToDeckSpeed)
        {
            elapsed += Time.deltaTime;
            float t = AnimationCurve.EaseInOut(0, 0, 1, 1).Evaluate(elapsed / cardFlyToDeckSpeed);

            foreach (var card in spawnedCards)
            {
                // ИСПОЛЬЗУЕМ anchoredPosition ВМЕСТО localPosition
                card.rectTransform.anchoredPosition = Vector2.Lerp(offscreenSpawnPoint, targetPos, t);
            }
            yield return null;
        }

        // 5. Раздача карт по столбцам
        for (int i = 0; i < dealSequence.Count; i++)
        {
            var action = dealSequence[i];
            var card = spawnedCards[i];
            var targetPile = pileManager.Tableau[action.columnIndex];

            targetPile.AddCard(card, true);
            card.transform.SetParent(targetPile.transform, true);
            card.transform.SetAsLastSibling();

            if (modeManager.dragManager != null)
                modeManager.dragManager.RegisterCardEvents(card);

            Vector2 finalPos = targetPile.GetDropAnchoredPosition(card);

            // Запускаем полет
            StartCoroutine(FlyCardToPile(card, card.rectTransform.anchoredPosition, finalPos, dealCardSpeed));

            yield return new WaitForSeconds(delayBetweenCards);
        }

        // --- ИСПРАВЛЕНИЕ 1: Ждем, пока все карты физически долетят! ---
        yield return new WaitForSeconds(dealCardSpeed + 0.1f);

        // 6. Выравниваем стопки только когда полет полностью завершен
        foreach (var tab in pileManager.Tableau)
        {
            tab.StartLayoutAnimationPublic();
        }

        // --- ИСПРАВЛЕНИЕ 2: Даем время внутренней анимации Tableau завершиться ---
        yield return new WaitForSeconds(0.4f);

        // --- ИСПРАВЛЕНИЕ 3: ПРИНУДИТЕЛЬНАЯ РАЗБЛОКИРОВКА СТОПОК И КАРТ ---
        foreach (var tab in pileManager.Tableau)
        {
            // Снимаем isLayoutLocked через рефлексию
            var type = typeof(TableauPile);
            var fieldLocked = type.GetField("isLayoutLocked", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldLocked != null) fieldLocked.SetValue(tab, false);

            // Возвращаем возможность кликать всем картам в стопке
            foreach (var card in tab.cards)
            {
                var cg = card.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = true;
            }
        }
    }

    private IEnumerator FlyCardToPile(CardController card, Vector2 from, Vector2 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // ИСПОЛЬЗУЕМ anchoredPosition ВМЕСТО localPosition
            card.rectTransform.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        card.rectTransform.anchoredPosition = to;
    }
}