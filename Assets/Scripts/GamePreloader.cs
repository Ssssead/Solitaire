using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;

public class GamePreloader : MonoBehaviour
{
    public Slider loadingBar;
    public Text progressText;
    public string menuSceneName = "MenuScene";

    [Tooltip("Label (метка) Addressables, а НЕ имя группы. Метка должна быть назначена ассетам группы MenuAssets.")]
    public string menuAssetsLabel = "MenuAssets";

    private void Start()
    {
        StartCoroutine(PreloadGameRoutine());
    }

    private IEnumerator PreloadGameRoutine()
    {
        // false = не освобождать хэндл автоматически. По умолчанию InitializeAsync() освобождает
        // хэндл сразу после завершения, и любое обращение к initOp.Status после yield кидает
        // "Attempting to use an invalid operation handle".
        var initOp = Addressables.InitializeAsync(false);
        yield return initOp;

        bool initOk = initOp.Status == AsyncOperationStatus.Succeeded;
        Addressables.Release(initOp);

        if (!initOk)
        {
            Debug.LogError("[GamePreloader] Addressables.InitializeAsync не удался. Проверь Remote Load Path и что каталог (catalog_*.json / .hash) лежит по этому пути.");
            yield break;
        }

        // DownloadDependenciesAsync по умолчанию НЕ освобождает хэндл сам — Release нужен вручную.
        var downloadOp = Addressables.DownloadDependenciesAsync(menuAssetsLabel);

        while (!downloadOp.IsDone)
        {
            if (loadingBar != null) loadingBar.value = downloadOp.PercentComplete;
            if (progressText != null) progressText.text = $"{(downloadOp.PercentComplete * 100):0}%";
            yield return null;
        }

        // Статус читаем ДО Release, после Release хэндл уже недействителен.
        bool downloadOk = downloadOp.Status == AsyncOperationStatus.Succeeded;
        Addressables.Release(downloadOp);

        if (!downloadOk)
        {
            Debug.LogError($"[GamePreloader] Не удалось скачать '{menuAssetsLabel}'. Проверь, что такая метка/адрес существует, и что .bundle файлы доступны по Remote Load Path.");
            yield break;
        }

        if (loadingBar != null) loadingBar.value = 1f;
        if (progressText != null) progressText.text = "100%";

        Addressables.LoadSceneAsync(menuSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}