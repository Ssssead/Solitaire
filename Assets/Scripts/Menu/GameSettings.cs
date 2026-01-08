// GameSettings.cs
public static class GameSettings
{
    // Тип выбранной игры
    public static GameType CurrentGameType;

    // Общие настройки
    public static Difficulty CurrentDifficulty = Difficulty.Medium;

    // Настройки для Klondike
    public static int KlondikeDrawCount = 1; // 1 или 3

    // Настройки для будущего Spider (пример)
    public static int SpiderSuitCount = 1;
}

// Перечисление всех ваших будущих игр
public enum GameType
{
    Klondike,
    Spider,
    FreeCell,
    Pyramid,
    TriPeaks,
    Octagon,
    Sultan,
    MulticoloredStar,
    Yukon,
    MonteCarlo
}