using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PyramidHelpPanelController : MonoBehaviour
{
    [Header("UI References (Landscape / Primary)")]
    public GameObject helpPanel;
    public Button toggleButton;
    public Button closeButton;
    public Button backgroundCloseButton;
    public TextMeshProUGUI pairsText;

    [Header("UI References (Portrait)")]
    public GameObject portraitHelpPanel;
    public Button portraitToggleButton;
    public Button portraitCloseButton;
    public Button portraitBackgroundCloseButton;
    public TextMeshProUGUI portraitPairsText;

    [Header("Animation Settings")]
    public float fadeDuration = 0.2f;

    private CanvasGroup landscapeCG;
    private CanvasGroup portraitCG;
    private bool isPanelShowing = false;
    private Coroutine fadeCoroutine;

    private GameUIController gameUI;

    private readonly string englishText =
@"   K
 Q + A
 J + 2
10 + 3
 9 + 4
 8 + 5
 7 + 6";

    private readonly string russianText =
@"   K
 Ä + Ò
 Â + 2
10 + 3
 9 + 4
 8 + 5
 7 + 6";

    private void Awake()
    {
        if (helpPanel != null)
            landscapeCG = helpPanel.GetComponent<CanvasGroup>() ?? helpPanel.AddComponent<CanvasGroup>();

        if (portraitHelpPanel != null)
            portraitCG = portraitHelpPanel.GetComponent<CanvasGroup>() ?? portraitHelpPanel.AddComponent<CanvasGroup>();

        if (toggleButton != null) toggleButton.onClick.AddListener(TogglePanel);
        if (closeButton != null) closeButton.onClick.AddListener(HidePanel);
        if (backgroundCloseButton != null) backgroundCloseButton.onClick.AddListener(HidePanel);

        if (portraitToggleButton != null) portraitToggleButton.onClick.AddListener(TogglePanel);
        if (portraitCloseButton != null) portraitCloseButton.onClick.AddListener(HidePanel);
        if (portraitBackgroundCloseButton != null) portraitBackgroundCloseButton.onClick.AddListener(HidePanel);

        if (helpPanel != null) helpPanel.SetActive(false);
        if (portraitHelpPanel != null) portraitHelpPanel.SetActive(false);
    }

    private void Update()
    {
        if (isPanelShowing)
        {
            if (IsAnySystemMenuOpen())
            {
                InstantHide();
                return;
            }

            // --- ÑÈÍÕÐÎÍÈÇÀÖÈß ÏÐÈ ÏÎÂÎÐÎÒÅ ÝÊÐÀÍÀ ---
            bool isPortrait = Screen.height > Screen.width;
            if (isPortrait)
            {
                if (helpPanel != null && helpPanel.activeSelf) helpPanel.SetActive(false);
                if (portraitHelpPanel != null && !portraitHelpPanel.activeSelf) portraitHelpPanel.SetActive(true);
            }
            else
            {
                if (portraitHelpPanel != null && portraitHelpPanel.activeSelf) portraitHelpPanel.SetActive(false);
                if (helpPanel != null && !helpPanel.activeSelf) helpPanel.SetActive(true);
            }
        }
    }

    private bool IsAnySystemMenuOpen()
    {
        if (gameUI == null) gameUI = FindObjectOfType<GameUIController>();
        if (gameUI == null) return false;

        return (gameUI.settingsPanel != null && gameUI.settingsPanel.activeInHierarchy) ||
               (gameUI.exitConfirmationPanel != null && gameUI.exitConfirmationPanel.activeInHierarchy) ||
               (gameUI.newGameConfirmationPanel != null && gameUI.newGameConfirmationPanel.activeInHierarchy) ||
               (gameUI.winPanel != null && gameUI.winPanel.activeInHierarchy) ||
               (gameUI.defeatPanel != null && gameUI.defeatPanel.activeInHierarchy);
    }

    public void TogglePanel()
    {
        if (isPanelShowing) HidePanel();
        else ShowPanel();
    }

    public void ShowPanel()
    {
        if (isPanelShowing) return;
        if (IsAnySystemMenuOpen()) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        UpdateTextLocalization();
        isPanelShowing = true;

        // --- ÎÒÊÐÛÂÀÅÌ ÒÎËÜÊÎ ÍÓÆÍÓÞ ÏÀÍÅËÜ ---
        bool isPortrait = Screen.height > Screen.width;
        if (isPortrait)
        {
            if (portraitHelpPanel != null) portraitHelpPanel.SetActive(true);
            if (helpPanel != null) helpPanel.SetActive(false);
        }
        else
        {
            if (helpPanel != null) helpPanel.SetActive(true);
            if (portraitHelpPanel != null) portraitHelpPanel.SetActive(false);
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(1f));
    }

    public void HidePanel()
    {
        if (!isPanelShowing) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        isPanelShowing = false;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(0f, () =>
        {
            if (helpPanel != null) helpPanel.SetActive(false);
            if (portraitHelpPanel != null) portraitHelpPanel.SetActive(false);
        }));
    }

    private void InstantHide()
    {
        if (!isPanelShowing) return;

        isPanelShowing = false;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        if (landscapeCG != null) landscapeCG.alpha = 0f;
        if (portraitCG != null) portraitCG.alpha = 0f;

        if (helpPanel != null) helpPanel.SetActive(false);
        if (portraitHelpPanel != null) portraitHelpPanel.SetActive(false);
    }

    private void UpdateTextLocalization()
    {
        bool isRussian = PlayerPrefs.GetInt("UseRussianSymbols", 0) == 1;
        string textToSet = isRussian ? russianText : englishText;

        if (pairsText != null) pairsText.text = textToSet;
        if (portraitPairsText != null) portraitPairsText.text = textToSet;
    }

    private IEnumerator FadeRoutine(float targetAlpha, System.Action onComplete = null)
    {
        float startAlpha = 0f;
        bool isPortrait = Screen.height > Screen.width;

        if (isPortrait && portraitCG != null) startAlpha = portraitCG.alpha;
        else if (!isPortrait && landscapeCG != null) startAlpha = landscapeCG.alpha;
        else if (landscapeCG != null) startAlpha = landscapeCG.alpha;
        else if (portraitCG != null) startAlpha = portraitCG.alpha;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);

            // Ñèíõðîííî îáíîâëÿåì ïðîçðà÷íîñòü äëÿ îáåèõ, ÷òîáû íå áûëî ñêà÷êîâ ïðè ïîâîðîòå
            if (landscapeCG != null) landscapeCG.alpha = currentAlpha;
            if (portraitCG != null) portraitCG.alpha = currentAlpha;

            yield return null;
        }

        if (landscapeCG != null) landscapeCG.alpha = targetAlpha;
        if (portraitCG != null) portraitCG.alpha = targetAlpha;

        onComplete?.Invoke();
    }
}