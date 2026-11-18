using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Sagara.Dermaga.Fishing {
public sealed class LivingFishingButton:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler {
 public Image surface;public RectTransform visual;public Button button;
 bool hover,pressed,focused;float amount;
 public void OnPointerEnter(PointerEventData e){hover=true;}public void OnPointerExit(PointerEventData e){hover=false;pressed=false;}
 public void OnPointerDown(PointerEventData e){pressed=true;}public void OnPointerUp(PointerEventData e){pressed=false;}
 public void OnSelect(BaseEventData e){focused=true;}public void OnDeselect(BaseEventData e){focused=false;}
 void Update(){float target=button!=null&&button.interactable?(pressed?-.7f:hover||focused?1:0):0;amount=Mathf.Lerp(amount,target,1-Mathf.Exp(-Time.unscaledDeltaTime*24));visual.localScale=Vector3.one*(1+amount*.025f);surface.color=Color.Lerp(new Color(.82f,.91f,.88f),Color.white,Mathf.Clamp01(.55f+amount*.45f));}
 void OnDisable(){hover=pressed=focused=false;amount=0;if(visual!=null)visual.localScale=Vector3.one;}
}
}
