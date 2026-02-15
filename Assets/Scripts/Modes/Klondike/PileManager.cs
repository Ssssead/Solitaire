// PileManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Управляет всеми стопками карт (tableau, foundations, stock, waste).
/// Создаёт компоненты контейнеров и предоставляет к ним доступ.
/// </summary>
public class PileManager : MonoBehaviour
{
    private KlondikeModeManager mode;

    [Header("Slot Parents")]
    public Transform tableauSlotsParent;
    public Transform foundationSlotsParent;
    public Transform stockSlotTransform;
    public Transform wasteSlotTransform;

    [Header("Settings")]
    private float tableauVerticalGap = 40f;

    [Header("Created Piles (Runtime)")]
    [SerializeField] private List<TableauPile> tableau = new List<TableauPile>();
    [SerializeField] private List<FoundationPile> foundations = new List<FoundationPile>();
    [SerializeField] private StockPile stockPile;
    [SerializeField] private WastePile wastePile;

    // Публичные свойства (read-only для внешних классов)
    public IReadOnlyList<TableauPile> Tableau => tableau;
    public IReadOnlyList<FoundationPile> Foundations => foundations;
    public StockPile StockPile => stockPile;
    public WastePile WastePile => wastePile;

    /// <summary>
    /// Инициализация PileManager.
    /// </summary>
    public void Initialize(KlondikeModeManager km,
                           Transform tableauParent = null,
                           Transform foundationParent = null,
                           Transform stockSlot = null,
                           Transform wasteSlot = null,
                           float tableauGap = 40f)
    {
        mode = km;

        if (tableauParent != null) tableauSlotsParent = tableauParent;
        if (foundationParent != null) foundationSlotsParent = foundationParent;
        if (stockSlot != null) stockSlotTransform = stockSlot;
        if (wasteSlot != null) wasteSlotTransform = wasteSlot;

        tableauVerticalGap = tableauGap;

#if UNITY_EDITOR
        Debug.Log($"[PileManager] Initialized with gap={tableauVerticalGap}");
#endif
    }

    /// <summary>
    /// Создаёт все стопки (компоненты контейнеров) на заданных слотах.
    /// </summary>
    public void CreatePiles()
    {
        // Очищаем предыдущие ссылки
        tableau.Clear();
        foundations.Clear();
        stockPile = null;
        wastePile = null;

        // Проверяем наличие слотов
        ValidateSlots();

        // Создаём Tableau piles (обычно 7 штук)
        CreateTableauPiles();

        // Создаём Foundation piles (4 штуки - по одной на масть)
        CreateFoundationPiles();

        // Создаём Stock pile
        CreateStockPile();

        // Создаём Waste pile
        CreateWastePile();

#if UNITY_EDITOR
        Debug.Log($"[PileManager] Created {tableau.Count} tableau, {foundations.Count} foundations, stock and waste piles");
#endif
    }

    #region Pile Creation

    private void CreateTableauPiles()
    {
        if (tableauSlotsParent == null)
        {
            Debug.LogError("[PileManager] Cannot create tableau: tableauSlotsParent is null!");
            return;
        }

        int childCount = tableauSlotsParent.childCount;

        if (childCount == 0)
        {
            Debug.LogWarning("[PileManager] tableauSlotsParent has no children!");
            return;
        }

        for (int i = 0; i < childCount; i++)
        {
            Transform slot = tableauSlotsParent.GetChild(i);

            // Получаем или добавляем компонент TableauPile
            TableauPile pile = slot.GetComponent<TableauPile>();
            if (pile == null)
            {
                pile = slot.gameObject.AddComponent<TableauPile>();
            }

            // Инициализируем
            RectTransform slotRect = slot as RectTransform;
            pile.Initialize(mode, slotRect, tableauVerticalGap);

            tableau.Add(pile);
        }
    }

    private void CreateFoundationPiles()
    {
        if (foundationSlotsParent == null)
        {
            Debug.LogError("[PileManager] Cannot create foundations: foundationSlotsParent is null!");
            return;
        }

        int childCount = foundationSlotsParent.childCount;

        if (childCount == 0)
        {
            Debug.LogWarning("[PileManager] foundationSlotsParent has no children!");
            return;
        }

        for (int i = 0; i < childCount; i++)
        {
            Transform slot = foundationSlotsParent.GetChild(i);

            // Получаем или добавляем компонент FoundationPile
            FoundationPile foundation = slot.GetComponent<FoundationPile>();
            if (foundation == null)
            {
                foundation = slot.gameObject.AddComponent<FoundationPile>();
            }

            // Инициализируем
            RectTransform slotRect = slot as RectTransform;
            foundation.Initialize(mode, slotRect);

            foundations.Add(foundation);
        }
    }

