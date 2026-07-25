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

    [HideInInspector] public bool isSkippingIntro = false;

    public void Initialize(MontanaModeManager mode, CardFactory factory, MontanaPileManager piles)
    {
        modeManager = mode;
        cardFactory = factory;
        pileManager = piles;
    }

    private void Update()
    {
        if (!modeManager.IsInputAllowed && (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
        {
            isSkippingIntro = true;
        }
    }

    public void DealInitial()
    {
        StartCoroutine(DealRoutine());
    }

    private IEnumerator DealRoutine()
    {
        isSkippingIntro = false;
        modeManager.IsInputAllowed = false;
        pileManager.ClearAllPiles();

        // === ИНТЕГРАЦИЯ ТУТОРИАЛА ===
        if (GameSettings.IsTutorialMode && modeManager.Tutorial != null)
        {
            var tutorial = modeManager.Tutorial as MontanaTutorialManager;
            if (tutorial != null)
            {
                tutorial.enabled = true; // Принудительно включаем скрипт
                yield return StartCoroutine(tutorial.PlayTutorialIntro());
                yield break; // Останавливаем обычную генерацию карт!
            }
        }
        // ==============================

        if (DealCacheSystem.Instance == null) yield break;
        while (!DealCacheSystem.Instance.IsReady) yield return null;

        int param = modeManager.IsHardMode ? 1 : 0;
        Difficulty diff = GameSettings.CurrentDifficulty;

        Deal deal = DealCacheSystem.Instance.GetDeal(modeManager.GameType, diff, param);
        if (deal == null) yield break;

        // Расшифровываем сид из колоды
        modeManager.puzzleManager.InitializeFromStock(deal.stock, diff, modeManager.MaxReshuffles);

        if (introController != null)
        {
            introController.PrepareIntro(modeManager.isRestarting);
            yield return StartCoroutine(introController.PlayIntroSequence(modeManager.isRestarting));
        }

        MontanaSlot slot0 = pileManager.GetSlot(0, 0);
        Vector3 targetStackPos = slot0.Transform.position;

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

                var card = cardFactory.CreateCard(inst.Card, modeManager.DragLayer, Vector2.zero);
                card.transform.position = startStackPos + cardOffset;

                card.GetComponent<CardData>()?.SetFaceUp(true, false);

                cardsToDeal.Add(card);
                targetSlotIndices.Add(i);
            }
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", 0.85f, 0.2f);

        float flyDuration = 0.85f;
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            float easedT = t * t * (3f - 2f * t);

            for (int i = 0; i < cardsToDeal.Count; i++)
            {
                Vector3 cardOffset = new Vector3(i * 1f * canvasScale, 0, 0);
                cardsToDeal[i].transform.position = Vector3.Lerp(startStackPos + cardOffset, targetStackPos + cardOffset, easedT);
            }
            yield return null;
        }

        for (int i = 0; i < cardsToDeal.Count; i++)
        {
            cardsToDeal[i].transform.position = targetStackPos + new Vector3(i * 1f * canvasScale, 0, 0);
        }

        yield return StartCoroutine(SkippableWait(0.15f));

        float dealSpeed = 0.02f;
        float cardMoveDuration = 0.22f;

        for (int i = cardsToDeal.Count - 1; i >= 0; i--)
        {
            var card = cardsToDeal[i];
            int slotIndex = targetSlotIndices[i];
            var targetSlot = pileManager.Slots[slotIndex];

            if (slotIndex == 0)
            {
                targetSlot.AcceptCard(card);
                modeManager.RegisterCardEvents(card);
            }
            else
            {
                StartCoroutine(MoveCardToSlotRoutine(card, targetSlot, cardMoveDuration));
                yield return StartCoroutine(SkippableWait(dealSpeed));
            }
        }

        yield return StartCoroutine(SkippableWait(cardMoveDuration));

        modeManager.SaveInitialState();
        modeManager.IsInputAllowed = true;
        modeManager.isRestarting = false;
        modeManager.CheckGameState();
    }

    private IEnumerator MoveCardToSlotRoutine(CardController card, MontanaSlot targetSlot, float duration)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Deal");

        Vector3 startPos = card.transform.position;
        Vector3 endPos = targetSlot.Transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);

            card.transform.position = Vector3.Lerp(startPos, endPos, easedT);
            yield return null;
        }

        card.transform.position = endPos;
        targetSlot.AcceptCard(card);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Card_Drop_Success");

        card.GetComponent<MontanaCardController>()?.SetAnimating(false);
        modeManager.RegisterCardEvents(card);
    }

    private IEnumerator SkippableWait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            yield return null;
        }
    }

    public IEnumerator ReshuffleRoutine()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        isSkippingIntro = false;
        modeManager.IsInputAllowed = false;

        // Делаем снимок стола ДО пересдачи, чтобы её можно было отменить
        var preReshuffleState = new Dictionary<CardController, MontanaSlot>();
        foreach (var slot in pileManager.Slots)
        {
            var topCard = slot.GetTopCard();
            if (topCard != null) preReshuffleState[topCard] = slot;
        }
        modeManager.PushReshuffleRecord(preReshuffleState);

        List<CardController> cardsToShuffle = new List<CardController>();
        List<MontanaSlot> slotsForShuffle = new List<MontanaSlot>();

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

        // ========================================================
        // НОВАЯ СИСТЕМА: Запрашиваем веса у Оракула
        // ========================================================
        bool isWinningRoll;
        Difficulty diff = GameSettings.CurrentDifficulty;

        Dictionary<string, int> stableWeights = modeManager.puzzleManager.GetReshuffleWeights(
            pileManager,
            modeManager.MaxReshuffles,
            modeManager.CurrentReshufflesLeft, // Передается уже уменьшенное значение (R)
            diff,
            modeManager.IsHardMode,
            out isWinningRoll
        );

        if (AudioManager.Instance != null)
        {
            // Подсказка эхолота: успех или тупик
            AudioManager.Instance.PlaySound(isWinningRoll ? "Card_Foundation_Success" : "Card_Drop_Fail");
        }

        // Сортировка карт по полученным весам
        cardsToShuffle = cardsToShuffle.OrderBy(c => stableWeights[$"{c.cardModel.suit}_{c.cardModel.rank}"]).ToList();
        // ========================================================

        foreach (var c in cardsToShuffle)
        {
            var oldSlot = pileManager.Slots.FirstOrDefault(s => s.GetTopCard() == c);
            if (oldSlot != null) oldSlot.RemoveCard(c);

            c.transform.SetParent(modeManager.DragLayer, true);
            c.GetComponent<MontanaCardController>()?.SetAnimating(true);
        }

        yield return StartCoroutine(SkippableWait(0.1f));

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", 0.4f, 0.1f);

        Vector3 gatherPos = pileManager.Slots[55].Transform.position;
        float gatherDuration = 0.4f;
        float gatherElapsed = 0f;
        List<Vector3> startPositions = cardsToShuffle.Select(c => c.transform.position).ToList();

        while (gatherElapsed < gatherDuration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            gatherElapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(gatherElapsed / gatherDuration);
            t = t * t * (3f - 2f * t);

            for (int i = 0; i < cardsToShuffle.Count; i++)
            {
                cardsToShuffle[i].transform.position = Vector3.Lerp(startPositions[i], gatherPos, t);
            }
            yield return null;
        }

        foreach (var c in cardsToShuffle) c.transform.position = gatherPos;

        yield return StartCoroutine(SkippableWait(0.2f));

        float dealSpeed = 0.02f;
        float cardMoveDuration = 0.25f;

        for (int i = 0; i < cardsToShuffle.Count; i++)
        {
            var card = cardsToShuffle[i];
            var targetSlot = slotsForShuffle[i];

            StartCoroutine(MoveCardToSlotRoutine(card, targetSlot, cardMoveDuration));
            yield return StartCoroutine(SkippableWait(dealSpeed));
        }

        yield return StartCoroutine(SkippableWait(cardMoveDuration));

        modeManager.IsInputAllowed = true;
        modeManager.CheckGameState();
    }
}