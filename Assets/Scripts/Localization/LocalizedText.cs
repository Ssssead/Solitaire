using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Graphic))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Ключ локализации; если пустой, текст из инспектора не будет заменяться")]
    public string key;

    public enum FontStyleType { Primary, Secondary }
    [Tooltip("Если вы используете для данного текста второстепенный (Secondary) шрифт")]
    public FontStyleType fontStyle = FontStyleType.Primary;

    private TextMeshProUGUI tmpText;
    private Text uiText;

    private void Awake()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
        if (tmpText == null)
        {
            uiText = GetComponent<Text>();
        }

        if (tmpText == null && uiText == null)
        {
            Debug.LogError($"[{nameof(LocalizedText)}] На объекте \"{gameObject.name}\" нет ни TMP, ни обычного Text!");
            return;
        }

        // Если LocalizationManager уже присутствует — просим его применить шрифт к этому TMP (инкрементально)
        if (tmpText != null && LocalizationManager.instance != null)
        {
            LocalizationManager.instance.ApplyFontToTMPIfNeeded(tmpText);
        }
    }

    private void Start()
    {
        UpdateText();
    }

    private void OnEnable()
    {
        // Если локализация уже загружена — обновляем сразу (поддерживает late Instantiate)
        if (!string.IsNullOrEmpty(key) && LocalizationManager.instance != null && LocalizationManager.instance.IsReady())
        {
            UpdateText();
        }
    }

    public void UpdateText()
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (LocalizationManager.instance == null || !LocalizationManager.instance.IsReady())
            return;

        string localized = LocalizationManager.instance.GetLocalizedValue(key);

        if (tmpText != null)
        {
            tmpText.text = localized;
        }
        else if (uiText != null)
        {
            uiText.text = localized;
        }
    }

    public void SetFont(TMP_FontAsset newFont)
    {
        if (tmpText == null || newFont == null)
            return;

        tmpText.font = newFont;
        if (newFont.material != null)
            tmpText.fontSharedMaterial = newFont.material;
        tmpText.SetAllDirty();
        tmpText.ForceMeshUpdate(true, true);
    }
}
