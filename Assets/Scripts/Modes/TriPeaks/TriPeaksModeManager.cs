using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class TriPeaksModeManager : MonoBehaviour, IModeManager, ICardGameMode
{
    [Header("Core References")]
    public CardFactory cardFactory;
    public Canvas rootCanvas;
    public RectTransform dragLayer;
    public GameUIController gameUI;
    public TriPeaksIntroController introController; // [NEW] Контроллер UI интро
    public TriPeaksTutorialManager tutorialManager;
    [Header("Services")]
    public TriPeaksPileManager pileManager;
    public TriPeaksScoreManager scoreManager;
    public TriPeaksAnimationService animationService;
    public DragManager dragManager;

    [Header("UI Buttons")]
    public Button undoButton;
    public Button undoAllButton;

    [Header("HUD")]
    public TMP_Text movesText;
    public TMP_Text scoreText;
    public TMP_Text timeText;

    [Header("Animation Settings")]
    public float dealFlyDuration = 0.25f;
    public float totalDealDuration = 1.2f;
    public float stockToWasteDuration = 0.15f;
    public float stockShiftDuration = 0.2f;
    public float tableauToWasteSpeed = 150f;
    public float flipWaitDelay = 0.3f;
    public float undoMoveDuration = 0.2f;
    public float undoAllMoveDuration = 0.08f;

    private Difficulty currentDifficulty = Difficulty.Easy;
    private int currentRound = 1;
    private int totalRounds = 1;

    private bool _isInputAllowed = true;
    private bool _isSetupRunning = false;
    private bool _isUndoing = false;
    

    private bool _hasGameStarted = false;
    private bool _isGameEnded = false;
    private bool _isGameWon = false;

    private float gameTimer = 0f;
    private bool isTimerRunning = false;
    private Coroutine _stockShiftCoroutine;

    private CardModel _logicTopCardModel;
    private Stack<TriPeaksMoveRecord> _undoStack = new Stack<TriPeaksMoveRecord>();

    public string GameName => "TriPeaks";
    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => null;
    public PileManager PileManager => null;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public GameType GameType => GameType.TriPeaks;
    public ITutorialManager Tutorial => tutorialManager;
    public bool IsInputAllowed
    {
        get => _isInputAllowed && !_isSetupRunning && !_isUndoing && !_isGameEnded;
        set
        {
            _isInputAllowed = value;

            // ХИТРОСТЬ ДЛЯ ПАНЕЛИ ПОРАЖЕНИЯ:
            // Глобальный GameUIController делает IsInputAllowed = true прямо перед тем, как 
            // попытаться "нажать" основную кнопку отмены. Мы перехватываем этот момент!
            if (value && _isGameEnded && !_isGameWon)
            {
                // Мгновенно снимаем статус проигрыша
                _isGameEnded = false;
                isTimerRunning = true;

                // Мгновенно включаем кнопки ДО того, как контроллер проверит их статус
                bool hasHistory = _undoStack.Count > 0;
                if (undoButton != null) undoButton.interactable = hasHistory;
                if (undoAllButton != null) undoAllButton.interactable = hasHistory;
            }
        }
    }
    public bool IsMatchInProgress() => _hasGameStarted && !_isGameEnded && !_isGameWon;

    private void Start()
    {
        if (animationService == null) animationService = GetComponent<TriPeaksAnimationService>();
        if (animationService == null) animationService = gameObject.AddComponent<TriPeaksAnimationService>();

        if (undoButton != null) undoButton.onClick.AddListener(OnUndoAction);
        if (undoAllButton != null) LongPressHoldTrigger.SubscribeToButton(undoAllButton, OnUndoAllAction);

        currentDifficulty = GameSettings.CurrentDifficulty;
        totalRounds = GameSettings.RoundsCount;
        if (totalRounds < 1) totalRounds = 1;

        InitializeMode();
    }

    private void Update()
    {
        // Кнопки зависят строго от того, разрешен ли ввод. 
        // При проигрыше IsInputAllowed = false, и кнопки сами потухнут.
        bool canInteract = IsInputAllowed;

        bool hasHistory = _undoStack.Count > 0;
        if (undoButton != null) undoButton.interactable = canInteract && hasHistory;
        if (undoAllButton != null) undoAllButton.interactable = canInteract && hasHistory;

        if (isTimerRunning && !_isGameEnded && !_isGameWon)
        {
            gameTimer += Time.deltaTime;
            UpdateTimeUI();
        }
    }

    private void UpdateFullUI()
    {
        if (movesText != null)
        {
            if (!_hasGameStarted) movesText.text = "0";
            else if (StatisticsManager.Instance != null) movesText.text = StatisticsManager.Instance.GetCurrentMoves().ToString();
            else movesText.text = "0";
        }
        if (scoreText != null)
        {
            if (!_hasGameStarted) scoreText.text = "0";
            else scoreText.text = $"{scoreManager?.CurrentScore ?? 0}";
        }
        if (!_hasGameStarted) UpdateTimeUI();
    }

    private void UpdateTimeUI()
    {
        if (timeText != null)
        {
            int totalSeconds = Mathf.FloorToInt(gameTimer);
            timeText.text = string.Format("{0}:{1:00}", totalSeconds / 60, totalSeconds % 60);
        }
    }

    private void RegisterActivity()
    {
        if (!_hasGameStarted)
        {
            _hasGameStarted = true;
            if (StatisticsManager.Instance != null)
            {
                string variant = GameSettings.GetCurrentVariantString(GameType.TriPeaks);
                StatisticsManager.Instance.OnGameStarted("TriPeaks", GameSettings.CurrentDifficulty, variant);
            }
        }

        isTimerRunning = true;
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
        UpdateFullUI();
    }

    private void OnDestroy()
    {
        if (_hasGameStarted && !_isGameWon)
            if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameAbandoned();
    }

    public void InitializeMode()
    {
        // При инициализации (первом входе) делаем полный сброс и ИГРАЕМ интро
        RestartGameInternal(true, true);
    }

    public void RestartGame()
    {
        currentDifficulty = GameSettings.CurrentDifficulty;
        totalRounds = GameSettings.RoundsCount;
        if (totalRounds < 1) totalRounds = 1;

        RestartGameInternal(true, false);
    }

    private void RestartGameInternal(bool fullReset, bool playIntro)
    {
        StopAllCoroutines();

        if (fullReset)
        {
            // 1. Закрываем старую игру
            if (_hasGameStarted && !_isGameWon && StatisticsManager.Instance != null)
            {
                StatisticsManager.Instance.OnGameAbandoned();
            }

            // 2. Инициализируем трекер
            string variant = GameSettings.GetCurrentVariantString(GameType.TriPeaks) ?? "None";
            GameQuestTracker.Instance?.StartMatch("TriPeaks", currentDifficulty, variant);

            _hasGameStarted = false;
            currentRound = 1;
            gameTimer = 0f;
            isTimerRunning = false;
            if (scoreManager != null) scoreManager.ResetScore();
        }

        _isGameEnded = false;
        _isGameWon = false;
        _isInputAllowed = true;
        _isSetupRunning = false;
        _isUndoing = false;
        _undoStack.Clear();

        UpdateFullUI();

        if (pileManager != null) pileManager.ClearAll();

        // Интеграция Туториала
        if (GameSettings.IsTutorialMode && tutorialManager != null && fullReset)
        {
            StartCoroutine(tutorialManager.PlayTutorialIntro(null));
        }
        else
        {
            StartCoroutine(SetupRoundRoutine(playIntro));
        }
    }

    // [NEW] Единая переписанная корутина настройки раунда и интро
    private IEnumerator SetupRoundRoutine(bool playIntro = false)
    {
        _isSetupRunning = true;
        if (pileManager == null) yield break;

        Deal deal = null;
        if (DealCacheSystem.Instance != null)
            deal = DealCacheSystem.Instance.GetDeal(GameType.TriPeaks, currentDifficulty, totalRounds);

        if (deal == null) { _isSetupRunning = false; yield break; }

        // --- 1. Анимация интерфейса ---
        if (introController != null) introController.PrepareIntro(playIntro);

        if (introController != null && playIntro)
        {
            yield return StartCoroutine(introController.PlayUIIntroSequence());
        }

        // --- 2. Генерация всех 52 карт ---
        List<CardInstance> stockSource = new List<CardInstance>(deal.stock);
        stockSource.Reverse();

        CardInstance wasteInst = null;
        if (stockSource.Count > 0)
        {
            wasteInst = stockSource[stockSource.Count - 1];
            stockSource.RemoveAt(stockSource.Count - 1);
        }

        List<CardController> allCardsToFly = new List<CardController>();
        List<CardController> stockCards = new List<CardController>();
        List<CardController> tableauCards = new List<CardController>();
        CardController initialWasteCard = null;

        for (int i = 0; i < stockSource.Count; i++)
        {
            CardController c = CreateFaceDownCard(stockSource[i].Card);
            stockCards.Add(c);
            pileManager.Stock.AddCard(c);
        }

        if (wasteInst != null)
        {
            initialWasteCard = CreateFaceDownCard(wasteInst.Card);
        }

        int tableauIndex = 0;
        foreach (var cardList in deal.tableau)
        {
            if (tableauIndex >= pileManager.TableauPiles.Count) break;
            if (cardList == null || cardList.Count == 0) { tableauIndex++; continue; }

            CardController c = CreateFaceDownCard(cardList[0].Card);
            tableauCards.Add(c);
            tableauIndex++;
        }

        // Правильный порядок для визуального слоения: 
        allCardsToFly.AddRange(stockCards);
        if (initialWasteCard != null) allCardsToFly.Add(initialWasteCard);
        for (int i = tableauCards.Count - 1; i >= 0; i--)
        {
            allCardsToFly.Add(tableauCards[i]);
        }

        // --- 3. Анимация: Вылет единой стопкой и раскрытие ---
        float screenW = rootCanvas.GetComponent<RectTransform>().rect.width;
        System.Func<bool> skipCheck = () => introController != null && introController.IsSkipping;
        float gap = pileManager.Stock.Gap > 0 ? pileManager.Stock.Gap : 5f;

        if (animationService != null)
        {
            yield return StartCoroutine(animationService.AnimateDeckArrivalAndExpand(allCardsToFly, pileManager.Stock.transform, screenW, stockCards.Count, skipCheck, 1f, gap));
        }

        // --- 4. Анимация: Разлет карт в Табло ---
        float delayPerCard = totalDealDuration / (tableauCards.Count + 1);
        for (int i = 0; i < tableauCards.Count; i++)
        {
            CardController card = tableauCards[i];
            TriPeaksTableauPile targetSlot = pileManager.TableauPiles[i];
            bool flyFaceUp = (i >= 18);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Deal");
            StartCoroutine(animationService.AnimateMoveCard(card, targetSlot.transform, dealFlyDuration, flyFaceUp, () =>
            {
                targetSlot.AddCard(card);
            }));

            float waitTimer = 0f;
            while (waitTimer < delayPerCard)
            {
                float speed = skipCheck() ? 15f : 1f;
                waitTimer += Time.deltaTime * speed;
                yield return null;
            }
        }

        // --- 5. Анимация: Открытие первой карты в Waste ---
        if (initialWasteCard != null)
        {
            _logicTopCardModel = initialWasteCard.cardModel;

            // <--- ЗВУК: ПЕРЕВОРОТ СТАРТОВОЙ КАРТЫ СБРОСА --->
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Flip");

            Vector3 offset = pileManager.Waste.GetTargetLocalPositionForNextCard();
            yield return StartCoroutine(animationService.AnimateMoveCard(initialWasteCard, pileManager.Waste.transform, dealFlyDuration, true, () =>
            {
                pileManager.Waste.AddCard(initialWasteCard);

                // Опционально: звук приземления карты в сброс
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySound("Card_Drop_Success");
            }, offset));
        }

        yield return new WaitForSeconds(dealFlyDuration + 0.1f);

        ForceEnableInput();
        pileManager.Stock.UpdateVisuals();

        _isSetupRunning = false;
        _isInputAllowed = true;

        // Запуск таймера при старте или переходе на новый раунд
        if (_hasGameStarted)
        {
            isTimerRunning = true;
        }
    }

    private CardController CreateFaceDownCard(CardModel model)
    {
        CardController c = cardFactory.CreateCard(model, pileManager.Stock.transform, Vector2.zero);
        var cd = c.GetComponent<CardData>();
        if (cd != null) cd.SetFaceUp(false, false);
        c.OnClicked += OnCardClicked;
        return c;
    }

    private void ForceEnableInput()
    {
        var allCards = FindObjectsOfType<CardController>();
        foreach (var c in allCards)
        {
            if (c.canvasGroup) { c.canvasGroup.blocksRaycasts = true; c.canvasGroup.interactable = true; }
            var img = c.GetComponent<Image>();
            if (img) img.raycastTarget = true;
        }
    }

    public void OnCardClicked(CardController card)
    {
        if (!IsInputAllowed) return;

        if (pileManager.Stock.Contains(card)) { OnStockClicked(); return; }

        TriPeaksTableauPile slot = pileManager.FindSlotWithCard(card);
        if (slot != null)
        {
            // 1. ОШИБКА: Карта заблокирована другими картами сверху
            if (slot.IsBlocked())
            {
                card.StopAllCoroutines(); // Прерываем предыдущую тряску от спам-кликов
                card.StartCoroutine(animationService.AnimateShakeError(card));
                return;
            }

            // 2. УСПЕХ: Карта подходит по правилам
            if (CheckMatch(card.cardModel, _logicTopCardModel))
            {
                StartCoroutine(MoveToWasteRoutine(card, slot, pileManager.Waste));
            }
            // 3. ОШИБКА: Карта свободна, но не совпадает по рангу с Waste
            else
            {
                card.StopAllCoroutines(); // Прерываем предыдущую тряску
                card.StartCoroutine(animationService.AnimateShakeError(card));
            }
        }
    }

    public void OnStockClicked()
    {
        if (!IsInputAllowed) return;
        DrawFromStock();
    }

    private void DrawFromStock()
    {
        if (pileManager.Stock.IsEmpty) return;
        CardController card = pileManager.Stock.DrawCard();
        if (card == null) return;
        StartCoroutine(DrawFromStockSequence(card));
    }

    private IEnumerator DrawFromStockSequence(CardController card)
    {
        RegisterActivity();

        // ---> ДОБАВИТЬ ЭТО: Трекинг колоды и сброс комбо <---
        GameQuestTracker.Instance?.RecordMove();
        GameQuestTracker.Instance?.RecordStockDraw();
        // ----------------------------------------------------

        // <--- ЗВУК 4: КЛИК ПО КАРТЕ СТОКА (ПЕРЕЛИСТЫВАНИЕ) --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Flip");
        _logicTopCardModel = card.cardModel;

        var record = new TriPeaksMoveRecord
        {
            MovedCard = card,
            IsFromStock = true,
            PreviousStreak = scoreManager ? scoreManager.CurrentStreak : 0,
            PointsEarned = 0
        };
        _undoStack.Push(record);

        // 1. Мгновенный логический перенос (исправляет мигание по центру экрана)
        Vector3 offset = pileManager.Waste.GetTargetLocalPositionForNextCard();
        pileManager.Stock.RemoveCard(card);
        pileManager.Waste.AddCard(card);

        // 2. Прерываем старую анимацию (если нажали отмену и тут же кликнули снова)
        card.StopAllCoroutines();

        // 3. Запускаем полет на самой карте
        Coroutine moveRoutine = card.StartCoroutine(animationService.AnimateMoveCard(card, pileManager.Waste.transform, stockToWasteDuration, true, null, offset));

        // 4. Безопасный сдвиг колоды
        if (animationService != null)
        {
            if (_stockShiftCoroutine != null) StopCoroutine(_stockShiftCoroutine);
            _stockShiftCoroutine = StartCoroutine(animationService.AnimateStockShift(pileManager.Stock, stockShiftDuration));
        }

        if (scoreManager) scoreManager.ResetStreak();
        UpdateFullUI();
        CheckGameState();

        yield return moveRoutine;
    }

    private IEnumerator MoveToWasteRoutine(CardController card, ICardContainer source, ICardContainer target)
    {
        RegisterActivity();

        // ---> ДОБАВИТЬ ЭТО: Удаление карты в Три Вершины и Комбо <---
        GameQuestTracker.Instance?.RecordMove();
        GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveBoardCard, 1);
        GameQuestTracker.Instance?.IncrementCombo(QuestActionType.ComboCardsWithoutDraw);
        GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, 1, card.cardModel.rank.ToString());
        // -------------------------------------------------------------

        _logicTopCardModel = card.cardModel;

        var record = new TriPeaksMoveRecord
        {
            MovedCard = card,
            IsFromStock = false,
            SourcePile = source as TriPeaksTableauPile,
            PreviousStreak = scoreManager ? scoreManager.CurrentStreak : 0
        };

        if (scoreManager)
        {
            int points = scoreManager.BasePoints + (scoreManager.CurrentStreak + 1) * scoreManager.StreakBonus;
            record.PointsEarned = points;
        }
        _undoStack.Push(record);

        // --- ИСПРАВЛЕНИЕ: Логический перенос СРАЗУ ---
        if (source is TriPeaksTableauPile tSource)
        {
            tSource.RemoveCard(card);

            // ---> ЗАДАНИЕ "ЧИСТЫЙ ГОРИЗОНТ" (Очистка вершин) <---
            int pileIndex = pileManager.TableauPiles.IndexOf(tSource);
            if (pileIndex == 0 || pileIndex == 1 || pileIndex == 2)
            {
                GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearPeak, 1);
            }
            // ------------------------------------------------
        }

        Vector3 offset = Vector3.zero;
        if (target is TriPeaksWastePile wTarget)
        {
            offset = wTarget.GetTargetLocalPositionForNextCard();
            wTarget.AddCard(card);
        }

        // <--- ЗВУК 3 (ВЗЛЕТ): Карта срывается с места --->
        if (AudioManager.Instance != null)
        {
            AudioSource whooshSource = AudioManager.Instance.PlaySound("Card_Whoosh_Out");
            if (whooshSource != null)
            {
                whooshSource.pitch = 1.3f; // Повышаем питч (можешь подстроить значение, например 1.5f)
            }
        }

        // ЗДЕСЬ ИСПОЛЬЗУЕМ НОВЫЙ БАЛЛИСТИЧЕСКИЙ МЕТОД
        // Передаем звук приземления в коллбек onComplete
        Coroutine moveRoutine = card.StartCoroutine(animationService.AnimateBallisticMoveCard(card, target.Transform, tableauToWasteSpeed, true, () =>
        {
            // <--- ЗВУК 3 (ПРИЗЕМЛЕНИЕ): Карта долетела до дома --->
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Foundation_Success");
        }, offset));

        if (source is TriPeaksTableauPile)
        {
            if (scoreManager) scoreManager.AddStreakScore();
            UpdateFullUI();
            StartCoroutine(UpdateTableauFacesRoutine(record));
        }

        CheckGameState();

        yield return moveRoutine;
    }

    private IEnumerator UpdateTableauFacesRoutine(TriPeaksMoveRecord record)
    {
        bool anyFlip = false;
        foreach (var slot in pileManager.TableauPiles)
        {
            if (slot.HasCard && !slot.IsBlocked())
            {
                var cardData = slot.CurrentCard.GetComponent<CardData>();
                if (cardData != null && !cardData.IsFaceUp())
                {
                    cardData.SetFaceUp(true, true);
                    if (record != null) record.FlipList.Add(slot);
                    anyFlip = true;

                    // ---> ДОБАВИТЬ ЭТО: Засчитываем открытие карты <---
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, 1);
                    // ---------------------------------------------------
                }
            }
        }
        if (anyFlip) yield return new WaitForSeconds(flipWaitDelay);
    }

    public void OnUndoAction()
    {
        bool isDefeatState = _isGameEnded && !_isGameWon;
        if (!IsInputAllowed && !isDefeatState) return;
        if (_undoStack.Count == 0) return;

        // <--- ЗВУК 5: КЛИК ПО КНОПКЕ ОТМЕНЫ --->
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Back");

        if (_isGameEnded)
        {
            _isGameEnded = false;
            _isInputAllowed = true;
            isTimerRunning = true;
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        // ---> ДОБАВИТЬ СЮДА <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // -----------------------

        UpdateFullUI();
        UndoLastMoveImmediate();
    }
    private void UndoLastMoveImmediate()
    {
        TriPeaksMoveRecord record = _undoStack.Pop();

        ApplyUndoLogic(record);

        CardController card = record.MovedCard;

        // <--- УМНЫЙ ЗВУК ОТМЕНЫ (ПАТТЕРН А) --->
        if (AudioManager.Instance != null)
        {
            if (!record.IsFromStock)
            {
                // Возврат на стол: быстрый свист
                AudioSource whooshSource = AudioManager.Instance.PlaySound("Card_Whoosh_Out");
                if (whooshSource != null) whooshSource.pitch = 1.4f;
            }
            else
            {
                // Возврат в сток: просто звук перелистывания
                AudioManager.Instance.PlaySound("Card_Flip");
            }
        }

        // Защита от спама: если карта еще летела в сброс, прерываем её полет
        card.StopAllCoroutines();
        pileManager.Waste.RemoveCard(card);
        // Ускоряем анимацию возврата для большей динамики
        float fastUndoDuration = 0.12f;

        if (record.IsFromStock)
        {
            var cd = card.GetComponent<CardData>();
            if (cd != null) cd.SetFaceUp(false, true);

            pileManager.Stock.AddCard(card);

            // Фиксируем позицию, так как AddCard мог её слегка сбить
            Vector3 startPos = card.transform.position;

            card.StartCoroutine(animationService.AnimateMoveCard(card, pileManager.Stock.transform, fastUndoDuration, false, () =>
            {
                pileManager.Stock.UpdateVisuals();
            }, Vector3.zero));

            card.transform.position = startPos;

            if (animationService != null)
            {
                if (_stockShiftCoroutine != null) StopCoroutine(_stockShiftCoroutine);
                _stockShiftCoroutine = StartCoroutine(animationService.AnimateStockShift(pileManager.Stock, stockShiftDuration));
            }
        }
        else
        {
            TriPeaksTableauPile targetSlot = record.SourcePile;

            // Занимаем слот ТОЛЬКО логически. 
            // Это решает проблему мгновенной телепортации и возвращает плавную анимацию
            targetSlot.CurrentCard = card;

            card.StartCoroutine(animationService.AnimateMoveCard(card, targetSlot.transform, fastUndoDuration, true, () =>
            {
                // Физически привязываем карту к слоту только когда она долетела
                targetSlot.AddCard(card);
            }, Vector3.zero));
        }

        CardController top = pileManager.Waste.TopCard;
        if (top != null) _logicTopCardModel = top.cardModel;
    }

    private void ApplyUndoLogic(TriPeaksMoveRecord record)
    {
        // ---> ОТКАТ КВЕСТОВ ПРИ ОТМЕНЕ ХОДА <---
        if (!record.IsFromStock)
        {
            // Откат обычной убранной карты
            GameQuestTracker.Instance?.SendEvent(QuestActionType.RemoveBoardCard, -1);
            GameQuestTracker.Instance?.SendEvent(QuestActionType.MoveSpecificRanks, -1, record.MovedCard.cardModel.rank.ToString());

            // Откат очищенной вершины
            if (record.SourcePile != null)
            {
                int pileIndex = pileManager.TableauPiles.IndexOf(record.SourcePile);
                if (pileIndex == 0 || pileIndex == 1 || pileIndex == 2)
                {
                    GameQuestTracker.Instance?.SendEvent(QuestActionType.ClearPeak, -1);
                }
            }
        }
        // ---------------------------------------
        if (scoreManager)
            scoreManager.RestoreScoreAndStreak(record.PointsEarned, record.PreviousStreak);

        if (record.FlipList != null && record.FlipList.Count > 0)
        {
            // ---> ДОБАВИТЬ ЭТО: Откат прогресса открытых карт <---
            GameQuestTracker.Instance?.SendEvent(QuestActionType.FlipHiddenCards, -record.FlipList.Count);
            // ------------------------------------------------------

            foreach (var slot in record.FlipList)
            {
                if (slot.CurrentCard != null)
                {
                    var data = slot.CurrentCard.GetComponent<CardData>();
                    if (data != null) data.SetFaceUp(false, true);
                }
            }
        }
        UpdateFullUI();
    }

    public void OnUndoAllAction()
    {
        bool isDefeatState = _isGameEnded && !_isGameWon;
        if (!IsInputAllowed && !isDefeatState) return;
        if (_undoStack.Count == 0) return;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("UI_Back");
        // Если игра была проиграна, мы ее "воскрешаем"
        if (_isGameEnded)
        {
            _isGameEnded = false;
            _isInputAllowed = true; // <--- ДОБАВЛЕНО: Возвращаем управление и включаем HUD-кнопки
            isTimerRunning = true;
        }

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.RegisterMove();

        // ---> ДОБАВИТЬ СЮДА <---
        GameQuestTracker.Instance?.RecordUndoUsed();
        // -----------------------

        UpdateFullUI();

        UndoAllImmediate();
    }
    private void UndoAllImmediate()
    {
        while (_undoStack.Count > 0)
        {
            TriPeaksMoveRecord record = _undoStack.Pop();
            ApplyUndoLogic(record);

            CardController card = record.MovedCard;
            card.StopAllCoroutines(); // Жестко прерываем любые полеты
            pileManager.Waste.RemoveCard(card);

            if (record.IsFromStock)
            {
                var cd = card.GetComponent<CardData>();
                if (cd != null) cd.SetFaceUp(false, true); // true = мгновенный переворот без анимации

                pileManager.Stock.AddCard(card);
            }
            else
            {
                TriPeaksTableauPile targetSlot = record.SourcePile;
                targetSlot.AddCard(card); // AddCard внутри себя мгновенно сбрасывает координаты в ноль
            }
        }

        // Останавливаем сдвиг стопки, если он еще анимировался
        if (animationService != null && _stockShiftCoroutine != null)
        {
            StopCoroutine(_stockShiftCoroutine);
        }

        // Мгновенно расставляем карты в стоке с правильными отступами
        pileManager.Stock.UpdateVisuals();

        CardController top = pileManager.Waste.TopCard;
        if (top != null) _logicTopCardModel = top.cardModel;
    }
    

    private bool CheckMatch(CardModel a, CardModel b)
    {
        int r1 = a.rank;
        int r2 = b.rank;
        if (Mathf.Abs(r1 - r2) == 1) return true;
        if ((r1 == 13 && r2 == 1) || (r1 == 1 && r2 == 13)) return true;
        return false;
    }

    public void CheckGameState()
    {
        if (pileManager.TableauPiles.All(p => !p.HasCard))
        {
            StartCoroutine(RoundWonRoutine());
            return;
        }

        if (pileManager.Stock.IsEmpty)
        {
            bool anyMovePossible = false;

            foreach (var slot in pileManager.TableauPiles)
            {
                if (slot.HasCard && !slot.IsBlocked())
                {
                    if (CheckMatch(slot.CurrentCard.cardModel, _logicTopCardModel))
                    {
                        anyMovePossible = true;
                        break;
                    }
                }
            }

            if (!anyMovePossible)
            {
                StartCoroutine(GameLostRoutine());
            }
        }
    }

    private IEnumerator GameLostRoutine()
    {
        if (_isGameEnded) yield break;

        _isInputAllowed = false;
        _isGameEnded = true;
        isTimerRunning = false;

        yield return new WaitForSeconds(1.0f);

        // УБРАНО: StatisticsManager.Instance.OnGameAbandoned();
        // Мы НЕ завершаем сессию статистики здесь! Игрок еще может нажать "Отмена" и продолжить игру.
        // Сессия закроется только если он реально выйдет в меню или нажмет рестарт.

        if (gameUI) gameUI.OnGameLost();
    }

    private IEnumerator RoundWonRoutine()
    {
        _isInputAllowed = false;
        isTimerRunning = false;

        if (scoreManager) scoreManager.AddScore(1000 * currentRound);
        UpdateFullUI();

        yield return new WaitForSeconds(0.5f);

        if (currentRound < totalRounds)
        {
            if (animationService != null)
            {
                yield return StartCoroutine(animationService.AnimateRoundClear(pileManager, rootCanvas, 0.1f));
            }

            currentRound++;
            _isSetupRunning = true;
            _isUndoing = false;
            _undoStack.Clear();
            pileManager.ClearAll();

            // Переход на следующий раунд теперь использует общую функцию!
            StartCoroutine(SetupRoundRoutine(false));
        }
        else
        {
            int finalMoves = 0;
            if (StatisticsManager.Instance != null)
                finalMoves = StatisticsManager.Instance.GetCurrentMoves();

            _isGameEnded = true;
            _isGameWon = true;

            if (StatisticsManager.Instance != null)
            {
                try
                {
                    StatisticsManager.Instance.OnGameWon(scoreManager ? scoreManager.CurrentScore : 0);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[TriPeaks] Ignored stats error: " + e.Message);
                }
            }

            // ---> ДОБАВИТЬ ЭТОТ БЛОК <---
            if (GameQuestTracker.Instance != null && pileManager != null)
            {
                int remainingCards = pileManager.Stock.Count;
                GameQuestTracker.Instance.SendEvent(QuestActionType.WinWithRemainingStock, remainingCards);
            }
            // -----------------------------

            if (gameUI) gameUI.OnGameWon(finalMoves);
        }
    }

    public ICardContainer FindNearestContainer(CardController c, Vector2 p, float d) => null;
    public bool OnDropToBoard(CardController c, Vector2 p) => false;
    public void OnCardLongPressed(CardController c) { }
    public void OnCardDroppedToContainer(CardController c, ICardContainer t) { }
    public void OnKeyboardPick(CardController c) { }
    public void OnCardDoubleClicked(CardController c) { OnCardClicked(c); }
}