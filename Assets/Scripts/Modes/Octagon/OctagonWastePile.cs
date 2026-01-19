using UnityEngine;

public class OctagonWastePile : MonoBehaviour, ICardContainer
{
    [SerializeField] private float spacing = 35f;

    public Transform Transform => transform;

    // В Waste руками класть нельзя (только из Stock по клику)
    public bool CanAccept(CardController card) => false;

    public void AcceptCard(CardController card)
    {
        AddCard(card);
    }

    public void AddCard(CardController card)
    {
        card.transform.SetParent(transform);

        // В Waste карты открыты и интерактивны
        var data = card.GetComponent<CardData>();
        if (data) data.SetFaceUp(true, true);

        var cg = card.GetComponent<CanvasGroup>();
        if (cg) cg.blocksRaycasts = true;

        UpdateLayout();
    }

    public void UpdateLayout()
    {
        int count = transform.childCount;
        if (count == 0) return;

        // Показываем последние 3 карты
        int startIndex = Mathf.Max(0, count - 3);

        for (int i = 0; i < count; i++)
        {
            Transform child = transform.GetChild(i);
            if (i >= startIndex)
            {
                float offset = (i - startIndex) * spacing;
                child.localPosition = new Vector3(offset, 0, 0);
            }
            else
            {
                child.localPosition = Vector3.zero;
            }
        }
    }

    public void OnCardIncoming(CardController card) { }
    public Vector2 GetDropAnchoredPosition(CardController card) => Vector2.zero;
}