using UnityEngine;
using UnityEngine.InputSystem;
namespace Sagara.Dermaga.Fishing {
/// <summary>Fishing UI state binding and transitions; no scoring or randomness.</summary>
public sealed class FishingHud : MonoBehaviour {
 public FishingSession session;
 public FishingUiTheme theme;
 public FishLandingPresentation landing;
 public FishingCatchCard catchCard;
 FishingUiPainter draw;
 FishingState previous=FishingState.Idle;
 float stateAge,shownScore,pressAge=10,combinationAge,resultAge;
 int previousCursor,previousGroup;
 bool correct;
 static Rect visibleBounds;
 static readonly Rect minigame=new Rect(440,752,1040,292);
 public static System.Func<Vector2,bool> PointerOverride;
 public static bool ContainsPointer(Vector2 point){
  if(PointerOverride!=null)return PointerOverride(point);
  var p=new Vector2(point.x*1920/Screen.width,(Screen.height-point.y)*1080/Screen.height);
  return visibleBounds.Contains(p);
 }
 void Update(){
  if(session==null)return;
  if(session.State!=previous){previous=session.State;stateAge=0;previousCursor=0;previousGroup=0;combinationAge=0;}
  if(landing!=null&&landing.ReadyForConfirmation)resultAge+=Time.deltaTime;else resultAge=0;
  stateAge+=Time.deltaTime;pressAge+=Time.deltaTime;combinationAge+=Time.deltaTime;
  shownScore=Mathf.MoveTowards(shownScore,session.Round==null?0:session.Round.Score,Time.deltaTime*120);
  if(session.Round!=null&&session.Round.Cursor!=previousCursor){
   int latest=session.Round.Cursor-1;correct=latest>=0&&session.Round.outcomes[latest]>0;pressAge=0;previousCursor=session.Round.Cursor;
   int group=Mathf.Min(4,previousCursor/4);if(group!=previousGroup){previousGroup=group;combinationAge=0;}
  }
  var key=Keyboard.current;
  if(landing!=null&&landing.ReadyForConfirmation&&key!=null&&key.enterKey.wasPressedThisFrame)landing.Acknowledge();
 }
 void OnDisable(){visibleBounds=new Rect();if(draw!=null)draw.Release();draw=null;}
 void OnGUI(){
  if(session==null||!session.isActiveAndEnabled||theme==null)return;
  if(draw==null)draw=new FishingUiPainter(theme);
  var oldMatrix=GUI.matrix;var oldColor=GUI.color;
  GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1920f,Screen.height/1080f,1));
  visibleBounds=new Rect();
  try{
   float enter=Mathf.SmoothStep(0,1,Mathf.Clamp01(stateAge/.22f));
   GUI.color=new Color(1,1,1,enter);
   if(session.State==FishingState.Idle){
    if(session.NearbySpot!=null)Prompt("E","Memancing di dermaga",new Rect(730,952,460,70));return;
   }
   if(session.State==FishingState.Casting){Prompt("~","Melempar kail…",new Rect(785,966,350,60));return;}
   if(session.State==FishingState.WaitingForBite){Prompt("~","Menunggu gigitan…",new Rect(770,966,380,60));return;}
   if(session.State==FishingState.BiteDetected){
    var accent=theme.Rarity(session.Selected.rarity);var r=new Rect(715,934+(1-enter)*8,490,92);visibleBounds=r;draw.Panel(r);draw.Icon(new Rect(r.x+20,r.y+20,40,44),theme.hook,accent);
    draw.Text(new Rect(r.x+78,r.y+10,390,34),"Ikan menggigit!",theme.ink,2);
    draw.Text(new Rect(r.x+78,r.y+45,390,33),session.Selected.rarity+" · Bersiap!",accent,1);
    var camera=Camera.main;if(camera!=null){var p=camera.WorldToScreenPoint(session.ActiveSpot.CastPosition+Vector3.up);if(p.z>0)draw.Text(new Rect(p.x*1920/Screen.width-20,(Screen.height-p.y)*1080/Screen.height-40,40,65),"!",accent,3,TextAnchor.MiddleCenter);}return;
   }
   if(session.State==FishingState.CatchSuccess){
    if(landing!=null&&landing.ReadyForConfirmation){GUI.color=new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(resultAge/.25f)));visibleBounds=catchCard.Bounds;catchCard.Draw(draw);}
    else Prompt("~",landing!=null&&landing.Stage=="Flopping"?"Tangkapan di dermaga":"Menarik tangkapan…",new Rect(750,966,420,60));return;
   }
   if(session.State==FishingState.CatchFail){
    var r=new Rect(720,942,480,84);visibleBounds=r;draw.Panel(r);draw.Icon(new Rect(r.x+22,r.y+20,40,42),theme.hook,theme.muted);
    draw.Text(new Rect(r.x+80,r.y+9,380,35),"Ikan berhasil lepas",theme.ink,2);
    draw.Text(new Rect(r.x+80,r.y+43,380,31),"Tenang, laut masih menyimpan cerita.",theme.muted,1);return;
   }
   if(session.State==FishingState.Minigame)Minigame(enter);
  }finally{GUI.matrix=oldMatrix;GUI.color=oldColor;}
 }
 void Prompt(string key,string text,Rect r){
  visibleBounds=r;draw.Panel(r);draw.Rect(new Rect(r.x+16,r.y+14,39,r.height-28),theme.teal);
  if(key=="~")draw.Icon(new Rect(r.x+21,r.y+17,29,r.height-34),theme.hook,theme.ivory);else draw.Text(new Rect(r.x+16,r.y+14,39,r.height-28),key,theme.ivory,2,TextAnchor.MiddleCenter);
  draw.Text(new Rect(r.x+69,r.y+8,r.width-80,r.height-16),text,theme.ink,1);
 }
 void Minigame(float enter){
  var r=minigame;r.y+=(1-enter)*12;visibleBounds=r;draw.Illustrated(r,false);
  var round=session.Round;int group=Mathf.Min(4,round.Cursor/4);var accent=theme.Rarity(session.Selected.rarity);
  draw.Text(new Rect(r.x+95,r.y+17,300,43),"MEMANCING",theme.teal,3,TextAnchor.MiddleCenter);
  draw.Text(new Rect(r.x+407,r.y+17,215,43),session.Selected.rarity.ToString(),theme.ivory,2,TextAnchor.MiddleCenter);
  draw.Text(new Rect(r.x+700,r.y+17,245,43),"Kombinasi "+(group+1)+" / 5",theme.wood,2,TextAnchor.MiddleCenter);
  float shift=(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(combinationAge/.16f)))*7;
  draw.Icon(new Rect(r.x+62,r.y+94,40,46),theme.hook,theme.teal);
  draw.Text(new Rect(r.x+114,r.y+91,190,32),"IKUTI ARAH",theme.wood,2);
  draw.Text(new Rect(r.x+114,r.y+125,180,30),pressAge<.55f?(correct?"+5 · Bagus!":"0 · Terlewat"):"Satu per satu",pressAge<.55f&&!correct?theme.coral:theme.teal,1);
  for(int i=0;i<4;i++){
   int slot=group*4+i;bool active=slot==round.Cursor;bool done=round.outcomes[slot]!=0;
   var tile=new Rect(r.x+322+i*112+shift,r.y+88,100,89);
   var color=round.outcomes[slot]>0?theme.teal:round.outcomes[slot]<0?theme.coral:theme.wood;
   draw.Keycap(tile,active,done?color:Color.white);
   float press=slot==previousCursor-1?Mathf.Max(0,1-pressAge/.18f):0;
   draw.Arrow(new Rect(tile.x+28+press*2,tile.y+24+press*2,44-press*4,40-press*4),round.sequence[slot],active?theme.ivory:color);
   if(done)draw.Text(new Rect(tile.x+29,tile.y+65,44,24),round.outcomes[slot]>0?"+5":"0",color,1,TextAnchor.MiddleCenter);
  }
  float remaining=session.ArrowRemaining;
  draw.Text(new Rect(r.x+805,r.y+91,165,40),remaining.ToString("0.0")+" dtk",theme.wood,4,TextAnchor.MiddleCenter);
  draw.Text(new Rect(r.x+805,r.y+131,165,29),"per arah",theme.muted,1,TextAnchor.MiddleCenter);
  draw.Rect(new Rect(r.x+120,r.y+193,790,5),new Color(.70f,.64f,.49f));
  draw.Rect(new Rect(r.x+120,r.y+193,790*Mathf.Clamp01(remaining/session.configuration.arrowSeconds),5),theme.teal);
  draw.Text(new Rect(r.x+195,r.y+207,260,40),"Skor "+Mathf.RoundToInt(shownScore)+" / 100",theme.wood,2);
  draw.Text(new Rect(r.x+435,r.y+207,220,40),"Target "+session.Selected.minimumScore,theme.wood,2,TextAnchor.MiddleCenter);
  draw.Text(new Rect(r.x+720,r.y+207,190,40),"Esc · Batal",theme.wood,1,TextAnchor.MiddleCenter);
 }
}
}
