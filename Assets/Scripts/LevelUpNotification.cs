using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class LevelUpNotification : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private float displayTime = 3.0f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Localization")]
    [SerializeField] private string localizationKey = "NewGlobalLevelNotification";

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        // Скрываем сразу при старте
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void ShowNotification(int newLevel)
    {
        if (levelText != null)
        {
            // Значение по умолчанию (на случай, если локализация не загрузилась)
            string textToShow = $"New Global Level: {newLevel}!";

            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string localizedFormat = LocalizationManager.instance.GetLocalizedValue(localizationKey);

                if (!string.IsNullOrEmpty(localizedFormat))
                {
                    // Вариант 1: Если в JSON написано "{newLevel}" (как в вашем примере)
                    if (localizedFormat.Contains("{newLevel}"))
                    {
                        textToShow = localizedFormat.Replace("{newLevel}", newLevel.ToString());
                    }
                    // Вариант 2: Если в JSON написано "{0}" (стандартный C# формат)
                    else if (localizedFormat.Contains("{0}"))
                    {
                        textToShow = string.Format(localizedFormat, newLevel);
                    }
                    else
                    {
                        // Если плейсхолдеров нет, просто выводим текст
                        textToShow = localizedFormat;
                    }
                }
            }

            levelText.text = textToShow;
        }

        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(NotificationRoutine());
    }

    private IEnumerator NotificationRoutine()
    {
        // 1. Fade In (Появление)
        float elapsed = 0f;
        canvasGroup.alpha = 0f; // Гарантируем старт с 0

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 2. Ждем 3 секунды
        yield return new WaitForSeconds(displayTime);

        // 3. Fade Out (Исчезновение)
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}