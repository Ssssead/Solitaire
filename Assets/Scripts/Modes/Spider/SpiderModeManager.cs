using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using YG;

public class SpiderModeManager : MonoBehaviour, ICardGameMode, ICardClickReceiver
{
    [Header("Managers")]
    public SpiderPileManager pileManager;
    public PileManager corePileManager;
    public SpiderDeckManager deckManager;
    public DragManager dragManager;
    public UndoManager undoManager;
    public AnimationService animationService;
    public GameUIController gameUI;
    [Header("Tutorial")]
    public SpiderTutorialManager tutorialManager;

    [Header("UI & Config")]
    public Canvas rootCanvas;
    public RectTransform dragLayer;
    public float tableauVerticalGap = 35f;

    [Header("UI & HUD")]
    public TMP_Text movesText;
    public TMP_Text scoreText;
    public TMP_Text timeText;
    public SpiderIntroController introController;
    [HideInInspector] public bool isRestarting = false;

    private SpiderDefeatManager _defeatManager;
    private SpiderScoreManager _scoreManager;

    // Флаги состояния игры
    private bool _isGameEnded = false;
    private bool _hasGameStarted = false;

    // Локальный таймер
    private float gameTimer = 0f;
    private bool isTimerRunning = false;
    private bool _isStockDrawFlag = false;
    public int ActiveFoundationAnimations { get; set; } = 0;

    // --- ICardGameMode Implementation ---
    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => animationService;
    public PileManager PileManager => corePileManager;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => tableauVerticalGap;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public bool IsInputAllowed { get; set; } = true;
    public string GameName => "Spider";
    public GameType GameType => GameType.Spider;

    // Статистика
    public int CurrentScore => _scoreManager != null ? _scoreManager.CurrentScore : 0;
    public int MoveCount => StatisticsManager.Instance != null ? StatisticsManager.Instance.GetCurrentMoves() : 0;
    public float GameTime => gameTimer;

    public SpiderScoreManager ScoreManager => _scoreManager;
    public ITutorialManager Tutorial => tutorialManager;

    void Start()
    {
        InitializeGame();
    }

