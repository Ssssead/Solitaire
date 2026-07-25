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
            // <--- ЗВУК ВЫЛЕТА КАРТЫ ПО ДВОЙНОМУ КЛИКУ --->
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            }

            // Сохраняем слот, из которого улетаем (чтобы кнопка Undo работала)
            montanaCard.CaptureStateForUndo();

            // Запускаем полет в нужный слот
            montanaCard.PerformAutoMove(targetSlot);
        }
        else
        {
            // 3. Подходящего места нет — красиво трясем карту со звуком
            StartCoroutine(ShakeCardRoutine(montanaCard));
        }
    }

    private IEnumerator ShakeCardRoutine(MontanaCardController card)
    {
        card.SetAnimating(true); // Запрещаем перетаскивать карту мышью, пока она трясется

        // <--- ПОДГОТОВКА ЗВУКА ТРЯСКИ --->
        AudioSource scrapeSource = null;
        float originalVolume = 1f;

        if (AudioManager.Instance != null)
        {
            scrapeSource = AudioManager.Instance.PlaySound("Card_Shake");
            if (scrapeSource != null)
            {
                originalVolume = scrapeSource.volume; // Запоминаем дефолтную громкость
            }
        }

        Vector3 startPos = card.rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float phase = Mathf.Sin(elapsed * 40f) * (1f - elapsed / shakeDuration);
            float offsetX = Mathf.Sin(elapsed * 60f) * shakeAmplitude * phase;

            // <--- ДИНАМИЧЕСКАЯ ГРОМКОСТЬ ОТ СКОРОСТИ ДВИЖЕНИЯ --->
            if (scrapeSource != null && scrapeSource.isPlaying)
            {
                // Abs(Cos) дает пульсацию от 0 до 1 синхронно с движением карты
                float speedMultiplier = Mathf.Abs(Mathf.Cos(elapsed * 60f));
                // Не уводим звук в абсолютный ноль, чтобы не было рваного обрыва
                float dynamicVolume = Mathf.Lerp(0.1f, 1f, speedMultiplier);
                // Плавно глушим общий звук к самому концу анимации
                float generalFade = 1f - (elapsed / shakeDuration);

                scrapeSource.volume = originalVolume * dynamicVolume * generalFade;
            }

            card.rectTransform.anchoredPosition = startPos + new Vector3(offsetX, 0f, 0f);
            yield return null;
        }

        // Жестко возвращаем ровно на стартовую точку после анимации
        card.rectTransform.anchoredPosition = startPos;
        card.SetAnimating(false);

        // <--- ОСТАНОВКА И СБРОС ЗВУКА --->
        if (scrapeSource != null)
        {
            scrapeSource.Stop(); // Жестко рубим длинный хвост файла
            scrapeSource.volume = originalVolume; // Возвращаем громкость для пула!
        }
    }
}