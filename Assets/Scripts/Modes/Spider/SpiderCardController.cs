using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class SpiderCardController : CardController, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private bool _spiderDragAllowed = false;
    private SpiderModeManager _cachedModeManager;
    private DragManager _cachedDragManager;
    void Start()
    {
        
        _cachedModeManager = FindObjectOfType<SpiderModeManager>();
        _cachedDragManager = FindObjectOfType<DragManager>();
    }

    public new void OnBeginDrag(PointerEventData eventData)
    {
        // 1. ЗАЩИТА: Глобальная блокировка (Раздача, Победа, Undo)
        // Если SpiderModeManager говорит "Нельзя" — сразу отменяем.
        if (_cachedModeManager != null && !_cachedModeManager.IsInputAllowed)
        {
            _spiderDragAllowed = false;
            eventData.pointerDrag = null; // Сбрасываем драг в Unity EventSystem
            return;
        }

        // 2. ПРАВИЛА ИГРЫ: Проверка цепочки (масть/ранг)
        if (IsSequenceBlockedDownwards())
        {
            _spiderDragAllowed = false;
            return;
        }

        // Если всё ок — разрешаем
        _spiderDragAllowed = true;
        base.OnBeginDrag(eventData);
    }

    public new void OnDrag(PointerEventData eventData)
    {
        if (!_spiderDragAllowed) return;
        base.OnDrag(eventData);
    }

    public new void OnEndDrag(PointerEventData eventData)
    {
        if (!_spiderDragAllowed) return;
        base.OnEndDrag(eventData);
        _spiderDragAllowed = false;
    }

    // --- ЛОГИКА ПРОВЕРКИ И ТРЯСКИ ---
    private bool IsSequenceBlockedDownwards()
    {
        var myData = GetComponent<CardData>();
        if (myData == null || !myData.IsFaceUp()) return true;

        if (transform.parent == null) return false;
        if (transform.parent.name.Contains("DragLayer")) return false;

        int myIndex = transform.GetSiblingIndex();
        int childCount = transform.parent.childCount;

        // Если карта самая нижняя — её всегда можно взять
        if (myIndex == childCount - 1) return false;

        // Проверяем цепочку вниз, чтобы найти ТОЧКУ РАЗРЫВА
        for (int i = myIndex; i < childCount - 1; i++)
        {
            CardController currentCard = transform.parent.GetChild(i).GetComponent<CardController>();
            CardController nextCard = transform.parent.GetChild(i + 1).GetComponent<CardController>();

            if (currentCard == null || nextCard == null) return true;

            // Правила Паука:
            bool sameSuit = currentCard.cardModel.suit == nextCard.cardModel.suit;
            bool correctRankDecrease = currentCard.cardModel.rank == nextCard.cardModel.rank + 1;

            // ЕСЛИ НАШЛИ РАЗРЫВ ЦЕПОЧКИ:
            if (!sameSuit || !correctRankDecrease)
            {
                // currentCard (верхняя) и nextCard (нижняя) не подходят друг другу.
                // Значит, nextCard и всё что ниже — это "мешающий хвост".

                if (SpiderEffectsService.Instance)
                {
                    List<CardController> blockers = new List<CardController>();

                    // Собираем карты начиная с nextCard (индекс i + 1) и до конца стопки
                    // (Именно это вы просили: если 3->7, то начинаем трясти с 7)
                    for (int j = i + 1; j < childCount; j++)
                    {
                        var c = transform.parent.GetChild(j).GetComponent<CardController>();
                        if (c) blockers.Add(c);
                    }

                    SpiderEffectsService.Instance.Shake(blockers);
                }

                return true; // Блокируем движение
            }
        }

        return false; // Разрывов нет, можно тащить
    }
}