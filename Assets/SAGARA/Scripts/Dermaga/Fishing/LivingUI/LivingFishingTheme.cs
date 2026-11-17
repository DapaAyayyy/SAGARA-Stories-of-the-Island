using UnityEngine;
namespace Sagara.Dermaga.Fishing {
[CreateAssetMenu(menuName="SAGARA/Fishing/Living UI Theme")]
public sealed class LivingFishingTheme : ScriptableObject {
 public Font displayFont,bodyFont;
 public Sprite paper,header,badge,button,keycap,cornerLeft,cornerRight,hook,arrow;
 public Sprite[] portraits;public string[] portraitNames;
 public Color ink=new Color(.20f,.27f,.23f),teal=new Color(.12f,.43f,.41f),ivory=new Color(1,.96f,.84f),coral=new Color(.72f,.32f,.25f);
 public float promptSeconds=.2f,panelSeconds=.26f,pressSeconds=.12f,feedbackSeconds=.18f,comboSeconds=.18f,resultSeconds=.42f;
 public Color Rarity(FishRarity r){switch(r){case FishRarity.Uncommon:return new Color(.27f,.48f,.26f);case FishRarity.Rare:return new Color(.23f,.44f,.65f);case FishRarity.Epic:return new Color(.50f,.32f,.62f);case FishRarity.Legendary:return new Color(.69f,.45f,.11f);default:return new Color(.42f,.45f,.39f);}}
 public Sprite Portrait(string name){for(int i=0;i<portraitNames.Length;i++)if(portraitNames[i]==name&&i<portraits.Length)return portraits[i];return null;}
}
}
