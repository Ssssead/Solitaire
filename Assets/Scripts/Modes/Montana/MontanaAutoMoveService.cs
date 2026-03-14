using System.Collections;
using UnityEngine;

public class MontanaAutoMoveService : MonoBehaviour
{
    private MontanaModeManager _mode;
    private MontanaPileManager _pileManager;

    [Header("Animation Settings")]
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeAmplitude = 8f;

    public void Initialize(MontanaModeManager mode, MontanaPileManager pileManager)
    {
        _mode = mode;
        _pileManager = pileManager;
    }

    public void OnCardRightClicked(CardController card)
    {
        // Блокируем, если игра остановлена
        if (_mode == null || !_mode.IsInputAllowed) return;

        var montanaCard = card.GetComponent<MontanaCardController>();

        // Если карта заблокирована (на своем месте в цепочке) - игнорируем
        if (montanaCard == null || montanaCard.IsLockedCard) return;

        // 1. Ищем подходящий пустой слот на столе
        MontanaSlot targetSlot = null;
        foreach (var slot in _pileManager.Slots)
        {
            // Если слот пустой И по правилам готов принять эту карту
            if (slot.GetTopCard() == null && slot.CanAccept(card))
            {
                targetSlot = (MontanaSlot)slot;
                break;
            }
        }

        // 2. Выполняем действие
        if (targetSlot != null)
        {
            // Сохраняем слот, из которого улетаем (чтобы кнопка Undo работала)
            montanaCard.CaptureStateForUndo();

            // Запускаем полет в нужный слот
            montanaCard.PerformAutoMove(targetSlot);
        }
        else
        {
            // 3. Подходящего места нет — красиво трясем карту
            StartCoroutine(ShakeCardRoutine(montanaCard));
        }
    }

    private IEnumerator ShakeCardRoutine(MontanaCardController card)
    {
        card.SetAnimating(true); // Запрещаем перетаскивать карту мышью, пока она трясется

        Vector3 startPos = card.rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float phase = Mathf.Sin(elapsed * 40f) * (1f - elapsed / shakeDuration);
            float offsetX = Mathf.Sin(elapsed * 60f) * shakeAmplitude * phase;

            card.rectTransform.anchoredPosition = startPos + new Vector3(offsetX, 0f, 0f);
            yield return null;
        }

        // Жестко возвращаем ровно на стартовую точку после анимации
        card.rectTransform.anchoredPosition = startPos;
        card.SetAnimating(false);
    }
}