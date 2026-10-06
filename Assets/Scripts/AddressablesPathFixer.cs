using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

// Addressables считает путь БЕЗ "://" локальным файлом и открывает его через AssetBundle.LoadFromFile,
// а в WebGL файловой системы нет ("Unable to open archive file", "Invalid path in AssetBundleProvider").
// Поэтому относительный "Bundles/xxx.bundle" превращаем в абсолютный URL относительно страницы игры
// прямо в рантайме. Скрипт не нужно вешать на объект — он запускается сам до загрузки первой сцены.
public static class AddressablesPathFixer
{
    private const string RemoteFolder = "Bundles/";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Должно быть выставлено ДО Addressables.InitializeAsync(), поэтому BeforeSceneLoad.
        Addressables.InternalIdTransformFunc = TransformInternalId;
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static string baseUrl;

    private static string TransformInternalId(IResourceLocation location)
    {
        string id = location.InternalId;

        // Уже полноценный URL (StreamingAssets, внешний CDN) или не наша папка — не трогаем.
        if (id.Contains("://") || !id.StartsWith(RemoteFolder)) return id;

        if (baseUrl == null) baseUrl = GetBaseUrl();
        return baseUrl + id;
    }

    // Application.absoluteURL = адрес страницы с игрой, например
    // https://host/path/index.html?draft=true&... — берём папку без имени файла и без query/hash.
    private static string GetBaseUrl()
    {
        string url = Application.absoluteURL;

        int cut = url.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) url = url.Substring(0, cut);

        int slash = url.LastIndexOf('/');
        return slash >= 0 ? url.Substring(0, slash + 1) : url;
    }
#endif
}