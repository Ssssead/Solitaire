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

    [Header("FreeCell Specific")]
    public Transform freeCellSlotsParent; // Родитель слотов для FreeCellPile
    private List<FreeCellPile> freeCells = new List<FreeCellPile>();

    [Header("Rules")]
    public float tableauVerticalGap = 35f;

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
    public AutoMoveService AutoMoveService => null; // Пока null, можно добавить позже
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => tableauVerticalGap;
    public StockDealMode StockDealMode => StockDealMode.Draw1; // Заглушка



    private void Start()
    {
        StartCoroutine(LateInitialize());
    }

    private void Update()
    {
        // Каждые кадр проверяем актуальный лимит
        // Это очень дешевая операция (проход по 12 элементам), 
        // зато интерфейс всегда будет показывать правду.

        if (pileManager == null) return;

        int currentLimit = GetMaxDragSequenceSize();

        // Если лимит изменился по сравнению с прошлым кадром - обновляем текст
        if (currentLimit != _cachedLimit)
        {
            _cachedLimit = currentLimit;
            if (moveLimitText != null)
            {
                moveLimitText.text = $"{_cachedLimit}";
            }
        }
    }



    private void Initialize()
    {
        // 1. Сначала настраиваем PileManager и ЗАСТАВЛЯЕМ его найти стопки прямо сейчас
        if (pileManager == null) pileManager = GetComponent<FreeCellPileManager>();

        // ВАЖНО: Этот метод собирает FreeCells и Tableau в списки
        pileManager.InitializeFreeCell(this);

        // 2. Инициализируем UndoManager (ему нужны ссылки на PileManager)
        if (undoManager != null) undoManager.Initialize(this);

        // 3. И только ТЕПЕРЬ настраиваем DragManager
        if (dragManager != null)
        {
            // ИСПРАВЛЕНО: Передаем dragLayer (третий параметр)
            dragManager.Initialize(this, rootCanvas, dragLayer, undoManager);

            // Регистрируем контейнеры
            var containers = pileManager.GetAllContainers();
            dragManager.RegisterAllContainers(containers);
        }

        // 4. Запускаем игру
        StartNewGame();
        
    }
    private System.Collections.IEnumerator LateInitialize()
    {
        // Ждем конца кадра, чтобы все объекты точно прогрузились
        yield return new WaitForEndOfFrame();

        Initialize();

        // Обновляем текст лимита еще раз
       
    }
    public void RestartGame()
    {
        isGameWon = false; // <-- СБРОС
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

        // Выводим текст. Например: "Max Drag: 5"
        moveLimitText.text = $"{limit}";
    }
    private void StartNewGame()
    {
        isGameWon = false;      // <-- СБРОС ФЛАГА
        IsInputAllowed = true;  // <-- РАЗБЛОКИРОВКА ВВОДА

        if (StatisticsManager.Instance != null)
        {
            // "FreeCell" - имя игры
            // Difficulty.Medium - сложность (если у вас есть выбор, передавайте переменную)
            // "Standard" - вариант правил (во FreeCell он обычно один)
            StatisticsManager.Instance.OnGameStarted("FreeCell", Difficulty.Medium, "Standard");
        }

        // ... ваш код генерации ...
        var generator = GetComponent<FreeCellGenerator>() ?? gameObject.AddComponent<FreeCellGenerator>();
        StartCoroutine(generator.GenerateDeal(Difficulty.Medium, 0, (deal, m) => ApplyDeal(deal)));
    }

    private void ApplyDeal(Deal deal)
    {
        // Раздаем карты
        for (int i = 0; i < 8; i++)
        {
            if (i >= deal.tableau.Count) break;

            // ВАЖНО: PileManager должен найти наши FreeCellTableauPile, 
            // так как они наследуются от TableauPile.
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

        if (StatisticsManager.Instance != null)
            StatisticsManager.Instance.OnGameStarted("FreeCell", Difficulty.Medium, "Standard");
    }

    // --- Super Move Logic ---
    /// <summary>
    /// Считает лимит карт для переноса: (1 + пустые_ячейки) * 2 ^ (пустые_столбцы)
    /// </summary>
    public int GetMaxDragSequenceSize()
    {
        int emptyFC = 0;

        // Считаем пустые ячейки
        foreach (var fc in pileManager.FreeCells)
        {
            if (fc.IsEmpty) emptyFC++;
        }

        // Считаем пустые столбцы
        int emptyCols = 0;
        foreach (var tab in pileManager.Tableau)
        {
            if (tab.cards.Count == 0) emptyCols++;
        }

        // Считаем "сырой" математический лимит
        int rawLimit = (1 + emptyFC) * (int)Mathf.Pow(2, emptyCols);

        // ОГРАНИЧЕНИЕ: В колоде всего 13 карт одной масти (K..A).
        // Нет смысла разрешать тащить больше 13, даже если пустых мест много.
        return Mathf.Min(rawLimit, 13);
    }

    // --- Game Mode Logic ---
    public void CheckGameState()
    {
        // 1. ПРОВЕРКА ПОБЕДЫ
        if (isGameWon) return; // Если уже выиграли, не проверяем

        int totalCardsInFoundation = 0;
        foreach (var f in pileManager.Foundations)
        {
            // Используем Count, так как мы уже настроили логическое добавление
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

        // 2. ПРОВЕРКА ПОРАЖЕНИЯ (НЕТ ХОДОВ)
        // Проверяем только если игра не выиграна
        if (!HasAnyValidMove())
        {
            Debug.Log("Defeat! No moves left.");
            isGameWon = true; // Блокируем дальнейшие проверки (используем тот же флаг или новый isGameLost)
            IsInputAllowed = false; // Блокируем ввод

            StartCoroutine(DefeatSequence());
        }
    }

    private System.Collections.IEnumerator DefeatSequence()
    {
        yield return new WaitForSeconds(1.0f);

        if (gameUI != null)
        {
            // Убедитесь, что в GameUIController есть метод OnGameLost
            gameUI.OnGameLost();
        }
        else
        {
            // Фолбэк, если UI не привязан
            var ui = FindObjectOfType<GameUIController>();
            if (ui) ui.OnGameLost();
        }
    }

    /// <summary>
    /// Проверяет, существует ли хоть один легальный ход.
    /// </summary>
    private bool HasAnyValidMove()
    {
        // Собираем список всех карт, которыми можно походить.
        // Это верхние карты столбцов и карты в ячейках.
        List<CardController> movableCards = new List<CardController>();

        // 1. Верхние карты Табло
        foreach (var tab in pileManager.Tableau)
        {
            if (tab.cards.Count > 0)
                movableCards.Add(tab.cards[tab.cards.Count - 1]);
        }

        // 2. Карты в FreeCells
        int emptyFreeCells = 0;
        foreach (var fc in pileManager.FreeCells)
        {
            if (fc.IsEmpty) emptyFreeCells++;
            else movableCards.Add(fc.GetComponentInChildren<CardController>());
        }

        // ТЕПЕРЬ ПРОВЕРЯЕМ КУДА ОНИ МОГУТ ПОЙТИ
        foreach (var card in movableCards)
        {
            if (card == null) continue;

            // А. Можно ли в Foundation?
            foreach (var f in pileManager.Foundations)
            {
                if (f.CanAccept(card)) return true; // Есть ход!
            }

            // Б. Можно ли в другое Tableau?
            foreach (var t in pileManager.Tableau)
            {
                // Не проверяем перенос в ту же самую стопку
                if (card.transform.parent == t.transform) continue;

                if (t.CanAccept(card)) return true; // Есть ход!
            }

            // В. Можно ли в пустой FreeCell?
            // Только если карта сейчас НЕ в FreeCell (перекладывать из ячейки в ячейку бессмысленно для спасения)
            bool isAlreadyInCell = card.transform.parent.GetComponent<FreeCellPile>() != null;
            if (!isAlreadyInCell && emptyFreeCells > 0)
            {
                return true; // Есть ход!
            }
        }

        // Если прошли все карты и ничего не нашли — ходов нет.
        return false;
    }
    private System.Collections.IEnumerator VictorySequence()
    {
        // Ждем секунду для красоты
        yield return new WaitForSeconds(1.0f);

        // 1. СНАЧАЛА СОХРАНЯЕМ СТАТИСТИКУ
        // Это важно сделать ДО показа UI, чтобы UI мог считать готовые данные (XP, время)
        if (StatisticsManager.Instance != null)
        {
            // Передаем текущий счет в менеджер статистики
            StatisticsManager.Instance.OnGameWon(CurrentScore);
        }

        // 2. ТЕПЕРЬ ПОКАЗЫВАЕМ UI
        if (gameUI != null)
        {
            gameUI.OnGameWon();
        }
        else
        {
            // Запасной вариант
            var foundUI = FindObjectOfType<GameUIController>();
            if (foundUI != null) foundUI.OnGameWon();
        }
    }

    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        // 1. Регистрируем, что ход состоялся (статистика + таймер)
        OnMoveMade();
        CheckGameState();
    }
    public void OnMoveMade()
    {
        // 1. Регистрируем ход в статистике (время, кол-во ходов)
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.RegisterMove();
        }

        // 2. Если нужно, применяем штраф к очкам (если логика не в ScoreManager.OnCardMove)
        // Но у вас очки считаются отдельно в OnCardMove, так что тут можно оставить пусто
        // или перенести логику штрафа сюда, если захотите.
    }
    // IModeManager методы
    public void OnUndoAction()
    {
        // ... ваш код (сброс флагов победы и т.д.) ...
        isGameWon = false;
        IsInputAllowed = true;

        var scoreMgr = GetComponent<FreeCellScoreManager>();
        if (scoreMgr != null) scoreMgr.OnUndo();

        // Добавляем регистрацию хода при отмене (если вы хотите считать Undo за "действие")
        if (StatisticsManager.Instance != null)
        {
            StatisticsManager.Instance.RegisterMove();
        }

        UpdateMoveLimitUI();
    }
    public void OnStockClicked() { }
    public void OnCardDoubleClicked(CardController card) { } // Авто-мув можно добавить тут
    public void OnCardClicked(CardController card) { }
    public void OnCardLongPressed(CardController card) { }
    public void OnKeyboardPick(CardController card) { }
    public bool OnDropToBoard(CardController card, Vector2 pos)
    {
        // Этот метод вызывается, если DragManager НЕ нашел контейнер (например, Raycast не сработал).
        // Мы вручную проверяем: "А может мы все-таки над свободной ячейкой?"

        // 1. Проверяем FreeCells
        foreach (var fc in pileManager.FreeCells)
        {
            // Геометрическая проверка: Попадает ли точка сброса в квадрат ячейки?
            if (IsPointOverRect(fc.transform as RectTransform, pos))
            {
                if (fc.CanAccept(card))
                {
                    // УРА! Мы нашли ячейку вручную.

                    // 1. Кладем карту
                    fc.AcceptCard(card);

                    // 2. Сообщаем об успехе (очки, звуки)
                    OnCardDroppedToContainer(card, fc);

                    // 3. Возвращаем true, чтобы DragManager знал, что мы обработали ситуацию
                    // и НЕ возвращал карту назад.
                    return true;
                }
            }
        }

        // 2. Проверяем Tableau (на всякий случай, если там тоже глючит)
        foreach (var tab in pileManager.Tableau)
        {
            // Проверяем попадание во всю колонку или в нижнюю карту
            RectTransform targetRect = tab.transform as RectTransform;

            // Если в колонке есть карты, лучше проверять попадание в последнюю карту
            if (tab.cards.Count > 0)
            {
                targetRect = tab.cards[tab.cards.Count - 1].rectTransform;
            }

            // Расширяем зону попадания (pad), чтобы было легче попасть
            if (IsPointOverRect(targetRect, pos, padding: 50f))
            {
                if (tab.CanAccept(card))
                {
                    tab.AddCard(card, true); // Добавляем карту
                    tab.StartLayoutAnimationPublic(); // Выравниваем стопку
                    OnCardDroppedToContainer(card, tab);
                    return true;
                }
            }
        }

        // Если ничего не нашли - возвращаем false, и карта вернется назад
        return false;
    }
    private bool IsPointOverRect(RectTransform rect, Vector2 screenPos, float padding = 0f)
    {
        if (rect == null) return false;

        // Получаем мировые углы
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        // Преобразуем в экранные координаты (если Canvas Overlay - это не нужно, но безопасно)
        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

        // Простая проверка через Unity Utility
        // padding можно учесть, увеличив rect, но для простоты используем стандарт
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam);
    }

    public ICardContainer FindNearestContainer(CardController card, Vector2 pos, float maxDist)
    {
        // 1. ГЕОМЕТРИЧЕСКАЯ ПРОВЕРКА ДЛЯ FREE CELLS (Самое надежное)
        // Мы игнорируем Raycast и просто смотрим координаты
        foreach (var fc in pileManager.FreeCells)
        {
            // Проверка: Попадает ли курсор в квадрат слота
            // Используем worldCamera из Canvas, если он ScreenSpace-Camera, или null если Overlay
            if (RectTransformUtility.RectangleContainsScreenPoint(fc.transform as RectTransform, pos, rootCanvas.worldCamera))
            {
                // Если попали в квадрат - проверяем, пуст ли он
                if (fc.CanAccept(card))
                {
                    return fc;
                }
            }
        }

        // 2. ГЕОМЕТРИЯ ДЛЯ TABLEAU (Столбцов)
        // Решает проблему переноса из Cell обратно на стол
        foreach (var tab in pileManager.Tableau)
        {
            // Попадаем ли мы в зону стопки?
            if (RectTransformUtility.RectangleContainsScreenPoint(tab.transform as RectTransform, pos, rootCanvas.worldCamera))
            {
                if (tab.CanAccept(card)) return tab;
            }
        }

        // 3. FOUNDATIONS
        foreach (var f in pileManager.Foundations)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(f.transform as RectTransform, pos, rootCanvas.worldCamera))
            {
                if (f.CanAccept(card)) return f;
            }
        }

        return null;
    }

}