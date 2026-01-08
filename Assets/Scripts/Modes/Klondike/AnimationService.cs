// AnimationService.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сервис анимаций и Z-сортировки карт в контейнерах.
/// Обеспечивает корректную визуальную глубину карт.
/// </summary>
public class AnimationService : MonoBehaviour
{
    private KlondikeModeManager mode;

    [Header("Z-Ordering Settings")]
    [SerializeField] private float zStep = 0.01f;
    [Tooltip("Максимальная разница по Z (защита от слишком больших значений)")]
    [SerializeField] private float maxZDepth = 5f;

    [Header("Animation Settings")]
    [SerializeField] private float defaultMoveDuration = 0.25f;
    [SerializeField] private AnimationCurve defaultMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public void Initialize(KlondikeModeManager m)
    {
        mode = m;
    }

    /// <summary>
    /// Переупорядочивает Z-координаты карт в контейнере.
    /// Карты ниже по иерархии (меньший sibling index) находятся глубже (больший Z).
    /// Верхняя карта (последняя в иерархии) имеет Z ближе к камере (меньший Z).
    /// </summary>
    public void ReorderContainerZ(Transform container, float tableauVerticalGap = 40f)
    {
        if (container == null) return;

        // Собираем все CardController-карты из детей контейнера
        List<CardController> cards = new List<CardController>();
        for (int i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            var cardCtrl = child.GetComponent<CardController>();
            if (cardCtrl != null)
            {
                cards.Add(cardCtrl);
            }
        }

        if (cards.Count == 0) return;

        // Сортируем по sibling index (порядок в иерархии)
        cards.Sort((a, b) => a.rectTransform.GetSiblingIndex().CompareTo(b.rectTransform.GetSiblingIndex()));

        int totalCards = cards.Count;

        // Применяем Z-координаты: первая карта (нижняя) получает максимальный Z,
        // последняя (верхняя) получает минимальный Z (ближе к камере)
        for (int i = 0; i < totalCards; i++)
        {
            var card = cards[i];
            var rt = card.rectTransform;

            // Расстояние от верхней карты (чем больше, тем глубже)
            int distanceFromTop = (totalCards - 1) - i;

            // Вычисляем Z с ограничением максимальной глубины
            float z = Mathf.Min(distanceFromTop * zStep, maxZDepth);

            Vector3 localPos = rt.localPosition;
            localPos.z = z;
            rt.localPosition = localPos;

            // Убеждаемся, что sibling index соответствует порядку
            if (rt.GetSiblingIndex() != i)
            {
                rt.SetSiblingIndex(i);
            }
        }
    }

    /// <summary>
    /// Переупорядочивает Z во всех переданных контейнерах и обновляет Canvas.
    /// </summary>
    public void ReorderAllContainers(IEnumerable<Transform> containers)
    {
        if (containers == null) return;

        foreach (var container in containers)
        {
            if (container != null)
            {
                ReorderContainerZ(container);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Анимированное перемещение карты из текущей позиции в целевую мировую позицию.
    /// </summary>
    public IEnumerator MoveCardWorldPosition(CardController card, Vector3 targetWorldPos, float duration = -1f)
    {
        if (card == null) yield break;

        if (duration < 0) duration = defaultMoveDuration;

        Vector3 startPos = card.rectTransform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = defaultMoveCurve.Evaluate(t);

            card.rectTransform.position = Vector3.Lerp(startPos, targetWorldPos, eased);

            yield return null;
        }

        // Финальная установка точной позиции
        card.rectTransform.position = targetWorldPos;
    }

    /// <summary>
    /// Анимированное перемещение группы карт.
    /// </summary>
    public IEnumerator MoveCardsWorldPositions(List<CardController> cards, List<Vector3> targetWorldPositions, float duration = -1f)
    {
        if (cards == null || targetWorldPositions == null || cards.Count != targetWorldPositions.Count)
            yield break;

        if (duration < 0) duration = defaultMoveDuration;

        List<Vector3> startPositions = new List<Vector3>(cards.Count);
        foreach (var card in cards)
        {
            startPositions.Add(card.rectTransform.position);
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = defaultMoveCurve.Evaluate(t);

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].rectTransform.position = Vector3.Lerp(startPositions[i], targetWorldPositions[i], eased);
                }
            }

            yield return null;
        }

        // Финальные позиции
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
            {
                cards[i].rectTransform.position = targetWorldPositions[i];
            }
        }
    }

    /// <summary>
    /// Вспомогательный метод: конвертирует anchored position в world position для заданного контейнера.
    /// </summary>
    public Vector3 AnchoredToWorldPosition(RectTransform container, Vector2 anchoredPos)
    {
        if (container == null) return Vector3.zero;

        // Создаём временный объект для расчёта
        GameObject tempObj = new GameObject("TempPosCalc");
        tempObj.transform.SetParent(container, false);
        RectTransform tempRect = tempObj.AddComponent<RectTransform>();
        tempRect.anchoredPosition = anchoredPos;

        Canvas.ForceUpdateCanvases();
        Vector3 worldPos = tempRect.position;

        Destroy(tempObj);
        return worldPos;
    }
}