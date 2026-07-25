using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Enum'ы TutorialActionType, TutorialPanelAnchorType и TutorialArrowType 
// используются общие из KlondikeTutorialManager (если они лежат в отдельном файле)
// Если нет, Unity возьмет их из скрипта Косынки.

public class SpiderTutorialManager : MonoBehaviour, ITutorialManager
{
    [Header("UI")]
    public RectTransform tutorialUIPanel;
    public TMP_Text instructionText;
    public TMP_Text stepIndicatorText;

    [Header("Panel Anchors")]
    public RectTransform topAnchor;
    public RectTransform centerAnchor;
    public RectTransform bottomAnchor;
    [Header("UI Buttons to Block")]
    public UnityEngine.UI.Button undoButton;    // Перетащите сюда кнопку отмены (1 ход)
    public UnityEngine.UI.Button undoAllButton; // Перетащите сюда кнопку отмены всех ходов
    [Header("Arrows")]
    public RectTransform arrowDown;
    public RectTransform arrowUp;
    public RectTransform arrowLeft;
    public RectTransform[] arrowAnchors = new RectTransform[10];

    [Header("Arrow Animation Settings")]
    public float arrowBounceAmplitude = 12f;
    public float arrowBounceSpeed = 6f;
    private Coroutine arrowAnimCoroutine;

    [Header("Highlights")]
    public List<GameObject> highlightObjects = new List<GameObject>();

    [Header("Sequence")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    public bool IsTutorialActive { get; private set; }
    private int currentStepIndex = 0;

    // ВАЖНО: Замените на ваш класс менеджера Паука
    private SpiderModeManager modeManager;
    private SpiderDeckManager _tempDeckManager;
    private Coroutine panelMoveCoroutine;

    private ICardContainer initialCardContainer;
    private int initialStockCount;
    private bool stepTransitioning = false;
    private bool autoWinForced = false;

    private Dictionary<RectTransform, Vector2> activeHighlightsOriginalPos = new Dictionary<RectTransform, Vector2>();
    private Coroutine highlightsAnimCoroutine;
    private const float HighlightOffscreenYOffset = 1500f;

    private void Awake()
    {
        IsTutorialActive = GameSettings.IsTutorialMode;

        // ФИКС 2: Сразу выключаем панель обучения, чтобы она не мелькала на старте
        if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);

        if (!IsTutorialActive)
        {
            DisableAllHighlights();
            this.enabled = false;
            return;
        }

        modeManager = GetComponent<SpiderModeManager>();
        if (modeManager != null) modeManager.tutorialManager = this;

        steps.Clear();
        InitializeDefaultSteps();
    }

