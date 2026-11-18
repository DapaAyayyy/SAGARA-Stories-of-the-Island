using UnityEngine;
using UnityEngine.UI;
namespace Sagara.Dermaga.Fishing {
/// <summary>A UI mesh surface: rounded geometry, directional bevel shading, or a soft shadow. No baked labels.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CoastalSurfaceGraphic:MaskableGraphic {
 public float radius=20,bevel=3,shade=.12f;public bool softShadow;
 const int Steps=10;const int Count=44;
 Vector2 Point(Rect r,int i,float rad) {
  int corner=i/(Steps+1),segment=i%(Steps+1);float a=(corner*90+segment*90f/Steps)*Mathf.Deg2Rad;
  Vector2 center=corner==0?new Vector2(r.xMax-rad,r.yMax-rad):corner==1?new Vector2(r.xMin+rad,r.yMax-rad):corner==2?new Vector2(r.xMin+rad,r.yMin+rad):new Vector2(r.xMax-rad,r.yMin+rad);
  return center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*rad;
 }
 Color Tone(Color c,float y,Rect r,float amount){return Color.Lerp(c,y>r.center.y?Color.white:new Color(c.r*.55f,c.g*.55f,c.b*.55f,c.a),Mathf.Abs(y-r.center.y)/Mathf.Max(1,r.height*.5f)*amount);}
 protected override void OnPopulateMesh(VertexHelper vh){
  vh.Clear();Rect r=GetPixelAdjustedRect();float rad=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
  Rect inner=new Rect(r.x+bevel,r.y+bevel,Mathf.Max(0,r.width-2*bevel),Mathf.Max(0,r.height-2*bevel));
  var center=UIVertex.simpleVert;center.position=r.center;center.color=color;vh.AddVert(center);
  for(int i=0;i<Count;i++){var v=UIVertex.simpleVert;v.position=Point(inner,i,Mathf.Max(0,rad-bevel));v.color=softShadow?color:Tone(color,v.position.y,r,shade*.5f);vh.AddVert(v);}
  for(int i=0;i<Count;i++)vh.AddTriangle(0,1+i,1+(i+1)%Count);
  for(int i=0;i<Count;i++){var v=UIVertex.simpleVert;v.position=Point(r,i,rad);v.color=softShadow?new Color(color.r,color.g,color.b,0):Tone(color,v.position.y,r,shade);vh.AddVert(v);}
  for(int i=0;i<Count;i++){int n=(i+1)%Count;vh.AddTriangle(1+i,1+Count+i,1+Count+n);vh.AddTriangle(1+i,1+Count+n,1+n);}
 }
}
}