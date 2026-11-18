using UnityEngine;
namespace Sagara.Dermaga.Fishing {
/// <summary>Shared drawing primitives for the isolated fishing IMGUI presentation.</summary>
public sealed class FishingUiPainter {
 readonly FishingUiTheme theme;
 Font displayFont;
 readonly GUIStyle small,body,bold,title,number,panel;
 public FishingUiPainter(FishingUiTheme theme){
  this.theme=theme;var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
  body=new GUIStyle(GUI.skin.label){font=font,fontSize=24,alignment=TextAnchor.MiddleLeft,wordWrap=false,padding=new RectOffset(0,0,0,0)};
  small=new GUIStyle(body){fontSize=21};bold=new GUIStyle(body){fontStyle=FontStyle.Bold};displayFont=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman","Arial"},32);displayFont.hideFlags=HideFlags.HideAndDontSave;title=new GUIStyle(bold){font=displayFont,fontSize=32};number=new GUIStyle(bold){fontSize=29};
  panel=new GUIStyle();panel.normal.background=theme.parchment;panel.border=new RectOffset(18,18,18,18);
 }
 public void Release(){title.font=null;if(displayFont!=null){if(Application.isPlaying)Object.Destroy(displayFont);else Object.DestroyImmediate(displayFont);}displayFont=null;}
 public void Rect(Rect r,Color color){var old=GUI.color;GUI.color=new Color(color.r,color.g,color.b,color.a*old.a);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
 public void Panel(Rect r){
  var shadow=r;shadow.position+=new Vector2(0,5);var old=GUI.color;GUI.color=new Color(.19f,.16f,.1f,.2f*old.a);GUI.Box(shadow,GUIContent.none,panel);GUI.color=old;GUI.Box(r,GUIContent.none,panel);
 }
 public void Text(Rect r,string text,Color color,int kind=0,TextAnchor alignment=TextAnchor.MiddleLeft){
  var style=kind==1?small:kind==2?bold:kind==3?title:kind==4?number:body;var oldAlignment=style.alignment;var old=GUI.contentColor;GUI.contentColor=color;style.alignment=alignment;GUI.Label(r,text,style);style.alignment=oldAlignment;GUI.contentColor=old;
 }
 public void Icon(Rect r,Texture2D texture,Color color){if(texture==null)return;var old=GUI.color;GUI.color=new Color(color.r,color.g,color.b,color.a*old.a);GUI.DrawTexture(r,texture,ScaleMode.ScaleToFit,true);GUI.color=old;}
 public void Arrow(Rect r,FishingDirection direction,Color color){
  var matrix=GUI.matrix;float angle=direction==FishingDirection.Down?180:direction==FishingDirection.Left?-90:direction==FishingDirection.Right?90:0;
  var pivot=r.center;GUI.matrix=matrix*Matrix4x4.Translate(new Vector3(pivot.x,pivot.y,0))*Matrix4x4.Rotate(Quaternion.Euler(0,0,angle))*Matrix4x4.Translate(new Vector3(-pivot.x,-pivot.y,0));Icon(r,theme.arrow,color);GUI.matrix=matrix;
 }
 public void Illustrated(Rect r,bool result){var art=result?theme.resultPanel:theme.illustratedPanel;if(art==null){Panel(r);return;}var old=GUI.color;GUI.DrawTexture(r,art,ScaleMode.StretchToFill,true);GUI.color=old;}
 public void Keycap(Rect r,bool active,Color state){if(theme.keycap==null){Panel(r);return;}var old=GUI.color;GUI.color=new Color(active?.30f:1,active?.72f:1,active?.68f:1,old.a);GUI.DrawTexture(r,theme.keycap,ScaleMode.StretchToFill,true);GUI.color=old;}
 public void Rule(Rect r){Rect(r,new Color(.67f,.55f,.37f,.3f));}
 public bool Button(Rect r,string text,Color accent){
  bool hover=r.Contains(Event.current.mousePosition);Rect(new Rect(r.x,r.y+3,r.width,r.height),theme.wood);Rect(r,hover?Color.Lerp(accent,theme.ivory,.12f):accent);
  Text(r,text,theme.ivory,2,TextAnchor.MiddleCenter);
  return GUI.Button(r,GUIContent.none,GUIStyle.none);
 }
}
}
