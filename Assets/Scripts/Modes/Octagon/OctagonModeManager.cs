using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using YG;

public class OctagonModeManager : MonoBehaviour, ICardGameMode, IModeManager, ICardClickReceiver
{
    [Header("Managers")]
    [SerializeField] private OctagonDeckManager deckManager;
    [SerializeField] private OctagonPileManager pileManager;
    [SerializeField] private GameUIController gameUI;
    [SerializeField] private OctagonAnimationService animationService;
    public OctagonTutorialManager tutorialManager;

    [Header("UI Controls")]
    [SerializeField] private Button undoButton;
    [SerializeField] private Button undoAllButton;

    [Header("UI & HUD")]
    public TMP_Text movesText;
    public TMP_Text timeText;
    public TMP_Text scoreText;

    [Header("Setup")]
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private Canvas rootCanvas;

    [Header("Game Rules")]
    [SerializeField] private int maxRecycles = 2;
    private int recyclesUsed = 0;
    private bool _isGameFinished = false;
    private bool _isGameWon = false; // НОВОЕ: Отдельно отслеживаем реальную победу
    public Difficulty CurrentDifficulty => GameSettings.CurrentDifficulty;
    public GameType GameType => GameType.Octagon;

    private Stack<OctagonMoveRecord> undoStack = new Stack<OctagonMoveRecord>();
    private bool isUndoing = false;
    public ITutorialManager Tutorial => tutorialManager;
    public OctagonIntroController introController;
    public bool playIntroOnStart = true;
    private bool isRestarting = false;

    private float gameTimer = 0f;
    private bool isTimerRunning = false;
    private bool hasGameStarted = false;

    // --- СИСТЕМА ОЧКОВ ---
    private int currentScore = 0;
    private int foundationCombo = 0;
    private Stack<int> scoreHistory = new Stack<int>();
    private Coroutine defeatRoutine;

    // Свойства
    public int CurrentScore => currentScore; // Публичный доступ на случай, если понадобится другим скриптам
    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => null;
    public OctagonAnimationService OctagonAnim => animationService;
    public PileManager PileManager => pileManager;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 25f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public bool IsInputAllowed { get; set; } = false;
    public string GameName => "Octagon";
    // ---> СТРУКТУРА ДЛЯ ОТМЕНЫ ХОДОВ В ОКТАГОНЕ <---
    private struct OctagonQuestUndoRecord
    {
        public bool IsToFoundation;
        public bool FromWasteToFoundation;
        public bool FromTableauToFoundation;
        public int CardRank;
    }
    private Stack<OctagonQuestUndoRecord> questUndoStack = new Stack<OctagonQuestUndoRecord>();
    private void Start()
    {
        if (deckManager == null) deckManager = GetComponent<OctagonDeckManager>();
        if (pileManager == null) pileManager = GetComponent<OctagonPileManager>();
        if (introController == null) introController = GetComponent<OctagonIntroController>();

        if (animationService == null)
        {
            animationService = GetComponent<OctagonAnimationService>();
            if (animationService == null) animationService = gameObject.AddComponent<OctagonAnimationService>();
        }

        if (undoButton) undoButton.onClick.AddListener(OnUndoAction);
        if (undoAllButton) LongPressHoldTrigger.SubscribeToButton(undoAllButton, OnUndoAllAction);

        // --- НОВОЕ: Защита от багов UI ---
        FixGameUIReferences();
        StartCoroutine(InterceptDefeatUndoButton());

        StartNewGame();
    }
    private void FixGameUIReferences()
    {
        if (gameUI == null) return;
        // Насильно инжектим текущий режим в GameUIController, чтобы предотвратить NRE
        var field = gameUI.GetType().GetField("activeGameMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(gameUI, this);
    }

    private IEnumerator InterceptDefeatUndoButton()
    {
        yield return new WaitForSeconds(0.5f); // Даем UI время на инициализацию
        if (gameUI != null && gameUI.defeatPanel != null)
        {
            Button[] buttons = gameUI.defeatPanel.GetComponentsInChildren<Button>(true);
            foreach (Button b in buttons)
            {
                bool isUndo = false;

                // Способ 1: Проверяем, привязан ли багованный метод в Инспекторе Unity
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    if (b.onClick.GetPersistentMethodName(i) == "OnUndoOneClicked")
                    {
                        isUndo = true;
                        break;
                    }
                }

                // Способ 2: Ищем по названию кнопки
                if (!isUndo && b.gameObject.name.IndexOf("undo", StringComparison.OrdinalIgnoreCase) >= 0) isUndo = true;

                if (isUndo)
                {
                    // Удаляем стандартный багованный вызов GameUIController
                    b.onClick.RemoveAllListeners();
                    // Добавляем наш безопасный вызов Восьмиугольника
                    b.onClick.AddListener(() =>
                    {
                        gameUI.defeatPanel.SetActive(false);
                        OnUndoAction();
                    });
                }
            }
        }
    }

