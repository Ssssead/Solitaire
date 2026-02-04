// LocalizationManager_YGIntegration.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using YG; // <-- Плагин YG (PluginYG)

[Serializable]
public class LocalizationLanguageFontMapping
{
    public string languageCode; // "en", "ru", "ja", ...
    public TMP_FontAsset primaryFont;   // Primary font used by default
    public TMP_FontAsset secondaryFont; // Optional secondary font (for FontStyleType.Secondary)
}

[Serializable]
public class LocalizationItem { public string key; public string value; }
[Serializable]
public class LocalizationData { public LocalizationItem[] items; }

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager instance { get; private set; }
    public static event Action OnLocalizationLoaded;
    public string CurrentLanguage { get; private set; }

    [Header("Files & Language")]
    [Tooltip("Имя подпапки в StreamingAssets (если файлы в подпапке)")]
    public string streamingFolder = "";
    [Tooltip("Код языка по умолчанию")]
    public string defaultLanguage = "en";

    [Header("Fonts")]
    public LocalizationLanguageFontMapping[] languageFontMappings;

    [Header("Runtime behavior")]
    [Tooltip("Если true — поддерживается регистрационная модель (регистрация TMP/LocalizedText).")]
    public bool supportRegistration = true;
    [Tooltip("Если true — при загрузке локализации выполняется одноразовый chunked-апдейт всех LocalizedText (fallback для тех, кто не регистрируется).")]
    public bool chunkedUpdateForLocalizedText = true;
    [Tooltip("Сколько LocalizedText объектов обновлять за кадр при chunked-апдейте.")]
    public int localizedTextChunkSize = 40;
    [Tooltip("Если true — ForceMeshUpdate выполняется отложенно (по одному за кадр). Уменьшает spike.")]
    public bool deferredForceMeshUpdate = true;

    const string PREF_SELECTED_LANG = "SelectedLanguage";
    const string MISSING_TEXT = "Localized text not found";

    // dictionary with localization
    private Dictionary<string, string> localizedText;

    // state
    private bool isReady = false;
    public bool IsReady() => isReady;

    // registration sets (optional)
    private readonly HashSet<TMPro.TextMeshProUGUI> registeredTmps = new HashSet<TextMeshProUGUI>();
    private readonly HashSet<LocalizedText> registeredLocalizedTexts = new HashSet<LocalizedText>();

    // to avoid reapplying fonts repeatedly
    private readonly HashSet<int> processedTmpInstanceIds = new HashSet<int>();

    // deferred force queue
    private readonly Queue<TextMeshProUGUI> deferredForceQueue = new Queue<TextMeshProUGUI>();
    private Coroutine deferredForceCoroutine;

    // to cancel chunked loads if needed
    private Coroutine chunkedLocalizedTextUpdateCoroutine;

    // avoid recursion when we call YG2.SwitchLanguage -> YG2.onSwitchLang triggers back
    private bool suppressYGEvent = false;

    // Static initializer: подпишемся на onCorrectLang ещё до загрузки сцен (рекомендация из доки)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitPluginYGCorrectLangHook()
    {
        // Если плагин установлен - подпишемся и при необходимости скорректируем код языка
        try
        {
            YG2.onCorrectLang += (string lang) =>
            {
                if (string.IsNullOrEmpty(lang)) return;
                // Normalize like your NormalizeLangCode: берем первую часть до '-' и lowercase
                var parts = lang.Split('-');
                string normalized = parts[0].ToLowerInvariant();

                // Если язык не установлен в PlayerPrefs, установим его
                if (string.IsNullOrEmpty(PlayerPrefs.GetString(PREF_SELECTED_LANG, "")))
                {
                    PlayerPrefs.SetString(PREF_SELECTED_LANG, normalized);
                    PlayerPrefs.Save();
                }
                // Если уже есть, не перезаписываем — поведение можно изменить по желанию
            };
        }
        catch (Exception)
        {
            // плагин отсутствует / нет доступа — тихо игнорируем
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // ensure language selection exists
        string lang = PlayerPrefs.GetString(PREF_SELECTED_LANG, "");
        if (string.IsNullOrEmpty(lang))
        {
            PlayerPrefs.SetString(PREF_SELECTED_LANG, defaultLanguage);
            PlayerPrefs.Save();
        }
        CurrentLanguage = PlayerPrefs.GetString(PREF_SELECTED_LANG, defaultLanguage);

        // Подпишемся на событие смены языка в PluginYG
        try
        {
            YG2.onSwitchLang += OnYG2SwitchLang;
        }
        catch (Exception)
        {
            // если плагин отсутствует — игнорируем
        }

        // start loading immediately
        StartCoroutine(LoadLocalizedTextCoroutine());
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        try { YG2.onSwitchLang -= OnYG2SwitchLang; } catch { }
    }

    // Обработчик события от YG2
    private void OnYG2SwitchLang(string lang)
    {
        if (string.IsNullOrEmpty(lang)) return;
        string normalized = NormalizeLangCode(lang);

        // Если уже стоит тот же язык — ничего не делаем
        if (string.Equals(PlayerPrefs.GetString(PREF_SELECTED_LANG, defaultLanguage), normalized, StringComparison.OrdinalIgnoreCase))
            return;

        // Если этот вызов пришёл от нас — игнорируем
        if (suppressYGEvent) return;

        // Применяем локаль локально (не вызываем YG2.SwitchLanguage чтобы не зациклиться)
        SetLanguageInternal(normalized);
    }

    // Публичный метод для смены языка (например, UI-кнопка)
    // Если PluginYG есть — лучше вызывать через YG2.SwitchLanguage (чтобы платформа знала)
    public void SetLanguage(string langCode)
    {
        if (string.IsNullOrEmpty(langCode)) return;
        string normalized = NormalizeLangCode(langCode);

        try
        {
            // Если уже тот же язык — применим локально (и обновим state / prefs)
            if (string.Equals(PlayerPrefs.GetString(PREF_SELECTED_LANG, defaultLanguage), normalized, StringComparison.OrdinalIgnoreCase))
            {
                PlayerPrefs.SetString(PREF_SELECTED_LANG, normalized);
                PlayerPrefs.Save();

                CurrentLanguage = normalized;
                processedTmpInstanceIds.Clear();

                SetLanguageInternal(normalized);
                return;
            }

            // Сохраняем выбор заранее — чтобы другие системы видели актуальный код языка,
            // но реальную загрузку локализации ожидаем от YG2.onSwitchLang callback'а.
            PlayerPrefs.SetString(PREF_SELECTED_LANG, normalized);
            PlayerPrefs.Save();

            // Установим флаг чтобы при callback от YG2 не зациклиться
            suppressYGEvent = true;
            YG2.SwitchLanguage(normalized);
            suppressYGEvent = false;
        }
        catch (Exception)
        {
            // Плагин недоступен — просто применим локально и обновим состояние
            PlayerPrefs.SetString(PREF_SELECTED_LANG, normalized);
            PlayerPrefs.Save();

            CurrentLanguage = normalized;
            processedTmpInstanceIds.Clear();

            SetLanguageInternal(normalized);
        }
    }


    // Внутренний метод, который действительно применяет язык (без вызова YG2.SwitchLanguage)
    private void SetLanguageInternal(string normalized)
    {
        if (string.IsNullOrEmpty(normalized)) return;
        PlayerPrefs.SetString(PREF_SELECTED_LANG, normalized);
        PlayerPrefs.Save();

        processedTmpInstanceIds.Clear();
        StartCoroutine(LoadLocalizedTextCoroutine());
    }

    public string GetLocalizedValue(string key)
    {
        if (!isReady || localizedText == null) return MISSING_TEXT;
        if (string.IsNullOrEmpty(key)) return MISSING_TEXT;
        if (localizedText.TryGetValue(key, out var v)) return v;
        return MISSING_TEXT;
    }

    private IEnumerator LoadLocalizedTextCoroutine()
    {
        isReady = false;
        localizedText = null;

        string language = PlayerPrefs.GetString(PREF_SELECTED_LANG, defaultLanguage);
        if (string.IsNullOrEmpty(language)) language = defaultLanguage;

        string filename = language + ".json";
        string filePath = string.IsNullOrEmpty(streamingFolder)
            ? Path.Combine(Application.streamingAssetsPath, filename)
            : Path.Combine(Path.Combine(Application.streamingAssetsPath, streamingFolder), filename);

#if UNITY_WEBGL && !UNITY_EDITOR
        string url = filePath;
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
#if UNITY_2020_1_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError($"[LocalizationManager] Localization load error: {req.error} (url: {url})");
                yield break;
            }
            ParseLocalizationJson(req.downloadHandler.text);
        }
