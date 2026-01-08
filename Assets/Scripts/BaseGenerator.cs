// BaseGenerator.cs
using System;
using System.Collections;
using UnityEngine;

public abstract class BaseGenerator : MonoBehaviour
{
    // Тип игры, который обслуживает этот генератор
    public abstract GameType GameType { get; }

    /// <summary>
    /// Универсальный метод запуска генерации.
    /// </summary>
    /// <param name="difficulty">Сложность</param>
    /// <param name="param">Вариативный параметр (Klondike: DrawCount, Spider: SuitCount)</param>
    /// <param name="onComplete">Callback с результатом</param>
    public abstract IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete);
}