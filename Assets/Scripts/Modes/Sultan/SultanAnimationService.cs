using System.Collections;
using UnityEngine;

public class SultanAnimationService : MonoBehaviour
{
    private SultanModeManager _mode;

    public void Initialize(SultanModeManager mode)
    {
        _mode = mode;
    }

    // Основной метод для запуска анимации Draw
    public void AnimateStockToWaste(CardController card, SultanWastePile targetWaste)
    {
        StartCoroutine(StockToWasteRoutine(card, targetWaste));
    }

    private IEnumerator StockToWasteRoutine(CardController card, SultanWastePile targetWaste)
    {
        var sultanCard = card.GetComponent<SultanCardController>();
        var cardData = card.GetComponent<CardData>();

        // 1. Подготовка
        if (sultanCard != null) sultanCard.SetAnimating(true);
        _mode.IsInputAllowed = false; // Блокируем весь ввод на время полета

        // Запоминаем мировые позиции для точного Lerp
        Vector3 startPos = card.transform.position;
        Vector3 endPos = targetWaste.transform.position;

        // Переносим карту на DragLayer (поверх всего)
        card.transform.SetParent(_mode.DragLayer, true);
        card.transform.SetAsLastSibling();

        // 2. Параметры полета
        float duration = 0.35f; // Длительность полета (подберите по вкусу)
        float elapsed = 0f;
        bool flippedFaceUp = false;

        // Инкремент для Z, чтобы карта была чуть выше всех в полете (защита от Z-fighting)
        Vector3 vZ = new Vector3(0, 0, -1);

        // 3. Цикл анимации (Lerp)
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Применяем кривую (SmoothStep) для плавного старта и остановки
            float smoothT = t * t * (3f - 2f * t);

            // Двигаем карту в мировых координатах
            card.transform.position = Vector3.Lerp(startPos, endPos, smoothT) + vZ;

            // --- КЛЮЧЕВОЙ МОМЕНТ: ПЕРЕВОРОТ ---
            // Если пролетели половину пути (t > 0.5f) и еще не перевернулись
            if (t >= 0.5f && !flippedFaceUp)
            {
                if (cardData != null)
                {
                    // Включаем лицо. В CardData должен быть флаг "анимировать переворот" (обычно true)
                    cardData.SetFaceUp(true, true);
                }
                flippedFaceUp = true;
            }

            yield return null;
        }

        // 4. Завершение
        // Окончательно фиксируем карту в Waste pile (метод AcceptCard сбросит иерархию и позицию)
        targetWaste.AcceptCard(card);

        if (sultanCard != null) sultanCard.SetAnimating(false);
        _mode.IsInputAllowed = true; // Разблокируем ввод
    }
}