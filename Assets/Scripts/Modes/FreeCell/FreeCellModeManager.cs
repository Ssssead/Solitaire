using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FreeCellModeManager : MonoBehaviour, ICardGameMode, IModeManager, ICardClickReceiver
{
    [Header("Core References")]
    public FreeCellPileManager pileManager;
    public DragManager dragManager;
    public UndoManager undoManager;
    public AnimationService animationService;
    public CardFactory cardFactory;
    public Canvas rootCanvas;
    public RectTransform dragLayer;
    public FreeCellTutorialManager tutorialManager;
    public GameUIController gameUI;
    public FreeCellIntroController introController;
    public FreeCellDeckManager deckManager;
    private bool isRestarting = false;
    private bool isGameWon = false;

    private bool hasGameStarted = false;

    [Header("UI & HUD - Landscape")]
    public TMP_Text moveLimitText;
    public TMP_Text movesText;
    public TMP_Text scoreText;
    public TMP_Text timeText;

    [Header("UI & HUD - Portrait")]
    public TMP_Text portraitMoveLimitText;
    public TMP_Text portraitMovesText;
    public TMP_Text portraitScoreText;
    public TMP_Text portraitTimeText;
    private float gameTimer = 0f;
    private bool isTimerRunning = false;
    [HideInInspector] public bool JustFailedDueToLimit = false;

    [Header("FreeCell Specific")]
    public Transform freeCellSlotsParent;
    private List<FreeCellPile> freeCells = new List<FreeCellPile>();
    [Header("Hint System")]
    public FreeCellHintSolver hintSolver;
    private Coroutine backgroundSolverCoroutine = null;
    private List<HintMoveCommand> cachedHintPath = null;
    private bool isExecutingHint = false;
    [Header("Rules")]
    public float tableauVerticalGap = 35f;

    private Difficulty currentDifficulty = Difficulty.Medium;
    private int currentSeed = 0;
    public GameType GameType => GameType.FreeCell;
    private int _cachedLimit = -1;

    private int _cachedEmptyLimit = -1;
    public ITutorialManager Tutorial => tutorialManager;

    public int CurrentDragCount { get; set; } = 1;
    public bool IsGrabbing { get; set; } = false;

    // --- ICardGameMode Properties ---
    public string GameName => "FreeCell";
    public int CurrentScore
    {
        get
        {
            var sm = GetComponent<FreeCellScoreManager>();
            return sm != null ? sm.CurrentScore : 0;
        }
    }
    public bool IsInputAllowed { get; set; } = true;
    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => animationService;
    public PileManager PileManager => pileManager;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => tableauVerticalGap;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    // --- ОТСЛЕЖИВАНИЕ РАЗРЕШЕНИЯ ---
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private void Start()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        StartCoroutine(LateInitialize());
    }

    public bool IsMatchInProgress()
    {
        return hasGameStarted;
    }

    private void Update()
    {
        // --- ЗАЩИТА РАЗМЕРОВ ПРИ ПОВОРОТЕ ЭКРАНА ---
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            StartCoroutine(DelayedLayoutFixRoutine());
        }

        if (pileManager == null) return;

        // 1. Ограничение SuperMove (Лимит)
        if (!IsInputAllowed && !hasGameStarted)
        {
            if (_cachedLimit != 5 || _cachedEmptyLimit != 5)
            {
                _cachedLimit = 5;
                _cachedEmptyLimit = 5;

                // --- ИСПРАВЛЕНИЕ: Вызываем общий метод вместо хардкода! ---
                // Он обновит и landscape, и portrait тексты.
                UpdateMoveLimitText();
            }
        }
        else
        {
            int currentLimit = GetMaxDragSequenceSize(false);
            int currentEmptyLimit = GetMaxDragSequenceSize(true);

            if (currentLimit != _cachedLimit || currentEmptyLimit != _cachedEmptyLimit)
            {
                _cachedLimit = currentLimit;
                _cachedEmptyLimit = currentEmptyLimit;
                UpdateMoveLimitText();
            }
        }

        // 2. Таймер
        if (hasGameStarted && !isGameWon)
        {
            if (!isTimerRunning) isTimerRunning = true;
            if (isTimerRunning) gameTimer += Time.deltaTime;
        }

        UpdateHUD();
    }
    private IEnumerator DelayedLayoutFixRoutine()
    {
        // Ждем 3 кадра, пока GameLayoutManager перестроит сетку
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();

        // 1. Форсируем фабрику обновить размер карт
        var factory = FindObjectOfType<CardFactory>();
        if (factory != null)
        {
            factory.UpdateAllCardsSize();
        }

        // 2. Восстанавливаем отступы у всех столбцов
        if (pileManager != null && pileManager.Tableau != null)
        {
            foreach (var tab in pileManager.Tableau)
            {
                if (tab != null)
                {
                    tab.ForceRecalculateLayout();
                }
            }
        }
    }
    private void UpdateHUD()
    {
        string movesStr = "0";
        if (StatisticsManager.Instance != null)
            movesStr = $"{StatisticsManager.Instance.GetCurrentMoves()}";

        if (movesText != null) movesText.text = movesStr;
        if (portraitMovesText != null) portraitMovesText.text = movesStr;

        string scoreStr = $"{CurrentScore}";
        if (scoreText != null) scoreText.text = scoreStr;
        if (portraitScoreText != null) portraitScoreText.text = scoreStr;

        UpdateTimeUI();
    }

    private void UpdateTimeUI()
    {
        int timeInSeconds = Mathf.FloorToInt(gameTimer);
        int minutes = timeInSeconds / 60;
        int seconds = timeInSeconds % 60;
        string timeStr = $"{minutes:0}:{seconds:00}";

        if (timeText != null) timeText.text = timeStr;
        if (portraitTimeText != null) portraitTimeText.text = timeStr;
    }

    public void InitializeMode(Difficulty difficulty, int seed)
    {
        currentDifficulty = difficulty;
        currentSeed = seed;
        if (pileManager != null) RestartGame();
    }

    private void Initialize()
    {
        if (pileManager == null) pileManager = GetComponent<FreeCellPileManager>();

        pileManager.InitializeFreeCell(this);
        if (undoManager != null) undoManager.Initialize(this);

        if (dragManager != null)
        {
            dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);
            var containers = pileManager.GetAllContainers();
            dragManager.RegisterAllContainers(containers);
        }

        currentDifficulty = GameSettings.CurrentDifficulty;
        StartNewGame();
    }

    private System.Collections.IEnumerator LateInitialize()
    {
        yield return new WaitForEndOfFrame();
        Initialize();
    }

    private void OnDestroy()
    {
        if (hasGameStarted && !isGameWon)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();

            // ДОБАВИТЬ ЭТУ СТРОКУ:
            SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }
    }

    public void RestartGame()
    {
        isGameWon = false;
        IsInputAllowed = false;
        isRestarting = true;

        if (hintSolver != null) hintSolver.CancelSearch(); // Гарантированно убиваем старый поиск

        pileManager.ClearAllPiles();
        foreach (var fc in freeCells) foreach (Transform child in fc.transform) Destroy(child.gameObject);

        StartNewGame();
        UpdateMoveLimitUI();
    }

    public void UpdateMoveLimitUI()
    {
        if (moveLimitText == null) return;
        _cachedLimit = GetMaxDragSequenceSize(false);
        _cachedEmptyLimit = GetMaxDragSequenceSize(true);
        UpdateMoveLimitText();
    }
    private void UpdateMoveLimitText()
    {
        string limitStr = "";
        if (_cachedLimit == _cachedEmptyLimit)
        {
            limitStr = $"{_cachedLimit}";
        }
        else
        {
            limitStr = $"{_cachedLimit}<color=#FFD700><size=75%>/{_cachedEmptyLimit}</size></color>";
        }

        if (moveLimitText != null) moveLimitText.text = limitStr;
        if (portraitMoveLimitText != null) portraitMoveLimitText.text = limitStr;
    }

    private void StartNewGame()
    {
        if (hasGameStarted && !isGameWon && StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameAbandoned();

        // ДОБАВИТЬ ЭТУ СТРОКУ:
        SimpleMetricsTracker.Instance?.TrackLevelQuit(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());

        string variant = GameSettings.GetCurrentVariantString(GameType.FreeCell) ?? "None";
        GameQuestTracker.Instance?.StartMatch("FreeCell", GameSettings.CurrentDifficulty, variant);

        isGameWon = false;
        IsInputAllowed = false;
        currentDifficulty = GameSettings.CurrentDifficulty;

        hasGameStarted = false;
        isTimerRunning = false;
        gameTimer = 0f;

        var scoreMgr = GetComponent<FreeCellScoreManager>();
        if (scoreMgr != null) scoreMgr.ResetScore();

        if (undoManager != null) undoManager.ClearHistory();

        UpdateHUD();

        if (introController != null) introController.PrepareIntro(isRestarting);

        if (GameSettings.IsTutorialMode && tutorialManager != null)
        {
            StartCoroutine(TutorialIntroRoutine());
            return;
        }

        bool dealLoadedFromCache = false;

        if (DealCacheSystem.Instance != null)
        {
            Deal cachedDeal = DealCacheSystem.Instance.GetDeal(GameType.FreeCell, currentDifficulty, currentSeed);
            if (cachedDeal != null)
            {
                dealLoadedFromCache = true;
                StartCoroutine(IntroSequenceRoutine(cachedDeal));
            }
        }

        if (!dealLoadedFromCache)
        {
            var generator = GetComponent<FreeCellGenerator>() ?? gameObject.AddComponent<FreeCellGenerator>();
            StartCoroutine(generator.GenerateDeal(currentDifficulty, currentSeed, (deal, m) =>
            {
                StartCoroutine(IntroSequenceRoutine(deal));
            }));
        }
    }
    // --- НОВЫЕ МЕТОДЫ ОЖИДАНИЯ АНИМАЦИИ СТАРТА ---
    private IEnumerator IntroSequenceRoutine(Deal deal)
    {
        if (introController != null)
        {
            yield return StartCoroutine(introController.PlayIntroSequence(deal, isRestarting));
        }
        isRestarting = false;
        IsInputAllowed = true;
        StartBackgroundSolver(); // ЗАПУСК СОЛВЕРА СО СТАРТА!
    }

    private IEnumerator TutorialIntroRoutine()
    {
        if (tutorialManager != null)
        {
            yield return StartCoroutine(tutorialManager.PlayTutorialIntro(null));
        }
        isRestarting = false;
        IsInputAllowed = true;
        StartBackgroundSolver();
    }
    private void ApplyDeal(Deal deal)
    {
        for (int i = 0; i < 8; i++)
        {
            if (i >= deal.tableau.Count) break;

            var pile = pileManager.GetTableau(i);

            foreach (var cData in deal.tableau[i])
            {
                var cModel = new CardModel(cData.Card.suit, cData.Card.rank);
                var card = cardFactory.CreateCard(cModel, pile.transform, Vector2.zero);

                card.GetComponent<CardData>().SetFaceUp(true, false);
                pile.AddCard(card, true);

                if (dragManager != null) dragManager.RegisterCardEvents(card);
            }
            pile.StartLayoutAnimationPublic();
        }
    }

    public int GetMaxDragSequenceSize(bool targetIsEmptyColumn = false)
    {
        if (IsGrabbing) return 999;

        int emptyFC = 0;
        foreach (var fc in pileManager.FreeCells) if (fc.IsEmpty) emptyFC++;

        int emptyCols = 0;
        foreach (var tab in pileManager.Tableau) if (tab.cards.Count == 0) emptyCols++;

        if (targetIsEmptyColumn && emptyCols > 0) emptyCols--;

        int rawLimit = (1 + emptyFC) * (int)Mathf.Pow(2, emptyCols);
        return Mathf.Min(rawLimit, 13);
    }

    public void ShakeMoveLimitUI()
    {
        // <--- ��������� ���� ������ --->
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Error");
        if (moveLimitText != null && moveLimitText.transform.parent != null)
        {
            RectTransform target = moveLimitText.transform.parent.GetComponent<RectTransform>();
            if (target != null) StartCoroutine(ShakeRoutine(target));
        }
    }

    private IEnumerator ShakeRoutine(RectTransform target)
    {
        if (target == null) yield break;
        // <--- ���� ���������� ������ ����� (��� � �����, �� ��������) --->
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Card_Error");
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Shake", 0.25f, 0.1f);
        }
        Vector3 originalPos = target.anchoredPosition;
        float elapsed = 0f;
        float duration = 0.25f;
        float magnitude = 15f;
        while (elapsed < duration)
        {
            float xOffset = Mathf.Sin(elapsed * 40f) * magnitude;
            target.anchoredPosition = new Vector3(originalPos.x + xOffset, originalPos.y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        target.anchoredPosition = originalPos;
    }

    public void CheckGameState()
    {
        if (isGameWon) return;

        int totalCardsInFoundation = 0;
        foreach (var f in pileManager.Foundations) totalCardsInFoundation += f.Count;

        if (totalCardsInFoundation == 52)
        {
            isGameWon = true;
            IsInputAllowed = false;
            StartCoroutine(VictorySequence());
            return;
        }

        if (!HasAnyValidMove())
        {

            StartCoroutine(LossSequence());

        }
    }

    private IEnumerator VictorySequence()
    {
        isGameWon = true;
        IsInputAllowed = false;

        yield return new WaitForSeconds(1.0f);

        int finalMoves = 0;
        if (StatisticsManager.Instance != null) finalMoves = StatisticsManager.Instance.GetCurrentMoves();

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameWon(CurrentScore);

        if (gameUI != null) gameUI.OnGameWon(finalMoves);
        else FindObjectOfType<GameUIController>()?.OnGameWon(finalMoves);
        SimpleMetricsTracker.Instance?.TrackLevelWin(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
    }
    private IEnumerator LossSequence()
    {
        yield return new WaitForSeconds(1);
        if (gameUI != null) gameUI.OnGameLost();

    }
    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        // --- ���������: ����������� ���� �������� ---
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, container))
            {
                tutorialManager.AdvanceStep();
            }
        }
        // --------------------------------------------

        OnMoveMade();
        CheckGameState();
    }

    public void OnMoveMade()
    {
        JustFailedDueToLimit = false;
        CurrentDragCount = 1;

        // Если это ручной ход - убиваем кэш и ищем новый путь
        if (!isExecutingHint)
        {
            StartBackgroundSolver();
        }

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true; // Подстраховка
            if (StatisticsManager.Instance != null)
            {
                string variant = GameSettings.GetCurrentVariantString(GameType.FreeCell) ?? "None";
                StatisticsManager.Instance.OnGameStarted("FreeCell", currentDifficulty, variant);
            }
            SimpleMetricsTracker.Instance?.TrackLevelStart(GameName, GameSettings.CurrentDifficulty.ToString().ToLower());
        }

        GameQuestTracker.Instance?.RecordMove();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
    }

    public void OnUndoAction()
    {
        if (!IsInputAllowed) return;

        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.Undo)) return;
        }

        // Отмена хода всегда меняет стол — путь подсказки для него больше не
        // актуален. На всякий случай (защита от гонок) явно сбрасываем
        // isExecutingHint ПЕРЕД OnMoveMade(), чтобы её внутренний
        // "if (!isExecutingHint) StartBackgroundSolver()" точно сработал,
        // а не пропустил пересчёт.
        isExecutingHint = false;

        OnMoveMade();
        isGameWon = false;
        IsInputAllowed = true;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Back");
            StartCoroutine(DelayedUndoDropSound(0.25f));
        }

        var scoreMgr = GetComponent<FreeCellScoreManager>();
        if (scoreMgr != null) scoreMgr.OnUndo();

        UpdateMoveLimitUI();
    }
    private IEnumerator DelayedUndoDropSound(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");
    }

    public void OnStockClicked() { }
    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed || isGameWon || card == null) return;

        // На ПК (Desktop) запускаем авто-перенос по двойному клику
        if (GameSettings.AutoMoveClickMode == 1)
        {
            ExecuteAutoMove(card);
        }
    }
    private void ExecuteAutoMove(CardController card)
    {
        if (tutorialManager != null && tutorialManager.IsTutorialActive) return;

        ICardContainer sourceContainer = card.GetComponentInParent<ICardContainer>();
        if (sourceContainer == null || sourceContainer is FoundationPile) return;

        List<CardController> sequence = new List<CardController> { card };

        if (sourceContainer is TableauPile tab)
        {
            int cardIndex = tab.IndexOfCard(card);
            bool isTopCard = (cardIndex == tab.cards.Count - 1);

            if (!isTopCard)
            {
                var potentialSeq = tab.GetFaceUpSequenceFrom(cardIndex);

                if (potentialSeq != null && potentialSeq.Count > 0 && potentialSeq[0] == card)
                {
                    if (IsValidFreeCellSequence(potentialSeq))
                    {
                        sequence = potentialSeq;
                    }
                    else
                    {
                        StartCoroutine(ShakeSequenceRoutine(potentialSeq, true));
                        return;
                    }
                }
                else
                {
                    StartCoroutine(ShakeSequenceRoutine(new List<CardController> { card }, true));
                    return;
                }
            }
        }

        // Передаем переменную limitFailed, чтобы узнать причину отказа
        bool limitFailed;
        bool moved = TryDoubleClickMove(sequence, sourceContainer, out limitFailed);

        if (moved)
        {
            OnMoveMade();
            UpdateMoveLimitUI();
            Invoke(nameof(CheckGameState), 0.3f);
        }
        else
        {
            // Если ход сорвался ИМЕННО из-за нехватки пустых ячеек
            if (limitFailed)
            {
                ShakeMoveLimitUI(); // Трясем табличку UI (она сама проиграет звук ошибки)
            }

            // Трясем сами карты (звук ошибки отключаем, если он уже сыграл в ShakeMoveLimitUI)
            StartCoroutine(ShakeSequenceRoutine(sequence, !limitFailed));
        }
    }
    private bool IsValidFreeCellSequence(List<CardController> seq)
    {
        if (seq == null || seq.Count <= 1) return true;

        for (int i = 0; i < seq.Count - 1; i++)
        {
            var current = seq[i];
            var next = seq[i + 1];

            if (current == null || next == null) return false;

            // Проверяем ранг (должен убывать ровно на 1)
            if (current.cardModel.rank != next.cardModel.rank + 1) return false;

            // Проверяем цвет (должен чередоваться)
            bool isCurrentRed = current.cardModel.suit == Suit.Diamonds || current.cardModel.suit == Suit.Hearts;
            bool isNextRed = next.cardModel.suit == Suit.Diamonds || next.cardModel.suit == Suit.Hearts;

            if (isCurrentRed == isNextRed) return false;
        }

        return true;
    }
    private bool TryDoubleClickMove(List<CardController> sequence, ICardContainer source, out bool limitFailed)
    {
        limitFailed = false;
        CardController card = sequence[0];

        bool isTopCard = false;
        if (source is TableauPile tab) isTopCard = (tab.cards.Count > 0 && tab.cards[tab.cards.Count - 1] == card);
        else isTopCard = true;

        // А. Попытка перенести в "Дом" (Foundation)
        if (isTopCard)
        {
            foreach (var f in pileManager.Foundations)
            {
                if (f.CanAccept(card))
                {
                    ExecuteProgrammaticSequenceMove(sequence, source, f);
                    return true;
                }
            }
        }

        // Б. Попытка перенести на другой столбец (Tableau)
        foreach (var t in pileManager.Tableau)
        {
            if (t == source) continue;

            // СНАЧАЛА проверяем, подходит ли карта по правилам игры (цвет/ранг)
            if (t.CanAccept(card))
            {
                bool isEmptyTarget = (t.cards.Count == 0);
                int currentLimit = GetMaxDragSequenceSize(isEmptyTarget);

                // Если по правилам подходит, но лимит превышен — запоминаем это!
                if (sequence.Count > currentLimit)
                {
                    limitFailed = true;
                    continue; // Продолжаем цикл, вдруг есть другой пустой столбец, куда лимита хватит
                }

                ExecuteProgrammaticSequenceMove(sequence, source, t);
                return true;
            }
        }

        // В. Попытка перенести в свободную ячейку (FreeCell)
        if (isTopCard && !(source is FreeCellPile))
        {
            foreach (var fc in pileManager.FreeCells)
            {
                if (fc.CanAccept(card))
                {
                    ExecuteProgrammaticSequenceMove(sequence, source, fc);
                    return true;
                }
            }
        }

        return false;
    }

    // 3. НОВЫЙ метод тряски, который умеет трясти целые ряды карт
    private IEnumerator ShakeSequenceRoutine(List<CardController> sequence, bool playErrorSound = true)
    {
        if (sequence == null || sequence.Count == 0) yield break;

        AudioSource shakeSource = null;
        float baseShakeVolume = 1f;

        if (AudioManager.Instance != null)
        {
            // Играем стук ошибки только если табличка лимита его уже не проиграла
            if (playErrorSound) AudioManager.Instance.PlaySound("Card_Error");

            shakeSource = AudioManager.Instance.PlaySound("Card_Shake");
            if (shakeSource != null) baseShakeVolume = shakeSource.volume;
        }

        List<Vector3> originalPositions = new List<Vector3>();
        foreach (var c in sequence)
        {
            originalPositions.Add(c.rectTransform.anchoredPosition);
        }

        float elapsed = 0f;
        float duration = 0.25f;
        float magnitude = 10f;
        float speed = 50f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float phase = 1f - (elapsed / duration);
            float xOffset = Mathf.Sin(elapsed * speed) * magnitude * phase;

            for (int i = 0; i < sequence.Count; i++)
            {
                if (sequence[i] != null)
                {
                    Vector3 orig = originalPositions[i];
                    sequence[i].rectTransform.anchoredPosition = new Vector3(orig.x + xOffset, orig.y, orig.z);
                }
            }

            if (shakeSource != null && shakeSource.isPlaying)
            {
                float velocityFactor = Mathf.Abs(Mathf.Cos(elapsed * speed));
                shakeSource.volume = baseShakeVolume * velocityFactor * phase;
            }

            yield return null;
        }

        for (int i = 0; i < sequence.Count; i++)
        {
            if (sequence[i] != null)
            {
                sequence[i].rectTransform.anchoredPosition = originalPositions[i];
            }
        }

        if (shakeSource != null && shakeSource.isPlaying)
        {
            shakeSource.Stop();
            shakeSource.volume = baseShakeVolume;
        }
    }
    private void ExecuteProgrammaticSequenceMove(List<CardController> sequence, ICardContainer source, ICardContainer target)
    {
        if (sequence == null || sequence.Count == 0) return;

        List<Transform> prevParents = new List<Transform>();
        List<Vector3> prevPositions = new List<Vector3>();
        List<int> prevSibs = new List<int>();

        // Сохраняем исходные данные для функции отмены (Undo)
        foreach (var c in sequence)
        {
            prevParents.Add(c.transform.parent);
            prevPositions.Add(c.transform.localPosition);
            prevSibs.Add(c.transform.GetSiblingIndex());
        }

        // Логически открепляем карты от источника
        if (source is TableauPile tab)
        {
            int idx = tab.IndexOfCard(sequence[0]);
            if (idx != -1) tab.RemoveSequenceFrom(idx);
        }

        // Бронируем дом (если летим туда)
        if (target is FoundationPile found) found.ReserveCard(sequence[0]);

        // Физический перенос
        if (target is TableauPile targetTab)
        {
            // ВАЖНО: Мы больше не добавляем карты в стопку мгновенно!
            // Запускаем единую корутину, которая доставит все карты и ТОЛЬКО ПОТОМ обновит макет.
            StartCoroutine(AnimateSequenceAutoMove(sequence, targetTab));
        }
        else
        {
            // Перенос одной карты (в Foundation или FreeCell)
            var card = sequence[0];
            var cardData = card.GetComponent<CardData>();
            if (cardData != null) cardData.SetFaceUp(true, false);

            if (DragLayer != null)
            {
                card.rectTransform.SetParent(DragLayer, true);
                card.rectTransform.SetAsLastSibling();
            }
            card.ForceSnapToContainer(target);
        }

        // Записываем ход в историю
        if (undoManager != null)
        {
            undoManager.RecordMove(
                sequence,
                source, target,
                prevParents,
                prevPositions,
                prevSibs
            );
        }

        // Обновляем счет
        var scoreMgr = GetComponent<FreeCellScoreManager>();
        if (scoreMgr != null) scoreMgr.OnCardMove(source, target);
    }

    // --- НОВОЕ: Единая корутина для синхронного полета и безопасного обновления макета ---
    private System.Collections.IEnumerator AnimateSequenceAutoMove(List<CardController> sequence, TableauPile targetTab)
    {
        // 1. Блокируем целевую стопку от кликов на время полета
        targetTab.SetAnimatingCard(true);

        // 2. Подготовка карт к полету
        List<Vector3> startPositions = new List<Vector3>();
        foreach (var c in sequence)
        {
            var cardData = c.GetComponent<CardData>();
            if (cardData != null) cardData.SetFaceUp(true, false);

            c.StopAllCoroutines();
            startPositions.Add(c.rectTransform.position);

            if (DragLayer != null)
            {
                c.rectTransform.SetParent(DragLayer, true);
                c.rectTransform.SetAsLastSibling();
            }
            if (c.canvasGroup != null) c.canvasGroup.blocksRaycasts = false;
        }

        Canvas.ForceUpdateCanvases();

        // 3. Расчет конечных точек
        Vector2 topAnchor = targetTab.GetDropAnchoredPosition(sequence[0]);
        List<Vector3> targetPositions = new List<Vector3>();

        for (int i = 0; i < sequence.Count; i++)
        {
            Vector2 anc = new Vector2(topAnchor.x, topAnchor.y - i * TableauVerticalGap);
            Vector3 world = Vector3.zero;

            if (AnimationService != null)
            {
                world = AnimationService.AnchoredToWorldPosition(targetTab.transform as RectTransform, anc);
            }
            else
            {
                GameObject t = new GameObject("tmp");
                t.transform.SetParent(targetTab.transform, false);
                t.AddComponent<RectTransform>().anchoredPosition = anc;
                Canvas.ForceUpdateCanvases();
                world = t.transform.position;
                Destroy(t);
            }
            targetPositions.Add(world);
        }

        // 4. Синхронный полет всех карт ряда
        float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < sequence.Count; i++)
            {
                sequence[i].rectTransform.position = Vector3.Lerp(startPositions[i], targetPositions[i], t);
            }
            yield return null;
        }

        // 5. Приземление и физическая привязка
        for (int i = 0; i < sequence.Count; i++)
        {
            var c = sequence[i];
            c.rectTransform.position = targetPositions[i];
            c.rectTransform.SetParent(targetTab.transform, true);

            if (c.canvasGroup != null)
            {
                c.canvasGroup.blocksRaycasts = true;
                c.canvasGroup.interactable = true;
            }
        }

        // 6. САМОЕ ВАЖНОЕ: Логическое добавление и перерасчет макета ТОЛЬКО ПОСЛЕ приземления!
        targetTab.AddCardsBatch(sequence, true);
        targetTab.SetAnimatingCard(false);      // Снимаем блокировку со стопки
        targetTab.StartLayoutAnimationPublic(); // Запускаем родной перерасчет отступов

        if (AnimationService != null) AnimationService.ReorderContainerZ(targetTab.transform);
    }
    private void ExecuteProgrammaticMove(CardController card, ICardContainer source, ICardContainer target)
    {
        Transform prevParent = card.transform.parent;
        int prevSib = card.transform.GetSiblingIndex();

        // --- ����������� 3: ���������� �������� ��������� ������� �� ������ ������ ---
        Vector3 prevPos = card.transform.localPosition;

        // ���������� �������� �� ���������
        if (source is TableauPile tab)
        {
            int idx = tab.IndexOfCard(card);
            if (idx != -1) tab.RemoveSequenceFrom(idx);
        }

        // ��������� ����������� ���� � Foundation, ����� ������������� ���������
        if (target is FoundationPile found) found.ReserveCard(card);

        // ������ ������. � ����� �������� ForceSnapToContainer ���� ������� AcceptCard � �������
        card.ForceSnapToContainer(target);

        // ������ Undo
        if (undoManager != null)
        {
            undoManager.RecordMove(
                new List<CardController> { card },
                source, target,
                new List<Transform> { prevParent },
                new List<Vector3> { prevPos }, // �������� �������� ������� ������ Vector3.zero!
                new List<int> { prevSib }
            );
        }

        // ����
        var scoreMgr = GetComponent<FreeCellScoreManager>();
        if (scoreMgr != null) scoreMgr.OnCardMove(source, target);
    }

    // �������� ������ ��� ���������� ����� � ���������� ���������
    private IEnumerator ShakeCardRoutine(RectTransform target)
    {
        if (target == null) yield break;

        // --- �������� ������� �: ���������� ---
        AudioSource shakeSource = null;
        float baseShakeVolume = 1f;

        if (AudioManager.Instance != null)
        {

            // 2. �������� ������ �� AudioSource ��������, ����� ������� ���������
            shakeSource = AudioManager.Instance.PlaySound("Card_Shake");
            if (shakeSource != null)
            {
                baseShakeVolume = shakeSource.volume; // ���������� ������� ��������� �� ��������
            }
        }

        Vector3 originalPos = target.anchoredPosition;
        float elapsed = 0f;
        float duration = 0.25f;
        float magnitude = 10f; // ��������� ������ �����
        float speed = 50f;     // �������� (�������) ���������

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float phase = 1f - (elapsed / duration); // ������� ��������� (�� 1 �� 0)

            // ������: �������� �� X
            float xOffset = Mathf.Sin(elapsed * speed) * magnitude * phase;
            target.anchoredPosition = new Vector3(originalPos.x + xOffset, originalPos.y, originalPos.z);

            // --- �������� ������� �: �������� ---
            if (shakeSource != null && shakeSource.isPlaying)
            {
                // ����� ����������� �� Sin (��� Cos), ����� �������� ��������.
                // Mathf.Abs ��������� � � ������������� ��������� �� 0 �� 1.
                float velocityFactor = Mathf.Abs(Mathf.Cos(elapsed * speed));

                // ��������� ��������� = ������� * �������� * ����� ��������� (phase)
                shakeSource.volume = baseShakeVolume * velocityFactor * phase;
            }

            yield return null;
        }

        if (target != null) target.anchoredPosition = originalPos;

        // ��������: ������������� ���� �������� ������, ���� �� ������� ��������
        if (shakeSource != null && shakeSource.isPlaying)
        {
            shakeSource.Stop();
            shakeSource.volume = baseShakeVolume; // ���������� ��������� � ����� ��� ����
        }
    }
    public void OnCardClicked(CardController card)
    {
        // УБРАНО: hasGameStarted = true; (Она ломала старт статистики)

        if (!IsInputAllowed || isGameWon) return;

        // Если включен авто-перенос по одному клику — запускаем авто-перенос
        if (GameSettings.AutoMoveClickMode == 0)
        {
            var dragManager = FindObjectOfType<DragManager>();
            dragManager?.ForceSnapBackLastDrop();

            ExecuteAutoMove(card);
        }
    }
    public void OnCardLongPressed(CardController card) { }
    public void OnKeyboardPick(CardController card) { }

    public bool OnDropToBoard(CardController card, Vector2 pos)
    {
        foreach (var fc in pileManager.FreeCells)
        {
            if (IsPointOverRect(fc.transform as RectTransform, pos))
            {
                if (fc.CanAccept(card))
                {
                    // --- ���������: ������ ��������� ---
                    if (tutorialManager != null && tutorialManager.IsTutorialActive &&
                        !tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, fc)) return false;
                    // -----------------------------------

                    OnCardDroppedToContainer(card, fc);
                    return true;
                }
            }
        }

        foreach (var tab in pileManager.Tableau)
        {
            RectTransform targetRect = tab.transform as RectTransform;
            if (tab.cards.Count > 0)
            {
                targetRect = tab.cards[tab.cards.Count - 1].rectTransform;
            }

            if (IsPointOverRect(targetRect, pos, padding: 50f))
            {
                if (tab.CanAccept(card))
                {
                    // --- ���������: ������ ��������� ---
                    if (tutorialManager != null && tutorialManager.IsTutorialActive &&
                        !tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, tab)) return false;
                    // -----------------------------------

                    OnCardDroppedToContainer(card, tab);
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPointOverRect(RectTransform rect, Vector2 screenPos, float padding = 0f)
    {
        if (rect == null) return false;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam);
    }

    public ICardContainer FindNearestContainer(CardController card, Vector2 pos, float maxDist)
    {
        ICardContainer foundContainer = null;

        foreach (var fc in pileManager.FreeCells)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(fc.transform as RectTransform, pos, rootCanvas.worldCamera))
            {
                if (fc.CanAccept(card)) { foundContainer = fc; break; }
            }
        }

        if (foundContainer == null)
        {
            foreach (var tab in pileManager.Tableau)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(tab.transform as RectTransform, pos, rootCanvas.worldCamera))
                {
                    if (tab.CanAccept(card)) { foundContainer = tab; break; }
                }
            }
        }

        if (foundContainer == null)
        {
            foreach (var f in pileManager.Foundations)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(f.transform as RectTransform, pos, rootCanvas.worldCamera))
                {
                    if (f.CanAccept(card)) { foundContainer = f; break; }
                }
            }
        }

        // --- ���������: ������ ��������� ---
        if (foundContainer != null && tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            if (!tutorialManager.IsActionAllowed(TutorialActionType.MoveCard, card, foundContainer)) return null;
        }
        // -----------------------------------

        return foundContainer;
    }

    private bool IsRed(CardController c)
    {
        return c.cardModel.suit == Suit.Diamonds || c.cardModel.suit == Suit.Hearts;
    }

    // Длина валидной последовательности (убывающий ранг, чередующийся цвет), считая с верха стопки
    private int GetValidSequenceLengthFromTop(TableauPile tab)
    {
        int count = tab.cards.Count;
        if (count == 0) return 0;

        int length = 1;
        for (int i = count - 1; i > 0; i--)
        {
            var current = tab.cards[i];
            var below = tab.cards[i - 1];

            bool isRankSequential = below.cardModel.rank == current.cardModel.rank + 1;
            bool isColorAlternating = IsRed(below) != IsRed(current);

            if (isRankSequential && isColorAlternating) length++;
            else break;
        }
        return length;
    }

    // Проверяет, есть ли у карты "полезное" место, куда её можно положить:
    // в Foundation, в свободную ячейку, в пустую колонку, или на валидный верх
    // другой непустой tableau-стопки (кроме excludeSource/excludeTarget).
    private bool CardHasUsefulDestination(CardController card, TableauPile excludeSource, TableauPile excludeTarget, int emptyFreeCells, int emptyCols)
    {
        foreach (var f in pileManager.Foundations)
            if (f.CanAccept(card)) return true;

        if (emptyFreeCells > 0) return true;
        if (emptyCols > 0) return true;

        foreach (var t in pileManager.Tableau)
        {
            if (t == excludeSource || t == excludeTarget) continue;
            if (t.cards.Count == 0) continue;

            var top = t.cards[t.cards.Count - 1];
            bool isColorDifferent = IsRed(top) != IsRed(card);
            bool isRankCorrect = top.cardModel.rank == card.cardModel.rank + 1;
            if (isColorDifferent && isRankCorrect) return true;
        }
        return false;
    }

    // Возвращает true, если в текущей позиции существует ход, который реально
    // меняет ситуацию (а не просто бесконечно перекладывает карты по кругу).
    private bool HasAnyValidMove()
    {
        int emptyFreeCells = 0;
        foreach (var fc in pileManager.FreeCells) if (fc.IsEmpty) emptyFreeCells++;

        int emptyCols = 0;
        foreach (var tab in pileManager.Tableau) if (tab.cards.Count == 0) emptyCols++;

        // --- 1. Карты во FreeCell: в фонд, либо на стол (освобождает ячейку) ---
        foreach (var fc in pileManager.FreeCells)
        {
            if (fc.IsEmpty) continue;

            var card = fc.GetComponentInChildren<CardController>();
            if (card == null) continue;

            foreach (var f in pileManager.Foundations)
                if (f.CanAccept(card)) return true;

            foreach (var t in pileManager.Tableau)
                if (t.CanAccept(card)) return true;
        }

        foreach (var tab in pileManager.Tableau)
        {
            if (tab.cards.Count == 0) continue;

            var headCard = tab.cards[tab.cards.Count - 1];

            // --- 2. Верхняя карта -> Foundation: всегда прогресс ---
            foreach (var f in pileManager.Foundations)
                if (f.CanAccept(headCard)) return true;

            // --- 3. Верхняя карта -> свободная ячейка: даёт дополнительный ресурс ---
            if (emptyFreeCells > 0) return true;

            // --- 4. SuperMove (перенос связки из 2+ карт) - всегда существенное изменение ---
            int seqLen = GetValidSequenceLengthFromTop(tab);
            for (int len = 2; len <= seqLen; len++)
            {
                var seqHead = tab.cards[tab.cards.Count - len];

                foreach (var t in pileManager.Tableau)
                {
                    if (t == tab) continue;

                    bool isEmptyTarget = (t.cards.Count == 0);

                    // Перенос всей стопки целиком в пустую колонку - ничего не меняет
                    if (isEmptyTarget && len == tab.cards.Count) continue;

                    int limit = GetMaxDragSequenceSize(isEmptyTarget);
                    if (len > limit) continue;

                    if (isEmptyTarget) return true;

                    var targetTop = t.cards[t.cards.Count - 1];
                    bool isColorDifferent = IsRed(targetTop) != IsRed(seqHead);
                    bool isRankCorrect = targetTop.cardModel.rank == seqHead.cardModel.rank + 1;
                    if (isColorDifferent && isRankCorrect) return true;
                }
            }

            // --- 5. Перенос одной верхней карты на другую tableau-стопку ---
            // Учитываем "бесполезные" перекладывания: ход легален, но если он не освобождает
            // исходную колонку и не открывает под собой карту, у которой появляется
            // реальный дальнейший ход - такой ход не считается "выходом" из тупика.
            foreach (var t in pileManager.Tableau)
            {
                if (t == tab) continue;

                bool isEmptyTarget = (t.cards.Count == 0);
                bool moveIsLegal;

                if (isEmptyTarget)
                {
                    moveIsLegal = true;
                }
                else
                {
                    var targetTop = t.cards[t.cards.Count - 1];
                    bool isColorDifferent = IsRed(targetTop) != IsRed(headCard);
                    bool isRankCorrect = targetTop.cardModel.rank == headCard.cardModel.rank + 1;
                    moveIsLegal = isColorDifferent && isRankCorrect;
                }

                if (!moveIsLegal) continue;

                if (tab.cards.Count == 1)
                {
                    // Перенос единственной карты в УЖЕ пустую колонку просто меняет местами,
                    // какая колонка пустая - реального прогресса нет.
                    if (isEmptyTarget) continue;

                    // А перенос на верх другой непустой стопки реально опустошает исходную
                    // колонку - это новый ресурс (пустая колонка).
                    return true;
                }

                // Карта, которая окажется на вершине исходной стопки после хода
                var exposed = tab.cards[tab.cards.Count - 2];

                // Если целевая колонка была пустой, она перестанет быть пустой после хода
                int availableEmptyCols = isEmptyTarget ? emptyCols - 1 : emptyCols;

                if (CardHasUsefulDestination(exposed, tab, t, emptyFreeCells, availableEmptyCols))
                    return true;

                // exposed может лечь прямо на headCard, который теперь на верху t
                bool expColorDifferent = IsRed(headCard) != IsRed(exposed);
                bool expRankCorrect = headCard.cardModel.rank == exposed.cardModel.rank + 1;
                if (expColorDifferent && expRankCorrect) return true;
            }
        }

        return false;
    }
    public void StartBackgroundSolver()
    {
        Debug.Log("[HintDebug] StartBackgroundSolver вызван.");
        if (backgroundSolverCoroutine != null) StopCoroutine(backgroundSolverCoroutine);
        if (hintSolver != null) hintSolver.CancelSearch();

        cachedHintPath = null;
        backgroundSolverCoroutine = StartCoroutine(BackgroundSolverRoutine());
    }

    private IEnumerator BackgroundSolverRoutine()
    {
        Debug.Log("[HintDebug] BackgroundSolver: Ждем завершения анимаций карт...");

        // ЗАЩИТА: Ждем максимум 3 секунды, чтобы не зависнуть из-за забагованной карты
        float animWaitTimer = 0f;
        while ((dragLayer != null && dragLayer.childCount > 0) || CheckActiveAnimations())
        {
            animWaitTimer += Time.deltaTime;
            if (animWaitTimer > 3f)
            {
                Debug.LogWarning("[HintDebug] ВНИМАНИЕ: Анимации шли дольше 3 секунд! Принудительно идем дальше.");
                break;
            }
            yield return null;
        }

        Debug.Log("[HintDebug] BackgroundSolver: Анимации завершены. Ждем 0.1с для логики...");
        yield return new WaitForSeconds(0.1f);

        if (isGameWon) yield break;

        bool solverFinished = false;
        if (hintSolver == null) hintSolver = gameObject.AddComponent<FreeCellHintSolver>();

        Debug.Log("[HintDebug] BackgroundSolver: Запускаем алгоритм hintSolver.FindPath()...");
        hintSolver.FindPath(pileManager, path => {
            Debug.Log($"[HintDebug] BackgroundSolver: Алгоритм вернул ответ! Путь найден: {path != null}");
            cachedHintPath = path;
            solverFinished = true;
        });

        while (!solverFinished) yield return null;

        Debug.Log("[HintDebug] BackgroundSolver: Корутина полностью завершена.");
        backgroundSolverCoroutine = null;
    }

    private bool CheckActiveAnimations()
    {
        foreach (var fc in pileManager.FreeCells)
        {
            var c = fc.GetComponentInChildren<CardController>();
            if (c != null && c.IsAnimating) return true;
        }
        foreach (var tab in pileManager.Tableau)
        {
            foreach (var c in tab.cards) if (c.IsAnimating) return true;
        }
        return false;
    }

    public void RequestHint(System.Action onWaitStart, System.Action<bool> onHintResult)
    {
        Debug.Log("[HintDebug] RequestHint: Игрок нажал на кнопку подсказки.");
        if (isGameWon || !IsInputAllowed)
        {
            Debug.Log($"[HintDebug] RequestHint ОТКЛОНЕН. isGameWon={isGameWon}, IsInputAllowed={IsInputAllowed}");
            onHintResult?.Invoke(false);
            return;
        }
        StartCoroutine(HintRoutine(onWaitStart, onHintResult));
    }

    private IEnumerator HintRoutine(System.Action onWaitStart, System.Action<bool> onResult)
    {
        Debug.Log("[HintDebug] HintRoutine: Блокируем ввод.");
        IsInputAllowed = false;

        if (cachedHintPath == null && backgroundSolverCoroutine == null)
        {
            Debug.Log("[HintDebug] HintRoutine: Кэш пуст, солвер не работает. Запускаем принудительно.");
            backgroundSolverCoroutine = StartCoroutine(BackgroundSolverRoutine());
        }

        if (backgroundSolverCoroutine != null)
        {
            Debug.Log("[HintDebug] HintRoutine: Включаем UI загрузки и ждем фон...");
            onWaitStart?.Invoke();

            float solverWaitTimer = 0f;
            while (backgroundSolverCoroutine != null)
            {
                solverWaitTimer += Time.deltaTime;
                if (solverWaitTimer > 20f)
                {
                    Debug.LogError("[HintDebug] ОШИБКА: Солвер завис! Ждем уже больше 20 секунд.");
                    break;
                }
                yield return null;
            }
            Debug.Log("[HintDebug] HintRoutine: Фон завершил работу.");
        }

        if (cachedHintPath != null && cachedHintPath.Count > 0)
        {
            var nextMove = cachedHintPath[0];
            cachedHintPath.RemoveAt(0);

            isExecutingHint = true;
            bool moveOk = ExecuteHintMove(nextMove);

            if (!moveOk)
            {
                // Кэш оказался неактуален для текущего стола — сбрасываем и пересчитываем.
                isExecutingHint = false;
                cachedHintPath = null;
                StartBackgroundSolver();
                IsInputAllowed = true;
                onResult?.Invoke(false);
                yield break;
            }

            onResult?.Invoke(true);
        }
        else
        {
            IsInputAllowed = true;
            onResult?.Invoke(false);
        }
    }

    private bool ExecuteHintMove(HintMoveCommand cmd)
    {
        ICardContainer source = null;
        ICardContainer target = null;
        List<CardController> sequence = new List<CardController>();

        if (cmd.Type == HintMoveCommand.MoveType.ToFoundation || cmd.Type == HintMoveCommand.MoveType.TabToTab || cmd.Type == HintMoveCommand.MoveType.ToFreeCell)
        {
            if (cmd.FromIndex < 8)
            {
                var tab = pileManager.Tableau[cmd.FromIndex];
                source = tab;
                int startIndex = tab.cards.Count - cmd.SequenceLength;
                if (startIndex < 0) return false; // стол разошёлся с ожиданиями солвера
                for (int i = startIndex; i < tab.cards.Count; i++) sequence.Add(tab.cards[i]);
            }
            else
            {
                var fc = pileManager.FreeCells[cmd.FromIndex - 8];
                if (fc.IsEmpty) return false;
                source = fc;
                sequence.Add(source.Transform.GetComponentInChildren<CardController>());
            }
        }
        else if (cmd.Type == HintMoveCommand.MoveType.FreeCellToTab)
        {
            var fc = pileManager.FreeCells[cmd.FromIndex];
            if (fc.IsEmpty) return false;
            source = fc;
            sequence.Add(source.Transform.GetComponentInChildren<CardController>());
        }

        if (sequence.Count == 0 || sequence[0] == null) return false;

        if (cmd.Type == HintMoveCommand.MoveType.ToFoundation)
        {
            // ВАЖНО: cmd.ToIndex для ToFoundation — это ID МАСТИ (0-3) из внутренней модели
            // FreeCellHintSolver (FastBoard.foundations индексируется мастью карты), а НЕ позиция
            // дома в pileManager.Foundations! Порядок Foundations задаётся сортировкой по имени
            // GameObject'а в сцене (FreeCellPileManager.InitializeFreeCell) и с мастью никак не
            // связан. Индексирование Foundations[cmd.ToIndex] отправляло карту в произвольный
            // физический дом без проверки CanAccept — отсюда карты не той масти/не по порядку
            // (например 2♠ в отдельном доме, а не поверх туза пик). Ищем реальный подходящий дом
            // так же, как это делает обычный (не через подсказку) авто-ход — по CanAccept.
            foreach (var f in pileManager.Foundations)
            {
                if (f.CanAccept(sequence[0])) { target = f; break; }
            }
        }
        else if (cmd.Type == HintMoveCommand.MoveType.ToFreeCell) target = pileManager.FreeCells[cmd.ToIndex];
        else target = pileManager.Tableau[cmd.ToIndex];

        if (target == null) return false;

        // Доп. защита: даже когда цель найдена, перепроверяем легальность хода прямо сейчас.
        // Стол мог разойтись с тем, что видел солвер (отмена хода, параллельное действие и т.п.) —
        // тогда лучше отклонить подсказку (вызывающий код сам сбросит кэш и пересчитает путь),
        // чем силой протолкнуть нелегальный ход и сломать логическое состояние стола.
        if (!target.CanAccept(sequence[0])) return false;

        ExecuteProgrammaticSequenceMove(sequence, source, target);
        StartCoroutine(WaitAndUnlockAfterHint(sequence));
        return true;
    }

    // НОВЫЙ МЕТОД: Ждет физического окончания полета карт, защищая от ложных поражений
    private IEnumerator WaitAndUnlockAfterHint(List<CardController> sequence)
    {
        // Даем один кадр на запуск анимаций и смену родителей
        yield return null;

        // Ждем, пока слой перетаскивания не опустеет и карты не перестанут лететь
        while (true)
        {
            bool isAnimating = false;

            if (dragLayer != null && dragLayer.childCount > 0) isAnimating = true;

            foreach (var c in sequence)
            {
                if (c != null && c.IsAnimating) isAnimating = true;
            }

            if (!isAnimating) break;
            yield return null;
        }

        // Даем игре еще 0.1 сек на обновление логических списков после приземления
        yield return new WaitForSeconds(0.1f);

        UnlockAfterHint();
    }

    private void UnlockAfterHint()
    {
        isExecutingHint = false;
        IsInputAllowed = true;

        // ФИКС 1: Принудительно запускаем старт игры и таймер, если игрок начал игру с подсказки
        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true;
            if (StatisticsManager.Instance != null)
            {
                string variant = GameSettings.GetCurrentVariantString(GameType.FreeCell) ?? "None";
                StatisticsManager.Instance.OnGameStarted("FreeCell", currentDifficulty, variant);
            }
        }

        // Ручная регистрация хода для статистики (БЕЗ полного сброса пути в кэше)
        GameQuestTracker.Instance?.RecordMove();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
        UpdateHUD();

        // Если кэш опустел, пора посчитать новый путь в фоне
        if (cachedHintPath == null || cachedHintPath.Count == 0)
        {
            StartBackgroundSolver();
        }

        CheckGameState();
    }

}