/*// Editor/DealDatabaseBaker.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class DealDatabaseBaker : EditorWindow
{
    private DealDatabase db;
    private KlondikeGenerator generator;

    private int amountToGenerate = 10;
    private Difficulty difficulty = Difficulty.Medium;
    private int drawCount = 1;

    [MenuItem("Solitaire/Deal Database Baker")]
    public static void ShowWindow()
    {
        GetWindow<DealDatabaseBaker>("Deal Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Batch Deal Generator", EditorStyles.boldLabel);

        db = (DealDatabase)EditorGUILayout.ObjectField("Database Asset", db, typeof(DealDatabase), false);
        generator = (KlondikeGenerator)EditorGUILayout.ObjectField("Generator Prefab/SceneObj", generator, typeof(KlondikeGenerator), true);

        EditorGUILayout.Space();

        difficulty = (Difficulty)EditorGUILayout.EnumPopup("Difficulty", difficulty);
        drawCount = EditorGUILayout.IntSlider("Draw Count (Param)", drawCount, 1, 3);
        amountToGenerate = EditorGUILayout.IntField("Amount to Generate", amountToGenerate);

        if (GUILayout.Button("Generate & Add to Database"))
        {
            BakeDeals();
        }

        if (GUILayout.Button("Clear This Set"))
        {
            ClearSet();
        }
    }

    private void BakeDeals()
    {
        // 1. Проверка ссылок
        if (db == null || generator == null)
        {
            Debug.LogError("Please assign Database Asset and Generator Script in the window!");
            return;
        }

        // 2. Получаем или создаем набор (DealSet) для текущих настроек
        DealSet set = db.GetSet(GameType.Klondike, difficulty, drawCount);
        if (set == null)
        {
            set = new DealSet
            {
                name = $"Klondike_{difficulty}_Draw{drawCount}",
                gameType = GameType.Klondike,
                difficulty = difficulty,
                param = drawCount
            };
            db.dealSets.Add(set);
            Debug.Log($"Created new set: {set.name}");
        }

        int addedCount = 0;
        int safetyCounter = 0; // Защита от бесконечного цикла при неудачах
        int maxSafetyAttempts = amountToGenerate * 50; // Максимум 50 попыток на 1 успешный расклад

        double startTime = EditorApplication.timeSinceStartup;

        // 3. Основной цикл генерации
        for (int i = 0; i < amountToGenerate; i++)
        {
            // Обновляем прогресс-бар (можно нажать Cancel)
            if (EditorUtility.DisplayCancelableProgressBar("Baking Deals",
                $"Generating {difficulty} (Draw {drawCount})... {i}/{amountToGenerate} found.",
                (float)i / amountToGenerate))
            {
                Debug.Log("Generation cancelled by user.");
                break;
            }

            DealMetrics metrics;

            // ВЫЗОВ СИНХРОННОГО МЕТОДА ГЕНЕРАТОРА
            // Этот метод блокирует поток, пока не найдет решение или не сдастся
            Deal rawDeal = generator.GenerateSynchronous(difficulty, drawCount, out metrics);

            // Проверка результата
            if (rawDeal != null && metrics.Solved)
            {
                // Упаковываем и добавляем в базу
                set.deals.Add(PackDeal(rawDeal));
                addedCount++;
            }
            else
            {
                // Если не получилось найти хороший расклад:
                // Откатываем счетчик i назад, чтобы попробовать снова для этой же позиции
                i--;

                safetyCounter++;
                if (safetyCounter > maxSafetyAttempts)
                {
                    Debug.LogError("Generator failed too many times. Stopping to prevent freeze. Check Generator settings.");
                    break;
                }
            }
        }

        // 4. Очистка и Сохранение
        EditorUtility.ClearProgressBar();

        if (addedCount > 0)
        {
            EditorUtility.SetDirty(db);   // Помечаем файл как измененный
            AssetDatabase.SaveAssets();   // Записываем на диск
            AssetDatabase.Refresh();      // Обновляем Unity Editor

            double duration = EditorApplication.timeSinceStartup - startTime;
            Debug.Log($"<color=green>SUCCESS:</color> Added {addedCount} deals to Set '{set.name}'. Total in set: {set.deals.Count}. Time: {duration:F2}s");
        }
        else
        {
            Debug.LogWarning("No deals were added. Generator returned null or unsolved deals.");
        }
    }

    private void ClearSet()
    {
        if (db == null) return;
        DealSet set = db.GetSet(GameType.Klondike, difficulty, drawCount);
        if (set != null)
        {
            set.deals.Clear();
            EditorUtility.SetDirty(db);
            Debug.Log("Set cleared.");
        }
    }

    private SerializedDeal PackDeal(Deal d)
    {
        SerializedDeal sd = new SerializedDeal();

        // Tableau
        foreach (var pile in d.tableau)
        {
            var sPile = new SerializedPile();
            foreach (var card in pile) sPile.cards.Add(new SerializedCard(card));
            sd.tableau.Add(sPile);
        }

        // Stock (в базе List, в игре Stack)
        // В Deal.stock порядок стековый. Сохраним его как есть (IEnumerable идет сверху вниз для стека или в порядке создания?)
        // Stack enumeration: Top to Bottom.
        // Сохраним в список как есть. При загрузке будем делать Push в обратном порядке.
        foreach (var card in d.stock) sd.stock.Add(new SerializedCard(card));

        return sd;
    }
}
*/