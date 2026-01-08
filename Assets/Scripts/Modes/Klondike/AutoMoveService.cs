// AutoMoveService.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сервис автоматического перемещения карт при правом клике (или двойном клике).
/// Приоритет: Foundation (single card) -> Tableau (sequence).
/// Поддерживает источники: Tableau, Waste, Stock, Foundation.
/// </summary>
public class AutoMoveService : MonoBehaviour
{
    private KlondikeModeManager mode;
    private PileManager pileManager;
    private UndoManager undoManager;
    private AnimationService animationService;
    private Canvas canvas;
    private RectTransform dragLayer;
    private float tableauVerticalGap = 30f;

    [Header("Animation Settings")]
    [SerializeField] private float singleMoveDuration = 0.20f;
    [SerializeField] private float sequenceMoveDuration = 0.28f;
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private float shakeAmplitude = 5f;

    public void Initialize(KlondikeModeManager mode,
                       PileManager pileManager,
                       UndoManager undoManager,
                       AnimationService animationService,
                       Canvas canvas,
                       RectTransform dragLayer, // <-- Добавлен аргумент
                       float tableauVerticalGap)
    {
        this.mode = mode;
        this.pileManager = pileManager;
        this.undoManager = undoManager;
        this.animationService = animationService;
        this.canvas = canvas;
        this.dragLayer = dragLayer; // <-- Сохраняем ссылку!
        this.tableauVerticalGap = tableauVerticalGap;
    }

    /// <summary>
    /// Регистрирует карту для автоперемещения (можно подписаться на события карты).
    /// </summary>
    public void RegisterCardForAutoMove(CardController card)
    {
        if (card == null) return;
        // Здесь можно подписаться на событие OnRightClick, если оно есть у CardController
        // card.OnRightClick += OnCardRightClicked;
    }

    /// <summary>
    /// Главный метод: обрабатывает правый клик/двойной клик по карте.
    /// </summary>
    public void OnCardRightClicked(CardController card)
    {
        if (card == null || pileManager == null || mode == null)
        {
            Debug.LogWarning("[AutoMoveService] Cannot process: card, pileManager or mode is null.");
            return;
        }

        // Определяем источник карты
        var sourceInfo = FindCardSource(card);

        if (sourceInfo.sourceType == SourceType.Unknown)
        {
            StartCoroutine(ShakeCard(card));
            return;
        }

        bool moveSuccessful = false;

        // ПРОВЕРЯЕМ, ЧТО КАРТА ЯВЛЯЕТСЯ ВЕРХНЕЙ ПЕРЕД ПЕРЕМЕЩЕНИЕМ В FOUNDATION
        if (sourceInfo.isTopCard)
        {
            // Приоритет 1: Попытка переместить в Foundation
            if (TryMoveToFoundation(card, sourceInfo))
            {
                moveSuccessful = true;
            }
        }

        // Приоритет 2: Попытка переместить в Tableau (если еще не переместили)
        if (!moveSuccessful)
        {
            if (sourceInfo.sourceType == SourceType.Tableau)
            {
                if (TryMoveToTableau(card, sourceInfo))
                {
                    moveSuccessful = true;
                }
            }
            else
            {
                // Для других источников перемещаем только верхнюю карту
                if (sourceInfo.isTopCard)
                {
                    if (TryMoveToTableau(card, sourceInfo))
                    {
                        moveSuccessful = true;
                    }
                }
            }
        }

        // ИТОГ:
        if (moveSuccessful)
        {
            // Ход сделан успешно — проверяем условия победы
            mode.CheckGameState();
        }
        else
        {
            // Не нашли подходящего места — тряска
            ShakeCardOrSequence(card, sourceInfo);
        }
    }


    #region Source Detection

    private enum SourceType { Unknown, Tableau, Waste, Stock, Foundation }

    private struct SourceInfo
    {
        public SourceType sourceType;
        public TableauPile tableauPile;
        public int tableauIndex;
        public FoundationPile foundationPile;
        public bool isTopCard;
    }

