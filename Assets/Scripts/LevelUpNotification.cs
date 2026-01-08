using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class LevelUpNotification : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text levelText; // Текст "Global Level: 5"
    [SerializeField] private float displayTime = 3.0f; // Сколько висит панель
    [SerializeField] private float fadeDuration = 0.5f; // Скорость появления

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
        if (levelText != null) levelText.text = $"New Global Level: {newLevel}!";

        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(NotificationRoutine());
    }

    private IEnumerator NotificationRoutine()
    {
        // 1. Fade In (Появление)
        float elapsed = 0f;
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