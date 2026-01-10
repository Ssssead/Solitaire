using System.Collections;
using UnityEngine;

public class FreeCellDeckManager : MonoBehaviour
{
    [Header("Settings")]
    public float dealDuration = 0.5f;
    public float cardFlySpeed = 2000f; // Скорость полета карты

    [Header("References")]
    public FreeCellPileManager pileManager;
    public CardFactory cardFactory;
    public FreeCellModeManager modeManager;

    public void ApplyDeal(Deal deal)
    {
        StartCoroutine(DealRoutine(deal));
    }

    private IEnumerator DealRoutine(Deal deal)
    {
        // 1. Создаем все карты (сразу 52)
        // В FreeCell карты обычно появляются "из ниоткуда" или летят из центра экрана
        // Мы сделаем генерацию сразу в столбцах, но с анимацией "проявления" или полета.

        // Очистка стола (на всякий случай)
        cardFactory.DestroyAllCards();

        // 2. Проходим по столбцам (8 штук)
        // В FreeCell deal.tableau должно содержать 8 списков
        int columnsCount = Mathf.Min(8, deal.tableau.Count);

        // Чтобы анимация была красивой (по одной карте в каждый столбец по кругу),
        // нужно преобразовать данные. Но для простоты раздадим по столбцам.

        // Подсчитаем общее кол-во карт для Z-сортировки
        int totalCards = 0;
        foreach (var col in deal.tableau) totalCards += col.Count;

        for (int i = 0; i < columnsCount; i++)
        {
            var pileData = deal.tableau[i];
            // Получаем ссылку на стопку. Важно: это FreeCellTableauPile
            var targetPile = pileManager.Tableau[i];

            foreach (var cardData in pileData)
            {
                CardModel model = new CardModel(cardData.Card.suit, cardData.Card.rank);

                // Создаем карту (пока скрытую, или в точке старта)
                // Точка старта - центр экрана или низ
                Vector2 startPos = Vector2.zero;

                CardController card = cardFactory.CreateCard(model, modeManager.DragLayer, startPos);

                // Настраиваем данные
                CardData data = card.GetComponent<CardData>();
                data.SetFaceUp(true, false); // В FreeCell все открыты сразу

                // Добавляем в стопку логически
                targetPile.AddCard(card, true);

                // Анимация полета
                // Можно использовать DOTween или корутину. Здесь упрощенно:
                // Перемещаем карту в иерархию стопки
                card.transform.SetParent(targetPile.transform, true);

                // Регистрируем события ввода
                if (modeManager.dragManager != null)
                    modeManager.dragManager.RegisterCardEvents(card);
            }

            // Запускаем пересчет позиций в стопке (Layout Animation)
            targetPile.StartLayoutAnimationPublic();

            // Небольшая задержка между столбцами (эффект "волны")
            yield return new WaitForSeconds(0.05f);
        }

        // Завершение
        modeManager.IsInputAllowed = true;
    }
}