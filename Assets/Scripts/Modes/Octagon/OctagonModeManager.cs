using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static KlondikeModeManager;

public class OctagonModeManager : MonoBehaviour, ICardGameMode, IModeManager
{
    [Header("Managers")]
    [SerializeField] private OctagonDeckManager deckManager;
    [SerializeField] private OctagonPileManager pileManager;
    [SerializeField] private UndoManager undoManager;
    [SerializeField] private GameUIController gameUI;

    [Header("Setup")]
    [SerializeField] private RectTransform dragLayer;
    [SerializeField] private Canvas rootCanvas;

    public RectTransform DragLayer => dragLayer;
    public AnimationService AnimationService => null;
    public PileManager PileManager => null;
    public AutoMoveService AutoMoveService => null;
    public Canvas RootCanvas => rootCanvas;
    public float TableauVerticalGap => 25f;
    public StockDealMode StockDealMode => StockDealMode.Draw1;
    public bool IsInputAllowed { get; set; } = false;
    public string GameName => "Octagon";

    private Difficulty requestDifficulty = Difficulty.Easy;
    private int requestParam = 0;

    private IEnumerator Start()
    {
        if (undoManager == null) undoManager = FindObjectOfType<UndoManager>();
        if (deckManager == null) deckManager = GetComponent<OctagonDeckManager>();

        if (undoManager != null) undoManager.Initialize(this);

        yield return null;

        if (DealCacheSystem.Instance != null)
        {
            Deal deal = null;
            int retries = 0;
            while (deal == null && retries < 10)
            {
                deal = DealCacheSystem.Instance.GetDeal(GameType.Octagon, requestDifficulty, requestParam);
                if (deal == null) { yield return new WaitForSeconds(0.1f); retries++; }
            }
            if (deal != null) deckManager.ApplyDeal(deal);
        }
    }

    // --- IModeManager: FindNearestContainer ---
    public ICardContainer FindNearestContainer(CardController card, Vector2 screenPos, float maxDistance)
    {
        Camera cam = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : rootCanvas.worldCamera;
        Vector2 pointer = Input.mousePosition;

        // 1. Foundation
        foreach (var f in pileManager.FoundationPiles)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(f.transform as RectTransform, pointer, cam))
            {
                if (f.CanAccept(card)) return f;
            }
        }

        // 2. Tableau Slots
        foreach (var group in pileManager.TableauGroups)
        {
            foreach (var slot in group.Slots)
            {
                // Проверяем попадание в карту, если она есть, или в слот
                RectTransform targetRect = slot.transform as RectTransform;
                var top = slot.GetTopCard();
                if (top != null) targetRect = top.rectTransform;

                if (RectTransformUtility.RectangleContainsScreenPoint(targetRect, pointer, cam))
                {
                    if (slot.CanAccept(card)) return slot;
                }
            }
        }
        return null;
    }

    public bool OnDropToBoard(CardController card, Vector2 pos) => false;

    // ВЫЗЫВАЕТСЯ ПОСЛЕ ПЕРЕМЕЩЕНИЯ КАРТЫ
    public void OnCardDroppedToContainer(CardController card, ICardContainer container)
    {
        // 1. Принудительно обновляем состояние ВСЕХ групп
        // Это гарантирует, что если карта ушла из группы, следующая откроется
        foreach (var group in pileManager.TableauGroups)
        {
            group.UpdateTopCardState();
        }

        // 2. Проверяем авто-пополнение и победу
        CheckAutoActions();
        CheckGameState();
    }

    private void CheckAutoActions()
    {
        foreach (var group in pileManager.TableauGroups)
        {
            if (group.IsEmpty())
            {
                if (pileManager.StockPile.CardCount > 0)
                {
                    RefillGroup(group);
                }
            }
        }
    }

    private void RefillGroup(OctagonTableauGroup group)
    {
        var cards = pileManager.StockPile.DrawCards(5);
        int count = cards.Count;
        int maxSlots = group.Slots.Count;

        for (int i = 0; i < count; i++)
        {
            int slotIndex = (maxSlots - 1) - i; // 4, 3, 2, 1, 0

            if (slotIndex >= 0 && slotIndex < maxSlots)
            {
                var card = cards[i];
                var slot = group.Slots[slotIndex];

                bool isTopOne = (i == count - 1); // Только последняя (Slot 0) открыта

                var data = card.GetComponent<CardData>();
                if (data != null) data.SetFaceUp(isTopOne, true);

                var cg = card.GetComponent<CanvasGroup>();
                if (cg) cg.blocksRaycasts = true;

                slot.AcceptCard(card);
            }
        }
    }

    // --- Gameplay & UI ---
    public void OnStockClicked()
    {
        if (pileManager.StockPile.CardCount > 0)
        {
            var list = pileManager.StockPile.DrawCards(1);
            if (list.Count > 0) pileManager.WastePile.AddCard(list[0]);
        }
        else
        {
            if (pileManager.WastePile.transform.childCount == 0) return;
            var cards = new List<CardController>(pileManager.WastePile.GetComponentsInChildren<CardController>());
            for (int i = cards.Count - 1; i >= 0; i--) pileManager.StockPile.AddCard(cards[i]);
            pileManager.WastePile.UpdateLayout();
        }
    }

    public void CheckGameState()
    {
        int kings = 0;
        foreach (var f in pileManager.FoundationPiles)
        {
            var top = f.GetTopCard();
            if (top != null && top.cardModel.rank == 13) kings++;
        }
        if (kings == 8)
        {
            Debug.Log("VICTORY!");
            if (gameUI) gameUI.OnGameWon(0);
        }
    }

    public void OnUndoAction() { }
    public void RestartGame() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void OnCardClicked(CardController c) { }
    public void OnCardDoubleClicked(CardController c)
    {
        // Автоход в Foundation
        foreach (var f in pileManager.FoundationPiles)
        {
            if (f.CanAccept(c))
            {
                f.AcceptCard(c);
                OnCardDroppedToContainer(c, f);
                return;
            }
        }
    }
    public void OnCardLongPressed(CardController c) { }
    public void OnKeyboardPick(CardController c) { }
}