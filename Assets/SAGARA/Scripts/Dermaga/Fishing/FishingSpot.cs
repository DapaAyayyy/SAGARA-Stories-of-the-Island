using UnityEngine;
namespace Sagara.Dermaga.Fishing {
[DisallowMultipleComponent]
public sealed class FishingSpot : MonoBehaviour {
 public string spotId;
 public Transform standingPoint, castPoint;
 [Min(.2f)] public float interactionRadius=1.35f;
 public bool Contains(Vector3 player){if(standingPoint==null||castPoint==null)return false;Vector3 delta=player-standingPoint.position;return Mathf.Abs(delta.y)<1.5f&&new Vector2(delta.x,delta.z).sqrMagnitude<=interactionRadius*interactionRadius;}
 public Vector3 CastPosition=>castPoint.position;
}
}