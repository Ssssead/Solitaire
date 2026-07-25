using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameInfoTooltip : MonoBehaviour
{
    public static GameInfoTooltip Instance;

    [Header("UI References")]
    [Tooltip("Объект строки Названия игры (Скрывается в статистике конкретной игры)")]
    public GameObject gameNameRow;
    public TMP_Text gameNameValueText;

    [Tooltip("Объект строки Режима игры (Скрывается, если режимов нет)")]
    public GameObject modeRow;
    public TMP_Text modeValueText;

    [Space]
    public TMP_Text difficultyText;
    public TMP_Text scoreText;
    public TMP_Text timeText;
    public TMP_Text movesText;

    [Header("Settings")]
    public Vector2 offset = new Vector2(20f, -20f);

    private RectTransform rectTransform;
    private Canvas parentCanvas;

    private void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        // Скрываем при старте
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // Следование за мышкой, пока объект активен
        if (gameObject.activeSelf)
        {
            MoveToMouse();
        }
    }

    private void MoveToMouse()
    {
        if (rectTransform == null || parentCanvas == null) return;

        Vector2 mousePos = Input.mousePosition;
        Vector2 finalPos = mousePos + offset;

        // Размеры тултипа с учетом масштаба канваса
        float width = rectTransform.rect.width * parentCanvas.scaleFactor;
        float height = rectTransform.rect.height * parentCanvas.scaleFactor;

        // --- ПРОВЕРКА ГРАНИЦ ЭКРАНА ---
        // (Предполагается, что Pivot панели установлен на X:0, Y:1)

        // Если вылезает за правый край экрана — отзеркаливаем влево от курсора
        if (finalPos.x + width > Screen.width)
        {
            finalPos.x = mousePos.x - width - offset.x;
        }

        // Если вылезает за нижний край экрана — поднимаем вверх
        if (finalPos.y - height < 0)
        {
            finalPos.y = mousePos.y + height + Mathf.Abs(offset.y);
        }

        transform.position = finalPos;
    }

    public void ShowTooltip(GameHistoryEntry data, HistorySlotHover.SlotType slotType)
    {
        if (data == null) return;

        // 1. НАСТРОЙКА СТРОКИ "НАЗВАНИЕ ИГРЫ"
        if (slotType == HistorySlotHover.SlotType.AppGlobal)
        {
            if (gameNameRow != null) gameNameRow.SetActive(true);
            if (gameNameValueText != null) gameNameValueText.text = GetLocalizedGameName(data.gameName);
        }
        else
        {
            if (gameNameRow != null) gameNameRow.SetActive(false);
        }

        // 2. НАСТРОЙКА СТРОКИ "РЕЖИМ ИГРЫ"
        UpdateModeLine(data);

        // 3. БАЗОВЫЕ ДАННЫЕ
        if (difficultyText) difficultyText.text = GetLocalizedDifficulty(data.difficulty);
        if (scoreText) scoreText.text = data.score.ToString("N0");
        if (timeText) timeText.text = FormatTime(data.time);
        if (movesText) movesText.text = data.moves.ToString();

        // 4. ВКЛЮЧЕНИЕ И ОБНОВЛЕНИЕ ВЕРСТКИ
        gameObject.SetActive(true);

        if (rectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        MoveToMouse();
    }

    private void UpdateModeLine(GameHistoryEntry data)
    {
        if (modeRow == null || modeValueText == null) return;

        if (!string.IsNullOrEmpty(data.variant) && data.variant != "Standard")
        {
            modeRow.SetActive(true);

            string locKey = data.variant;
            string fallbackText = data.variant;

            // Сопоставляем внутреннее имя с ключами локализации и дефолтным текстом
            switch (data.variant)
            {
                // Klondike
                case "Draw1": locKey = "Draw1"; fallbackText = "Draw 1"; break;
                case "Draw3": locKey = "Draw3"; fallbackText = "Draw 3"; break;
                // Spider
                case "1Suit": locKey = "Suit1"; fallbackText = "1 Suit"; break;
                case "2Suits": locKey = "Suit2"; fallbackText = "2 Suits"; break;
                case "4Suits": locKey = "Suit4"; fallbackText = "4 Suits"; break;
                // Pyramid / TriPeaks
                case "1Rounds": locKey = "Round1"; fallbackText = "1 Round"; break;
                case "2Rounds": locKey = "Round2"; fallbackText = "2 Rounds"; break;
                case "3Rounds": locKey = "Round3"; fallbackText = "3 Rounds"; break;
                // Monte Carlo
                case "8Ways": locKey = "8Ways"; fallbackText = "8 Ways"; break;
                case "4Ways": locKey = "4Ways"; fallbackText = "4 Ways"; break;
                // Yukon / Montana
                case "Russian": locKey = "Russian"; fallbackText = "Russian"; break;
                case "Classic": locKey = "Classic"; fallbackText = "Classic"; break;
                case "Hard": locKey = "DiffHard"; fallbackText = "Hard"; break;
            }

            string textToShow = fallbackText;

            // Пробуем получить локализацию
            if (LocalizationManager.instance != null)
            {
                string locTry = LocalizationManager.instance.GetLocalizedValue(locKey);
                // Проверяем, что локализация нашлась и не вернула дефолтный MISSING_TEXT
                if (!string.IsNullOrEmpty(locTry) && locTry != locKey && locTry != "Localized text not found")
                {
                    textToShow = locTry;
                }
            }

            modeValueText.text = textToShow;
        }
        else
        {
            modeRow.SetActive(false);
        }
    }

    private string GetLocalizedGameName(string gameName)
    {
        string locKey = gameName;
        if (locKey == "FreeCell") locKey = "Freecell";

        if (LocalizationManager.instance != null)
        {
            string locTry = LocalizationManager.instance.GetLocalizedValue(locKey);
            if (!string.IsNullOrEmpty(locTry) && locTry != locKey && locTry != "Localized text not found") return locTry;
        }
        return gameName;
    }

    private string GetLocalizedDifficulty(string diff)
    {
        if (LocalizationManager.instance != null)
        {
            string locKey = "Diff" + diff;
            string locTry = LocalizationManager.instance.GetLocalizedValue(locKey);
            if (!string.IsNullOrEmpty(locTry) && locTry != locKey && locTry != "Localized text not found") return locTry;
        }
        return diff;
    }

    public void HideTooltip()
    {
        gameObject.SetActive(false);
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60F);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60F);
        return string.Format("{0}:{1:00}", minutes, seconds);
    }
}