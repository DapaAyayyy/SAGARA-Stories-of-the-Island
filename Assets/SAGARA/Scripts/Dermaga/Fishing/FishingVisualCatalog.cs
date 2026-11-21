using System;
using UnityEngine;

namespace Sagara.Dermaga.Fishing
{
    [Serializable]
    public sealed class FishVisualEntry
    {
        // Lookup identity.
        public FishRarity rarity;
        public string temporaryFishId;

        // Visual representation.
        public GameObject prefab;
        public string displayName;
        public Texture2D portrait;
    }

    [CreateAssetMenu(menuName = "SAGARA/Dermaga/Fish visual catalog")]
    public sealed class FishingVisualCatalog : ScriptableObject
    {
        // Available visual entries.
        public FishVisualEntry[] entries;

        public FishVisualEntry Find(RaritySettings selected)
        {
            if (selected == null || entries == null)
            {
                return null;
            }

            foreach (var e in entries)
            {
                if (e != null
                    && e.rarity == selected.rarity
                    && e.temporaryFishId == selected.fishId)
                {
                    return e;
                }
            }

            return null;
        }
    }
}