    public void StartNewGame()
    {
        if (hasGameStarted && !_isGameWon && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }

        // 2. ЗАТЕМ сообщаем трекеру настройки (У Восьмигранника всегда Standard)
        GameQuestTracker.Instance?.StartMatch("Octagon", GameSettings.CurrentDifficulty, "Standard");

        if (defeatRoutine != null)
        {
            StopCoroutine(defeatRoutine);
            defeatRoutine = null;
        }

        hasGameStarted = false;
        _isGameFinished = false;
        _isGameWon = false;
        IsInputAllowed = false;
        recyclesUsed = 0;
        undoStack.Clear();

        currentScore = 0;
        foundationCombo = 0;
        scoreHistory.Clear();

        gameTimer = 0f;
        isTimerRunning = false;
        UpdateFullUI();

        if (introController != null) introController.PrepareIntro(isRestarting);
        if (deckManager != null) deckManager.ClearBoard();

        Deal dealToPlay = null;

        // ВАЖНО: Как в Косынке! Мы не делаем return, а записываем расклад в переменную.
        if (GameSettings.IsTutorialMode && tutorialManager != null)
        {
            dealToPlay = tutorialManager.GenerateTutorialDeal();
        }
        else if (DealCacheSystem.Instance != null)
        {
            dealToPlay = DealCacheSystem.Instance.GetDeal(GameType.Octagon, CurrentDifficulty, 0);
        }

        // Запускаем интро. Оно проявит слоты и затем само вызовет deckManager.ApplyDeal(dealToPlay)
        StartCoroutine(IntroSequenceRoutine(dealToPlay));
    }

    private IEnumerator IntroSequenceRoutine(Deal deal)
    {
        yield return null;

        if (playIntroOnStart && introController != null)
        {
            yield return StartCoroutine(introController.PlayIntroSequence(isRestarting));
        }

        if (deal != null && deckManager != null)
        {
            if (introController != null) deckManager.isSkippingIntro = introController.isSkipping;
            deckManager.ApplyDeal(deal);
        }
        else
        {
            IsInputAllowed = true;
        }

        isRestarting = false;
    }

    public bool IsMatchInProgress()
    {
        return hasGameStarted;
    }

