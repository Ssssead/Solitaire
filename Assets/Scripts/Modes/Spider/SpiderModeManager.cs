using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using TMPro;

public class SpiderModeManager : MonoBehaviour, ICardGameMode
{
    [Header("Managers")]
    public SpiderPileManager pileManager;
    public PileManager corePileManager;
    public SpiderDeckManager deckManager;
    public DragManager dragManager;
    public UndoManager undoManager;
    public AnimationService animationService;
    public GameUIController gameUI;

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

        int suits = GameSettings.SpiderSuitCount;
        if (suits == 0) suits = 1;

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

        deckManager.CreateAndDeal(suits, diff);
        UpdateTableauLayouts();

        UpdateFullUI();
    }

    public void OnMoveMade()
    {
        // --- ЗАЩИТА ОТ ФАНТОМНЫХ ХОДОВ ВО ВРЕМЯ АНИМАЦИЙ И ПОСЛЕ ПОБЕДЫ ---
        if (_isGameEnded || !IsInputAllowed) return;

        if (!_hasGameStarted)
        {
            _hasGameStarted = true;
            isTimerRunning = true;

            if (StatisticsManager.Instance != null)
            {
                int suits = GameSettings.SpiderSuitCount;
                if (suits == 0) suits = 1;
                Difficulty diff = GameSettings.CurrentDifficulty;
                string variant = $"{suits}Suit" + (suits > 1 ? "s" : "");

                StatisticsManager.Instance.OnGameStarted("Spider", diff, variant);
            }
        }

        if (_scoreManager) _scoreManager.ApplyPenalty();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();

        UpdateFullUI();
    }

    public void OnStockClicked()
    {
        if (_isGameEnded || !IsInputAllowed) return;

        OnMoveMade();
        UpdateFullUI();
    }

    public void OnUndoAction()
    {
        // Если игра не начата, или закончилась победой, или заблокирован ввод — отменяем действие
        if (!_hasGameStarted || _isGameEnded || !IsInputAllowed) return;

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();

        IsInputAllowed = true;

        if (_defeatManager != null) _defeatManager.OnUndo();
        if (_scoreManager != null)
        {
            _scoreManager.ApplyPenalty();
        }
        StopCoroutine("DelayedTableauUpdate");
        StartCoroutine("DelayedTableauUpdate");

        UpdateFullUI();
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

    public void OnCardDoubleClicked(CardController card) { }

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

        if (_scoreManager) _scoreManager.ResetScore();
        if (_defeatManager != null) _defeatManager.OnUndo();

        deckManager.RestartGame();
        UpdateTableauLayouts();

        UpdateFullUI();
    }
}