    private SourceInfo FindCardSource(CardController card)
    {
        var info = new SourceInfo { sourceType = SourceType.Unknown, tableauIndex = -1 };

        // Проверка Tableau - проверяем transform.parent
        if (pileManager.Tableau != null)
        {
            for (int i = 0; i < pileManager.Tableau.Count; i++)
            {
                var tableau = pileManager.Tableau[i];
                if (tableau == null) continue;

                if (card.transform.parent == tableau.transform)
                {
                    // Находим индекс по sibling index
                    var allCards = new List<CardController>();
                    for (int j = 0; j < tableau.transform.childCount; j++)
                    {
                        var child = tableau.transform.GetChild(j);
                        var cardCtrl = child.GetComponent<CardController>();
                        if (cardCtrl != null)
                        {
                            allCards.Add(cardCtrl);
                        }
                    }

                    // Сортируем по sibling index
                    allCards.Sort((a, b) => a.rectTransform.GetSiblingIndex().CompareTo(b.rectTransform.GetSiblingIndex()));

                    int idx = allCards.IndexOf(card);
                    if (idx >= 0)
                    {
                        info.sourceType = SourceType.Tableau;
                        info.tableauPile = tableau;
                        info.tableauIndex = idx;
                        info.isTopCard = (idx == allCards.Count - 1);
                        return info;
                    }
                }
            }
        }

        // Проверка Waste
        if (pileManager.WastePile != null && card.transform.parent == pileManager.WastePile.transform)
        {
            info.sourceType = SourceType.Waste;
            info.isTopCard = true; // предполагаем, что это верхняя карта
            return info;
        }

        // Проверка Stock
        if (pileManager.StockPile != null && card.transform.parent == pileManager.StockPile.transform)
        {
            info.sourceType = SourceType.Stock;
            info.isTopCard = true;
            return info;
        }

        // Проверка Foundation - КРИТИЧНОЕ ИСПРАВЛЕНИЕ
        if (pileManager.Foundations != null)
        {
            foreach (var foundation in pileManager.Foundations)
            {
                if (foundation == null) continue;

                // Проверяем физическое расположение
                if (card.transform.parent == foundation.transform)
                {
                    // Проверяем, верхняя ли это карта
                    var allFoundationCards = new List<CardController>();
                    for (int i = 0; i < foundation.transform.childCount; i++)
                    {
                        var child = foundation.transform.GetChild(i);
                        var cardCtrl = child.GetComponent<CardController>();
                        if (cardCtrl != null)
                        {
                            allFoundationCards.Add(cardCtrl);
                        }
                    }

                    if (allFoundationCards.Count > 0)
                    {
                        info.sourceType = SourceType.Foundation;
                        info.foundationPile = foundation;
                        info.isTopCard = (allFoundationCards[allFoundationCards.Count - 1] == card);
                        return info;
                    }
                }
            }
        }

        return info;
    }

    #endregion

    #region Move to Foundation

    private bool TryMoveToFoundation(CardController card, SourceInfo sourceInfo)
    {
        // МОЖНО ПЕРЕМЕСТИТЬ В FOUNDATION ТОЛЬКО ВЕРХНЮЮ КАРТУ
        if (!sourceInfo.isTopCard)
        {
            return false;
        }

        // Ищем подходящий Foundation
        if (pileManager.Foundations == null) return false;

        foreach (var foundation in pileManager.Foundations)
        {
            if (foundation == null) continue;
            if (!foundation.CanAccept(card)) continue;

            // Нашли подходящий Foundation - выполняем перемещение
            return ExecuteMoveToFoundation(card, sourceInfo, foundation);
        }

        return false;
    }

