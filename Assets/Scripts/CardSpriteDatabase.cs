using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Cards/CardSpriteDatabase")]
public class CardSpriteDatabase : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public Suit suit;
        public int rank;
        public Sprite sprite;
    }

    [Header("Standard Set (English: J, Q, K, A)")]
    public List<Entry> entries = new List<Entry>();

    [Header("Localized Overrides (Russian: В, Д, К, Т)")]
    // Сюда добавьте только те карты, которые отличаются (например, Вальтов, Дам, Тузов)
    public List<Entry> localizedOverrides = new List<Entry>();

    [Header("Backs and Backgrounds")]
    public Sprite backSprite; // Текущая рубашка
    [Header("Backs Collection")]
    public List<Sprite> backSprites = new List<Sprite>();
    // Кэш для быстрого поиска
    private Dictionary<string, Sprite> cache;
    private bool useRussianSymbols = false;

    // Метод для переключения режима (вызывается из UI)
    public void SetSymbolMode(bool isRussian)
    {
        useRussianSymbols = isRussian;
        BuildCache(); // Пересобираем кэш с новыми настройками
    }

    public void BuildCache()
    {
        cache = new Dictionary<string, Sprite>();

        // 1. Сначала загружаем стандартные спрайты
        foreach (var e in entries)
        {
            string key = $"{(int)e.suit}-{e.rank}";
            if (!cache.ContainsKey(key))
            {
                cache.Add(key, e.sprite);
            }
        }

        // 2. Если включен русский режим, перезаписываем нужные карты
        if (useRussianSymbols)
        {
            foreach (var e in localizedOverrides)
            {
                string key = $"{(int)e.suit}-{e.rank}";
                // Перезаписываем или добавляем
                cache[key] = e.sprite;
            }
        }
    }

    public Sprite GetSprite(Suit suit, int rank)
    {
        if (cache == null) BuildCache();

        string key = $"{(int)suit}-{rank}";
        cache.TryGetValue(key, out var sp);
        return sp;
    }
    public Sprite GetCurrentBackSprite()
    {
        // Читаем индекс из PlayerPrefs (по умолчанию 0 — первая рубашка)
        int index = PlayerPrefs.GetInt("SelectedBackIndex", 0);

        // Проверка безопасности: если индекс в порядке, отдаем спрайт из списка
        if (backSprites != null && index >= 0 && index < backSprites.Count)
        {
            return backSprites[index];
        }

        // Если список пуст или индекс неверный, отдаем дефолтную рубашку
        return backSprite;
    }
}