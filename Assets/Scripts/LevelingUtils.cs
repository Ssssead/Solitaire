using UnityEngine;

public static class LevelingUtils
{
    // Базовый опыт
    public const int BASE_XP_EASY = 100;
    public const int BASE_XP_MEDIUM = 200;
    public const int BASE_XP_HARD = 400;

    // Множители Klondike
    public const float MULTIPLIER_KLONDIKE_DRAW3 = 1.5f;

    // Множители Spider
    public const float MULTIPLIER_SPIDER_2SUIT = 1.5f;
    public const float MULTIPLIER_SPIDER_4SUIT = 2.5f;

    // Премиум
    public const float MULTIPLIER_PREMIUM = 1.2f;

    public static int CalculateXP(string gameName, Difficulty difficulty, string variant, bool isPremium)
    {
        // 1. Базовый опыт
        float xp = 0;
        switch (difficulty)
        {
            case Difficulty.Easy: xp = BASE_XP_EASY; break;
            case Difficulty.Medium: xp = BASE_XP_MEDIUM; break;
            case Difficulty.Hard: xp = BASE_XP_HARD; break;
        }

        // 2. Модификаторы режимов
        if (gameName == "Klondike")
        {
            if (variant.Contains("Draw3")) xp *= MULTIPLIER_KLONDIKE_DRAW3;
            // Draw1 - множитель x1.0, ничего не делаем
        }
        else if (gameName == "Spider")
        {
            if (variant.Contains("2Suit")) xp *= MULTIPLIER_SPIDER_2SUIT;
            else if (variant.Contains("4Suit")) xp *= MULTIPLIER_SPIDER_4SUIT;
            // 1Suit - множитель x1.0
        }

        // 3. Премиум бонус
        if (isPremium)
        {
            xp *= MULTIPLIER_PREMIUM;
        }

        // Округляем до целого
        return Mathf.RoundToInt(xp);
    }
}