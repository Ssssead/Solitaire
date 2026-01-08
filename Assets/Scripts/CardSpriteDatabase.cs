// CardSpriteDatabase.cs
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

    public Sprite backSprite;
    public List<Entry> entries = new List<Entry>();

    // בûסענûי ך‎ר
    private Dictionary<string, Sprite> cache;

    public void BuildCache()
    {
        cache = new Dictionary<string, Sprite>(entries.Count);
        foreach (var e in entries)
        {
            cache[$"{(int)e.suit}-{e.rank}"] = e.sprite;
        }
    }

    public Sprite GetSprite(Suit suit, int rank)
    {
        if (cache == null) BuildCache();
        cache.TryGetValue($"{(int)suit}-{rank}", out var sp);
        return sp;
    }
}
