// DefeatManager.cs [SCANNER UPDATE]
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefeatManager : MonoBehaviour
{
    private PileManager pileManager;
    private GameUIController gameUI;
    private DeckManager deckManager;

    [Header("Settings")]
    [SerializeField] private float defeatDelay = 0.5f;
    [SerializeField] private bool showDebugInfo = true;

    [Header("Undo Grace")]
    [SerializeField] private float undoGracePeriod = 2.0f;
    private float ignoreChecksUntil = 0f;

    private Coroutine pendingDefeatCoroutine;
    private bool isDefeatPending = false;

    public void Initialize(PileManager pm, GameUIController ui)
    {
        pileManager = pm;
        gameUI = ui;
        deckManager = FindObjectOfType<DeckManager>();
    }

    public void ResetManager()
    {
        StopDefeatTimer();
        ignoreChecksUntil = 0f;
    }

    public void OnUndo()
    {
        StopDefeatTimer();
        ignoreChecksUntil = Time.time + undoGracePeriod;
    }

    /// <summary>
    /// Проверяет, есть ли среди переданных карт хоть одна, которую можно куда-то пристроить.
    /// Используется DeckManager'ом перед рециклом.
    /// </summary>
    public bool CheckIfAnyMovePossibleInList(List<CardController> cardsToCheck)
    {
        if (cardsToCheck == null || cardsToCheck.Count == 0) return false;

        foreach (var card in cardsToCheck)
        {
            // Проверяем Foundation
            if (CanMoveToFoundation(card))
            {
                if (showDebugInfo) Debug.Log($"[DefeatManager] Hidden Move Found in Stock: {card.GetComponent<CardData>().model} -> Foundation");
                return true;
            }

            // Проверяем Tableau
            // isSourceTableau = false, так как эти карты идут из Waste/Stock
            if (CanMoveToTableau(card, isSourceTableau: false, sourcePile: null))
            {
                if (showDebugInfo) Debug.Log($"[DefeatManager] Hidden Move Found in Stock: {card.GetComponent<CardData>().model} -> Tableau");
                return true;
            }
        }
        return false;
    }

    // --- ПУБЛИЧНЫЙ МЕТОД ДЛЯ ПРОВЕРКИ ТЕКУЩЕГО СТОЛА ---
    public bool HasAnyProductiveMoveOnTable()
    {
        return HasAnyProductiveMove();
    }

    public void CheckGameStatus()
    {
        if (pileManager == null || deckManager == null) return;
        if (Time.time < ignoreChecksUntil) return;

        // 1. Stock должен быть пуст
        if (!pileManager.StockPile.IsEmpty())
        {
            StopDefeatTimer();
            return;
        }

        // 2. Лимит переворотов
        if (!deckManager.IsStalemateReached)
        {
            StopDefeatTimer();
            return;
        }

        // 3. Нет ходов
        if (HasAnyProductiveMove())
        {
            if (isDefeatPending) StopDefeatTimer();
            return;
        }

        // 4. Поражение
        if (!isDefeatPending)
        {
            if (showDebugInfo) Debug.Log($"[DefeatManager] STALEMATE REACHED. Defeat in {defeatDelay}s...");
            pendingDefeatCoroutine = StartCoroutine(ShowDefeatRoutine());
            isDefeatPending = true;
        }
    }

    private void StopDefeatTimer()
    {
        if (pendingDefeatCoroutine != null)
        {
            StopCoroutine(pendingDefeatCoroutine);
            pendingDefeatCoroutine = null;
        }
        isDefeatPending = false;
    }

    private IEnumerator ShowDefeatRoutine()
    {
        yield return new WaitForSeconds(defeatDelay);
        if (gameUI != null) gameUI.OnGameLost();
        isDefeatPending = false;
        pendingDefeatCoroutine = null;
    }

    // --- ВНУТРЕННЯЯ ЛОГИКА ---

    private bool HasAnyProductiveMove()
    {
        // 1. Waste (Top Card only)
        if (pileManager.WastePile != null && !pileManager.WastePile.IsEmpty())
        {
            var wasteCard = pileManager.WastePile.GetTopCard();
            if (wasteCard != null)
            {
                if (CanMoveToFoundation(wasteCard)) return true;
                if (CanMoveToTableau(wasteCard, isSourceTableau: false, sourcePile: null)) return true;
            }
        }

        // 2. Tableau
        if (pileManager.Tableau != null)
        {
            foreach (var sourcePile in pileManager.Tableau)
            {
                var topCard = sourcePile.GetTopCard();
                if (topCard != null && CanMoveToFoundation(topCard)) return true;

                var faceUpCards = sourcePile.GetFaceUpCards();
                foreach (var card in faceUpCards)
                {
                    if (CanMoveToTableau(card, isSourceTableau: true, sourcePile: sourcePile)) return true;
                }
            }
        }
        return false;
    }

    private bool CanMoveToFoundation(CardController card)
    {
        if (card == null) return false;
        foreach (var foundation in pileManager.Foundations)
        {
            if (foundation.CanAccept(card)) return true;
        }
        return false;
    }

    private bool CanMoveToTableau(CardController card, bool isSourceTableau, TableauPile sourcePile = null)
    {
        var cardData = card.GetComponent<CardData>();
        if (cardData == null) return false;
        var cardModel = cardData.model;

        foreach (var targetPile in pileManager.Tableau)
        {
            if (card.transform.parent == targetPile.transform) continue;

            if (targetPile.CanAccept(card))
            {
                // Фильтр бесполезных ходов (только для карт со стола)
                if (isSourceTableau)
                {
                    // 1. Король на пустое место (если он уже на дне)
                    if (targetPile.IsEmpty() && cardModel.rank == 13)
                    {
                        if (sourcePile != null && sourcePile.IndexOfCard(card) == 0) continue;
                    }

                    // 2. Шило на мыло
                    if (sourcePile != null && !targetPile.IsEmpty())
                    {
                        int cardIndex = sourcePile.IndexOfCard(card);
                        if (cardIndex > 0)
                        {
                            var cardBelowCtrl = sourcePile.cards[cardIndex - 1];
                            var cardBelowData = cardBelowCtrl.GetComponent<CardData>();

                            // Если под картой ЗАКРЫТАЯ карта - это полезный ход!
                            if (cardBelowData != null && !cardBelowData.IsFaceUp())
                            {
                                return true;
                            }

                            // Если под картой открытая такая же (ранг+цвет) - бесполезно
                            var cardBelowModel = cardBelowData.model;
                            var targetTopModel = targetPile.GetTopCard().GetComponent<CardData>().model;

                            if (cardBelowModel.rank == targetTopModel.rank &&
                                IsSameColorSuit(cardBelowModel.suit, targetTopModel.suit))
                            {
                                continue;
                            }
                        }
                    }
                }
                return true;
            }
        }
        return false;
    }

    private bool IsSameColorSuit(Suit a, Suit b)
    {
        bool aRed = (a == Suit.Diamonds || a == Suit.Hearts);
        bool bRed = (b == Suit.Diamonds || b == Suit.Hearts);
        return aRed == bRed;
    }
}