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
        ICardContainer source = card.transform.parent?.GetComponent<ICardContainer>();

        // --- ИСПРАВЛЕНИЕ БАГА: Запрещаем авто-ход ИЗ Домов и Центра ---
        if (source is SultanFoundationPile || source is SultanCenterPile)
        {
            // Карта просто потрясется, показывая, что ее нельзя отсюда забрать
            StartCoroutine(ShakeCardRoutine(card));
            return;
        }

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

        // <--- ИСПРАВЛЕН ЗВУК: Быстрый свист с повышенным питчем --->
        if (AudioManager.Instance != null)
        {
            AudioSource whoosh = AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            if (whoosh != null) whoosh.pitch = 1.3f; // Повышаем питч для легкости
        }

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

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");

        if (sultanCard != null) sultanCard.SetAnimating(false);

        // ---> ВАЖНО: Завершаем ход через главный менеджер.
        // Поскольку в SultanModeManager мы добавили универсальный трекинг квестов 
        // прямо внутрь OnCardDroppedToContainer, все задания для авто-хода 
        // будут засчитаны АВТОМАТИЧЕСКИ без необходимости дублировать код!
        _mode.OnCardDroppedToContainer(card, targetPile);
    }

    // Анимация отрицания (тряска), перенесенная из Klondike
    private IEnumerator ShakeCardRoutine(CardController card)
    {
        // <--- ПОДГОТОВКА ЗВУКА --->
        AudioSource scrapeSource = null;
        float originalVolume = 1f;

        if (AudioManager.Instance != null)
        {
            scrapeSource = AudioManager.Instance.PlaySound("Card_Shake"); // Берем ссылку на длинный звук трения

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

            // <--- ДИНАМИЧЕСКАЯ ГРОМКОСТЬ ОТ СКОРОСТИ --->
            if (scrapeSource != null && scrapeSource.isPlaying)
            {
                // Abs(Cos) дает пульсацию от 0 до 1 синхронно с движением карты
                float speedMultiplier = Mathf.Abs(Mathf.Cos(elapsed * 60f));

                // Не уводим звук в абсолютный ноль (0.1f), чтобы не было "рваного" обрыва
                float dynamicVolume = Mathf.Lerp(0.1f, 1f, speedMultiplier);

                // Плавно глушим общий звук к самому концу анимации
                float generalFade = 1f - (elapsed / shakeDuration);

                // Применяем финальную громкость
                scrapeSource.volume = originalVolume * dynamicVolume * generalFade;
            }

            float offsetX = Mathf.Sin(elapsed * 60f) * shakeAmplitude * phase;
            card.rectTransform.anchoredPosition = startPos + new Vector3(offsetX, 0f, 0f);

            yield return null;
        }

        // Возвращаем в исходную позицию
        card.rectTransform.anchoredPosition = startPos;

        // <--- ОСТАНОВКА И СБРОС ЗВУКА --->
        if (scrapeSource != null)
        {
            scrapeSource.Stop(); // Жестко рубим длинный хвост файла
            scrapeSource.volume = originalVolume; // ВАЖНО: возвращаем громкость, иначе пул сломается!
        }
    }
}