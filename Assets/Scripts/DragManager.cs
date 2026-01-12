// DragManager.cs [UNIVERSAL VERSION]
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DragManager : MonoBehaviour
{
    // --- ЗАМЕНА: Используем интерфейс ---
    private ICardGameMode mode;

    private PileManager pileManager;
    private UndoManager undoManager;
    private Canvas canvas;
    private RectTransform dragLayer;

    private List<ICardContainer> allContainers = new List<ICardContainer>();

    [Header("Drag Settings")]
    [SerializeField] private float maxContainerDistance = 300f;

    // State
    private CardController draggingTopCard = null;
    private List<CardController> draggingStack = new List<CardController>();
    private List<Transform> originalParents = new List<Transform>();
    private List<Vector3> originalLocalPositions = new List<Vector3>();
    private List<int> originalSiblingIndices = new List<int>();
    private List<Vector3> localOffsets = new List<Vector3>();
    private TableauPile sourceTableau = null;
    private ICardContainer sourceContainer = null;
    private int sourceIndex = -1;
  
    /// <summary>
    /// Инициализация DragManager.
    /// </summary>
    public void Initialize(ICardGameMode m, Canvas c, RectTransform dragLayerRt, UndoManager undo)
    {
        mode = m;
        canvas = c;
        dragLayer = dragLayerRt;
        pileManager = m?.PileManager;
        undoManager = undo; // UndoManager передаем явно или берем из контекста

        if (pileManager == null) pileManager = FindObjectOfType<PileManager>();
        PopulateContainers();
    }

    /// <summary>
    /// Получает все контейнеры из PileManager.
    /// </summary>
    private void PopulateContainers()
    {
        allContainers.Clear();
        if (pileManager != null)
        {
            var containers = pileManager.GetAllContainers();
            if (containers != null) allContainers.AddRange(containers);
        }
    }

    /// <summary>
    /// Регистрирует события для карты.
    /// </summary>
    public void RegisterCardEvents(CardController card)
    {
        if (card == null) return;
        card.OnPickedUp += OnCardPickedUp;
        card.OnDroppedToContainer += OnCardDroppedToContainer;
        card.OnDroppedToBoard += OnCardDroppedToBoardEvent;
        card.OnClicked += OnCardClicked;
        card.OnDoubleClick += OnCardDoubleClicked;
        card.OnLongPress += OnCardLongPressed;
        //card.modeManager = mode; // Убираем прямую ссылку, если она была жесткой
    }

    #region Card Pickup

    /// <summary>
    /// Вызывается когда карта подхватывается (начало drag).
    /// </summary>
    private void OnCardPickedUp(CardController card)
    {
        if (mode != null && !mode.IsInputAllowed) return;

        // Очищаем старое состояние
        ClearDraggingState();

        var sourceInfo = FindCardSourceInfo(card);
        if (!sourceInfo.found) return;

        // 1. Блокировка целевой стопки (куда летят карты)
        if (sourceInfo.tableauPile != null && sourceInfo.tableauPile.IsLocked)
        {
            return;
        }

        // 2. Проверка целостности стопки (Tableau)
        // Предотвращаем разрыв цепочки, если карты выше уже анимируются/летят
        if (sourceInfo.tableauPile != null)
        {
            // Проверяем все карты, начиная от той, которую хватаем, и до верха стопки
            for (int i = sourceInfo.index; i < sourceInfo.tableauPile.cards.Count; i++)
            {
                var c = sourceInfo.tableauPile.cards[i];
                // Если карта есть в списке, но её родитель не стопка (значит она в DragLayer/летит)
                if (c != null && c.transform.parent != sourceInfo.tableauPile.transform)
                {
                    return; // Блокируем взятие
                }
            }
        }

        // 3. --- ЛОГИКА FOUNDATION (ДОМА) ---
        if (sourceInfo.container is FoundationPile foundation)
        {
            // А. СПЕЦИФИКА FREECELL: Доставать карты из дома ЗАПРЕЩЕНО ПОЛНОСТЬЮ
            if (mode is FreeCellModeManager)
            {
                return; // Просто выходим, не начиная драг
            }

            // Б. ДЛЯ ВСЕХ ОСТАЛЬНЫХ (Klondike): Разрешаем, но только ВЕРХНЮЮ карту
            if (!foundation.IsCardOnTop(card))
            {
                return; // Если карта не верхняя - нельзя брать
            }
        }
        // ------------------------------------


        sourceTableau = sourceInfo.tableauPile;
        sourceContainer = sourceInfo.container;
        sourceIndex = sourceInfo.index;

        List<CardController> sequence = GetDraggableSequence(card, sourceInfo);
        if (sequence == null || sequence.Count == 0) return;

        foreach (var c in sequence)
        {
            if (c == null) continue;

            // 1. Жесткая остановка всех анимаций (включая тряску)
            c.StopAllCoroutines();

            // 2. Сброс поворота (если тряска вращала карту) - это критично для SetParent
            c.transform.localRotation = Quaternion.identity;
            c.transform.localScale = Vector3.one;

            // 3. Сброс смещения позиции
            if (sourceInfo.tableauPile != null)
            {
                var rt = c.rectTransform;
                Vector2 pos = rt.anchoredPosition;
                pos.x = 0; // Центрируем карту в стопке
                rt.anchoredPosition = pos;
            }
            else if (sourceInfo.container is FoundationPile || sourceInfo.container is WastePile)
            {
                c.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        // 4. ВАЖНО: Принудительно обновляем Canvas, чтобы Unity пересчитала 
        // мировые координаты (WorldPosition) после сброса AnchoredPosition.
        Canvas.ForceUpdateCanvases();

        if (dragLayer == null && canvas != null) dragLayer = canvas.transform as RectTransform;

        PrepareSequenceForDrag(sequence, sourceInfo);
        if (sequence.Count > 0) draggingTopCard = sequence[0];
    }

    /// <summary>
    /// Находит информацию об источнике карты.
    /// </summary>
    private struct SourceInfo
    {
        public bool found;
        public ICardContainer container;
        public TableauPile tableauPile;
        public int index;
    }

    private SourceInfo FindCardSourceInfo(CardController card)
    {
        var info = new SourceInfo { found = false, index = -1 };

        // Проверка Tableau
        if (pileManager?.Tableau != null)
        {
            foreach (var tableau in pileManager.Tableau)
            {
                if (tableau == null) continue;

                int idx = tableau.IndexOfCard(card);
                if (idx >= 0)
                {
                    info.found = true;
                    info.container = tableau;
                    info.tableauPile = tableau;
                    info.index = idx;
                    return info;
                }
            }
        }

        // Проверка Waste
        if (pileManager?.WastePile != null && pileManager.WastePile.ContainsCard(card))
        {
            info.found = true;
            info.container = pileManager.WastePile;
            return info;
        }

        // Проверка Foundation
        if (pileManager?.Foundations != null)
        {
            foreach (var foundation in pileManager.Foundations)
            {
                if (foundation == null) continue;

                if (foundation.IsCardOnTop(card))
                {
                    info.found = true;
                    info.container = foundation;
                    return info;
                }
            }
        }
        if (pileManager is FreeCellPileManager fcManager)
        {
            foreach (var fc in fcManager.FreeCells)
            {
                // Проверяем, лежит ли карта в этой ячейке
                if (fc.GetComponentInChildren<CardController>() == card)
                {
                    info.found = true;
                    info.container = fc;
                    return info;
                }
            }
        }
        // Или если pileManager - это обычный PileManager, но мы знаем что контейнер может быть FreeCellPile
        if (card.transform.parent != null)
        {
            var fc = card.transform.parent.GetComponent<FreeCellPile>();
            if (fc != null)
            {
                info.found = true;
                info.container = fc;
                return info;
            }
        }
        return info;
    }

    /// <summary>
    /// Получает последовательность карт, которую можно перетаскивать.
    /// </summary>
    private List<CardController> GetDraggableSequence(CardController card, SourceInfo sourceInfo)
    {
        List<CardController> sequence = new List<CardController>();

        // Если источник - Tableau, берём последовательность лицевых карт
        if (sourceInfo.tableauPile != null && sourceInfo.index >= 0)
        {
            sequence = sourceInfo.tableauPile.GetFaceUpSequenceFrom(sourceInfo.index);
        }
        else
        {
            // Для остальных источников - только одна карта
            sequence.Add(card);
        }

        // Проверяем что все карты в последовательности лицом вверх
        for (int i = sequence.Count - 1; i >= 0; i--)
        {
            var cardData = sequence[i].GetComponent<CardData>();
            if (cardData == null || !cardData.IsFaceUp())
            {
                sequence.RemoveRange(i, sequence.Count - i);
                break;
            }
        }

        if (sequence.Count == 0)
        {
            sequence.Add(card);
        }
        if (mode is FreeCellModeManager freeCellMode)
        {
            int limit = freeCellMode.GetMaxDragSequenceSize();
            // Если пачка больше лимита, берем только верхнюю карту или запрещаем
            if (sequence.Count > limit)
            {
                // Возвращаем только одну карту (самую верхнюю), либо null чтобы запретить совсем
                return null;
            }
        }
        return sequence;
    }

    /// <summary>
    /// Подготавливает последовательность для перетаскивания.
    /// </summary>
    private void PrepareSequenceForDrag(List<CardController> sequence, SourceInfo sourceInfo)
    {
        originalParents.Clear();
        originalLocalPositions.Clear();
        originalSiblingIndices.Clear();
        localOffsets.Clear();
        draggingStack.Clear();

        // Используем dragLayer или canvas как fallback
        RectTransform targetDragLayer = dragLayer ?? (canvas != null ? canvas.transform as RectTransform : null);

        // 1) СОХРАНЯЕМ ВСЕ ДАННЫЕ ПЕРЕД ПЕРЕМЕЩЕНИЕМ
        for (int i = 0; i < sequence.Count; i++)
        {
            var card = sequence[i];
            if (card == null) continue;

            // Сохраняем parent (для tableau сохраняем tableau.transform)
            Transform savedParent = card.rectTransform.parent;
            if (sourceInfo.tableauPile != null)
            {
                savedParent = sourceInfo.tableauPile.transform;
            }
            originalParents.Add(savedParent);

            // Сохраняем anchoredPosition (если RectTransform) и z
            var rt = card.rectTransform;
            Vector3 savedLocal = Vector3.zero;
            if (rt != null)
            {
                // Сохраняем anchoredPosition.x/y и localPosition.z
                savedLocal.x = rt.anchoredPosition.x;
                savedLocal.y = rt.anchoredPosition.y;
                savedLocal.z = rt.localPosition.z;
            }
            originalLocalPositions.Add(savedLocal);

            // СОХРАНЯЕМ SIBLING INDEX ПЕРЕД ПЕРЕМЕЩЕНИЕМ (КЛЮЧЕВОЕ ИЗМЕНЕНИЕ!)
            originalSiblingIndices.Add(rt.GetSiblingIndex());

            draggingStack.Add(card);
        }

        // 2) ТЕПЕРЬ ПЕРЕМЕЩАЕМ ВСЕ КАРТЫ В DRAG LAYER
        for (int i = 0; i < draggingStack.Count; i++)
        {
            var card = draggingStack[i];
            if (card == null) continue;

            // Получаем мировую позицию ДО смены parent
            Vector3 worldPos = card.rectTransform.position;

            if (targetDragLayer != null)
                card.rectTransform.SetParent(targetDragLayer, false);  // false чтобы не менять позицию

            // Устанавливаем мировую позицию
            card.rectTransform.position = worldPos;

            // Сбрасываем Z локально в dragLayer
            Vector3 lp = card.rectTransform.localPosition;
            lp.z = 0f;
            card.rectTransform.localPosition = lp;

            // Отключаем raycast
            if (card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = false;
            }
        }

        // 3) Вычисляем локальные оффсеты относительно ведущей карты (в dragLayer)
        if (draggingStack.Count > 0)
        {
            Vector3 topLocal = draggingStack[0].rectTransform.localPosition;
            foreach (var card in draggingStack)
            {
                localOffsets.Add(card.rectTransform.localPosition - topLocal);
            }
        }
    }

    #endregion

    #region Update Loop

    /// <summary>
    /// Обновляет позиции перетаскиваемых карт относительно ведущей карты.
    /// </summary>
    private void Update()
    {
        if (draggingTopCard != null && draggingStack.Count > 0 && localOffsets.Count == draggingStack.Count)
        {
            Vector3 topLocal = draggingTopCard.rectTransform.localPosition;
            for (int i = 0; i < draggingStack.Count; i++)
            {
                if (draggingStack[i] != null) draggingStack[i].rectTransform.localPosition = topLocal + localOffsets[i];
            }
        }
    }

    #endregion

    #region Container Finding

    /// <summary>
    /// Находит ближайший контейнер, который может принять карту.
    /// </summary>
    public ICardContainer FindNearestContainer(CardController card, Vector2 screenPointOrAnchored, float maxDistance)
    {
        if (card == null) return null;
        if (allContainers == null || allContainers.Count == 0) RefreshContainers();

        // 1. Получаем границы перетаскиваемой карты в мировых координатах
        Rect cardRect = GetWorldRect(card.rectTransform);

        ICardContainer bestContainer = null;
        float maxArea = 0f;

        foreach (var container in allContainers)
        {
            // --- ВАЖНЫЕ ПРОВЕРКИ ---
            // 1. Не ищем тот контейнер, откуда взяли карту (чтобы не сломать Undo)
            if (container == sourceContainer) continue;

            // 2. Не ищем контейнер, если карта уже визуально является его дочерним объектом
            if (card.transform.parent == container.Transform) continue;

            // 3. Специфичные проверки (как у вас были)
            if (container is FoundationPile && draggingStack.Count > 1) continue;
            if (container is TableauPile tp && tp.IsLocked) continue;
            if (container is FreeCellPile && draggingStack.Count > 1) continue;

            // --- ОПРЕДЕЛЕНИЕ ЦЕЛИ (Target) ---
            RectTransform targetTransform = container.Transform as RectTransform;

            // ХИТРОСТЬ: Если это Стопка (Tableau) и в ней есть карты,
            // мы проверяем пересечение с ПОСЛЕДНЕЙ КАРТОЙ (визуально нижней).
            if (container is TableauPile tableau)
            {
                var cards = tableau.GetAllCards();
                if (cards != null && cards.Count > 0)
                {
                    var lastCard = cards[cards.Count - 1];
                    if (lastCard != null) targetTransform = lastCard.rectTransform;
                }
            }
            // Поддержка других типов стопок (на всякий случай)
            else if (container.Transform.childCount > 0 && !(container is FoundationPile) && !(container is FreeCellPile))
            {
                var lastChild = container.Transform.GetChild(container.Transform.childCount - 1);
                if (lastChild.GetComponent<CardController>() != null)
                {
                    targetTransform = lastChild as RectTransform;
                }
            }

            if (targetTransform == null) continue;

            // --- РАСЧЕТ ПЕРЕСЕЧЕНИЯ ---
            Rect targetRect = GetWorldRect(targetTransform);
            float area = GetIntersectionArea(cardRect, targetRect);

            // Если есть пересечение и оно больше, чем у предыдущего кандидата
            if (area > 0 && area > maxArea)
            {
                // Проверяем правила игры ТОЛЬКО если есть визуальное наложение
                if (container.CanAccept(card))
                {
                    maxArea = area;
                    bestContainer = container;
                }
            }
        }

        return bestContainer;
    }

    /// <summary>
    /// Обрабатывает drop на доску (не на конкретный контейнер).
    /// </summary>
    public bool OnDropToBoard(CardController card, Vector2 anchoredPosition)
    {
        // --- СТАРАЯ ЛОГИКА (удаляем/комментируем) ---
        // if (pileManager?.Tableau == null) return false;
        // foreach ... RectContainsPoint ...

        // --- НОВАЯ ЛОГИКА (по пересечению) ---
        // 1. Ищем контейнер, с которым карта пересекается визуально
        ICardContainer target = FindBestReceiver(card);

        // 2. Если нашли
        if (target != null)
        {
            // CanAccept уже проверен внутри FindBestReceiver, но для надежности можно повторить
            if (target.CanAccept(card))
            {
                // Обязательно вызываем AcceptCard для логического обновления стопки
                target.AcceptCard(card);

                // Запускаем процесс переноса (Undo, статистика, анимация)
                OnCardDroppedToContainer(card, target);
                return true;
            }
        }

        return false;
    }

    private bool RectContainsPoint(RectTransform rect, Vector2 anchoredPos)
    {
        if (rect == null) return false;

        Rect r = rect.rect;
        Vector2 pos = rect.anchoredPosition;

        return (anchoredPos.x >= r.xMin + pos.x &&
                anchoredPos.x <= r.xMax + pos.x &&
                anchoredPos.y >= r.yMin + pos.y &&
                anchoredPos.y <= r.yMax + pos.y);
    }

    #endregion

    #region Card Drop Handling

    /// <summary>
    /// Обрабатывает drop карты на доску (не попали в контейнер).
    /// </summary>
    private void OnCardDroppedToBoardEvent(CardController card)
    {
        if (draggingStack != null && draggingStack.Count > 0)
        {
            // 1. ДЕЛАЕМ СНИМОК СОСТОЯНИЯ (Копируем все списки)
            var snapshot = new DragSnapshot
            {
                cards = new List<CardController>(draggingStack),
                parents = new List<Transform>(originalParents),
                positions = new List<Vector3>(originalLocalPositions),
                siblings = new List<int>(originalSiblingIndices)
            };

            // 2. СРАЗУ ОЧИЩАЕМ ГЛОБАЛЬНОЕ СОСТОЯНИЕ
            // Теперь игрок может брать следующую карту, пока эти летят обратно
            ClearDraggingState();

            // 3. ЗАПУСКАЕМ АНИМАЦИЮ, ПЕРЕДАВАЯ ЕЙ СНИМОК
            StartCoroutine(AnimateReturnDraggingStack(snapshot));
        }
        else
        {
            ClearDraggingState();
        }
    }
    private class DragSnapshot
    {
        public List<CardController> cards;
        public List<Transform> parents;
        public List<Vector3> positions;
        public List<int> siblings;
    }


    private System.Collections.IEnumerator AnimateReturnDraggingStack(DragSnapshot data)
    {
        if (data == null || data.cards == null || data.cards.Count == 0) yield break;

        // 1. БЛОКИРУЕМ СТОПКИ-ИСТОЧНИКИ
        HashSet<TableauPile> lockedPiles = new HashSet<TableauPile>();
        foreach (var parentTransform in data.parents)
        {
            if (parentTransform != null)
            {
                var tableau = parentTransform.GetComponent<TableauPile>();
                if (tableau != null) lockedPiles.Add(tableau);
            }
        }

        foreach (var pile in lockedPiles) pile.SetAnimatingCard(true);

        // Подготовка (отключаем лучи летящим)
        foreach (var c in data.cards)
        {
            if (c != null && c.canvasGroup != null) c.canvasGroup.blocksRaycasts = false;
        }

        RectTransform layer = dragLayer ?? (mode?.RootCanvas?.transform as RectTransform);
        List<Vector3> startWorldPositions = new List<Vector3>();

        foreach (var card in data.cards)
        {
            if (card != null)
            {
                card.StopAllCoroutines();
                startWorldPositions.Add(card.rectTransform.position);
                if (layer != null && card.rectTransform.parent != layer)
                {
                    card.rectTransform.SetParent(layer, true);
                }
                if (layer != null) card.rectTransform.SetAsLastSibling();
            }
            else
            {
                startWorldPositions.Add(Vector3.zero);
            }
        }

        Canvas.ForceUpdateCanvases();

        // Расчет позиций
        var animSvc = mode?.AnimationService;
        List<Vector3> targetWorldPositions = new List<Vector3>();

        for (int i = 0; i < data.cards.Count; i++)
        {
            Vector3 savedLocal = (i < data.positions.Count) ? data.positions[i] : Vector3.zero;
            Transform savedParent = (i < data.parents.Count) ? data.parents[i] : null;
            Vector3 targetWorld = Vector3.zero;

            if (savedParent != null && savedParent is RectTransform parentRt)
            {
                Vector2 anchored = new Vector2(savedLocal.x, savedLocal.y);
                if (animSvc != null) targetWorld = animSvc.AnchoredToWorldPosition(parentRt, anchored);
                else
                {
                    // Fallback
                    GameObject tmp = new GameObject("TMP_Return");
                    tmp.transform.SetParent(parentRt, false);
                    var rt = tmp.AddComponent<RectTransform>();
                    rt.anchoredPosition = anchored;
                    Canvas.ForceUpdateCanvases();
                    targetWorld = rt.position;
                    Destroy(tmp);
                }
            }
            else if (data.cards[i] != null)
            {
                targetWorld = data.cards[i].rectTransform.position;
            }
            targetWorldPositions.Add(targetWorld);
        }

        // Анимация полета
        float duration = 0.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            for (int i = 0; i < data.cards.Count; i++)
            {
                if (data.cards[i] != null)
                {
                    data.cards[i].rectTransform.position = Vector3.Lerp(startWorldPositions[i], targetWorldPositions[i], t);
                }
            }
            yield return null;
        }

        // Финализация (возврат родителя)
        HashSet<TableauPile> affectedTableaus = new HashSet<TableauPile>();

        for (int i = 0; i < data.cards.Count; i++)
        {
            var card = data.cards[i];
            if (card == null) continue;

            Transform savedParent = (i < data.parents.Count) ? data.parents[i] : null;
            Vector3 savedLocal = (i < data.positions.Count) ? data.positions[i] : Vector3.zero;

            if (savedParent != null)
            {
                card.rectTransform.SetParent(savedParent, true);
                var tab = savedParent.GetComponent<TableauPile>();
                if (tab != null) affectedTableaus.Add(tab);
            }

            card.rectTransform.anchoredPosition = new Vector2(savedLocal.x, savedLocal.y);
            Vector3 lp = card.rectTransform.localPosition;
            lp.z = savedLocal.z;
            card.rectTransform.localPosition = lp;

            if (i < data.siblings.Count && data.siblings[i] >= 0)
            {
                int count = savedParent != null ? savedParent.childCount : 0;
                int idx = Mathf.Clamp(data.siblings[i], 0, Mathf.Max(0, count));
                card.rectTransform.SetSiblingIndex(idx);
            }

            // Включаем Raycast для самих карт
            if (card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = true;
                card.canvasGroup.interactable = true;
            }
        }

        if (mode?.AnimationService != null)
        {
            HashSet<Transform> parentsToUpdate = new HashSet<Transform>();
            foreach (var p in data.parents) if (p != null) parentsToUpdate.Add(p);
            foreach (var p in parentsToUpdate) mode.AnimationService.ReorderContainerZ(p);
        }

        // --- ИСПРАВЛЕНИЕ: ПОРЯДОК РАЗБЛОКИРОВКИ ---

        // 1. Сначала СНИМАЕМ БЛОКИРОВКУ со стопок
        // Теперь стопки снова интерактивны и могут запускать свои внутренние анимации
        foreach (var pile in lockedPiles)
        {
            pile.SetAnimatingCard(false);
        }

        // 2. Теперь запускаем выравнивание
        // Так как стопки разблокированы, StartLayoutAnimationPublic сработает корректно
        foreach (var tab in affectedTableaus)
        {
            tab.StartLayoutAnimationPublic();
        }
    }

    /// <summary>
    /// Обрабатывает drop карты в контейнер.
    /// </summary>

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        // 1. Базовые проверки
        if (draggingStack == null || draggingStack.Count == 0)
        {
            // Обработка одиночной карты без драг-сессии (на всякий случай)
            if (card != null && container != null && card.transform.parent != container.Transform)
            {
                var cardData = card.GetComponent<CardData>();
                cardData?.SetFaceUp(true);
                card.rectTransform.SetParent(container.Transform, false);
                card.rectTransform.anchoredPosition = container.GetDropAnchoredPosition(card);

                if (container is TableauPile tableau) tableau.AddCard(card, true);
                else if (container is FoundationPile foundation) foundation.AcceptCard(card);
                else if (container is WastePile waste) waste.AddCard(card, true);
                else if (container is FreeCellPile fc) fc.AcceptCard(card);

                // Даже одиночный перенос считается ходом
                NotifyGameModeOnMove(); // <--- ДОБАВЛЕНО
            }
            ClearDraggingState();
            return;
        }

        if (draggingStack[0] != card)
        {
            OnCardDroppedToBoardEvent(card);
            return;
        }

        var scoreMgr = FindObjectOfType<FreeCellScoreManager>();

        // 2. ИЗЪЯТИЕ КАРТ
        List<CardController> removedSequence = null;
        bool sourceFlipped = false;
        int sourceFlippedIndex = -1;

        if (sourceTableau != null && sourceIndex >= 0)
        {
            removedSequence = sourceTableau.RemoveSequenceFrom(sourceIndex);
            if (sourceTableau.CheckAndFlipTop())
            {
                sourceFlipped = true;
                sourceFlippedIndex = sourceTableau.cards.Count - 1;
            }
        }
        else if (sourceContainer is WastePile waste)
        {
            waste.PopTop();
        }
        else if (sourceContainer is FoundationPile foundation)
        {
            var removedCard = foundation.PopTop();
            if (removedCard != null)
            {
                removedSequence = new List<CardController> { removedCard };
                foundation.ForceRemove(removedCard);
            }
        }

        // ==========================================================================================
        // ЦЕЛЬ: TABLEAU
        // ==========================================================================================
        if (container is TableauPile targetTableau)
        {
            targetTableau.SetAnimatingCard(true);

            RecordMoveToUndo(removedSequence ?? draggingStack, container);

            // >>> СТАТИСТИКА: ВЫЗОВ НОВОГО МЕТОДА <<<
            NotifyGameModeOnMove();
            // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>

            if (sourceFlipped && undoManager != null) undoManager.RecordFlipInSource(sourceFlippedIndex);

            if (scoreMgr != null) scoreMgr.OnCardMove(sourceContainer, targetTableau);

            var cardsToFly = new List<CardController>(draggingStack);
            ClearDraggingState();
            mode?.CheckGameState();
            StartCoroutine(AnimateSequenceToTableau(cardsToFly, targetTableau));
            return;
        }

        // ==========================================================================================
        // ЦЕЛЬ: FOUNDATION
        // ==========================================================================================
        else if (container is FoundationPile targetFoundation)
        {
            var firstCard = draggingStack[0];

            if (sourceContainer is FoundationPile sourceFoundation) sourceFoundation.ForceRemove(firstCard);

            firstCard.ForceSnapToContainer(container);

            if (firstCard.canvasGroup != null)
            {
                firstCard.canvasGroup.blocksRaycasts = true;
                firstCard.canvasGroup.interactable = true;
            }

            if (firstCard.transform.parent == targetFoundation.transform) targetFoundation.AcceptCard(firstCard);

            if (draggingStack.Count > 1) ReturnExtraCardsToSource(1);

            if (undoManager != null)
            {
                undoManager.RecordMove(
                    new List<CardController> { firstCard },
                    sourceContainer, targetFoundation,
                    new List<Transform> { sourceContainer?.Transform },
                    new List<Vector3> { Vector3.zero }, new List<int> { -1 }
                );
            }

            // >>> СТАТИСТИКА <<<
            NotifyGameModeOnMove();
            // >>>>>>>>>>>>>>>>>

            if (sourceFlipped && undoManager != null) undoManager.RecordFlipInSource(sourceFlippedIndex);

            var anim = mode?.AnimationService;
            anim?.ReorderContainerZ(targetFoundation.transform);
            anim?.ReorderContainerZ(sourceContainer?.Transform);

            if (scoreMgr != null) scoreMgr.OnCardMove(sourceContainer, targetFoundation);

            mode?.CheckGameState();
            ClearDraggingState();
            return;
        }

        // ==========================================================================================
        // ЦЕЛЬ: FREE CELL
        // ==========================================================================================
        else if (container is FreeCellPile freeCell)
        {
            if (draggingStack.Count > 1)
            {
                OnCardDroppedToBoardEvent(card);
                return;
            }

            CardController cardToDrop = draggingStack[0];
            cardToDrop.ForceSnapToContainer(container);

            if (cardToDrop.canvasGroup != null)
            {
                cardToDrop.canvasGroup.blocksRaycasts = true;
                cardToDrop.canvasGroup.interactable = true;
            }

            freeCell.AcceptCard(cardToDrop);
            RecordMoveToUndo(draggingStack, container);

            // >>> СТАТИСТИКА <<<
            NotifyGameModeOnMove();
            // >>>>>>>>>>>>>>>>>

            if (sourceFlipped && undoManager != null)
            {
                undoManager.RecordFlipInSource(sourceFlippedIndex);
            }

            if (scoreMgr != null) scoreMgr.OnCardMove(sourceContainer, freeCell);

            mode?.CheckGameState();
            ClearDraggingState();
            return;
        }

        // ==========================================================================================
        // ИНАЧЕ
        // ==========================================================================================
        else
        {
            OnCardDroppedToBoardEvent(card);
        }
    }
    /*
    // Вспомогательный метод для обработки переворота (чтобы не дублировать код)
    private void HandleSourceFlip()
    {
        // Убеждаемся, что sourceTableau актуален (на случай если драг был из Tableau)
        if (sourceTableau == null && sourceContainer is TableauPile tp)
        {
            sourceTableau = tp;
        }

        if (sourceTableau != null)
        {
            // Пытаемся перевернуть верхнюю карту. 
            // CheckAndFlipTop вернет true ТОЛЬКО если карта была закрыта и стала открытой.
            bool flipped = sourceTableau.CheckAndFlipTop();

            if (flipped && undoManager != null)
            {
                // Записываем в последний ход UndoStack информацию о перевороте.
                // Используем cards.Count - 1, так как карта уже перевернута и находится на верху стопки.
                undoManager.RecordFlipInSource(sourceTableau.cards.Count - 1);
            }
        }
    }
    */

    /// <summary>
    /// Возвращает "лишние" карты обратно в источник (для Foundation drop).
    /// </summary>
    private void ReturnExtraCardsToSource(int startIndex)
    {
        if (sourceTableau != null)
        {
            for (int i = startIndex; i < draggingStack.Count; i++)
            {
                var c = draggingStack[i];
                if (c == null) continue;

                c.rectTransform.SetParent(sourceTableau.transform, false);
                sourceTableau.AddCard(c, true);

                if (c.canvasGroup != null)
                {
                    c.canvasGroup.blocksRaycasts = true;
                }
            }

            sourceTableau.FlipTopIfNeeded();
        }
        else if (sourceContainer is WastePile waste)
        {
            for (int i = startIndex; i < draggingStack.Count; i++)
            {
                var c = draggingStack[i];
                if (c == null) continue;

                c.rectTransform.SetParent(waste.transform, false);
                waste.AddCard(c, true);

                if (c.canvasGroup != null)
                {
                    c.canvasGroup.blocksRaycasts = true;
                }
            }
        }
    }

    /// <summary>
    /// Возвращает всю перетаскиваемую последовательность в исходное положение.
    /// Исправляет проблему перевёрнутого порядка карточек при возврате в тот же Tableau.
    /// </summary>
    public void ReturnDraggingStackToOrigin()
    {
        if (draggingStack == null || draggingStack.Count == 0)
            return;

        // Список для запоминания уникальных контейнеров
        var affectedParents = new HashSet<Transform>();

        // 1) Восстанавливаем parent / anchored / z
        for (int i = 0; i < draggingStack.Count; i++)
        {
            var card = draggingStack[i];
            if (card == null) continue;

            // restore parent
            if (i < originalParents.Count && originalParents[i] != null)
            {
                card.rectTransform.SetParent(originalParents[i], false);
                affectedParents.Add(originalParents[i]);
            }

            // restore anchoredPosition.x/y and local z
            if (i < originalLocalPositions.Count)
            {
                Vector3 saved = originalLocalPositions[i];
                try
                {
                    card.rectTransform.anchoredPosition = new Vector2(saved.x, saved.y);
                }
                catch { }

                Vector3 lp = card.rectTransform.localPosition;
                lp.z = saved.z;
                card.rectTransform.localPosition = lp;
            }

            // temporarily disable raycast
            if (card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = false;
            }
        }

        // 2) Восстанавливаем sibling indices в правильном порядке
        for (int i = 0; i < draggingStack.Count; i++)
        {
            var card = draggingStack[i];
            if (card == null) continue;

            if (i < originalSiblingIndices.Count && originalSiblingIndices[i] >= 0)
            {
                var parent = card.rectTransform.parent;
                int desired = originalSiblingIndices[i];
                int childCount = parent != null ? parent.childCount : 0;
                int clamped = Mathf.Clamp(desired, 0, Mathf.Max(0, childCount));
                card.rectTransform.SetSiblingIndex(clamped);
            }
        }

        // 3) Включаем raycast для всех карт
        foreach (var card in draggingStack)
        {
            if (card == null) continue;
            if (card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = true;
            }
        }

        // 4) Обновляем контейнеры
        var animSvc = mode?.AnimationService;
        foreach (var parent in affectedParents)
        {
            if (parent == null) continue;
            var tableau = parent.GetComponent<TableauPile>();
            if (tableau != null)
            {
                tableau.ForceUpdateFromTransform();
            }
            var foundation = parent.GetComponent<FoundationPile>();
            if (foundation != null)
            {
                foundation.ForceUpdateFromTransform();
            }
            if (animSvc != null)
            {
                animSvc.ReorderContainerZ(parent);
            }
        }

        Canvas.ForceUpdateCanvases();

        // 5) Очистка
        draggingStack.Clear();
        localOffsets.Clear();
        originalParents.Clear();
        originalLocalPositions.Clear();
        originalSiblingIndices.Clear();
        sourceTableau = null;
        sourceContainer = null;
        sourceIndex = -1;
        draggingTopCard = null;
    }

    

    /// <summary>
    /// Записывает ход в UndoManager.
    /// </summary>
    private void RecordMoveToUndo(List<CardController> cards, ICardContainer targetContainer)
    {
        if (undoManager == null) return;

        undoManager.RecordMove(
            cards,
            sourceContainer,
            targetContainer,
            new List<Transform>(originalParents),
            new List<Vector3>(originalLocalPositions),
            new List<int>(originalSiblingIndices)
        );
    }

    /// <summary>
    /// Очищает состояние перетаскивания.
    /// </summary>
    private void ClearDraggingState()
    {
        // Включаем raycast для всех карт
        if (draggingStack != null)
        {
            foreach (var card in draggingStack)
            {
                if (card != null && card.canvasGroup != null)
                {
                    card.canvasGroup.blocksRaycasts = true;
                }
            }
        }

        draggingStack.Clear();
        originalParents.Clear();
        originalLocalPositions.Clear();
        originalSiblingIndices.Clear();
        localOffsets.Clear();
        sourceTableau = null;
        sourceContainer = null;
        sourceIndex = -1;
        draggingTopCard = null;
    }

    #endregion

    #region Card Click Events

    /// <summary>
    /// Обрабатывает обычный клик по карте.
    /// </summary>
    public void OnCardClicked(CardController card)
    {
        // Если карта в Stock - делегируем обработку в GameMode
        if (pileManager?.StockPile != null && pileManager.StockPile.ContainsCard(card))
        {
            if (card.transform.parent != pileManager.StockPile.transform) return;

            // Вызываем универсальный метод
            mode?.OnStockClicked();
            return;
        }
    }
    /// <summary>
    /// Пытается переместить карту из Foundation в Tableau.
    /// </summary>
    private void TryMoveCardFromFoundation(CardController card, FoundationPile sourceFoundation)
    {
        if (card == null || pileManager?.Tableau == null) return;

        // Ищем подходящий Tableau
        foreach (var tableau in pileManager.Tableau)
        {
            if (tableau != null && tableau.CanAccept(card))
            {
                // Удаляем из Foundation
                var removedCard = sourceFoundation.PopTop();
                if (removedCard == null || removedCard != card) continue;

                // Добавляем в Tableau
                removedCard.ForceSnapToContainer(tableau);

                // Обновляем Z-сортировку
                var animService = mode?.AnimationService;
                animService?.ReorderContainerZ(sourceFoundation.transform);
                animService?.ReorderContainerZ(tableau.transform);

                // Записываем в Undo
                undoManager?.RecordMove(
                    new List<CardController> { removedCard },
                    sourceFoundation,
                    tableau,
                    new List<Transform> { sourceFoundation.transform },
                    new List<Vector3> { Vector3.zero },
                    new List<int> { -1 }
                );

                return;
            }
        }

        // Если не нашли подходящего Tableau - возвращаем карту в Foundation
        sourceFoundation.AcceptCard(card);
    }

    /// <summary>
    /// Обрабатывает двойной клик по карте.
    /// </summary>
    public void OnCardDoubleClicked(CardController card)
    {
        if (mode != null)
        {
            mode.OnCardDoubleClicked(card);
        }
    }

    /// <summary>
    /// Обрабатывает долгое нажатие на карту.
    /// </summary>
    public void OnCardLongPressed(CardController card)
    {
        // Подсвечиваем все контейнеры, которые могут принять эту карту
        foreach (var container in allContainers)
        {
            if (container != null && container.CanAccept(card))
            {
                container.OnCardIncoming(card);
            }
        }
    }

    /// <summary>
    /// Обработка клавиатурного выбора карты.
    /// </summary>
    public void OnKeyboardPick(CardController card)
    {
        OnCardClicked(card);
    }

    #endregion

    #region Stock Operations





    #endregion

    #region Sequence Animation

    /// <summary>
    /// Анимирует перемещение всей последовательности карт в целевой tableau.
    /// </summary>

    private System.Collections.IEnumerator AnimateSequenceToTableau(List<CardController> sequence, TableauPile targetTableau)
    {
        if (sequence == null || sequence.Count == 0 || targetTableau == null)
        {
            // Если анимация не состоялась, нужно обязательно разблокировать стопку!
            if (targetTableau != null) targetTableau.SetAnimatingCard(false);
            yield break;
        }

        // --- СТРОКУ targetTableau.SetAnimatingCard(true) УДАЛЯЕМ ОТСЮДА ---
        // (Мы уже вызвали её в OnCardDroppedToContainer)

        Canvas.ForceUpdateCanvases();

        // 1. ПОДГОТОВКА: Отключаем лучи у летящих карт
        RectTransform layer = dragLayer ?? (mode?.RootCanvas?.transform as RectTransform);
        List<Vector3> startWorldPositions = new List<Vector3>();

        foreach (var c in sequence)
        {
            if (c == null) continue;
            c.StopAllCoroutines();
            startWorldPositions.Add(c.rectTransform.position);

            if (layer != null)
            {
                c.rectTransform.SetParent(layer, true);
                c.rectTransform.SetAsLastSibling();
            }

            if (c.canvasGroup != null) c.canvasGroup.blocksRaycasts = false;
        }

        // 2. РАСЧЕТ ПОЗИЦИЙ
        Vector2 topAnchor = targetTableau.GetDropAnchoredPosition(sequence[0]);
        List<Vector3> targetWorldPositions = new List<Vector3>();
        float gap = mode != null ? mode.TableauVerticalGap : 40f;
        var animSvc = mode?.AnimationService;

        for (int i = 0; i < sequence.Count; i++)
        {
            Vector2 anc = new Vector2(topAnchor.x, topAnchor.y - i * gap);
            Vector3 world = Vector3.zero;

            if (animSvc != null)
            {
                world = animSvc.AnchoredToWorldPosition(targetTableau.transform as RectTransform, anc);
            }
            else
            {
                GameObject t = new GameObject("tmp");
                t.transform.SetParent(targetTableau.transform, false);
                t.AddComponent<RectTransform>().anchoredPosition = anc;
                Canvas.ForceUpdateCanvases();
                world = t.transform.position;
                Destroy(t);
            }
            targetWorldPositions.Add(world);
        }

        // 3. АНИМАЦИЯ
        float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            for (int i = 0; i < sequence.Count; i++)
            {
                if (sequence[i] != null)
                {
                    sequence[i].rectTransform.position = Vector3.Lerp(startWorldPositions[i], targetWorldPositions[i], t);
                }
            }
            yield return null;
        }

        // 4. ФИНАЛИЗАЦИЯ
        for (int i = 0; i < sequence.Count; i++)
        {
            var card = sequence[i];
            if (card == null) continue;

            card.rectTransform.position = targetWorldPositions[i];

            if (card.rectTransform.parent != targetTableau.transform)
            {
                card.rectTransform.SetParent(targetTableau.transform, true);
            }

            if (card.canvasGroup != null)
            {
                card.canvasGroup.blocksRaycasts = true;
                card.canvasGroup.interactable = true;
            }
        }

        targetTableau.AddCardsBatch(sequence, true);

        if (animSvc != null) animSvc.ReorderContainerZ(targetTableau.transform);

        Canvas.ForceUpdateCanvases();

        // --- РАЗБЛОКИРУЕМ ЦЕЛЕВУЮ СТОПКУ ---
        // Анимация завершена, включаем Raycast обратно для карт в стопке
        targetTableau.SetAnimatingCard(false);

        targetTableau.StartLayoutAnimation();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Регистрирует список контейнеров вручную (опционально).
    /// </summary>
    public void RegisterAllContainers(IEnumerable<ICardContainer> containers)
    {
        allContainers.Clear();
        if (containers != null)
        {
            allContainers.AddRange(containers);
        }
    }
    private ICardContainer FindBestReceiver(CardController card)
    {
        if (allContainers == null) return null;

        Rect cardRect = GetWorldRect(card.rectTransform);
        ICardContainer bestContainer = null;
        float maxArea = 0f;

        foreach (var container in allContainers)
        {
            // [КРИТИЧЕСКИ ВАЖНО] Игнорируем тот контейнер, откуда взяли карту.
            // Иначе карта "упадет" сама в себя, что сломает Undo и логику игры.
            if (container == sourceContainer) continue;

            // Определяем, с чем именно проверять пересечение
            RectTransform targetTransform = container.Transform as RectTransform;

            // ХИТРОСТЬ: Если это Tableau (стопка) и там есть карты,
            // проверяем пересечение с ПОСЛЕДНЕЙ КАРТОЙ (визуально нижней).
            // Это решает проблему "синей точки" — можно цеплять карту за край.
            if (container is TableauPile tableau)
            {
                if (tableau.cards.Count > 0)
                {
                    // Берем последнюю карту в стопке как цель
                    var lastCard = tableau.cards[tableau.cards.Count - 1];
                    if (lastCard != null) targetTransform = lastCard.rectTransform;
                }
            }
            // Поддержка специфичных классов, если они не наследуются напрямую от TableauPile,
            // но имеют структуру карт внутри transform
            else if (container.Transform.childCount > 0 && !(container is FoundationPile))
            {
                // Для Foundation обычно проверяем сам слот, для остальных - верхнюю карту
                var lastChild = container.Transform.GetChild(container.Transform.childCount - 1);
                if (lastChild.GetComponent<CardController>() != null)
                {
                    targetTransform = lastChild as RectTransform;
                }
            }

            if (targetTransform == null) continue;

            // Считаем площадь пересечения
            Rect targetRect = GetWorldRect(targetTransform);
            float area = GetIntersectionArea(cardRect, targetRect);

            // Если пересечение есть и оно больше предыдущего лучшего
            if (area > 0 && area > maxArea)
            {
                // Проверяем правила игры ТОЛЬКО если есть визуальное пересечение
                if (container.CanAccept(card))
                {
                    maxArea = area;
                    bestContainer = container;
                }
            }
        }

        return bestContainer;
    }


    private Rect GetWorldRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // Вычисляем границы (Min/Max X и Y)
        float xMin = corners[0].x;
        float xMax = corners[0].x;
        float yMin = corners[0].y;
        float yMax = corners[0].y;

        for (int i = 1; i < 4; i++)
        {
            if (corners[i].x < xMin) xMin = corners[i].x;
            if (corners[i].x > xMax) xMax = corners[i].x;
            if (corners[i].y < yMin) yMin = corners[i].y;
            if (corners[i].y > yMax) yMax = corners[i].y;
        }

        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private float GetIntersectionArea(Rect r1, Rect r2)
    {
        float xMin = Mathf.Max(r1.x, r2.x);
        float xMax = Mathf.Min(r1.x + r1.width, r2.x + r2.width);
        float yMin = Mathf.Max(r1.y, r2.y);
        float yMax = Mathf.Min(r1.y + r1.height, r2.y + r2.height);

        float w = xMax - xMin;
        float h = yMax - yMin;

        if (w > 0 && h > 0) return w * h;
        return 0f;
    }
    private void NotifyGameModeOnMove()
    {
        if (mode == null) return;

        // Klondike
        if (mode is KlondikeModeManager klondike)
        {
            klondike.RegisterMoveAndStartIfNeeded();
        }
        // Spider
        else if (mode is SpiderModeManager spider)
        {
            spider.OnMoveMade();
        }
        // FreeCell
        else if (mode is FreeCellModeManager freeCell)
        {
            freeCell.OnMoveMade();
        }
        // Сюда можно добавить другие режимы (Yukon, Pyramid и т.д.) по мере их обновления
    }
    /// <summary>
    /// Обновляет список контейнеров из PileManager.
    /// </summary>
    public void RefreshContainers()
    {
        PopulateContainers();
    }

    #endregion
}