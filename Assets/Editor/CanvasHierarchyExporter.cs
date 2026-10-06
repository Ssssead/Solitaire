using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class CanvasHierarchyExporter : MonoBehaviour
{
    [MenuItem("Tools/Export Canvas Hierarchy")]
    public static void ExportHierarchy()
    {
        GameObject selectedObj = Selection.activeGameObject;
        if (selectedObj == null)
        {
            Debug.LogWarning("Пожалуйста, выделите корневой Canvas в иерархии перед экспортом.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"--- Иерархия для: {selectedObj.name} ---");

        Traverse(selectedObj.transform, 0, sb);

        string path = Application.dataPath + "/CanvasHierarchy.txt";
        File.WriteAllText(path, sb.ToString());
        AssetDatabase.Refresh();

        Debug.Log($"[Успешно] Иерархия экспортирована в файл: {path}");
        EditorUtility.RevealInFinder(path);
    }

    private static void Traverse(Transform t, int depth, StringBuilder sb)
    {
        string indent = new string('-', depth * 2);
        string components = GetAllComponents(t);

        sb.AppendLine($"{indent} {t.name} {components}");

        foreach (Transform child in t)
        {
            Traverse(child, depth + 1, sb);
        }
    }

    private static string GetAllComponents(Transform t)
    {
        Component[] comps = t.GetComponents<Component>();
        List<string> compNames = new List<string>();

        foreach (var c in comps)
        {
            if (c == null) continue; // Защита от "Missing Script"

            string name = c.GetType().Name;

            // Игнорируем обязательные невидимые компоненты для чистоты лога
            if (name == "RectTransform" || name == "Transform" || name == "CanvasRenderer")
                continue;

            compNames.Add(name);
        }

        if (compNames.Count > 0)
            return $"[{string.Join(", ", compNames)}]";

        return "";
    }
}