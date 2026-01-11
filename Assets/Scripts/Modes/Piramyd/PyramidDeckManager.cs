using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PyramidDeckManager : MonoBehaviour
{
    public PyramidModeManager modeManager;
    public PyramidPileManager pileManager;
    public CardFactory cardFactory;
    public PyramidAnimationManager animManager;

    [Header("Tableau & Piles")]
    public List<Transform> rowParents;
    public Transform stockRoot;
    public Transform wasteRoot;

    [Header("Foundations")]
    public Transform leftFoundation;
    public Transform rightFoundation;

    [Header("Visual Settings")]
    public Sprite selectionFrameSprite;
    public Vector2 selectionFrameScale = Vector2.one;
    public float selectionScaleAmount = 1.05f;

    private GameObject highlightObject;

    private void Awake()
    {
        if (!cardFactory) cardFactory = FindObjectOfType<CardFactory>();
        if (!animManager) animManager = FindObjectOfType<PyramidAnimationManager>();

        if (pileManager.Stock == null) pileManager.Stock = stockRoot.gameObject.AddComponent<PyramidStockPile>();
        if (pileManager.Waste == null) pileManager.Waste = wasteRoot.gameObject.AddComponent<PyramidWastePile>();

        pileManager.Initialize(rowParents);
        CreateHighlightObject();
    }

    public List<CardController> InstantiateDeal(Deal deal)
    {
        ClearBoard();
        List<CardController> cardsToAnimate = new List<CardController>();

        // 1. Пирамида
        for (int r = 0; r < deal.tableau.Count; r++)
        {
            var rowData = deal.tableau[r];
            for (int c = 0; c < rowData.Count; c++)
            {
                var instance = rowData[c];
                var slot = pileManager.TableauSlots.Find(s => s.Row == r && s.Col == c);

                if (slot)
                {
                    // Создаем в Stock (визуально)
                    var cardObj = cardFactory.CreateCard(instance.Card, stockRoot, Vector2.zero);

                    cardObj.CardmodeManager = modeManager;
                    cardObj.OnClicked += modeManager.OnCardClicked;

                    var data = cardObj.GetComponent<CardData>();
                    if (data)
                    {
                        data.SetFaceUp(true, true);
                        if (data.image) data.image.color = Color.white; // Белая
                    }

                    slot.Card = cardObj;

                    // Сохраняем цель
                    var info = cardObj.gameObject.AddComponent<CardInfoStorage>();
                    info.LinkedSlot = slot.transform;

                    cardsToAnimate.Add(cardObj);
                }
            }
        }

        // 2. Сток
        var stockList = deal.stock.ToList();
        stockList.Reverse();

        foreach (var instance in stockList)
        {
            var cardObj = cardFactory.CreateCard(instance.Card, stockRoot, Vector2.zero);
            cardObj.CardmodeManager = modeManager;
            cardObj.OnClicked += modeManager.OnStockClicked;

            var data = cardObj.GetComponent<CardData>();
            if (data)
            {
                data.SetFaceUp(instance.FaceUp, false);
                if (data.image) data.image.color = Color.white; // Белая
            }

            pileManager.Stock.Add(cardObj);
        }

        // Замки НЕ обновляем здесь, это сделает AnimationManager в конце раздачи

        return cardsToAnimate;
    }

    // ... [CreateHighlightObject, SetCardHighlight, ClearBoard - как в прошлом ответе] ...
    private void CreateHighlightObject()
    {
        if (selectionFrameSprite == null) return;
        highlightObject = new GameObject("SelectionFrame");
        highlightObject.transform.SetParent(transform);
        Image img = highlightObject.AddComponent<Image>();
        img.sprite = selectionFrameSprite;
        img.raycastTarget = false;
        img.type = Image.Type.Sliced;
        RectTransform rt = highlightObject.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.localScale = new Vector3(selectionFrameScale.x, selectionFrameScale.y, 1f);
        highlightObject.SetActive(false);
    }

    public void SetCardHighlight(CardController card, bool active)
    {
        if (highlightObject == null) return;
        if (active && card != null)
        {
            highlightObject.transform.SetParent(card.transform, false);
            highlightObject.transform.localPosition = Vector3.zero;
            highlightObject.transform.localScale = new Vector3(selectionFrameScale.x, selectionFrameScale.y, 1f);
            highlightObject.transform.SetAsLastSibling();
            highlightObject.SetActive(true);
            card.transform.localScale = Vector3.one * selectionScaleAmount;
        }
        else
        {
            highlightObject.SetActive(false);
            highlightObject.transform.SetParent(transform, false);
            if (card != null) card.transform.localScale = Vector3.one;
        }
    }

    public void ClearBoard()
    {
        if (highlightObject != null) { highlightObject.SetActive(false); highlightObject.transform.SetParent(transform, false); }
        foreach (var slot in pileManager.TableauSlots) { if (slot.Card) Destroy(slot.Card.gameObject); slot.Card = null; }
        foreach (Transform t in stockRoot) Destroy(t.gameObject);
        pileManager.Stock.Clear();
        foreach (Transform t in wasteRoot) Destroy(t.gameObject);
        pileManager.Waste.Clear();
        if (leftFoundation) foreach (Transform t in leftFoundation) Destroy(t.gameObject);
        if (rightFoundation) foreach (Transform t in rightFoundation) Destroy(t.gameObject);
    }
}