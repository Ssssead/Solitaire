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
    public TMP_Text moveLimitText;
    public GameUIController gameUI;

    private bool isGameWon = false;

    // --- НОВОЕ: Флаг, началась ли игра реально (был ли ход) ---
    private bool hasGameStarted = false;


    [Header("FreeCell Specific")]
    public Transform freeCellSlotsParent;
    private List<FreeCellPile> freeCells = new List<FreeCellPile>();

    [Header("Rules")]
    public float tableauVerticalGap = 35f;

    // По умолчанию Medium, но будет перезаписано из GameSettings
    private Difficulty currentDifficulty = Difficulty.Medium;
    private int currentSeed = 0;
    public GameType GameType => GameType.FreeCell;
    private int _cachedLimit = -1;

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

        int currentLimit = GetMaxDragSequenceSize();

        if (currentLimit != _cachedLimit)
        {
            _cachedLimit = currentLimit;
            if (moveLimitText != null)
            {
                moveLimitText.text = $"{_cachedLimit}";
            }
        }
    }

    // Вызывается из Меню принудительно (если используется)
    public void InitializeMode(Difficulty difficulty, int seed)
    {
        currentDifficulty = difficulty;
        currentSeed = seed;

        if (pileManager != null)
        {
            RestartGame();
        }
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

        // Синхронизация с глобальными настройками
        currentDifficulty = GameSettings.CurrentDifficulty;

        StartNewGame();
    }

    private System.Collections.IEnumerator LateInitialize()
    {
        yield return new WaitForEndOfFrame();
        Initialize();
    }

    // --- ИСПРАВЛЕНИЕ: Обработка выхода со сцены ---
    private void OnDestroy()
    {
        // Если игра была начата (сделан ход), но не выиграна -> Поражение
        if (hasGameStarted && !isGameWon)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }
    }
    // ----------------------------------------------

    public void RestartGame()
    {
        // Если рестартим активную игру -> засчитываем поражение предыдущей
        if (hasGameStarted && !isGameWon)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }

        isGameWon = false;
        hasGameStarted = false; // Сброс флага
        IsInputAllowed = true;

        pileManager.ClearAllPiles();
        foreach (var fc in freeCells)
        {
            foreach (Transform child in fc.transform) Destroy(child.gameObject);
        }

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
        hasGameStarted = false; // Сброс флага
        IsInputAllowed = true;

        // ВАЖНО: Мы НЕ вызываем OnGameStarted здесь. Ждем первого хода.

        // --- ИНТЕГРАЦИЯ КЭША ---
        bool dealLoadedFromCache = false;

        if (DealCacheSystem.Instance != null)
        {
            Deal cachedDeal = DealCacheSystem.Instance.GetDeal(GameType.FreeCell, currentDifficulty, currentSeed);

            if (cachedDeal != null)
            {
                Debug.Log($"[FreeCell] Loaded deal from Cache! (Diff: {currentDifficulty})");
                ApplyDeal(cachedDeal);
                dealLoadedFromCache = true;
            }
        }

        // Если кэша нет — генерируем новый расклад
        if (!dealLoadedFromCache)
        {
            Debug.Log($"[FreeCell] Cache miss. Generating new deal... (Diff: {currentDifficulty})");
            var generator = GetComponent<FreeCellGenerator>() ?? gameObject.AddComponent<FreeCellGenerator>();
            StartCoroutine(generator.GenerateDeal(currentDifficulty, currentSeed, (deal, m) => ApplyDeal(deal)));
        }
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

    public int GetMaxDragSequenceSize()
    {
        int emptyFC = 0;
        foreach (var fc in pileManager.FreeCells)
        {
            if (fc.IsEmpty) emptyFC++;
        }

        int emptyCols = 0;
        foreach (var tab in pileManager.Tableau)
        {
            if (tab.cards.Count == 0) emptyCols++;
        }

        int rawLimit = (1 + emptyFC) * (int)Mathf.Pow(2, emptyCols);
        return Mathf.Min(rawLimit, 13);
    }

    public void CheckGameState()
    {
        if (isGameWon) return;

        int totalCardsInFoundation = 0;
        foreach (var f in pileManager.Foundations)
        {
            totalCardsInFoundation += f.Count;
        }

        if (totalCardsInFoundation == 52)
        {
            Debug.Log("Victory!");
            isGameWon = true;
            IsInputAllowed = false;
            StartCoroutine(VictorySequence());
            return;
        }

        if (!HasAnyValidMove())
        {
            Debug.Log("Defeat! No moves left.");
            // Здесь можно вызывать поражение, но в FreeCell часто дают игроку самому нажать Restart/Undo
            // Если вы хотите авто-поражение:
            // isGameWon = true; // Блокируем ввод
            // if (gameUI) gameUI.OnGameLost();
        }
    }

    private IEnumerator VictorySequence()
    {
        isGameWon = true;
        IsInputAllowed = false; // Блокируем ввод

        yield return new WaitForSeconds(1.0f);

        // 1. ЗАПОМИНАЕМ ХОДЫ ПЕРЕД СБРОСОМ
        int finalMoves = 0;
        if (StatisticsManager.Instance != null)
        {
            finalMoves = StatisticsManager.Instance.GetCurrentMoves();
        }

        // 2. ОТПРАВЛЯЕМ ПОБЕДУ В СТАТИСТИКУ (счетчик сбросится в 0)
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameWon(CurrentScore);
        }

        // 3. ПОКАЗЫВАЕМ UI С СОХРАНЕННЫМИ ХОДАМИ
        if (gameUI != null)
        {
            gameUI.OnGameWon(finalMoves);
        }
        else
        {
            var foundUI = FindObjectOfType<GameUIController>();
            if (foundUI != null) foundUI.OnGameWon(finalMoves);
        }
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        OnMoveMade(); // Любое перетаскивание - это ход
        CheckGameState();
    }

    // --- ИСПРАВЛЕНИЕ: Логика первого хода ---
    public void OnMoveMade()
    {
        // Если это первый ход - регистрируем начало игры
        if (!hasGameStarted)
        {
            hasGameStarted = true;
            if (StatisticsManager.Instance != null)
            {
                StatisticsManager.Instance.OnGameStarted("FreeCell", currentDifficulty, "Standard");
            }
        }

        // Регистрируем сам ход (счетчик ходов)
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.RegisterMove();
        }
    }

    public void OnUndoAction()
    {
        // Undo тоже считается действием
        OnMoveMade();

        isGameWon = false;
        IsInputAllowed = true;

        var scoreMgr = GetComponent<FreeCellScoreManager>();
        if (scoreMgr != null) scoreMgr.OnUndo();

        UpdateMoveLimitUI();
    }

    // --- Остальные методы ---
    public void OnStockClicked() { }
    public void OnCardDoubleClicked(CardController card) { }
    public void OnCardClicked(CardController card) { }
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