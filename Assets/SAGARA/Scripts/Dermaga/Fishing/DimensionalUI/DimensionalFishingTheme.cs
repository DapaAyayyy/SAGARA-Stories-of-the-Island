using UnityEngine;
namespace Sagara.Dermaga.Fishing {
[CreateAssetMenu(menuName="SAGARA/Fishing/Dimensional UI Theme")]
public sealed class DimensionalFishingTheme:ScriptableObject {
 public Sprite frame,header,badgeRim,button,keycap,activeKeycap,crown,flower,wave,wheel;
 public Color ink=new Color(.30f,.16f,.08f),cream=new Color(1,.94f,.79f),teal=new Color(.02f,.52f,.49f);
 public float headerDelay=.045f,detailDelay=.08f,settleSeconds=.22f,pressDepth=6f,scoreSpeed=48f;
 public Color Rarity(FishRarity r){switch(r){case FishRarity.Uncommon:return new Color(.20f,.45f,.27f);case FishRarity.Rare:return new Color(.16f,.42f,.65f);case FishRarity.Epic:return new Color(.46f,.24f,.58f);case FishRarity.Legendary:return new Color(.67f,.34f,.06f);default:return new Color(.44f,.42f,.33f);}}
}
}