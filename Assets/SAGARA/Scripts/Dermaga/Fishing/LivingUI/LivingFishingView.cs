using System;
using UnityEngine;
using UnityEngine.UI;
namespace Sagara.Dermaga.Fishing {
[Serializable] public sealed class LivingPanel {
 public RectTransform rect;public CanvasGroup group;
 [NonSerialized] public Vector2 home;[NonSerialized] public float amount;
 public void Init(){home=rect.anchoredPosition;group.alpha=0;group.blocksRaycasts=false;group.interactable=false;amount=0;}
 public void Tick(bool shown,float seconds,float delta){amount=Mathf.MoveTowards(amount,shown?1:0,delta/Mathf.Max(.01f,seconds));float eased=amount*amount*(3-2*amount);group.alpha=eased;rect.anchoredPosition=home+Vector2.down*((1-eased)*14);}
 public bool Hit(Vector2 point){return group.alpha>.02f&&RectTransformUtility.RectangleContainsScreenPoint(rect,point,null);}
}
[Serializable] public sealed class LivingArrowToken {
 public RectTransform visual;public Image face,arrow,accent;public Text feedback;
 [NonSerialized]public float age=10,highlight;[NonSerialized]public int outcome;[NonSerialized]public bool missed;
 public void Reset(){age=10;highlight=0;outcome=0;missed=false;feedback.text="";visual.localScale=Vector3.one;visual.anchoredPosition=Vector2.zero;}
}
public sealed class LivingFishingView:MonoBehaviour {
 public LivingFishingTheme theme;public Canvas canvas;
 public LivingPanel prompt,waiting,reveal,minigame,result,failure;
 public Text worldBite;
 public Text actionText,waitingText,revealText,rarityText,comboText,scoreText,targetText,timeText,fishName,resultRarity,resultScore,continueText,feedbackText;
 public RectTransform waitingIcon,revealBadge,arrowRow,ghostRow,portraitRoot,scoreRoot;
 public CanvasGroup rowGroup,ghostGroup;public Image timerFill,portrait;
 public LivingArrowToken[] tokens;public Image[] ghostArrows,progress;public Image[] sparkles;
 public Button continueButton;public LivingFishingButton buttonFeedback;
 public LivingPanel[] Panels(){return new[]{prompt,waiting,reveal,minigame,result,failure};}
}
}
