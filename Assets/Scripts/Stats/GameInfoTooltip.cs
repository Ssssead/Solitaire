using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameInfoTooltip : MonoBehaviour
{
    public static GameInfoTooltip Instance;

    [Header("UI References")]
    public TMP_Text difficultyText;
    public TMP_Text scoreText;
    public TMP_Text timeText;
    public TMP_Text movesText;
    public TMP_Text modeValueText;


    [Header("Mode Info Settings")]
    [Tooltip("Весь объект строки Mode (чтобы скрывать его для других игр)")]
    public GameObject modeRow;

    [Header("Settings")]
    // Смещение: X=50 сдвинет вправо, Y=-50 сдвинет вниз (зависит от Canvas)
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
        // Следование за мышкой
        if (gameObject.activeSelf)
        {
            MoveToMouse();
        }
    }

    private void MoveToMouse()
    {
        Vector2 mousePos = Input.mousePosition;

        // Прибавляем смещение
        // (Убедитесь, что в Инспекторе Offset X > 0, чтобы панель была справа)
        Vector2 finalPos = mousePos + offset;

        transform.position = finalPos;

        // Опционально: проверка, чтобы не улетало за экран
        // (Можно добавить позже, если понадобится)
    }

    public void ShowTooltip(GameHistoryEntry data)
    {
        if (data == null) return;

        // Заполняем основные данные
        if (difficultyText) difficultyText.text = data.difficulty;
        if (scoreText) scoreText.text = data.score.ToString();
        if (timeText) timeText.text = FormatTime(data.time);
        if (movesText) movesText.text = data.moves.ToString();

        // --- ИСПРАВЛЕНИЕ: Передаем data целиком ---
        UpdateModeLine(data);
        // ------------------------------------------

        MoveToMouse();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }
    private void UpdateModeLine(GameHistoryEntry data)
    {
        if (modeRow == null || modeValueText == null) return;

        // Проверяем имя игры из истории
        // (Используем строки, так как в JSON сохраняются строки)
        if (data.gameName == "Klondike" || data.gameName == "Spider")
        {
            // Если вариант записан (например "1Suit" или "Draw 3"), показываем его
            if (!string.IsNullOrEmpty(data.variant))
            {
                modeRow.SetActive(true);

                // Можно добавить красоту для текста
                string textToShow = data.variant;

                // Например, превратить "1Suit" в "1 Suit" (опционально)
                if (textToShow == "1Suit") textToShow = "1 Suit";
                if (textToShow == "2Suits") textToShow = "2 Suits";
                if (textToShow == "4Suits") textToShow = "4 Suits";
                if (textToShow == "Draw1") textToShow = "Draw 1";
                if (textToShow == "Draw3") textToShow = "Draw 3";

                modeValueText.text = textToShow;
            }
            else
            {
                // Если данных о варианте нет (старые сохранения), скрываем
                modeRow.SetActive(false);
            }
        }
        else
        {
            // Для других игр скрываем
            modeRow.SetActive(false);
        }
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