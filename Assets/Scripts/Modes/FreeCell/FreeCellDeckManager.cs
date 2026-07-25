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

    // Флаг пропуска анимации
    private bool isSkippingIntro = false;

    private struct DealAction
    {
        public int columnIndex;
        public CardModel model;
    }

    // --- ДОБАВЛЕНО: Отслеживаем клик для ускорения раздачи ---
    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            isSkippingIntro = true;
        }
    }
    // --------------------------------------------------------

    // --- ДОБАВЛЕНО: Кастомный таймер для пропуска ---
    private IEnumerator SkippableWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            yield return null;
        }
    }
    // ------------------------------------------------

    public IEnumerator PlayIntroDeal(Deal deal)
    {
        isSkippingIntro = false; // Сбрасываем перед началом

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

        // <--- ЗВУК: КОЛОДА СРЫВАЕТСЯ С МЕСТА --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Whoosh_In");

        while (elapsed < cardFlyToDeckSpeed)
        {
            // Ускоряем влет колоды
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;

            float t = Mathf.Clamp01(elapsed / cardFlyToDeckSpeed);
            float evaluatedT = AnimationCurve.EaseInOut(0, 0, 1, 1).Evaluate(t);

            foreach (var card in spawnedCards)
            {
                card.rectTransform.anchoredPosition = Vector2.Lerp(offscreenSpawnPoint, targetPos, evaluatedT);
            }
            yield return null;
        }

        // <--- ЗВУК: КОЛОДА ПАДАЕТ НА СТОЛ --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");

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

            // <--- ЗВУК: ШЕЛЕСТ РАЗДАЧИ КАРТЫ --->
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Deal");

            // Пауза с возможностью пропуска
            yield return StartCoroutine(SkippableWait(delayBetweenCards));
        }
        // 6. Ждем, пока все карты физически долетят
        yield return StartCoroutine(SkippableWait(dealCardSpeed + 0.1f));

        // Выравниваем стопки только когда полет полностью завершен
        foreach (var tab in pileManager.Tableau)
        {
            tab.StartLayoutAnimationPublic();
        }

        // Даем время внутренней анимации Tableau завершиться
        yield return StartCoroutine(SkippableWait(0.4f));

        // ПРИНУДИТЕЛЬНАЯ РАЗБЛОКИРОВКА СТОПОК И КАРТ
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
            // Ускоряем полет отдельной карты
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;

            float t = Mathf.Clamp01(elapsed / duration);
            card.rectTransform.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        card.rectTransform.anchoredPosition = to;
    }
}