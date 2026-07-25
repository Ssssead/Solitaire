using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PyramidHelpPanelController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject helpPanel;
    public Button toggleButton;
    public Button closeButton;
    public Button backgroundCloseButton;
    public TextMeshProUGUI pairsText;

    [Header("Animation Settings")]
    public float fadeDuration = 0.2f;

    private CanvasGroup panelCanvasGroup;
    private bool isPanelShowing = false;
    private Coroutine fadeCoroutine;
    
    // Ссылка на контроллер UI
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
 Д + Т
 В + 2
10 + 3
 9 + 4
 8 + 5
 7 + 6";

    private void Awake()
    {
        if (helpPanel != null) panelCanvasGroup = helpPanel.GetComponent<CanvasGroup>();
        
        if (toggleButton != null) toggleButton.onClick.AddListener(TogglePanel);
        if (closeButton != null) closeButton.onClick.AddListener(HidePanel);
        if (backgroundCloseButton != null) backgroundCloseButton.onClick.AddListener(HidePanel);

        if (helpPanel != null) helpPanel.SetActive(false);
    }

    private void Update()
    {
        // Проверяем каждую секунду, не открыл ли игрок поверх какое-то меню
        if (isPanelShowing)
        {
            if (IsAnySystemMenuOpen())
            {
                InstantHide(); // Мгновенно закрываем подсказку, чтобы не мешала
            }
        }
    }

    // Ищет контроллер и проверяет состояние ВСЕХ глобальных панелей
    private bool IsAnySystemMenuOpen()
    {
        // Динамический поиск гарантирует, что ссылка не потеряется при старте
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
        if (helpPanel == null || isPanelShowing) return;
        
        // Блокируем кнопку открытия, если поверх уже висит меню паузы/выхода
        if (IsAnySystemMenuOpen()) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        UpdateTextLocalization();
        isPanelShowing = true;
        helpPanel.SetActive(true);

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(1f));
    }

    // Плавное закрытие (когда игрок сам закрывает подсказку кнопкой)
    public void HidePanel()
    {
        if (helpPanel == null || !isPanelShowing) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("UI_Click");

        isPanelShowing = false;

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeRoutine(0f, () => helpPanel.SetActive(false)));
    }

    // Резкое закрытие (срабатывает, когда игрок нажимает на глобальные кнопки интерфейса)
    private void InstantHide()
    {
        if (helpPanel == null || !isPanelShowing) return;
        
        isPanelShowing = false;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        helpPanel.SetActive(false);
    }

    private void UpdateTextLocalization()
    {
        if (pairsText != null)
        {
            bool isRussian = PlayerPrefs.GetInt("UseRussianSymbols", 0) == 1;
            pairsText.text = isRussian ? russianText : englishText;
        }
    }

    private IEnumerator FadeRoutine(float targetAlpha, System.Action onComplete = null)
    {
        if (panelCanvasGroup == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float startAlpha = panelCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Работает даже при Time.timeScale = 0
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        panelCanvasGroup.alpha = targetAlpha;
        onComplete?.Invoke();
    }
}