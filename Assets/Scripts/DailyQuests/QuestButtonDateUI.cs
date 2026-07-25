using UnityEngine;
using TMPro;
using System;

public class QuestButtonDateUI : MonoBehaviour
{
    [Tooltip("Перетащите сюда текстовый компонент внутри календаря (где написана цифра 1)")]
    public TMP_Text dayText;

    private void OnEnable()
    {
        UpdateDate();
    }

    private void Start()
    {
        // Увеличиваем частоту опроса до 0.5с, чтобы кнопка мгновенно подхватывала изменения,
        // если иконка главного меню видна на заднем плане под полупрозрачной панелью квестов.
        InvokeRepeating(nameof(UpdateDate), 0.5f, 0.5f);
    }

    private void UpdateDate()
    {
        if (dayText == null) return;

        int currentDay;

        // ---> ИСПРАВЛЕНИЕ: Берем число именно выбранного (активного) дня из QuestManager <---
        if (QuestManager.Instance != null && !string.IsNullOrEmpty(QuestManager.Instance.SelectedDateKey))
        {
            if (DateTime.TryParseExact(QuestManager.Instance.SelectedDateKey, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime selectedDate))
            {
                currentDay = selectedDate.Day;
            }
            else
            {
                currentDay = QuestManager.Instance.GetMoscowTime().Day;
            }
        }
        else if (QuestManager.Instance != null)
        {
            currentDay = QuestManager.Instance.GetMoscowTime().Day;
        }
        else
        {
            currentDay = DateTime.Now.Day;
        }

        dayText.text = currentDay.ToString();
    }
}