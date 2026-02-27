using UnityEngine;

public class CardSettingsManager : MonoBehaviour
{
    private const string BackIndexKey = "SelectedBackIndex";

    /// <summary>
    /// Метод для кнопок выбора рубашки. 
    /// Передавайте 0 для первой, 1 для второй и т.д.
    /// </summary>
    public void SelectBack(int index)
    {
        PlayerPrefs.SetInt(BackIndexKey, index);
        PlayerPrefs.Save();
        Debug.Log($"Выбрана рубашка №{index}");
    }
}