#else
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[LocalizationManager] Localization file not found: {filePath}");
            yield break;
        }
        string json = File.ReadAllText(filePath);
        ParseLocalizationJson(json);
        yield return null;
#endif
    }

    private void ParseLocalizationJson(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<LocalizationData>(json);
            if (data == null || data.items == null)
            {
                Debug.LogError("[LocalizationManager] Localization JSON parse error or items==null");
                return;
            }

            localizedText = new Dictionary<string, string>(data.items.Length);
            foreach (var it in data.items) localizedText[it.key] = it.value;

            isReady = true;

            ApplyLocalizationToRegistered();

            if (chunkedUpdateForLocalizedText)
            {
                if (chunkedLocalizedTextUpdateCoroutine != null) StopCoroutine(chunkedLocalizedTextUpdateCoroutine);
                chunkedLocalizedTextUpdateCoroutine = StartCoroutine(ChunkedUpdateAllLocalizedTextCoroutine());
            }

            try { OnLocalizationLoaded?.Invoke(); } catch { }

            Debug.Log($"[LocalizationManager] Localization loaded: {localizedText.Count} entries.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalizationManager] Exception parsing localization JSON: {e.Message}");
        }
    }

    private void ApplyLocalizationToRegistered()
    {
        foreach (var lt in registeredLocalizedTexts)
        {
            if (lt == null) continue;
            try { lt.UpdateText(); } catch { }
        }

        foreach (var tmp in registeredTmps)
        {
            if (tmp == null) continue;
            ApplyFontToTMPIfNeeded(tmp);
        }
    }

    private IEnumerator ChunkedUpdateAllLocalizedTextCoroutine()
    {
        var all = UnityEngine.Object.FindObjectsOfType<LocalizedText>(true);
        int total = all.Length;
        int idx = 0;
        while (idx < total)
        {
            int end = Math.Min(total, idx + Math.Max(1, localizedTextChunkSize));
            for (int i = idx; i < end; i++)
            {
                var lt = all[i];
                if (lt == null) continue;
                if (registeredLocalizedTexts.Contains(lt)) continue;
                try { lt.UpdateText(); } catch { }
            }
            idx = end;
            yield return null;
        }
        chunkedLocalizedTextUpdateCoroutine = null;
    }

    public void RegisterLocalizedText(LocalizedText lt)
    {
        if (lt == null) return;
        registeredLocalizedTexts.Add(lt);
        if (isReady) { try { lt.UpdateText(); } catch { } }
    }

    public void UnregisterLocalizedText(LocalizedText lt)
    {
        if (lt == null) return;
        registeredLocalizedTexts.Remove(lt);
    }

    public void RegisterTMP(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        registeredTmps.Add(tmp);
        if (isReady) ApplyFontToTMPIfNeeded(tmp);
    }

    public void UnregisterTMP(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        registeredTmps.Remove(tmp);
        processedTmpInstanceIds.Remove(tmp.GetInstanceID());
    }

    public void ApplyFontToTMPIfNeeded(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;

        int id = tmp.GetInstanceID();
        if (processedTmpInstanceIds.Contains(id)) return;

        TMP_FontAsset fontToApply = null;
        var lt = tmp.GetComponent<LocalizedText>();
        string lang = PlayerPrefs.GetString(PREF_SELECTED_LANG, defaultLanguage);
        if (string.IsNullOrEmpty(lang)) lang = defaultLanguage;

        var mapping = FindFontMappingForLang(lang);

        if (mapping != null)
        {
            if (lt != null && lt.fontStyle == LocalizedText.FontStyleType.Secondary && mapping.secondaryFont != null)
                fontToApply = mapping.secondaryFont;
            else
                fontToApply = mapping.primaryFont;
        }
        else
        {
            var defMapping = FindFontMappingForLang(defaultLanguage);
            if (lt != null && lt.fontStyle == LocalizedText.FontStyleType.Secondary && defMapping != null && defMapping.secondaryFont != null)
                fontToApply = defMapping.secondaryFont;
            else if (defMapping != null)
                fontToApply = defMapping.primaryFont;
        }

        if (fontToApply != null)
        {
            tmp.font = fontToApply;
            if (fontToApply.material != null) tmp.fontSharedMaterial = fontToApply.material;
        }

        tmp.SetAllDirty();

        if (deferredForceMeshUpdate)
            EnqueueDeferredForce(tmp);
        else
        {
            try { tmp.ForceMeshUpdate(false, false); } catch { }
        }

        processedTmpInstanceIds.Add(id);
    }

    private LocalizationLanguageFontMapping FindFontMappingForLang(string lang)
    {
        if (languageFontMappings == null) return null;
        for (int i = 0; i < languageFontMappings.Length; i++)
        {
            if (string.Equals(languageFontMappings[i].languageCode, lang, StringComparison.OrdinalIgnoreCase))
                return languageFontMappings[i];
        }
        return null;
    }

    private void EnqueueDeferredForce(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        deferredForceQueue.Enqueue(tmp);
        if (deferredForceCoroutine == null) deferredForceCoroutine = StartCoroutine(DeferredForceWorker());
    }

    private IEnumerator DeferredForceWorker()
    {
        while (deferredForceQueue.Count > 0)
        {
            var t = deferredForceQueue.Dequeue();
            if (t != null)
            {
                try { t.ForceMeshUpdate(false, false); } catch { }
            }
            yield return null;
        }
        deferredForceCoroutine = null;
    }

    private string NormalizeLangCode(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return defaultLanguage;
        var parts = raw.Split('-');
        return parts[0].ToLowerInvariant();
    }
}
