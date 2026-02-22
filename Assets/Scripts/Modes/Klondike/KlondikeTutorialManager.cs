using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public enum TutorialActionType
{
    MoveCard, ClickStock, Undo, DoubleClick
}

[System.Serializable]
public class TutorialStep
{
    [TextArea(2, 4)] public string instructionText;
    public TutorialActionType expectedAction;
    public string expectedCardName;
    public string expectedTargetPileName;
}

public class KlondikeTutorialManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject tutorialUIPanel;
    public TMP_Text instructionText;

    [Header("Sequence")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    public bool IsTutorialActive { get; private set; }
    private int currentStepIndex = 0;
    private KlondikeModeManager modeManager;

    private void Awake()
    {
        IsTutorialActive = GameSettings.IsTutorialMode;

        if (!IsTutorialActive)
        {
            if (tutorialUIPanel) tutorialUIPanel.SetActive(false);

            // [ИСПРАВЛЕНО] Отключаем только этот скрипт, а не весь объект!
            // Иначе, если они висят на одном объекте, выключается вся игра.
            this.enabled = false;

            return;
        }

        modeManager = GetComponent<KlondikeModeManager>();
        if (steps.Count == 0) InitializeDefaultSteps();
    }

    public void AdvanceStep()
    {
        if (!IsTutorialActive) return;

        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            instructionText.text = "Поздравляем! Вы освоили все механики. Удачи в игре!";
            IsTutorialActive = false;
        }
        else
        {
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (instructionText != null && currentStepIndex < steps.Count)
            instructionText.text = steps[currentStepIndex].instructionText;
    }

    public bool IsActionAllowed(TutorialActionType action, CardController card = null, ICardContainer target = null)
    {
        if (!IsTutorialActive || currentStepIndex >= steps.Count) return true;

        TutorialStep step = steps[currentStepIndex];
        if (step.expectedAction != action) return false;

        if (action == TutorialActionType.MoveCard || action == TutorialActionType.DoubleClick)
        {
            if (!string.IsNullOrEmpty(step.expectedCardName) && card != null)
                if (!card.gameObject.name.Contains(step.expectedCardName)) return false;

            if (action == TutorialActionType.MoveCard && !string.IsNullOrEmpty(step.expectedTargetPileName) && target != null)
            {
                var targetMono = target as MonoBehaviour;
                if (targetMono == null || !targetMono.gameObject.name.Contains(step.expectedTargetPileName)) return false;
            }
        }
        return true;
    }

    // ==========================================================
    // ГЕНЕРАТОР ОБУЧЕНИЯ (С последовательным падением стопок)
    // ==========================================================
    public IEnumerator PlayTutorialIntro(Deal dummyDeal)
    {
        modeManager.IsInputAllowed = false;

        // 1. Прячем DeckManager от интро, чтобы заблокировать стандартную раздачу!
        var tempDeck = modeManager.deckManager;
        modeManager.deckManager = null;

        // Запускаем UI интро (выезд панелей, появление слотов)
        if (modeManager.introController != null)
            yield return StartCoroutine(modeManager.introController.PlayIntroSequence(false));

        // Возвращаем DeckManager
        modeManager.deckManager = tempDeck;

        // 2. Очищаем стол и физически уничтожаем возможные старые карты
        PileManager pm = modeManager.pileManager;
        pm.ClearAllPiles();
        modeManager.cardFactory.DestroyAllCards();

        // 3. Формируем 52 карты по нашему сценарию
        string[] F0 = { "Spades_1_up", "Spades_2_up", "Spades_3_up" };
        string[] F1 = { "Hearts_1_up", "Hearts_2_up", "Hearts_3_up" };
        string[] F2 = { "Clubs_1_up", "Clubs_2_up", "Clubs_3_up", "Clubs_4_up", "Clubs_5_up", "Clubs_6_up", "Clubs_7_up", "Clubs_8_up", "Clubs_9_up" };
        string[] F3 = { "Diamonds_1_up", "Diamonds_2_up", "Diamonds_3_up", "Diamonds_4_up", "Diamonds_5_up", "Diamonds_6_up", "Diamonds_7_up", "Diamonds_8_up", "Diamonds_9_up" };
        string[] Stock = { "Spades_8_down", "Hearts_9_down", "Spades_10_down" };

        string[] T0 = { "Spades_4_up" };
        string[] T1 = { "Spades_5_down", "Hearts_4_up" };
        string[] T2 = { "Hearts_5_down", "Clubs_10_up" };
        string[] T3 = { "Diamonds_13_up", "Clubs_12_up", "Diamonds_11_up" };
        string[] T4 = { "Hearts_6_down", "Diamonds_10_up" };
        string[] T5 = { "Spades_13_up", "Hearts_12_up", "Clubs_11_up" };
        string[] T6 = { "Spades_6_down", "Spades_7_down", "Spades_9_down", "Spades_11_down", "Hearts_8_down", "Hearts_10_down", "Clubs_13_down", "Diamonds_12_down", "Hearts_7_down", "Hearts_13_up", "Spades_12_up", "Hearts_11_up" };

        // 4. Мгновенно генерируем и расставляем карты
        SpawnAndPlaceCards(F0, pm.Foundations[0]);
        SpawnAndPlaceCards(F1, pm.Foundations[1]);
        SpawnAndPlaceCards(F2, pm.Foundations[2]);
        SpawnAndPlaceCards(F3, pm.Foundations[3]);
        SpawnAndPlaceCards(Stock, pm.StockPile);
        SpawnAndPlaceCards(T0, pm.Tableau[0]);
        SpawnAndPlaceCards(T1, pm.Tableau[1]);
        SpawnAndPlaceCards(T2, pm.Tableau[2]);
        SpawnAndPlaceCards(T3, pm.Tableau[3]);
        SpawnAndPlaceCards(T4, pm.Tableau[4]);
        SpawnAndPlaceCards(T5, pm.Tableau[5]);
        SpawnAndPlaceCards(T6, pm.Tableau[6]);

        // Принудительно обновляем UI-слой, чтобы карты заняли финальные координаты
        Canvas.ForceUpdateCanvases();

        // 5. АНИМАЦИЯ: Сохраняем целевые координаты и прячем карты наверх
        Vector2 offScreenOffset = new Vector2(0, 1500f);
        Dictionary<CardController, Vector2> targetAnchors = new Dictionary<CardController, Vector2>();

        // Очередь анимации: Stock -> Дома -> Поле (слева направо)
        List<ICardContainer> animSequence = new List<ICardContainer> {
            pm.Tableau[0], pm.Tableau[1], pm.Tableau[2], pm.Tableau[3], pm.Tableau[4], pm.Tableau[5], pm.Tableau[6],
            pm.StockPile,
            pm.Foundations[0], pm.Foundations[1], pm.Foundations[2], pm.Foundations[3]
        };

        foreach (var container in animSequence)
        {
            var mono = container as MonoBehaviour;
            if (mono == null) continue;
            foreach (Transform child in mono.transform)
            {
                var card = child.GetComponent<CardController>();
                if (card != null)
                {
                    targetAnchors[card] = card.rectTransform.anchoredPosition;
                    card.rectTransform.anchoredPosition += offScreenOffset; // Поднимаем за экран
                }
            }
        }

        // Запускаем последовательный "сброс" стопок
        // Запускаем последовательный "сброс" стопок
        float stackMoveDuration = 0.3f; // Возвращаем приятную скорость полета
        float stackDelay = 0.05f;       // Очень короткая пауза ДО старта следующей стопки

        foreach (var container in animSequence)
        {
            var mono = container as MonoBehaviour;
            if (mono == null) continue;

            var cardsInStack = new List<CardController>();
            foreach (Transform child in mono.transform)
            {
                var card = child.GetComponent<CardController>();
                if (card != null) cardsInStack.Add(card);
            }

            if (cardsInStack.Count == 0) continue;

            // [ИСПРАВЛЕНИЕ] Запускаем анимацию стопки ПАРАЛЛЕЛЬНО и идем дальше
            StartCoroutine(AnimateStackDrop(cardsInStack, targetAnchors, offScreenOffset, stackMoveDuration));

            // Ждем долю секунды и сразу запускаем следующую стопку (эффект волны)
            yield return new WaitForSeconds(stackDelay);
        }

        // Ждем, пока приземлится самая последняя запущенная стопка
        yield return new WaitForSeconds(stackMoveDuration);

        // 6. Выравниваем Z-индексы
        modeManager.animationService.ReorderAllContainers(pm.GetAllContainerTransforms());

        // 7. Сбрасываем статистику, чтобы счетчик ходов начался с нуля
        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameStarted("Klondike", Difficulty.Easy, "Tutorial");
        if (modeManager.scoreManager != null) modeManager.scoreManager.ResetScore();

        if (tutorialUIPanel) tutorialUIPanel.SetActive(true);
        currentStepIndex = 0;
        UpdateUI();

        modeManager.IsInputAllowed = true;
    }
    private IEnumerator AnimateStackDrop(List<CardController> cardsInStack, Dictionary<CardController, Vector2> targetAnchors, Vector2 offScreenOffset, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f); // Плавное торможение (EaseOutCubic)

            foreach (var card in cardsInStack)
            {
                if (card != null)
                {
                    Vector2 startPos = targetAnchors[card] + offScreenOffset;
                    card.rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetAnchors[card], t);
                }
            }
            yield return null;
        }

        // Точно ставим на место в конце анимации
        foreach (var card in cardsInStack)
        {
            if (card != null)
            {
                card.rectTransform.anchoredPosition = targetAnchors[card];
            }
        }
    }
    private void SpawnAndPlaceCards(string[] cardData, ICardContainer target)
    {
        foreach (var data in cardData)
        {
            // Парсим строку (пример: "Spades_4_up")
            string[] parts = data.Split('_');
            string suitStr = parts[0];
            int rank = int.Parse(parts[1]);
            bool isFaceUp = parts[2] == "up";

            // Определяем масть
            Suit suit = Suit.Spades;
            if (suitStr == "Hearts") suit = Suit.Hearts;
            else if (suitStr == "Clubs") suit = Suit.Clubs;
            else if (suitStr == "Diamonds") suit = Suit.Diamonds;

            // 1. Создаем карту через официальную фабрику
            CardModel model = new CardModel(suit, rank);
            CardController card = modeManager.cardFactory.CreateCard(model, ((MonoBehaviour)target).transform, Vector2.zero);

            // 2. Называем карту для проверок туториала (например, "Card_Spades_4")
            card.gameObject.name = $"Card_{suitStr}_{rank}";

            // 3. Регистрируем события (Drag, Click) чтобы ее можно было трогать
            modeManager.RegisterCardEvents(card);

            // 4. Добавляем в нужный контейнер через официальные методы
            if (target is TableauPile tableau)
            {
                tableau.AddCard(card, isFaceUp);
            }
            else if (target is FoundationPile foundation)
            {
                foundation.AcceptCard(card);
            }
            else // StockPile или WastePile
            {
                var type = target.GetType();
                var add2 = type.GetMethod("AddCard", new System.Type[] { typeof(CardController), typeof(bool) });
                if (add2 != null) add2.Invoke(target, new object[] { card, isFaceUp });
                else
                {
                    var add1 = type.GetMethod("AddCard", new System.Type[] { typeof(CardController) });
                    if (add1 != null) add1.Invoke(target, new object[] { card });
                }

                var cData = card.GetComponent<CardData>();
                if (cData != null) cData.SetFaceUp(isFaceUp, false);
            }
        }

        // 5. Завершаем настройку для Tableau (останавливаем внутренние анимации и выстраиваем лесенку)
        if (target is TableauPile tPile)
        {
            tPile.StopAllCoroutines();  // Отключаем внутреннюю анимацию раздачи Tableau
            tPile.ForceRebuildLayout(); // Мгновенно расставляем карты

            // Защита от перетаскивания закрытых карт
            for (int i = 0; i < tPile.cards.Count; i++)
            {
                if (!tPile.faceUp[i] && tPile.cards[i].canvasGroup != null)
                {
                    tPile.cards[i].canvasGroup.blocksRaycasts = false;
                }
            }
        }
    }

    private void InitializeDefaultSteps()
    {
        steps.Add(new TutorialStep { instructionText = "Цель игры — собрать карты в 'Дома' сверху.\nПеретащите открытую Четверку Пик в первый Дом.", expectedAction = TutorialActionType.MoveCard, expectedCardName = "Spades_4", expectedTargetPileName = "Foundation" });
        steps.Add(new TutorialStep { instructionText = "На поле карты складываются по убыванию с чередованием цвета.\nПеретащите черную Десятку Треф на красного Валета Бубен.", expectedAction = TutorialActionType.MoveCard, expectedCardName = "Clubs_10", expectedTargetPileName = "Tableau" });
        steps.Add(new TutorialStep { instructionText = "Под картой открылась Пятерка. Карты можно быстро отправлять в Дом двойным кликом.\nДважды кликните по Четверке Червей.", expectedAction = TutorialActionType.DoubleClick, expectedCardName = "Hearts_4" });
        steps.Add(new TutorialStep { instructionText = "В пустые ячейки на поле можно класть ТОЛЬКО Королей.\nПеретащите стопку с черным Королем Пик в пустую колонку.", expectedAction = TutorialActionType.MoveCard, expectedCardName = "Spades_13", expectedTargetPileName = "Tableau" });
        steps.Add(new TutorialStep { instructionText = "Вы можете переносить сразу несколько открытых карт.\nПеренесите красную Десятку Бубен на черного Валета Треф.", expectedAction = TutorialActionType.MoveCard, expectedCardName = "Diamonds_10", expectedTargetPileName = "Tableau" });

        string deckText = "Используйте колоду (слева сверху). Кликните по ней, положите выпавшую карту на поле. Если ошиблись — нажмите 'Отмена' (Undo).";
        steps.Add(new TutorialStep { instructionText = deckText, expectedAction = TutorialActionType.ClickStock });
        steps.Add(new TutorialStep { instructionText = deckText, expectedAction = TutorialActionType.MoveCard, expectedCardName = "Spades_10", expectedTargetPileName = "Tableau" });
        steps.Add(new TutorialStep { instructionText = deckText, expectedAction = TutorialActionType.Undo });

        steps.Add(new TutorialStep { instructionText = "Иногда карту выгодно вернуть из Дома обратно на поле.\nПеретащите Девятку Треф из Дома на красную Десятку Бубен.", expectedAction = TutorialActionType.MoveCard, expectedCardName = "Clubs_9", expectedTargetPileName = "Tableau" });
        steps.Add(new TutorialStep { instructionText = "В нашей обучающей колоде осталось две карты. Давайте достанем их!\nКликните по колоде.", expectedAction = TutorialActionType.ClickStock });
        steps.Add(new TutorialStep { instructionText = "Кликните по колоде последний раз.", expectedAction = TutorialActionType.ClickStock });

        steps.Add(new TutorialStep { instructionText = "Колода пуста! Теперь откроем оставшиеся рубашки.\nДважды кликните по Пятерке Пик.", expectedAction = TutorialActionType.DoubleClick, expectedCardName = "Spades_5" });
        steps.Add(new TutorialStep { instructionText = "Дважды кликните по Пятерке Червей.", expectedAction = TutorialActionType.DoubleClick, expectedCardName = "Hearts_5" });
        steps.Add(new TutorialStep { instructionText = "Осталось открыть последнюю карту!\nДважды кликните по Шестерке Червей.", expectedAction = TutorialActionType.DoubleClick, expectedCardName = "Hearts_6" });

        steps.Add(new TutorialStep { instructionText = "Поздравляем! Все карты открыты. В таких случаях игра предлагает добить партию за вас.\nНажмите кнопку 'АВТО-СБОР'!", expectedAction = TutorialActionType.DoubleClick });
    }
}