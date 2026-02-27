using System.Collections;
using UnityEngine;

public class SultanAutoMoveService : MonoBehaviour
{
    private SultanModeManager _mode;
    private SultanPileManager _pileManager;
    private UndoManager _undoManager;
    private RectTransform _dragLayer;

    public void Initialize(SultanModeManager m, SultanPileManager pm, UndoManager undo, AnimationService anim, RectTransform dragLayer)
    {
        _mode = m;
        _pileManager = pm;
        _undoManager = undo;
        _dragLayer = dragLayer;
    }

    public void OnCardRightClicked(CardController card)
    {
        if (card == null || _mode == null || !_mode.IsInputAllowed) return;

        foreach (var foundation in _pileManager.Foundations)
        {
            if (foundation.CanAccept(card))
            {
                // --- «¿’¬¿“€¬¿≈Ã —Œ—“ŒﬂÕ»≈ ƒÀﬂ UNDO ---
                var sultanCard = card.GetComponent<SultanCardController>();
                if (sultanCard != null) sultanCard.CaptureStateForUndo();

                StartCoroutine(PerformMoveRoutine(card, foundation));
                return;
            }
        }
    }

    private IEnumerator PerformMoveRoutine(CardController card, SultanFoundationPile targetPile)
    {
        var sultanCard = card.GetComponent<SultanCardController>();
        if (sultanCard != null) sultanCard.SetAnimating(true);

        Transform oldParent = card.transform.parent;
        card.transform.SetParent(_dragLayer, true);

        Vector3 startPos = card.transform.position;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            card.transform.position = Vector3.Lerp(startPos, targetPile.transform.position, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        targetPile.AcceptCard(card);

        if (sultanCard != null) sultanCard.SetAnimating(false);

        // --- »«Ã≈Õ≈Õ»≈: ¬˚Á˚‚‡ÂÏ ÏÂÚÓ‰ ÏÂÌÂ‰ÊÂ‡, ˜ÚÓ·˚ ÓÌ Á‡Ò˜ËÚ‡Î Ó˜ÍË Ë ıÓ‰ ---
        _mode.OnCardDroppedToContainer(card, targetPile);
    }
}