using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq; // ОБЯЗАТЕЛЬНО для .Cast<>()

public class YukonPileManager : PileManager
{
    [Header("Yukon Specifics")]
    public Transform yukonSlotsParent;

    // Список наших специфичных стопок
    [SerializeField] private List<YukonTableauPile> yukonTableaus = new List<YukonTableauPile>();

    public void InitializeYukon(YukonModeManager modeManager)
    {
        // 1. Собираем стопки со сцены
        yukonTableaus.Clear();
        if (yukonSlotsParent != null)
        {
            yukonTableaus.AddRange(yukonSlotsParent.GetComponentsInChildren<YukonTableauPile>());
        }

        // Сортируем
        yukonTableaus.Sort((a, b) => (a as MonoBehaviour).name.CompareTo((b as MonoBehaviour).name));

        // 2. --- ИСПРАВЛЕНИЕ ОШИБКИ LIST CONVERSION ---
        // Используем .Cast<TableauPile>().ToList() для корректного преобразования типов
        List<TableauPile> baseTableauList = yukonTableaus.Cast<TableauPile>().ToList();

        SetPrivateList("tableau", baseTableauList);

        // 3. Регистрируем Foundations
        var allFounds = FindObjectsOfType<FoundationPile>();
        var sortedFounds = new List<FoundationPile>(allFounds);
        sortedFounds.Sort((a, b) => a.name.CompareTo(b.name));

        SetPrivateList("foundations", sortedFounds);

        // Инициализация стопок
        foreach (var f in sortedFounds) f.Initialize(null, null);

        // Обнуляем stock и waste
        SetPrivateField("stockPile", null);
        SetPrivateField("wastePile", null);
    }

    private void SetPrivateList<T>(string fieldName, List<T> list)
    {
        var field = typeof(PileManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(this, list);
        }
        else
        {
            Debug.LogError($"[YukonPileManager] Не найдено поле '{fieldName}' в PileManager!");
        }
    }

    private void SetPrivateField(string fieldName, object value)
    {
        var field = typeof(PileManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null) field.SetValue(this, value);
    }

    public override List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();
        // Добавляем наши стопки (приведение типов через Cast, чтобы избежать ошибок)
        if (yukonTableaus != null) list.AddRange(yukonTableaus.Cast<ICardContainer>());

        var f = FindObjectsOfType<FoundationPile>();
        if (f != null) list.AddRange(f);

        return list;
    }
}