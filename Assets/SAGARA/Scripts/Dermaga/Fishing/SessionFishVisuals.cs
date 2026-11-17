using UnityEngine;
namespace Sagara.Dermaga.Fishing {
[DisallowMultipleComponent]
public sealed class SessionFishVisuals : MonoBehaviour {
 public FishingSession session;
 public FishingVisualCatalog catalog;
 [Tooltip("World height of the existing Dermaga water surface.")]
 public float waterHeight=-.9f;
 public bool showDebugMarkers;
 public Renderer[] debugMarkers;
 void OnValidate(){ApplyDebugMarkers();}
 void ApplyDebugMarkers(){if(debugMarkers!=null)foreach(var r in debugMarkers)if(r!=null)r.enabled=showDebugMarkers;}
 GameObject fish;
 Animator fishAnimator;
 FishingState observed=FishingState.Idle;
 Vector3 target,approach,resultStart;
 float phaseTime,clock,nextBubble;
 string activeClip;
 public GameObject ActiveFish=>fish;
 public string ActiveClip=>activeClip;
 public string ActiveFishId {get;private set;}
 public FishRarity ActiveRarity {get;private set;}
 void OnEnable(){ApplyDebugMarkers();if(session==null)session=GetComponent<FishingSession>();if(session!=null)session.StateChanged+=OnState;}
 void OnState(FishingState state){
  observed=state;phaseTime=0;
  if(state==FishingState.WaitingForBite)Spawn();
  else if(state==FishingState.BiteDetected){Play("Bite");}
  else if(state==FishingState.Minigame)Play("Struggle");
  else if(state==FishingState.CatchSuccess||state==FishingState.CatchFail){
   if(fish==null)return;resultStart=fish.transform.position;
   Play(state==FishingState.CatchSuccess?"OutOfWater":"Escape");
   if(state==FishingState.CatchSuccess)session.presentation?.Splash(target+Vector3.up*.08f,session.Selected.aura,12+(int)ActiveRarity*5);
  }
  else if(state==FishingState.Idle||state==FishingState.Cleanup)Clear();
 }
 void Spawn(){
  Clear();if(session==null||session.ActiveSpot==null)return;
  var entry=catalog==null?null:catalog.Find(session.Selected);if(entry==null||entry.prefab==null)return;
  ActiveFishId=session.Selected.fishId;ActiveRarity=session.Selected.rarity;
  target=session.ActiveSpot.CastPosition;target.y=waterHeight-.22f;
  approach=target+new Vector3(2.1f,-.15f,1.1f);
  fish=Instantiate(entry.prefab,approach,Quaternion.identity,transform);fish.name="SessionFish_"+entry.displayName;
  fishAnimator=fish.GetComponentInChildren<Animator>();clock=0;nextBubble=0;Play("Swim");
 }
 void Play(string state){activeClip=state;if(fishAnimator!=null)fishAnimator.CrossFadeInFixedTime(state,.12f);}
 void Update(){
  if(fish==null||session==null)return;float dt=Time.deltaTime;clock+=dt;phaseTime+=dt;
  Vector3 previous=fish.transform.position,next=previous;
  switch(observed){
   case FishingState.WaitingForBite:
    float p=Mathf.SmoothStep(0,1,Mathf.Clamp01(phaseTime/1.8f));
    next=Vector3.Lerp(approach,target,p)+new Vector3(0,Mathf.Sin(clock*3)*.04f,Mathf.Sin(clock*2)*.15f);
    break;
   case FishingState.BiteDetected:
    next=target+new Vector3(Mathf.Sin(clock*10)*.08f,.1f,Mathf.Cos(clock*8)*.09f);break;
   case FishingState.Minigame:
    next=target+new Vector3(Mathf.Sin(clock*3.5f)*.35f,Mathf.Sin(clock*5)*.035f,Mathf.Cos(clock*3.5f)*.35f);break;
   case FishingState.CatchSuccess:
    if(GetComponent<FishLandingPresentation>()!=null)return;
    float rise=Mathf.Clamp01(phaseTime/.85f);
    next=Vector3.Lerp(resultStart,target+new Vector3(-.5f,1.9f,.2f),Mathf.SmoothStep(0,1,rise));
    next.y+=Mathf.Sin(rise*Mathf.PI)*.5f;
    if(rise>=1)next.y+=Mathf.Sin(clock*3)*.06f;
    if(phaseTime>.55f&&nextBubble<1000){nextBubble=1000;Celebrate(next);}
    fish.transform.rotation=Quaternion.Euler(0,220,0);
    break;
   case FishingState.CatchFail:
    next=resultStart+new Vector3(phaseTime*1.8f,-phaseTime*.65f,phaseTime*.65f);
    fish.transform.localScale=Vector3.one*Mathf.Clamp01(1-phaseTime/2.4f);
    break;
  }
  if(observed!=FishingState.CatchSuccess){var heading=next-previous;heading.y=0;if(heading.sqrMagnitude>.00001f)fish.transform.rotation=Quaternion.Slerp(fish.transform.rotation,Quaternion.LookRotation(heading),dt*8);}
  fish.transform.position=next;
  if((observed==FishingState.WaitingForBite||observed==FishingState.Minigame)&&clock>nextBubble){nextBubble=clock+1.1f;session.presentation?.Splash(new Vector3(next.x,waterHeight+.02f,next.z),new Color(.55f,.85f,.9f,.35f),2);}
 }
 public void Celebrate(Vector3 position){
  var go=new GameObject("CatchCelebration");go.transform.SetParent(fish.transform.parent,false);go.transform.position=position;
  var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var m=ps.main;m.loop=false;m.duration=.5f;m.startLifetime=.7f;m.startSpeed=.6f;m.startSize=.045f;m.startColor=session.Selected.aura;m.gravityModifier=.05f;var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(8+(int)ActiveRarity*5))});var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.3f;ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=session.presentation.waterMaterial;ps.Play();Destroy(go,1.3f);
 }
 public void Clear(){if(fish!=null)Destroy(fish);fish=null;fishAnimator=null;activeClip=null;ActiveFishId=null;}
 void OnDisable(){if(session!=null)session.StateChanged-=OnState;Clear();}
 void OnDestroy(){Clear();}
}
}
