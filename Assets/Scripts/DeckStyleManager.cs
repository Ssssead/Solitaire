using UnityEngine;

public class DeckStyleManager : MonoBehaviour
{
    private const string DeckStyleKey = "SelectedDeckStyle";

    // Вызовите этот метод при нажатии на кнопку "Base"
    public void SelectBaseDeck()
    {
        PlayerPrefs.SetInt(DeckStyleKey, 0); // 0 = Base
        PlayerPrefs.Save();
        Debug.Log("Выбран стиль колоды: Base");
    }

    // Вызовите этот метод при нажатии на кнопку "Premium"
    public void SelectPremiumDeck()
    {
        PlayerPrefs.SetInt(DeckStyleKey, 1); // 1 = Premium
        PlayerPrefs.Save();
        Debug.Log("Выбран стиль колоды: Premium");
    }
}