    private void LateUpdate()
    {
        if (!IsTutorialActive || stepTransitioning || currentStepIndex >= steps.Count) return;

        TutorialStep step = steps[currentStepIndex];
        CardController expectedCard = FindCardByName(step.expectedCardName);
        var allCards = FindObjectsOfType<CardController>();

        // 1. ЖЕСТКОЕ УПРАВЛЕНИЕ КНОПКАМИ ОТМЕНЫ
        if (undoAllButton != null) undoAllButton.interactable = false;
        if (undoButton != null) undoButton.interactable = (step.expectedAction == TutorialActionType.Undo);

        // 2. УПРАВЛЕНИЕ КЛИКАБЕЛЬНОСТЬЮ КАРТ
        foreach (var c in allCards)
        {
            if (c.canvasGroup == null) continue;

            bool isFlying = c.GetComponentInParent<ICardContainer>() == null;
            if (isFlying) continue;

            if (step.expectedAction == TutorialActionType.MoveCard)
            {
                if (expectedCard != null) c.canvasGroup.blocksRaycasts = (c == expectedCard);
                else c.canvasGroup.blocksRaycasts = false;
            }
            else if (step.expectedAction == TutorialActionType.ClickStock)
            {
                bool isStock = c.GetComponentInParent<SpiderStockPile>() != null;
                if (!isStock) c.canvasGroup.blocksRaycasts = false;
            }
            else if (step.expectedAction == TutorialActionType.Undo)
            {
                c.canvasGroup.blocksRaycasts = false;
            }
        }

        // 3. АВТО-ПЕРЕКЛЮЧЕНИЕ И ПРОВЕРКА МЕСТА ПАДЕНИЯ
        if (expectedCard != null)
        {
            ICardContainer currentContainer = expectedCard.GetComponentInParent<ICardContainer>();

            if (step.expectedAction == TutorialActionType.MoveCard)
            {
                // Если карта оказалась в новом месте
                if (currentContainer != null && currentContainer != initialCardContainer)
                {
                    bool isCorrectDestination = false;

                    if (currentContainer is SpiderTableauPile tp && tp.cards.Contains(expectedCard))
                    {
                        // Находим индекс нашей перетаскиваемой карты в новой стопке
                        int cardIndex = tp.cards.IndexOf(expectedCard);

                        // Для шага с пустой колонкой (Шаг 5) — наша карта должна лежать на самом дне (индекс 0)
                        if (step.localizationKey == "SpiderTutorial5")
                        {
                            if (cardIndex == 0) isCorrectDestination = true;
                        }
                        // Для остальных шагов проверяем карту, которая лежит СТРОГО ПОД нашей (cardIndex - 1)
                        else if (cardIndex > 0)
                        {
                            string underName = tp.cards[cardIndex - 1].gameObject.name;

                            if (step.localizationKey == "SpiderTutorial1" && underName.Contains("Spades_9")) isCorrectDestination = true;
                            else if (step.localizationKey == "SpiderTutorial2" && underName.Contains("Spades_8")) isCorrectDestination = true;
                            else if (step.localizationKey == "SpiderTutorial3" && underName.Contains("Spades_5")) isCorrectDestination = true;

                            // Защита от броска не туда (Шаг 7)
                            else if (step.localizationKey == "SpiderTutorial7" && underName.Contains("Spades_4")) isCorrectDestination = true;

                            else if (step.localizationKey == "SpiderTutorial8" && underName.Contains("Hearts_2")) isCorrectDestination = true;
                            else if (step.localizationKey == "SpiderTutorial9" && underName.Contains("Spades_5")) isCorrectDestination = true;
                            else if (step.localizationKey == "SpiderTutorial10" && underName.Contains("Spades_2")) isCorrectDestination = true;
                        }
                    }

                    // Если место верное - засчитываем шаг!
                    if (isCorrectDestination)
                    {
                        AdvanceStep();
                    }
                    // Если игрок ошибся - мгновенно отменяем ход системным Undo!
                    else
                    {
                        if (modeManager != null && modeManager.undoManager != null)
                        {
                            // Оборачиваем в StartCoroutine, чтобы магия Unity заработала
                            StartCoroutine(modeManager.undoManager.UndoLastCoroutine());
                        }
                    }
                }
            }
            else if (step.expectedAction == TutorialActionType.Undo)
            {
                // Засчитываем шаг "Отмена" только когда карта вернулась назад
                if (currentContainer != null && currentContainer != initialCardContainer)
                {
                    AdvanceStep();
                }
            }
        }
    }

