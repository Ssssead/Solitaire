using System.Collections;
using UnityEngine;

public class SultanAutoMoveService : MonoBehaviour
{
    private SultanModeManager _mode;
    private SultanPileManager _pileManager;
    private UndoManager _undoManager;
    private RectTransform _dragLayer;

    [Header("Animation Settings")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeAmplitude = 5f;

    public void Initialize(SultanModeManager m, SultanPileManager pm, UndoManager undo, AnimationService anim, RectTransform dragLayer)
    {
        _mode = m;
        _pileManager = pm;
        _undoManager = undo;
        _dragLayer = dragLayer;
    }

    public void OnCardRightClicked(CardController card)
    {
        if (card == null || _mode == null || !_mode.IsInputAllowed) return;

        var sultanCard = card.GetComponent<SultanCardController>();

        // 1. ПРИОРИТЕТ 1: Проверяем Дома (Foundations)
        foreach (var foundation in _pileManager.Foundations)
        {
            if (foundation.CanAccept(card))
            {
                if (sultanCard != null) sultanCard.CaptureStateForUndo();
                StartCoroutine(PerformMoveRoutine(card, foundation));
                return;
            }
        }

        // 2. ПРИОРИТЕТ 2: Проверяем Резервы (Reserve Slots)
        // Не позволяем карте прыгать из резерва в резерв по двойному клику
        ICardContainer source = card.transform.parent?.GetComponent<ICardContainer>();
        if (!(source is SultanReserveSlot))
        {
            foreach (var reserve in _pileManager.Reserves)
            {
                if (reserve.CanAccept(card))
                {
                    if (sultanCard != null) sultanCard.CaptureStateForUndo();
                    StartCoroutine(PerformMoveRoutine(card, reserve));
                    return;
                }
            }
        }

        // 3. ЕСЛИ НЕТ ХОДОВ: Запускаем анимацию тряски
        StartCoroutine(ShakeCardRoutine(card));
    }

    // Обратите внимание: теперь метод принимает ICardContainer, чтобы работать и с Домами, и с Резервами
    private IEnumerator PerformMoveRoutine(CardController card, ICardContainer targetPile)
    {
        var sultanCard = card.GetComponent<SultanCardController>();
        if (sultanCard != null) sultanCard.SetAnimating(true);

        Transform oldParent = card.transform.parent;
        card.transform.SetParent(_dragLayer, true);

        Vector3 startPos = card.transform.position;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            card.transform.position = Vector3.Lerp(startPos, targetPile.Transform.position, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        targetPile.AcceptCard(card);

        if (sultanCard != null) sultanCard.SetAnimating(false);

        // Вызываем метод менеджера, чтобы засчитать очки, ход и отправить запись в Undo
        _mode.OnCardDroppedToContainer(card, targetPile);
    }

    // Анимация отрицания (тряска), перенесенная из Klondike
    private IEnumerator ShakeCardRoutine(CardController card)
    {
        Vector3 startPos = card.rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Расчет физики затухающего колебания
            float phase = Mathf.Sin(elapsed * 40f) * (1f - elapsed / shakeDuration);
            float offsetX = Mathf.Sin(elapsed * 60f) * shakeAmplitude * phase;

            card.rectTransform.anchoredPosition = startPos + new Vector3(offsetX, 0f, 0f);

            yield return null;
        }

        // Гарантированно возвращаем карту на идеальную исходную позицию
        card.rectTransform.anchoredPosition = startPos;
    }
}