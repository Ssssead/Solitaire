using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Для обработки наведения мыши
using TMPro;
using System.Collections;

// Добавляем интерфейсы наведения
public class QuestUIItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Slot Settings")]
    public QuestDifficulty fixedDifficulty;

    [Header("Texts")]
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public TMP_Text progressText;

    [Header("Progress")]
    public Image progressBarFill;

    [Header("Icon")]
    public Image gameIcon;

    [Header("Background")]
    public Image backgroundImage;
    public Color inProgressColor = new Color32(154, 95, 64, 255);
    public Color completedColor = new Color32(194, 122, 78, 255);

    [Header("Difficulty Colors")]
    public Color easyColor = new Color(0.5f, 0.8f, 0.2f);
    public Color mediumColor = new Color(1f, 0.7f, 0f);
    public Color hardColor = new Color(0.9f, 0.2f, 0.2f);

    [Header("Hover Animation")]
    public float hoverScaleMultiplier = 1.03f; // Насколько увеличивать (1.03 = на 3%)
    public float scaleSpeed = 15f;             // Скорость анимации
    private Vector3 originalScale;
    private Coroutine scaleCoroutine;

    // Скрытая переменная, чтобы тултип иконки знал, какая это игра
    [HideInInspector] public QuestCategory currentCategory;
    public QuestInstance ActiveQuest { get; private set; }
    private void Awake()
    {
        // Запоминаем оригинальный размер плашки при старте
        originalScale = transform.localScale;
    }

    public void Setup(QuestInstance quest, Sprite iconSprite)
    {
        ActiveQuest = quest;
        currentCategory = quest.template.category;

        // 1. ЛОКАЛИЗАЦИЯ НАЗВАНИЯ КВЕСТА
        string titleKey = quest.template.questId + "_Title";
        string localizedTitle = LocalizationManager.instance?.GetLocalizedValue(titleKey) ?? "";

        if (string.IsNullOrEmpty(localizedTitle) || localizedTitle == titleKey || localizedTitle == "Localized text not found")
        {
            localizedTitle = quest.template.questName;
        }
        titleText.text = localizedTitle.ToUpper();
        descriptionText.text = quest.GetDescriptionForUI();

        // 2. ПРОГРЕСС И ФОН
        progressText.text = $"{quest.currentProgress}/{quest.targetValue}";
        progressBarFill.fillAmount = quest.targetValue > 0 ? (float)quest.currentProgress / quest.targetValue : 0f;

        if (backgroundImage != null)
            backgroundImage.color = quest.isCompleted ? completedColor : inProgressColor;

        // 3. ПОКРАСКА
        Color diffColor = GetColorForDifficulty(fixedDifficulty);
        titleText.color = diffColor;

        if (gameIcon != null)
        {
            gameIcon.sprite = iconSprite;
            gameIcon.color = diffColor;
        }
    }

    // --- МЕТОД ДЛЯ ТУЛТИПА ---
    public string GetLocalizedGameName()
    {
        // Конвертируем Enum в строку ключа (General, Klondike, Spider и т.д.)
        string key = currentCategory.ToString();

        // Важно: в вашем JSON ключ "Freecell" написан с маленькой буквой 'c'
        if (currentCategory == QuestCategory.FreeCell) key = "Freecell";

        if (LocalizationManager.instance != null)
        {
            string loc = LocalizationManager.instance.GetLocalizedValue(key);
            // Проверяем, что перевод реально найден
            if (!string.IsNullOrEmpty(loc) && loc != key && loc != "Localized text not found")
            {
                return loc;
            }
        }

        // Запасной вариант, если перевода вдруг нет
        return key;
    }

    // --- АНИМАЦИЯ НАВЕДЕНИЯ ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleTo(originalScale * hoverScaleMultiplier));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleTo(originalScale));
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        while (Vector3.Distance(transform.localScale, target) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, target, Time.unscaledDeltaTime * scaleSpeed);
            yield return null;
        }
        transform.localScale = target;
    }

    private Color GetColorForDifficulty(QuestDifficulty diff)
    {
        switch (diff)
        {
            case QuestDifficulty.Easy: return easyColor;
            case QuestDifficulty.Medium: return mediumColor;
            case QuestDifficulty.Hard: return hardColor;
            default: return easyColor;
        }
    }
}