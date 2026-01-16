using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UndoManager : MonoBehaviour
{
    private ICardGameMode gameMode;
    private AnimationService animationService;
    private PileManager pileManager;
    private Stack<MoveRecord> undoStack = new Stack<MoveRecord>();

    [Header("UI Buttons")]
    public Button undoButton;
    public Button undoAllButton;

    [Header("Animation Settings")]
    [SerializeField] private float undoAnimDuration = 0.25f;
    [SerializeField] private float stockRapidDelay = 0.05f;

    private bool inProgress = false;
    public bool IsUndoing => inProgress;

    public void Initialize(ICardGameMode mode)
    {
        gameMode = mode;
        if (gameMode != null)
        {
            animationService = gameMode.AnimationService;
            pileManager = gameMode.PileManager;
        }
        if (animationService == null) animationService = FindObjectOfType<AnimationService>();
        if (pileManager == null) pileManager = FindObjectOfType<PileManager>();

        if (undoButton != null) undoButton.onClick.AddListener(OnUndoButtonClick);
        if (undoAllButton != null) undoAllButton.onClick.AddListener(OnUndoAllButtonClick);
        UpdateButtons();
    }

    // Сохраняем поддержку isRapidUndo
    public void RecordMove(List<CardController> cards, ICardContainer source, ICardContainer target,
                           List<Transform> origParents = null, List<Vector3> origLocal = null, List<int> origSibling = null,
                           string groupID = null, bool isRapidUndo = false)
    {
        if (undoStack.Count == 0 && DealCacheSystem.Instance != null)
            DealCacheSystem.Instance.MarkCurrentDealAsPlayed();

        if (cards == null || cards.Count == 0) return;

        var record = new MoveRecord(cards, source, target, origParents, origLocal, origSibling);
        record.groupID = groupID;
        record.isRapidUndo = isRapidUndo;
        record.SaveFaceUpStates();

        undoStack.Push(record);
        UpdateButtons();
    }

    public void TagLastMove(string groupID)
    {
        if (undoStack.Count > 0) undoStack.Peek().groupID = groupID;
    }

    public void RecordFlipInSource(int cardIndex)
    {
        if (undoStack.Count > 0)
        {
            var rec = undoStack.Peek();
            rec.sourceCardWasFlipped = true;
            rec.flippedCardIndex = cardIndex;
        }
    }

    private void OnUndoButtonClick() { if (!inProgress) StartCoroutine(UndoLastCoroutine()); }
    private void OnUndoAllButtonClick() { if (!inProgress) StartCoroutine(UndoAllCoroutine()); }

    private IEnumerator UndoLastCoroutine()
    {
        if (inProgress || undoStack.Count == 0) yield break;
        inProgress = true;

        gameMode?.OnUndoAction();

        var currentRecord = undoStack.Pop();
        string currentGroupID = currentRecord.groupID;
        bool isRapid = currentRecord.isRapidUndo;

        // Если это быстрый режим (Stock в Spider)
        if (isRapid)
        {
            StartCoroutine(PerformUndo(currentRecord, immediate: false));

            if (!string.IsNullOrEmpty(currentGroupID))
            {
                while (undoStack.Count > 0 && undoStack.Peek().groupID == currentGroupID)
                {
                    yield return new WaitForSeconds(stockRapidDelay);
                    var nextRecord = undoStack.Pop();
                    StartCoroutine(PerformUndo(nextRecord, immediate: false));
                }
            }
            yield return new WaitForSeconds(undoAnimDuration);
        }
        else // Стандартный режим (Klondike и Foundation в Spider)
        {
            yield return StartCoroutine(PerformUndo(currentRecord, immediate: false));

            if (!string.IsNullOrEmpty(currentGroupID))
            {
                while (undoStack.Count > 0 && undoStack.Peek().groupID == currentGroupID)
                {
                    var nextRecord = undoStack.Pop();
                    yield return StartCoroutine(PerformUndo(nextRecord, immediate: false));
                }
            }
        }

        inProgress = false;
        UpdateButtons();
        gameMode?.CheckGameState();
    }

    // --- ИЗМЕНЕННЫЙ МЕТОД: ВЫПОЛНЯЕТСЯ ЗА ОДИН КАДР ---
    private IEnumerator UndoAllCoroutine()
    {
        if (inProgress || undoStack.Count == 0) yield break;
        inProgress = true;

        StopAllCoroutines();
        gameMode?.OnUndoAction();

        while (undoStack.Count > 0)
        {
            var record = undoStack.Pop();

            // --- ЗАЩИТА ОТ КРАША (Fix для FreeCell/Klondike) ---
            // Проверяем, существуют ли карты физически. 
            // Unity уничтожает объекты при перезагрузке, но C# ссылка остается (как null).
            bool isRecordBroken = false;
            if (record.cards == null || record.cards.Count == 0) isRecordBroken = true;
            else
            {
                foreach (var card in record.cards)
                {
                    // Проверка: (card == null) сработает, если Unity уничтожила объект
                    if (card == null || card.gameObject == null)
                    {
                        isRecordBroken = true;
                        break;
                    }
                }
            }

            if (isRecordBroken)
            {
                // Если запись ссылается на мертвые карты, просто пропускаем её
                continue;
            }
            // ----------------------------------------------------

            // Если карты живы, выполняем откат
            RemoveCardsFromTarget(record);

            if (record.sourceCardWasFlipped && record.sourceContainer is TableauPile tableau)
            {
                if (record.flippedCardIndex >= 0) tableau.ForceFlipFaceDown(record.flippedCardIndex, true);
            }

            RestoreCardsPositionImmediate(record);
            AddCardsBackToSource(record, true);
        }

        Canvas.ForceUpdateCanvases();

        if (animationService != null && pileManager != null)
            animationService.ReorderAllContainers(pileManager.GetAllContainerTransforms());

        if (pileManager != null && pileManager.Tableau != null)
        {
            foreach (var container in pileManager.Tableau)
            {
                var pileScript = container.GetComponent<TableauPile>();
                if (pileScript != null) pileScript.StartLayoutAnimationPublic();
            }
        }

        // Обновляем Waste (для Klondike)
        var waste = FindObjectOfType<WastePile>();
        if (waste != null) waste.UpdateLayout();

        inProgress = false;
        UpdateButtons();
        gameMode?.CheckGameState();
    }
    // ----------------------------------------------------

    private IEnumerator PerformUndo(MoveRecord record, bool immediate)
    {
        if (record == null || record.cards == null || record.cards.Count == 0) yield break;
        RemoveCardsFromTarget(record);
        if (record.sourceCardWasFlipped && record.sourceContainer is TableauPile tableau)
        {
            if (record.flippedCardIndex >= 0) tableau.ForceFlipFaceDown(record.flippedCardIndex, immediate);
        }
        if (immediate) RestoreCardsPositionImmediate(record);
        else yield return StartCoroutine(AnimateCardsReturn(record));
        AddCardsBackToSource(record, immediate);
    }

    private void RestoreCardsPositionImmediate(MoveRecord record)
    {
        Transform sourceTrans = (record.sourceContainer as Component)?.transform;
        for (int i = 0; i < record.cards.Count; i++)
        {
            var card = record.cards[i];
            if (card == null) continue;
            card.StopAllCoroutines();

            // Если Stock - закрываем
            if (record.sourceContainer.GetType().Name.Contains("Stock"))
                card.GetComponent<CardData>()?.SetFaceUp(false, animate: false);

            if (record.sourceLocalPositions != null && i < record.sourceLocalPositions.Count && sourceTrans != null)
            {
                if (card.rectTransform.parent != sourceTrans) card.rectTransform.SetParent(sourceTrans, false);
                Vector3 savedLocal = record.sourceLocalPositions[i];
                card.rectTransform.anchoredPosition = new Vector2(savedLocal.x, savedLocal.y);
            }
            else if (sourceTrans != null) { card.rectTransform.SetParent(sourceTrans, false); card.rectTransform.anchoredPosition = Vector2.zero; }
        }
    }

    private IEnumerator AnimateCardsReturn(MoveRecord record)
    {
        var cards = record.cards;
        List<Vector3> startPos = new List<Vector3>();
        List<Vector3> targetPos = new List<Vector3>();
        RectTransform dragLayer = gameMode?.DragLayer;
        Transform sourceTrans = (record.sourceContainer as Component)?.transform;
        bool isStock = record.sourceContainer.GetType().Name.Contains("Stock");

        // Проверка: возвращаем ли мы карты в WastePile?
        WastePile wasteTarget = record.sourceContainer as WastePile;

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) continue;
            startPos.Add(card.rectTransform.position);

            // Поднимаем в слой драга для красивого полета
            if (dragLayer != null) card.rectTransform.SetParent(dragLayer, true);
            card.rectTransform.SetAsLastSibling();

            if (isStock) card.GetComponent<CardData>()?.SetFaceUp(false, animate: true);

            Vector3 targetW = Vector3.zero;

            // --- ЛОГИКА ИСПРАВЛЕНИЯ ---
            if (wasteTarget != null)
            {
                // Для WastePile игнорируем savedPositions, так как лейаут мог измениться.
                // Рассчитываем, где карта должна оказаться "в будущем".
                int futureCount = wasteTarget.Count + cards.Count;
                int futureIndex = wasteTarget.Count + i; // i идет от 0 до cards.Count, сохраняя порядок

                Vector2 anchor = wasteTarget.GetAnchoredPositionForFutureIndex(futureCount, futureIndex);

                if (animationService != null)
                    targetW = animationService.AnchoredToWorldPosition(wasteTarget.Transform as RectTransform, anchor);
                else
                    targetW = wasteTarget.Transform.TransformPoint(anchor);
            }
            // ---------------------------
            else if (record.sourceLocalPositions != null && i < record.sourceLocalPositions.Count && sourceTrans != null)
            {
                // Стандартная логика для Tableau и других
                Vector3 savedLocal = record.sourceLocalPositions[i];
                if (animationService != null) targetW = animationService.AnchoredToWorldPosition(sourceTrans as RectTransform, new Vector2(savedLocal.x, savedLocal.y));
                else targetW = sourceTrans.TransformPoint(savedLocal);
            }
            else
            {
                targetW = sourceTrans != null ? sourceTrans.position : card.transform.position;
            }

            targetPos.Add(targetW);
        }

        float t = 0;
        while (t < undoAnimDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / undoAnimDuration;
            // Добавим SmoothStep для более плавной анимации, как в DeckManager
            p = p * p * (3f - 2f * p);

            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) cards[i].rectTransform.position = Vector3.Lerp(startPos[i], targetPos[i], p);
            yield return null;
        }
    }

    private void AddCardsBackToSource(MoveRecord record, bool immediate)
    {
        var cards = record.cards;
        var source = record.sourceContainer;

        // Проверяем имя, чтобы работало и для StockPile, и для SpiderStockPile
        if (source.GetType().Name.Contains("Stock"))
        {
            if (source is StockPile st)
            {
                for (int i = cards.Count - 1; i >= 0; i--) st.AddCard(cards[i], false);
            }
            else if (source is TableauPile tp)
            {
                for (int i = cards.Count - 1; i >= 0; i--) tp.AddCard(cards[i], false);
            }

            // ИСПРАВЛЕНИЕ: МЫ БОЛЬШЕ НЕ МЕНЯЕМ blocksRaycasts ЗДЕСЬ.
            // SpiderStockPile сам отключит их в LateUpdate.
            // StockPile (Klondike) оставит их как есть (обычно true), что правильно для Клондайка.
        }
        else if (source is WastePile waste)
        {
            List<CardController> cardsToAdd = new List<CardController>(cards);
            if (record.targetContainer.GetType().Name.Contains("Stock")) cardsToAdd.Reverse();
            waste.AddCardsBatch(cardsToAdd, true);
            foreach (var c in cardsToAdd) if (c && c.canvasGroup) { c.canvasGroup.blocksRaycasts = true; c.canvasGroup.interactable = true; }
        }
        else if (source is TableauPile tableau)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;
                bool wasFaceUp = (record.sourceFaceUpStates != null && i < record.sourceFaceUpStates.Count) ? record.sourceFaceUpStates[i] : true;
                if (immediate && !wasFaceUp) card.GetComponent<CardData>()?.SetFaceUp(false, animate: false);
                tableau.AddCard(card, wasFaceUp);
                if (card.canvasGroup != null) { card.canvasGroup.blocksRaycasts = true; card.canvasGroup.interactable = true; }
            }
            tableau.ForceRecalculateLayout();
        }
        else if (source is FoundationPile foundation)
        {
            foreach (var card in cards) foundation.AcceptCard(card);
        }
        else if (source is FreeCellPile freeCell)
        {
            if (cards.Count > 0) freeCell.AcceptCard(cards[0]);
        }
        if (source is Component comp && !immediate) animationService?.ReorderContainerZ(comp.transform);
    }

    private void RemoveCardsFromTarget(MoveRecord record)
    {
        if (record.targetContainer is TableauPile tab)
        {
            int count = record.cards.Count;
            int total = tab.cards.Count;
            if (total >= count) tab.RemoveCardsSilent(count);
        }
        else if (record.targetContainer is WastePile waste)
        {
            waste.RemoveCardsSilent(record.cards.Count);
            waste.UpdateLayout();
        }
        else if (record.targetContainer is FoundationPile found)
        {
            // Foundation обычно принимает по 1 карте
            found.ForceRemove(record.cards[0]);
        }
        else if (record.targetContainer is FreeCellPile freeCell)
        {
            // Так как FreeCellPile содержит карты как детей, undo просто заберет их при перемещении
            // Но если есть специфическая логика удаления, она должна быть тут.
            // Обычно для Transform-based контейнеров ничего делать не надо, если DragManager меняет parent.
        }
        else if (record.targetContainer.GetType().Name.Contains("Stock"))
        {
            if (record.targetContainer is StockPile st)
            {
                // --- ИСПРАВЛЕНИЕ ---
                // Удаляем столько карт, сколько было добавлено в этом ходу
                int count = record.cards.Count;
                for (int i = 0; i < count; i++)
                {
                    st.PopTop();
                }
                // -------------------
            }
        }
    }

    public void ResetHistory() { undoStack.Clear(); inProgress = false; UpdateButtons(); gameMode?.OnUndoAction(); }
    private void UpdateButtons() { if (undoButton) undoButton.interactable = undoStack.Count > 0 && !inProgress; if (undoAllButton) undoAllButton.interactable = undoStack.Count > 0 && !inProgress; }
}