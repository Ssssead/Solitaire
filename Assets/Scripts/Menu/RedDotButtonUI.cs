using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(Button))]
public class RedDotButtonUI : MonoBehaviour
{
    public enum ButtonType
    {
        General,      // Для Настроек, Магазина и т.д.
        DailyQuests   // Для заданий (реагирует на новые дни и квесты)
    }

    [Header("Настройки логики")]
    public ButtonType buttonType = ButtonType.General;
    [Tooltip("Уникальный ключ. Например: 'SettingsRedDot'")]
    public string uniqueSaveKey = "DefaultRedDot";

    [Header("Ссылки на UI")]
    public GameObject indicator;
    [Tooltip("Панель/Подсказка, которая будет анимированно появляться")]
    public RectTransform animatedPanel;

    [Header("Настройки анимации")]
    public float animDuration = 0.3f;
    [Tooltip("Пиксели смещения: вылет сверху при открытии, улет вниз при закрытии")]
    public float flyOffset = 150f;

    private Button btn;
    private Vector2 panelStartPos;
    private bool isPanelPosSaved = false;
    private Coroutine currentAnim;

    private void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        CheckIndicatorStatus();

        // Подписываемся на прогресс заданий, если это кнопка квестов
        if (buttonType == ButtonType.DailyQuests && QuestManager.Instance != null)
        {
            QuestManager.OnQuestProgressNotification += HandleQuestProgress;
        }
    }

    private void OnDisable()
    {
        if (buttonType == ButtonType.DailyQuests && QuestManager.Instance != null)
        {
            QuestManager.OnQuestProgressNotification -= HandleQuestProgress;
        }
    }

    private void HandleQuestProgress(QuestInstance quest, int oldProgress, int newProgress)
    {
        if (quest != null && quest.isCompleted && oldProgress < quest.targetValue)
        {
            if (indicator != null) indicator.SetActive(true);
        }
    }

    public void CheckIndicatorStatus()
    {
        if (indicator == null) return;

        if (buttonType == ButtonType.General)
        {
            bool hasSeen = PlayerPrefs.GetInt(uniqueSaveKey, 0) == 1;
            indicator.SetActive(!hasSeen);
        }
        else if (buttonType == ButtonType.DailyQuests)
        {
            if (QuestManager.Instance == null) return;

            string todayKey = QuestManager.Instance.GetMoscowTime().Date.ToString("yyyy-MM-dd");
            var todayRecord = QuestManager.Instance.saveData.archive.FirstOrDefault(r => r.dateKey == todayKey);

            if (todayRecord == null) return;

            int currentCompletedCount = todayRecord.CompletedCount;
            string lastViewedDate = PlayerPrefs.GetString(uniqueSaveKey + "_Date", "");
            int lastViewedCompletedCount = PlayerPrefs.GetInt(uniqueSaveKey + "_Count", 0);

            if (lastViewedDate != todayKey || currentCompletedCount > lastViewedCompletedCount)
            {
                indicator.SetActive(true);
            }
            else
            {
                indicator.SetActive(false);
            }
        }
    }

    private void OnClick()
    {
        // Запоминаем, горела ли точка ДО того, как мы её выключим
        bool wasIndicatorActive = false;
        if (indicator != null && indicator.activeInHierarchy)
        {
            wasIndicatorActive = true;
        }

        // 1. Выключаем точку и сохраняем статус
        if (indicator != null) indicator.SetActive(false);

        if (buttonType == ButtonType.General)
        {
            PlayerPrefs.SetInt(uniqueSaveKey, 1);
            PlayerPrefs.Save();
        }
        else if (buttonType == ButtonType.DailyQuests && QuestManager.Instance != null)
        {
            string todayKey = QuestManager.Instance.GetMoscowTime().Date.ToString("yyyy-MM-dd");
            var todayRecord = QuestManager.Instance.saveData.archive.FirstOrDefault(r => r.dateKey == todayKey);
            int currentCompletedCount = todayRecord != null ? todayRecord.CompletedCount : 0;

            PlayerPrefs.SetString(uniqueSaveKey + "_Date", todayKey);
            PlayerPrefs.SetInt(uniqueSaveKey + "_Count", currentCompletedCount);
            PlayerPrefs.Save();
        }

        // 2. Анимируем открытие панели-подсказки ТОЛЬКО если была красная точка
        if (wasIndicatorActive)
        {
            OpenAnimatedPanel();
        }
    }

    // --- БЛОК АНИМАЦИИ ---

    public void OpenAnimatedPanel()
    {
        if (animatedPanel == null) return;

        SaveOriginalPosition();

        animatedPanel.gameObject.SetActive(true);

        if (currentAnim != null) StopCoroutine(currentAnim);
        // Вылет панели сверху вниз на исходную позицию
        currentAnim = StartCoroutine(AnimateCurve(panelStartPos + new Vector2(0, flyOffset), panelStartPos, false));
    }

    // ЭТУ ФУНКЦИЮ нужно вызывать при нажатии на крестик (закрытие панели)
    public void CloseAnimatedPanel()
    {
        if (animatedPanel == null || !animatedPanel.gameObject.activeInHierarchy) return;

        if (currentAnim != null) StopCoroutine(currentAnim);
        // Плавный улет вниз с последующим отключением объекта
        currentAnim = StartCoroutine(AnimateCurve(animatedPanel.anchoredPosition, panelStartPos - new Vector2(0, flyOffset), true));
    }

    private void SaveOriginalPosition()
    {
        if (!isPanelPosSaved)
        {
            panelStartPos = animatedPanel.anchoredPosition;
            isPanelPosSaved = true;
        }
    }

    private IEnumerator AnimateCurve(Vector2 from, Vector2 to, bool disableAfter)
    {
        float t = 0;
        animatedPanel.anchoredPosition = from;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animDuration;
            // Формула сглаживания (Ease-out)
            float easeT = t < 1f ? 1f - (1f - t) * (1f - t) : 1f;

            animatedPanel.anchoredPosition = Vector2.Lerp(from, to, easeT);
            yield return null;
        }

        animatedPanel.anchoredPosition = to;

        if (disableAfter)
        {
            animatedPanel.gameObject.SetActive(false);
        }
    }
}