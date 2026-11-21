using UnityEngine;
using UnityEngine.UI;

namespace Sagara.Dermaga.Fishing
{
    /// <summary>
    /// Presentation-only score meter. Reads the existing authoritative round and never
    /// changes scoring or rewards.
    /// </summary>
    [DefaultExecutionOrder(270)]
    public sealed class FishingScoreGaugeUI : MonoBehaviour
    {
        // Gauge visual bindings.
        public LivingFishingPresenter presenter;
        public RectTransform track;
        public RectTransform fill;
        public RectTransform fishMarker;
        public RectTransform targetMarker;
        public Image targetEmblem;
        public CoastalSurfaceGraphic fillSurface;
        public CoastalSurfaceGraphic targetLine;
        public CoastalSurfaceGraphic glint;
        public Image[] sparkles;

        // Fill and completion timing.
        [Range(.15f, .25f)]
        public float riseSeconds = .20f;
        [Range(0, .5f)]
        public float completionHoldSeconds = .50f;

        // Animated fill and pulse state.
        float shown;
        float startValue;
        float goal;
        float age;
        float resolvedAge;
        float pulseAge = 99;
        float maximumAge = 99;

        // Score and target tracking.
        int lastScore = -1;
        int threshold;

        // Threshold confirmation state.
        bool active;
        bool thresholdConfirmed;
        bool maximumConfirmed;

        // Active round snapshot.
        FishRarity rarity;
        Color targetColor;
        FishingRound round;

        public float DisplayedFraction => shown;

        public int AuthoritativeScore => lastScore < 0 ? 0 : lastScore;

        public int TargetScore => threshold;

        public int ThresholdPulses { get; private set; }

        public int MaximumPulses { get; private set; }

        public bool TargetReached => thresholdConfirmed;

        Color RarityColor(FishRarity r)
        {
            switch (r)
            {
                case FishRarity.Uncommon:
                    return new Color(.24f, .57f, .29f);
                case FishRarity.Rare:
                    return new Color(.15f, .48f, .78f);
                case FishRarity.Epic:
                    return new Color(.58f, .32f, .75f);
                case FishRarity.Legendary:
                    return new Color(1, .72f, .15f);
                default:
                    return new Color(.73f, .78f, .76f);
            }
        }

        void Init(FishingSession session)
        {
            round = session.Round;
            active = true;
            shown = startValue = goal = 0;
            age = resolvedAge = 0;
            lastScore = 0;

            threshold = session.Selected.minimumScore;
            rarity = session.Selected.rarity;
            targetColor = RarityColor(rarity);
            thresholdConfirmed = maximumConfirmed = false;
            ThresholdPulses = MaximumPulses = 0;
            pulseAge = maximumAge = 99;

            targetMarker.anchorMin = targetMarker.anchorMax = new Vector2(1, threshold / 100f);
            targetLine.color = targetColor;
            targetEmblem.color = targetColor;

            Render();
        }

        void LateUpdate()
        {
            var session = presenter.Session;

            if (session == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            bool mini = session.State == FishingState.Minigame;

            if (mini && (!active || round != session.Round))
            {
                Init(session);
            }

            bool resolved = session.State == FishingState.CatchSuccess
                || session.State == FishingState.CatchFail;

            if (active && (mini || resolved) && session.Round != null)
            {
                if (resolved)
                {
                    resolvedAge += dt;

                    if (resolvedAge < completionHoldSeconds)
                    {
                        var panel = presenter.view.minigame;
                        panel.amount = 1;
                        panel.group.alpha = 1;
                        panel.rect.anchoredPosition = panel.home;
                    }
                }

                int score = Mathf.Clamp(session.Round.Score, 0, 100);

                if (score != lastScore)
                {
                    startValue = shown;
                    goal = score / 100f;
                    age = 0;
                    lastScore = score;
                }

                age += dt;
                float p = Mathf.Clamp01(age / Mathf.Max(.01f, riseSeconds));
                shown = Mathf.Lerp(startValue, goal, p * p * (3 - 2 * p));

                if (!thresholdConfirmed
                    && score >= threshold
                    && shown + 0.00001f >= threshold / 100f)
                {
                    thresholdConfirmed = true;
                    pulseAge = 0;
                    ThresholdPulses++;
                }

                if (!maximumConfirmed && score == 100 && shown >= .99999f)
                {
                    maximumConfirmed = true;
                    maximumAge = 0;
                    MaximumPulses++;
                }

                pulseAge += dt;
                maximumAge += dt;
                Render();

                if (resolved && presenter.view.minigame.group.alpha > .01f)
                {
                    presenter.view.waiting.group.alpha = 0;
                    presenter.view.waiting.amount = 0;
                }
            }
            else if (!mini && !resolved && active)
            {
                ResetGauge();
            }
        }

        void Render()
        {
            fill.anchorMax = new Vector2(1, shown);
            fillSurface.enabled = shown > .00001f;
            float height = track.rect.height;
            fishMarker.anchoredPosition =
                new Vector2(
                    0,
                    Mathf.Clamp(
                        shown * height - 18,
                        18,
                        height - 18
                    )
                );
            float move = age < riseSeconds
                ? Mathf.Sin(Mathf.Clamp01(age / riseSeconds) * Mathf.PI)
                : 0;
            glint.color = new Color(.75f, 1, .95f, move * .30f);
            glint.rectTransform.anchoredPosition =
                new Vector2(
                    0,
                    Mathf.Clamp(
                        shown * height,
                        1,
                        height - 1
                    )
                );
            float pulse = pulseAge < .55f ? Mathf.Sin(pulseAge / .55f * Mathf.PI) : 0;
            float maximum = maximumAge < .45f ? Mathf.Sin(maximumAge / .45f * Mathf.PI) : 0;
            float anticipation = lastScore >= threshold - 10 && !thresholdConfirmed ? .12f : 0;
            targetEmblem.color = Color.Lerp(targetColor, Color.white, pulse * .45f + anticipation);
            targetEmblem.rectTransform.localScale = Vector3.one * (1 + pulse * .10f);
            targetLine.color = Color.Lerp(targetColor, Color.white, pulse * .4f);
            fillSurface.color = Color.Lerp(
                new Color(.015f, .66f, .62f),
                new Color(.68f, .92f, .61f),
                maximum * .22f
            );

            for (int i = 0; i < sparkles.Length; i++)
            {
                float t = pulseAge / .5f;
                float alpha = t < 1 ? Mathf.Sin(t * Mathf.PI) * .8f : 0;
                var c = targetColor;
                c.a = alpha;
                sparkles[i].color = c;
                sparkles[i].rectTransform.anchoredPosition = new Vector2((i - 1) * 12, 8 + t * 16);
                sparkles[i].rectTransform.localScale = Vector3.one * (.6f + alpha * .5f);
            }
        }

        public void ResetGauge()
        {
            active = false;
            round = null;
            shown = startValue = goal = 0;
            age = 0;
            lastScore = -1;
            thresholdConfirmed = maximumConfirmed = false;
            pulseAge = maximumAge = 99;
            ThresholdPulses = MaximumPulses = 0;

            fill.anchorMax = new Vector2(1, 0);
            fillSurface.enabled = false;
            fishMarker.anchoredPosition = new Vector2(0, 18);
            glint.color = Color.clear;

            foreach (var sparkle in sparkles)
            {
                sparkle.color = Color.clear;
            }

            targetEmblem.rectTransform.localScale = Vector3.one;
        }

        void OnDisable()
        {
            ResetGauge();
        }
    }
}
