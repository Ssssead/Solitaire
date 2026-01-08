
using UnityEngine;
// IModeManager.cs
public interface IModeManager
{
    /// <summary>
    /// Ќайти ближайший контейнер дл€ карты (по позиции и snapDistance)
    /// </summary>
    ICardContainer FindNearestContainer(CardController card, Vector2 anchoredPosition, float maxDistance);

    /// <summary> ≈сли карта была брошена на доску и не попала в контейнер - менеджер может прин€ть еЄ (например, drop на пустое место) </summary>
    /// <returns> true если менеджер обработал drop, false чтобы карта вернулась на исходную позицию </returns>
    bool OnDropToBoard(CardController card, Vector2 anchoredPosition);

    // optional hooks called from card
    void OnCardClicked(CardController card);
    void OnCardDoubleClicked(CardController card);
    void OnCardLongPressed(CardController card);
    void OnCardDroppedToContainer(CardController card, ICardContainer container);

    // keyboard integration example
    void OnKeyboardPick(CardController card);
}
