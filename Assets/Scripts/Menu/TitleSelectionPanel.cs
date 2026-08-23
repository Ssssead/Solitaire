using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

[System.Serializable]
public class CategoryTab
{
    public string categoryName;
    public Button tabButton;
}

public class TitleSelectionPanel : MonoBehaviour
{
    [Header("Настройки контента")]
    public Transform contentContainer;
    public GameObject titleItemPrefab;
    public Button closeButton;

    [Header("Вкладки категорий")]
    public List<CategoryTab> categoryTabs = new List<CategoryTab>();

    [Header("Настройки анимации вылета")]
    [Tooltip("Время анимации в секундах")]
    public float animationDuration = 0.25f;
    [Tooltip("Координата X, куда улетает панель (должна быть левее экрана, например -2000)")]
    public float hiddenXOffset = -2000f;

    // Цвета
    private Color defaultBtnColor;
    private Color defaultTxtColor;
    private Color selectedBtnColor;
    private Color selectedTxtColor;

    private List<GameObject> spawnedItems = new List<GameObject>();
    private string currentCategory = "Global";

    private RectTransform rectTransform;
    private Vector2 targetPosition; // Исходная позиция панели на экране
    private Coroutine activeAnimation;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        // Запоминаем штатную позицию панели из инспектора
        targetPosition = rectTransform.anchoredPosition;

