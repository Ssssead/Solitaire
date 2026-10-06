using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq; // ќЅя«ј“≈Ћ№Ќќ дл€ .Cast<>()

public class YukonPileManager : PileManager
{
    [Header("Yukon Specifics")]
    public Transform yukonSlotsParent;

    // —писок наших специфичных стопок
    [SerializeField] private List<YukonTableauPile> yukonTableaus = new List<YukonTableauPile>();

    public void InitializeYukon(YukonModeManager modeManager)
    {
        // »щем глобально
        yukonTableaus.Clear();
        var allTabs = FindObjectsOfType<YukonTableauPile>();
        yukonTableaus.AddRange(allTabs);
        yukonTableaus.Sort((a, b) => (a as MonoBehaviour).name.CompareTo((b as MonoBehaviour).name));

        List<TableauPile> baseTableauList = yukonTableaus.Cast<TableauPile>().ToList();
        SetPrivateList("tableau", baseTableauList);

        var allFounds = FindObjectsOfType<FoundationPile>();
        var sortedFounds = new List<FoundationPile>(allFounds);
        sortedFounds.Sort((a, b) => a.name.CompareTo(b.name));

        SetPrivateList("foundations", sortedFounds);

        foreach (var f in sortedFounds) f.Initialize(null, null);

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
            Debug.LogError($"[YukonPileManager] Ќе найдено поле '{fieldName}' в PileManager!");
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
        // ƒобавл€ем наши стопки (приведение типов через Cast, чтобы избежать ошибок)
        if (yukonTableaus != null) list.AddRange(yukonTableaus.Cast<ICardContainer>());

        var f = FindObjectsOfType<FoundationPile>();
        if (f != null) list.AddRange(f);

        return list;
    }
}