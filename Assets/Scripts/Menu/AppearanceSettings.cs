using UnityEngine;
using UnityEngine.UI;

public class AppearanceSettings : MonoBehaviour
{
    [Header("Database Reference")]
    public CardSpriteDatabase spriteDatabase;

    [Header("Symbol Buttons")]
    public Button englishSymbolsBtn; // Кнопка "English (J, Q, A)"
    public Button russianSymbolsBtn; // Кнопка "Russian (В, Д, Т)"

    [Header("Visual Feedback")]
    public Color selectedColor = Color.green;
    public Color normalColor = Color.white;

    private void Start()
    {
        // При старте проверяем текущие настройки (можно хранить в PlayerPrefs)
        bool isRus = PlayerPrefs.GetInt("UseRussianSymbols", 0) == 1;
        SetSymbolMode(isRus);

        // Назначаем слушатели
        englishSymbolsBtn.onClick.AddListener(() => OnSymbolBtnClicked(false));
        russianSymbolsBtn.onClick.AddListener(() => OnSymbolBtnClicked(true));
    }

    private void OnSymbolBtnClicked(bool isRussian)
    {
        SetSymbolMode(isRussian);

        // Сохраняем выбор
        PlayerPrefs.SetInt("UseRussianSymbols", isRussian ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SetSymbolMode(bool isRussian)
    {
        if (spriteDatabase != null)
        {
            spriteDatabase.SetSymbolMode(isRussian);
        }

        // Обновляем вид кнопок
        englishSymbolsBtn.image.color = isRussian ? normalColor : selectedColor;
        russianSymbolsBtn.image.color = isRussian ? selectedColor : normalColor;

        // Если в сцене меню есть декоративные карты, их нужно обновить вручную,
        // так как они уже созданы. Но для новой игры спрайты подтянутся автоматически.
    }
}