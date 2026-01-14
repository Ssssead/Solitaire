using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting.Antlr3.Runtime.Misc;

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

    private SpiderDefeatManager _defeatManager;
    private SpiderScoreManager _scoreManager;
    private bool _isGameEnded = false;
    
    // --- НОВОЕ: Флаг старта игры ---
    private bool _hasGameStarted = false; 

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

    // Статистика
    public int CurrentScore => _scoreManager != null ? _scoreManager.CurrentScore : 0;
    public int MoveCount => StatisticsManager.Instance != null ? StatisticsManager.Instance.GetCurrentMoves() : 0;
    public float GameTime => 0f;

    public SpiderScoreManager ScoreManager => _scoreManager;

    void Start()
    {
        InitializeGame();
    }

    public void InitializeGame()
    {
        _isGameEnded = false;
        _hasGameStarted = false; // Сброс флага
        ActiveFoundationAnimations = 0;

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

        // ВАЖНО: Убрали вызов StatisticsManager.OnGameStarted отсюда.

        if (dragManager != null)
        {
            dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);
            dragManager.RegisterAllContainers(pileManager.GetAllContainers());
        }

        if (undoManager != null) undoManager.Initialize(this);

        deckManager.CreateAndDeal(suits, diff);
        UpdateTableauLayouts();
    }

    // --- МЕТОД РЕГИСТРАЦИИ ХОДА (Вызывать при действиях) ---
    public void OnMoveMade()
    {
        // 1. Регистрируем старт игры при первом ходе
        if (!_hasGameStarted)
        {
            _hasGameStarted = true;
            
            if (StatisticsManager.Instance != null)
            {
                int suits = GameSettings.SpiderSuitCount;
                if (suits == 0) suits = 1;
                Difficulty diff = GameSettings.CurrentDifficulty;
                string variant = $"{suits}Suit" + (suits > 1 ? "s" : "");

                StatisticsManager.Instance.OnGameStarted("Spider", diff, variant);
            }
        }

        // 2. Регистрируем сам ход
        if (_scoreManager) _scoreManager.ApplyPenalty();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
    }

    public void OnStockClicked()
    {
        // Клик по стоку - это ход
        OnMoveMade();
        if (_scoreManager) _scoreManager.ApplyPenalty();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
    }

    public void OnUndoAction()
    {
        // Undo тоже считается активностью
        if (!_hasGameStarted)
        {
             // Если вдруг нажали Undo до первого хода (теоретически невозможно, но для надежности)
             OnMoveMade(); 
        }
        else
        {
             // Просто регистрируем +1 ход
             if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
        }

        _isGameEnded = false;
        IsInputAllowed = true;
        
        if (_defeatManager != null) _defeatManager.OnUndo();
        if (_scoreManager) _scoreManager.ApplyPenalty();
        
        UpdateTableauLayouts();
    }

    public void OnRowCompleted()
    {
        if (_scoreManager) _scoreManager.AddRowBonus();
        CheckGameState(); // Проверяем победу сразу после сбора ряда
    }

    public void UpdateTableauLayouts()
    {
        if (pileManager == null) return;

        bool hasStockCards = (pileManager.StockPile != null && pileManager.StockPile.cards.Count > 0);
        SetPileCompressed(9, hasStockCards);

        int filledFoundations = 0;
        if (pileManager.FoundationPiles != null)
        {
            foreach (var f in pileManager.FoundationPiles)
            {
                if (f.IsFull) filledFoundations++;
            }
        }

        filledFoundations += ActiveFoundationAnimations;

        bool compressSlot1 = filledFoundations >= 1;
        bool compressSlot2 = filledFoundations >= 2;
        bool compressSlot3 = filledFoundations >= 6;

        SetPileCompressed(0, compressSlot1);
        SetPileCompressed(1, compressSlot2);
        SetPileCompressed(2, compressSlot3);

        for (int i = 3; i <= 8; i++) SetPileCompressed(i, false);
    }

    private void SetPileCompressed(int index, bool compressed)
    {
        if (index >= 0 && index < pileManager.TableauPiles.Count)
        {
            var pile = pileManager.TableauPiles[index].GetComponent<SpiderTableauPile>();
            if (pile != null)
            {
                pile.SetLayoutCompressed(compressed);
            }
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
            IsInputAllowed = false;
            StartCoroutine(VictoryRoutine());
            return;
        }

        if (_defeatManager != null)
        {
            _defeatManager.CheckDefeatCondition();
        }
    }

    // --- НОВОЕ: Обработка выхода со сцены ---
    private void OnDestroy()
    {
        if (_hasGameStarted && !_isGameEnded)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }
    }
    // ----------------------------------------

    private IEnumerator VictoryRoutine()
    {
        _isGameEnded = true;
        IsInputAllowed = false;

        yield return new WaitForSeconds(1.0f);

        // 1. Запоминаем ходы ДО сброса
        int finalMoves = 0;
        if (StatisticsManager.Instance != null)
            finalMoves = StatisticsManager.Instance.GetCurrentMoves();

        // 2. Отправляем в статистику (счетчик сбросится)
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameWon(CurrentScore);
        }

        // 3. Показываем UI с сохраненными ходами
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
            if (!f.IsFull) return f;
        }
        return null;
    }

    public void OnCardDoubleClicked(CardController card) { }

    public void RestartGame()
    {
        // Если рестартим активную игру -> поражение
        if (_hasGameStarted && !_isGameEnded)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }

        _isGameEnded = false;
        _hasGameStarted = false; // Сброс
        IsInputAllowed = true;
        ActiveFoundationAnimations = 0;

        if (_scoreManager) _scoreManager.ResetScore();
        if (_defeatManager != null) _defeatManager.OnUndo();
        
        deckManager.RestartGame();
        UpdateTableauLayouts();
    }
}