        // Инициализируем цвета
        ColorUtility.TryParseHtmlString("#9A5F40", out defaultBtnColor);
        ColorUtility.TryParseHtmlString("#C0C0C0", out defaultTxtColor);
        ColorUtility.TryParseHtmlString("#FFB01A", out selectedBtnColor);
        ColorUtility.TryParseHtmlString("#24140C", out selectedTxtColor);
    }

    private void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            // Перехватываем стандартное закрытие на метод с анимацией
            closeButton.onClick.AddListener(ClosePanel);
        }

        foreach (var tab in categoryTabs)
        {
            string catName = tab.categoryName;
            tab.tabButton.onClick.AddListener(() => SwitchCategory(catName));
        }
    }

    /// <summary>
    /// Публичный метод открытия панели с анимацией вылета слева
    /// </summary>
    public void OpenPanel()
    {
        gameObject.SetActive(true);

        if (activeAnimation != null) StopCoroutine(activeAnimation);
        Vector2 startPos = new Vector2(hiddenXOffset, targetPosition.y);
        activeAnimation = StartCoroutine(AnimatePanel(startPos, targetPosition, false));
    }

    /// <summary>
    /// Публичный метод закрытия панели с анимацией улета влево
    /// </summary>
    public void ClosePanel()
    {
        if (activeAnimation != null) StopCoroutine(activeAnimation);
        Vector2 endPos = new Vector2(hiddenXOffset, targetPosition.y);
        activeAnimation = StartCoroutine(AnimatePanel(rectTransform.anchoredPosition, endPos, true));
    }

    private void OnEnable()
    {
        LocalizationManager.OnLocalizationLoaded += RebuildList;
        UpdateTabsVisuals();
        RebuildList();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLocalizationLoaded -= RebuildList;
    }

    public void SwitchCategory(string newCategory)
    {
        if (newCategory == currentCategory && spawnedItems.Count > 0) return;

        currentCategory = newCategory;
        UpdateTabsVisuals();
        RebuildList();
    }

    private void UpdateTabsVisuals()
    {
        foreach (var tab in categoryTabs)
        {
            bool isCurrent = (tab.categoryName == currentCategory);
            tab.tabButton.interactable = true;

            TMP_Text tabText = tab.tabButton.GetComponentInChildren<TMP_Text>();

            if (isCurrent)
                ApplyColors(tab.tabButton, tabText, selectedBtnColor, selectedTxtColor);
            else
                ApplyColors(tab.tabButton, tabText, defaultBtnColor, defaultTxtColor);
        }
    }

    private void RebuildList()
    {
        if (LocalizationManager.instance == null || !LocalizationManager.instance.IsReady())
            return; //

        foreach (var item in spawnedItems) Destroy(item); //
        spawnedItems.Clear(); //[cite: 1]

        foreach (TitleDef titleDef in TitleManager.Instance.allTitles) //[cite: 1]
        {
            if (titleDef.category != currentCategory) continue; //[cite: 1]

            GameObject obj = Instantiate(titleItemPrefab, contentContainer); //[cite: 1]
            spawnedItems.Add(obj); //[cite: 1]

            Button selectBtn = obj.GetComponent<Button>(); //[cite: 1]
            string keyToSet = titleDef.titleKey; //[cite: 1]

            selectBtn.onClick.AddListener(() => SelectTitle(keyToSet)); //[cite: 1]
        }

        UpdateListVisuals(); //[cite: 1]

        // --- НОВАЯ СТРОКА ---
        AdjustGridCellSize();
    }
    /// <summary>
    /// Динамически пересчитывает ширину ячеек Grid Layout Group под текущий размер экрана.
    /// </summary>
    private void AdjustGridCellSize()
    {
        if (contentContainer == null) return; // Защита от ошибок до инициализации

        GridLayoutGroup grid = contentContainer.GetComponent<GridLayoutGroup>();
        RectTransform containerRect = contentContainer.GetComponent<RectTransform>();

        if (grid != null && containerRect != null)
        {
            float totalWidth = containerRect.rect.width;

            // Если UI еще не прогрузился и ширина равна 0, пропускаем
            if (totalWidth <= 0) return;

            int columns = grid.constraintCount;
            if (columns <= 0) columns = 3;

            float paddingX = grid.padding.left + grid.padding.right;
            float spacingX = grid.spacing.x * (columns - 1);

            float cellWidth = (totalWidth - paddingX - spacingX) / columns;

            // ВАЖНО: меняем размер ячейки только если он действительно изменился (защита от зацикливания)
            if (Mathf.Abs(grid.cellSize.x - cellWidth) > 0.1f)
            {
                grid.cellSize = new Vector2(cellWidth, grid.cellSize.y);
            }
        }
    }
    protected void OnRectTransformDimensionsChange()
    {
        // Пересчитываем сетку только если контейнер уже назначен и панель активна
        if (contentContainer != null && gameObject.activeInHierarchy)
        {
            AdjustGridCellSize();
        }
    }
    private void UpdateListVisuals()
    {
        if (LocalizationManager.instance == null || !LocalizationManager.instance.IsReady()) return;

        string currentSelectedTitleKey = TitleManager.Instance.GetCurrentTitleKey();
        int itemIndex = 0;

        foreach (TitleDef titleDef in TitleManager.Instance.allTitles)
        {
            if (titleDef.category != currentCategory) continue;
            if (itemIndex >= spawnedItems.Count) break;

            GameObject obj = spawnedItems[itemIndex];
            TMP_Text buttonText = obj.GetComponentInChildren<TMP_Text>();
            Button selectBtn = obj.GetComponent<Button>();

            bool isUnlocked = TitleManager.Instance.IsTitleUnlocked(titleDef);
            string titleNameLocal = LocalizationManager.instance.GetLocalizedValue(titleDef.titleKey);

            if (isUnlocked)
            {
                if (titleDef.titleKey == currentSelectedTitleKey)
                {
                    string selectedTextLocal = LocalizationManager.instance.GetLocalizedValue("TitleUI_Selected");
                    buttonText.text = $"{titleNameLocal}\n<size=75%>({selectedTextLocal})</size>";
                    selectBtn.interactable = true;
                    ApplyColors(selectBtn, buttonText, selectedBtnColor, selectedTxtColor);
                }
                else
                {
                    buttonText.text = titleNameLocal;
                    selectBtn.interactable = true;
                    ApplyColors(selectBtn, buttonText, defaultBtnColor, defaultTxtColor);
                }
            }
            else
            {
                selectBtn.interactable = false;

                string reqFormat = LocalizationManager.instance.GetLocalizedValue("TitleUI_Requirement");
                buttonText.text = string.Format(reqFormat, titleDef.requiredLevel);

                ApplyColors(selectBtn, buttonText, defaultBtnColor, defaultTxtColor);
            }

            itemIndex++;
        }
    }

    private void SelectTitle(string titleKey)
    {
        if (TitleManager.Instance.GetCurrentTitleKey() == titleKey) return;

        TitleManager.Instance.SetCurrentTitle(titleKey);
        UpdateListVisuals();
    }

    private void ApplyColors(Button btn, TMP_Text txt, Color btnColor, Color txtColor)
    {
        if (btn != null && btn.image != null)
            btn.image.color = btnColor;

        if (txt != null)
            txt.color = txtColor;
    }

    // Рабочая корутина для перемещения UI-окна без использования сторонних плагинов
    private IEnumerator AnimatePanel(Vector2 startPos, Vector2 endPos, bool deactivateAtEnd)
    {
        float elapsedTime = 0f;
        rectTransform.anchoredPosition = startPos;

        while (elapsedTime < animationDuration)
        {
            // Используем unscaledDeltaTime на случай, если в игре включена пауза (Time.timeScale = 0)
            elapsedTime += Time.unscaledDeltaTime;

            // Сглаживание интерполяции (плавный разгон и торможение панели)
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / animationDuration);
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rectTransform.anchoredPosition = endPos;

        if (deactivateAtEnd)
        {
            gameObject.SetActive(false);
        }

        activeAnimation = null;
    }
}