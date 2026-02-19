// ICardGameMode.cs
using UnityEngine;
using static KlondikeModeManager;

public interface ICardGameMode
{
    // Методы
    void CheckGameState();
    void OnUndoAction();
    void OnStockClicked(); // Для клика по колоде

    // Свойства (Все с Большой Буквы!)
    RectTransform DragLayer { get; }
    AnimationService AnimationService { get; }
    PileManager PileManager { get; }

    // --- НОВЫЕ СВОЙСТВА ДЛЯ ИСПРАВЛЕНИЯ ОШИБОК ---
    AutoMoveService AutoMoveService { get; } // Был autoMoveService
    Canvas RootCanvas { get; }               // Был canvas
    float TableauVerticalGap { get; }        // Был tableauVerticalGap
    StockDealMode StockDealMode { get; }     // Для логики раздачи (1 или 3 карты)
    bool IsInputAllowed { get; set; }
    void OnCardDoubleClicked(CardController card);

    // --- НОВОЕ: Для GameUIController ---
    string GameName { get; } // Чтобы UI знал имя игры для статистики ("Klondike", "Spider")
    GameType GameType { get; }
    void RestartGame();      // Универсальный метод перезапуска
    bool IsMatchInProgress();
}