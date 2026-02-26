using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class BackgroundColorManager : MonoBehaviour
{
    private Image backgroundImage;

    private const string ColorPrefKey = "SavedBackgroundColor";
    private const string DefaultColorHex = "#204D20";

    [Tooltip("Скорость смены цвета (в секундах)")]
    public float transitionDuration = 0.5f;

    // Ссылка на текущий процесс смены цвета, чтобы его можно было прервать
    private Coroutine colorTransitionCoroutine;

    // Статическая переменная живет всё время, пока игра включена.
    // Она поможет нам понять, это самый первый запуск игры или просто смена сцены.
    private static bool isFirstLaunch = true;

    void Awake()
    {
        backgroundImage = GetComponent<Image>();

        if (isFirstLaunch)
        {
            // При самом первом запуске ставим дефолтный зеленый цвет
            ColorUtility.TryParseHtmlString(DefaultColorHex, out Color defaultColor);
            backgroundImage.color = defaultColor;

            // Запускаем отложенную смену цвета
            StartCoroutine(LoadColorWithDelay());

            // Отмечаем, что первый запуск прошел
            isFirstLaunch = false;
        }
        else
        {
            // Если игрок просто переходит между сценами меню и уровней,
            // сразу ставим нужный цвет без задержек, чтобы не было "моргания" зеленым
            ApplySavedColorInstantly();
        }
    }

    // Корутина для задержки при старте игры
    private IEnumerator LoadColorWithDelay()
    {
        // Ждем 1 секунду
        yield return new WaitForSeconds(1f);

        // Получаем сохраненный цвет
        string savedHex = PlayerPrefs.GetString(ColorPrefKey, DefaultColorHex);

        // Если сохраненный цвет не равен дефолтному, плавно переходим в него
        if (savedHex != DefaultColorHex && ColorUtility.TryParseHtmlString(savedHex, out Color targetColor))
        {
            ChangeColorSmoothly(targetColor);
        }
    }

    /// <summary>
    /// Вызывайте этот метод с кнопок в меню, передавая HEX (например "#5C2020")
    /// </summary>
    public void SetColor(string hexColor)
    {
        if (ColorUtility.TryParseHtmlString(hexColor, out Color newColor))
        {
            // Сохраняем
            PlayerPrefs.SetString(ColorPrefKey, hexColor);
            PlayerPrefs.Save();

            // Запускаем плавный переход
            ChangeColorSmoothly(newColor);
        }
        else
        {
            Debug.LogError($"Неверный формат HEX: {hexColor}");
        }
    }

    // Метод подготовки плавного перехода
    private void ChangeColorSmoothly(Color targetColor)
    {
        // Если цвет уже меняется прямо сейчас (игрок быстро кликает кнопки),
        // останавливаем старый процесс, чтобы они не конфликтовали
        if (colorTransitionCoroutine != null)
        {
            StopCoroutine(colorTransitionCoroutine);
        }

        // Запускаем новый процесс изменения
        colorTransitionCoroutine = StartCoroutine(ColorTransitionRoutine(targetColor));
    }

    // Сама корутина плавного изменения цвета
    private IEnumerator ColorTransitionRoutine(Color targetColor)
    {
        Color startColor = backgroundImage.color;
        float elapsedTime = 0f;

        // Пока не пройдет время transitionDuration, каждый кадр немного меняем цвет
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / transitionDuration;

            backgroundImage.color = Color.Lerp(startColor, targetColor, normalizedTime);

            yield return null; // Ждем следующий кадр
        }

        // В самом конце жестко задаем целевой цвет для идеальной точности
        backgroundImage.color = targetColor;
    }

    // Метод для моментальной установки цвета (используется при загрузке новых сцен)
    private void ApplySavedColorInstantly()
    {
        string savedHex = PlayerPrefs.GetString(ColorPrefKey, DefaultColorHex);
        if (ColorUtility.TryParseHtmlString(savedHex, out Color loadedColor))
        {
            backgroundImage.color = loadedColor;
        }
    }
}