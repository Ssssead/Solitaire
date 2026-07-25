using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TitleDef
{
    [Tooltip("Ключ локализации (например: Title_Klondike_10)")]
    public string titleKey;

    [Tooltip("Категория (Global, Klondike, Spider и т.д.). Должна совпадать с ключами имен игр!")]
    public string category;

    [Tooltip("Требуемый уровень")]
    public int requiredLevel;
}

public class TitleManager : MonoBehaviour
{
    public static TitleManager Instance;

    [Header("База званий")]
    public List<TitleDef> allTitles = new List<TitleDef>();

    private const string SELECTED_TITLE_KEY = "SelectedPlayerTitleKey";

    // Событие для обновления UI
    public event Action OnTitleChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Возвращает КЛЮЧ текущего звания
    /// </summary>
    public string GetCurrentTitleKey()
    {
        return PlayerPrefs.GetString(SELECTED_TITLE_KEY, "Title_Global_1");
    }

    /// <summary>
    /// Сохраняет новое звание и обновляет интерфейс
    /// </summary>
    public void SetCurrentTitle(string titleKey)
    {
        PlayerPrefs.SetString(SELECTED_TITLE_KEY, titleKey);
        PlayerPrefs.Save();

        OnTitleChanged?.Invoke();
    }

    /// <summary>
    /// Проверяет, достиг ли игрок нужного уровня в указанной категории
    /// </summary>
    public bool IsTitleUnlocked(TitleDef title)
    {
        if (StatisticsManager.Instance == null) return false;

        int currentLevel = 0;

        if (title.category == "Global")
        {
            currentLevel = StatisticsManager.Instance.GetGlobalStats().currentLevel;
        }
        else
        {
            // Берем статистику конкретного режима
            currentLevel = StatisticsManager.Instance.GetGameGlobalStats(title.category).currentLevel;
        }

        return currentLevel >= title.requiredLevel;
    }
}