    private void Update()
    {
        if (isTimerRunning && !_isGameEnded)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }
    }

    public bool IsMatchInProgress()
    {
        return _hasGameStarted && !_isGameEnded;
    }

    public void InitializeGame()
    {
        // 1. СНАЧАЛА честно закрываем и сохраняем прошлую игру (если она была)
        if (_hasGameStarted && !_isGameEnded && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }

        // 2. ЗАТЕМ сообщаем трекеру настройки НОВОГО матча ДО раздачи карт
        int suits = GameSettings.SpiderSuitCount;
        if (suits == 0) suits = 1;
        string variant = suits == 1 ? "1Suit" : suits + "Suits";
        GameQuestTracker.Instance?.StartMatch("Spider", GameSettings.CurrentDifficulty, variant);

        _isGameEnded = false;
        _hasGameStarted = false;
        ActiveFoundationAnimations = 0;

        gameTimer = 0f;
        isTimerRunning = false;

        if (pileManager != null && pileManager.FoundationPiles != null)
        {
            foreach (var f in pileManager.FoundationPiles)
            {
                f.ResetFoundation();
            }
        }

        Difficulty diff = GameSettings.CurrentDifficulty;

        if (corePileManager == null)
            corePileManager = GetComponent<PileManager>() ?? gameObject.AddComponent<PileManager>();
        SyncPileManager();

        _defeatManager = GetComponent<SpiderDefeatManager>();
        if (_defeatManager == null) _defeatManager = gameObject.AddComponent<SpiderDefeatManager>();
        _defeatManager.Initialize(pileManager, gameUI, this);

        _scoreManager = GetComponent<SpiderScoreManager>();
        if (_scoreManager == null) _scoreManager = gameObject.AddComponent<SpiderScoreManager>();
        _scoreManager.ResetScore();

        if (dragManager != null)
        {
            dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);
            dragManager.RegisterAllContainers(pileManager.GetAllContainers());
        }

        if (undoManager != null) undoManager.Initialize(this);

        // --- Проверка на режим обучения ---
        if (GameSettings.IsTutorialMode && tutorialManager != null)
        {
            StartCoroutine(IntroSequenceRoutine());
        }
        else
        {
            deckManager.CreateAndDeal(suits, diff);
            UpdateTableauLayouts();
            UpdateFullUI();
        }
    }

    public void OnMoveMade()
    {
        if (_isGameEnded || !IsInputAllowed) return;

        // ---> ДОБАВИТЬ ЭТО: Считаем ход, если это не просто сдача ряда из колоды <---
        if (!_isStockDrawFlag) GameQuestTracker.Instance?.RecordMove();
        // ----------------------------------------------------------------------------

        if (!_hasGameStarted)
        {
            _hasGameStarted = true;
            isTimerRunning = true;

            if (StatisticsManager.Instance != null)
            {
                Difficulty diff = GameSettings.CurrentDifficulty;
                string variant = GameSettings.GetCurrentVariantString(GameType.Spider);
                StatisticsManager.Instance.OnGameStarted("Spider", diff, variant);
            }
        }

        if (_scoreManager) _scoreManager.ApplyPenalty();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();

        // Никакой телеметрии здесь больше нет, она вся в OnDropToBoard!
        UpdateFullUI();
    }

    public void OnStockClicked()
    {
        if (_isGameEnded || !IsInputAllowed) return;

        // Проверка туториала
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.ClickStock)) return;
            tutorialManager.AdvanceStep();
        }

        // ---> ДОБАВИТЬ ЭТО: Клик по колоде в Пауке <---
        GameQuestTracker.Instance?.RecordStockDraw();
        // ----------------------------------------------

        // Ставим флаг, чтобы OnMoveMade не записал это как обычный перенос карты
        _isStockDrawFlag = true;
        OnMoveMade();
        _isStockDrawFlag = false;

       
        UpdateFullUI();
    }

    public void OnUndoAction()
    {
        if (!_hasGameStarted || _isGameEnded || !IsInputAllowed) return;

        // --- ИЗМЕНЕНИЕ: Проверка туториала перед отменой ---
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.Undo))
            {
                // Если сейчас не шаг 4, просто выходим и не даем отменить
                return;
            }
            tutorialManager.AdvanceStep();
        }
        // ---------------------------------------------------

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
       
        IsInputAllowed = true;

        if (_defeatManager != null) _defeatManager.OnUndo();
        if (_scoreManager != null)
        {
            _scoreManager.ApplyPenalty();
        }
        StopCoroutine("DelayedTableauUpdate");
        StartCoroutine("DelayedTableauUpdate");

        // <--- НОВОЕ: ЗВУКИ ОТМЕНЫ КАК В КЛОНДАЙКЕ --->
        if (AudioManager.Instance != null)
        {
            // Звук нажатия кнопки
            AudioManager.Instance.PlaySound("UI_Back");

            // Звук улетающей карты
            AudioManager.Instance.PlaySound("Card_Whoosh_Out");

            // Запускаем корутину для звука приземления ровно через 0.25 сек (время анимации отмены)
            StartCoroutine(DelayedUndoDropSound(0.25f));
        }

        UpdateFullUI();

    }
    // <--- НОВОЕ: Корутина для звука приземления отмененной карты --->
    private IEnumerator DelayedUndoDropSound(float delay)
    {
        // Ждем, пока отмененная карта летит на свое старое место
        yield return new WaitForSeconds(delay);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Card_Drop_Success");
        }
    }
    public bool OnDropToBoard(CardController card, Vector2 anchoredPosition)
    {
        // 1. ИЩЕМ ЦЕЛЬ И ИСТОЧНИК ЗАРАНЕЕ (до броска)
        ICardContainer sourceContainer = card.transform.parent?.GetComponent<ICardContainer>();
        ICardContainer targetContainer = dragManager?.FindNearestContainer(card, anchoredPosition, 300f);

        // Проверка туториала
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, targetContainer))
            {
                return false;
            }
        }

        // 2. Вычисляем точные индексы колонок (0-9) через PileManager
        int sourceIndex = -1;
        int targetIndex = -1;

        if (sourceContainer is SpiderTableauPile sPile && pileManager != null)
            sourceIndex = pileManager.TableauPiles.IndexOf(sPile);

        if (targetContainer is SpiderTableauPile tPile && pileManager != null)
            targetIndex = pileManager.TableauPiles.IndexOf(tPile);

        // 3. Отдаем ход в DragManager
        bool success = dragManager?.OnDropToBoard(card, anchoredPosition) ?? false;

        // 4. Если ход легальный - записываем ИДЕАЛЬНО точный лог
       

        return success;
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        Debug.Log($"[SpiderModeManager] Карта {card.gameObject.name} успешно упала в {container.GetType().Name}");

        // Стандартная логика привязки карты
        dragManager?.OnCardDroppedToContainer(card, container);

        // ---> ИСПРАВЛЕНИЕ: ТЕЛЕМЕТРИЯ ИГРОКА <---
       
        // Проверяем туториал
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            bool isAllowed = tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, container);
            Debug.Log($"[SpiderModeManager] Ответ туториала на этот ход: {isAllowed}");

            if (isAllowed)
            {
                Debug.Log("[SpiderModeManager] Запускаем переключение шага (AdvanceStep)!");
                tutorialManager.AdvanceStep();
            }
        }

        OnMoveMade();
    }
    private IEnumerator DelayedTableauUpdate()
    {
        if (undoManager != null)
        {
            while (undoManager.IsUndoing)
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(0.8f);
        }

        yield return new WaitForEndOfFrame();
        UpdateTableauLayouts();

        if (pileManager != null && pileManager.TableauPiles != null && pileManager.TableauPiles.Count > 9)
        {
            pileManager.TableauPiles[9].ForceRecalculateLayout();
        }
    }

    public void OnRowCompleted()
    {
        if (_scoreManager) _scoreManager.AddRowBonus();
        UpdateFullUI();
        CheckGameState();
    }

    public void UpdateTableauLayouts(bool isStockEmptying = false)
    {
        if (pileManager == null) return;

        int stockCardsCount = 0;
        if (pileManager.StockPile != null)
        {
            foreach (Transform child in pileManager.StockPile.transform)
            {
                if (child.GetComponent<CardController>() != null) stockCardsCount++;
            }
        }

        bool hasStockCards = isStockEmptying ? false : (stockCardsCount > 0);
        SetPileCompressed(9, hasStockCards);

        // --- ИСПРАВЛЕНИЕ НЕВИДИМОЙ СТЕНЫ У СТОКА ---
        if (pileManager.StockPile != null)
        {
            // Отключаем Image (Raycast Target), так как именно он перехватывает клики
            var img = pileManager.StockPile.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.raycastTarget = hasStockCards;

            // На всякий случай отключаем и CanvasGroup
            var cg = pileManager.StockPile.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = hasStockCards;
        }
        // -------------------------------------------

        int filledFoundations = 0;
        if (pileManager.FoundationPiles != null)
        {
            foreach (var f in pileManager.FoundationPiles)
            {
                if (f.IsFull || f.isReserved) filledFoundations++;
            }
        }

        SetPileCompressed(0, filledFoundations >= 1);
        SetPileCompressed(1, filledFoundations >= 3);
        SetPileCompressed(2, filledFoundations >= 7);

        for (int i = 3; i <= 8; i++) SetPileCompressed(i, false);
    }

    private void SetPileCompressed(int index, bool isCompressed)
    {
        if (pileManager.TableauPiles != null && pileManager.TableauPiles.Count > index)
        {
            pileManager.TableauPiles[index].SetLayoutCompressed(isCompressed);
        }
    }

    public void CheckGameState()
    {
        UpdateTableauLayouts();

        if (_isGameEnded) return;
        if (ActiveFoundationAnimations > 0) return;

        int fullFoundations = 0;
        foreach (var f in pileManager.FoundationPiles)
        {
            if (f.IsFull) fullFoundations++;
        }

        if (fullFoundations >= 8)
        {
            Debug.Log("Spider: Victory!");
            _isGameEnded = true;
            isTimerRunning = false;
            IsInputAllowed = false;
            StartCoroutine(VictoryRoutine());
            return;
        }

        if (_defeatManager != null)
        {
            _defeatManager.CheckDefeatCondition();
        }
    }

    private void UpdateFullUI()
    {
        if (movesText != null)
        {
            if (!_hasGameStarted) movesText.text = "0";
            else if (StatisticsManager.Instance != null)
                movesText.text = $"{StatisticsManager.Instance.GetCurrentMoves()}";
            else movesText.text = "0";
        }

        if (scoreText != null)
        {
            int score = _scoreManager != null ? _scoreManager.CurrentScore : 0;
            scoreText.text = $"{score}";
        }

        UpdateTimeUI();
    }

    private void UpdateTimeUI()
    {
        if (timeText != null)
        {
            int totalSeconds = Mathf.FloorToInt(gameTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timeText.text = string.Format("{0}:{1:00}", minutes, seconds);
        }
    }

    private void OnDestroy()
    {
        if (_hasGameStarted && !_isGameEnded)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }
    }

    private IEnumerator VictoryRoutine()
    {
        _isGameEnded = true;
        IsInputAllowed = false;

        // --- ИСПРАВЛЕНИЕ: Мгновенно сбрасываем историю отмены ---
        // Это автоматически сделает кнопки Undo некликабельными (серыми)
        if (undoManager != null)
        {
            undoManager.ResetHistory();
        }

        yield return new WaitForSeconds(1.0f);

        int finalMoves = 0;
        if (StatisticsManager.Instance != null)
            finalMoves = StatisticsManager.Instance.GetCurrentMoves();

        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameWon(CurrentScore);
        }

        if (gameUI != null)
            gameUI.OnGameWon(finalMoves);
       
    }

    private void SyncPileManager()
    {
        if (corePileManager == null || pileManager == null) return;
        var tableauField = typeof(PileManager).GetField("tableau", BindingFlags.NonPublic | BindingFlags.Instance);
        if (tableauField != null)
        {
            List<TableauPile> coreTableaus = new List<TableauPile>();
            foreach (var pile in pileManager.TableauPiles) coreTableaus.Add(pile);
            tableauField.SetValue(corePileManager, coreTableaus);
        }
    }

    public SpiderFoundationPile GetNextEmptyFoundation()
    {
        foreach (var f in pileManager.FoundationPiles)
        {
            if (!f.IsFull && !f.isReserved)
            {
                f.isReserved = true;
                return f;
            }
        }
        return null;
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // Если это мобильное устройство или планшет — запускаем авто-перенос (как при двойном клике)
        if (GameSettings.AutoMoveClickMode == 0)
        {
            var dragManager = FindObjectOfType<DragManager>();
            dragManager?.ForceSnapBackLastDrop();

            ExecuteAutoMove(card);
        }
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // На ПК авто-перенос срабатывает только по двойному клику
        if (GameSettings.AutoMoveClickMode == 1)
        {
            ExecuteAutoMove(card);
        }
    }

    private void ExecuteAutoMove(CardController card)
    {
        if (tutorialManager != null && tutorialManager.IsTutorialActive) return;

        SpiderTableauPile sourceContainer = GetCardSourceReliably(card);
        if (sourceContainer == null) return;

        // Жестко снимаем блокировку, если микросвайп успел её повесить
        var fieldLocked = typeof(TableauPile).GetField("isLayoutLocked", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldLocked != null) fieldLocked.SetValue(sourceContainer, false);

        int cardIndex = sourceContainer.cards.IndexOf(card);
        if (cardIndex == -1 || !sourceContainer.faceUp[cardIndex]) return;

        List<CardController> toShake;
        List<CardController> sequence = GetValidSequenceOrShake(sourceContainer, cardIndex, out toShake);

        // Останавливаем все остаточные анимации на картах
        if (sequence != null)
        {
            foreach (var c in sequence) c.StopAllCoroutines();
        }
        else
        {
            // Если ряд неправильный (разные масти), трясем мешающий хвост (или саму карту)
            foreach (var c in toShake) c.StopAllCoroutines();

            if (toShake.Count > 0) SpiderEffectsService.Instance?.Shake(toShake);
            else SpiderEffectsService.Instance?.Shake(new List<CardController> { card });
            return;
        }

        bool moved = TryAutoMove(sequence, sourceContainer);

        if (!moved)
        {
            // Если нет места, принудительно возвращаем карты и трясем правильную собранную стопку
            foreach (var c in sequence) c.rectTransform.SetParent(sourceContainer.transform, true);
            sourceContainer.ForceRecalculateLayout();
            SpiderEffectsService.Instance?.Shake(sequence);
        }
    }

    private SpiderTableauPile GetCardSourceReliably(CardController card)
    {
        if (pileManager == null || pileManager.TableauPiles == null) return null;
        foreach (var tab in pileManager.TableauPiles)
        {
            if (tab.cards.Contains(card)) return tab;
        }
        return card.GetComponentInParent<SpiderTableauPile>();
    }

    // Проверяет, можно ли перенести ряд, и возвращает его. Если нет — отдает хвост для тряски.
    private List<CardController> GetValidSequenceOrShake(SpiderTableauPile tab, int startIndex, out List<CardController> toShake)
    {
        toShake = new List<CardController>();
        List<CardController> seq = new List<CardController> { tab.cards[startIndex] };

        for (int i = startIndex; i < tab.cards.Count - 1; i++)
        {
            var current = tab.cards[i];
            var next = tab.cards[i + 1];

            bool sameSuit = current.cardModel.suit == next.cardModel.suit;
            bool correctRank = current.cardModel.rank == next.cardModel.rank + 1;

            if (!sameSuit || !correctRank)
            {
                // Нашли разрыв: собираем всё, что ниже точки разрыва (от next и до конца)
                for (int j = i + 1; j < tab.cards.Count; j++) toShake.Add(tab.cards[j]);
                return null;
            }
            seq.Add(next);
        }
        return seq;
    }

    // Ищет самое оптимальное место для ряда
    private bool TryAutoMove(List<CardController> sequence, SpiderTableauPile source)
    {
        CardController leadCard = sequence[0];
        SpiderTableauPile bestTarget = null;
        int bestScore = -1;

        foreach (var t in pileManager.TableauPiles)
        {
            if (t == source) continue;

            if (t.CanAccept(leadCard))
            {
                int score = 0;
                if (t.cards.Count == 0)
                {
                    score = 1; // Пустая ячейка (Низший приоритет)
                }
                else
                {
                    var targetTop = t.cards[t.cards.Count - 1];
                    if (targetTop.cardModel.suit == leadCard.cardModel.suit)
                        score = 3; // Идеально: по масти и по рангу (Высший приоритет)
                    else
                        score = 2; // Можно: по рангу, но другая масть (Средний приоритет)
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = t;
                }
            }
        }

        if (bestTarget != null)
        {
            ExecuteProgrammaticSequenceMove(sequence, source, bestTarget);
            return true;
        }

        return false;
    }

    private void ExecuteProgrammaticSequenceMove(List<CardController> sequence, SpiderTableauPile source, SpiderTableauPile target)
    {
        string batchID = System.Guid.NewGuid().ToString();
        List<Transform> prevParents = new List<Transform>();
        List<Vector3> prevPositions = new List<Vector3>();
        List<int> prevSibs = new List<int>();

        foreach (var c in sequence)
        {
            prevParents.Add(c.transform.parent);
            prevPositions.Add(c.transform.localPosition);
            prevSibs.Add(c.transform.GetSiblingIndex());
        }

        int startIdx = source.cards.IndexOf(sequence[0]);
        if (startIdx != -1)
        {
            source.RemoveSequenceFrom(startIdx);
        }

        StartCoroutine(AnimateSequenceAutoMove(sequence, source, target, prevParents, prevPositions, prevSibs, batchID));
    }

    private IEnumerator AnimateSequenceAutoMove(
        List<CardController> sequence, SpiderTableauPile source, SpiderTableauPile target,
        List<Transform> prevParents, List<Vector3> prevPositions, List<int> prevSibs, string batchID)
    {
        target.SetAnimatingCard(true);

        List<Vector3> startPositions = new List<Vector3>();
        foreach (var c in sequence)
        {
            startPositions.Add(c.rectTransform.position);
            if (dragLayer != null)
            {
                c.rectTransform.SetParent(dragLayer, true);
                c.rectTransform.SetAsLastSibling();
            }
            if (c.canvasGroup) c.canvasGroup.blocksRaycasts = false;
        }

        Canvas.ForceUpdateCanvases();

        // 1. Временно добавляем карты, чтобы получить правильный динамический макет Паука
        target.cards.AddRange(sequence);
        for (int i = 0; i < sequence.Count; i++) target.faceUp.Add(true);

        var calcMethod = typeof(SpiderTableauPile).GetMethod("CalculateSpiderAnchors", BindingFlags.NonPublic | BindingFlags.Instance);
        List<Vector2> allAnchors = (List<Vector2>)calcMethod.Invoke(target, null);

        target.cards.RemoveRange(target.cards.Count - sequence.Count, sequence.Count);
        target.faceUp.RemoveRange(target.faceUp.Count - sequence.Count, sequence.Count);

        // 2. Считаем мировые координаты приземления
        List<Vector3> targetWorldPositions = new List<Vector3>();
        int startIndexInTarget = target.cards.Count;

        for (int i = 0; i < sequence.Count; i++)
        {
            Vector2 anc = allAnchors[startIndexInTarget + i];
            GameObject tmp = new GameObject("tmp");
            tmp.transform.SetParent(target.transform, false);
            tmp.AddComponent<RectTransform>().anchoredPosition = anc;
            Canvas.ForceUpdateCanvases();
            targetWorldPositions.Add(tmp.transform.position);
            Destroy(tmp);
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_PickUp");

        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < sequence.Count; i++)
            {
                sequence[i].rectTransform.position = Vector3.Lerp(startPositions[i], targetWorldPositions[i], t);
            }
            yield return null;
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");

        for (int i = 0; i < sequence.Count; i++)
        {
            var c = sequence[i];
            c.rectTransform.position = targetWorldPositions[i];
            c.rectTransform.SetParent(target.transform, true);
            if (c.canvasGroup != null)
            {
                c.canvasGroup.blocksRaycasts = true;
                c.canvasGroup.interactable = true;
            }
        }

        target.AddCardsBatch(sequence, true);
        target.SetAnimatingCard(false);

        if (undoManager != null)
        {
            undoManager.RecordMove(sequence, source, target, prevParents, prevPositions, prevSibs, batchID);
        }

        // Авто-открытие карты
        if (source.cards.Count > 0)
        {
            int topIndex = source.cards.Count - 1;
            if (!source.faceUp[topIndex])
            {
                source.CheckAndFlipTop();
                if (undoManager != null) undoManager.RecordFlipInSource(topIndex);
            }
        }

        if (source.cards.Count == 0) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, 1);
        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveTableauToTableau, 1);

        target.ForceRecalculateLayout();
        source.ForceRecalculateLayout();

        OnMoveMade();

        // Проверяем, не собралась ли масть после нашего хода
        var checkMethod = typeof(SpiderTableauPile).GetMethod("CheckSuit", BindingFlags.NonPublic | BindingFlags.Instance);
        if (checkMethod != null) checkMethod.Invoke(target, null);

        CheckGameState();
    }

    public void RestartGame()
    {
        isRestarting = true;

        if (_hasGameStarted && !_isGameEnded)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }

        _isGameEnded = false;
        _hasGameStarted = false;
        IsInputAllowed = true;
        ActiveFoundationAnimations = 0;

        gameTimer = 0f;
        isTimerRunning = false;

        if (undoManager != null) undoManager.ResetHistory();

        if (pileManager != null && pileManager.FoundationPiles != null)
        {
            foreach (var f in pileManager.FoundationPiles)
            {
                f.ResetFoundation();
            }
        }

        // --- ИСПРАВЛЕНИЕ БАГА: ОЧИЩАЕМ ЛОГИЧЕСКОЕ СОСТОЯНИЕ СТОПОК ---
        if (pileManager != null)
        {
            if (pileManager.TableauPiles != null)
            {
                foreach (var pile in pileManager.TableauPiles)
                {
                    if (pile != null) pile.ClearLogicalState();
                }
            }
            if (pileManager.StockPile != null)
            {
                // У стока нет faceUp, поэтому просто чистим cards
                pileManager.StockPile.cards.Clear();
            }
        }
        // -------------------------------------------------------------

        if (_scoreManager) _scoreManager.ResetScore();
        if (_defeatManager != null) _defeatManager.OnUndo();

        // --- ИЗМЕНЕНИЕ: Проверка на режим обучения ---
        if (GameSettings.IsTutorialMode && tutorialManager != null)
        {
            StartCoroutine(IntroSequenceRoutine());
        }
        else
        {
            deckManager.RestartGame();
            UpdateTableauLayouts();
            UpdateFullUI();
        }
    }
    private IEnumerator IntroSequenceRoutine()
    {
        // ФИКС 1: Мгновенно прячем UI до задержки, чтобы избежать мелькания в 1-й кадр
        if (introController != null) introController.SetupIntro(false);

        yield return null;
        yield return StartCoroutine(tutorialManager.PlayTutorialIntro(null));

        isRestarting = false;
        IsInputAllowed = true;

        UpdateTableauLayouts();
        UpdateFullUI();
    }
}