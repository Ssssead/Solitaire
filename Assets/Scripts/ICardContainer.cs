// ICardContainer.cs
using UnityEngine;

public interface ICardContainer
{
    /// <summary> Transform to parent card when accepted </summary>
    Transform Transform { get; }

    /// <summary> Whether container accepts this specific card (game rules check) </summary>
    bool CanAccept(CardController card);

    /// <summary> Called when card is incoming (for visual feedback) </summary>
    void OnCardIncoming(CardController card);

    /// <summary> Return anchored position (local to container) for card when dropped </summary>
    Vector2 GetDropAnchoredPosition(CardController card);

    /// <summary> Accept card (do internal state changes like push to stack) </summary>
    void AcceptCard(CardController card);
}
