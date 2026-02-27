using UnityEngine;

public class SultanWastePile : MonoBehaviour, ICardContainer
{
    public Transform Transform => transform;
    private SultanModeManager _mode;

    public void Initialize(SultanModeManager mode, RectTransform tf)
    {
        _mode = mode;
    }

    // В сброс нельзя перетаскивать карты руками (защита от игрока)
    public bool CanAccept(CardController card) => false;

    // --- ИСПРАВЛЕНИЕ: Теперь метод корректно принимает карту от сервиса анимаций ---
    public void AcceptCard(CardController card)
    {
        AddCard(card, true);
    }

    public void AddCard(CardController card, bool faceUp)
    {
        if (card == null) return;

        // Забираем карту к себе из DragLayer
        card.transform.SetParent(transform);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.SetAsLastSibling(); // Кладем поверх остальных

        var data = card.GetComponent<CardData>();
        // Передаем false (без анимации), так как карта уже перевернулась в воздухе
        if (data != null) data.SetFaceUp(faceUp, false);

        // Включаем физику, чтобы карту можно было тащить дальше
        var cg = card.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }
    }

    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;

    public void Clear()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
    }
}