using UnityEngine;
using System.Collections.Generic;

public class YukonDeckManager : MonoBehaviour
{
    public CardFactory cardFactory;

    [Header("Prefabs")]
    [SerializeField] private YukonCardController cardPrefab;

    private YukonModeManager modeManager;
    private List<CardController> allCards = new List<CardController>();

    private void Awake()
    {
        modeManager = GetComponentInParent<YukonModeManager>();
        if (cardFactory == null) cardFactory = FindObjectOfType<CardFactory>();
    }

    public void LoadDeal(Deal deal)
    {
        ClearBoard();

        var tableaus = modeManager.tableaus;

        if (tableaus == null || tableaus.Count == 0)
        {
            Debug.LogError("YukonDeckManager: Tableaus not found in ModeManager!");
            return;
        }

        // Раздача (7 колонок в Юконе)
        for (int i = 0; i < deal.tableau.Count; i++)
        {
            if (i >= tableaus.Count) break;

            YukonTableauPile pile = tableaus[i];
            List<CardInstance> columnData = deal.tableau[i];

            foreach (CardInstance data in columnData)
            {
                CardController newCard = CreateCard(data.Card);

                // --- ВАЖНО ---
                // Устанавливаем свойство IsFaceUp ДО добавления в стопку
                if (newCard is YukonCardController ycc)
                {
                    ycc.SetFaceUp(data.FaceUp);
                }

                // Добавляем карту в стопку
                pile.AcceptCard(newCard);
            }

            pile.StartLayoutAnimationPublic();
        }
    }

    private YukonCardController CreateCard(CardModel model)
    {
        // --- ИСПРАВЛЕНИЕ МАСШТАБА ---
        // Создаем карту сразу внутри Canvas (rootCanvas.transform), чтобы масштаб был (1,1,1)
        // 'false' означает "не сохранять мировые координаты", т.е. сбросить их относительно UI
        var newCard = Instantiate(cardPrefab, modeManager.RootCanvas.transform, false);
        // ----------------------------

        newCard.name = $"Card_{model.suit}_{model.rank}";

        // Настройка зависимостей
        newCard.cardModel = model;
        newCard.CardmodeManager = modeManager;
        newCard.canvas = modeManager.RootCanvas;

        // Настройка визуала
        var data = newCard.GetComponent<CardData>();
        if (data != null && cardFactory.spriteDb != null)
        {
            Sprite face = null;
            foreach (var e in cardFactory.spriteDb.entries)
                if (e.suit == model.suit && e.rank == model.rank) { face = e.sprite; break; }

            data.backSprite = cardFactory.spriteDb.backSprite;
            data.SetModel(model, face);

            if (data.image != null) data.image.sprite = data.backSprite;
        }

        // Включаем Raycasts
        if (newCard.canvasGroup != null)
        {
            newCard.canvasGroup.blocksRaycasts = true;
            newCard.canvasGroup.interactable = true;
        }

        allCards.Add(newCard);
        return newCard;
    }

    public void ClearBoard()
    {
        foreach (var c in allCards) if (c != null) Destroy(c.gameObject);
        allCards.Clear();

        if (modeManager.tableaus != null)
        {
            foreach (var t in modeManager.tableaus) t.Clear();
        }

        if (modeManager.foundations != null)
        {
            foreach (var f in modeManager.foundations)
            {
                // Очистка детей Foundation (так как метода Clear нет в FoundationPile)
                for (int i = f.transform.childCount - 1; i >= 0; i--) Destroy(f.transform.GetChild(i).gameObject);
            }
        }
    }
}