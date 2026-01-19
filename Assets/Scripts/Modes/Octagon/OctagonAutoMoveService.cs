using System.Collections;
using UnityEngine;

public class OctagonAutoMoveService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OctagonModeManager modeManager;
    [SerializeField] private OctagonPileManager pileManager;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 1500f; // Скорость полета карты

    // Метод вызывается из ModeManager при двойном клике
    public bool TryAutoMove(CardController card)
    {
        if (card == null) return false;

        // Ищем подходящую базу (Foundation)
        foreach (var foundation in pileManager.FoundationPiles)
        {
            if (foundation.CanAccept(card))
            {
                StartCoroutine(PerformMoveRoutine(card, foundation));
                return true;
            }
        }

        return false;
    }

    private IEnumerator PerformMoveRoutine(CardController card, OctagonFoundationPile targetPile)
    {
        // 1. Блокируем ввод на время анимации (опционально)
        // card.canvasGroup.blocksRaycasts = false;

        // 2. Переносим карту в иерархии в новый контейнер (чтобы она отрисовалась поверх)
        // Но пока оставляем визуальную позицию, чтобы она "полетела"
        Transform oldParent = card.transform.parent;

        // Чтобы карта летела поверх всего, можно временно прицепить к корню Canvas или DragLayer
        // Но для простоты сразу положим в таргет и анимируем anchoredPosition
        card.transform.SetParent(targetPile.transform);

        // 3. Анимация полета
        // Вычисляем позицию 0,0 относительно нового родителя (центра стопки)
        Vector2 startPos = card.rectTransform.anchoredPosition;
        Vector2 targetPos = Vector2.zero;

        float dist = Vector2.Distance(startPos, targetPos);
        float duration = dist / moveSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            card.rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 4. Финализация
        card.rectTransform.anchoredPosition = targetPos;

        // Оповещаем менеджер о ходе для проверки победы
        modeManager.CheckGameState();
    }
}