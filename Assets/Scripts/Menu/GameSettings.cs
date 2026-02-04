// GameSettings.cs
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