using System.Collections.Generic;
using UnityEngine;

// Единый интерфейс для всех контроллеров интро-анимаций
public interface IIntroController
{
    // Возвращает список элементов, которые должны уезжать ВВЕРХ за экран
    List<RectTransform> GetTopUIElements();

    // Возвращает список элементов, которые должны уезжать ВНИЗ за экран
    List<RectTransform> GetBottomUIElements();
}