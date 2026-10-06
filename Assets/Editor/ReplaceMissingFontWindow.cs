using UnityEngine;
using UnityEditor;
using TMPro;

public class ReplaceMissingFontWindow : EditorWindow
{
    public TMP_FontAsset newFont;

    [MenuItem("Tools/Fix Missing TMP Fonts")]
    public static void ShowWindow()
    {
        GetWindow<ReplaceMissingFontWindow>("Fix Missing Fonts");
    }

    private void OnGUI()
    {
        GUILayout.Label("Замена удаленных (Missing) шрифтов", EditorStyles.boldLabel);

        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Новый шрифт", newFont, typeof(TMP_FontAsset), false);

        if (GUILayout.Button("Починить везде (Сцены и Префабы)"))
        {
            if (newFont == null)
            {
                Debug.LogWarning("Назначьте новый шрифт!");
                return;
            }

            FixInActiveScene();
            FixInAllPrefabs();

            Debug.Log("Починка отсутствующих шрифтов завершена!");
        }
    }

    private void FixInActiveScene()
    {
        TextMeshProUGUI[] textComponents = FindObjectsOfType<TextMeshProUGUI>(true);
        int count = 0;

        foreach (var tmp in textComponents)
        {
            // Ищем объекты, потерявшие шрифт
            if (tmp.font == null)
            {
                Undo.RecordObject(tmp, "Fix Missing TMP Font");
                tmp.font = newFont;
                EditorUtility.SetDirty(tmp);
                count++;
            }
        }
        Debug.Log($"Восстановлено {count} объектов в активной сцене.");
    }

    private void FixInAllPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int count = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            bool modified = false;
            TextMeshProUGUI[] texts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true);

            foreach (var tmp in texts)
            {
                // Ищем потерянный шрифт внутри префабов
                if (tmp.font == null)
                {
                    tmp.font = newFont;
                    modified = true;
                    count++;
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }
        Debug.Log($"Восстановлено {count} объектов в префабах.");
    }
}