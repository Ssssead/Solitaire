using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections;
using YG;

public enum YukonVariant { Classic, Russian }

public class YukonModeManager : MonoBehaviour, IModeManager, ICardGameMode, ICardClickReceiver
{
    [Header("Core Setup")]
    public YukonVariant CurrentVariant = YukonVariant.Classic;
    public Difficulty currentDifficulty = Difficulty.Medium;
    public ITutorialManager Tutorial => tutorialManager;
    [Header("Intro")]
    public YukonIntroController introController;
    public bool playIntroOnStart = true;
    private bool isRestarting = false;

    [Header("References")]
    public YukonDeckManager deckManager;
    public Canvas rootCanvas;
    public RectTransform dragLayer;
    public ScoreManager scoreManager;
    public UndoManager undoManager;
    public GameUIController gameUI;
    public YukonTutorialManager tutorialManager;
    private Deal currentInitialDeal;
    [Header("Containers")]
    public Transform tableauSlotsParent;
    public Transform foundationSlotsParent;
    private bool wasUndoing = false;

    [Header("UI & HUD")]
    public TMP_Text movesText;
    public TMP_Text scoreText;
    public TMP_Text timeText;

    [Tooltip("Кнопка авто-сбора открытых карт (Auto Collect)")]
    public Button autoWinButton;
    private int _snapHiddenCount = 0;
    private int _snapEmptyColumns = 0;

    [HideInInspector] public List<YukonTableauPile> tableaus = new List<YukonTableauPile>();
    [HideInInspector] public List<FoundationPile> foundations = new List<FoundationPile>();

    // --- ИСПРАВЛЕНИЕ БАГА UNDO: Теперь привязываем переворот к летящей карте ---
    private Dictionary<CardController, int> pendingAutoFlips = new Dictionary<CardController, int>();

    private struct YukonQuestUndoRecord
    {
        public int SequenceCount;
        public bool IsTabToTab;
        public bool IsToFoundation;      // Карта улетела в дом?
        public bool CompletedFoundation; // Был ли это Король (закрыл стопку)?
        public int CardRank;             // <--- ДОБАВЛЕНО: Ранг карты для отката квестов
    }
    private Stack<YukonQuestUndoRecord> questUndoStack = new Stack<YukonQuestUndoRecord>();
    // -------------------------------------------------
    public string GameName => "Yukon";
    public RectTransform DragLayer => dragLayer;
    public Canvas RootCanvas => rootCanvas;
    public bool IsInputAllowed { get; set; } = true;
    public float TableauVerticalGap => 35f;
    public AnimationService AnimationService => null;
    public PileManager PileManager => null;
    public AutoMoveService AutoMoveService => null;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public GameType GameType => GameType.Yukon;

    // --- СТАТИСТИКА И СОСТОЯНИЕ ИГРЫ ---
    private bool hasWonGame = false;
    private bool hasGameStarted = false;
    private float gameTimer = 0f;
    private bool isTimerRunning = false;

    private IEnumerator Start()
    {
        if (undoManager == null) undoManager = FindObjectOfType<UndoManager>();
        if (undoManager != null)
        {
            undoManager.Initialize(this);

            if (undoManager.undoAllButton != null)
            {
                undoManager.undoAllButton.onClick.RemoveAllListeners();
                undoManager.undoAllButton.onClick.AddListener(RestartCurrentDeal);
            }
        }

        if (autoWinButton != null)
        {
            autoWinButton.gameObject.SetActive(true);
            autoWinButton.onClick.RemoveAllListeners();
            autoWinButton.onClick.AddListener(OnAutoCollectClicked);
        }

        yield return new WaitForEndOfFrame();
        InitializeMode();
    }

    public bool IsMatchInProgress()
    {
        return hasGameStarted;
    }

    private void Update()
    {
        if (isTimerRunning && !hasWonGame)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }

