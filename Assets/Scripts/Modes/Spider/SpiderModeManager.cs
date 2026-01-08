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
    // Время считается внутри StatisticsManager, но для UI можно возвращать 0 или брать из менеджера
    public float GameTime => 0f;

    public SpiderScoreManager ScoreManager => _scoreManager;

    void Start()
    {
        InitializeGame();
    }

    public void InitializeGame()
    {
        _isGameEnded = false;
        ActiveFoundationAnimations = 0;

        // --- ИСПРАВЛЕНИЕ: Объявляем переменные В НАЧАЛЕ метода ---
        int suits = GameSettings.SpiderSuitCount;
        if (suits == 0) suits = 1;

        Difficulty diff = GameSettings.CurrentDifficulty;
        // --------------------------------------------------------

        if (corePileManager == null)
            corePileManager = GetComponent<PileManager>() ?? gameObject.AddComponent<PileManager>();
        SyncPileManager();

        _defeatManager = GetComponent<SpiderDefeatManager>();
        if (_defeatManager == null) _defeatManager = gameObject.AddComponent<SpiderDefeatManager>();
        _defeatManager.Initialize(pileManager, gameUI, this);

        _scoreManager = GetComponent<SpiderScoreManager>();
        if (_scoreManager == null) _scoreManager = gameObject.AddComponent<SpiderScoreManager>();
        _scoreManager.ResetScore();

        // Статистика
        if (StatisticsManager.Instance != null)
        {
            // Используем уже объявленные переменные suits и diff
            string variant = $"{suits}Suit" + (suits > 1 ? "s" : "");

            // Передаем правильные данные
            StatisticsManager.Instance.OnGameStarted("Spider", diff, variant);
        }

        if (dragManager != null)
        {
            dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);
            dragManager.RegisterAllContainers(pileManager.GetAllContainers());
        }

        if (undoManager != null) undoManager.Initialize(this);

        // --- ТЕПЕРЬ ПЕРЕМЕННЫЕ ВИДНЫ И ЗДЕСЬ ---
        deckManager.CreateAndDeal(suits, diff);
        UpdateTableauLayouts();
    }

    // ... (Методы OnMoveMade, OnStockClicked, OnUndoAction, OnRowCompleted, CheckGameState - ОСТАЮТСЯ БЕЗ ИЗМЕНЕНИЙ) ...
    public void OnMoveMade()
    {
        if (_scoreManager) _scoreManager.ApplyPenalty();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
    }

    public void OnStockClicked()
    {
        if (_scoreManager) _scoreManager.ApplyPenalty();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
    }

    public void OnUndoAction()
    {
        _isGameEnded = false;
        IsInputAllowed = true;
        if (_defeatManager != null) _defeatManager.OnUndo();
        if (_scoreManager) _scoreManager.ApplyPenalty();
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.RegisterMove();
       UpdateTableauLayouts();
    }

    public void OnRowCompleted()
    {
        if (_scoreManager) _scoreManager.AddRowBonus();
    }

    public void UpdateTableauLayouts()
    {
        if (pileManager == null) return;

        // 1. СТОК (10-й слот)
        bool hasStockCards = (pileManager.StockPile != null && pileManager.StockPile.cards.Count > 0);
        SetPileCompressed(9, hasStockCards);

        // 2. FOUNDATIONS
        int filledFoundations = 0;
        if (pileManager.FoundationPiles != null)
        {
            foreach (var f in pileManager.FoundationPiles)
            {
                if (f.IsFull) filledFoundations++;
            }
        }

        // --- ИСПРАВЛЕНИЕ: Добавляем ряды, которые сейчас летят ---
        // Это заставит стопки сжаться сразу, как только ряд собран, не дожидаясь конца полета
        filledFoundations += ActiveFoundationAnimations;
        // --------------------------------------------------------

        // Логика сжатия (как в ТЗ)
        bool compressSlot1 = filledFoundations >= 1;
        bool compressSlot2 = filledFoundations >= 2;
        bool compressSlot3 = filledFoundations >= 6;

        SetPileCompressed(0, compressSlot1);
        SetPileCompressed(1, compressSlot2);
        SetPileCompressed(2, compressSlot3);

        // Остальные по дефолту
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
    private void OnDestroy()
    {
        // Если объект уничтожается (уходим в меню), а игра еще идет и не выиграна
        if (!_isGameEnded && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }
    }
    private IEnumerator VictoryRoutine()
    {
        // --- ИСПРАВЛЕНИЕ ОШИБКИ 2: Передаем int (счет) ---
        // Так как StatisticsManager уже знает, что мы играем в Spider (из OnGameStarted),
        // ему нужен только итоговый СЧЕТ.
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameWon(CurrentScore);
        }
        // -------------------------------------------------

        yield return new WaitForSeconds(1.0f);

        if (gameUI != null) gameUI.OnGameWon();
    }

    // Служебные методы (SyncPileManager, GetNextEmptyFoundation, RestartGame и т.д.)
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
        // --- ДОБАВИТЬ ЭТО ---
        // Если рестартим игру, которая не была закончена победой - это поражение
        if (!_isGameEnded && StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.OnGameAbandoned();
        }
        // --------------------

        _isGameEnded = false;
        IsInputAllowed = true;
        ActiveFoundationAnimations = 0;

        // Сразу запускаем новую сессию (таймер сбросится внутри OnGameStarted)
        if (StatisticsManager.Instance != null)
        {
            // Повторяем логику старта, чтобы создать новую запись
            // (Код формирования ключей такой же, как в InitializeGame)
            int suits = GameSettings.SpiderSuitCount;
            Difficulty diff = Difficulty.Easy;
            if (suits == 2) diff = Difficulty.Medium;
            if (suits == 4) diff = Difficulty.Hard;
            string variant = $"{suits}Suit" + (suits > 1 ? "s" : "");

            StatisticsManager.Instance.OnGameStarted("Spider", diff, variant);
        }

        if (_scoreManager) _scoreManager.ResetScore();
        if (_defeatManager != null) _defeatManager.OnUndo();
        deckManager.RestartGame();
        UpdateTableauLayouts();
    }
}