    private bool ExecuteMoveToFoundation(CardController card, SourceInfo sourceInfo, FoundationPile targetFoundation)
    {
        if (!sourceInfo.isTopCard) return false;

        List<CardController> movedCards = new List<CardController> { card };
        Transform sourceTransform = null;
        Vector3 sourceLocalPos = Vector3.zero;
        int sourceSiblingIndex = -1;

        // Флаг переворота для Tableau
        bool sourceFlipped = false;
        int sourceFlippedIndex = -1;

        // 1. Изъятие
        switch (sourceInfo.sourceType)
        {
            case SourceType.Tableau:
                if (sourceInfo.tableauPile != null)
                {
                    var sequence = sourceInfo.tableauPile.RemoveSequenceFrom(sourceInfo.tableauIndex);
                    if (sequence == null || sequence.Count == 0) return false;

                    card = sequence[0];
                    movedCards[0] = card;
                    sourceTransform = sourceInfo.tableauPile.transform;
                    sourceLocalPos = new Vector3(0f, -sourceInfo.tableauIndex * tableauVerticalGap, 0f);
                    sourceSiblingIndex = sourceInfo.tableauIndex;

                    // Возвращаем лишние
                    for (int i = 1; i < sequence.Count; i++)
                    {
                        var extra = sequence[i];
                        if (extra != null)
                        {
                            extra.rectTransform.SetParent(sourceInfo.tableauPile.transform, false);
                            sourceInfo.tableauPile.AddCard(extra, true);
                        }
                    }

                    // Проверяем переворот
                    if (sourceInfo.tableauPile.CheckAndFlipTop())
                    {
                        sourceFlipped = true;
                        sourceFlippedIndex = sourceInfo.tableauPile.cards.Count - 1;
                    }

                    animationService?.ReorderContainerZ(sourceInfo.tableauPile.transform);
                }
                break;

            case SourceType.Waste:
                // Запоминаем позицию ПЕРЕД тем, как PopTop потенциально изменит состояние
                // (хотя PopTop только удаляет из списка, позиция на экране сохраняется)
                Vector3 wastePos = card.rectTransform.anchoredPosition;

                card = pileManager.WastePile.PopTop();
                if (card == null) return false;

                sourceTransform = pileManager.WastePile.transform;
                // ИСПРАВЛЕНИЕ: Передаем wastePos вместо Vector3.zero
                sourceLocalPos = wastePos;
                break;

            case SourceType.Stock:
                card = pileManager.StockPile.PopTop();
                sourceTransform = pileManager.StockPile.transform;
                break;

            default: return false;
        }

        // 2. Undo
        undoManager?.RecordMove(movedCards, GetSourceContainer(sourceInfo), targetFoundation,
            new List<Transform> { sourceTransform }, new List<Vector3> { sourceLocalPos }, new List<int> { sourceSiblingIndex });

        // 3. Запись переворота
        if (sourceFlipped && undoManager != null)
        {
            undoManager.RecordFlipInSource(sourceFlippedIndex);
        }

        // 4. DragLayer
        if (dragLayer != null)
        {
            card.rectTransform.SetParent(dragLayer, true);
            card.rectTransform.SetAsLastSibling();
        }

        targetFoundation.ReserveCard(card);
        card.ForceSnapToContainer(targetFoundation);
        animationService?.ReorderContainerZ(targetFoundation.transform);

        return true;
    }

    #endregion

    #region Move to Tableau

    private bool TryMoveToTableau(CardController card, SourceInfo sourceInfo)
    {
        // Для Tableau: если источник - Tableau, можем перемещать последовательность
        // если источник - другой контейнер, перемещаем только верхнюю карту
        if (sourceInfo.sourceType == SourceType.Tableau)
        {
            // Для Tableau - проверяем, можно ли перемещать последовательность
            if (pileManager.Tableau == null) return false;

            // Ищем подходящий Tableau
            foreach (var tableau in pileManager.Tableau)
            {
                if (tableau == null) continue;
                if (tableau == sourceInfo.tableauPile) continue; // Не перемещаем в тот же tableau
                if (!tableau.CanAccept(card)) continue;

                // Нашли подходящий Tableau
                return ExecuteMoveToTableau(card, sourceInfo, tableau);
            }
        }
        else
        {
            // Для других источников - перемещаем только верхнюю карту
            if (!sourceInfo.isTopCard)
            {
                return false;
            }

            if (pileManager.Tableau == null) return false;

            // Ищем подходящий Tableau
            foreach (var tableau in pileManager.Tableau)
            {
                if (tableau == null) continue;
                if (!tableau.CanAccept(card)) continue;

                // Нашли подходящий Tableau
                return ExecuteMoveToTableau(card, sourceInfo, tableau);
            }
        }

        return false;
    }

