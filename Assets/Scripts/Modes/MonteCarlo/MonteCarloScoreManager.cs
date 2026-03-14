using UnityEngine;

public class MonteCarloScoreManager : MonoBehaviour
{
    public int Score { get; private set; }

    [Header("Scoring Rules")]
    public int baseMatchScore = 10;

    [Tooltip("Очки за каждую сдвинутую карту. Чем меньше индекс слота, тем больше сдвиг.")]
    public int shiftBonusMultiplier = 1;

    public void AddPoints(int amount)
    {
        Score += amount;
        if (Score < 0) Score = 0;
    }

    // Расчет очков в зависимости от позиции
    public int CalculateAndAddMatchScore(int index1, int index2)
    {
        // Считаем, сколько карт находится ПОСЛЕ убранных (индексы 0-24).
        // Если индекс 0, после него 24 карты. Если индекс 24, после него 0 карт.
        int shift1 = 24 - index1;
        int shift2 = 24 - index2;

        int totalBonus = (shift1 + shift2) * shiftBonusMultiplier;
        int earnedPoints = baseMatchScore + totalBonus;

        AddPoints(earnedPoints);

        // Возвращаем заработанные очки, чтобы менеджер мог сохранить их для Undo
        return earnedPoints;
    }

    public void ResetScore()
    {
        Score = 0;
    }
}