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

    public int CommittedGames { get; private set; }
    public int PendingBoosters { get; private set; }

    private RewardDistributionPanelUI mainPanel;

    public void Init(RewardDistributionPanelUI panel)
    {
        mainPanel = panel;
        PendingBoosters = 0;

        var buff = QuestManager.Instance.saveData.activeXpBuffs?.FirstOrDefault(b => b.gameCategory == category);
        CommittedGames = buff != null ? buff.remainingWins : 0;
    }

    public void OnPlusClicked()
    {
        if (mainPanel.TryAddBooster())
        {
            // ---> ÇÂÓÊ ÊËÈÊÀ <---
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

            PendingBoosters++;
            mainPanel.UpdateAllCardsUI();
        }
    }

    public void OnMinusClicked()
    {
        if (PendingBoosters > 0)
        {
            // ---> ÇÂÓÊ ÊËÈÊÀ <---
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

            PendingBoosters--;
            mainPanel.RemoveBooster();
        }
    }

    public void RefreshUI(bool canAddMore)
    {
        int totalGames = CommittedGames + (PendingBoosters * mainPanel.GamesPerBooster);
        gamesCountText.text = totalGames.ToString();

        if (totalGames > 0)
            x2Text.color = new Color32(255, 196, 0, 255);
        else
            x2Text.color = new Color32(0, 0, 0, 102);

        plusButton.interactable = canAddMore;
        if (canAddMore)
            plusButton.image.color = new Color32(255, 196, 0, 255);
        else
            plusButton.image.color = new Color32(180, 180, 180, 255);

        bool canMinus = PendingBoosters > 0;
        minusButton.interactable = canMinus;
        if (canMinus)
            minusButton.image.color = new Color32(140, 155, 181, 255);
        else
            minusButton.image.color = new Color32(180, 180, 180, 255);
    }
}