    private bool ExecuteMoveToTableau(CardController card, SourceInfo sourceInfo, TableauPile targetTableau)
    {
        List<CardController> sequence = null;

        // Флаг переворота
        bool sourceFlipped = false;
        int sourceFlippedIndex = -1;

        switch (sourceInfo.sourceType)
        {
            case SourceType.Tableau:
                if (sourceInfo.tableauPile == null) return false;

                // БЛОКИРУЕМ ИСТОЧНИК
                sourceInfo.tableauPile.SetAnimatingCard(true);

                sequence = sourceInfo.tableauPile.RemoveSequenceFrom(sourceInfo.tableauIndex);
                if (sequence == null || sequence.Count == 0)
                {
                    sourceInfo.tableauPile.SetAnimatingCard(false); // Снимаем блок при ошибке
                    return false;
                }

                if (sourceInfo.tableauPile.CheckAndFlipTop())
                {
                    sourceFlipped = true;
                    sourceFlippedIndex = sourceInfo.tableauPile.cards.Count - 1;
                }

                undoManager?.RecordMove(sequence, sourceInfo.tableauPile, targetTableau,
                    MakeParentsList(sequence, sourceInfo.tableauPile.transform),
                    MakeLocalPosList(sequence, sourceInfo.tableauIndex, tableauVerticalGap),
                    MakeSiblingList(sequence, sourceInfo.tableauIndex));

                if (sourceFlipped && undoManager != null)
                {
                    undoManager.RecordFlipInSource(sourceFlippedIndex);
                }

                // БЛОКИРУЕМ ЦЕЛЬ
                targetTableau.SetAnimatingCard(true);

                StartCoroutine(MoveSequenceToTableauWithSourceLock(sequence, targetTableau, sourceInfo.tableauPile));
                animationService?.ReorderContainerZ(sourceInfo.tableauPile.transform);
                break;

            case SourceType.Waste:
                Vector3 wastePosT = card.rectTransform.anchoredPosition;
                card = pileManager.WastePile.PopTop();
                if (card == null) return false;

                undoManager?.RecordMove(
                    new List<CardController> { card },
                    pileManager.WastePile,
                    targetTableau,
                    new List<Transform> { pileManager.WastePile.transform },
                    new List<Vector3> { wastePosT },
                    new List<int> { -1 }
                );

                // БЛОКИРУЕМ ЦЕЛЬ
                targetTableau.SetAnimatingCard(true);

                // ЗАПУСКАЕМ КОРУТИНУ С РАЗБЛОКИРОВКОЙ (вместо ForceSnap)
                StartCoroutine(AnimateSingleCardToTableau(card, targetTableau, pileManager.WastePile));

                animationService?.ReorderContainerZ(pileManager.WastePile.transform);
                break;

            case SourceType.Foundation:
                var fPile = sourceInfo.foundationPile;
                card = fPile.PopTop();
                undoManager?.RecordMove(new List<CardController> { card }, fPile, targetTableau,
                    new List<Transform> { fPile.transform }, new List<Vector3> { Vector3.zero }, new List<int> { -1 });

                targetTableau.SetAnimatingCard(true);
                StartCoroutine(AnimateSingleCardToTableau(card, targetTableau, fPile));

                animationService?.ReorderContainerZ(fPile.transform);
                break;

            case SourceType.Stock:
                card = pileManager.StockPile.PopTop();
                undoManager?.RecordMove(new List<CardController> { card }, pileManager.StockPile, targetTableau,
                   new List<Transform> { pileManager.StockPile.transform }, new List<Vector3> { Vector3.zero }, new List<int> { -1 });

                targetTableau.SetAnimatingCard(true);
                StartCoroutine(AnimateSingleCardToTableau(card, targetTableau, pileManager.StockPile));
                break;
        }
        return true;
    }


    #endregion

    #region Animation Coroutines

    private IEnumerator AnimateSingleCardToTableau(CardController card, TableauPile targetTableau, ICardContainer sourceContainer)
    {
        // 1. Подготовка
        if (this.dragLayer != null)
        {
            card.rectTransform.SetParent(this.dragLayer, true);
            card.rectTransform.SetAsLastSibling();
        }
        if (card.canvasGroup != null) card.canvasGroup.blocksRaycasts = false;

        // 2. Анимация (используем логику ForceSnap, но вручную, чтобы контролировать Unlock)
        Vector2 targetAnchored = targetTableau.GetDropAnchoredPosition(card);
        // Конвертация в World
        Vector3 targetWorld = animationService.AnchoredToWorldPosition(targetTableau.transform as RectTransform, targetAnchored);

        Vector3 startPos = card.rectTransform.position;
        float elapsed = 0f;
        float duration = 0.2f; // Длительность как у ForceSnap

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            card.rectTransform.position = Vector3.Lerp(startPos, targetWorld, t);
            yield return null;
        }

        // 3. Финал
        card.rectTransform.position = targetWorld;
        if (card.rectTransform.parent != targetTableau.transform)
        {
            card.rectTransform.SetParent(targetTableau.transform, true);
        }

        // Логическое добавление
        targetTableau.AddCard(card, true);

