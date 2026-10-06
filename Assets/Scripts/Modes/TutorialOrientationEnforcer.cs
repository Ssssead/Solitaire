using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Reflection;

[DefaultExecutionOrder(1000)] // Гарантированно выполняется в самом конце кадра
public class TutorialOrientationEnforcer : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject rotatePromptPanel;
    public TMP_Text rotatePromptText;

    private MonoBehaviour activeTutorial;
    private bool isPortraitPromptActive = false;

    // Кэшируем рефлексию для высокой производительности
    private FieldInfo panelField;
    private FieldInfo[] arrowFields;
    private FieldInfo highlightsField;

    private void Awake()
    {
        if (rotatePromptPanel != null) rotatePromptPanel.SetActive(false);
    }

    private void LateUpdate()
    {
        // Используем LateUpdate, чтобы выключать UI после того, 
        // как туториал попытался его включить в своем Update или Корутине.
        if (!GameSettings.IsTutorialMode)
        {
            if (rotatePromptPanel != null && rotatePromptPanel.activeSelf) rotatePromptPanel.SetActive(false);
            isPortraitPromptActive = false;
            activeTutorial = null;
            return;
        }

        bool isPortrait = Screen.height > Screen.width;

        if (isPortrait)
        {
            if (!isPortraitPromptActive)
            {
                isPortraitPromptActive = true;
                if (rotatePromptPanel) rotatePromptPanel.SetActive(true);
                UpdateLocalization();
            }

            // ПОСТОЯННОЕ ПОДАВЛЕНИЕ:
            // Жестко выключаем UI туториала каждый кадр, пока телефон вертикально.
            EnforceTutorialVisualsHidden();
        }
        else
        {
            if (isPortraitPromptActive)
            {
                isPortraitPromptActive = false;
                if (rotatePromptPanel) rotatePromptPanel.SetActive(false);
                RestoreTutorialState();
            }
        }
    }

    private void FindActiveTutorial()
    {
        var monos = FindObjectsOfType<MonoBehaviour>();
        foreach (var mono in monos)
        {
            if (mono is ITutorialManager && mono.enabled && mono.gameObject.activeInHierarchy)
            {
                activeTutorial = mono;
                var type = activeTutorial.GetType();

                panelField = type.GetField("tutorialUIPanel");
                arrowFields = new FieldInfo[] {
                    type.GetField("arrowDown"),
                    type.GetField("arrowUp"),
                    type.GetField("arrowLeft")
                };
                highlightsField = type.GetField("highlightObjects");
                break;
            }
        }
    }

    private void EnforceTutorialVisualsHidden()
    {
        // Ищем туториал, если еще не нашли
        if (activeTutorial == null) FindActiveTutorial();
        if (activeTutorial == null) return;

        // Прячем главную панель
        if (panelField != null)
        {
            var panel = panelField.GetValue(activeTutorial) as RectTransform;
            if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
        }

        // Прячем стрелки
        if (arrowFields != null)
        {
            foreach (var arrowField in arrowFields)
            {
                if (arrowField != null)
                {
                    var arrow = arrowField.GetValue(activeTutorial) as RectTransform;
                    if (arrow != null && arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
                }
            }
        }

        // Прячем подсветки
        if (highlightsField != null)
        {
            var highlights = highlightsField.GetValue(activeTutorial) as List<GameObject>;
            if (highlights != null)
            {
                foreach (var h in highlights)
                {
                    if (h != null && h.activeSelf) h.SetActive(false);
                }
            }
        }
    }

    private void RestoreTutorialState()
    {
        if (activeTutorial == null) return;

        // Если мы вернулись в горизонт, включаем панель обратно
        if (panelField != null)
        {
            var panel = panelField.GetValue(activeTutorial) as RectTransform;
            if (panel != null) panel.gameObject.SetActive(true);
        }

        // Просим сам туториал перерисовать стрелки и подсветки в правильных местах
        var updateMethod = activeTutorial.GetType().GetMethod("UpdateUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (updateMethod != null)
        {
            updateMethod.Invoke(activeTutorial, null);
        }
        else
        {
            (activeTutorial as ITutorialManager)?.RestorePanelPosition();
        }
    }

    private void UpdateLocalization()
    {
        if (rotatePromptText != null)
        {
            string loc = "Поверните устройство горизонтально на время обучения";
            if (LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
            {
                string fetchedLoc = LocalizationManager.instance.GetLocalizedValue("RotateDevicePrompt");
                if (!string.IsNullOrEmpty(fetchedLoc) && fetchedLoc != "RotateDevicePrompt")
                    loc = fetchedLoc;
            }
            rotatePromptText.text = loc;
        }
    }
}