    private void CreateStockPile()
    {
        if (stockSlotTransform == null)
        {
            Debug.LogError("[PileManager] Cannot create stock: stockSlotTransform is null!");
            return;
        }

        // ВАЖНО: Проверяем что stockSlotTransform - это отдельный GameObject, а не родитель других слотов
        if (stockSlotTransform == tableauSlotsParent || stockSlotTransform == foundationSlotsParent)
        {
            Debug.LogError($"[PileManager] ERROR! stockSlotTransform ({stockSlotTransform.name}) is the same as tableau/foundation parent! This will cause cards to appear in wrong places!");
            return;
        }

        // Получаем или добавляем компонент StockPile
        stockPile = stockSlotTransform.GetComponent<StockPile>();
        bool wasCreated = false;

        if (stockPile == null)
        {
            Debug.Log($"[PileManager] Adding StockPile component to {stockSlotTransform.name}");
            stockPile = stockSlotTransform.gameObject.AddComponent<StockPile>();
            wasCreated = true;
        }
        else
        {
            Debug.Log($"[PileManager] Found existing StockPile component on {stockSlotTransform.name}");
        }

        // ВСЕГДА инициализируем (даже если компонент уже существовал)
        RectTransform slotRect = stockSlotTransform as RectTransform;
        stockPile.Initialize(mode, slotRect);

        //Debug.Log($"[PileManager] StockPile ready: GameObject={stockSlotTransform.name}, Component={(wasCreated ? "created" : "existing")}, Path={GetGameObjectPath(stockSlotTransform.gameObject)}");
    }

    private void CreateWastePile()
    {
        if (wasteSlotTransform == null)
        {
            Debug.LogError("[PileManager] Cannot create waste: wasteSlotTransform is null!");
            return;
        }

        // ВАЖНО: Проверяем что wasteSlotTransform - это отдельный GameObject
        if (wasteSlotTransform == tableauSlotsParent || wasteSlotTransform == foundationSlotsParent)
        {
            Debug.LogError($"[PileManager] ERROR! wasteSlotTransform ({wasteSlotTransform.name}) is the same as tableau/foundation parent! This will cause cards to appear in wrong places!");
            return;
        }

        // Получаем или добавляем компонент WastePile
        wastePile = wasteSlotTransform.GetComponent<WastePile>();
        bool wasCreated = false;

        if (wastePile == null)
        {
            Debug.Log($"[PileManager] Adding WastePile component to {wasteSlotTransform.name}");
            wastePile = wasteSlotTransform.gameObject.AddComponent<WastePile>();
            wasCreated = true;
        }
        else
        {
            Debug.Log($"[PileManager] Found existing WastePile component on {wasteSlotTransform.name}");
        }

        // ВСЕГДА инициализируем
        RectTransform slotRect = wasteSlotTransform as RectTransform;
        wastePile.Initialize(mode, slotRect);

        //Debug.Log($"[PileManager] WastePile ready: GameObject={wasteSlotTransform.name}, Component={(wasCreated ? "created" : "existing")}, Path={GetGameObjectPath(wasteSlotTransform.gameObject)}");
    }

    #endregion

    #region Validation

