using UnityEngine;

namespace Sagara.Dermaga.Fishing
{
    [CreateAssetMenu(menuName = "SAGARA/Fishing/Living UI Theme")]
    public sealed class LivingFishingTheme : ScriptableObject
    {
        // Typography.
        public Font displayFont;
        public Font bodyFont;

        // Panel and control artwork.
        public Sprite paper;
        public Sprite header;
        public Sprite badge;
        public Sprite button;
        public Sprite keycap;
        public Sprite cornerLeft;
        public Sprite cornerRight;
        public Sprite hook;
        public Sprite arrow;

        // Fish portrait lookup.
        public Sprite[] portraits;
        public string[] portraitNames;

        // Palette.
        public Color ink = new Color(.20f, .27f, .23f);
        public Color teal = new Color(.12f, .43f, .41f);
        public Color ivory = new Color(1, .96f, .84f);
        public Color coral = new Color(.72f, .32f, .25f);

        // Transition and feedback timing.
        public float promptSeconds = .2f;
        public float panelSeconds = .26f;
        public float pressSeconds = .12f;
        public float feedbackSeconds = .18f;
        public float comboSeconds = .18f;
        public float resultSeconds = .42f;

        public Color Rarity(FishRarity r)
        {
            switch (r)
            {
                case FishRarity.Uncommon:
                    return new Color(.27f, .48f, .26f);
                case FishRarity.Rare:
                    return new Color(.23f, .44f, .65f);
                case FishRarity.Epic:
                    return new Color(.50f, .32f, .62f);
                case FishRarity.Legendary:
                    return new Color(.69f, .45f, .11f);
                default:
                    return new Color(.42f, .45f, .39f);
            }
        }

        public Sprite Portrait(string name)
        {
            for (int i = 0; i < portraitNames.Length; i++)
            {
                if (portraitNames[i] == name && i < portraits.Length)
                {
                    return portraits[i];
                }
            }

            return null;
        }
    }
}
