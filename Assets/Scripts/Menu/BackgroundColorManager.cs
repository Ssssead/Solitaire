using System.Collections;
using System.Collections.Generic; // Не забудь добавить для Dictionary
using UnityEngine;
using UnityEngine.UI;

public class BackgroundColorManager : MonoBehaviour
{
    [Header("Настройки фона (для всех сцен)")]
    public Image backgroundImage;
    private const string ColorPrefKey = "SavedBackgroundColor";
    private const string DefaultColorHex = "#204D20";

    // ==========================================
    // СТРУКТУРА ДЛЯ ПАНЕЛЕЙ (Двойная ориентация)
    // ==========================================
    [System.Serializable]
    public class BackgroundUIGroup
    {
        public RectTransform selectionOutline;
        public Button[] colorButtons;
    }

    [Header("Настройки UI (только для Меню)")]
    public MenuController menuController;
    public BackgroundUIGroup landscapeUI;
    public BackgroundUIGroup portraitUI;
    public string[] hexColors = new string[8];

    [Header("Настройки анимаций")]
    public float transitionDuration = 0.5f;
    public float selectedScale = 1.05f;
    public float outlineScale = 1.075f;
    public float uiAnimDuration = 0.2f;

    private Coroutine colorTransitionCoroutine;
    // Используем словарь, чтобы хранить анимацию конкретной кнопки
    private Dictionary<RectTransform, Coroutine> scaleCoroutines = new Dictionary<RectTransform, Coroutine>();
    private int currentIndex = -1;

    private static bool isFirstLaunch = true;

    void Awake()
    {
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();

        if (isFirstLaunch)
        {
            if (ColorUtility.TryParseHtmlString(DefaultColorHex, out Color defaultColor))
            {
                backgroundImage.color = defaultColor;
            }
            StartCoroutine(LoadColorWithDelay());
            isFirstLaunch = false;
        }
        else
        {
            ApplySavedColorInstantly();
        }
    }

