using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using YG; // Подключаем пространство имен плагина[cite: 5]

public class SimpleMetricsTracker : MonoBehaviour
{
    public static SimpleMetricsTracker Instance;

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

    private void Start()
    {
        // При старте игры замеряем ориентацию экрана
        TrackScreenOrientation();

        // Подписываемся на смену сцен
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Фиксируем стартовую сцену
        TrackSceneState(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrackSceneState(scene.name);
    }

    // --- ОТСЛЕЖИВАНИЕ ТОЧКИ ВЫХОДА И СОСТОЯНИЯ ---

    private void TrackSceneState(string sceneName)
    {
        var eventData = new Dictionary<string, string>
        {
            { "scene_name", sceneName }
        };

        YG2.MetricaSend("scene_entered", eventData); //[cite: 5]
        Debug.Log($"<color=#00FF00>[Метрика]</color> Отправлено событие: <b>scene_entered</b> | Параметр: {sceneName}");
    }

    private void TrackScreenOrientation()
    {
        string orientation = Screen.width > Screen.height ? "landscape" : "portrait";

        var eventData = new Dictionary<string, string>
        {
            { "orientation", orientation }
        };

        YG2.MetricaSend("screen_orientation", eventData); //[cite: 5]
        Debug.Log($"<color=#00FF00>[Метрика]</color> Отправлено событие: <b>screen_orientation</b> | Параметр: {orientation}");
    }

    // --- БАЗОВЫЙ ЦИКЛ ГЕЙМПЛЕЯ ---

    public void TrackLevelStart(string gameName, string difficulty)
    {
        var eventData = new Dictionary<string, string>
        {
            { "game_name", gameName },
            { "difficulty", difficulty }
        };

        YG2.MetricaSend("level_start", eventData);
        Debug.Log($"<color=#00FF00>[Метрика]</color> Отправлено событие: <b>level_start</b> | Игра: {gameName} | Сложность: {difficulty}");
    }

    public void TrackLevelWin(string gameName, string difficulty)
    {
        var eventData = new Dictionary<string, string>
        {
            { "game_name", gameName },
            { "difficulty", difficulty }
        };

        YG2.MetricaSend("level_win", eventData);
        Debug.Log($"<color=#00FF00>[Метрика]</color> Отправлено событие: <b>level_win</b> | Игра: {gameName} | Сложность: {difficulty}");
    }

    public void TrackLevelQuit(string gameName, string difficulty)
    {
        var eventData = new Dictionary<string, string>
        {
            { "game_name", gameName },
            { "difficulty", difficulty }
        };

        YG2.MetricaSend("level_quit", eventData);
        Debug.Log($"<color=#00FF00>[Метрика]</color> Отправлено событие: <b>level_quit</b> (Сдался) | Игра: {gameName} | Сложность: {difficulty}");
    }

    // --- НОВЫЕ ФИЧИ ---

    public void TrackHintUsed()
    {
        YG2.MetricaSend("action_hint_used"); //[cite: 5]
        Debug.Log("<color=#00FF00>[Метрика]</color> Отправлено событие: <b>action_hint_used</b> (без параметров)");
    }
}