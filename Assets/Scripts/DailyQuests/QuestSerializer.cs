using System;
using System.IO;
using UnityEngine;

public static class QuestSerializer
{
    public static string Serialize(QuestSaveData data)
    {
        if (data == null) return "";

        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // Пакуем JSON строку в бинарный поток (как в StatsSerializer)
            string json = JsonUtility.ToJson(data);
            writer.Write(json ?? "");

            return Convert.ToBase64String(ms.ToArray());
        }
    }

    public static void Deserialize(string data, out QuestSaveData questData)
    {
        questData = new QuestSaveData();

        if (string.IsNullOrEmpty(data)) return;

        try
        {
            byte[] bytes = Convert.FromBase64String(data);
            using (var ms = new MemoryStream(bytes))
            using (var reader = new BinaryReader(ms))
            {
                // Достаем защищенную строку обратно
                string json = reader.ReadString();
                if (!string.IsNullOrEmpty(json))
                {
                    questData = JsonUtility.FromJson<QuestSaveData>(json);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[QuestSerializer] Ошибка десериализации: {e.Message}");
        }

        if (questData == null) questData = new QuestSaveData();
    }
}