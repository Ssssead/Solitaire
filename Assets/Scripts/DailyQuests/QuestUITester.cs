using UnityEngine;
using TMPro;

public class QuestUITester : MonoBehaviour
{
    [Header("Настройки задания")]
    public DailyQuestData quest;

    [Tooltip("Число для проверки склонений (подставится вместо {0})")]
    public int testValue = 250;

    [Header("Ссылки на UI")]
    public TMP_Text titleText;
    public TMP_Text descText;

    // Цвета сложностей
    private readonly string colorEasy = "#9fd25c";
    private readonly string colorMedium = "#eeae32";
    private readonly string colorHard = "#e14849";

    private void OnEnable()
    {
        LocalizationManager.OnLocalizationLoaded += UpdateUI;
        // Обновляем UI каждый раз, когда объект становится активным (панель открывается)
        UpdateUI();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLocalizationLoaded -= UpdateUI;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateUI();
        }
    }

    [ContextMenu("Принудительно обновить UI")]
    public void UpdateUI()
    {
        if (quest == null || titleText == null || descText == null) return;

        // Защита: если менеджер локализации еще не проснулся
        if (LocalizationManager.instance == null || !LocalizationManager.instance.IsReady()) return;

        string currentLang = LocalizationManager.instance.CurrentLanguage;
        if (string.IsNullOrEmpty(currentLang)) currentLang = "ru";

        bool isRussian = currentLang.ToLower().StartsWith("ru");

        // ==========================================
        // 1. ЛОГИКА НАЗВАНИЯ
        // ==========================================
        string titleKey = quest.questId + "_Title";
        string rawTitle = LocalizationManager.instance.GetLocalizedValue(titleKey);

        if (string.IsNullOrEmpty(rawTitle) || rawTitle == "Localized text not found")
            rawTitle = "MISSING: " + titleKey;

        string colorHex = colorEasy;
        if (quest.difficulty == QuestDifficulty.Medium) colorHex = colorMedium;
        if (quest.difficulty == QuestDifficulty.Hard) colorHex = colorHard;

        // Отладочные маркеры убраны
        titleText.text = $"<color={colorHex}>{rawTitle.ToUpper()}</color>";

        // ==========================================
        // 2. ЛОГИКА ОПИСАНИЯ
        // ==========================================
        string descTemplate = "";

        if (isRussian)
        {
            string suffix = GetRussianPluralSuffix(testValue);
            string descKey = quest.questId + "_Desc_" + suffix;
            descTemplate = LocalizationManager.instance.GetLocalizedValue(descKey);

            if (string.IsNullOrEmpty(descTemplate) || descTemplate == "Localized text not found")
                descTemplate = "MISSING: " + descKey;
        }
        else
        {
            string descKey = quest.questId + "_Desc";
            descTemplate = LocalizationManager.instance.GetLocalizedValue(descKey);

            if (string.IsNullOrEmpty(descTemplate) || descTemplate == "Localized text not found")
                descTemplate = "MISSING: " + descKey;
        }

        descText.text = string.Format(descTemplate, testValue);
    }

    private string GetRussianPluralSuffix(int number)
    {
        int n = Mathf.Abs(number) % 100;
        int n10 = n % 10;

        if (n >= 11 && n <= 19) return "5";
        if (n10 == 1) return "1";
        if (n10 >= 2 && n10 <= 4) return "2";

        return "5";
    }
}