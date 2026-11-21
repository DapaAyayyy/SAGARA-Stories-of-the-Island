using UnityEngine;

namespace Sagara.Dermaga.Fishing
{
    [CreateAssetMenu(menuName = "SAGARA/Fishing/Dimensional UI Theme")]
    public sealed class DimensionalFishingTheme : ScriptableObject
    {
        // Dimensional artwork.
        public Sprite frame;
        public Sprite header;
        public Sprite badgeRim;
        public Sprite button;
        public Sprite keycap;
        public Sprite activeKeycap;
        public Sprite crown;
        public Sprite flower;
        public Sprite wave;
        public Sprite wheel;

        // Palette.
        public Color ink = new Color(.30f, .16f, .08f);
        public Color cream = new Color(1, .94f, .79f);
        public Color teal = new Color(.02f, .52f, .49f);

        // Layer and button animation timing.
        public float headerDelay = .045f;
        public float detailDelay = .08f;
        public float settleSeconds = .22f;
        public float pressDepth = 6f;
        public float scoreSpeed = 48f;

        public Color Rarity(FishRarity r)
        {
            switch (r)
            {
                case FishRarity.Uncommon:
                    return new Color(.20f, .45f, .27f);
                case FishRarity.Rare:
                    return new Color(.16f, .42f, .65f);
                case FishRarity.Epic:
                    return new Color(.46f, .24f, .58f);
                case FishRarity.Legendary:
                    return new Color(.67f, .34f, .06f);
                default:
                    return new Color(.44f, .42f, .33f);
            }
        }
    }
}