    void Start()
    {
        string savedHex = PlayerPrefs.GetString(ColorPrefKey, DefaultColorHex);

        // Определяем стартовый индекс
        for (int i = 0; i < hexColors.Length; i++)
        {
            if (hexColors[i].Equals(savedHex, System.StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        // Настраиваем обе панели
        SetupUIGroup(landscapeUI);
        SetupUIGroup(portraitUI);
    }

    // --- Инициализация конкретной группы UI ---
    private void SetupUIGroup(BackgroundUIGroup group)
    {
        if (group == null || group.colorButtons == null) return;

        for (int i = 0; i < group.colorButtons.Length; i++)
        {
            int index = i;
            if (group.colorButtons[i] != null)
            {
                group.colorButtons[i].onClick.AddListener(() => OnColorButtonClicked(index));
            }
        }

        // Если нашли сохраненный цвет, сразу выделяем его рамкой и размером
        if (currentIndex != -1 && currentIndex < group.colorButtons.Length)
        {
            if (group.selectionOutline != null)
            {
                group.selectionOutline.gameObject.SetActive(true);
                group.selectionOutline.position = group.colorButtons[currentIndex].GetComponent<RectTransform>().position;
                group.selectionOutline.localScale = new Vector3(outlineScale, outlineScale, 1f);
            }

            if (group.colorButtons[currentIndex] != null)
            {
                group.colorButtons[currentIndex].GetComponent<RectTransform>().localScale = new Vector3(selectedScale, selectedScale, 1f);
            }
        }
    }

    public void OnColorButtonClicked(int index)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("BG_Switch");

        if (currentIndex == index) return;

        // 1. Уменьшаем старые кнопки в обеих панелях
        if (currentIndex >= 0)
        {
            AnimateButtonInGroup(landscapeUI, currentIndex, 1f);
            AnimateButtonInGroup(portraitUI, currentIndex, 1f);
        }

        currentIndex = index;

        // 2. Увеличиваем новые кнопки в обеих панелях
        AnimateButtonInGroup(landscapeUI, currentIndex, selectedScale);
        AnimateButtonInGroup(portraitUI, currentIndex, selectedScale);

        // 3. Двигаем рамку в обеих панелях
        UpdateOutlineInGroup(landscapeUI);
        UpdateOutlineInGroup(portraitUI);

        // 4. Меняем цвет
        if (index < hexColors.Length)
        {
            string hex = hexColors[index];
            PlayerPrefs.SetString(ColorPrefKey, hex);
            PlayerPrefs.Save();

            if (ColorUtility.TryParseHtmlString(hex, out Color newColor))
                ChangeColorSmoothly(newColor);
        }
    }

    // --- Вспомогательные методы для синхронизации групп ---
    private void AnimateButtonInGroup(BackgroundUIGroup group, int index, float targetScale)
    {
        if (group == null || group.colorButtons == null || index >= group.colorButtons.Length || group.colorButtons[index] == null) return;

        RectTransform target = group.colorButtons[index].GetComponent<RectTransform>();

        if (scaleCoroutines.ContainsKey(target) && scaleCoroutines[target] != null)
        {
            StopCoroutine(scaleCoroutines[target]);
        }

        scaleCoroutines[target] = StartCoroutine(ScaleRoutine(target, targetScale));
    }

    private void UpdateOutlineInGroup(BackgroundUIGroup group)
    {
        if (group == null || group.selectionOutline == null || group.colorButtons == null) return;

        if (currentIndex >= 0 && currentIndex < group.colorButtons.Length && group.colorButtons[currentIndex] != null)
        {
            group.selectionOutline.gameObject.SetActive(true);
            group.selectionOutline.position = group.colorButtons[currentIndex].GetComponent<RectTransform>().position;
            group.selectionOutline.localScale = new Vector3(outlineScale, outlineScale, 1f);
        }
    }

    public void OnAcceptClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        if (menuController != null)
            menuController.CloseBackgroundSelection();
    }

    // --- ЛОГИКА ЦВЕТА И АНИМАЦИЙ ---

    private IEnumerator LoadColorWithDelay()
    {
        yield return new WaitForSeconds(1f);

        string savedHex = PlayerPrefs.GetString(ColorPrefKey, DefaultColorHex);
        if (savedHex != DefaultColorHex && ColorUtility.TryParseHtmlString(savedHex, out Color targetColor))
        {
            ChangeColorSmoothly(targetColor);
        }
    }

    private void ApplySavedColorInstantly()
    {
        if (backgroundImage == null) return;
        string savedHex = PlayerPrefs.GetString(ColorPrefKey, DefaultColorHex);
        if (ColorUtility.TryParseHtmlString(savedHex, out Color loadedColor))
            backgroundImage.color = loadedColor;
    }

    private void ChangeColorSmoothly(Color targetColor)
    {
        if (backgroundImage == null) return;
        if (colorTransitionCoroutine != null) StopCoroutine(colorTransitionCoroutine);
        colorTransitionCoroutine = StartCoroutine(ColorTransitionRoutine(targetColor));
    }

    private IEnumerator ColorTransitionRoutine(Color targetColor)
    {
        Color startColor = backgroundImage.color;
        float elapsedTime = 0f;
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            backgroundImage.color = Color.Lerp(startColor, targetColor, elapsedTime / transitionDuration);
            yield return null;
        }
        backgroundImage.color = targetColor;
    }

    private IEnumerator ScaleRoutine(RectTransform target, float targetScale)
    {
        Vector3 startScale = target.localScale;
        Vector3 endScale = new Vector3(targetScale, targetScale, 1f);
        float elapsed = 0f;
        while (elapsed < uiAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / uiAnimDuration;
            float smoothT = t * t * (3f - 2f * t);
            target.localScale = Vector3.Lerp(startScale, endScale, smoothT);
            yield return null;
        }
        target.localScale = endScale;
    }
}