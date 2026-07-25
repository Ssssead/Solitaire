using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class RewardCardUI : MonoBehaviour
{
    [Header("Settings")]
    public QuestCategory category;

    [Header("UI References")]
    public TMP_Text x2Text;
    public TMP_Text gamesCountText;

    public Button plusButton;
    public Button minusButton;

    // В базе мы храним количество ИГР, а не бустеров (защита от потери при смене премиума)
    public int CommittedGames { get; private set; }
    public int PendingBoosters { get; private set; }

    private RewardDistributionPanelUI mainPanel;

    public void Init(RewardDistributionPanelUI panel)
    {
        mainPanel = panel;
        PendingBoosters = 0;

        // Считываем уже сохраненные победы из базы
        var buff = QuestManager.Instance.saveData.activeXpBuffs?.FirstOrDefault(b => b.gameCategory == category);
        CommittedGames = buff != null ? buff.remainingWins : 0;
    }

    public void OnPlusClicked()
    {
        if (mainPanel.TryAddBooster())
        {
            PendingBoosters++;
            mainPanel.UpdateAllCardsUI();
        }
    }

    public void OnMinusClicked()
    {
        // Отменяем только "ожидающие" бустеры! Старые достижения защищены.
        if (PendingBoosters > 0)
        {
            PendingBoosters--;
            mainPanel.RemoveBooster();
        }
    }

    public void RefreshUI(bool canAddMore)
    {
        // Считаем общее количество бонусных игр
        int totalGames = CommittedGames + (PendingBoosters * mainPanel.GamesPerBooster);
        gamesCountText.text = totalGames.ToString();

        // А. Текст "Х2"
        if (totalGames > 0)
            x2Text.color = new Color32(255, 196, 0, 255); // FFC400
        else
            x2Text.color = new Color32(0, 0, 0, 102); // Черный 40% Alpha

        // Б. Кнопка "+"
        plusButton.interactable = canAddMore;
        if (canAddMore)
            plusButton.image.color = new Color32(255, 196, 0, 255); // FFC400 Золотой
        else
            plusButton.image.color = new Color32(180, 180, 180, 255); // Серый

        // В. Кнопка "-"
        bool canMinus = PendingBoosters > 0;
        minusButton.interactable = canMinus;
        if (canMinus)
            minusButton.image.color = new Color32(140, 155, 181, 255); // 8C9BB5 Сине-серый
        else
            minusButton.image.color = new Color32(180, 180, 180, 255); // Серый
    }
}