    private void Update()
    {
        if (undoButton) undoButton.interactable = undoStack.Count > 0 && !isUndoing;
        if (undoAllButton) undoAllButton.interactable = undoStack.Count > 0 && !isUndoing;

        if (isTimerRunning && !_isGameFinished)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }
    }

    private void UpdateTimeUI()
    {
        if (timeText != null)
        {
            int totalSeconds = Mathf.FloorToInt(gameTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    private void UpdateFullUI()
    {
        if (movesText != null)
        {
            if (!hasGameStarted || StatisticsManager.Instance == null) movesText.text = "0";
            else movesText.text = StatisticsManager.Instance.GetCurrentMoves().ToString();
        }
        if (!GameSettings.IsTutorialMode || tutorialManager == null || !tutorialManager.IsTutorialActive)
        {
            if (undoButton != null)
                undoButton.interactable = (undoStack != null && undoStack.Count > 0);

            if (undoAllButton != null)
                undoAllButton.interactable = (undoStack != null && undoStack.Count > 0);
        }
        if (scoreText != null)
        {
            scoreText.text = currentScore.ToString();
        }

        UpdateTimeUI();
    }

    public void RegisterMoveAndStartIfNeeded()
    {
        if (!IsInputAllowed) return;

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordMove();
        // ----------------------

        if (!hasGameStarted)
        {
            hasGameStarted = true;
            isTimerRunning = true;

            if (StatisticsManager.Instance != null)
            {
                // Записываем актуальную сложность в статистику
                StatisticsManager.Instance.OnGameStarted(GameName, CurrentDifficulty, "Classic");
            }
        }

        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.RegisterMove();
        }

        UpdateFullUI();
    }

    private void OnCardPlacedToFoundation()
    {
        foundationCombo++;
        currentScore += 5 * foundationCombo;
        UpdateFullUI();
    }

    private void ResetFoundationCombo()
    {
        foundationCombo = 0;
    }

    // --- ACTIONS ---

    public void OnStockClicked()
    {
        if (!IsInputAllowed || _isGameFinished || isUndoing) return;

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordStockDraw();
        // ----------------------

        if (pileManager.StockPile.CardCount == 0)
        {
            if (recyclesUsed >= maxRecycles)
            {
                CheckGameState(); // Передаем ответственность за проверку на тупик общему методу
                return;
            }
        }
        if (pileManager.StockPile.CardCount > 0)
        {
            int savedScore = currentScore;
            ResetFoundationCombo();

            RegisterMoveAndStartIfNeeded();
            StartCoroutine(AnimateStockToWaste(savedScore));
        }
        else if (pileManager.WastePile.CardCount > 0)
        {
            if (recyclesUsed < maxRecycles)
            {
                int savedScore = currentScore;
                ResetFoundationCombo();

                RegisterMoveAndStartIfNeeded();
                recyclesUsed++;
                StartCoroutine(AnimateRecycle(savedScore));
            }
        }
    }

    public void OnCardCreate(CardController card) { }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed || _isGameFinished || isUndoing) return;

        // Читаем глобальную настройку: 1 = Двойной клик
        if (GameSettings.AutoMoveClickMode == 1)
        {
            ProcessAutoMove(card);
        }
    }

    // Вспомогательный метод, объединяющий логику авто-переноса для обоих типов клика
    private void ProcessAutoMove(CardController card)
    {
        var data = card.GetComponent<CardData>();
        if (data != null && !data.IsFaceUp()) return;

        if (!IsCardMovable(card))
        {
            return;
        }

        foreach (var f in pileManager.FoundationPiles)
        {
            if (f.CanAccept(card))
            {
                int savedScore = currentScore;
                StartCoroutine(AnimateAutoMove(card, f, savedScore));
                return;
            }
        }
        foreach (var group in pileManager.TableauGroups)
        {
            foreach (var slot in group.Slots)
            {
                if (card.transform.parent == slot.transform) break;
                if (slot.CanAccept(card))
                {
                    var top = slot.GetTopCard();
                    if (top != null && top.cardModel.suit == card.cardModel.suit)
                    {
                        int savedScore = currentScore;
                        StartCoroutine(AnimateAutoMove(card, slot, savedScore));
                        return;
                    }
                }
                if (slot.transform.childCount > 0) break;
            }
        }

        // Трясем карту, если она свободная, но места для нее нет
        StartCoroutine(animationService.AnimateShake(card));
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        int savedScore = currentScore;

        var octCard = card as OctagonCardController;
        ICardContainer source = octCard != null ? octCard.SourceContainer : card.GetComponentInParent<ICardContainer>();

        if (source == container)
        {
            StartCoroutine(PlayDelayedSound("Card_Drop_Fail", 0.2f));
            return;
        }

        RegisterMoveAndStartIfNeeded();

        OctagonMoveRecord record = new OctagonMoveRecord();
        bool wasFaceUp = true;
        record.AddMove(card, source, container, wasFaceUp);

        if (container is OctagonFoundationPile)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Foundation_Success");
            OnCardPlacedToFoundation();
        }
        else
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");
            ResetFoundationCombo();
        }

        CheckRevealCardUnder(source, record, card);

        // ---> УЛЬТРА-НАДЕЖНЫЙ ТРЕКИНГ КВЕСТОВ ДЛЯ РУЧНОГО ХОДА <---
        if (!GameSettings.IsTutorialMode)
        {
            bool isFromCorner = source is OctagonTableauSlot || source is OctagonTableauGroup;
            bool isFromWaste = source is OctagonWastePile;

            if (isFromCorner && (container is OctagonTableauSlot || container is OctagonTableauGroup))
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveTableauToTableau, 1);

            if (container is OctagonFoundationPile)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 1);
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());

                if (isFromWaste) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, 1);
                else if (isFromCorner) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, 1);
            }

            if (record.RevealedCard != null) GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, 1);

            if (source is OctagonTableauSlot slot && slot.Group != null)
            {
                slot.Group.UpdateTopCardState();
                if (slot.Group.IsEmpty()) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, 1);
            }
        }
        // --------------------------------------------------------

        StartCoroutine(CheckRefillAndFinalizeMove(record, savedScore));
    }

    // --- ВСПОМОГАТЕЛЬНЫЙ МЕТОД ДЛЯ СИНХРОНИЗАЦИИ ЗВУКА С АНИМАЦИЕЙ ---
    private IEnumerator PlayDelayedSound(string soundName, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound(soundName);
        }
    }

    private void CheckRevealCardUnder(ICardContainer source, OctagonMoveRecord record, CardController movingCard)
    {
        if (source is OctagonTableauSlot sourceSlot && sourceSlot.Group != null)
        {
            var group = sourceSlot.Group;
            CardController candidateToReveal = null;

            for (int i = 0; i < group.Slots.Count; i++)
            {
                var slot = group.Slots[i];
                var cardInSlot = slot.GetTopCard();

                if (cardInSlot != null)
                {
                    if (cardInSlot == movingCard) continue;
                    candidateToReveal = cardInSlot;
                    break;
                }
            }

            if (candidateToReveal != null)
            {
                var data = candidateToReveal.GetComponent<CardData>();
                if (data != null && !data.IsFaceUp())
                {
                    record.RevealedCard = candidateToReveal;
                }
            }
        }
    }

    private IEnumerator CheckRefillAndFinalizeMove(OctagonMoveRecord currentRecord, int savedScore)
    {
        IsInputAllowed = false;
        yield return new WaitForSeconds(0.1f);

        foreach (var group in pileManager.TableauGroups)
        {
            if (group.IsEmpty())
            {
                if (pileManager.StockPile.CardCount + pileManager.WastePile.CardCount > 0)
                {
                    yield return StartCoroutine(RefillGroupRoutine(group, currentRecord));
                    break;
                }
            }
        }

        scoreHistory.Push(savedScore);
        undoStack.Push(currentRecord);
        IsInputAllowed = true;

        CheckGameState(); // Автоматически проверит победу или тупик
    }

    private IEnumerator RefillGroupRoutine(OctagonTableauGroup group, OctagonMoveRecord record)
    {
        List<(CardController card, ICardContainer source)> moveList = new List<(CardController, ICardContainer)>();
        int needed = 5;

        // Сбор карт из Stock
        while (moveList.Count < needed && pileManager.StockPile.CardCount > 0)
        {
            var c = pileManager.StockPile.PopTopCard();
            if (c != null) moveList.Add((c, pileManager.StockPile));
            else break;
        }

        // Добор из Waste, если в Stock не хватило
        while (moveList.Count < needed && pileManager.WastePile.CardCount > 0)
        {
            var c = pileManager.WastePile.PopBottomCard();
            if (c != null) moveList.Add((c, pileManager.WastePile));
            else break;
        }

        if (moveList.Count == 0) yield break;

        int maxSlots = group.Slots.Count;
        float duration = 0.3f;

        for (int i = 0; i < moveList.Count; i++)
        {
            var item = moveList[i];
            var card = item.card;
            var source = item.source;

            int targetSlotIndex = (maxSlots - 1) - i;
            if (targetSlotIndex >= 0 && targetSlotIndex < group.Slots.Count)
            {
                var targetSlot = group.Slots[targetSlotIndex];
                bool isLastOne = (i == moveList.Count - 1);
                bool wasFaceUpInSource = (source is OctagonWastePile);

                record.AddMove(card, source, targetSlot, wasFaceUpInSource);

                // --- ЗВУК 1: Вылет карты при заполнении слота ---
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySound("Card_Flip");
                }

                StartCoroutine(animationService.AnimateMoveCard(
                    card, targetSlot.Transform, Vector3.zero, duration, isLastOne,
                    () =>
                    {
                        targetSlot.AcceptCard(card);

                        // --- ЗВУК 2: Приземление карты в слот ---
                        if (AudioManager.Instance != null)
                        {
                            AudioManager.Instance.PlaySound("Card_Drop_Success");
                        }

                        var cg = card.GetComponent<CanvasGroup>();
                        if (cg) cg.blocksRaycasts = isLastOne;
                    }
                ));

                // Задержка между вылетами карт (чтобы звуки не сливались в один)
                yield return new WaitForSeconds(0.1f);
            }
        }

        yield return new WaitForSeconds(duration);
    }

    public void OnUndoAction()
    {
        if (isUndoing || undoStack.Count == 0) return;

        // --- ВОСКРЕШЕНИЕ ПОСЛЕ ПОРАЖЕНИЯ ---
        // Если GameUIController вызвал отмену из панели поражения, снимаем внутренние блокировки
        if (_isGameFinished)
        {
            _isGameFinished = false;
            isTimerRunning = true;
        }
        else if (!IsInputAllowed)
        {
            // Обычная защита от кликов во время полета карт
            return;
        }

        // --- ЗВУК: Нажатие кнопки отмены ---
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Back");
        }

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // ----------------------

        RegisterMoveAndStartIfNeeded();
        StartCoroutine(UndoAnimatedRoutine());
    }

    public void OnUndoAllAction()
    {
        if (isUndoing || undoStack.Count == 0) return;

        // --- НОВОЕ: ВОСКРЕШЕНИЕ ПОСЛЕ ПОРАЖЕНИЯ ---
        if (_isGameFinished)
        {
            _isGameFinished = false;
            isTimerRunning = true;
        }
        else if (!IsInputAllowed)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("UI_Back");
            AudioManager.Instance.PlaySound("Card_Whoosh_Out");
        }

        // ---> ДОБАВИТЬ ЭТО <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // ----------------------

        RegisterMoveAndStartIfNeeded();

        IsInputAllowed = false;
        isUndoing = true;

        while (undoStack.Count > 0)
        {
            OctagonMoveRecord record = undoStack.Pop();
            if (scoreHistory.Count > 0) currentScore = scoreHistory.Pop();
            PerformImmediateUndo(record);
        }

        ResetFoundationCombo();

        foreach (var g in pileManager.TableauGroups) g.UpdateTopCardState();

        isUndoing = false;
        IsInputAllowed = true;
        UpdateFullUI();
    }

    private void PerformImmediateUndo(OctagonMoveRecord record)
    {
        if (record.SubMoves.Count > 0)
        {
            var firstMove = record.SubMoves[0];
            if (firstMove.Source == pileManager.WastePile && firstMove.Target == pileManager.StockPile)
                recyclesUsed = Mathf.Max(0, recyclesUsed - 1);
        }

        if (record.RevealedCard != null)
        {
            GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, -1);
            var data = record.RevealedCard.GetComponent<CardData>();
            if (data != null) data.SetFaceUp(false, false);
        }

        for (int i = record.SubMoves.Count - 1; i >= 0; i--)
        {
            var move = record.SubMoves[i];
            CardController card = move.Card;
            ICardContainer targetContainer = move.Source; // КУДА летит карта (возврат)
            ICardContainer currentContainer = card.GetComponentInParent<ICardContainer>(); // ОТКУДА улетает

            if (targetContainer == null || card == null) continue;

            // ---> АНТИ-ЧИТ ДЛЯ ДОМОВ И СТОЛБЦОВ <---
            if (currentContainer is OctagonFoundationPile)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, -1);
                if (card.cardModel.rank > 0)
                {
                    GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(card.cardModel.rank);
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -1, card.cardModel.rank.ToString());
                }

                if (targetContainer is OctagonWastePile)
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, -1);
                else if (targetContainer is OctagonTableauSlot || targetContainer is OctagonTableauGroup)
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, -1);
            }

            if (targetContainer is OctagonTableauSlot tsAntiCheat && tsAntiCheat.Group != null && tsAntiCheat.Group.IsEmpty())
                GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, -1);
            // ---------------------------------------

            bool targetFaceUp = move.WasFaceUp;
            if (targetContainer is OctagonStockPile) targetFaceUp = false;
            else if (targetContainer is OctagonWastePile) targetFaceUp = true;

            Vector3 targetLocalPos = Vector3.zero;

            if (targetContainer is OctagonStockPile spPos)
                targetLocalPos = new Vector3(spPos.CardCount * spPos.offsetX, spPos.CardCount * spPos.offsetY, 0f);
            else if (targetContainer is OctagonWastePile wpPos)
                targetLocalPos = new Vector3(wpPos.CardCount * wpPos.offsetX, wpPos.CardCount * wpPos.offsetY, 0f);
            else
                targetLocalPos = targetContainer.GetDropAnchoredPosition(card);

            card.transform.SetParent(targetContainer.Transform);
            card.rectTransform.anchoredPosition = targetLocalPos;
            card.transform.localRotation = Quaternion.identity;

            targetContainer.AcceptCard(card);

            if (targetContainer is OctagonTableauSlot tsLayout) tsLayout.UpdateLayout();
            else if (targetContainer is OctagonWastePile wpLayout) wpLayout.UpdateLayout();

            if (currentContainer is OctagonWastePile oldWp) oldWp.UpdateLayout();
            else if (currentContainer is OctagonTableauSlot oldTs) oldTs.UpdateLayout();

            var data = card.GetComponent<CardData>();
            if (data) data.SetFaceUp(targetFaceUp, false);

            var cg = card.GetComponent<CanvasGroup>();
            if (cg) cg.blocksRaycasts = move.WasRaycastBlocked;
        }
    }

    private IEnumerator UndoAnimatedRoutine()
    {
        isUndoing = true;
        IsInputAllowed = false;

        GameQuestTracker.Instance?.RecordUndoUsed();

        if (scoreHistory.Count > 0) currentScore = scoreHistory.Pop();
        ResetFoundationCombo();
        UpdateFullUI();

        OctagonMoveRecord record = undoStack.Pop();

        if (record.SubMoves.Count > 0)
        {
            var firstMove = record.SubMoves[0];
            if (firstMove.Source == pileManager.WastePile && firstMove.Target == pileManager.StockPile)
                recyclesUsed = Mathf.Max(0, recyclesUsed - 1);
        }

        if (record.RevealedCard != null)
        {
            GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, -1);
            var data = record.RevealedCard.GetComponent<CardData>();
            if (data != null) data.SetFaceUp(false, true);
        }

        float undoMoveDuration = 0.25f;
        float undoInterval = 0.08f;

        for (int i = record.SubMoves.Count - 1; i >= 0; i--)
        {
            var move = record.SubMoves[i];
            CardController card = move.Card;
            ICardContainer targetContainer = move.Source;
            ICardContainer currentContainer = card.GetComponentInParent<ICardContainer>();

            if (targetContainer == null || card == null) continue;

            // ---> АНТИ-ЧИТ ДЛЯ ДОМОВ И СТОЛБЦОВ <---
            if (currentContainer is OctagonFoundationPile)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, -1);
                if (card.cardModel.rank > 0)
                {
                    GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(card.cardModel.rank);
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -1, card.cardModel.rank.ToString());
                }

                if (targetContainer is OctagonWastePile)
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, -1);
                else if (targetContainer is OctagonTableauSlot || targetContainer is OctagonTableauGroup)
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, -1);
            }

            if (targetContainer is OctagonTableauSlot ts && ts.Group != null && ts.Group.IsEmpty())
                GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, -1);
            // ---------------------------------------

            bool targetFaceUp = move.WasFaceUp;
            if (targetContainer is OctagonStockPile) targetFaceUp = false;
            else if (targetContainer is OctagonWastePile) targetFaceUp = true;

            Vector3 targetLocalPos = Vector3.zero;
            if (targetContainer is OctagonStockPile sp)
                targetLocalPos = new Vector3(sp.CardCount * sp.offsetX, sp.CardCount * sp.offsetY, 0f);
            else if (targetContainer is OctagonWastePile wp)
                targetLocalPos = new Vector3(wp.CardCount * wp.offsetX, wp.CardCount * wp.offsetY, 0f);
            else
                targetLocalPos = targetContainer.GetDropAnchoredPosition(card);


            bool isStockWasteMove = false;
            if (currentContainer != null)
            {
                isStockWasteMove = (targetContainer is OctagonStockPile || targetContainer is OctagonWastePile) &&
                                   (currentContainer is OctagonStockPile || currentContainer is OctagonWastePile);
            }

            if (AudioManager.Instance != null)
            {
                if (isStockWasteMove) AudioManager.Instance.PlaySound("Card_Flip");
                else AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            }

            StartCoroutine(animationService.AnimateMoveCard(
                card,
                targetContainer.Transform,
                targetLocalPos,
                undoMoveDuration,
                targetFaceUp,
                () =>
                {
                    targetContainer.AcceptCard(card);
                    var cg = card.GetComponent<CanvasGroup>();
                    if (cg) cg.blocksRaycasts = move.WasRaycastBlocked;

                    if (targetContainer is OctagonWastePile wp) wp.UpdateLayout();
                    if (targetContainer is OctagonTableauSlot tSlot) tSlot.UpdateLayout();

                    if (AudioManager.Instance != null && !isStockWasteMove)
                        AudioManager.Instance.PlaySound("Card_Drop_Success");
                }
            ));

            if (currentContainer is OctagonWastePile oldWp) oldWp.UpdateLayout();
            if (currentContainer is OctagonTableauSlot oldTs) oldTs.UpdateLayout();

            yield return new WaitForSeconds(undoInterval);
        }

        yield return new WaitForSeconds(undoMoveDuration);
        foreach (var g in pileManager.TableauGroups) g.UpdateTopCardState();

        IsInputAllowed = true;
        isUndoing = false;
    }

    private IEnumerator AnimateStockToWaste(int savedScore)
    {
        IsInputAllowed = false;

        // --- ЗВУК: Листание одиночной карты ---
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySound("Card_Flip");
        }

        var card = pileManager.StockPile.PopTopCard();
        if (card != null)
        {
            OctagonMoveRecord record = new OctagonMoveRecord();
            record.AddMove(card, pileManager.StockPile, pileManager.WastePile, false);

            scoreHistory.Push(savedScore);
            undoStack.Push(record);

            int nextWasteIndex = pileManager.WastePile.CardCount;
            Vector3 targetLocalPos = new Vector3(
                nextWasteIndex * pileManager.WastePile.offsetX,
                nextWasteIndex * pileManager.WastePile.offsetY,
                0f
            );

            yield return StartCoroutine(animationService.AnimateMoveCard(
                card,
                pileManager.WastePile.transform,
                targetLocalPos,
                0.25f,
                true,
                () =>
                {
                    pileManager.WastePile.AddCard(card);
                    CheckGameState();
                }
            ));
        }
        IsInputAllowed = true;
    }

    private IEnumerator AnimateRecycle(int savedScore)
    {
        IsInputAllowed = false;
        OctagonMoveRecord record = new OctagonMoveRecord();
        var cards = new List<CardController>(pileManager.WastePile.GetComponentsInChildren<CardController>());

        int virtualStockCount = pileManager.StockPile.CardCount;

        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var card = cards[i];
            record.AddMove(card, pileManager.WastePile, pileManager.StockPile, true);

            Vector3 targetLocalPos = new Vector3(
                virtualStockCount * pileManager.StockPile.offsetX,
                virtualStockCount * pileManager.StockPile.offsetY,
                0f
            );
            virtualStockCount++;

            // --- ЗВУК: Рецикл колоды (звук проигрывается для каждой летящей карты) ---
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound("Card_Flip");
            }

            StartCoroutine(animationService.AnimateMoveCard(
                card, pileManager.StockPile.transform, targetLocalPos, 0.15f, false,
                () => { pileManager.StockPile.AddCard(card); }
            ));
            yield return new WaitForSeconds(0.02f);
        }

        scoreHistory.Push(savedScore);
        undoStack.Push(record);

        yield return new WaitForSeconds(0.3f);
        pileManager.WastePile.UpdateLayout();

        IsInputAllowed = true;
        CheckGameState();
    }

    private IEnumerator AnimateAutoMove(CardController card, ICardContainer target, int savedScore)
    {
        // 1. СНАЧАЛА регистрируем ход, пока IsInputAllowed еще = true!
        RegisterMoveAndStartIfNeeded();

        // 2. Теперь блокируем ввод на время анимации
        IsInputAllowed = false;

        // Надежно фиксируем источник
        ICardContainer source = card.GetComponentInParent<ICardContainer>();
        if (source == null && card is OctagonCardController octCard) source = octCard.SourceContainer;

        OctagonMoveRecord record = new OctagonMoveRecord();
        bool wasFaceUp = true;
        record.AddMove(card, source, target, wasFaceUp);

        CheckRevealCardUnder(source, record, card);

        Vector3 targetLocalPos = target.GetDropAnchoredPosition(card);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Whoosh_Out");

        yield return StartCoroutine(animationService.AnimateMoveCard(
            card, target.Transform, targetLocalPos, 0.2f, true,
            () =>
            {
                target.AcceptCard(card);
                // ВНИМАНИЕ: Строка RegisterMoveAndStartIfNeeded() убрана отсюда, так как она уже вызвана выше

                if (target is OctagonFoundationPile)
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Foundation_Success");
                    OnCardPlacedToFoundation();
                }
                else
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");
                    ResetFoundationCombo();
                }

                // ---> ТРЕКИНГ КВЕСТОВ ПРИ АВТО-ХОДЕ (ДВОЙНОЙ КЛИК) <---
                if (!GameSettings.IsTutorialMode)
                {
                    bool isFromCorner = source is OctagonTableauSlot || source is OctagonTableauGroup;
                    bool isFromWaste = source is OctagonWastePile;

                    if (target is OctagonFoundationPile)
                    {
                        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 1);
                        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());

                        if (isFromWaste) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, 1);
                        else if (isFromCorner) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, 1);
                    }

                    if (record.RevealedCard != null) GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, 1);

                    if (source is OctagonTableauSlot slot && slot.Group != null)
                    {
                        slot.Group.UpdateTopCardState();
                        if (slot.Group.IsEmpty()) GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearTableauColumn, 1);
                    }
                }
                // --------------------------------------------------------

                StartCoroutine(CheckRefillAndFinalizeMove(record, savedScore));
            }
        ));
    }

    public ICardContainer FindNearestContainer(CardController card, Vector2 screenPos, float maxDistance)
    {
        Rect cardRect = GetWorldRect(card.rectTransform);
        ICardContainer best = null;
        float bestArea = 0f;
        List<ICardContainer> all = new List<ICardContainer>();
        all.AddRange(pileManager.FoundationPiles);
        foreach (var g in pileManager.TableauGroups) all.AddRange(g.Slots);

        foreach (var c in all)
        {
            var mono = c as MonoBehaviour;
            if (mono == null) continue;
            Rect targetRect;
            CardController top = null;
            if (c is OctagonTableauSlot ts) top = ts.GetTopCard();
            else if (c is OctagonFoundationPile fp) top = fp.GetTopCard();

            if (top != null) targetRect = GetWorldRect(top.rectTransform);
            else targetRect = GetWorldRect(mono.transform as RectTransform);

            float area = GetIntersectionArea(cardRect, targetRect);
            if (area > bestArea && c.CanAccept(card)) { bestArea = area; best = c; }
        }
        return best;
    }

    private bool IsCardMovable(CardController card)
    {
        Transform p = card.transform.parent;
        if (p == null || p.GetComponent<OctagonStockPile>()) return false;
        return p.GetChild(p.childCount - 1) == card.transform;
    }

    private Rect GetWorldRect(RectTransform rt) { Vector3[] c = new Vector3[4]; rt.GetWorldCorners(c); return new Rect(c[0].x, c[0].y, Mathf.Abs(c[2].x - c[0].x), Mathf.Abs(c[2].y - c[0].y)); }
    private float GetIntersectionArea(Rect r1, Rect r2) { float w = Mathf.Min(r1.xMax, r2.xMax) - Mathf.Max(r1.xMin, r2.xMin); float h = Mathf.Min(r1.yMax, r2.yMax) - Mathf.Max(r1.yMin, r2.yMin); return (w > 0 && h > 0) ? w * h : 0f; }

    // ==========================================
    // ИСПРАВЛЕННЫЙ МЕТОД ЗАВЕРШЕНИЯ ИГРЫ
    // ==========================================
    public void CheckGameState()
    {
        if (_isGameFinished) return;

        int k = 0;
        foreach (var f in pileManager.FoundationPiles)
        {
            if (f.GetTopCard()?.cardModel.rank == 13) k++;
        }

        if (k == 8)
        {
            _isGameFinished = true;
            _isGameWon = true; // Фиксируем, что это именно победа
            isTimerRunning = false;
            undoStack.Clear(); // Блокируем отмену ходов после победы

            int finalMoves = 0;
            if (GameSettings.IsTutorialMode && tutorialManager != null)
            {
                // Если у вас есть система локализации, достаньте строку "OctagonTutorialEnd". 
                // Если её нет под рукой, можно передать обычную строку:
                string victoryText = "<color=#FCA311>Поздравляем!</color> Вы успешно прошли обучение и собрали пасьянс Восьмиугольник!";
                tutorialManager.ShowVictoryStep(victoryText);
            }
            if (StatisticsManager.Instance != null)
            {
                finalMoves = StatisticsManager.Instance.GetCurrentMoves();
                StatisticsManager.Instance.OnGameWon(currentScore);
            }

            if (gameUI)
            {
                gameUI.OnGameWon(finalMoves);

                // --- ГЛАВНЫЙ ФИКС: Принудительно передаем наши очки в панель ---
                if (gameUI.winScoreText != null)
                {
                    gameUI.winScoreText.text = currentScore.ToString();
                }
            }
            return;
        }

        // Запускаем отложенную проверку поражения
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);
        defeatRoutine = StartCoroutine(ShowDefeatRoutine());
    }
    private IEnumerator ShowDefeatRoutine()
    {
        // --- ГЛАВНЫЙ ФИКС ТАЙМИНГА ---
        // Сначала ждем, пока все карты долетят и управление вернется игроку
        while (!IsInputAllowed || isUndoing || (dragLayer != null && dragLayer.childCount > 0))
        {
            yield return null;
        }

        // Только ПОСЛЕ завершения всех анимаций отсчитываем 1.5 секунды
        yield return new WaitForSeconds(1.5f);
        if (_isGameFinished) yield break;

        // На всякий случай проверяем, не нажал ли игрок на какую-то другую кнопку за эти 1.5 секунды
        if (!IsInputAllowed || isUndoing) yield break;

        if (!HasAnyValidMove() && gameUI != null)
        {
            _isGameFinished = true;
            isTimerRunning = false;
            IsInputAllowed = false;

            // МЫ УБРАЛИ OnGameAbandoned() отсюда, чтобы статистика не сбрасывалась!
            // Игра считается покинутой, только если игрок выйдет в меню или нажмет "Новая игра".

            // Вызываем стандартную логику UI для показа панели поражения
            gameUI.OnGameLost();
        }

        defeatRoutine = null;
    }

    // ==========================================
    // ИСПРАВЛЕННЫЙ СКАНЕР ВСЕХ ДОСТУПНЫХ ХОДОВ
    // ==========================================
    private bool HasAnyValidMove()
    {
        // 1. Если карта прямо сейчас летит/перетаскивается
        if (dragLayer != null && dragLayer.childCount > 0) return true;

        // 2. Если есть карты в колоде или доступны перелистывания
        if (pileManager.StockPile.CardCount > 0) return true;
        if (pileManager.WastePile.CardCount > 0 && recyclesUsed < maxRecycles) return true;

        // 3. Проверяем верхнюю карту сброса
        var topWaste = pileManager.WastePile.GetTopCard();
        if (topWaste != null)
        {
            var wasteData = topWaste.GetComponent<CardData>();
            if (wasteData != null && wasteData.IsFaceUp())
            {
                foreach (var f in pileManager.FoundationPiles)
                    if (f.CanAccept(topWaste)) return true;

                foreach (var group in pileManager.TableauGroups)
                    foreach (var slot in group.Slots)
                        if (slot.CanAccept(topWaste)) return true;
            }
        }

        // 4. Проверяем верхние карты на столе
        foreach (var group in pileManager.TableauGroups)
        {
            // Считаем общее количество карт во всей группе
            int totalCardsInGroup = 0;
            foreach (var s in group.Slots) totalCardsInGroup += s.transform.childCount;

            foreach (var slot in group.Slots)
            {
                var topCard = slot.GetTopCard();
                if (topCard == null) continue;

                var topData = topCard.GetComponent<CardData>();
                if (topData == null || !topData.IsFaceUp()) continue;

                // А. Может ли верхняя карта уйти прямо в Дом? (Это абсолютный прогресс)
                foreach (var f in pileManager.FoundationPiles)
                {
                    if (f.CanAccept(topCard)) return true;
                }

                // Б. Может ли перелечь на другой слот стола?
                bool isProgressiveMove = false;

                // --- НОВОЕ ПРАВИЛО: Авто-заполнение ---
                // Если это ПОСЛЕДНЯЯ карта в группе, и у нас есть резерв в сбросе или колоде, 
                // то ее перенос освободит группу и вызовет заполнение новыми картами!
                if (totalCardsInGroup == 1 && (pileManager.WastePile.CardCount > 0 || pileManager.StockPile.CardCount > 0))
                {
                    isProgressiveMove = true;
                }
                // --- СТАРОЕ ПРАВИЛО: Поиск нужной карты снизу ---
                else if (slot.transform.childCount > 1)
                {
                    for (int i = slot.transform.childCount - 2; i >= 0; i--)
                    {
                        var underCard = slot.transform.GetChild(i).GetComponent<CardController>();
                        var underData = underCard?.GetComponent<CardData>();
                        if (underData == null) break;

                        if (!underData.IsFaceUp())
                        {
                            isProgressiveMove = true; // Нашли закрытую карту
                            break;
                        }
                        else
                        {
                            foreach (var f in pileManager.FoundationPiles)
                            {
                                if (f.CanAccept(underCard))
                                {
                                    isProgressiveMove = true; // Нашли карту для Дома
                                    break;
                                }
                            }
                            if (isProgressiveMove) break;
                        }
                    }
                }

                // Если перемещение имеет смысл — ищем, КУДА можно положить верхнюю карту
                if (isProgressiveMove)
                {
                    foreach (var targetGroup in pileManager.TableauGroups)
                    {
                        foreach (var targetSlot in targetGroup.Slots)
                        {
                            if (slot == targetSlot) continue;
                            if (targetSlot.CanAccept(topCard)) return true; // Нашли полезный ход!
                        }
                    }
                }
            }
        }

        // Если ни один ход не ведет к прогрессу — это 100% тупик!
        return false;
    }
    private void OnDestroy()
    {
        // ИЗМЕНЕНО: !_isGameFinished заменено на !_isGameWon
        if (hasGameStarted && !_isGameWon && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }
    }

    public bool OnDropToBoard(CardController c, Vector2 p)
    {
        // Запускаем звук с задержкой 0.2с, чтобы он совпал с концом анимации возврата
        StartCoroutine(PlayDelayedSound("Card_Drop_Fail", 0.2f));
        return false;
    }

    public void OnUndoActionDummy() { }
    public void RestartGame()
    {
        isRestarting = true;
        StopAllCoroutines();
        StartNewGame();
    }
    public void TrackOctagonQuest(CardController card, ICardContainer source, ICardContainer container)
    {
        if (!IsInputAllowed || GameSettings.IsTutorialMode) return;

        bool isToFoundation = false;
        bool fromWasteToFoundation = false;
        bool fromTableauToFoundation = false;

        // 1. Карта попала в ДОМ
        if (container is OctagonFoundationPile)
        {
            isToFoundation = true;
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveCardsToFoundation, 1);
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());

            // Задание: "Центральное снабжение"
            if (source is OctagonWastePile)
            {
                fromWasteToFoundation = true;
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, 1);
            }
            // Задание: "Раскрытие углов"
            else if (source is OctagonTableauSlot)
            {
                fromTableauToFoundation = true;
                GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, 1);
            }
        }

        // 2. ОТКАТ, если игрок вытащил карту из Дома руками
        if (source is OctagonFoundationPile && container != source)
        {
            GameQuestTracker.Instance?.RecordCardRemovedFromFoundation(card.cardModel.rank);
            if (container is OctagonWastePile) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromWasteToFoundation, -1);
            else if (container is OctagonTableauSlot) GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveFromCorner, -1);
        }

        questUndoStack.Push(new OctagonQuestUndoRecord
        {
            IsToFoundation = isToFoundation,
            FromWasteToFoundation = fromWasteToFoundation,
            FromTableauToFoundation = fromTableauToFoundation,
            CardRank = card.cardModel.rank
        });
    }
    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed || _isGameFinished || isUndoing) return;

        // На мобильных устройствах и планшетах авто-перенос срабатывает по одинарному клику
        if(GameSettings.AutoMoveClickMode == 0)
        {
            // Защита от микро-свайпов: если палец сдвинулся, возвращаем карту на место
            var octCard = card as OctagonCardController;
            if (octCard != null && octCard.transform.parent == dragLayer)
            {
                octCard.StopAllCoroutines();
                if (octCard.SourceContainer != null && octCard.SourceContainer.Transform != null)
                {
                    octCard.transform.SetParent(octCard.SourceContainer.Transform, true);
                    octCard.transform.localPosition = Vector3.zero;
                }
            }

            ProcessAutoMove(card);
        }
    }
    public void OnCardDoubleClicked(CardController c, bool b) { OnCardDoubleClicked(c); }
    public void OnCardLongPressed(CardController c) { }
    public void OnKeyboardPick(CardController c) { }
}