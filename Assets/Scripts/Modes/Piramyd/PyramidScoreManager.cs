using UnityEngine;
using System;

public class PyramidScoreManager : MonoBehaviour
{
    private int _currentScore;

    // Свойство для GameUIController (Reflection)
    public int CurrentScore => _currentScore;
    public int Score => _currentScore; // Дубликат для удобства

    // Событие для обновления UI (если потребуется в будущем)
    public event Action<int> OnScoreChanged;

    public void AddPoints(int amount)
    {
        _currentScore += amount;
        OnScoreChanged?.Invoke(_currentScore);
    }

    public void ResetScore()
    {
        _currentScore = 0;
        OnScoreChanged?.Invoke(_currentScore);
    }
}