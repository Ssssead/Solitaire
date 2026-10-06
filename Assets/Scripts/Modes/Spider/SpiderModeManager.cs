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

    [Header("UI & HUD - Landscape")]
    public TMP_Text movesText;
    public TMP_Text scoreText;
    public TMP_Text timeText;

    [Header("UI & HUD - Portrait")]
    public TMP_Text portraitMovesText;
    public TMP_Text portraitScoreText;
    public TMP_Text portraitTimeText;

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
        // ДОБАВИТЬ ЭТУ СТРОКУ:
        SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());

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
        if (_isGameEnded) return;

        // --- ИСПРАВЛЕНИЕ: Разрешаем OnMoveMade, если это ручной ход (IsInputAllowed) 
        // ИЛИ если это работает автоматическая подсказка (isExecutingHint) ---
        if (!IsInputAllowed && !isExecutingHint) return;

        // Считаем ход, если это не просто сдача ряда из колоды
        if (!_isStockDrawFlag) GameQuestTracker.Instance?.RecordMove();

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
            SimpleMetricsTracker.Instance?.TrackLevelStart(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }

        if (_scoreManager) _scoreManager.ApplyPenalty();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();

        UpdateFullUI();

        // Запускаем перерасчет нового пути только для ручных ходов
        if (!isExecutingHint && _hasGameStarted && !_isGameEnded)
        {
            StartBackgroundSolver();
        }
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

        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.Undo))
            {
                return;
            }
            tutorialManager.AdvanceStep();
        }

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();

        IsInputAllowed = true;

        if (_defeatManager != null) _defeatManager.OnUndo();
        if (_scoreManager != null)
        {
            _scoreManager.ApplyPenalty();
        }

        // Отмена хода меняет стол — ранее посчитанный путь подсказки (и любой ещё
        // выполняющийся её расчёт) для нового состояния уже не актуален.
        // Сбрасываем кэш и запускаем пересчёт заново, как это делает OnMoveMade().
        StartBackgroundSolver();

        StopCoroutine("DelayedTableauUpdate");
        StartCoroutine("DelayedTableauUpdate");

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Back");
            AudioManager.Instance.PlaySound("Card_Whoosh_Out");
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
        // 1. Ходы
        string movesStr = "0";
        if (_hasGameStarted && StatisticsManager.Instance != null)
        {
            movesStr = $"{StatisticsManager.Instance.GetCurrentMoves()}";
        }

        if (movesText != null) movesText.text = movesStr;
        if (portraitMovesText != null) portraitMovesText.text = movesStr;

        // 2. Очки
        string scoreStr = "0";
        int score = _scoreManager != null ? _scoreManager.CurrentScore : 0;
        scoreStr = $"{score}";

        if (scoreText != null) scoreText.text = scoreStr;
        if (portraitScoreText != null) portraitScoreText.text = scoreStr;

        // 3. Время
        UpdateTimeUI();
    }

    private void UpdateTimeUI()
    {
        int totalSeconds = Mathf.FloorToInt(gameTimer);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        string timeStr = string.Format("{0}:{1:00}", minutes, seconds);

        if (timeText != null) timeText.text = timeStr;
        if (portraitTimeText != null) portraitTimeText.text = timeStr;
    }

    private void OnDestroy()
    {
        if (_hasGameStarted && !_isGameEnded)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }
        // ДОБАВИТЬ ЭТУ СТРОКУ:
        SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
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
        SimpleMetricsTracker.Instance?.TrackLevelWin(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
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

        // Сначала проверяем/запускаем автосборку завершённой масти —
        // чтобы ActiveFoundationAnimations был выставлен ДО StartBackgroundSolver().
        var checkMethod = typeof(SpiderTableauPile).GetMethod("CheckSuit", BindingFlags.NonPublic | BindingFlags.Instance);
        if (checkMethod != null) checkMethod.Invoke(target, null);

        OnMoveMade();

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
        // ДОБАВИТЬ ЭТУ СТРОКУ:
        SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());

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
        StartBackgroundSolver();
    }
    #region Hint System

    private List<SpiderHintSolver.MoveCommand> cachedHintPath = null;
    private Coroutine backgroundSolverCoroutine = null;
    private bool isExecutingHint = false;

    public void StartBackgroundSolver()
    {
        if (backgroundSolverCoroutine != null) StopCoroutine(backgroundSolverCoroutine);
        cachedHintPath = null;
        backgroundSolverCoroutine = StartCoroutine(BackgroundSolverRoutine());
    }

    private IEnumerator BackgroundSolverRoutine()
    {
        // Ждём, пока уляжется ЛЮБАЯ анимация, способная временно исказить состояние стола:
        // перетаскивание карт (dragLayer) и автосборку завершённой масти в фундамент
        // (ActiveFoundationAnimations — собранная K..A ещё физически лежит в колонке
        // до 0.5с после начала анимации). Иначе солвер может снять "грязный" снимок
        // стола и ложно решить, что ходов нет.
        while (true)
        {
            while ((dragLayer != null && dragLayer.childCount > 0) || ActiveFoundationAnimations > 0)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.1f);

            // За время ожидания могла запуститься новая анимация (например, CheckSuit
            // сработал уже после того как мы прошли первую проверку) — перепроверяем.
            if ((dragLayer != null && dragLayer.childCount > 0) || ActiveFoundationAnimations > 0)
            {
                continue;
            }

            break;
        }

        if (_isGameEnded)
        {
            backgroundSolverCoroutine = null;
            yield break;
        }

        Deal currentDeal = GetCurrentDealState();

        int suitsCount = GameSettings.SpiderSuitCount;
        if (suitsCount == 0) suitsCount = 1;

        yield return StartCoroutine(SpiderHintSolver.GetHintPathAsync(currentDeal, suitsCount, 15f, (path) => {
            cachedHintPath = path;
        }));

        backgroundSolverCoroutine = null;
    }

    public void RequestHint(System.Action onWaitStart, System.Action<bool> onHintResult)
    {
        if (!IsInputAllowed) return;
        StartCoroutine(HintRoutine(onWaitStart, onHintResult));
    }

    private IEnumerator HintRoutine(System.Action onWaitStart, System.Action<bool> onResult)
    {
        IsInputAllowed = false;

        if (cachedHintPath == null && backgroundSolverCoroutine == null)
            backgroundSolverCoroutine = StartCoroutine(BackgroundSolverRoutine());

        // Если солвер еще думает, показываем панель ожидания
        if (backgroundSolverCoroutine != null)
        {
            onWaitStart?.Invoke();
            while (backgroundSolverCoroutine != null) yield return null;
        }

        if (cachedHintPath != null && cachedHintPath.Count > 0)
        {
            var nextMove = cachedHintPath[0];
            cachedHintPath.RemoveAt(0);

            isExecutingHint = true;
            bool moveOk = ExecuteSolverMove(nextMove);

            if (!moveOk)
            {
                // Путь оказался невалиден для текущего стола — пересчитываем с нуля
                isExecutingHint = false;
                IsInputAllowed = true;
                StartBackgroundSolver();
                onResult?.Invoke(false);
                yield break;
            }

            // Ждём завершения анимации перемещения или раздачи
            if (dragLayer != null)
            {
                while (dragLayer.childCount > 0) yield return null;
            }

            // Ждём и завершения автосборки завершённой масти в фундамент, если ход
            // подсказки её вызвал — иначе снимок стола для следующей подсказки
            // может быть снят раньше, чем колонка реально освободится.
            while (ActiveFoundationAnimations > 0)
            {
                yield return null;
            }

            // Флаг снимаем только теперь: OnMoveMade (вызываемый в конце полёта карт
            // внутри AnimateSequenceAutoMove) успевает отработать с isExecutingHint = true
            // и не запускает лишний StartBackgroundSolver() поверх ещё не выполненного пути.
            isExecutingHint = false;

            IsInputAllowed = true;
            onResult?.Invoke(true);
        }
        else
        {
            IsInputAllowed = true;
            onResult?.Invoke(false);
        }
    }

    private Deal GetCurrentDealState()
    {
        Deal d = new Deal();
        for (int i = 0; i < 10; i++) d.tableau.Add(new List<CardInstance>());

        for (int i = 0; i < 10; i++)
        {
            foreach (var cardCtrl in pileManager.TableauPiles[i].cards)
            {
                var cardData = cardCtrl.GetComponent<CardData>();
                d.tableau[i].Add(new CardInstance(cardData.model, cardData.IsFaceUp()));
            }
        }
        if (pileManager.StockPile != null)
        {
            foreach (var cardCtrl in pileManager.StockPile.cards)
            {
                var cardData = cardCtrl.GetComponent<CardData>();
                d.stock.Push(new CardInstance(cardData.model, false));
            }
        }
        return d;
    }

    private bool ExecuteSolverMove(SpiderHintSolver.MoveCommand move)
    {
        if (move.Type == SpiderHintSolver.MoveType.StockDraw)
        {
            // TryDealRow()/OnStockClicked() сами проверяют IsInputAllowed,
            // а он сейчас выключен HintRoutine на время выполнения хода подсказки.
            // Снимаем блокировку ровно на время синхронного вызова (yield тут нет,
            // поэтому реальный пользовательский клик в этот момент вклиниться не может).
            bool wasAllowed = IsInputAllowed;
            IsInputAllowed = true;
            deckManager.TryDealRow();
            IsInputAllowed = wasAllowed;
            return true;
        }

        if (move.Type == SpiderHintSolver.MoveType.MoveColumn)
        {
            var source = pileManager.TableauPiles[move.From];
            var target = pileManager.TableauPiles[move.To];

            // Защита: если реальное состояние стола разошлось с тем, что предполагал
            // солвер (например, в столбце физически меньше карт, чем нужно для хода),
            // не пытаемся выполнить заведомо некорректный ход.
            if (source.cards.Count < move.Count)
            {
                cachedHintPath = null;
                return false;
            }

            List<CardController> sequence = new List<CardController>();
            for (int i = source.cards.Count - move.Count; i < source.cards.Count; i++)
            {
                sequence.Add(source.cards[i]);
            }

            // Финальная проверка по правилам паука: ранг верхней карты цели
            // должен быть на 1 больше ранга переносимой карты.
            var movingCard = sequence[0].cardModel;
            if (target.cards.Count > 0)
            {
                var targetTop = target.cards[target.cards.Count - 1].cardModel;
                if (targetTop.rank != movingCard.rank + 1)
                {
                    cachedHintPath = null;
                    return false;
                }
            }

            var method = typeof(SpiderModeManager).GetMethod("ExecuteProgrammaticSequenceMove", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(this, new object[] { sequence, source, target });
            }
            return true;
        }

        return false;
    }

    #endregion
}