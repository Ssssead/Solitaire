using UnityEngine;

public static class LevelingUtils
{
    // --- Константы базового опыта ---
    public const int BASE_XP_EASY = 100;
    public const int BASE_XP_MEDIUM = 200;
    public const int BASE_XP_HARD = 400;

    // --- Кривая опыта ---
    // Формула: Target = Base * Level * Multiplier. 
    // Пример: 1->2 (1000xp), 2->3 (1200xp)...
    private const int LEVEL_BASE_TARGET = 1000;
    private const float LEVEL_GROWTH_FACTOR = 1.2f;

    // --- Бонус за мастерство (каждые 10 уровней) ---
    // На сколько процентов растет опыт каждые 10 уровней конкретной игры
    // Например 0.1f = +10%
    public const float MASTERY_BONUS_PER_TIER = 0.1f;
    public const float MULTIPLIER_PREMIUM = 1.2f;

    /// <summary>
    /// Рассчитывает необходимое кол-во XP для достижения следующего уровня
    /// </summary>
    public static int GetXPForNextLevel(int currentLevel)
    {
        // Простая формула: 1000 + (уровень * 500). Можно усложнить.
        // Для 1 уровня нужно 1000, для 2 -> 1500, и т.д.
        if (currentLevel <= 0) currentLevel = 1;
        return Mathf.RoundToInt(LEVEL_BASE_TARGET + (currentLevel * 500));
    }

    /// <summary>
    /// Главный метод расчета полученного опыта
    /// </summary>
    /// <param name="gameType">Тип игры</param>
    /// <param name="gameLevel">ТЕКУЩИЙ уровень игрока в этой игре (для бонуса мастерства)</param>
    /// <param name="difficulty">Сложность</param>
    /// <param name="variant">Строка варианта (Draw3, 4Suit, и т.д.)</param>
    /// <param name="isPremium">Есть ли премиум</param>
    public static int CalculateXP(GameType gameType, int gameLevel, Difficulty difficulty, string variant, bool isPremium)
    {
        // 1. База от сложности
        float xp = 0;
        switch (difficulty)
        {
            case Difficulty.Easy: xp = BASE_XP_EASY; break;
            case Difficulty.Medium: xp = BASE_XP_MEDIUM; break;
            case Difficulty.Hard: xp = BASE_XP_HARD; break;
        }

        // 2. Модификаторы конкретных игр и режимов
        float modeMultiplier = GetModeMultiplier(gameType, variant);
        xp *= modeMultiplier;

        // 3. Бонус мастерства (НОВОЕ: каждые 10 уровней игры)
        // Если уровень 0-9 -> бонус 0%. 10-19 -> 10%. 20-29 -> 20%.
        int masteryTier = gameLevel / 10;
        float masteryMultiplier = 1.0f + (masteryTier * MASTERY_BONUS_PER_TIER);
        xp *= masteryMultiplier;

        // 4. Премиум
        if (isPremium)
        {
            xp *= MULTIPLIER_PREMIUM;
        }

        return Mathf.RoundToInt(xp);
    }

    private static float GetModeMultiplier(GameType game, string variant)
    {
        // Нормализация строки для надежности
        variant = variant.ToLower();

        switch (game)
        {
            case GameType.Klondike:
                // Draw 3 сложнее/дольше, даем бонус
                if (variant.Contains("draw3")) return 1.5f;
                return 1.0f; // Draw 1

            case GameType.Spider:
                if (variant.Contains("4suit")) return 2.5f;
                if (variant.Contains("2suit")) return 1.5f;
                return 1.0f; // 1 suit

            case GameType.FreeCell:
                return 1.0f; // Нет режимов

            case GameType.Pyramid:
                // Режимы: раунды. Больше раундов = больше времени = больше опыта
                if (variant.Contains("3")) return 3.0f; // 3 Rounds
                if (variant.Contains("2")) return 2.0f; // 2 Rounds
                return 1.0f; // 1 Round

            case GameType.TriPeaks:
                if (variant.Contains("3")) return 3.0f;
                if (variant.Contains("2")) return 2.0f;
                return 1.0f;

            case GameType.Yukon:
                if (variant.Contains("russian")) return 1.5f; // Russian usually harder
                return 1.0f; // Classic

            case GameType.Sultan:
                return 1.0f;

            case GameType.Octagon:
                return 1.0f;

            case GameType.MonteCarlo:
                // 4 Ways сложнее чем 8 Ways (обычно 8 ways проще убирать карты)
                // Или наоборот, зависит от вашей реализации. Допустим 4 Ways сложнее:
                if (variant.Contains("4ways")) return 1.2f;
                return 1.0f; // 8 Ways

            case GameType.Montana:
                if (variant.Contains("hard")) return 1.5f;
                return 1.0f; // Classic

            default:
                return 1.0f;
        }
    }
}