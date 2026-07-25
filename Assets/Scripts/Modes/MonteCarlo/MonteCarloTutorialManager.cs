using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MonteCarloTutorialManager : MonoBehaviour, ITutorialManager
{
    [Header("UI")]
    public RectTransform tutorialUIPanel;
    public TMP_Text instructionText;
    public TMP_Text stepIndicatorText;

    [Header("Panel Anchors")]
    public RectTransform topAnchor;
    public RectTransform centerAnchor;
    public RectTransform bottomAnchor;

    [Header("Arrows")]
    public RectTransform arrowDown;
    public RectTransform arrowUp;
    public RectTransform arrowLeft;

    [Tooltip("Массив якорей. 0-24: слоты стола. 25: Кнопка отмены.")]
    public RectTransform[] arrowAnchors = new RectTransform[26];

    [Header("Arrow Animation Settings")]
    public float arrowBounceAmplitude = 12f;
    public float arrowBounceSpeed = 6f;
    private Coroutine arrowAnimCoroutine;

    private RectTransform activeArrow;
    private TutorialArrowType activeArrowType;
    private CardController lastSelectedCard;

    [Header("Sequence")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    public bool IsTutorialActive { get; private set; }
    private int currentStepIndex = 0;
    private MonteCarloModeManager modeManager;
    private Coroutine panelMoveCoroutine;

    private bool stepTransitioning = false;
    private bool isIntroAnimating = true;

    // --- ДОБАВЛЕНО: Флаг для отслеживания улетевшей панели меню ---
    private bool isPanelHiddenByMenu = false;

    private void Awake()
    {
        IsTutorialActive = GameSettings.IsTutorialMode;
        if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);

        if (!IsTutorialActive)
        {
            this.enabled = false;
            return;
        }

        modeManager = GetComponent<MonteCarloModeManager>();
        if (modeManager != null) modeManager.tutorialManager = this;

        steps.Clear();
        InitializeDefaultSteps();
    }

    private void LateUpdate()
    {
        if (!IsTutorialActive || stepTransitioning || isIntroAnimating) return;

        if (modeManager != null && modeManager.gameUI != null)
        {
            bool isSystemMenuOpen = (modeManager.gameUI.settingsPanel != null && modeManager.gameUI.settingsPanel.activeSelf) ||
                                    (modeManager.gameUI.exitConfirmationPanel != null && modeManager.gameUI.exitConfirmationPanel.activeSelf) ||
                                    (modeManager.gameUI.newGameConfirmationPanel != null && modeManager.gameUI.newGameConfirmationPanel.activeSelf) ||
                                    (modeManager.gameUI.winPanel != null && modeManager.gameUI.winPanel.activeSelf) ||
                                    (modeManager.gameUI.defeatPanel != null && modeManager.gameUI.defeatPanel.activeSelf);

            // --- ИЗМЕНЕНО: Логика Паука. Вызываем анимацию улетания, а не жесткое отключение ---
            if (isSystemMenuOpen && !isPanelHiddenByMenu)
            {
                isPanelHiddenByMenu = true;
                HidePanelToLeft();
                return;
            }
            else if (!isSystemMenuOpen && isPanelHiddenByMenu)
            {
                isPanelHiddenByMenu = false;
                if (modeManager.IsInputAllowed)
                {
                    RestorePanelPosition();
                }
            }

            if (isSystemMenuOpen) return; // Блокируем остальные обновления туториала, пока открыто меню
        }

        if (modeManager != null && currentStepIndex < steps.Count)
        {
            if (steps[currentStepIndex].localizationKey == "MonteCarloTutorial2") modeManager.is8Ways = false;
            else modeManager.is8Ways = true;

            if (modeManager.SelectedCard != lastSelectedCard)
            {
                lastSelectedCard = modeManager.SelectedCard;
                SetupArrows(steps[currentStepIndex]);
            }

            if (currentStepIndex == 5 && modeManager.isGameWon)
            {
                stepTransitioning = true;
                InternalAdvanceStep();
            }
        }
    }

    public void AdvanceStep()
    {
        if (currentStepIndex == 5) return;

        if (!stepTransitioning && IsTutorialActive && gameObject.activeInHierarchy)
        {
            stepTransitioning = true;
            StartCoroutine(AdvanceStepRoutine());
        }
    }

    private IEnumerator AdvanceStepRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        InternalAdvanceStep();
        stepTransitioning = false;
    }

    public bool IsActionAllowed(TutorialActionType action, CardController card = null, ICardContainer target = null)
    {
        if (!IsTutorialActive || currentStepIndex >= steps.Count) return true;

        TutorialStep step = steps[currentStepIndex];
        if (action != step.expectedAction) return false;

        if (action == TutorialActionType.MoveCard && card != null)
        {
            if (string.IsNullOrEmpty(step.expectedCardName)) return true;

            if (card.gameObject.name != step.expectedCardName && card.gameObject.name != step.expectedTargetPileName)
                return false;
        }

        return true;
    }

    private void InternalAdvanceStep()
    {
        if (!IsTutorialActive) return;

        currentStepIndex++;
        lastSelectedCard = null;

        if (currentStepIndex >= steps.Count)
        {
            string endText = "Поздравляем! Вы очистили всё поле!";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue("MonteCarloTutorialEnd");
                if (!string.IsNullOrEmpty(loc)) endText = loc;
            }
            if (instructionText != null) instructionText.text = endText;
            if (stepIndicatorText != null) stepIndicatorText.text = "";

            HideArrows();
            IsTutorialActive = false;
        }
        else
        {
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];

        if (instructionText != null)
        {
            string textToShow = step.fallbackText;
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue(step.localizationKey);
                if (!string.IsNullOrEmpty(loc)) textToShow = loc;
            }
            instructionText.text = textToShow;
        }

        if (stepIndicatorText != null) stepIndicatorText.text = $"{currentStepIndex + 1}/{steps.Count}";

        if (tutorialUIPanel != null)
        {
            if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);

            RectTransform targetAnchor = GetAnchorRect(step.panelAnchor);

            tutorialUIPanel.anchorMin = targetAnchor.anchorMin;
            tutorialUIPanel.anchorMax = targetAnchor.anchorMax;
            tutorialUIPanel.pivot = targetAnchor.pivot;
            tutorialUIPanel.sizeDelta = targetAnchor.sizeDelta;

            panelMoveCoroutine = StartCoroutine(MovePanelToPosRoutine(targetAnchor.anchoredPosition));
        }

        SetupArrows(step);
    }

    private void SetupArrows(TutorialStep step)
    {
        HideArrows();

        if (arrowAnchors == null || arrowAnchors.Length == 0) return;

        bool showSecondArrow = false;

        if (modeManager != null && modeManager.SelectedCard != null)
        {
            if (modeManager.SelectedCard.gameObject.name == step.expectedCardName)
            {
                showSecondArrow = true;
            }
        }

        if (showSecondArrow && step.arrowType2 != TutorialArrowType.None && step.arrowAnchorIndex2 >= 0 && step.arrowAnchorIndex2 < arrowAnchors.Length)
        {
            RectTransform anchor2 = arrowAnchors[step.arrowAnchorIndex2];
            if (anchor2 != null)
            {
                activeArrow = GetArrowPrefab(step.arrowType2);
                activeArrowType = step.arrowType2;
                if (activeArrow != null)
                {
                    activeArrow.gameObject.SetActive(true);
                    activeArrow.SetParent(anchor2, false);
                    activeArrow.anchoredPosition = Vector2.zero;
                }
            }
        }
        else if (step.arrowType != TutorialArrowType.None && step.arrowAnchorIndex >= 0 && step.arrowAnchorIndex < arrowAnchors.Length)
        {
            RectTransform anchor1 = arrowAnchors[step.arrowAnchorIndex];
            if (anchor1 != null)
            {
                activeArrow = GetArrowPrefab(step.arrowType);
                activeArrowType = step.arrowType;
                if (activeArrow != null)
                {
                    activeArrow.gameObject.SetActive(true);
                    activeArrow.SetParent(anchor1, false);
                    activeArrow.anchoredPosition = Vector2.zero;
                }
            }
        }

        if (activeArrow != null)
        {
            arrowAnimCoroutine = StartCoroutine(AnimateArrowBounce());
        }
    }

    public void HideHighlights()
    {
        HideArrows();
    }

    private void HideArrows()
    {
        if (arrowAnimCoroutine != null) StopCoroutine(arrowAnimCoroutine);
        if (arrowDown) arrowDown.gameObject.SetActive(false);
        if (arrowUp) arrowUp.gameObject.SetActive(false);
        if (arrowLeft) arrowLeft.gameObject.SetActive(false);
        activeArrow = null;
    }

    private IEnumerator AnimateArrowBounce()
    {
        float elapsed = 0f;
        Vector2 basePos = activeArrow != null ? activeArrow.anchoredPosition : Vector2.zero;

        while (activeArrow != null)
        {
            elapsed += Time.unscaledDeltaTime * arrowBounceSpeed;
            float offset = Mathf.Sin(elapsed) * arrowBounceAmplitude;

            if (activeArrowType == TutorialArrowType.Down || activeArrowType == TutorialArrowType.Up)
                activeArrow.anchoredPosition = basePos + new Vector2(0, offset);
            else if (activeArrowType == TutorialArrowType.Left)
                activeArrow.anchoredPosition = basePos + new Vector2(offset, 0);

            yield return null;
        }
    }

    private RectTransform GetArrowPrefab(TutorialArrowType type)
    {
        if (type == TutorialArrowType.Down) return arrowDown;
        if (type == TutorialArrowType.Up) return arrowUp;
        if (type == TutorialArrowType.Left) return arrowLeft;
        return null;
    }

    private RectTransform GetAnchorRect(TutorialPanelAnchorType type)
    {
        if (type == TutorialPanelAnchorType.Top) return topAnchor;
        if (type == TutorialPanelAnchorType.Center) return centerAnchor;
        return bottomAnchor;
    }

    // --- ИЗМЕНЕНО: Новая корутина, принимающая Vector2 для анимации улетания/возврата ---
    private IEnumerator MovePanelToPosRoutine(Vector2 targetPos)
    {
        Vector2 startPos = tutorialUIPanel.anchoredPosition;
        float duration = 0.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // unscaledDeltaTime позволяет анимации работать даже если открыто меню паузы
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - (elapsed / duration), 3f);
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        tutorialUIPanel.anchoredPosition = targetPos;
    }

    // --- ДОБАВЛЕНО: Полная реализация методов интерфейса из Паука ---
    public void HidePanelToLeft()
    {
        if (tutorialUIPanel != null && tutorialUIPanel.gameObject.activeInHierarchy)
        {
            if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);

            Vector2 hiddenPos = tutorialUIPanel.anchoredPosition;
            hiddenPos.x = -2500f; // Запускаем панель далеко влево за экран

            panelMoveCoroutine = StartCoroutine(MovePanelToPosRoutine(hiddenPos));
        }
        HideArrows();
    }

    public void RestorePanelPosition()
    {
        if (tutorialUIPanel != null && currentStepIndex < steps.Count)
        {
            if (!tutorialUIPanel.gameObject.activeSelf) tutorialUIPanel.gameObject.SetActive(true);
            if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);

            RectTransform targetAnchor = GetAnchorRect(steps[currentStepIndex].panelAnchor);

            tutorialUIPanel.anchorMin = targetAnchor.anchorMin;
            tutorialUIPanel.anchorMax = targetAnchor.anchorMax;
            tutorialUIPanel.pivot = targetAnchor.pivot;
            tutorialUIPanel.sizeDelta = targetAnchor.sizeDelta;

            panelMoveCoroutine = StartCoroutine(MovePanelToPosRoutine(targetAnchor.anchoredPosition));
            SetupArrows(steps[currentStepIndex]);
        }
    }

    public IEnumerator PlayTutorialIntro(Deal dummyDeal)
    {
        isIntroAnimating = true;
        modeManager.IsInputAllowed = false;

        if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);
        HideArrows();

        if (modeManager.introController != null) modeManager.introController.PrepareIntro(false);

        modeManager.pileManager.ClearAll();
        modeManager.deckManager.cardFactory.DestroyAllCards();

        string[] boardCards = {
            "Hearts_5", "Spades_5", "Diamonds_7", "Hearts_2", "Spades_2",
            "Hearts_3", "Hearts_13", "Clubs_7", "Spades_3", "Hearts_4",
            "Spades_4", "Hearts_6", "Spades_6", "Spades_13", "Hearts_8",
            "Spades_8", "Hearts_9", "Spades_9", "Hearts_10", "Spades_10",
            "Hearts_11", "Spades_11", "Hearts_12", "Spades_12", "Hearts_1"
        };
        string[] stockCards = { "Spades_1", "Clubs_2", "Diamonds_2" };

        for (int i = 0; i < 25; i++)
        {
            var c = SpawnCard(boardCards[i], modeManager.pileManager.TableauSlots[i]);
            modeManager.pileManager.BoardCards[i] = c;
        }

        for (int i = 0; i < stockCards.Length; i++)
        {
            var c = SpawnCard(stockCards[i], modeManager.pileManager.StockRoot);
            modeManager.pileManager.StockCards.Add(c);
        }

        modeManager.pileManager.UpdateShadows();

        if (modeManager.introController != null)
        {
            modeManager.introController.InstantHideCardsOffscreen();
            yield return StartCoroutine(modeManager.introController.PlayIntroSequence(false));
        }

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameStarted("MonteCarlo", Difficulty.Easy, "Tutorial");

        currentStepIndex = 0;

        if (tutorialUIPanel)
        {
            RectTransform targetAnchor = GetAnchorRect(steps[0].panelAnchor);
            tutorialUIPanel.anchorMin = targetAnchor.anchorMin;
            tutorialUIPanel.anchorMax = targetAnchor.anchorMax;
            tutorialUIPanel.pivot = targetAnchor.pivot;
            tutorialUIPanel.sizeDelta = targetAnchor.sizeDelta;

            // Стартуем из-за правого края экрана, чтобы красиво выехать
            tutorialUIPanel.anchoredPosition = targetAnchor.anchoredPosition + new Vector2(2500f, 0);
            tutorialUIPanel.gameObject.SetActive(true);
        }

        UpdateUI();

        isIntroAnimating = false;
        modeManager.IsInputAllowed = true;
    }

    private CardController SpawnCard(string nameData, Transform target)
    {
        string[] parts = nameData.Split('_');
        string suitStr = parts[0];
        int rank = int.Parse(parts[1]);

        Suit suit = Suit.Spades;
        if (suitStr == "Hearts") suit = Suit.Hearts;
        else if (suitStr == "Clubs") suit = Suit.Clubs;
        else if (suitStr == "Diamonds") suit = Suit.Diamonds;

        CardModel model = new CardModel(suit, rank);
        CardController card = modeManager.deckManager.cardFactory.CreateCard(model, target, Vector2.zero);

        card.gameObject.name = $"Card_{suitStr}_{rank}";
        card.CardmodeManager = modeManager;
        card.OnClicked += modeManager.OnCardClicked;

        var cData = card.GetComponent<CardData>();
        if (cData)
        {
            cData.SetFaceUp(true, false);
            if (cData.image) cData.image.color = Color.white;
        }
        return card;
    }

    private void OnDisable()
    {
        if (tutorialUIPanel != null) tutorialUIPanel.gameObject.SetActive(false);
        HideArrows();
    }

    private void OnDestroy()
    {
        GameSettings.IsTutorialMode = false;
        if (tutorialUIPanel != null) tutorialUIPanel.gameObject.SetActive(false);
        HideArrows();
    }

    private void InitializeDefaultSteps()
    {
        string clrOrg = "<color=#FCA311>";
        string clrRed = "<color=#FF6B6B>";
        string clrBlk = "<color=#8294FF>";
        string endClr = "</color>";

        // Шаг 1: Пятерки (Слоты 0 и 1)
        steps.Add(new TutorialStep
        {
            localizationKey = "MonteCarloTutorial1",
            fallbackText = $"В Монте-Карло нужно убирать пары одинаковых карт.\nСначала правило {clrOrg}'8 сторон'{endClr}. Кликните на две стоящие рядом Пятерки: {clrRed}(5♥){endClr} и {clrBlk}(5♠){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Hearts_5",
            expectedTargetPileName = "Card_Spades_5",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 0,
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 1
        });

        // Шаг 2: Семерки (Слоты 2 и 7)
        steps.Add(new TutorialStep
        {
            localizationKey = "MonteCarloTutorial2",
            fallbackText = $"Карты сдвинулись! Теперь включен режим {clrOrg}'4 стороны'{endClr} (крестом).\nСемерки оказались друг над другом. Уберите {clrRed}(7♦){endClr} и {clrBlk}(7♣){endClr}!",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Diamonds_7",
            expectedTargetPileName = "Card_Clubs_7",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 2,
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 3
        });

        // Шаг 3: Короли (Слоты 7 и 13)
        steps.Add(new TutorialStep
        {
            localizationKey = "MonteCarloTutorial3",
            fallbackText = $"Снова режим {clrOrg}'8 сторон'{endClr}! Теперь можно спаривать и по диагонали.\nУберите двух Королей: {clrRed}(K♥){endClr} и {clrBlk}(K♠){endClr}.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Hearts_13",
            expectedTargetPileName = "Card_Spades_13",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 4,
            arrowType2 = TutorialArrowType.Up,
            arrowAnchorIndex2 = 5
        });

        // Шаг 4: Отмена
        steps.Add(new TutorialStep
        {
            localizationKey = "MonteCarloTutorial4",
            fallbackText = $"Отличный ход! Но давайте проверим как работает {clrOrg}'Отмена'{endClr}.\nНажмите кнопку отмены хода внизу экрана.",
            expectedAction = TutorialActionType.Undo,
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.Down,
            arrowAnchorIndex = 6
        });

        // Шаг 5: Переход к финалу
        steps.Add(new TutorialStep
        {
            localizationKey = "MonteCarloTutorial5",
            fallbackText = $"Короли вернулись на место. Вы знаете все правила!\nСделайте еще один ход, чтобы продолжить.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "",
            panelAnchor = TutorialPanelAnchorType.Bottom
        });

        // --- НОВЫЙ ШАГ 6: Ожидание победы ---
        steps.Add(new TutorialStep
        {
            localizationKey = "MonteCarloTutorial6",
            fallbackText = $"Продолжайте убирать пары до полной победы!",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "",
            panelAnchor = TutorialPanelAnchorType.Bottom,
            arrowType = TutorialArrowType.None
        });
    }
}