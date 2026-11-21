using UnityEngine;

namespace Sagara.Dermaga.Fishing
{
    [CreateAssetMenu(menuName = "SAGARA/Dermaga/Coastal fishing UI theme")]
    public sealed class FishingUiTheme : ScriptableObject
    {
        // IMGUI artwork.
        public Texture2D parchment;
        public Texture2D arrow;
        public Texture2D hook;
        public Texture2D illustratedPanel;
        public Texture2D resultPanel;
        public Texture2D keycap;

        // Palette.
        public Color ink = new Color(.22f, .25f, .20f);
        public Color muted = new Color(.43f, .46f, .37f);
        public Color teal = new Color(.15f, .43f, .40f);
        public Color wood = new Color(.39f, .27f, .17f);
        public Color coral = new Color(.73f, .36f, .29f);
        public Color ivory = new Color(1, .97f, .87f);

        public Color Rarity(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Uncommon:
                    return new Color(.29f, .47f, .28f);
                case FishRarity.Rare:
                    return new Color(.22f, .42f, .63f);
                case FishRarity.Epic:
                    return new Color(.48f, .32f, .58f);
                case FishRarity.Legendary:
                    return new Color(.63f, .43f, .11f);
                default:
                    return new Color(.42f, .43f, .36f);
            }
        }
    }
}
