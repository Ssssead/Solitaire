using UnityEngine;
using UnityEngine.UI;

public class SpiderFoundationPile : MonoBehaviour, ICardContainer
{
    [Header("Spider References")]
    // Ссылка нужна для доступа к ScoreManager. 
    // Unity сама найдет её в Start, если вы забудете назначить.
    public SpiderModeManager spiderMode;

    [Header("Visuals")]
    public Image iconImage; // Опционально: иконка масти

    private bool isFull = false;

    // --- ICardContainer Свойства ---
    public Transform Transform => transform;
    public bool IsFull => isFull;

    private void Start()
    {
        // Автоматический поиск менеджера, если не назначен в инспекторе
        if (spiderMode == null)
        {
            spiderMode = FindObjectOfType<SpiderModeManager>();
        }
    }

    // --- ЛОГИКА ИСПРАВЛЕНИЯ БАГА (Farming Exploit) ---
    // Этот метод вызывается Unity автоматически при изменении иерархии (добавили/убрали карту)
    private void OnTransformChildrenChanged()
    {
        // Если детей стало 0, значит UndoManager забрал карты обратно на стол
        if (transform.childCount == 0)
        {
            // Если стопка считалась полной
            if (isFull)
            {
                isFull = false;
                Debug.Log("[Foundation] Emptied via Undo. Resetting status.");

                // Убираем незаслуженные очки (+100), которые игрок получил за сборку
                if (spiderMode != null && spiderMode.ScoreManager != null)
                {
                    spiderMode.ScoreManager.RemoveRowBonus();
                    Debug.Log("[Score] Row Bonus Reverted (-100).");
                }
            }
        }
    }
    // -------------------------------------------------

    public void SetCompleted(Suit suit)
    {
        isFull = true;
        // if (iconImage) iconImage.sprite = ... (код для иконки)
        Debug.Log($"Foundation completed: {suit}");
    }

    // --- ICardContainer Реализация ---

    // Игрок не может класть карты в Foundation вручную (только авто-сбор)
    public bool CanAccept(CardController card) => false;

    // Вызывается кодом авто-сбора, когда карты прилетают
    public void AcceptCard(CardController card)
    {
        card.transform.SetParent(transform);
        card.rectTransform.anchoredPosition = Vector2.zero;
        card.transform.SetAsLastSibling();
    }

    public void OnCardIncoming(CardController card) { }

    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
}