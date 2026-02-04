using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;


/// <summary>
/// LanguageDropdownTMP — менеджер дропдауна для выбора языка + отображение флагов.
/// Положи этот скрипт на тот же объект, что содержит TMP_Dropdown.
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class LanguageDropdownTMP : MonoBehaviour
{
    [Header("Настройки языков (код -> отображаемое имя)")]
    [Tooltip("Порядок кодов должен соответствовать displayNames и languageSprites")]
    public string[] languageCodes = new string[] { "ru", "en", "de", "es", "pt", "fr", "tr", "ja" };
    public string[] displayNames = new string[] { "Русский", "English", "Deutsch", "Espa?ol", "Portugu?s", "Fran?ais", "T?rk?e", "???" };

    [Header("Иконки флагов (по индексу)")]
    public Sprite[] languageSprites; // можно оставить пустым

    [Header("Опционально: принудительно назначить Image из шаблона")]
    public Image itemImageOverride;      // перетащи Template/Viewport/Content/Item/Image
    public Image captionImageOverride;   // перетащи Caption Image (обычно на корне Dropdown)

    private TMP_Dropdown dropdown;
    private const string PREF_SELECTED_LANG = "SelectedLanguage";

    // Флаг: при программном изменении значения подавлять обработчик onValueChanged
    private bool suppressOnValueChanged = false;
    private Coroutine syncCoroutine;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();

        // Применяем принудительные override, если заданы
        if (captionImageOverride != null) dropdown.captionImage = captionImageOverride;
        if (itemImageOverride != null) dropdown.itemImage = itemImageOverride;

        AutoAssignDropdownImages();
        PopulateOptions();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLocalizationLoaded += OnLocalizationLoaded;
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);

        // Запускаем асинхронную синхронизацию — ждём LocalizationManager и применяем язык
        if (syncCoroutine != null) StopCoroutine(syncCoroutine);
        syncCoroutine = StartCoroutine(SyncWhenReady());
    }

    private void OnDisable()
    {
        LocalizationManager.OnLocalizationLoaded -= OnLocalizationLoaded;
        dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);

        if (syncCoroutine != null)
        {
            StopCoroutine(syncCoroutine);
            syncCoroutine = null;
        }
    }

    /// <summary>
    /// Публичный метод, вызывай при открытии панели настроек, чтобы форсировать обновление дропдауна.
    /// </summary>
    public void RefreshDropdown()
    {
        if (syncCoroutine != null) StopCoroutine(syncCoroutine);
        syncCoroutine = StartCoroutine(SyncWhenReady());
    }

    /// <summary>
    /// Ждёт появления/готовности LocalizationManager (до таймаута) и затем применяет текущий язык к дропдауну.
    /// </summary>
    private IEnumerator SyncWhenReady()
    {
        // хотя бы один кадр подождать
        yield return null;

        float timeout = 1.0f;
        float waited = 0f;

        while (waited < timeout)
        {
            if (LocalizationManager.instance != null)
            {
                bool ready = true;
                try { ready = LocalizationManager.instance.IsReady(); } catch { ready = true; }

                if (ready) break;
            }

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // Определяем желаемый код языка: предпочтение CurrentLanguage -> PlayerPrefs -> default
        string desired = null;

        if (LocalizationManager.instance != null)
        {
            try
            {
                var lmType = LocalizationManager.instance.GetType();
                var prop = lmType.GetProperty("CurrentLanguage");
                if (prop != null)
                {
                    desired = prop.GetValue(LocalizationManager.instance, null) as string;
                }
            }
            catch { desired = null; }
        }

        if (string.IsNullOrEmpty(desired))
        {
            desired = PlayerPrefs.GetString(PREF_SELECTED_LANG, "");
        }

        if (string.IsNullOrEmpty(desired) && LocalizationManager.instance != null)
        {
            try { desired = LocalizationManager.instance.defaultLanguage; } catch { desired = "en"; }
        }

        ApplyDropdownValueByLang(desired);

        syncCoroutine = null;
    }

    private void PopulateOptions()
    {
        dropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>();
        int count = Mathf.Min(languageCodes.Length, displayNames.Length);
        for (int i = 0; i < count; i++)
        {
            var od = new TMP_Dropdown.OptionData(displayNames[i]);
            if (languageSprites != null && i < languageSprites.Length && languageSprites[i] != null)
                od.image = languageSprites[i];
            options.Add(od);
        }

        dropdown.AddOptions(options);
        dropdown.RefreshShownValue();

        // Обновить captionImage спрайтом текущего значения
        if (dropdown.captionImage != null && dropdown.options.Count > 0)
        {
            int val = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
            var sprite = dropdown.options[val].image;
            if (sprite != null) dropdown.captionImage.sprite = sprite;
        }
    }

    private void OnDropdownValueChanged(int index)
    {
        if (suppressOnValueChanged) return; // пропускаем если это программная установка
        if (index < 0 || index >= languageCodes.Length) return;

        string code = languageCodes[index];
        Debug.Log($"[LanguageDropdownTMP] User selected language: {code}");

        // Сохраняем выбор в PlayerPrefs
        PlayerPrefs.SetString(PREF_SELECTED_LANG, code);
        PlayerPrefs.Save();

        // Обновляем caption
        if (dropdown.captionImage != null && index >= 0 && index < dropdown.options.Count)
        {
            var sprite = dropdown.options[index].image;
            if (sprite != null) dropdown.captionImage.sprite = sprite;
        }

        // Применяем язык через LocalizationManager если он доступен
        if (LocalizationManager.instance != null)
        {
            LocalizationManager.instance.SetLanguage(code);
        }
    }

    private void OnLocalizationLoaded()
    {
        // Когда локализация извне загружена — синхронизируем UI с текущим языком менеджера
        string current = null;
        if (LocalizationManager.instance != null)
        {
            var lmType = LocalizationManager.instance.GetType();
            var prop = lmType.GetProperty("CurrentLanguage");
            if (prop != null)
            {
                current = prop.GetValue(LocalizationManager.instance, null) as string;
            }
        }

        if (string.IsNullOrEmpty(current))
            current = PlayerPrefs.GetString(PREF_SELECTED_LANG, LocalizationManager.instance != null ? LocalizationManager.instance.defaultLanguage : "en");

        ApplyDropdownValueByLang(current);
    }

    private int IndexOfCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return -1;
        for (int i = 0; i < languageCodes.Length; i++)
            if (languageCodes[i].Equals(code, System.StringComparison.InvariantCultureIgnoreCase))
                return i;
        return -1;
    }

    // Установить значение дропдауна по коду языка, программно (без вызова обработчика)
    private void ApplyDropdownValueByLang(string code)
    {
        int idx = IndexOfCode(code);
        if (idx < 0) idx = 0;

        suppressOnValueChanged = true;
        dropdown.SetValueWithoutNotify(idx);
        dropdown.RefreshShownValue();
        // обновим captionImage если требуется
        if (dropdown.captionImage != null && idx >= 0 && idx < dropdown.options.Count)
            dropdown.captionImage.sprite = dropdown.options[idx].image;
        suppressOnValueChanged = false;
    }

    // Принудительное применение выбранного языка из дропдауна (удобно для Debug).
    [ContextMenu("ForceApplySelectedLanguage")]
    private void ForceApplySelectedLanguage()
    {
        int idx = dropdown.value;
        if (idx < 0 || idx >= languageCodes.Length) return;
        string code = languageCodes[idx];
        PlayerPrefs.SetString(PREF_SELECTED_LANG, code);
        PlayerPrefs.Save();
        if (LocalizationManager.instance != null) LocalizationManager.instance.SetLanguage(code);
    }

    // Автовыявление Image компонентов внутри шаблона Dropdown (если не назначены вручную)
    private void AutoAssignDropdownImages()
    {
        if (dropdown.captionImage != null && dropdown.itemImage != null) return;

        var images = GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            string name = img.gameObject.name.ToLowerInvariant();
            if (dropdown.captionImage == null && (name.Contains("caption") || name.Contains("label") || name.Contains("selected")))
            {
                dropdown.captionImage = img;
            }
            if (dropdown.itemImage == null && (name.Contains("item image") || name == "image" || name.Contains("item")))
            {
                if (IsChildOfTemplate(img.transform))
                {
                    dropdown.itemImage = img;
                }
            }
            if (dropdown.captionImage != null && dropdown.itemImage != null) break;
        }

        if (dropdown.itemImage == null)
        {
            Transform t = transform.Find("Template/Viewport/Content/Item/Image");
            if (t != null)
            {
                var comp = t.GetComponent<Image>();
                if (comp != null) dropdown.itemImage = comp;
            }
        }

        if (dropdown.captionImage == null)
        {
            var rootImg = GetComponent<Image>();
            if (rootImg != null) dropdown.captionImage = rootImg;
        }
    }

    private bool IsChildOfTemplate(Transform t)
    {
        if (t == null) return false;
        Transform cur = t;
        while (cur != null)
        {
            if (cur.name.ToLowerInvariant().Contains("template")) return true;
            cur = cur.parent;
        }
        return false;
    }
}