        if (undoManager != null)
        {
            if (undoManager.IsUndoing && !wasUndoing)
            {
                // ---> ОБНОВИТЬ ЭТОТ БЛОК ДЛЯ СНЯТИЯ СНАПШОТА <---
                _snapHiddenCount = tableaus.Sum(t => t.faceUp.Count(f => !f));
                _snapEmptyColumns = tableaus.Count(t => t.cards.Count == 0);
                wasUndoing = true;
            }
            else if (!undoManager.IsUndoing && wasUndoing)
            {
                wasUndoing = false;
                ForceGlobalLayoutSync();

                // ---> ДОБАВИТЬ ЭТОТ БЛОК АНТИ-ЧИТА <---
                int hiddenDelta = tableaus.Sum(t => t.faceUp.Count(f => !f)) - _snapHiddenCount;
                if (hiddenDelta > 0) GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, -hiddenDelta);

                int emptyDelta = _snapEmptyColumns - tableaus.Count(t => t.cards.Count == 0);
                if (emptyDelta > 0) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, -emptyDelta);
                // --------------------------------------
            }
        }
    }

    private void OnDestroy()
    {
        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }
    }

    public void InitializeMode()
    {
        currentDifficulty = GameSettings.CurrentDifficulty;
        CurrentVariant = GameSettings.YukonRussian ? YukonVariant.Russian : YukonVariant.Classic;

        tableaus.Clear();
        if (tableauSlotsParent)
        {
            tableaus.AddRange(tableauSlotsParent.GetComponentsInChildren<YukonTableauPile>());
            tableaus.Sort((a, b) => a.name.CompareTo(b.name));
        }

        foundations.Clear();
        if (foundationSlotsParent)
        {
            foundations.AddRange(foundationSlotsParent.GetComponentsInChildren<FoundationPile>());
        }

        StartNewGame();
    }

    public void StartNewGame()
    {
        // 1. СНАЧАЛА честно закрываем старую игру
        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }

        // 2. ЗАТЕМ сообщаем трекеру настройки нового матча
        // CurrentVariant.ToString() автоматически выдаст "Classic" или "Russian"
        string variant = CurrentVariant.ToString();
        GameQuestTracker.Instance?.StartMatch("Yukon", currentDifficulty, variant);

        IsInputAllowed = false;
        hasWonGame = false;
        hasGameStarted = false;
        gameTimer = 0f;
        isTimerRunning = false;

        if (scoreManager) scoreManager.ResetScore();
        if (undoManager) undoManager.ResetHistory();

        if (foundations != null)
        {
            foreach (var f in foundations)
            {
                f.Clear();
            }
        }

        UpdateFullUI();

        if (introController != null) introController.PrepareIntro(isRestarting);

        if (DealCacheSystem.Instance)
        {
            int variantParam = CurrentVariant == YukonVariant.Russian ? 1 : 0;
            var deal = DealCacheSystem.Instance.GetDeal(GameType.Yukon, currentDifficulty, variantParam);

            if (deal != null)
            {
                currentInitialDeal = deal.DeepClone();
                StartCoroutine(IntroSequenceRoutine(deal));
            }
        }
    }

    private IEnumerator IntroSequenceRoutine(Deal deal)
    {
        yield return null;

        // ==========================================
        // ИНТЕГРАЦИЯ ТУТОРИАЛА
        // ==========================================
        if (GameSettings.IsTutorialMode && tutorialManager != null)
        {
            // Запускаем интро обучения Юкона
            yield return StartCoroutine(tutorialManager.PlayTutorialIntro(deal));

            isRestarting = false;
            IsInputAllowed = true;
            UpdateFullUI();
        }
        else
        {
            // ==========================================
            // ОБЫЧНАЯ ИГРА
            // ==========================================
            bool cardsDealtByIntro = false;

            if (introController != null && playIntroOnStart)
            {
                yield return StartCoroutine(introController.PlayIntroSequence(isRestarting, deal));
                cardsDealtByIntro = true;
            }
            else if (deckManager != null && isRestarting)
            {
                yield return StartCoroutine(deckManager.PlayIntroDeckArrival(deal, isRestarting));
                cardsDealtByIntro = true;
            }

            if (!cardsDealtByIntro && deckManager != null)
            {
                deckManager.LoadDeal(deal, animate: false);
            }

            YukonSolver.ExtendedSolverResult solverRes = new YukonSolver.ExtendedSolverResult();
            int variantParam = CurrentVariant == YukonVariant.Russian ? 1 : 0;
            yield return StartCoroutine(YukonSolver.SolveAsync(deal.DeepClone(), variantParam, 32f, solverRes, 50000));

            string rID = $"Deal_{UnityEngine.Random.Range(1000, 9999)}";

            isRestarting = false;
            IsInputAllowed = true;
            UpdateFullUI();
        }
    }

    public void RestartGame()
    {
        if (hasGameStarted && !hasWonGame && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }

        currentDifficulty = GameSettings.CurrentDifficulty;
        CurrentVariant = GameSettings.YukonRussian ? YukonVariant.Russian : YukonVariant.Classic;

        isRestarting = true;
        StopAllCoroutines();
        StartNewGame();
    }

    public void RestartCurrentDeal()
    {
        if (currentInitialDeal == null || hasWonGame) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Back");

        RegisterMoveAndStartIfNeeded();

        IsInputAllowed = false;

        if (scoreManager) scoreManager.ResetScore();
        if (undoManager) undoManager.ClearHistory();

        if (foundations != null)
        {
            foreach (var f in foundations) f.Clear();
        }

        if (deckManager != null)
        {
            deckManager.LoadDeal(currentInitialDeal.DeepClone(), animate: false);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");

        UpdateFullUI();
        IsInputAllowed = true;
    }

    public void RegisterMoveAndStartIfNeeded()
    {
        // ---> ДОБАВИТЬ СЮДА <---
        GameQuestTracker.Instance?.RecordMove();

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true;

            if (StatisticsManager.Instance != null)
            {
                string variant = GameSettings.GetCurrentVariantString(GameType.Yukon);
                StatisticsManager.Instance.OnGameStarted("Yukon", currentDifficulty, variant);
            }
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();
    }

    private void OnAutoCollectClicked()
    {
        if (!IsInputAllowed || hasWonGame) return;

        // --- ТУТОРИАЛ: Засчитываем клик по авто-сбору ---
        if (tutorialManager != null && tutorialManager.IsTutorialActive)
        {
            tutorialManager.AdvanceStep();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Click");
        }

        StartCoroutine(AutoCollectRoutine());
    }

    private IEnumerator AutoCollectRoutine()
    {
        IsInputAllowed = false;

        

        bool movedAny = true;
        int maxIterations = 52;
        int iterations = 0;

        Dictionary<FoundationPile, int> virtualFoundationRanks = new Dictionary<FoundationPile, int>();
        Dictionary<FoundationPile, Suit> virtualFoundationSuits = new Dictionary<FoundationPile, Suit>();

        foreach (var f in foundations)
        {
            if (f.cards.Count > 0)
            {
                virtualFoundationRanks[f] = f.cards[f.cards.Count - 1].cardModel.rank;
                virtualFoundationSuits[f] = f.cards[f.cards.Count - 1].cardModel.suit;
            }
            else
            {
                virtualFoundationRanks[f] = 0;
            }
        }

        while (movedAny && iterations < maxIterations)
        {
            movedAny = false;
            iterations++;

            foreach (var tab in tableaus)
            {
                if (tab.cards.Count == 0) continue;

                int topIndex = tab.cards.Count - 1;
                CardController topCard = tab.cards[topIndex];

                var topData = topCard.GetComponent<CardData>();
                if (topData != null)
                {
                    if (!topData.IsFaceUp()) continue;

                    if (topData.IsFlipping())
                    {
                        yield return new WaitUntil(() => topData == null || !topData.IsFlipping());
                        if (topData == null) break;
                    }
                }

                CardModel model = topCard.cardModel;

                foreach (var f in foundations)
                {
                    bool canVirtualAccept = false;
                    int vRank = virtualFoundationRanks[f];

                    if (vRank == 0)
                    {
                        if (f.CanAccept(topCard)) canVirtualAccept = true;
                    }
                    else
                    {
                        if (model.suit == virtualFoundationSuits[f] && model.rank == vRank + 1)
                        {
                            canVirtualAccept = true;
                        }
                    }

                    if (canVirtualAccept)
                    {
                        virtualFoundationRanks[f] = model.rank;
                        virtualFoundationSuits[f] = model.suit;

                        // МЫ УДАЛИЛИ ОТСЮДА ОТПРАВКУ ИВЕНТОВ В КВЕСТ-МЕНЕДЖЕР!
                        // Теперь это делает сам FoundationPile.AcceptCard, чтобы не было х2 (удвоения).

                        if (topCard is YukonCardController yCard)
                        {
                            tab.RemoveSequenceFrom(topIndex);
                            yCard.SetSourceForAutoMove(tab);

                            if (AudioManager.Instance != null)
                                AudioManager.Instance.PlaySound("Card_PickUp");

                            yCard.transform.SetParent(dragLayer, true);
                            yCard.transform.SetAsLastSibling();

                            yCard.AnimateToTarget(f, f.transform.position);

                            if (tab.cards.Count == 0) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, 1);

                            if (tab.cards.Count > 0)
                            {
                                int newTopIndex = tab.cards.Count - 1;
                                if (tab.faceUp.Count > newTopIndex && !tab.faceUp[newTopIndex])
                                {
                                    var nextData = tab.cards[newTopIndex].GetComponent<CardData>();
                                    if (nextData != null && !nextData.IsFaceUp() && !nextData.IsFlipping())
                                    {
                                        tab.CheckAndFlipTop();
                                        YukonScoreManager yScore = scoreManager as YukonScoreManager;
                                        yScore?.AddReward(yScore.revealReward);

                                        pendingAutoFlips[topCard] = newTopIndex;

                                        GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, 1);
                                    }
                                }
                            }

                            movedAny = true;
                            yield return new WaitForSeconds(0.08f);
                            break;
                        }
                    }
                }
                if (movedAny) break;
            }
        }

        yield return new WaitForSeconds(0.3f);
        IsInputAllowed = true;
        CheckGameState();
    }

    private void UpdateFullUI()
    {
        if (movesText != null)
        {
            if (!hasGameStarted)
            {
                movesText.text = "0";
            }
            else if (!hasWonGame && StatisticsManager.Instance != null)
            {
                movesText.text = StatisticsManager.Instance.GetCurrentMoves().ToString();
            }
        }

        if (scoreText != null)
            scoreText.text = (!hasGameStarted) ? "0" : $"{(scoreManager != null ? scoreManager.CurrentScore : 0)}";

        if (!hasGameStarted) UpdateTimeUI();
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

    private void ForceGlobalLayoutSync()
    {
        foreach (var t in tableaus)
        {
            t.ForceUpdateFromTransform();

            for (int i = 0; i < t.cards.Count; i++)
            {
                var card = t.cards[i];
                if (card != null)
                {
                    card.StopAllCoroutines();
                    card.transform.localScale = Vector3.one;

                    var data = card.GetComponent<CardData>();
                    if (data != null)
                    {
                        data.SetFaceUp(data.IsFaceUp(), false);
                    }
                }
            }
            t.ForceRecalculateLayout();
        }

        foreach (var f in foundations)
        {
            f.cards.Clear();
            foreach (Transform child in f.transform)
            {
                var card = child.GetComponent<CardController>();
                if (card != null)
                {
                    card.StopAllCoroutines();
                    card.transform.localScale = Vector3.one;

                    var data = card.GetComponent<CardData>();
                    if (data != null) data.SetFaceUp(data.IsFaceUp(), false);

                    f.cards.Add(card);
                }
            }
        }
    }

    public ICardContainer FindNearestContainer(CardController card, Vector2 unusedPos = default, float unusedDist = 0)
    {
        return FindNearestContainer(card, card.transform.parent);
    }

    public ICardContainer FindNearestContainer(CardController card, Transform ignoreSource)
    {
        ICardContainer bestContainer = null;
        float maxOverlapArea = 0f;
        Rect cardRect = GetWorldRect(card.rectTransform);

        List<MonoBehaviour> candidates = new List<MonoBehaviour>();
        candidates.AddRange(tableaus);
        candidates.AddRange(foundations);

        foreach (var candidate in candidates)
        {
            if (card.transform.IsChildOf(candidate.transform)) continue;
            if (candidate.transform == ignoreSource) continue;

            RectTransform targetRT = candidate.transform as RectTransform;
            if (candidate is YukonTableauPile tab && tab.cards.Count > 0)
            {
                var lastCard = tab.cards[tab.cards.Count - 1];
                if (lastCard != null) targetRT = lastCard.rectTransform;
            }

            float area = GetIntersectionArea(cardRect, GetWorldRect(targetRT));
            if (area > 0 && area > maxOverlapArea)
            {
                ICardContainer container = candidate as ICardContainer;
                if (container != null && container.CanAccept(card))
                {
                    maxOverlapArea = area;
                    bestContainer = container;
                }
            }
        }
        return bestContainer;
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        if (undoManager != null && undoManager.IsUndoing) return;

        var yCard = card as YukonCardController;
        ICardContainer source = yCard?.SourceContainer;

        int sequenceCount = 0;
        bool isTabToTab = false;
        bool isToFoundation = false;
        bool completedFoundation = false;

        YukonScoreManager yScore = scoreManager as YukonScoreManager;
        yScore?.BeginMove();

        // 1. ПЕРЕНОС НА ИГРОВОМ СТОЛЕ
        if (container is YukonTableauPile)
        {
            if (source is YukonTableauPile)
            {
                isTabToTab = true;
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveTableauToTableau, 1);
            }
            List<CardController> movedCardsLocal = yCard.GetMovedCards();
            if (movedCardsLocal != null && movedCardsLocal.Count > 1)
            {
                sequenceCount = movedCardsLocal.Count;
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardSequence, sequenceCount);
            }
        }
        // 2. ПЕРЕНОС В ДОМ
        else if (container is FoundationPile fContainer)
        {
            isToFoundation = true;
            if (card.cardModel.rank == 13) completedFoundation = true;

            // МЫ УДАЛИЛИ отсюда +1, так как FoundationPile сам прибавляет +1 при принятии карты
        }

        // 3. ОТКАТ ВРУЧНУЮ ИЗ ДОМА (Игрок сам забрал карту)
        if (source is FoundationPile fPile && source != container)
        {
            // ---> ИСПРАВЛЕНИЕ: Вызываем универсальный метод, который сам отнимет и Дом, и Ранги! <---
            GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(card.cardModel.rank);

            if (fPile.cards.Contains(card))
            {
                fPile.cards.Remove(card);

                if (fPile.cards.Count > 0)
                {
                    var newTopCard = fPile.cards[fPile.cards.Count - 1];
                    if (newTopCard != null && newTopCard.canvasGroup != null)
                    {
                        newTopCard.canvasGroup.blocksRaycasts = true;
                        newTopCard.canvasGroup.interactable = true;
                    }
                }
            }
        }

        // 4. ЗАПИСЬ ХОДА ДЛЯ UNDO
        if (undoManager != null && source != null)
        {
            questUndoStack.Push(new YukonQuestUndoRecord
            {
                SequenceCount = sequenceCount,
                IsTabToTab = isTabToTab,
                IsToFoundation = isToFoundation,
                CompletedFoundation = completedFoundation,
                CardRank = card.cardModel.rank // <--- ЗАПОМИНАЕМ РАНГ ДЛЯ ОТКАТА
            });

            List<CardController> movedCards = yCard.GetMovedCards();
            List<Transform> parents = new List<Transform>();
            List<Vector3> positions = yCard.GetSavedLocalPositions();
            List<int> siblings = new List<int>();
            int startIndex = yCard.OriginalSiblingIndex;

            for (int i = 0; i < movedCards.Count; i++)
            {
                parents.Add(source.Transform);
                siblings.Add(startIndex + i);
                if (positions == null || positions.Count <= i) positions.Add(Vector3.zero);
            }

            undoManager.RecordMove(movedCards, source, container, parents, positions, siblings);
        }

        int emptyBefore = tableaus.Count(t => t.cards.Count == 0);

        foreach (var t in tableaus)
        {
            t.ForceUpdateFromTransform();

            if (t.cards.Count > 0)
            {
                int topIndex = t.cards.Count - 1;

                if (t.faceUp.Count > topIndex && !t.faceUp[topIndex])
                {
                    var data = t.cards[topIndex].GetComponent<CardData>();
                    if (data != null && (data.IsFlipping() || data.IsFaceUp())) continue;

                    t.CheckAndFlipTop();
                    if (undoManager != null) undoManager.RecordFlipInSource(topIndex);
                    yScore?.AddReward(yScore.revealReward);

                    GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, 1);
                }
            }
        }

        int emptyAfter = tableaus.Count(t => t.cards.Count == 0);
        int clearedColumns = emptyAfter - emptyBefore;
        if (clearedColumns > 0) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, clearedColumns);

        if (pendingAutoFlips.ContainsKey(card))
        {
            if (undoManager != null) undoManager.RecordFlipInSource(pendingAutoFlips[card]);
            pendingAutoFlips.Remove(card);
        }

        RegisterMoveAndStartIfNeeded();

        if (container is FoundationPile)
            yScore?.AddReward(yScore.foundationReward);
        else if (source is FoundationPile && container is YukonTableauPile)
            yScore?.AddReward(yScore.foundationPenalty);

        yScore?.CommitMove();

        CheckGameState();
        UpdateFullUI();
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        if (!YG.YG2.envir.isDesktop) return;

        ExecuteAutoMove(card);
    }
    private void ExecuteAutoMove(CardController card)
    {
        if (tutorialManager != null && tutorialManager.IsTutorialActive) return;

        // ВАЖНО: Пуленепробиваемый поиск источника! Защита от микро-свайпов
        ICardContainer sourceContainer = GetCardSourceReliably(card);

        if (sourceContainer == null || sourceContainer is FoundationPile) return;

        var cardData = card.GetComponent<CardData>();
        if (cardData != null && !cardData.IsFaceUp()) return; // Закрытые кликать нельзя

        List<CardController> sequence = new List<CardController> { card };

        if (sourceContainer is YukonTableauPile tab)
        {
            int cardIndex = tab.IndexOfCard(card);
            if (cardIndex != -1)
            {
                sequence = tab.cards.GetRange(cardIndex, tab.cards.Count - cardIndex);
            }
        }

        // Жестко глушим стандартное возвращение карты, запущенное микро-свайпом
        foreach (var c in sequence) c.StopAllCoroutines();

        bool moved = TryAutoMove(sequence, sourceContainer);

        if (!moved)
        {
            // Если ход невозможен — принудительно вбиваем карты обратно в столбец
            foreach (var c in sequence)
            {
                c.rectTransform.SetParent(sourceContainer.Transform, true);
            }
            if (sourceContainer is YukonTableauPile tabPile)
            {
                tabPile.ForceRecalculateLayout();
            }

            // Запускаем тряску из правильной стартовой позиции
            StartCoroutine(ShakeSequenceRoutine(sequence));
        }
    }

    // --- НОВЫЙ МЕТОД ---
    // Ищет реального владельца карты в системной памяти, игнорируя визуальные баги
    private ICardContainer GetCardSourceReliably(CardController card)
    {
        foreach (var tab in tableaus)
        {
            if (tab.cards.Contains(card)) return tab;
        }
        foreach (var f in foundations)
        {
            if (f.cards.Contains(card)) return f;
        }

        // Резервный вариант на всякий случай
        return card.GetComponentInParent<ICardContainer>();
    }
    // 2. Поиск доступного места
    private bool TryAutoMove(List<CardController> sequence, ICardContainer source)
    {
        CardController leadCard = sequence[0];
        bool isTopCard = (sequence.Count == 1);

        // А. Попытка перенести в "Дом" (Foundation). ТОЛЬКО верхние одиночные карты!
        if (isTopCard)
        {
            foreach (var f in foundations)
            {
                if (f.CanAccept(leadCard))
                {
                    ExecuteProgrammaticSequenceMove(sequence, source, f);
                    return true;
                }
            }
        }

        // Б. Попытка перенести на другой столбец (Tableau). Работает для любых рядов!
        foreach (var t in tableaus)
        {
            if (t == source) continue;

            if (t.CanAccept(leadCard))
            {
                ExecuteProgrammaticSequenceMove(sequence, source, t);
                return true;
            }
        }

        return false;
    }

    // 3. Выполнение физического и логического переноса ряда (сохраняем квесты, очки и Undo)
    private void ExecuteProgrammaticSequenceMove(List<CardController> sequence, ICardContainer source, ICardContainer target)
    {
        if (sequence == null || sequence.Count == 0) return;

        CardController leadCard = sequence[0];
        YukonCardController yLeadCard = leadCard as YukonCardController;

        int sequenceCount = sequence.Count;
        bool isTabToTab = (source is YukonTableauPile && target is YukonTableauPile);
        bool isToFoundation = (target is FoundationPile);
        bool completedFoundation = (leadCard.cardModel.rank == 13 && isToFoundation);

        // 1. Сохраняем стейт для Undo
        List<Transform> prevParents = new List<Transform>();
        List<Vector3> prevPositions = new List<Vector3>();
        List<int> prevSibs = new List<int>();

        foreach (var c in sequence)
        {
            prevParents.Add(c.transform.parent);
            prevPositions.Add(c.transform.localPosition);
            prevSibs.Add(c.transform.GetSiblingIndex());
        }

        // 2. Логически изымаем из источника
        if (source is YukonTableauPile tab)
        {
            int idx = tab.IndexOfCard(leadCard);
            if (idx != -1) tab.RemoveSequenceFrom(idx);
        }

        // 3. Запуск полета и бронирование
        if (target is FoundationPile found)
        {
            found.ReserveCard(leadCard);
            if (yLeadCard != null)
            {
                yLeadCard.SetSourceForAutoMove(source);
                yLeadCard.transform.SetParent(dragLayer, true);
                yLeadCard.transform.SetAsLastSibling();
                yLeadCard.AnimateToTarget(found, found.transform.position); // Родная анимация в Дом
            }
        }
        else if (target is YukonTableauPile targetTab)
        {
            StartCoroutine(AnimateSequenceAutoMove(sequence, targetTab));
        }

        // 4. Очки, Трекер квестов и Запись Undo (полностью зеркалит ручной дроп)
        YukonScoreManager yScore = scoreManager as YukonScoreManager;
        yScore?.BeginMove();

        if (isTabToTab) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveTableauToTableau, 1);
        if (sequenceCount > 1) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardSequence, sequenceCount);

        if (undoManager != null)
        {
            questUndoStack.Push(new YukonQuestUndoRecord
            {
                SequenceCount = sequenceCount,
                IsTabToTab = isTabToTab,
                IsToFoundation = isToFoundation,
                CompletedFoundation = completedFoundation,
                CardRank = leadCard.cardModel.rank
            });

            undoManager.RecordMove(sequence, source, target, prevParents, prevPositions, prevSibs);
        }

        // 5. Авто-открытие верхней карты, если мы оголили скрытую
        if (source is YukonTableauPile sourceTab)
        {
            int emptyBefore = tableaus.Count(t => t.cards.Count == 0);

            if (sourceTab.cards.Count > 0)
            {
                int topIndex = sourceTab.cards.Count - 1;
                if (sourceTab.faceUp.Count > topIndex && !sourceTab.faceUp[topIndex])
                {
                    sourceTab.CheckAndFlipTop();
                    if (undoManager != null) undoManager.RecordFlipInSource(topIndex);

                    yScore?.AddReward(yScore.revealReward);
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, 1);
                }
            }

            int emptyAfter = tableaus.Count(t => t.cards.Count == 0);
            if (emptyAfter - emptyBefore > 0) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, emptyAfter - emptyBefore);
        }

        RegisterMoveAndStartIfNeeded();

        if (target is FoundationPile) yScore?.AddReward(yScore.foundationReward);

        yScore?.CommitMove();

        Invoke(nameof(CheckGameState), 0.3f);
        UpdateFullUI();
    }

    // 4. Корутина синхронного полета ряда карт
    private IEnumerator AnimateSequenceAutoMove(List<CardController> sequence, YukonTableauPile targetTab)
    {
        List<Vector3> startPositions = new List<Vector3>();
        foreach (var c in sequence)
        {
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

        // Считаем точки приземления
        Vector2 topAnchor = targetTab.GetDropAnchoredPosition(sequence[0]);
        List<Vector3> targetPositions = new List<Vector3>();

        for (int i = 0; i < sequence.Count; i++)
        {
            Vector2 anc = new Vector2(topAnchor.x, topAnchor.y - i * TableauVerticalGap);
            GameObject t = new GameObject("tmp");
            t.transform.SetParent(targetTab.transform, false);
            t.AddComponent<RectTransform>().anchoredPosition = anc;
            Canvas.ForceUpdateCanvases();
            targetPositions.Add(t.transform.position);
            Destroy(t);
        }

        float duration = 0.22f;
        float elapsed = 0f;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_PickUp");

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

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");

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

        targetTab.AddCardsBatch(sequence, true);
        targetTab.ForceRecalculateLayout();
    }

    // 5. Анимация тряски ряда при отказе
    private IEnumerator ShakeSequenceRoutine(List<CardController> sequence)
    {
        if (sequence == null || sequence.Count == 0) yield break;

        AudioSource shakeSource = null;
        float baseShakeVolume = 1f;

        if (AudioManager.Instance != null)
        {
            
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
    public void OnUndoAction()
    {
        if (!hasGameStarted) return;

        RegisterMoveAndStartIfNeeded();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Back");
            AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            StartCoroutine(DelayedUndoDropSound(0.25f));
        }

        if (scoreManager != null) scoreManager.OnUndo();

        GameQuestTracker.Instance?.RecordUndoUsed();

        // ---> ОБНОВЛЕНО: Откат всех квестов <---
        if (questUndoStack.Count > 0)
        {
            var qRecord = questUndoStack.Pop();

            if (qRecord.IsTabToTab)
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveTableauToTableau, -1);

            if (qRecord.SequenceCount > 1)
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardSequence, -qRecord.SequenceCount);

            if (qRecord.IsToFoundation)
            {
                // ---> ИСПРАВЛЕНИЕ: Универсальный метод отката из дома <---
                // Он сам корректно отнимет -1 у заданий "Сбор мастей" и заданий на Тузов/Королей!
                GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(qRecord.CardRank);
            }
        }
        // ----------------------------------------

        UpdateFullUI();
    }

    private IEnumerator DelayedUndoDropSound(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");
    }

    public void CheckGameState()
    {
        if (hasWonGame) return;

        int win = foundations.Count(f => f.Count == 13);
        if (win == 4)
        {
            hasWonGame = true;
            IsInputAllowed = false;
            isTimerRunning = false;

            if (undoManager != null) undoManager.ClearAndLock();

            if (StatisticsManager.Instance != null)
            {
                int finalMoves = StatisticsManager.Instance.GetCurrentMoves();

                if (movesText != null) movesText.text = finalMoves.ToString();

                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;

                StatisticsManager.Instance.OnGameWon(finalScore);
                if (gameUI != null) gameUI.OnGameWon(finalMoves);
            }
        }
    }

    private Rect GetWorldRect(RectTransform rt) { Vector3[] c = new Vector3[4]; rt.GetWorldCorners(c); return new Rect(c[0].x, c[0].y, Mathf.Abs(c[2].x - c[0].x), Mathf.Abs(c[2].y - c[0].y)); }
    private float GetIntersectionArea(Rect r1, Rect r2) { float w = Mathf.Min(r1.xMax, r2.xMax) - Mathf.Max(r1.xMin, r2.xMin); float h = Mathf.Min(r1.yMax, r2.yMax) - Mathf.Max(r1.yMin, r2.yMin); return (w > 0 && h > 0) ? w * h : 0f; }

    public bool OnDropToBoard(CardController c, Vector2 p) => false;
    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        // На мобилках и планшетах запускаем авто-перенос
        if (YG.YG2.envir.isMobile || YG.YG2.envir.isTablet)
        {
            ExecuteAutoMove(card);
        }
    }
    public void OnCardLongPressed(CardController c) { }
    public void OnKeyboardPick(CardController c) { }
    public void OnStockClicked() { }
}