using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FreeCellModeManager : MonoBehaviour, ICardGameMode, IModeManager
{
    [Header("Core References")]
    public FreeCellPileManager pileManager;
    public DragManager dragManager;
    public UndoManager undoManager;
    public AnimationService animationService;
    public CardFactory cardFactory;
    public Canvas rootCanvas;
    public RectTransform dragLayer;

    public GameUIController gameUI;
    public FreeCellIntroController introController;
    public FreeCellDeckManager deckManager;
    private bool isRestarting = false;
    private bool isGameWon = false;

    private bool hasGameStarted = false;

    [Header("UI & HUD")]
    [Tooltip("Лимит карт для перемещения (SuperMove)")]
    public TMP_Text moveLimitText;
    [Tooltip("Текст для отображения количества ходов")]
    public TMP_Text movesText;
    [Tooltip("Текст для отображения очков")]
    public TMP_Text scoreText;
    [Tooltip("Текст для отображения времени")]
    public TMP_Text timeText;
    private float gameTimer = 0f;
    private bool isTimerRunning = false;
    [HideInInspector] public bool JustFailedDueToLimit = false;

    [Header("FreeCell Specific")]
    public Transform freeCellSlotsParent;
    private List<FreeCellPile> freeCells = new List<FreeCellPile>();

    [Header("Rules")]
    public float tableauVerticalGap = 35f;

    private Difficulty currentDifficulty = Difficulty.Medium;
    private int currentSeed = 0;
    public GameType GameType => GameType.FreeCell;
    private int _cachedLimit = -1;

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

    private void Start()
    {
        StartCoroutine(LateInitialize());
    }

    public bool IsMatchInProgress()
    {
        return hasGameStarted;
    }

    private void Update()
    {
        if (pileManager == null) return;

        // 1. Обновление SuperMove лимита
        // --- ИСПРАВЛЕНИЕ АНИМАЦИИ: Жестко фиксируем лимит на 5 до начала игры ---
        if (!IsInputAllowed && !hasGameStarted)
        {
            if (_cachedLimit != 5)
            {
                _cachedLimit = 5;
                if (moveLimitText != null) moveLimitText.text = "5";
            }
        }
        else
        {
            int currentLimit = GetMaxDragSequenceSize();
            if (currentLimit != _cachedLimit)
            {
                _cachedLimit = currentLimit;
                if (moveLimitText != null) moveLimitText.text = $"{_cachedLimit}";
            }
        }

        // 2. Обновление Таймера и HUD
        if (IsInputAllowed && !isGameWon)
        {
            if (hasGameStarted && !isTimerRunning) isTimerRunning = true;
            if (isTimerRunning) gameTimer += Time.deltaTime;
            UpdateHUD();
        }
    }

    private void UpdateHUD()
    {
        if (movesText != null && StatisticsManager.Instance != null)
            movesText.text = $"{StatisticsManager.Instance.GetCurrentMoves()}";

        if (timeText != null)
        {
            int timeInSeconds = Mathf.FloorToInt(gameTimer);
            int minutes = timeInSeconds / 60;
            int seconds = timeInSeconds % 60;
            timeText.text = $"{minutes:0}:{seconds:00}";
        }

        if (scoreText != null) scoreText.text = $"{CurrentScore}";
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
        }
    }

    public void RestartGame()
    {
        isGameWon = false;
        IsInputAllowed = false;
        isRestarting = true;

        pileManager.ClearAllPiles();
        foreach (var fc in freeCells) foreach (Transform child in fc.transform) Destroy(child.gameObject);

        StartNewGame();
        UpdateMoveLimitUI();
    }

    public void UpdateMoveLimitUI()
    {
        if (moveLimitText == null) return;
        int limit = GetMaxDragSequenceSize();
        moveLimitText.text = $"{limit}";
    }

    private void StartNewGame()
    {
        isGameWon = false;
        IsInputAllowed = false;
        currentDifficulty = GameSettings.CurrentDifficulty;

        hasGameStarted = false;
        isTimerRunning = false;
        gameTimer = 0f;

        var scoreMgr = GetComponent<FreeCellScoreManager>();
        if (scoreMgr != null) scoreMgr.ResetScore();

        // --- ИСПРАВЛЕНИЕ ТАЙМЕРА: Используем ClearHistory вместо ResetHistory ---
        // Это очистит логи Undo, но не вызовет OnUndoAction() и не стартанет таймер
        if (undoManager != null) undoManager.ClearHistory();
        // ------------------------------------------------------------------------

        UpdateHUD();

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameStarted("FreeCell", currentDifficulty, "Standard");

        if (introController != null) introController.PrepareIntro(isRestarting);

        bool dealLoadedFromCache = false;

        if (DealCacheSystem.Instance != null)
        {
            Deal cachedDeal = DealCacheSystem.Instance.GetDeal(GameType.FreeCell, currentDifficulty, currentSeed);
            if (cachedDeal != null)
            {
                dealLoadedFromCache = true;
                StartCoroutine(introController.PlayIntroSequence(cachedDeal, isRestarting));
            }
        }

        if (!dealLoadedFromCache)
        {
            var generator = GetComponent<FreeCellGenerator>() ?? gameObject.AddComponent<FreeCellGenerator>();
            StartCoroutine(generator.GenerateDeal(currentDifficulty, currentSeed, (deal, m) =>
            {
                StartCoroutine(introController.PlayIntroSequence(deal, isRestarting));
            }));
        }

        isRestarting = false;
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
        if (moveLimitText != null && moveLimitText.transform.parent != null)
        {
            RectTransform target = moveLimitText.transform.parent.GetComponent<RectTransform>();
            if (target != null) StartCoroutine(ShakeRoutine(target));
        }
    }

    private IEnumerator ShakeRoutine(RectTransform target)
    {
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
    }
    private IEnumerator LossSequence()
    {
        yield return new WaitForSeconds(1);
        if (gameUI != null) gameUI.OnGameLost();
        
    }
    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        OnMoveMade();
        CheckGameState();
    }

    public void OnMoveMade()
    {
        hasGameStarted = true;
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
    }

    public void OnUndoAction()
    {
        OnMoveMade();
        isGameWon = false;
        IsInputAllowed = true;

        var scoreMgr = GetComponent<FreeCellScoreManager>();
        if (scoreMgr != null) scoreMgr.OnUndo();

        UpdateMoveLimitUI();
    }

    public void OnStockClicked() { }
    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed || isGameWon || card == null) return;

        ICardContainer sourceContainer = card.GetComponentInParent<ICardContainer>();
        if (sourceContainer == null || sourceContainer is FoundationPile) return;

        // Проверяем, что карта свободна (верхняя в стопке)
        if (sourceContainer is TableauPile tab)
        {
            if (tab.cards.Count == 0 || tab.cards[tab.cards.Count - 1] != card)
            {
                StartCoroutine(ShakeCardRoutine(card.rectTransform));
                return;
            }
        }

        bool moved = TryDoubleClickMove(card, sourceContainer);

        if (moved)
        {
            OnMoveMade();
            UpdateMoveLimitUI();
            Invoke(nameof(CheckGameState), 0.3f); // Проверка победы после того как карта долетит
        }
        else
        {
            // Если нет вариантов куда перенести - трясем саму карту
            StartCoroutine(ShakeCardRoutine(card.rectTransform));
        }
    }
    private bool TryDoubleClickMove(CardController card, ICardContainer source)
    {
        // 1. Приоритет: перемещение в Foundation (Дом)
        foreach (var f in pileManager.Foundations)
        {
            if (f.CanAccept(card))
            {
                ExecuteProgrammaticMove(card, source, f);
                return true;
            }
        }

        // 2. Вторичный приоритет: перемещение в пустой FreeCell (если мы не берем из него же)
        if (!(source is FreeCellPile))
        {
            foreach (var fc in pileManager.FreeCells)
            {
                if (fc.CanAccept(card))
                {
                    ExecuteProgrammaticMove(card, source, fc);
                    return true;
                }
            }
        }

        return false;
    }
    private void ExecuteProgrammaticMove(CardController card, ICardContainer source, ICardContainer target)
    {
        Transform prevParent = card.transform.parent;
        int prevSib = card.transform.GetSiblingIndex();

        // --- ИСПРАВЛЕНИЕ 3: Запоминаем РЕАЛЬНУЮ локальную позицию до старта полета ---
        Vector3 prevPos = card.transform.localPosition;

        // Логическое удаление из источника
        if (source is TableauPile tab)
        {
            int idx = tab.IndexOfCard(card);
            if (idx != -1) tab.RemoveSequenceFrom(idx);
        }

        // Мгновенно резервируем слот в Foundation, чтобы предотвратить наложение
        if (target is FoundationPile found) found.ReserveCard(card);

        // Запуск полета. В конце анимации ForceSnapToContainer сама вызовет AcceptCard у таргета
        card.ForceSnapToContainer(target);

        // Запись Undo
        if (undoManager != null)
        {
            undoManager.RecordMove(
                new List<CardController> { card },
                source, target,
                new List<Transform> { prevParent },
                new List<Vector3> { prevPos }, // Передаем реальную позицию вместо Vector3.zero!
                new List<int> { prevSib }
            );
        }

        // Очки
        var scoreMgr = GetComponent<FreeCellScoreManager>();
        if (scoreMgr != null) scoreMgr.OnCardMove(source, target);
    }

    // Анимация тряски для конкретной карты с затуханием амплитуды
    private IEnumerator ShakeCardRoutine(RectTransform target)
    {
        if (target == null) yield break;

        Vector3 originalPos = target.anchoredPosition;
        float elapsed = 0f;
        float duration = 0.25f;
        float magnitude = 10f; // Амплитуда тряски карты

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float phase = 1f - (elapsed / duration); // Плавное затухание
            float xOffset = Mathf.Sin(elapsed * 50f) * magnitude * phase;

            target.anchoredPosition = new Vector3(originalPos.x + xOffset, originalPos.y, originalPos.z);
            yield return null;
        }

        if (target != null) target.anchoredPosition = originalPos;
    }
    public void OnCardClicked(CardController card) { hasGameStarted = true; }
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
                    fc.AcceptCard(card);
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
                    tab.AddCard(card, true);
                    tab.StartLayoutAnimationPublic();
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
        foreach (var fc in pileManager.FreeCells)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(fc.transform as RectTransform, pos, rootCanvas.worldCamera))
            {
                if (fc.CanAccept(card)) return fc;
            }
        }

        foreach (var tab in pileManager.Tableau)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(tab.transform as RectTransform, pos, rootCanvas.worldCamera))
            {
                if (tab.CanAccept(card)) return tab;
            }
        }

        foreach (var f in pileManager.Foundations)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(f.transform as RectTransform, pos, rootCanvas.worldCamera))
            {
                if (f.CanAccept(card)) return f;
            }
        }

        return null;
    }

    private bool HasAnyValidMove()
    {
        List<CardController> movableCards = new List<CardController>();

        foreach (var tab in pileManager.Tableau)
        {
            if (tab.cards.Count > 0)
                movableCards.Add(tab.cards[tab.cards.Count - 1]);
        }

        int emptyFreeCells = 0;
        foreach (var fc in pileManager.FreeCells)
        {
            if (fc.IsEmpty) emptyFreeCells++;
            else movableCards.Add(fc.GetComponentInChildren<CardController>());
        }

        foreach (var card in movableCards)
        {
            if (card == null) continue;

            foreach (var f in pileManager.Foundations)
            {
                if (f.CanAccept(card)) return true;
            }

            foreach (var t in pileManager.Tableau)
            {
                if (card.transform.parent == t.transform) continue;
                if (t.CanAccept(card)) return true;
            }

            bool isAlreadyInCell = card.transform.parent.GetComponent<FreeCellPile>() != null;
            if (!isAlreadyInCell && emptyFreeCells > 0)
            {
                return true;
            }
        }
        return false;
    }
}