using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
namespace Sagara.Dermaga.Fishing {
/// <summary>Isolated runtime display of the actual catalog model. Never moves or edits the caught world fish.</summary>
[DefaultExecutionOrder(210)]
public sealed class FishingCatchPreview:MonoBehaviour {
 public LivingFishingPresenter presenter;public RawImage output;public Shader previewShader;
 readonly System.Collections.Generic.List<Material> ownedMaterials=new System.Collections.Generic.List<Material>();
 GameObject stage,model,turntable;Camera previewCamera;RenderTexture target;string identity;float age;
 const int PreviewLayer=31;readonly Vector3 origin=new Vector3(10000,10000,10000);
 public string PreviewIdentity=>identity;public bool Active=>model!=null;public float PreviewYaw=>turntable==null?0:turntable.transform.localEulerAngles.y;
 void CreateStage(){
  stage=new GameObject("FishingPreviewStage_Runtime");stage.hideFlags=HideFlags.DontSave;stage.transform.position=origin;
  var cameraObject=new GameObject("CatchPreviewCamera");cameraObject.transform.SetParent(stage.transform,false);previewCamera=cameraObject.AddComponent<Camera>();previewCamera.orthographic=true;previewCamera.orthographicSize=1.45f;previewCamera.nearClipPlane=.05f;previewCamera.farClipPlane=15;previewCamera.cullingMask=1<<PreviewLayer;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(0,0,0,0);previewCamera.allowHDR=false;previewCamera.allowMSAA=true;previewCamera.depth=-20;
  cameraObject.transform.localPosition=new Vector3(0,.12f,-4);cameraObject.transform.LookAt(origin);
  var data=previewCamera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.renderShadows=false;data.volumeLayerMask=0;data.requiresColorTexture=false;data.requiresDepthTexture=false;
  target=new RenderTexture(768,576,24,RenderTextureFormat.ARGB32){name="CatchPreview_768x576_Runtime",antiAliasing=4,hideFlags=HideFlags.DontSave};target.Create();previewCamera.targetTexture=target;output.texture=target;

  turntable=new GameObject("FishTurntable");turntable.transform.SetParent(stage.transform,false);
 }
 void AddLight(string name,Vector3 p,Color tint,float intensity){var go=new GameObject(name);go.transform.SetParent(stage.transform,false);go.transform.localPosition=p;var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=8;light.intensity=intensity;light.color=tint;light.shadows=LightShadows.None;light.cullingMask=1<<PreviewLayer;}
 Bounds BoundsOf(){var renderers=model.GetComponentsInChildren<Renderer>();var b=new Bounds();bool first=true;var mesh=new Mesh();foreach(var renderer in renderers){var skin=renderer as SkinnedMeshRenderer;if(skin!=null){skin.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices){var point=skin.transform.TransformPoint(vertex);if(first){b=new Bounds(point,Vector3.zero);first=false;}else b.Encapsulate(point);}}else{if(first){b=renderer.bounds;first=false;}else b.Encapsulate(renderer.bounds);}}Destroy(mesh);return b;}
 void Show(){
  var session=presenter.Session;var catalog=session.GetComponent<SessionFishVisuals>().catalog;var entry=catalog.Find(session.Selected);if(entry==null||entry.prefab==null)return;
  if(stage==null)CreateStage();ClearModel();model=Instantiate(entry.prefab,turntable.transform);model.name="Preview_"+entry.displayName;foreach(var t in model.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=PreviewLayer;foreach(var col in t.GetComponents<Collider>())col.enabled=false;}
  foreach(var renderer in model.GetComponentsInChildren<Renderer>()){var source=renderer.sharedMaterials;var materials=new Material[source.Length];for(int i=0;i<source.Length;i++){var material=new Material(previewShader);material.name="PreviewPalette_Runtime";material.SetColor("_BaseColor",source[i].HasProperty("_BaseColor")?source[i].GetColor("_BaseColor"):source[i].color);ownedMaterials.Add(material);materials[i]=material;}renderer.sharedMaterials=materials;}
  model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(0,90,0);
  var animator=model.GetComponentInChildren<Animator>();if(animator!=null){animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Play("Swim",0,0);animator.Update(0);}
  var b=BoundsOf();float scale=.96f/Mathf.Max(b.size.x/3.6f,b.size.y/2.45f);model.transform.localScale*=scale;b=BoundsOf();model.transform.position+=origin-b.center;
  identity=session.Selected.fishId;age=0;previewCamera.enabled=true;
 }
 void LateUpdate(){
  if(presenter==null||presenter.Session==null)return;
  bool show=presenter.Session.State==FishingState.CatchSuccess&&presenter.CanContinue||presenter.Session.State==FishingState.CatchSuccess&&presenter.ResultReveal>0;
  if(show&&identity!=presenter.Session.Selected.fishId)Show();
  if(show&&model!=null){age+=Time.unscaledDeltaTime;turntable.transform.localRotation=Quaternion.Euler(0,Mathf.Sin(age*.55f)*13f,0);turntable.transform.localPosition=Vector3.up*(Mathf.Sin(age*1.3f)*.025f);}
  if(!show&&presenter.view.result.group.alpha<.01f){ClearModel();if(previewCamera!=null)previewCamera.enabled=false;}
 }
 void ClearModel(){if(model!=null)Destroy(model);foreach(var material in ownedMaterials)Destroy(material);ownedMaterials.Clear();model=null;identity=null;if(turntable!=null){turntable.transform.localRotation=Quaternion.identity;turntable.transform.localPosition=Vector3.zero;}}
 void OnDisable(){ClearModel();if(output!=null)output.texture=null;if(target!=null){target.Release();Destroy(target);}if(stage!=null)Destroy(stage);stage=null;previewCamera=null;target=null;}
}
}