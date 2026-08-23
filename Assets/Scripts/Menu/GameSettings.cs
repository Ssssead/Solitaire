using UnityEngine;
public static class GameSettings
{
    // --- Основные ---
    public static GameType CurrentGameType;
    public static Difficulty CurrentDifficulty = Difficulty.Medium;

    // --- Klondike ---
    public static int KlondikeDrawCount = 1; // 1 или 3

    // --- Spider ---
    public static int SpiderSuitCount = 1; // 1, 2 или 4

    // --- Pyramid / TriPeaks ---
    public static int RoundsCount = 1; // 1, 2 или 3

    // --- Yukon ---
    public static bool YukonRussian = false; // false = Classic, true = Russian

    // --- Monte Carlo ---
    public static bool MonteCarlo4Ways = false; // false = 8 Ways, true = 4 Ways

    // --- Montana ---
    public static bool MontanaHard = false; // false = Classic, true = Hard
    public static bool IsTutorialMode = false;

    // --- НОВЫЙ МЕТОД: Единый генератор названия режима ---
    public static string GetCurrentVariantString(GameType type)
    {
        switch (type)
        {
            case GameType.Klondike: return KlondikeDrawCount == 3 ? "Draw3" : "Draw1";
            case GameType.Spider: return SpiderSuitCount == 4 ? "4Suits" : (SpiderSuitCount == 2 ? "2Suits" : "1Suit");
            case GameType.Pyramid:
            case GameType.TriPeaks: return RoundsCount.ToString() + "Rounds";
            case GameType.Yukon: return YukonRussian ? "Russian" : "Classic";
            case GameType.MonteCarlo: return MonteCarlo4Ways ? "4Ways" : "8Ways";
            case GameType.Montana: return MontanaHard ? "Hard" : "Standard"; // Montana теперь использует Standard

            // FreeCell, Sultan, Octagon не имеют режимов, поэтому всегда "Standard"
            default: return "Standard";
        }
    }
    public static int AutoMoveClickMode
    {
        get
        {
            if (!PlayerPrefs.HasKey("AutoMoveClickMode"))
            {
                // По умолчанию: Десктоп = 1 (Двойной), Мобилки = 0 (Одинарный)
                return YG.YG2.envir.isDesktop ? 1 : 0;
            }
            return PlayerPrefs.GetInt("AutoMoveClickMode");
        }
        set
        {
            PlayerPrefs.SetInt("AutoMoveClickMode", value);
            PlayerPrefs.Save();
        }
    }
}

public enum GameType
{
    Klondike,
    Spider,
    FreeCell,
    Pyramid,
    TriPeaks,
    Octagon,
    Sultan,
    Montana,
    Yukon,
    MonteCarlo
}