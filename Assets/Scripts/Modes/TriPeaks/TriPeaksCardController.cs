using UnityEngine;
using UnityEngine.EventSystems;

public class TriPeaksCardController : CardController
{
    private TriPeaksModeManager _modeManager;

    private void Start()
    {
        _modeManager = FindObjectOfType<TriPeaksModeManager>();
        EnforceAnimationSettings();
    }

    public void Configure(CardModel model)
    {
        this.cardModel = model;
        this.name = $"Card_{model.suit}_{model.rank}";

        var dataComponent = GetComponent<CardData>();
        if (dataComponent != null)
        {
            dataComponent.model = model;
            // Принудительно чиним длительность анимации, если она сломана
            EnforceAnimationSettings();
        }

        UpdateVisualState();
    }

    private void EnforceAnimationSettings()
    {
        var data = GetComponent<CardData>();
        if (data != null)
        {
            // Если время анимации слишком маленькое (или 0), ставим стандартные 0.25 сек
            if (data.flipDuration < 0.1f)
            {
                data.flipDuration = 0.25f;
            }
        }
    }

    public void UpdateVisualState()
    {
        // Здесь можно добавить визуальные эффекты для заблокированных карт
    }
}