using System;
using UnityEngine;
namespace Sagara.Dermaga.Fishing {
[Serializable] public sealed class FishVisualEntry {
 public FishRarity rarity;
 public string temporaryFishId;
 public GameObject prefab;
 public string displayName;
 public Texture2D portrait;
}
[CreateAssetMenu(menuName="SAGARA/Dermaga/Fish visual catalog")]
public sealed class FishingVisualCatalog : ScriptableObject {
 public FishVisualEntry[] entries;
 public FishVisualEntry Find(RaritySettings selected) {
  if(selected==null||entries==null)return null;
  foreach(var e in entries)if(e!=null&&e.rarity==selected.rarity&&e.temporaryFishId==selected.fishId)return e;
  return null;
 }
}
}
