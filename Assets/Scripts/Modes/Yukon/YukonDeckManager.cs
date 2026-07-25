using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class YukonDeckManager : MonoBehaviour
{
    public CardFactory cardFactory;

    [Header("Prefabs")]
    [SerializeField] private YukonCardController cardPrefab;

    [Header("Intro Animation")]
    public Transform offScreenSpawnPoint;

    private YukonModeManager modeManager;
    private List<CardController> allCards = new List<CardController>();

    private bool isSkippingIntro = false;

    private class DealInstruction
    {
        public CardController card;
        public YukonTableauPile targetPile;
        public bool faceUp;
        public int row;
    }

    private List<DealInstruction> dealInstructions = new List<DealInstruction>();
    private DealInstruction slot0Instruction;

    private void Awake()
    {
        modeManager = GetComponentInParent<YukonModeManager>();
        if (cardFactory == null) cardFactory = FindObjectOfType<CardFactory>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            isSkippingIntro = true;
        }
    }

    public void LoadDeal(Deal deal, bool animate = false)
    {
        if (animate) StartCoroutine(PlayIntroDeckArrival(deal, false));
        else LoadDealInstant(deal);
    }

    private void LoadDealInstant(Deal deal)
    {
        ClearBoard();
        var tableaus = modeManager.tableaus;
        if (tableaus == null || tableaus.Count == 0) return;

        for (int i = 0; i < deal.tableau.Count; i++)
        {
            if (i >= tableaus.Count) break;
            YukonTableauPile pile = tableaus[i];

            foreach (CardInstance data in deal.tableau[i])
            {
                CardController newCard = CreateCard(data.Card);

                // --- ИСПРАВЛЕНИЕ: Жестко телепортируем карту в стопку ДО любых расчетов ---
                newCard.transform.position = pile.transform.position;

                if (newCard is YukonCardController ycc) ycc.SetFaceUp(data.FaceUp, true);
                pile.AcceptCard(newCard);
            }

            // --- ИСПРАВЛЕНИЕ: Мгновенная расстановка без анимации ---
            pile.ForceRecalculateLayout();
        }
    }

    // --- ЛОГИКА АНИМАЦИИ ---

    public IEnumerator PlayIntroDeckArrival(Deal deal, bool isRestart)
    {
        isSkippingIntro = false;

        // 1. Формируем толстую колоду со сдвигами
        PrepareCardsForIntro(deal);

        YukonTableauPile slot0 = modeManager.tableaus[0];

        // 2. Колода ВСЕГДА вылетает из-за экрана
        Vector3 spawnPos = offScreenSpawnPoint != null ? offScreenSpawnPoint.position : slot0.transform.position + new Vector3(-1500, 0, 0);

        List<Transform> allCardTransforms = new List<Transform>();
        List<Vector3> targetLocalPositions = new List<Vector3>();

        // Собираем карты из slot0 (они уже лежат в правильном визуальном порядке)
        foreach (Transform child in slot0.transform)
        {
            if (child.GetComponent<CardController>() != null)
            {
                allCardTransforms.Add(child);
                targetLocalPositions.Add(child.localPosition); // Сохраняем "сдвиги толщины"
                child.position = spawnPos; // Переносим за экран
            }
        }

        float flyDuration = 1.0f;
        float elapsed = 0f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySoundWithAutoFade("Card_Whoosh_In", flyDuration, 0.2f);
        // Полет всей стопки целиком
        while (elapsed < flyDuration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / flyDuration));

            for (int i = 0; i < allCardTransforms.Count; i++)
            {
                // Переводим локальный сдвиг в мировую цель
                Vector3 targetWorld = slot0.transform.TransformPoint(targetLocalPositions[i]);
                allCardTransforms[i].position = Vector3.Lerp(spawnPos, targetWorld, t);
            }
            yield return null;
        }

        // Страховочная доводка до точных координат
        for (int i = 0; i < allCardTransforms.Count; i++)
        {
            allCardTransforms[i].position = slot0.transform.TransformPoint(targetLocalPositions[i]);
        }

        // 3. Раздача карт по рядам
        yield return StartCoroutine(AnimateOpeningDeal());
    }

    private void PrepareCardsForIntro(Deal deal)
    {
        ClearBoard();
        dealInstructions.Clear();
        YukonTableauPile slot0 = modeManager.tableaus[0];

        // А. Инструкция для 1-й карты (останется в слоте 0)
        var cardInst0 = deal.tableau[0][0];
        CardController card0 = CreateCard(cardInst0.Card);
        slot0Instruction = new DealInstruction { card = card0, targetPile = slot0, faceUp = cardInst0.FaceUp, row = 0 };

        // Б. Собираем инструкции для остальных карт
        List<DealInstruction> tempInstructions = new List<DealInstruction>();
        int maxRows = 12;

        for (int row = 0; row < maxRows; row++)
        {
            for (int col = 1; col < 7; col++)
            {
                if (deal.tableau[col] != null && row < deal.tableau[col].Count)
                {
                    var cardInst = deal.tableau[col][row];
                    CardController card = CreateCard(cardInst.Card);
                    tempInstructions.Add(new DealInstruction { card = card, targetPile = modeManager.tableaus[col], faceUp = cardInst.FaceUp, row = row });
                }
            }
        }
        dealInstructions = tempInstructions;

        // В. ВИЗУАЛЬНАЯ ТОЛЩИНА КОЛОДЫ
        List<CardController> deckVis = new List<CardController>();
        deckVis.Add(slot0Instruction.card); // Нижняя карта

        // Добавляем карты задом наперед, чтобы те, что сдаются первыми, лежали сверху
        for (int i = dealInstructions.Count - 1; i >= 0; i--) deckVis.Add(dealInstructions[i].card);

        // Укладываем карты в Slot0 с микро-отступами
        for (int i = 0; i < deckVis.Count; i++)
        {
            var c = deckVis[i];
            c.transform.SetParent(slot0.transform, false);
            c.transform.SetAsLastSibling();

            var data = c.GetComponent<CardData>();
            data.SetFaceUp(false, false);
            if (c.canvasGroup) c.canvasGroup.blocksRaycasts = false;

            // Сдвиг: немного вверх по Y и ближе к камере по Z
            c.transform.localPosition = new Vector3(0, i * 0.4f, -i * 0.01f);
        }
    }

    private IEnumerator AnimateOpeningDeal()
    {
        float durationPerCard = 0.25f;
        float delayBetweenCards = 0.04f;
        RectTransform flyLayer = modeManager.DragLayer;

        foreach (var inst in dealInstructions)
        {
            if (flyLayer != null)
            {
                inst.card.transform.SetParent(flyLayer, true);
                inst.card.transform.SetAsLastSibling();
            }

            // Высчитываем будущую позицию карты
            float yOffset = 0f;
            int colIndex = modeManager.tableaus.IndexOf(inst.targetPile);

            for (int i = 0; i < inst.row; i++)
            {
                bool isPrevFaceUp = i >= colIndex;
                yOffset += isPrevFaceUp ? 35f : 10f;
            }

            float scaleY = inst.targetPile.transform.lossyScale.y;
            Vector3 targetPos = inst.targetPile.transform.position - new Vector3(0, yOffset * scaleY, 0.01f * inst.row);

            StartCoroutine(MoveCardRoutine(inst, targetPos, durationPerCard));
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound("Card_Deal");
            float waitTimer = 0f;
            while (waitTimer < delayBetweenCards)
            {
                float speed = isSkippingIntro ? 15f : 1f;
                waitTimer += Time.deltaTime * speed;
                yield return null;
            }
        }

        float finalTimer = 0f;
        while (finalTimer < durationPerCard)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            finalTimer += Time.deltaTime * speed;
            yield return null;
        }

        // Последний штрих — карта в Slot0 открывается
        slot0Instruction.card.transform.localPosition = Vector3.zero; // Сбрасываем сдвиг толщины
        slot0Instruction.targetPile.AcceptCard(slot0Instruction.card);
        var data0 = slot0Instruction.card.GetComponent<CardData>();

        if (slot0Instruction.faceUp)
        {
            if (slot0Instruction.card is YukonCardController ycc0) ycc0.SetFaceUp(true, false);
            else data0.SetFaceUp(true, animate: true);
        }

        if (slot0Instruction.card.canvasGroup)
        {
            slot0Instruction.card.canvasGroup.blocksRaycasts = true;
            slot0Instruction.card.canvasGroup.interactable = true;
        }

        foreach (var t in modeManager.tableaus) t.ForceRecalculateLayout();
    }

    private IEnumerator MoveCardRoutine(DealInstruction inst, Vector3 targetPos, float duration)
    {
        Vector3 startPos = inst.card.transform.position;
        float elapsed = 0f;
        bool flipped = false;

        while (elapsed < duration)
        {
            float speed = isSkippingIntro ? 15f : 1f;
            elapsed += Time.deltaTime * speed;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            inst.card.transform.position = Vector3.Lerp(startPos, targetPos, t);

            // Переворот в середине полета
            if (inst.faceUp && t >= 0.5f && !flipped)
            {
                flipped = true;
                if (inst.card is YukonCardController ycc) ycc.SetFaceUp(true, false);
            }

            yield return null;
        }

        inst.card.transform.position = targetPos;
        inst.card.transform.SetParent(inst.targetPile.transform, true);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound("Card_Drop_Success");
        inst.targetPile.AcceptCard(inst.card);

        if (inst.card.canvasGroup)
        {
            inst.card.canvasGroup.blocksRaycasts = true;
            inst.card.canvasGroup.interactable = true;
        }
    }

    private YukonCardController CreateCard(CardModel model)
    {
        var newCard = Instantiate(cardPrefab, modeManager.RootCanvas.transform, false);
        newCard.name = $"Card_{model.suit}_{model.rank}";
        newCard.cardModel = model;
        newCard.CardmodeManager = modeManager;
        newCard.canvas = modeManager.RootCanvas;

        var data = newCard.GetComponent<CardData>();
        if (data != null && cardFactory.spriteDb != null)
        {
            // ИСПРАВЛЕНИЕ 1: Используем GetSprite(), чтобы работали Русские Буквы!
            Sprite face = cardFactory.spriteDb.GetSprite(model.suit, model.rank);

            // ИСПРАВЛЕНИЕ 2: Запрашиваем АКТУАЛЬНУЮ рубашку, а не дефолтную!
            data.backSprite = cardFactory.spriteDb.GetCurrentBackSprite();

            data.SetModel(model, face);
        }

        allCards.Add(newCard);
        return newCard;
    }

    public void ClearBoard()
    {
        foreach (var c in allCards) if (c != null) Destroy(c.gameObject);
        allCards.Clear();

        if (modeManager.tableaus != null) foreach (var t in modeManager.tableaus) t.Clear();
        if(modeManager.foundations != null)
{
            foreach (var f in modeManager.foundations)
            {
                f.Clear();
            }
        }
    }
}