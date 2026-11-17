using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Sagara.Dermaga.Fishing {
[DefaultExecutionOrder(250)]
public sealed class DimensionalButtonDepth:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler {
 public RectTransform face,shadow;public Button button;public Image sheen;bool over,down,focus;float amount,hover;
 public void OnPointerEnter(PointerEventData e){over=true;}public void OnPointerExit(PointerEventData e){over=false;down=false;}
 public void OnPointerDown(PointerEventData e){if(button.interactable)down=true;}public void OnPointerUp(PointerEventData e){down=false;}
 public void OnSelect(BaseEventData e){focus=true;}public void OnDeselect(BaseEventData e){focus=false;}
 void LateUpdate(){float dt=Time.unscaledDeltaTime;bool enabledNow=button!=null&&button.interactable;amount=Mathf.MoveTowards(amount,enabledNow&&down?1:0,dt/.07f);hover=Mathf.Lerp(hover,enabledNow&&(over||focus)?1:0,1-Mathf.Exp(-dt*18));face.anchoredPosition=new Vector2(0,-amount*6+hover*1.5f);shadow.localScale=new Vector3(1-amount*.035f,1-amount*.12f,1);shadow.anchoredPosition=new Vector2(0,-8+amount*4);if(sheen!=null)sheen.color=new Color(1,1,1,hover*.08f);}
 void OnDisable(){over=down=focus=false;amount=hover=0;if(face!=null)face.anchoredPosition=Vector2.zero;}
 public bool Pressed=>down;public bool Hovered=>over;public bool Focused=>focus;
}
}