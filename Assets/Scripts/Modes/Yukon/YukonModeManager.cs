using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public enum YukonVariant { Classic, Russian }

public class YukonModeManager : MonoBehaviour, IModeManager, ICardGameMode
{
    [Header("Core Setup")]
    public YukonVariant CurrentVariant = YukonVariant.Classic;
    public Difficulty currentDifficulty = Difficulty.Medium;

    [Header("References")]
    public YukonDeckManager deckManager;
    public Canvas rootCanvas;
    public RectTransform dragLayer;
    public ScoreManager scoreManager;
    public UndoManager undoManager;

    [Header("Containers")]
    public Transform tableauSlotsParent;
    public Transform foundationSlotsParent;

    [Header("UI")]
    public Button autoWinButton;

    // ВАЖНО: Список теперь типа YukonTableauPile, но т.к. это наследник, всё ок
    [HideInInspector] public List<YukonTableauPile> tableaus = new List<YukonTableauPile>();
    [HideInInspector] public List<FoundationPile> foundations = new List<FoundationPile>();

    public string GameName => CurrentVariant == YukonVariant.Russian ? "Russian Solitaire" : "Yukon";
    public RectTransform DragLayer => dragLayer;
    public Canvas RootCanvas => rootCanvas;
    public bool IsInputAllowed { get; set; } = true;
    private bool hasGameStarted = false;
    public float TableauVerticalGap => 35f;
    public AnimationService AnimationService => null;
    public PileManager PileManager => null;
    public AutoMoveService AutoMoveService => null;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public GameType GameType => GameType.Yukon;

    private void Start()
    {
        if (undoManager == null) undoManager = FindObjectOfType<UndoManager>();
        if (undoManager != null) undoManager.Initialize(this);
        InitializeMode();
    }
    public bool IsMatchInProgress()
    {
        return hasGameStarted;
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
        IsInputAllowed = false;
        if (scoreManager) scoreManager.ResetScore();
        if (autoWinButton) autoWinButton.gameObject.SetActive(false);
        if (undoManager) undoManager.ResetHistory();

        foreach (var t in tableaus) t.Clear();
        foreach (var f in foundations)
        {
            for (int i = f.transform.childCount - 1; i >= 0; i--) Destroy(f.transform.GetChild(i).gameObject);
        }

        if (DealCacheSystem.Instance)
        {
            var deal = DealCacheSystem.Instance.GetDeal(GameType.Yukon, currentDifficulty, 0);
            if (deal != null)
            {
                deckManager.LoadDeal(deal);
                IsInputAllowed = true;
            }
        }
    }

    public void RestartGame() => StartNewGame();

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
        // 1. Запись в UndoManager
        if (undoManager != null)
        {
            var yCard = card as YukonCardController;
            ICardContainer source = yCard?.SourceContainer;

            if (source != null)
            {
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
        }

        // 2. Логика открытия карт
        foreach (var t in tableaus)
        {
            // Используем метод из базового класса TableauPile
            t.ForceUpdateFromTransform();

            // Проверка на переворот
            if (t.cards.Count > 0)
            {
                // ВАЖНО: Работаем через индекс, чтобы синхронизировать список faceUp
                int topIndex = t.cards.Count - 1;

                // Проверяем список faceUp (он есть в базовом классе)
                if (t.faceUp.Count > topIndex && !t.faceUp[topIndex])
                {
                    // Метод TableauPile, который обновляет и список, и визуал
                    t.CheckAndFlipTop();

                    if (undoManager != null)
                        undoManager.RecordFlipInSource(topIndex);
                }
            }
        }

        CheckGameState();
        if (scoreManager)
        {
            if (container is FoundationPile) scoreManager.OnCardMove(container);
            else scoreManager.OnCardMove(null);
        }
    }

    public void OnCardDoubleClicked(CardController card)
    {
        if (!IsInputAllowed) return;
        if (card.transform.parent != null)
        {
            int myIndex = card.transform.GetSiblingIndex();
            int totalChildren = card.transform.parent.childCount;
            if (myIndex != totalChildren - 1) return;
        }

        foreach (var f in foundations)
        {
            if (f.CanAccept(card))
            {
                if (card is YukonCardController yCard)
                {
                    var source = card.transform.parent.GetComponent<ICardContainer>();
                    yCard.SetSourceForAutoMove(source);
                    yCard.AnimateToTarget(f, f.transform.position);
                }
                break;
            }
        }
    }

    public void OnUndoAction()
    {
        if (autoWinButton != null) autoWinButton.gameObject.SetActive(false);
        if (scoreManager != null) scoreManager.OnUndo();
    }

    private Rect GetWorldRect(RectTransform rt) { Vector3[] c = new Vector3[4]; rt.GetWorldCorners(c); return new Rect(c[0].x, c[0].y, Mathf.Abs(c[2].x - c[0].x), Mathf.Abs(c[2].y - c[0].y)); }
    private float GetIntersectionArea(Rect r1, Rect r2) { float w = Mathf.Min(r1.xMax, r2.xMax) - Mathf.Max(r1.xMin, r2.xMin); float h = Mathf.Min(r1.yMax, r2.yMax) - Mathf.Max(r1.yMin, r2.yMin); return (w > 0 && h > 0) ? w * h : 0f; }

    public void CheckGameState()
    {
        int win = foundations.Count(f => f.Count == 13);
        if (win == 4)
        {
            Debug.Log("Yukon Won!");
            if (autoWinButton) autoWinButton.gameObject.SetActive(false);
        }
    }

    public bool OnDropToBoard(CardController c, Vector2 p) => false;
    public void OnCardClicked(CardController c) { }
    public void OnCardLongPressed(CardController c) { }
    public void OnKeyboardPick(CardController c) { }
    public void OnStockClicked() { }
}