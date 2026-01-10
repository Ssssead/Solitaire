using UnityEngine;
using TMPro; // Если нужно обновлять UI

public class FreeCellScoreManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int startingScore = 500;
    [SerializeField] private int baseFoundationScore = 1;
    [SerializeField] private int movePenalty = 1;
    [SerializeField] private int cellPenalty = 5;
    [SerializeField] private int undoPenalty = 10;

    [Header("UI References")]
    public TMP_Text scoreText; // Ссылка на текст счета в Canvas

    // Текущее состояние
    private int currentScore;
    private int comboMultiplier = 0; // Длина текущей цепочки

    public int CurrentScore => currentScore;

    private void Start()
    {
        ResetScore();
    }

    public void ResetScore()
    {
        currentScore = startingScore;
        comboMultiplier = 0;
        UpdateUI();
    }

    /// <summary>
    /// Основной метод начисления очков
    /// </summary>
    public void OnCardMove(ICardContainer source, ICardContainer target)
    {
        // 1. ИГРОК ПОЛОЖИЛ КАРТУ В FOUNDATION (ДОМ)
        if (target is FoundationPile)
        {
            // Если карта пришла из другого Foundation - это не считается (перекладывание тузов)
            if (source is FoundationPile) return;

            // Увеличиваем счетчик комбо (было 0 -> стало 1)
            comboMultiplier++;

            // Формула: 10 * 1, 10 * 2, 10 * 3...
            int pointsToAdd = baseFoundationScore * comboMultiplier;

            AddScore(pointsToAdd);

            Debug.Log($"<color=green>Foundation Combo x{comboMultiplier}! +{pointsToAdd}</color>");
        }
        // 2. ЛЮБОЙ ДРУГОЙ ХОД (СБРОС ЦЕПОЧКИ)
        else
        {
            // Если цепочка была, мы ее теряем
            if (comboMultiplier > 0)
            {
                Debug.Log($"<color=yellow>Combo Broken!</color>");
            }
            comboMultiplier = 0;

            // Начисляем штрафы
            if (target is FreeCellPile)
            {
                // Штраф за использование ячейки
                AddScore(-cellPenalty);
            }
            else
            {
                // Обычный ход по столу
                AddScore(-movePenalty);
            }
        }
    }

    public void OnUndo()
    {
        // 1. Самое важное: СБРОС КОМБО
        // Любая отмена хода прерывает серию "потока".
        if (comboMultiplier > 0)
        {
            Debug.Log($"Combo Lost due to Undo!");
        }
        comboMultiplier = 0;

        // 2. Штраф за отмену
        // Важно: Штраф должен быть больше или равен базовой награде за карту (10),
        // чтобы игрок не мог "фармить" очки (положил +10, отменил -5, положил +10 = итог +15).
        AddScore(-undoPenalty);
    }

    private void AddScore(int amount)
    {
        currentScore += amount;

        // (Опционально) Не даем уйти в минус, если хотите
        // if (currentScore < 0) currentScore = 0;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {currentScore}";

            // Если есть активное комбо, можно дописать его
            if (comboMultiplier > 1)
            {
                scoreText.text += $" <color=yellow>(x{comboMultiplier})</color>";
            }
        }
    }
}