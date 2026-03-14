using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MontanaDeckManager : MonoBehaviour
{
    public MontanaModeManager modeManager;
    public MontanaPileManager pileManager;
    public CardFactory cardFactory;

    [Header("Intro Settings")]
    public MontanaIntroController introController;

    public void Initialize(MontanaModeManager mode, CardFactory factory, MontanaPileManager piles)
    {
        modeManager = mode;
        cardFactory = factory;
        pileManager = piles;
    }

    public void DealInitial()
    {
        StartCoroutine(DealRoutine());
    }

    private IEnumerator DealRoutine()
    {
        modeManager.IsInputAllowed = false;
        pileManager.ClearAllPiles();

        if (DealCacheSystem.Instance == null) yield break;
        while (!DealCacheSystem.Instance.IsReady) yield return null;

        int param = modeManager.IsHardMode ? 1 : 0;
        Difficulty diff = GameSettings.CurrentDifficulty;

        Deal deal = DealCacheSystem.Instance.GetDeal(modeManager.GameType, diff, param);
        if (deal == null) yield break;

        // --- ИЗМЕНЕНИЯ ЗДЕСЬ: Передаем флаг isRestarting ---
        if (introController != null)
        {
            introController.PrepareIntro(modeManager.isRestarting);
            yield return StartCoroutine(introController.PlayIntroSequence(modeManager.isRestarting));
        }

        // 2. ВЛЕТ СТОПКИ В SLOT0
        MontanaSlot slot0 = pileManager.GetSlot(0, 0);
        Vector3 targetStackPos = slot0.Transform.position;

        // --- ИСПРАВЛЕНИЕ 1: Учитываем масштаб Canvas, чтобы отступ был правильным на любых экранах ---
        float canvasScale = modeManager.rootCanvas.transform.localScale.x;
        Vector3 startStackPos = targetStackPos + new Vector3(-1500f * canvasScale, 0, 0);

        List<CardController> cardsToDeal = new List<CardController>();
        List<int> targetSlotIndices = new List<int>();

        for (int i = 0; i < 56; i++)
        {
            if (deal.tableau[i].Count > 0)
            {
                var inst = deal.tableau[i][0];
                Vector3 cardOffset = new Vector3(-i * 1f * canvasScale, 0, 0);

                // --- ИСПРАВЛЕНИЕ 2: Создаем карты СРАЗУ за экраном, чтобы не было "вспышки" по центру ---
                var card = cardFactory.CreateCard(inst.Card, modeManager.DragLayer, Vector2.zero);
                card.transform.position = startStackPos + cardOffset;

                card.GetComponent<CardData>()?.SetFaceUp(true, false);

                cardsToDeal.Add(card);
                targetSlotIndices.Add(i);
            }
        }

        // --- ИСПРАВЛЕНИЕ 3: Увеличиваем время полета (с 0.55 до 0.85 секунд) ---
        float flyDuration = 0.85f;
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flyDuration;
            // Используем формулу SmoothStep для плавного разгона и красивого торможения в конце
            float easedT = t * t * (3f - 2f * t);

            for (int i = 0; i < cardsToDeal.Count; i++)
            {
                Vector3 cardOffset = new Vector3(i * 1f * canvasScale, 0, 0);
                cardsToDeal[i].transform.position = Vector3.Lerp(startStackPos + cardOffset, targetStackPos + cardOffset, easedT);
            }
            yield return null;
        }

        // Жесткая фиксация в slot0
        for (int i = 0; i < cardsToDeal.Count; i++)
        {
            cardsToDeal[i].transform.position = targetStackPos + new Vector3(i * 1f * canvasScale, 0, 0);
        }

        yield return new WaitForSeconds(0.15f); // Короткая пауза перед разлетом

        // 3. РАЗЛЕТ КАРТ В СВОИ СЛОТЫ (начиная с 56/55 слота)
        float dealSpeed = 0.02f; // Задержка (в сек) между вылетом следующей карты
        float cardMoveDuration = 0.22f; // Время полета одной карты

        // Перебираем массив с конца (снимаем верхние карты стопки)
        for (int i = cardsToDeal.Count - 1; i >= 0; i--)
        {
            var card = cardsToDeal[i];
            int slotIndex = targetSlotIndices[i];
            var targetSlot = pileManager.Slots[slotIndex];

            // Карта для slot0 уже на месте, просто закрепляем её
            if (slotIndex == 0)
            {
                targetSlot.AcceptCard(card);
                modeManager.RegisterCardEvents(card);
            }
            else
            {
                StartCoroutine(MoveCardToSlotRoutine(card, targetSlot, cardMoveDuration));
                yield return new WaitForSeconds(dealSpeed);
            }
        }

        // Ждем, пока последняя вылетевшая карта долетит до своего места
        yield return new WaitForSeconds(cardMoveDuration);
        modeManager.SaveInitialState();
        modeManager.IsInputAllowed = true;
        modeManager.isRestarting = false;
        modeManager.CheckGameState();
    }

    private IEnumerator MoveCardToSlotRoutine(CardController card, MontanaSlot targetSlot, float duration)
    {
        Vector3 startPos = card.transform.position;
        Vector3 endPos = targetSlot.Transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = t * t * (3f - 2f * t);

            card.transform.position = Vector3.Lerp(startPos, endPos, easedT);
            yield return null;
        }

        card.transform.position = endPos;
        targetSlot.AcceptCard(card);

        // Снимаем статус анимации, чтобы карту снова можно было брать
        card.GetComponent<MontanaCardController>()?.SetAnimating(false);
        modeManager.RegisterCardEvents(card);
    }

    // --- Метод ReshuffleRoutine оставляем без изменений (из прошлого шага) ---
    public IEnumerator ReshuffleRoutine()
    {
        modeManager.IsInputAllowed = false;
        List<CardController> cardsToShuffle = new List<CardController>();
        List<MontanaSlot> slotsForShuffle = new List<MontanaSlot>();

        // 1. Ищем, какие карты нужно перетасовать и какие слоты заполнить
        for (int r = 0; r < 4; r++)
        {
            int validLength = 0;
            CardModel? expectedCard = null;

            for (int c = 0; c < 13; c++)
            {
                var card = pileManager.GetSlot(r, c).GetTopCard();

                if (c == 0)
                {
                    if (card != null && card.cardModel.rank == 1)
                    {
                        validLength = 1;
                        expectedCard = new CardModel(card.cardModel.suit, 2);
                    }
                    else break;
                }
                else
                {
                    if (card != null && expectedCard.HasValue &&
                        card.cardModel.suit == expectedCard.Value.suit &&
                        card.cardModel.rank == expectedCard.Value.rank)
                    {
                        validLength++;
                        expectedCard = new CardModel(card.cardModel.suit, card.cardModel.rank + 1);
                    }
                    else break;
                }
            }

            int gapCol = modeManager.IsHardMode ? 13 : validLength;

            for (int c = validLength; c < 14; c++)
            {
                var slot = pileManager.GetSlot(r, c);
                if (slot.GetTopCard() != null) cardsToShuffle.Add(slot.GetTopCard());
                if (c != gapCol) slotsForShuffle.Add(slot);
            }
        }

        if (cardsToShuffle.Count == 0)
        {
            modeManager.IsInputAllowed = true;
            yield break;
        }

        // 2. Тасуем логически
        System.Random rng = new System.Random();
        cardsToShuffle = cardsToShuffle.OrderBy(x => rng.Next()).ToList();

        foreach (var c in cardsToShuffle)
        {
            var oldSlot = pileManager.Slots.FirstOrDefault(s => s.GetTopCard() == c);
            if (oldSlot != null) oldSlot.RemoveCard(c);

            c.transform.SetParent(modeManager.DragLayer, true);
            c.GetComponent<MontanaCardController>()?.SetAnimating(true);
        }

        yield return new WaitForSeconds(0.1f);

        // --- ФАЗА АНИМАЦИИ 1: Слет в 55 слот (правый нижний угол) ---
        Vector3 gatherPos = pileManager.Slots[55].Transform.position;
        float gatherDuration = 0.4f;
        float gatherElapsed = 0f;
        List<Vector3> startPositions = cardsToShuffle.Select(c => c.transform.position).ToList();

        while (gatherElapsed < gatherDuration)
        {
            gatherElapsed += Time.deltaTime;
            float t = gatherElapsed / gatherDuration;
            t = t * t * (3f - 2f * t); // Плавное торможение

            for (int i = 0; i < cardsToShuffle.Count; i++)
            {
                cardsToShuffle[i].transform.position = Vector3.Lerp(startPositions[i], gatherPos, t);
            }
            yield return null;
        }

        foreach (var c in cardsToShuffle) c.transform.position = gatherPos;

        // Небольшая пауза, пока карты лежат стопкой
        yield return new WaitForSeconds(0.2f);

        // --- ФАЗА АНИМАЦИИ 2: Раздача из 55 слота на новые места ---
        float dealSpeed = 0.02f; // Скорость пулеметной очереди
        float cardMoveDuration = 0.25f; // Время полета одной карты

        for (int i = 0; i < cardsToShuffle.Count; i++)
        {
            var card = cardsToShuffle[i];
            var targetSlot = slotsForShuffle[i];

            // Выпускаем карты по одной с задержкой
            StartCoroutine(MoveCardToSlotRoutine(card, targetSlot, cardMoveDuration));
            yield return new WaitForSeconds(dealSpeed);
        }

        // Ждем приземления последней карты
        yield return new WaitForSeconds(cardMoveDuration);

        modeManager.IsInputAllowed = true;
        modeManager.CheckGameState();
    }
}