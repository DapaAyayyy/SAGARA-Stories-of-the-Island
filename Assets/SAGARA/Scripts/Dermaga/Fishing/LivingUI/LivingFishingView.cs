using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sagara.Dermaga.Fishing
{
    [Serializable]
    public sealed class LivingPanel
    {
        // Panel bindings.
        public RectTransform rect;
        public CanvasGroup group;

        // Runtime transition state.
        [NonSerialized]
        public Vector2 home;
        [NonSerialized]
        public float amount;

        public void Init()
        {
            home = rect.anchoredPosition;
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;
            amount = 0;
        }

        public void Tick(bool shown, float seconds, float delta)
        {
            amount = Mathf.MoveTowards(amount, shown ? 1 : 0, delta / Mathf.Max(.01f, seconds));
            float eased = amount * amount * (3 - 2 * amount);
            group.alpha = eased;
            rect.anchoredPosition = home + Vector2.down * ((1 - eased) * 14);
        }

        public bool Hit(Vector2 point)
        {
            return group.alpha > .02f
                && RectTransformUtility.RectangleContainsScreenPoint(rect, point, null);
        }
    }

    [Serializable]
    public sealed class LivingArrowToken
    {
        // Arrow visual bindings.
        public RectTransform visual;
        public Image face;
        public Image arrow;
        public Image accent;
        public Text feedback;

        // Transient feedback state.
        [NonSerialized]
        public float age = 10;
        [NonSerialized]
        public float highlight;
        [NonSerialized]
        public int outcome;
        [NonSerialized]
        public bool missed;

        public void Reset()
        {
            age = 10;
            highlight = 0;
            outcome = 0;
            missed = false;
            feedback.text = "";
            visual.localScale = Vector3.one;
            visual.anchoredPosition = Vector2.zero;
        }
    }

    public sealed class LivingFishingView : MonoBehaviour
    {
        // Theme and canvas.
        public LivingFishingTheme theme;
        public Canvas canvas;

        // Panel containers.
        public LivingPanel prompt;
        public LivingPanel waiting;
        public LivingPanel reveal;
        public LivingPanel minigame;
        public LivingPanel result;
        public LivingPanel failure;

        // Labels and feedback.
        public Text worldBite;
        public Text actionText;
        public Text waitingText;
        public Text revealText;
        public Text rarityText;
        public Text comboText;
        public Text scoreText;
        public Text targetText;
        public Text timeText;
        public Text fishName;
        public Text resultRarity;
        public Text resultScore;
        public Text continueText;
        public Text feedbackText;

        // Animated layout.
        public RectTransform waitingIcon;
        public RectTransform revealBadge;
        public RectTransform arrowRow;
        public RectTransform ghostRow;
        public RectTransform portraitRoot;
        public RectTransform scoreRoot;
        public CanvasGroup rowGroup;
        public CanvasGroup ghostGroup;

        // Images and visual feedback.
        public Image timerFill;
        public Image portrait;
        public LivingArrowToken[] tokens;
        public Image[] ghostArrows;
        public Image[] progress;
        public Image[] sparkles;

        // Continue interaction.
        public Button continueButton;
        public LivingFishingButton buttonFeedback;

        public LivingPanel[] Panels()
        {
            return new[]
            {
                prompt,
                waiting,
                reveal,
                minigame,
                result,
                failure
            };
        }
    }
}
