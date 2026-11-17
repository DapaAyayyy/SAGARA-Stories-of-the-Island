using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
namespace Sagara.Dermaga.Fishing {
/// <summary>Presentation only. Observes the fishing session; owns no scoring, timing, reward or world behavior.</summary>
[DefaultExecutionOrder(100)]
public sealed class LivingFishingPresenter:MonoBehaviour {
 public LivingFishingView view;
 FishingSession session;FishLandingPresentation landing;SessionFishVisuals fish;
 FishingState state=FishingState.Idle;float age,resultAge,comboAge=10,scoreAge=10,lastRemaining,clock;
 int cursor,group,lastScore=-1,lastTenths=-1,dots=-1,headerGroup=-1;string headerFish;bool acknowledged,wasReady;
 readonly FishingDirection[] displayed=new FishingDirection[4];
 Func<Vector2,bool> pointer;
 public string LastFeedback {get;private set;}
 public float ResultReveal=>resultAge;
 public bool CanContinue=>landing!=null&&landing.ReadyForConfirmation&&resultAge>=view.theme.resultSeconds&&!acknowledged;
 public FishingSession Session=>session;
 void OnEnable(){
  if(view==null)view=GetComponent<LivingFishingView>();
  session=GetComponentInParent<FishingSession>();landing=GetComponentInParent<FishLandingPresentation>();fish=GetComponentInParent<SessionFishVisuals>();
  if(session==null||view==null)return;
  foreach(var p in view.Panels())p.Init();foreach(var token in view.tokens)token.Reset();
  pointer=ContainsPointer;FishingHud.PointerOverride=pointer;
  view.continueButton.onClick.AddListener(Acknowledge);state=session.State;age=resultAge=0;cursor=group=0;
 }
 bool ContainsPointer(Vector2 point){return view.prompt.Hit(point)||view.waiting.Hit(point)||view.reveal.Hit(point)||view.minigame.Hit(point)||view.result.Hit(point)||view.failure.Hit(point);}
 public void Acknowledge(){if(!CanContinue)return;acknowledged=true;view.continueButton.interactable=false;landing.Acknowledge();}
 void Update(){
  if(session==null||view==null)return;float dt=Time.unscaledDeltaTime;clock+=dt;age+=dt;comboAge+=dt;scoreAge+=dt;
  var theme=view.theme;
  if(state!=session.State){
   state=session.State;age=0;lastTenths=-1;dots=-1;
   if(state==FishingState.Casting){acknowledged=false;wasReady=false;resultAge=0;lastScore=-1;cursor=group=0;foreach(var t in view.tokens)t.Reset();LastFeedback="";view.feedbackText.text="Satu per satu";view.feedbackText.color=theme.teal;}
   if(state==FishingState.Minigame){cursor=0;group=0;comboAge=10;}
  }
  bool idle=state==FishingState.Idle;
  view.prompt.Tick(idle&&session.NearbySpot!=null,theme.promptSeconds,dt);
  view.waiting.Tick(state==FishingState.Casting||state==FishingState.WaitingForBite||state==FishingState.CatchSuccess&&!landing.ReadyForConfirmation,theme.promptSeconds,dt);
  view.reveal.Tick(state==FishingState.BiteDetected,theme.promptSeconds,dt);
  view.minigame.Tick(state==FishingState.Minigame,theme.panelSeconds,dt);
  bool ready=state==FishingState.CatchSuccess&&landing.ReadyForConfirmation;
  if(ready)resultAge+=dt;else resultAge=0;
  view.result.Tick(ready,theme.resultSeconds,dt);view.failure.Tick(state==FishingState.CatchFail&&age>.18f,theme.promptSeconds,dt);
  view.result.group.blocksRaycasts=CanContinue;view.result.group.interactable=CanContinue;view.continueButton.interactable=CanContinue;view.buttonFeedback.enabled=ready;
  if(ready&&!wasReady){PopulateResult();wasReady=true;}
  if(!ready)wasReady=false;
  if(ready){
   float r=Mathf.Clamp01(resultAge/theme.resultSeconds);
   view.portraitRoot.localScale=Vector3.one*Mathf.Lerp(.94f,1,r*r*(3-2*r));
   view.resultRarity.color=theme.Rarity(session.LastResult.rarity)*new Color(1,1,1,Mathf.Clamp01((r-.25f)/.4f));
   view.resultScore.color=theme.ink*new Color(1,1,1,Mathf.Clamp01((r-.45f)/.4f));
   view.continueText.color=theme.ivory*new Color(1,1,1,Mathf.Clamp01((r-.7f)/.3f));
   if(CanContinue&&Keyboard.current!=null&&Keyboard.current.enterKey.wasPressedThisFrame)Acknowledge();
  }
  if(state==FishingState.Casting||state==FishingState.WaitingForBite||state==FishingState.CatchSuccess){
   string caption=state==FishingState.Casting?"Melempar kail":state==FishingState.CatchSuccess?"Menarik tangkapan":"Menunggu gigitan";
   if(state==FishingState.CatchSuccess&&landing.Stage=="Flopping")caption="Tangkapan di dermaga";
   int dot=(int)(clock*2.5f)%4;if(dot!=dots){dots=dot;view.waitingText.text=caption+new string('.',dot);}
   view.waitingIcon.localScale=Vector3.one*(1+Mathf.Sin(clock*2.8f)*.025f);
  }
  if(state==FishingState.BiteDetected){
   var color=theme.Rarity(session.Selected.rarity);view.revealText.text=session.Selected.rarity+" · Ikan menggigit!";
   if(view.worldBite!=null&&Camera.main!=null){var screen=Camera.main.WorldToScreenPoint(session.ActiveSpot.CastPosition+Vector3.up*.8f);view.worldBite.rectTransform.position=screen;view.worldBite.color=color;}
   view.revealText.color=color;view.revealBadge.localScale=Vector3.one*(1+Mathf.Sin(Mathf.Clamp01(age/.28f)*Mathf.PI)*.07f);
   for(int i=0;i<view.sparkles.Length;i++){float p=Mathf.Clamp01(age/.6f);view.sparkles[i].color=new Color(color.r,color.g,color.b,(session.Selected.rarity==FishRarity.Legendary?1:.35f)*(1-p));view.sparkles[i].rectTransform.localScale=Vector3.one*(.4f+Mathf.Sin(p*Mathf.PI)*.6f);}
  }
  if(state==FishingState.Minigame)Minigame(dt);
  else foreach(var t in view.tokens){t.age+=dt;AnimateToken(t,false,dt);}
 }
 void PopulateResult(){
  var entry=fish.catalog.Find(session.Selected);view.fishName.text=entry==null?session.LastResult.fishId:entry.displayName;
  view.portrait.sprite=entry==null?null:view.theme.Portrait(entry.displayName);
  view.resultRarity.text=session.LastResult.rarity.ToString();view.resultScore.text="Skor "+session.LastResult.score+" dari 100";
  view.continueText.text="Lanjutkan  [Enter]";
 }
 void Minigame(float dt){
  var round=session.Round;var theme=view.theme;int currentGroup=Mathf.Min(4,round.Cursor/4);
  if(round.Cursor!=cursor){
   int previousSlot=(round.Cursor-1)%4;bool success=round.outcomes[round.Cursor-1]>0;
   bool missed=!success&&lastRemaining<Mathf.Max(.055f,Time.deltaTime*2);
   var token=view.tokens[previousSlot];token.age=0;token.outcome=success?1:-1;token.missed=missed;
   LastFeedback=success?"Bagus! +5":missed?"Terlewat":"Salah arah";view.feedbackText.text=LastFeedback;view.feedbackText.color=success?theme.teal:theme.coral;cursor=round.Cursor;
  }
  if(currentGroup!=group){
   for(int i=0;i<4;i++){view.ghostArrows[i].rectTransform.localEulerAngles=new Vector3(0,0,Angle(displayed[i]));view.ghostArrows[i].color=theme.teal;view.tokens[i].Reset();}
   group=currentGroup;comboAge=0;
  }
  float transition=Mathf.Clamp01(comboAge/theme.comboSeconds);
  view.ghostGroup.alpha=1-transition;view.ghostRow.anchoredPosition=Vector2.left*transition*15;
  view.rowGroup.alpha=.45f+.55f*transition;view.arrowRow.anchoredPosition=Vector2.right*(1-transition)*15;
  for(int i=0;i<4;i++){
   int slot=group*4+i;displayed[i]=round.sequence[slot];var token=view.tokens[i];
   token.arrow.rectTransform.localEulerAngles=new Vector3(0,0,Angle(displayed[i]));
   if(round.outcomes[slot]!=0&&token.outcome==0)token.outcome=round.outcomes[slot];
   token.age+=dt;AnimateToken(token,slot==round.Cursor,dt);
   float entrance=Mathf.Clamp01((age-i*.025f)/.12f);token.visual.localScale*=Mathf.Lerp(.94f,1,entrance);token.visual.anchoredPosition+=Vector2.down*(1-entrance)*5;
  }
  for(int i=0;i<5;i++){view.progress[i].color=i<=group?theme.teal:new Color(.78f,.72f,.57f);view.progress[i].rectTransform.localScale=Vector3.one*(1+(i==group?Mathf.Sin(Mathf.Clamp01(comboAge/theme.comboSeconds)*Mathf.PI)*.10f:0));}
  if(headerGroup!=group||headerFish!=session.Selected.fishId){headerGroup=group;headerFish=session.Selected.fishId;view.comboText.text="Kombinasi "+(group+1)+" / 5";view.rarityText.text=session.Selected.rarity.ToString();view.rarityText.color=theme.Rarity(session.Selected.rarity);view.targetText.text="Target "+session.Selected.minimumScore;}
  int tenths=Mathf.CeilToInt(session.ArrowRemaining*10);if(tenths!=lastTenths){lastTenths=tenths;view.timeText.text=(tenths/10f).ToString("0.0")+" dtk";}
  lastRemaining=session.ArrowRemaining;float fraction=Mathf.Clamp01(lastRemaining/session.configuration.arrowSeconds);view.timerFill.fillAmount=fraction;view.timerFill.color=Color.Lerp(theme.coral,theme.teal,Mathf.Clamp01(fraction*3));
  if(round.Score!=lastScore){lastScore=round.Score;view.scoreText.text="Skor "+lastScore+" / 100";scoreAge=0;}
  view.scoreRoot.localScale=Vector3.one*(1+Mathf.Sin(Mathf.Clamp01(scoreAge/.18f)*Mathf.PI)*.045f);
 }
 void AnimateToken(LivingArrowToken t,bool active,float dt){
  var theme=view.theme;t.highlight=Mathf.MoveTowards(t.highlight,active?1:0,dt/.09f);
  Color face=Color.Lerp(Color.white,new Color(.40f,.78f,.73f),t.highlight);
  if(t.outcome!=0)face=Color.Lerp(Color.white,t.outcome>0?new Color(.65f,.86f,.72f):new Color(.94f,.69f,.59f),.65f);
  t.face.color=face;t.arrow.color=t.outcome<0?theme.coral:active?theme.teal:theme.ink;
  t.accent.color=new Color(theme.teal.r,theme.teal.g,theme.teal.b,t.highlight*.9f);
  float press=Mathf.Sin(Mathf.Clamp01(t.age/theme.pressSeconds)*Mathf.PI);
  t.visual.localScale=Vector3.one*(1-press*.08f);
  float shake=t.outcome<0&&!t.missed&&t.age<theme.feedbackSeconds?Mathf.Sin(t.age*65)*3*(1-t.age/theme.feedbackSeconds):0;
  t.visual.anchoredPosition=new Vector2(shake,0);
  t.feedback.text=t.outcome>0?"+5":t.outcome<0?(t.missed?"Lewat":"Salah"):"";
  t.feedback.color=t.outcome>0?theme.teal:theme.coral;
 }
 static float Angle(FishingDirection d){return d==FishingDirection.Down?180:d==FishingDirection.Left?90:d==FishingDirection.Right?-90:0;}
 void OnDisable(){
  if(view!=null){view.continueButton.onClick.RemoveListener(Acknowledge);foreach(var p in view.Panels())if(p.group!=null){p.group.alpha=0;p.amount=0;}view.ghostGroup.alpha=0;}
  if(FishingHud.PointerOverride==pointer)FishingHud.PointerOverride=null;
 }
}
}