    private IEnumerator AdvanceStepRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        InternalAdvanceStep();
        stepTransitioning = false;
    }

    private CardController FindCardByName(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;
        var allCards = FindObjectsOfType<CardController>();

        foreach (var c in allCards)
        {
            // Игнорируем карты в домах
            if (c.GetComponentInParent<SpiderFoundationPile>() != null) continue;

            // Ищем строгое совпадение полного имени
            if (c.gameObject.name == exactName)
                return c;
        }
        return null;
    }

    // --- ФИКС 3: Реализуем метод, чтобы он реально переключал шаг по запросу из ModeManager ---
    public void AdvanceStep()
    {
        if (!stepTransitioning && IsTutorialActive && gameObject.activeInHierarchy)
        {
            stepTransitioning = true;
            StartCoroutine(AdvanceStepRoutine());
        }
    }
    public bool IsActionAllowed(TutorialActionType action, CardController card = null, ICardContainer target = null)
    {
        if (!IsTutorialActive || currentStepIndex >= steps.Count) return true;

        TutorialStep step = steps[currentStepIndex];

        if (action != step.expectedAction) return false;

        if (action == TutorialActionType.MoveCard)
        {
            // Строго проверяем точное имя перетаскиваемой карты
            if (card != null && card.gameObject.name != step.expectedCardName) return false;

            if (target != null)
            {
                if (step.expectedTargetPileName == "Tableau" && !(target is SpiderTableauPile)) return false;

                if (target is SpiderTableauPile tp)
                {
                    // Для 5-го шага строго требуем пустую колонку
                    if (step.localizationKey == "SpiderTutorial5")
                    {
                        if (tp.cards.Count > 0) return false;
                    }
                    else
                    {
                        // --- НОВОЕ: ЖЕСТКАЯ ПРОВЕРКА ЦЕЛИ ---
                        // Получаем верхнюю карту стопки, на которую игрок пытается бросить карту
                        CardController targetTopCard = tp.cards.Count > 0 ? tp.cards[tp.cards.Count - 1] : null;

                        if (targetTopCard != null)
                        {
                            string topName = targetTopCard.gameObject.name;

                            // Блокируем бросок, если целевая карта не совпадает с задуманной по сценарию
                            if (step.localizationKey == "SpiderTutorial1" && !topName.Contains("Spades_9")) return false;
                            if (step.localizationKey == "SpiderTutorial2" && !topName.Contains("Spades_8")) return false;
                            if (step.localizationKey == "SpiderTutorial3" && !topName.Contains("Spades_5")) return false;

                            // ФИКС ВАШЕЙ ПРОБЛЕМЫ (Шаг 7): разрешаем бросать 3-ку ТОЛЬКО на 4 Пик
                            if (step.localizationKey == "SpiderTutorial7" && !topName.Contains("Spades_4")) return false;

                            if (step.localizationKey == "SpiderTutorial8" && !topName.Contains("Hearts_2")) return false;
                            if (step.localizationKey == "SpiderTutorial9" && !topName.Contains("Spades_5")) return false;
                            if (step.localizationKey == "SpiderTutorial10" && !topName.Contains("Spades_2")) return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    private void InternalAdvanceStep()
    {
        if (!IsTutorialActive) return;

        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            // 1. Обновляем тексты
            string endText = "Поздравляем! Вы освоили все механики Паука!";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string loc = LocalizationManager.instance.GetLocalizedValue("SpiderTutorialEnd");
                if (!string.IsNullOrEmpty(loc)) endText = loc;
            }
            if (instructionText != null) instructionText.text = endText;
            if (stepIndicatorText != null) stepIndicatorText.text = "";
            DisableAllHighlights();

            // --- НОВОЕ: Принудительно отправляем панель вниз при победе ---
            if (tutorialUIPanel != null)
            {
                if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);
                RectTransform targetAnchor = GetAnchorRect(TutorialPanelAnchorType.Bottom);
                panelMoveCoroutine = StartCoroutine(MovePanelRoutine(targetAnchor));
            }

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

        CardController expectedCard = FindCardByName(step.expectedCardName);
        initialCardContainer = expectedCard != null ? expectedCard.GetComponentInParent<ICardContainer>() : null;
        if (modeManager != null && modeManager.pileManager.StockPile != null)
            initialStockCount = modeManager.pileManager.StockPile.Transform.childCount;

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
            panelMoveCoroutine = StartCoroutine(MovePanelRoutine(targetAnchor));
        }

        // --- СТРЕЛОЧКИ ---
        if (arrowAnimCoroutine != null) StopCoroutine(arrowAnimCoroutine);
        if (arrowDown != null) arrowDown.gameObject.SetActive(false);
        if (arrowUp != null) arrowUp.gameObject.SetActive(false);
        if (arrowLeft != null) arrowLeft.gameObject.SetActive(false);

        if (step.arrowType != TutorialArrowType.None && step.arrowAnchorIndex >= 0 && step.arrowAnchorIndex < arrowAnchors.Length)
        {
            RectTransform activeArrow = null;
            if (step.arrowType == TutorialArrowType.Down) activeArrow = arrowDown;
            else if (step.arrowType == TutorialArrowType.Up) activeArrow = arrowUp;
            else if (step.arrowType == TutorialArrowType.Left) activeArrow = arrowLeft;

            RectTransform targetArrowAnchor = arrowAnchors[step.arrowAnchorIndex];

            if (activeArrow != null && targetArrowAnchor != null)
            {
                activeArrow.gameObject.SetActive(true);
                activeArrow.SetParent(targetArrowAnchor, false);
                activeArrow.anchoredPosition = Vector2.zero;
                arrowAnimCoroutine = StartCoroutine(AnimateArrowBounce(activeArrow, step.arrowType));
            }
        }

        // --- ХАЙЛАЙТЫ ---
        activeHighlightsOriginalPos.Clear();
        foreach (var highlight in highlightObjects)
        {
            if (highlight == null) continue;
            bool shouldBeActive = step.highlightElements != null && step.highlightElements.Contains(highlight.name);
            highlight.SetActive(shouldBeActive);

            if (shouldBeActive)
            {
                RectTransform rt = highlight.GetComponent<RectTransform>();
                if (rt != null && !activeHighlightsOriginalPos.ContainsKey(rt))
                {
                    activeHighlightsOriginalPos.Add(rt, rt.anchoredPosition);
                }
            }
        }
    }

    private IEnumerator AnimateArrowBounce(RectTransform arrow, TutorialArrowType type)
    {
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime * arrowBounceSpeed;
            float offset = Mathf.Sin(elapsed) * arrowBounceAmplitude;

            if (type == TutorialArrowType.Down || type == TutorialArrowType.Up)
                arrow.anchoredPosition = new Vector2(0, offset);
            else if (type == TutorialArrowType.Left)
                arrow.anchoredPosition = new Vector2(offset, 0);

            yield return null;
        }
    }

    private RectTransform GetAnchorRect(TutorialPanelAnchorType type)
    {
        switch (type)
        {
            case TutorialPanelAnchorType.Top: return topAnchor;
            case TutorialPanelAnchorType.Bottom: return bottomAnchor;
            case TutorialPanelAnchorType.Center: return centerAnchor;
            default: return centerAnchor;
        }
    }

    private IEnumerator MovePanelRoutine(RectTransform targetRect)
    {
        if (targetRect == null) yield break;

        Vector3 startWorldPos = tutorialUIPanel.position;
        tutorialUIPanel.anchorMin = targetRect.anchorMin;
        tutorialUIPanel.anchorMax = targetRect.anchorMax;
        tutorialUIPanel.pivot = targetRect.pivot;
        tutorialUIPanel.sizeDelta = targetRect.sizeDelta;
        tutorialUIPanel.position = startWorldPos;

        Vector2 startAnchoredPos = tutorialUIPanel.anchoredPosition;
        Vector2 targetAnchoredPos = targetRect.anchoredPosition;

        float distance = Vector2.Distance(startAnchoredPos, targetAnchoredPos);
        float duration = distance > 1000f ? 0.6f : 0.3f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startAnchoredPos, targetAnchoredPos, t);
            yield return null;
        }
        tutorialUIPanel.anchoredPosition = targetAnchoredPos;
    }

    private void DisableAllHighlights()
    {
        foreach (var highlight in highlightObjects) if (highlight != null) highlight.SetActive(false);
        activeHighlightsOriginalPos.Clear();

        if (arrowAnimCoroutine != null) StopCoroutine(arrowAnimCoroutine);
        if (arrowDown != null) arrowDown.gameObject.SetActive(false);
        if (arrowUp != null) arrowUp.gameObject.SetActive(false);
        if (arrowLeft != null) arrowLeft.gameObject.SetActive(false);
    }

    public void HidePanelToLeft()
    {
        if (tutorialUIPanel == null || !tutorialUIPanel.gameObject.activeInHierarchy) return;

        if (panelMoveCoroutine != null) StopCoroutine(panelMoveCoroutine);

        // Отправляем панель за левый край экрана (с анимацией)
        Vector2 targetPos = tutorialUIPanel.anchoredPosition + new Vector2(-2500f, 0);
        panelMoveCoroutine = StartCoroutine(MovePanelOutRoutine(targetPos));

        // Выключаем туториал и снимаем все аппаратные блокировки
        UnlockAllElements();
    }
    private IEnumerator MovePanelOutRoutine(Vector2 targetAnchoredPos)
    {
        Vector2 startAnchoredPos = tutorialUIPanel.anchoredPosition;
        float duration = 0.3f;
        float elapsed = 0f;

        // Используем unscaledDeltaTime, чтобы панель улетала даже если игра на паузе
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            tutorialUIPanel.anchoredPosition = Vector2.Lerp(startAnchoredPos, targetAnchoredPos, t);
            yield return null;
        }

        tutorialUIPanel.anchoredPosition = targetAnchoredPos;
        tutorialUIPanel.gameObject.SetActive(false);
    }
    public void RestorePanelPosition()
    {
        if (!IsTutorialActive) return;
        UpdateUI();
    }

    public void HideHighlights()
    {
        DisableAllHighlights();
        UnlockAllElements();
    }

    private void UnlockAllElements()
    {
        IsTutorialActive = false;

        // 1. Возвращаем к жизни кнопки Отмены
        if (undoAllButton != null) undoAllButton.interactable = true;
        if (undoButton != null) undoButton.interactable = true;

        // 2. Разблокируем кликабельность всех карт на столе
        var allCards = FindObjectsOfType<CardController>();
        foreach (var c in allCards)
        {
            if (c.canvasGroup != null) c.canvasGroup.blocksRaycasts = true;
        }
    }

    // ==========================================
    // СПЕЦИФИКА ПАУКА: ГЕНЕРАЦИЯ СТОЛА
    // ==========================================

    public IEnumerator PlayTutorialIntro(Deal dummyDeal)
    {
        modeManager.IsInputAllowed = false;

        if (tutorialUIPanel) tutorialUIPanel.gameObject.SetActive(false);
        DisableAllHighlights();

        // 1. Эта команда теперь 100% скроет все стопки на столе (благодаря фиксу в IntroController)
        if (modeManager.introController != null) modeManager.introController.SetupIntro(false);

        _tempDeckManager = modeManager.deckManager;
        modeManager.deckManager = null;

        SpiderPileManager pm = modeManager.pileManager;

        foreach (var t in pm.TableauPiles) t.Clear();
        foreach (var f in pm.FoundationPiles) f.ResetFoundation();
        if (pm.StockPile != null) pm.StockPile.Clear();
        _tempDeckManager.cardFactory.DestroyAllCards();

        string[] F_Spades = { "Spades_1_up", "Spades_2_up", "Spades_3_up", "Spades_4_up", "Spades_5_up", "Spades_6_up", "Spades_7_up", "Spades_8_up", "Spades_9_up", "Spades_10_up", "Spades_11_up", "Spades_12_up", "Spades_13_up" };
        string[] F_Hearts = { "Hearts_1_up", "Hearts_2_up", "Hearts_3_up", "Hearts_4_up", "Hearts_5_up", "Hearts_6_up", "Hearts_7_up", "Hearts_8_up", "Hearts_9_up", "Hearts_10_up", "Hearts_11_up", "Hearts_12_up", "Hearts_13_up" };

        SpawnAndPlaceCards(F_Spades, pm.FoundationPiles[0]); pm.FoundationPiles[0].SetCompleted(Suit.Spades);
        SpawnAndPlaceCards(F_Spades, pm.FoundationPiles[1]); pm.FoundationPiles[1].SetCompleted(Suit.Spades);
        SpawnAndPlaceCards(F_Spades, pm.FoundationPiles[2]); pm.FoundationPiles[2].SetCompleted(Suit.Spades);
        SpawnAndPlaceCards(F_Hearts, pm.FoundationPiles[3]); pm.FoundationPiles[3].SetCompleted(Suit.Hearts);
        SpawnAndPlaceCards(F_Hearts, pm.FoundationPiles[4]); pm.FoundationPiles[4].SetCompleted(Suit.Hearts);
        SpawnAndPlaceCards(F_Hearts, pm.FoundationPiles[5]); pm.FoundationPiles[5].SetCompleted(Suit.Hearts);

        // --- СООБЩАЕМ ПАУКУ О СЖАТИИ ---
        if (modeManager != null) modeManager.UpdateTableauLayouts();

        string[] T0 = { "Hearts_13_up", "Hearts_12_up", "Hearts_11_up", "Hearts_10_up", "Hearts_9_up", "Hearts_8_up", "Hearts_7_up", "Hearts_6_up", "Hearts_5_up", "Hearts_4_up", "Hearts_3_up", "Hearts_2_up" };
        string[] T1 = { "Spades_13_up", "Spades_12_up", "Spades_11_up", "Spades_10_up", "Spades_9_up", "Spades_8_up", "Spades_7_up", "Spades_6_up", "Spades_5_up", "Spades_4_up", "Spades_3_up", "Spades_2_up" };
        string[] T2 = { "Spades_10_down", "Spades_8_up_step1" };
        string[] T3 = { "Hearts_10_down", "Spades_9_up" };
        string[] T4 = { "Spades_11_down", "Spades_7_up_step2", "Spades_6_up", "Spades_5_up" };
        string[] T5 = { "Hearts_11_down", "Hearts_4_up_step3" };
        string[] T6 = { "Spades_12_down", "Spades_13_up_step5" };
        string[] T7 = { "Hearts_12_down", "Hearts_11_up" };
        string[] T8 = { "Spades_12_down", "Hearts_12_up" };
        string[] T9 = { };
        string[] Stock = { "Hearts_10_down", "Spades_10_down", "Hearts_11_down", "Spades_11_down", "Spades_5_down_dummy", "Spades_4_down_dummy", "Spades_1_down_step10", "Hearts_1_down_step8", "Hearts_4_down_step9", "Spades_3_down_step7" };

        SpawnAndPlaceCards(T0, pm.TableauPiles[0]);
        SpawnAndPlaceCards(T1, pm.TableauPiles[1]);
        SpawnAndPlaceCards(T2, pm.TableauPiles[2]);
        SpawnAndPlaceCards(T3, pm.TableauPiles[3]);
        SpawnAndPlaceCards(T4, pm.TableauPiles[4]);
        SpawnAndPlaceCards(T5, pm.TableauPiles[5]);
        SpawnAndPlaceCards(T6, pm.TableauPiles[6]);
        SpawnAndPlaceCards(T7, pm.TableauPiles[7]);
        SpawnAndPlaceCards(T8, pm.TableauPiles[8]);
        SpawnAndPlaceCards(T9, pm.TableauPiles[9]);
        SpawnAndPlaceCards(Stock, pm.StockPile);

        // --- ФИКС СЖАТИЯ: Принудительно и мгновенно пересчитываем макеты ---
        foreach (var tPile in pm.TableauPiles)
        {
            if (tPile != null)
            {
                // Останавливаем плавную анимацию Паука и заставляем принять нужный отступ сразу
                tPile.StopAllCoroutines();
                tPile.ForceRecalculateLayout();
            }
        }

        Canvas.ForceUpdateCanvases();

        // Ждем всего 1 кадр, чтобы Unity обновила UI. Мелькания не будет (стол прозрачный)
        yield return null;

        List<ICardContainer> animSequence = new List<ICardContainer> {
            pm.TableauPiles[0], pm.TableauPiles[1], pm.TableauPiles[2], pm.TableauPiles[3], pm.TableauPiles[4],
            pm.TableauPiles[5], pm.TableauPiles[6], pm.TableauPiles[7], pm.TableauPiles[8], pm.TableauPiles[9],
            pm.StockPile,
            pm.FoundationPiles[0], pm.FoundationPiles[1], pm.FoundationPiles[2],
            pm.FoundationPiles[3], pm.FoundationPiles[4], pm.FoundationPiles[5]
        };

        Vector2 offScreenOffset = new Vector2(0, 1500f);
        Dictionary<CardController, Vector2> targetAnchors = new Dictionary<CardController, Vector2>();

        // Переносим карты за экран
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
                    card.rectTransform.anchoredPosition += offScreenOffset;
                }
            }
        }

        // Запускаем Intro. Оно плавно проявит стопки (FadeInSlots) на экране
        if (modeManager.introController != null) yield return StartCoroutine(modeManager.introController.PlayIntro(false));

        modeManager.deckManager = _tempDeckManager;

        // Эффектное каскадное падение уже видимых карт на видимый стол
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
            if (cardsInStack.Count > 0) StartCoroutine(AnimateStackDrop(cardsInStack, targetAnchors, offScreenOffset, 0.3f));
            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(0.3f);
        modeManager.AnimationService.ReorderAllContainers(pm.GetAllContainerTransforms());

        if (StatisticsManager.Instance != null) StatisticsManager.Instance.OnGameStarted("Spider", Difficulty.Easy, "Tutorial");

        currentStepIndex = 0;
        DisableAllHighlights();

        if (tutorialUIPanel)
        {
            RectTransform targetAnchor = GetAnchorRect(steps.Count > 0 ? steps[0].panelAnchor : TutorialPanelAnchorType.Bottom);
            if (targetAnchor != null)
            {
                tutorialUIPanel.anchorMin = targetAnchor.anchorMin;
                tutorialUIPanel.anchorMax = targetAnchor.anchorMax;
                tutorialUIPanel.pivot = targetAnchor.pivot;
                tutorialUIPanel.sizeDelta = targetAnchor.sizeDelta;
                tutorialUIPanel.anchoredPosition = targetAnchor.anchoredPosition + new Vector2(2500f, 0);
            }
            tutorialUIPanel.gameObject.SetActive(true);
        }

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
            t = 1f - Mathf.Pow(1f - t, 3f);
            foreach (var card in cardsInStack) if (card != null) card.rectTransform.anchoredPosition = Vector2.Lerp(targetAnchors[card] + offScreenOffset, targetAnchors[card], t);
            yield return null;
        }
        foreach (var card in cardsInStack) if (card != null) card.rectTransform.anchoredPosition = targetAnchors[card];
    }

    private void SpawnAndPlaceCards(string[] cardData, ICardContainer target)
    {
        foreach (var data in cardData)
        {
            string[] parts = data.Split('_');
            string suitStr = parts[0];
            int rank = int.Parse(parts[1]);
            bool isFaceUp = parts[2] == "up";
            string customId = parts.Length > 3 ? parts[3] : "";

            Suit suit = Suit.Spades;
            if (suitStr == "Hearts") suit = Suit.Hearts;
            else if (suitStr == "Clubs") suit = Suit.Clubs;
            else if (suitStr == "Diamonds") suit = Suit.Diamonds;

            CardModel model = new CardModel(suit, rank);
            CardController card = _tempDeckManager.cardFactory.CreateCard(model, ((MonoBehaviour)target).transform, Vector2.zero);

            card.gameObject.name = string.IsNullOrEmpty(customId) ? $"Card_{suitStr}_{rank}" : $"Card_{suitStr}_{rank}_{customId}";

            FindObjectOfType<DragManager>()?.RegisterCardEvents(card);

            if (target is SpiderTableauPile tableau)
            {
                tableau.AddCard(card, isFaceUp);
            }
            else if (target is SpiderStockPile stock)
            {
                stock.AddCard(card, isFaceUp);
                card.GetComponent<CardData>()?.SetFaceUp(isFaceUp, false);
            }
            else
            {
                var add2 = target.GetType().GetMethod("AddCard", new System.Type[] { typeof(CardController), typeof(bool) });
                if (add2 != null) add2.Invoke(target, new object[] { card, isFaceUp });
                else target.GetType().GetMethod("AddCard", new System.Type[] { typeof(CardController) })?.Invoke(target, new object[] { card });
                card.GetComponent<CardData>()?.SetFaceUp(isFaceUp, false);
            }
        }

        if (target is SpiderTableauPile tPile) { tPile.StopAllCoroutines(); tPile.ForceRecalculateLayout(); }
        else if (target is SpiderStockPile sPile) { sPile.StopAllCoroutines(); sPile.ForceRecalculateLayout(); }
    }

    // ==========================================
    // СПЕЦИФИКА ПАУКА: СЦЕНАРИЙ ШАГОВ
    // ==========================================

    private void InitializeDefaultSteps()
    {
        // 1. Обычный перенос (8 на 9)
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial1",
            fallbackText = "Перетащите 8 Пик на 9 Пик.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Spades_8_step1", // Точное имя!
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_Tableau" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 0
        });

        // 2. Перенос стопки (7-6-5 на 8)
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial2",
            fallbackText = "Перенесите стопку 7-6-5 Пик на 8 Пик.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Spades_7_step2",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 1
        });

        // 3. Создание разномастной стопки (4♥ на 5♠)
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial3",
            fallbackText = "Положите 4 Червей на 5 Пик.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Hearts_4_step3",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 2
        });

        // 4. Отмена хода
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial4",
            fallbackText = "Нажмите 'Отмена'.",
            expectedAction = TutorialActionType.Undo,
            expectedCardName = "Card_Hearts_4_step3",
            expectedTargetPileName = "",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_UndoButton" },
            arrowType = TutorialArrowType.None
        });

        // 5. Заполнение пустой колонки (K♠ в пустую)
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial5",
            fallbackText = "Перенесите Короля Пик в пустую колонку.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Spades_13_step5",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Center,
            highlightElements = new List<string> { "Highlight_EmptyTableau" },
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 3
        });

        // 6. Раздача
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial6",
            fallbackText = "Кликните по колоде.",
            expectedAction = TutorialActionType.ClickStock,
            expectedCardName = "",
            expectedTargetPileName = "",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string> { "Highlight_Stock" },
            arrowType = TutorialArrowType.None
        });

        // 7. Расчистка после раздачи (3♠ на 4♠)
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial7",
            fallbackText = "Уберите 3 Пик на 4 Пик.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Spades_3_step7",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 4
        });

        // 8. Собираем 7-й дом (A♥ на 2♥)
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial8",
            fallbackText = "Перенесите Туза Червей на 2 Червей.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Hearts_1_step8",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 0
        });

        // 9. Расчистка (4♥ на 5♠)
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial9",
            fallbackText = "Уберите 4 Червей на 5 Пик.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Hearts_4_step9", // 4 Червей с раздачи
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 6
        });

        // 10. ПОБЕДНЫЙ ХОД (A♠ на 2♠)
        steps.Add(new TutorialStep
        {
            localizationKey = "SpiderTutorial10",
            fallbackText = "Перенесите Туз Пик на 2 Пик.",
            expectedAction = TutorialActionType.MoveCard,
            expectedCardName = "Card_Spades_1_step10",
            expectedTargetPileName = "Tableau",
            panelAnchor = TutorialPanelAnchorType.Top,
            highlightElements = new List<string>(),
            arrowType = TutorialArrowType.Up,
            arrowAnchorIndex = 7
        });
    }
}