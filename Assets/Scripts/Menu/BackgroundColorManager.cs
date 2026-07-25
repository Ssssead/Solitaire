using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundColorManager : MonoBehaviour
{
    [Header("Настройки фона (для всех сцен)")]
    public Image backgroundImage;
    private const string ColorPrefKey = "SavedBackgroundColor";
    private const string DefaultColorHex = "#204D20";

    [Header("Настройки UI (только для Меню)")]
    public MenuController menuController;
    public RectTransform selectionOutline;
    public Button[] colorButtons;
    public string[] hexColors = new string[8];

    [Header("Настройки анимаций")]
    public float transitionDuration = 0.5f;
    public float selectedScale = 1.05f;
    public float outlineScale = 1.075f;
    public float uiAnimDuration = 0.2f;

    private Coroutine colorTransitionCoroutine;
    private Coroutine[] scaleCoroutines;
    private int currentIndex = -1;

    // --- НОВОЕ: Статическая переменная для отслеживания первого запуска ---
    private static bool isFirstLaunch = true;

    void Awake()
    {
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();

        if (isFirstLaunch)
        {
            // 1. При самом первом запуске: ставим дефолтный цвет и ждем
            if (ColorUtility.TryParseHtmlString(DefaultColorHex, out Color defaultColor))
            {
                backgroundImage.color = defaultColor;
            }
            StartCoroutine(LoadColorWithDelay());

            // Отмечаем, что первый запуск прошел. Больше в эту ветку мы не зайдем до перезапуска приложения.
            isFirstLaunch = false;
        }
        else
        {
            // 2. При переходе между сценами: применяем цвет моментально
            ApplySavedColorInstantly();
        }
    }

    void Start()
    {
        if (colorButtons == null || colorButtons.Length == 0) return;

        scaleCoroutines = new Coroutine[colorButtons.Length];
        string savedHex = PlayerPrefs.GetString(ColorPrefKey, DefaultColorHex);

        for (int i = 0; i < colorButtons.Length; i++)
        {
            int index = i;
            if (colorButtons[i] != null)
                colorButtons[i].onClick.AddListener(() => OnColorButtonClicked(index));

            if (hexColors.Length > i && hexColors[i].Equals(savedHex, System.StringComparison.OrdinalIgnoreCase))
                currentIndex = index;
        }

        if (currentIndex != -1 && selectionOutline != null)
        {
            selectionOutline.gameObject.SetActive(true);
            selectionOutline.position = colorButtons[currentIndex].GetComponent<RectTransform>().position;
            colorButtons[currentIndex].GetComponent<RectTransform>().localScale = new Vector3(selectedScale, selectedScale, 1f);
            selectionOutline.localScale = new Vector3(outlineScale, outlineScale, 1f);
        }
    }

    public void OnColorButtonClicked(int index)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("BG_Switch");

        if (currentIndex == index) return;

        if (currentIndex >= 0 && currentIndex < colorButtons.Length)
            AnimateButtonScale(currentIndex, 1f);

        currentIndex = index;
        AnimateButtonScale(currentIndex, selectedScale);

        if (selectionOutline != null && colorButtons[currentIndex] != null)
        {
            selectionOutline.gameObject.SetActive(true);
            selectionOutline.position = colorButtons[currentIndex].GetComponent<RectTransform>().position;
            selectionOutline.localScale = new Vector3(outlineScale, outlineScale, 1f);
        }

        if (index < hexColors.Length)
        {
            string hex = hexColors[index];
            PlayerPrefs.SetString(ColorPrefKey, hex);
            PlayerPrefs.Save();

            if (ColorUtility.TryParseHtmlString(hex, out Color newColor))
                ChangeColorSmoothly(newColor);
        }
    }

    public void OnAcceptClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        if (menuController != null)
            menuController.CloseBackgroundSelection();
    }

    // --- ЛОГИКА ЦВЕТА И АНИМАЦИЙ ---

    // Корутина для плавного появления при старте игры
    private IEnumerator LoadColorWithDelay()
    {
        yield return new WaitForSeconds(1f); // Ждем 1 секунду

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

    private void AnimateButtonScale(int index, float targetScale)
    {
        if (colorButtons == null || index < 0 || index >= colorButtons.Length || colorButtons[index] == null) return;
        if (scaleCoroutines[index] != null) StopCoroutine(scaleCoroutines[index]);
        scaleCoroutines[index] = StartCoroutine(ScaleRoutine(colorButtons[index].GetComponent<RectTransform>(), targetScale));
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