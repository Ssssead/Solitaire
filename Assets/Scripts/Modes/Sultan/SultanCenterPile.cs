using UnityEngine;

public class SultanCenterPile : MonoBehaviour, ICardContainer
{
    public Transform Transform => transform;
    private SultanModeManager _mode;

    public void Initialize(SultanModeManager mode, RectTransform tf)
    {
        _mode = mode;
    }

    // Султан в центре никогда не принимает новые карты
    public bool CanAccept(CardController card) => false;

    public void AcceptCard(CardController card)
    {
        if (card == null) return;
        card.transform.SetParent(transform);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.localRotation = Quaternion.identity;

        // Отключаем взаимодействие, чтобы Султан навсегда остался в центре
        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
    }

    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;

    public void Clear()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
    }
}