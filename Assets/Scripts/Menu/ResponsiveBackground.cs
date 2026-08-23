using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ResponsiveBackground : MonoBehaviour
{
    private RectTransform rectTransform;
    private RectTransform rootCanvasRect;
    private bool isPortrait;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // 1. Ищем САМЫЙ ГЛАВНЫЙ Canvas (rootCanvas), чтобы избежать проблем, 
        // если фон лежит внутри других панелей
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            rootCanvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
        }

        // 2. Жестко центрируем якоря
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // 3. Сбрасываем Scale на всякий случай (частая причина бага на скриншотах)
        rectTransform.localScale = Vector3.one;
    }

    private void Start()
    {
        isPortrait = Screen.width < Screen.height;
        UpdateBackground();
    }

    private void Update()
    {
        bool checkPortrait = Screen.width < Screen.height;

        // Обновляем фон, если сменилась ориентация ИЛИ если размеры Canvas
        // изменились (например, CanvasScaler обновил их с задержкой)
        if (checkPortrait != isPortrait || NeedsSizeUpdate())
        {
            isPortrait = checkPortrait;
            UpdateBackground();
        }
    }

    private bool NeedsSizeUpdate()
    {
        if (rootCanvasRect == null) return false;

        // Считаем, какими ДОЛЖНЫ быть размеры фона прямо сейчас
        float expectedWidth = isPortrait ? rootCanvasRect.rect.height : rootCanvasRect.rect.width;
        float expectedHeight = isPortrait ? rootCanvasRect.rect.width : rootCanvasRect.rect.height;

        // Если текущие размеры не совпадают с ожидаемыми — сигнализируем, что нужно обновить
        return !Mathf.Approximately(rectTransform.sizeDelta.x, expectedWidth) ||
               !Mathf.Approximately(rectTransform.sizeDelta.y, expectedHeight);
    }

    private void UpdateBackground()
    {
        if (rootCanvasRect == null) return;

        // Берем АКТУАЛЬНЫЕ размеры главного Canvas
        float canvasWidth = rootCanvasRect.rect.width;
        float canvasHeight = rootCanvasRect.rect.height;

        if (isPortrait)
        {
            // Портрет: картинка лежит на боку (-90 градусов).
            // Значит, её "ширина" должна быть равна высоте Canvas, а "высота" — ширине Canvas.
            rectTransform.sizeDelta = new Vector2(canvasHeight, canvasWidth);
            rectTransform.localRotation = Quaternion.Euler(0, 0, -90f);
        }
        else
        {
            // Ландшафт: всё стандартно.
            rectTransform.sizeDelta = new Vector2(canvasWidth, canvasHeight);
            rectTransform.localRotation = Quaternion.identity;
        }
    }
}