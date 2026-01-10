using UnityEngine;

public class PyramidScoreManager : MonoBehaviour
{
    public int Score { get; private set; }

    public void ResetScore()
    {
        Score = 0;
        // Update UI logic here
    }

    public void AddPoints(int points)
    {
        Score += points;
        // Update UI logic here
    }
}