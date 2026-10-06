using UnityEngine;

// Вешать на "Cards" (или на любой RectTransform, который надо сжимать целиком).
// В отличие от прошлой версии, здесь НЕ измеряется "родной" размер content через
// ещё один RectTransform — если content сам stretch-анкорнут (как ваш "Cards",
// Min 0,0 / Max 1,1), его rect.size всегда совпадает с safeArea, и сравнивать
// там нечего. Вместо этого "родной" размер задаётся числом designSize — это и
// есть Reference Resolution вашего Canvas Scaler (Inspector -> Canvas Scaler ->
// Reference Resolution), т.е. размер, под который сетка карт реально расставлена.
[ExecuteAlways]
[DisallowMultipleComponent]
public class GridShrinkToFit : MonoBehaviour
{
    [Tooltip("RectTransform, который нужно сжимать целиком (например 'Cards').")]
    public RectTransform content;

    [Tooltip("RectTransform доступной области экрана (например 'BG', растянутый на весь экран).")]
    public RectTransform safeArea;

    [Tooltip("Родной размер content, под который расставлена сетка. Обычно = Reference Resolution из Canvas Scaler (Canvas -> Canvas Scaler -> Reference Resolution). ВАЖНО: это НЕ текущий content.rect.size — если content сам растянут (stretch), его rect.size всегда совпадает с safeArea, и сравнивать там нечего, поэтому раньше скрипт всегда выдавал scale = 1.")]
    public Vector2 designSize = new Vector2(1920f, 1080f);

    [Tooltip("Отступ от краёв safeArea в UI-юнитах.")]
    public float padding = 0f;

    [Tooltip("Не сжимать сильнее этого множителя (лучше вылезти за safeArea чуть-чуть, чем сделать карты нечитаемыми).")]
    [Range(0.1f, 1f)] public float minScale = 0.5f;

    private Vector2 lastSafeSize;

    private void OnEnable()
    {
        lastSafeSize = Vector2.zero;
    }

    // LateUpdate — чтобы сработать уже после того, как ваша логика ориентации
    // в этом же кадре расставила карты по местам.
    private void LateUpdate()
    {
        if (content == null || safeArea == null) return;

        Vector2 safeSize = safeArea.rect.size;
        if (safeSize == lastSafeSize) return;
        lastSafeSize = safeSize;

        float availW = Mathf.Max(0f, safeSize.x - padding * 2f);
        float availH = Mathf.Max(0f, safeSize.y - padding * 2f);

        float scaleX = designSize.x > 0f ? availW / designSize.x : 1f;
        float scaleY = designSize.y > 0f ? availH / designSize.y : 1f;

        // Берём min по обеим осям и применяем ОДИНАКОВО на X и Y (uniform),
        // чтобы карты не сплющивались/растягивались, а просто пропорционально
        // уменьшались, если не помещаются по высоте (лишнее место по бокам —
        // это нормально, лучше поля, чем наложение рядов).
        float scale = Mathf.Min(1f, scaleX, scaleY);
        scale = Mathf.Max(scale, minScale);

        content.localScale = new Vector3(scale, scale, 1f);
    }
}