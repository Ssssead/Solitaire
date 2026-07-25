using System;
using System.IO;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class OctagonTelemetryLogger : MonoBehaviour
{
    public static OctagonTelemetryLogger Instance { get; private set; }

    [Header("Network Settings")]
    [Tooltip("URL вашего бэкенда. Оставьте пустым, если пока хотите писать только в локальный файл.")]
    public string serverURL = "";

    private StringBuilder moveLog = new StringBuilder();
    private int moveCounter = 0;
    private Difficulty currentDifficulty;
    private bool isLogging = false;

    private string initialDealString = "";
    private string solverSolutionString = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- 1. СТАРТ ЗАПИСИ ---
    public void StartGameLog(Difficulty difficulty)
    {
        if (GameSettings.IsTutorialMode) return; // Игнорируем обучение

        currentDifficulty = difficulty;
        moveLog.Clear();
        moveCounter = 0;
        initialDealString = "";
        solverSolutionString = "";
        isLogging = true;

        Debug.Log("[Telemetry] Начата запись партии Восьмиугольника.");
    }

    // --- 2. ЗАПИСЬ НАЧАЛЬНОГО РАСКЛАДА ---
    public void RecordInitialDeal(Deal deal)
    {
        if (!isLogging || deal == null) return;

        StringBuilder sb = new StringBuilder();

        // Стол (Tableau)
        sb.Append("T:");
        for (int g = 0; g < deal.tableau.Count; g++)
        {
            for (int s = 0; s < deal.tableau[g].Count; s++)
            {
                sb.Append(GetCardCode(deal.tableau[g][s].Card)).Append(",");
            }
            sb.Append(";");
        }

        // Колода (Stock)
        sb.Append("|S:");
        var stockArr = deal.stock.ToArray();
        for (int i = stockArr.Length - 1; i >= 0; i--)
        {
            sb.Append(GetCardCode(stockArr[i].Card)).Append(",");
        }

        initialDealString = sb.ToString().TrimEnd(',');
    }

    // --- 3. ЗАПИСЬ ПУТИ РЕШЕНИЯ ОТ СОЛВЕРА ---
    public void RecordSolverPath(string path)
    {
        if (isLogging) solverSolutionString = path;
    }

    // --- 4. ЗАПИСЬ ИГРОВЫХ ХОДОВ И ДЕЙСТВИЙ ---
    public void LogMove(CardModel card, ICardContainer source, ICardContainer target)
    {
        if (!isLogging) return;

        string cardStr = GetCardCode(card);
        string srcStr = GetContainerCode(source);
        string tgtStr = GetContainerCode(target);

        moveLog.Append($"{++moveCounter}:{cardStr}>{srcStr}>{tgtStr}|");
    }

    public void LogAction(string actionCode)
    {
        if (!isLogging) return;
        moveLog.Append($"{++moveCounter}:{actionCode}|");
    }

    // --- 5. ФИНАЛИЗАЦИЯ И ОТПРАВКА ---
    public void FinalizeLog(string endReason, int finalScore)
    {
        if (!isLogging) return;
        isLogging = false;

        string finalLog = moveLog.ToString();

        // Формируем JSON Payload
        string jsonPayload = $@"{{
            ""timestamp"": ""{DateTime.Now:s}"",
            ""game"": ""Octagon"",
            ""difficulty"": ""{currentDifficulty}"",
            ""reason"": ""{endReason}"",
            ""score"": {finalScore},
            ""moves"": {moveCounter},
            ""deal"": ""{initialDealString}"",
            ""solver_path"": ""{solverSolutionString}"",
            ""player_log"": ""{finalLog}""
        }}";

        // 1. Всегда сохраняем локально (для надежности и дебага)
        SaveToLocalFile(jsonPayload.Replace("\n", "").Replace("\r", "").Replace("  ", ""));

        // 2. Если указан URL, отправляем на сервер
        if (!string.IsNullOrEmpty(serverURL))
        {
            StartCoroutine(PostDataToServer(jsonPayload));
        }

        Debug.Log($"[Telemetry] Игра завершена ({endReason}). Очки: {finalScore}");
        moveLog.Clear();
    }

    private void SaveToLocalFile(string data)
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "OctagonTelemetry.jsonl");
            File.AppendAllText(path, data + Environment.NewLine);
            Debug.Log($"[Telemetry] Данные локально записаны в: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Telemetry] Ошибка локальной записи: {e.Message}");
        }
    }

    private IEnumerator PostDataToServer(string jsonData)
    {
        using (UnityWebRequest request = new UnityWebRequest(serverURL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogError($"[Telemetry] Ошибка отправки на сервер: {request.error}");
            else
                Debug.Log("[Telemetry] Лог успешно отправлен на сервер!");
        }
    }

    // --- СЛОВАРИ КОДИРОВАНИЯ ---
    private string GetCardCode(CardModel model)
    {
        char suit = model.suit.ToString()[0]; // S, H, C, D
        return $"{suit}{model.rank}";
    }

    private string GetContainerCode(ICardContainer container)
    {
        if (container is OctagonStockPile) return "St";
        if (container is OctagonWastePile) return "W";
        if (container is OctagonFoundationPile fp) return $"F{fp.transform.GetSiblingIndex()}";
        if (container is OctagonTableauSlot slot)
        {
            int gIndex = slot.Group != null ? slot.Group.transform.GetSiblingIndex() : 0;
            return $"T{gIndex}{slot.SlotIndex}"; // Пример: T04 (Группа 0, нижний слот 4)
        }
        return "?";
    }
}