    private void ValidateSlots()
    {
        if (tableauSlotsParent == null)
        {
            Debug.LogWarning("[PileManager] tableauSlotsParent is null!");
        }

        if (foundationSlotsParent == null)
        {
            Debug.LogWarning("[PileManager] foundationSlotsParent is null!");
        }

        if (stockSlotTransform == null)
        {
            Debug.LogWarning("[PileManager] stockSlotTransform is null!");
        }

        if (wasteSlotTransform == null)
        {
            Debug.LogWarning("[PileManager] wasteSlotTransform is null!");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Возвращает все контейнеры как ICardContainer.
    /// </summary>
    public virtual List<ICardContainer> GetAllContainers()
    {
        List<ICardContainer> list = new List<ICardContainer>();

        // Добавляем Tableau (если список не пуст)
        if (tableau != null) list.AddRange(tableau);

        // Добавляем Foundations (если список не пуст)
        if (foundations != null) list.AddRange(foundations);

        // Добавляем Stock и Waste (если они существуют)
        if (stockPile != null) list.Add(stockPile);
        if (wastePile != null) list.Add(wastePile);

        return list;
    }

    /// <summary>
    /// Возвращает Transform всех контейнеров (для AnimationService).
    /// </summary>
    public List<Transform> GetAllContainerTransforms()
    {
        var transforms = new List<Transform>();

        foreach (var t in tableau)
        {
            if (t != null) transforms.Add(t.transform);
        }

        foreach (var f in foundations)
        {
            if (f != null) transforms.Add(f.transform);
        }

        if (stockPile != null) transforms.Add(stockPile.transform);
        if (wastePile != null) transforms.Add(wastePile.transform);

        return transforms;
    }

    /// <summary>
    /// Получает конкретный Tableau pile по индексу.
    /// </summary>
    public TableauPile GetTableau(int index)
    {
        if (index < 0 || index >= tableau.Count) return null;
        return tableau[index];
    }

    /// <summary>
    /// Получает конкретный Foundation pile по индексу.
    /// </summary>
    public FoundationPile GetFoundation(int index)
    {
        if (index < 0 || index >= foundations.Count) return null;
        return foundations[index];
    }

    /// <summary>
    /// Очищает все стопки (удаляет визуальные карты).
    /// </summary>
    public void ClearAllPiles()
    {
        foreach (var t in tableau)
        {
            t?.Clear();
        }

        foreach (var f in foundations)
        {
            f?.Clear();
        }

        stockPile?.Clear();
        wastePile?.Clear();

#if UNITY_EDITOR
        Debug.Log("[PileManager] All piles cleared.");
#endif
    }

    /// <summary>
    /// Возвращает Transform слота Stock (для DeckManager).
    /// </summary>
    public Transform GetStockSlotTransform() => stockSlotTransform;

    /// <summary>
    /// Возвращает Transform слота Waste.
    /// </summary>
    public Transform GetWasteSlotTransform() => wasteSlotTransform;

    /// <summary>
    /// Подсчитывает общее количество карт во всех стопках.
    /// </summary>
    public int CountTotalCards()
    {
        int total = 0;

        foreach (var t in tableau)
        {
            if (t != null) total += t.GetAllCards()?.Count ?? 0;
        }

        foreach (var f in foundations)
        {
            if (f != null) total += f.Count;
        }

        if (wastePile != null) total += wastePile.Count;

        // Stock сложнее подсчитать напрямую, так как у него нет публичного Count
        // Можно добавить метод GetCardCount() в StockPile при необходимости

        return total;
    }

    #endregion
    #region Visual Control (Intro)

    /// <summary>
    /// Устанавливает прозрачность всем слотам (для интро).
    /// </summary>
    public void SetAllSlotsAlpha(float alpha)
    {
        SetGroupAlpha(tableauSlotsParent, alpha);
        SetGroupAlpha(foundationSlotsParent, alpha);
        SetGroupAlpha(stockSlotTransform, alpha);
        SetGroupAlpha(wasteSlotTransform, alpha);
    }

    private void SetGroupAlpha(Transform parent, float alpha)
    {
        if (parent == null) return;

        // Пытаемся найти CanvasGroup, если нет - добавляем
        CanvasGroup cg = parent.GetComponent<CanvasGroup>();
        if (cg == null) cg = parent.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = alpha;
    }

    /// <summary>
    /// Корутина для плавного появления слотов.
    /// </summary>
    public System.Collections.IEnumerator FadeInSlots(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            SetAllSlotsAlpha(alpha);
            yield return null;
        }
        SetAllSlotsAlpha(1f);
    }

    #endregion
#if UNITY_EDITOR
    [ContextMenu("Debug: Count All Cards")]
    private void DebugCountCards()
    {
        int total = CountTotalCards();
        Debug.Log($"[PileManager] Total cards in piles: {total}");
    }

    [ContextMenu("Debug: Clear All Piles")]
    private void DebugClearAll()
    {
        ClearAllPiles();
    }

    [ContextMenu("Debug: Show Pile Hierarchy")]
    private void DebugShowHierarchy()
    {
        Debug.Log("=== PILE HIERARCHY ===");
        Debug.Log($"tableauSlotsParent: {GetGameObjectPath(tableauSlotsParent?.gameObject)}");
        Debug.Log($"foundationSlotsParent: {GetGameObjectPath(foundationSlotsParent?.gameObject)}");
        Debug.Log($"stockSlotTransform: {GetGameObjectPath(stockSlotTransform?.gameObject)}");
        Debug.Log($"wasteSlotTransform: {GetGameObjectPath(wasteSlotTransform?.gameObject)}");

        if (stockPile != null)
        {
            Debug.Log($"StockPile component on: {GetGameObjectPath(stockPile.gameObject)}");
        }

        if (wastePile != null)
        {
            Debug.Log($"WastePile component on: {GetGameObjectPath(wastePile.gameObject)}");
        }
    }

    /// <summary>
    /// Возвращает полный путь GameObject в иерархии (для отладки).
    /// </summary>
    private string GetGameObjectPath(GameObject obj)
    {
        if (obj == null) return "null";

        string path = obj.name;
        Transform current = obj.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
#endif
}