using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TriPeaksModeManager : MonoBehaviour, IModeManager, ICardGameMode
{
    [Header("Core References")]
    public CardFactory cardFactory;
    public Canvas rootCanvas;
    public RectTransform dragLayer;
    public GameUIController gameUI;

    [Header("Services")]
    public TriPeaksPileManager pileManager;
    public TriPeaksScoreManager scoreManager;
    public UndoManager undoManager;
    public AnimationService animationService;
    public DragManager dragManager;

    [Header("Settings")]
    public float moveSpeed = 10f;

    // Настройки сложности и раундов
    private Difficulty currentDifficulty = Difficulty.Easy;
    private int currentRound = 1;
    private int totalRounds = 1;

    // --- State ---
    private bool _isInputAllowed = true;
    private bool _hasGameStarted = false;
    private bool _isGameWon = false;

    // --- ICardGameMode Implementation ---
    public string GameName => "TriPeaks";
    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => animationService;
    public PileManager PileManager => null; // Используем специфичный pileManager
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 0f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;

    public bool IsInputAllowed
    {
        get => _isInputAllowed;
        set => _isInputAllowed = value;
    }

    private void Start()
    {
        // Инициализация при старте сцены, читаем настройки из MenuController/GameSettings
        InitializeMode();
    }

    public void InitializeMode()
    {
        // Читаем глобальные настройки
        currentDifficulty = GameSettings.CurrentDifficulty;
        totalRounds = GameSettings.RoundsCount;
        if (totalRounds < 1) totalRounds = 1;

        currentRound = 1;

        RestartGameInternal(true);
    }

    public void RestartGame()
    {
        // Если перезапуск из меню паузы - сбрасываем всё
        if (_hasGameStarted && !_isGameWon)
        {
            if (StatisticsManager.Instance != null)
                StatisticsManager.Instance.OnGameAbandoned();
        }

        // Перечитываем настройки (на случай изменения)
        currentDifficulty = GameSettings.CurrentDifficulty;
        totalRounds = GameSettings.RoundsCount;
        currentRound = 1;

        RestartGameInternal(true);
    }

    private void RestartGameInternal(bool fullReset)
    {
        StopAllCoroutines();
        _hasGameStarted = false;
        _isInputAllowed = true;
        _isGameWon = false;

        if (fullReset)
        {
            if (scoreManager != null) scoreManager.ResetScore();
        }

        // Очистка стола
        if (pileManager != null) pileManager.ClearAll();
        if (undoManager != null) undoManager.ResetHistory();

        StartCoroutine(SetupRoundRoutine());
    }

    private IEnumerator SetupRoundRoutine()
    {
        // Проверка критических ссылок
        if (pileManager == null || pileManager.TableauPiles == null || pileManager.TableauPiles.Count == 0)
        {
            Debug.LogError("TriPeaks: TableauPiles are missing or empty in PileManager!");
            yield break;
        }

        Deal deal = null;

        // 1. Получение расклада
        if (DealCacheSystem.Instance != null)
        {
            // Используем totalRounds или currentRound как параметр, если генератор это учитывает.
            // В вашем коде был параметр 1. Обычно передается кол-во раундов или сложность.
            // Для совместимости с Pyramid передадим totalRounds.
            int requestParam = totalRounds;

            Debug.Log($"TriPeaks: Requesting deal -> Diff: {currentDifficulty}, Rounds: {requestParam}");

            float timeout = 3f;
            while (deal == null && timeout > 0)
            {
                deal = DealCacheSystem.Instance.GetDeal(GameType.TriPeaks, currentDifficulty, requestParam);
                if (deal == null) yield return null;
                timeout -= Time.deltaTime;
            }
        }
        else
        {
            Debug.LogError("DealCacheSystem not found!");
            yield break;
        }

        if (deal == null)
        {
            Debug.LogError("TriPeaks: Failed to get deal from cache.");
            yield break;
        }

        // 2. Расстановка карт по Табло
        int cardsCreated = 0;
        int tableauIndex = 0;

        foreach (var cardList in deal.tableau)
        {
            if (tableauIndex >= pileManager.TableauPiles.Count) break;
            if (cardList == null || cardList.Count == 0)
            {
                tableauIndex++;
                continue;
            }

            CardInstance instance = cardList[0];
            TriPeaksTableauPile slot = pileManager.TableauPiles[tableauIndex];

            CardController card = cardFactory.CreateCard(instance.Card, slot.transform, Vector2.zero);
            if (card == null) continue;

            slot.AddCard(card);
            cardsCreated++;

            // Логика FaceUp: карта открыта, если её никто не перекрывает
            bool shouldBeFaceUp = !slot.IsBlocked();
            card.transform.localRotation = shouldBeFaceUp ? Quaternion.identity : Quaternion.Euler(0, 180, 0);

            card.OnClicked += OnCardClicked;
            tableauIndex++;
        }

        // 3. Заполнение Стока
        foreach (var instance in deal.stock)
        {
            CardController card = cardFactory.CreateCard(instance.Card, pileManager.Stock.transform, Vector2.zero);
            card.transform.localRotation = Quaternion.Euler(0, 180, 0); // FaceDown
            pileManager.Stock.AddCard(card);
            card.gameObject.SetActive(false);
            card.OnClicked += OnCardClicked;
        }

        pileManager.Stock.UpdateVisuals();
        yield return null;

        // 4. Первый ход (карта из стока в отбой)
        // Важно: не записываем это в Undo, это стартовое состояние
        DrawFromStock(false);

        // Статистика (только если это первый раунд)
        if (currentRound == 1)
        {
            if (StatisticsManager.Instance)
                StatisticsManager.Instance.OnGameStarted("TriPeaks", currentDifficulty, $"{totalRounds}Rounds");
        }
    }

    // --- Геймплей ---

    public void OnCardClicked(CardController card)
    {
        if (!_isInputAllowed) return;

        // Фиксация старта игры (если вдруг статистика не запустилась)
        if (!_hasGameStarted) _hasGameStarted = true;

        // Клик по стоку
        if (pileManager.Stock.Contains(card))
        {
            OnStockClicked();
            return;
        }

        // Клик по карте на поле
        TriPeaksTableauPile slot = pileManager.FindSlotWithCard(card);
        if (slot != null)
        {
            if (slot.IsBlocked()) return;

            CardController wasteTop = pileManager.Waste.TopCard;
            if (wasteTop != null && CheckMatch(card.cardModel, wasteTop.cardModel))
            {
                MoveToWaste(card, slot, pileManager.Waste);
            }
        }
    }

    public void OnStockClicked()
    {
        if (!_isInputAllowed) return;
        DrawFromStock(true);
    }

    private void DrawFromStock(bool recordUndo)
    {
        CardController card = pileManager.Stock.DrawCard();
        if (card == null) return;

        MoveToWaste(card, pileManager.Stock, pileManager.Waste, recordUndo);

        // При взятии из колоды стрик сбрасывается
        if (scoreManager) scoreManager.ResetStreak();
        pileManager.Stock.UpdateVisuals();
    }

    private void MoveToWaste(CardController card, ICardContainer source, ICardContainer target, bool recordUndo = true)
    {
        // Подготовка данных для Undo
        List<CardController> movingCards = new List<CardController> { card };
        List<Transform> oldParents = new List<Transform> { card.transform.parent };
        List<Vector3> oldPos = new List<Vector3> { card.transform.position };
        List<int> oldSiblings = new List<int> { card.transform.GetSiblingIndex() };

        // Логическое перемещение
        if (source is TriPeaksTableauPile tSource) tSource.RemoveCard(card);
        else if (source is TriPeaksStockPile sSource) sSource.RemoveCard(card);

        if (target is TriPeaksWastePile wTarget) wTarget.AddCard(card);

        // Визуальное перемещение
        card.gameObject.SetActive(true);
        card.transform.SetParent(target.Transform);
        card.transform.SetAsLastSibling();

        if (animationService)
            StartCoroutine(RotateCardRoutine(card, true));
        else
            card.transform.localRotation = Quaternion.identity;

        // Если карта ушла с поля - начисляем очки и обновляем состояние
        if (source is TriPeaksTableauPile)
        {
            if (scoreManager) scoreManager.AddStreakScore();
            UpdateTableauFaces(); // Открываем освободившиеся карты
        }

        // Запись Undo и Статистики
        if (recordUndo && undoManager)
        {
            if (StatisticsManager.Instance) StatisticsManager.Instance.RegisterMove();

            undoManager.RecordMove(
                movingCards,
                source,
                target,
                oldParents,
                oldPos,
                oldSiblings,
                "Move",
                false // false = не объединять с предыдущим ходом
            );
        }

        CheckGameState();
    }

    private void UpdateTableauFaces()
    {
        foreach (var slot in pileManager.TableauPiles)
        {
            if (slot.HasCard && !slot.IsBlocked())
            {
                // Если карта лежит рубашкой вверх (FaceDown), переворачиваем её
                // FaceDown = 180 по Y. FaceUp = 0.
                if (Mathf.Abs(slot.CurrentCard.transform.localEulerAngles.y - 180) < 10f)
                {
                    StartCoroutine(RotateCardRoutine(slot.CurrentCard, true));
                }
            }
        }
    }

    private bool CheckMatch(CardModel a, CardModel b)
    {
        int r1 = a.rank;
        int r2 = b.rank;
        // Разница 1 (например 5 и 6) или Король(13)-Туз(1)
        if (Mathf.Abs(r1 - r2) == 1) return true;
        if ((r1 == 13 && r2 == 1) || (r1 == 1 && r2 == 13)) return true;
        return false;
    }

    public void CheckGameState()
    {
        // Победа в РАУНДЕ: все карты с поля убраны
        if (pileManager.TableauPiles.All(p => !p.HasCard))
        {
            StartCoroutine(RoundWonRoutine());
        }
        // Поражение: карт в стоке нет и ходов нет
        else if (pileManager.Stock.IsEmpty)
        {
            // Здесь можно добавить проверку на наличие возможных ходов
            // Но в простом варианте часто ждут действий игрока (Undo или Restart)
        }
    }

    private IEnumerator RoundWonRoutine()
    {
        _isInputAllowed = false;

        // Бонус за раунд
        if (scoreManager) scoreManager.AddScore(1000 * currentRound); // Пример бонуса

        yield return new WaitForSeconds(1.0f);

        if (currentRound < totalRounds)
        {
            // Переход к следующему раунду
            currentRound++;
            // Очищаем стол и раздаем заново, сохраняя счет
            RestartGameInternal(false);
        }
        else
        {
            // Полная победа
            _isGameWon = true;
            if (StatisticsManager.Instance) StatisticsManager.Instance.OnGameWon(scoreManager ? scoreManager.CurrentScore : 0);
            if (gameUI) gameUI.OnGameWon();
        }
    }

    public void OnUndoAction()
    {
        // При Undo сбрасываем стрик
        if (scoreManager) scoreManager.CancelStreak();

        // Восстанавливаем состояние "заблокированных" карт (закрываем их обратно)
        foreach (var slot in pileManager.TableauPiles)
        {
            if (slot.HasCard && slot.IsBlocked())
            {
                slot.CurrentCard.transform.localRotation = Quaternion.Euler(0, 180, 0);
            }
        }
    }

    // --- Заглушки ---
    public ICardContainer FindNearestContainer(CardController c, Vector2 p, float d) => null;
    public bool OnDropToBoard(CardController c, Vector2 p) => false;
    public void OnCardLongPressed(CardController c) { }
    public void OnCardDroppedToContainer(CardController c, ICardContainer t) { }
    public void OnKeyboardPick(CardController c) { }
    public void OnCardDoubleClicked(CardController c) { OnCardClicked(c); }

    private IEnumerator RotateCardRoutine(CardController card, bool faceUp)
    {
        float t = 0;
        Quaternion start = card.transform.localRotation;
        Quaternion end = faceUp ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
        while (t < 1)
        {
            t += Time.deltaTime * moveSpeed; // Используем moveSpeed
            card.transform.localRotation = Quaternion.Lerp(start, end, t);
            yield return null;
        }
        card.transform.localRotation = end;
    }
}