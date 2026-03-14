using System.Collections.Generic;
using UnityEngine;

public class PyramidWastePile : MonoBehaviour
{
    private List<CardController> cards = new List<CardController>();

    [Header("Visual Settings")]
    [Tooltip("Смещение каждой следующей карты влево (должно быть отрицательным)")]
    public float stackGap = -4f; // Минус сдвигает карты влево

    public void Add(CardController c)
    {
        c.transform.SetParent(transform);
        c.transform.SetAsLastSibling();

        cards.Add(c);
        UpdateLayout(); // Пересчитываем позиции
    }

    public void Remove(CardController c)
    {
        if (cards.Contains(c))
        {
            cards.Remove(c);
            UpdateLayout(); // Пересчитываем позиции
        }
    }

    public List<CardController> DrawAll()
    {
        var temp = new List<CardController>(cards);
        cards.Clear();
        return temp;
    }

    public CardController TopCard() => cards.Count > 0 ? cards[cards.Count - 1] : null;
    public bool HasCard(CardController c) => cards.Contains(c);
    public List<CardController> GetCards() => cards;

    public void Clear()
    {
        cards.Clear();
    }

    // --- НОВОЕ: Пересчет позиций всех карт в сбросе ---
    public void UpdateLayout()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
            {
                // Сдвигаем влево: умножаем индекс на отрицательный gap (-4f)
                float xOffset = i * stackGap;

                RectTransform rect = cards[i].GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(xOffset, 0f);
                }
                else
                {
                    cards[i].transform.localPosition = new Vector3(xOffset, 0f, 0f);
                }
            }
        }
    }
    public Vector3 GetCardWorldPosition(CardController c)
    {
        int idx = cards.IndexOf(c);
        if (idx == -1) return transform.position;
        float xOffset = idx * stackGap;
        return transform.TransformPoint(new Vector3(xOffset, 0f, 0f));
    }
}