        // Восстанавливаем Raycast
        if (card.canvasGroup != null)
        {
            card.canvasGroup.blocksRaycasts = true;
            card.canvasGroup.interactable = true;
        }

        // Z-сортировка
        animationService?.ReorderContainerZ(targetTableau.transform);
        if (sourceContainer is Component sourceComp)
        {
            animationService?.ReorderContainerZ(sourceComp.transform);
        }

        Canvas.ForceUpdateCanvases();

        // 4. ГЛАВНОЕ: СНИМАЕМ БЛОКИРОВКУ
        targetTableau.SetAnimatingCard(false); // Включает Raycast обратно для всей стопки
        targetTableau.StartLayoutAnimation();
    }

    /// <summary>
    /// Автоматически отправляет все карты в Foundation.
    /// Используется, когда Stock/Waste пусты и все карты в Tableau открыты.
    /// </summary>
    public IEnumerator PlayAutoWinAnimation()
    {
        bool movedCard = true;

        // Пока мы находим карты для перемещения, продолжаем цикл
        while (movedCard)
        {
            movedCard = false;

            // Проходим по всем столбцам Tableau
            foreach (var tableau in pileManager.Tableau)
            {
                if (tableau == null || tableau.IsEmpty()) continue;

                var allCards = tableau.GetAllCards();
                if (allCards.Count == 0) continue;

                // Берем верхнюю карту
                CardController topCard = allCards[allCards.Count - 1];

                // Ищем Foundation, который её примет
                foreach (var foundation in pileManager.Foundations)
                {
                    if (foundation.CanAccept(topCard))
                    {
                        // Создаем фейковый SourceInfo для переиспользования логики
                        SourceInfo info = new SourceInfo
                        {
                            sourceType = SourceType.Tableau,
                            tableauPile = tableau,
                            tableauIndex = tableau.IndexOfCard(topCard),
                            isTopCard = true
                        };

                        // Используем существующий метод перемещения (он уже поддерживает DragLayer!)
                        bool success = ExecuteMoveToFoundation(topCard, info, foundation);

                        if (success)
                        {
                            movedCard = true;
                            // Ждем немного перед следующей картой (красивый эффект пулемета)
                            yield return new WaitForSeconds(0.05f);
                            goto NextIteration; // Прерываем foreach и начинаем искать заново (так надежнее)
                        }
                    }
                }
            }

        NextIteration:;
            // Если в этом проходе мы ничего не переместили — значит, либо победа, либо тупик (не должно быть при условиях AutoWin)
            if (!movedCard) break;
        }

        // Проверяем победу
        mode.CheckGameState();
    }


    private System.Collections.IEnumerator MoveSequenceToTableauWithSourceLock(List<CardController> sequence, TableauPile targetTableau, TableauPile sourceTableau)
    {
        if (sequence == null || sequence.Count == 0 || targetTableau == null)
        {
            // Если ошибка - обязательно разблокируем
            if (sourceTableau != null) sourceTableau.SetAnimatingCard(false);
            if (targetTableau != null) targetTableau.SetAnimatingCard(false);
            yield break;
        }

        Canvas.ForceUpdateCanvases();

        // 1. ПОДГОТОВКА: ПЕРЕНОСИМ В DRAG LAYER
        foreach (var card in sequence)
        {
            if (card == null) continue;
            card.StopAllCoroutines();

            if (this.dragLayer != null)
            {
                card.rectTransform.SetParent(this.dragLayer, true);
                card.rectTransform.SetAsLastSibling();
            }
            if (card.canvasGroup != null) card.canvasGroup.blocksRaycasts = false;
        }

        // Расчет позиций
        Vector2 topAnchor = targetTableau.GetDropAnchoredPosition(sequence[0]);
        List<Vector3> startWorldPositions = new List<Vector3>();
        foreach (var card in sequence) startWorldPositions.Add(card.rectTransform.position);

        List<Vector3> targetWorldPositions = new List<Vector3>();
        for (int i = 0; i < sequence.Count; i++)
        {
            Vector2 anchoredPos = new Vector2(topAnchor.x, topAnchor.y - i * tableauVerticalGap);
            Vector3 worldPos = animationService.AnchoredToWorldPosition(targetTableau.transform as RectTransform, anchoredPos);
            targetWorldPositions.Add(worldPos);
        }

        // Анимация
        float elapsed = 0f;
        while (elapsed < sequenceMoveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / sequenceMoveDuration));
            for (int i = 0; i < sequence.Count; i++)
            {
                if (sequence[i] != null)
                {
                    sequence[i].rectTransform.position = Vector3.Lerp(startWorldPositions[i], targetWorldPositions[i], t);
                }
            }
            yield return null;
        }

        // Финализация
        for (int i = 0; i < sequence.Count; i++)
        {
            if (sequence[i] != null)
            {
                sequence[i].rectTransform.position = targetWorldPositions[i];
                sequence[i].rectTransform.SetParent(targetTableau.transform, true);
                if (sequence[i].canvasGroup != null) sequence[i].canvasGroup.blocksRaycasts = true;
            }
        }

        targetTableau.AddCardsBatch(sequence, true);

        // --- ВАЖНОЕ ИСПРАВЛЕНИЕ: ПРАВИЛЬНАЯ РАЗБЛОКИРОВКА ---
        if (sourceTableau != null)
        {
            // Используем SetAnimatingCard(false) вместо UnlockLayout, чтобы вернуть Raycast
            sourceTableau.SetAnimatingCard(false);
            sourceTableau.FlipTopIfNeeded();
            sourceTableau.StartLayoutAnimation();
        }

        targetTableau.SetAnimatingCard(false); // Разблокируем целевую стопку
        targetTableau.StartLayoutAnimation();

        animationService?.ReorderContainerZ(targetTableau.transform);
        Canvas.ForceUpdateCanvases();
    }

    private void ShakeCardOrSequence(CardController card, SourceInfo sourceInfo)
    {
        List<CardController> toShake = new List<CardController>();

        if (sourceInfo.sourceType == SourceType.Tableau && sourceInfo.tableauPile != null)
        {
            toShake = sourceInfo.tableauPile.GetFaceUpSequenceFrom(sourceInfo.tableauIndex);
        }
        else
        {
            toShake.Add(card);
        }

        StartCoroutine(ShakeSequence(toShake));
    }

    private IEnumerator ShakeCard(CardController card)
    {
        yield return ShakeSequence(new List<CardController> { card });
    }

    private IEnumerator ShakeSequence(List<CardController> sequence)
    {
        if (sequence == null || sequence.Count == 0) yield break;

        List<Vector3> startPositions = new List<Vector3>();
        foreach (var card in sequence)
        {
            if (card != null)
            {
                startPositions.Add(card.rectTransform.anchoredPosition);
            }
            else
            {
                startPositions.Add(Vector3.zero);
            }
        }

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float phase = Mathf.Sin(elapsed * 40f) * (1f - elapsed / shakeDuration);

            for (int i = 0; i < sequence.Count; i++)
            {
                var card = sequence[i];
                if (card == null) continue;

                Vector3 start = startPositions[i];
                float offsetX = Mathf.Sin(elapsed * 60f + i) * shakeAmplitude * phase;
                card.rectTransform.anchoredPosition = start + new Vector3(offsetX, 0f, 0f);
            }

            yield return null;
        }

        // Возвращаем в исходные позиции
        for (int i = 0; i < sequence.Count; i++)
        {
            if (sequence[i] != null)
            {
                sequence[i].rectTransform.anchoredPosition = startPositions[i];
            }
        }
    }

    #endregion

    #region Helper Methods

    private ICardContainer GetSourceContainer(SourceInfo sourceInfo)
    {
        switch (sourceInfo.sourceType)
        {
            case SourceType.Tableau: return sourceInfo.tableauPile;
            case SourceType.Waste: return pileManager.WastePile;
            case SourceType.Stock: return pileManager.StockPile;
            case SourceType.Foundation: return sourceInfo.foundationPile;
            default: return null;
        }
    }

    private List<Transform> MakeParentsList(List<CardController> sequence, Transform parent)
    {
        var list = new List<Transform>();
        foreach (var card in sequence)
        {
            list.Add(parent);
        }
        return list;
    }

    private List<Vector3> MakeLocalPosList(List<CardController> sequence, int startIndex, float gap)
    {
        var list = new List<Vector3>();
        for (int i = 0; i < sequence.Count; i++)
        {
            list.Add(new Vector3(0f, -(startIndex + i) * gap, 0f));
        }
        return list;
    }

    private List<int> MakeSiblingList(List<CardController> sequence, int startIndex)
    {
        var list = new List<int>();
        for (int i = 0; i < sequence.Count; i++)
        {
            list.Add(startIndex + i);
        }
        return list;
    }

    #endregion
}