using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class StatisticsUI : MonoBehaviour
{
    [Header("UI Header")]
    public TMP_Text headerText;

    [Header("Global Stats References")]
    public StatBinder globalBinder;

    [Header("Difficulty Stats References")]
    public DifficultyGroupBinder difficultyBinder;

    [Header("Variant Selector")]
    public GameObject selectorContainer;
    public TMP_Text variantLabel;
    public Button prevButton;
    public Button nextButton;

    [Header("Assets")]
    public Sprite winSprite;
    public Sprite lossSprite;
    public Sprite emptySprite;

    // Состояние
    private GameType currentGame;
    private int currentVariantIndex = 0;

    // Списки вариантов
    private List<string> variantKeys = new List<string>();
    private List<string> variantLabels = new List<string>();

    private void Start()
    {
        if (prevButton) prevButton.onClick.AddListener(OnPrevVariant);
        if (nextButton) nextButton.onClick.AddListener(OnNextVariant);
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    // --- ГЛАВНЫЙ МЕТОД ---
    public void ShowStatsForGame(GameType gameType)
    {
        currentGame = gameType;
        if (headerText) headerText.text = gameType.ToString() + " Stats";

        SetupVariants();
        RefreshUI();
    }
    // ---------------------

    private void SetupVariants()
    {
        variantKeys.Clear();
        variantLabels.Clear();
        currentVariantIndex = 0;

        switch (currentGame)
        {
            // --- 1. KLONDIKE ---
            case GameType.Klondike:
                variantKeys.Add("Draw1"); variantLabels.Add("Draw 1");
                variantKeys.Add("Draw3"); variantLabels.Add("Draw 3");
                break;

            // --- 2. SPIDER ---
            case GameType.Spider:
                variantKeys.Add("1Suit"); variantLabels.Add("1 Suit");
                variantKeys.Add("2Suits"); variantLabels.Add("2 Suits");
                variantKeys.Add("4Suits"); variantLabels.Add("4 Suits");
                break;

            // --- 3. FREECELL ---
            case GameType.FreeCell:
                variantKeys.Add("Standard"); variantLabels.Add("Standard");
                break;

            // --- 4. PYRAMID ---
            case GameType.Pyramid:
                variantKeys.Add("1"); variantLabels.Add("1 Round");
                variantKeys.Add("2"); variantLabels.Add("2 Rounds");
                variantKeys.Add("3"); variantLabels.Add("3 Rounds");
                break;

            // --- 5. TRIPEAKS ---
            case GameType.TriPeaks:
                // TriPeaksModeManager сохраняет как "1Rounds", "2Rounds"
                // Исправляем ключи здесь, чтобы они совпадали с записью
                variantKeys.Add("1Rounds"); variantLabels.Add("1 Round");
                variantKeys.Add("2Rounds"); variantLabels.Add("2 Rounds");
                variantKeys.Add("3Rounds"); variantLabels.Add("3 Rounds");
                break;

            // --- 6. YUKON ---
            case GameType.Yukon:
                variantKeys.Add("Classic"); variantLabels.Add("Classic");
                variantKeys.Add("Russian"); variantLabels.Add("Russian");
                break;

            // --- 7. MONTE CARLO ---
            case GameType.MonteCarlo:
                variantKeys.Add("8Ways"); variantLabels.Add("8 Ways");
                variantKeys.Add("4Ways"); variantLabels.Add("4 Ways");
                break;

            // --- 8. SULTAN ---
            case GameType.Sultan:
                variantKeys.Add("Standard"); variantLabels.Add("Standard");
                break;

            // --- 9. OCTAGON ---
            case GameType.Octagon:
                variantKeys.Add("Standard"); variantLabels.Add("Standard");
                break;

            // --- 10. Montana ---
            case GameType.Montana:
                variantKeys.Add("Standard"); variantLabels.Add("Standard");
                break;

            // --- DEFAULT ---
            default:
                variantKeys.Add("Standard"); variantLabels.Add("Standard");
                break;
        }

        // Показываем кнопки переключения, только если вариантов больше 1
        if (selectorContainer) selectorContainer.SetActive(variantKeys.Count > 1);

        UpdateVariantLabel();
    }

    private void OnPrevVariant()
    {
        if (variantKeys.Count <= 1) return;
        currentVariantIndex--;
        if (currentVariantIndex < 0) currentVariantIndex = variantKeys.Count - 1;
        UpdateVariantLabel();
        RefreshUI();
    }

    private void OnNextVariant()
    {
        if (variantKeys.Count <= 1) return;
        currentVariantIndex++;
        if (currentVariantIndex >= variantKeys.Count) currentVariantIndex = 0;
        UpdateVariantLabel();
        RefreshUI();
    }

    private void UpdateVariantLabel()
    {
        if (variantLabel && variantLabels.Count > currentVariantIndex)
        {
            variantLabel.text = variantLabels[currentVariantIndex];
        }
    }

    private void RefreshUI()
    {
        if (StatisticsManager.Instance == null) return;
        if (variantKeys.Count == 0) return;

        string currentVariantKey = variantKeys[currentVariantIndex];
        string gameName = currentGame.ToString();

        // Заполняем слоты сложностей
        FillBinder(difficultyBinder.easy, StatisticsManager.Instance.GetStats(gameName, Difficulty.Easy, currentVariantKey), HistorySlotHover.SlotType.Difficulty);
        FillBinder(difficultyBinder.medium, StatisticsManager.Instance.GetStats(gameName, Difficulty.Medium, currentVariantKey), HistorySlotHover.SlotType.Difficulty);
        FillBinder(difficultyBinder.hard, StatisticsManager.Instance.GetStats(gameName, Difficulty.Hard, currentVariantKey), HistorySlotHover.SlotType.Difficulty);

        // Заполняем глобальную статистику
        StatData globalData = StatisticsManager.Instance.GetGameGlobalStats(gameName);
        FillBinder(globalBinder, globalData, HistorySlotHover.SlotType.GameGlobal);
    }

    private void FillBinder(StatBinder binder, StatData data, HistorySlotHover.SlotType slotType)
    {
        if (data == null) data = new StatData();

        SetText(binder.gamesPlayed, data.gamesStarted);
        SetText(binder.wins, data.gamesWon);

        SetText(binder.score, data.bestScore);
        SetText(binder.time, FormatTime(data.bestTime));
        SetText(binder.moves, (data.fewestMoves == 0 || data.fewestMoves == int.MaxValue) ? "-" : data.fewestMoves.ToString());

        SetText(binder.winRate, $"{data.WinRate:F0}%");

        SetText(binder.avgTime, FormatTime(data.AvgTime));
        SetText(binder.avgMoves, $"{data.AvgMoves:F0}");
        SetText(binder.currentStreak, data.currentStreak);
        SetText(binder.bestStreak, data.bestStreak);

        // --- ИСТОРИЯ ---
        if (binder.historySlots != null && binder.historySlots.Length > 0)
        {
            int historyCount = data.history.Count;

            for (int i = 0; i < binder.historySlots.Length; i++)
            {
                Image img = binder.historySlots[i];
                if (img == null) continue;

                int dataIndex = historyCount - 1 - i;

                HistorySlotHover hover = img.GetComponent<HistorySlotHover>();
                if (hover == null) hover = img.gameObject.AddComponent<HistorySlotHover>();

                if (dataIndex >= 0)
                {
                    GameHistoryEntry entry = data.history[dataIndex];
                    img.sprite = entry.won ? winSprite : lossSprite;
                    img.color = Color.white;
                    hover.Setup(entry, slotType);
                }
                else
                {
                    img.sprite = emptySprite;
                    img.color = (emptySprite == null) ? new Color(0, 0, 0, 0) : new Color(1, 1, 1, 0.5f);
                    hover.Setup(null, slotType);
                }
            }
        }
    }

    private void SetText(TMP_Text textParams, object value)
    {
        if (textParams != null) textParams.text = value.ToString();
    }

    private string FormatTime(float s)
    {
        if (s == 0 || s > 360000) return "-";

        if (s > 3600)
        {
            int hours = Mathf.FloorToInt(s / 3600);
            int minutes = Mathf.FloorToInt((s % 3600) / 60);
            return $"{hours}h {minutes}m";
        }

        return $"{(int)(s / 60)}:{(int)(s % 60):00}";
    }
}

// --- ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ---

[System.Serializable]
public class StatBinder
{
    [Header("Basic")]
    public TMP_Text gamesPlayed;
    public TMP_Text wins;

    [Header("Records")]
    public TMP_Text score;
    public TMP_Text time;
    public TMP_Text moves;

    [Header("Performance")]
    public TMP_Text winRate;
    public TMP_Text avgTime;
    public TMP_Text avgMoves;

    [Header("Streaks")]
    public TMP_Text currentStreak;
    public TMP_Text bestStreak;

    [Header("History (Drag 10 Images here)")]
    public Image[] historySlots;
}

[System.Serializable]
public class DifficultyGroupBinder
{
    public StatBinder easy;
    public StatBinder medium;
    public StatBinder hard;
}