using UnityEngine;
using UnityEngine.UI;

namespace Sagara.Dermaga.Fishing
{
    /// <summary>
    /// Additional 2.5D layer choreography. Reads session outcomes; never submits inputs
    /// or awards catches.
    /// </summary>
    [DefaultExecutionOrder(240)]
    public sealed class DimensionalFishingMotion : MonoBehaviour
    {
        // Presenter and style.
        public LivingFishingPresenter presenter;
        public DimensionalFishingTheme theme;

        // Layer transforms and visibility.
        public RectTransform miniHeader;
        public RectTransform miniDecor;
        public RectTransform resultHeader;
        public RectTransform resultDecor;
        public CanvasGroup miniHeaderGroup;
        public CanvasGroup miniDecorGroup;
        public CanvasGroup resultHeaderGroup;
        public CanvasGroup resultDecorGroup;
        public CanvasGroup previewGroup;
        public CanvasGroup rarityGroup;
        public CanvasGroup scoreGroup;
        public CanvasGroup continueGroup;

        // Arrow depth visuals.
        public RectTransform[] arrowShadows;
        public RectTransform[] ghostFaces;

        // Observed input timing.
        readonly float[] pressAges =
        {
            99,
            99,
            99,
            99
        };
        int observedCursor;
        int observedGroup;
        float comboClock = 99;

        // Animated accents and surfaces.
        public Image[] arrowGlows;
        public Image[] legendSparkles;
        public CoastalSurfaceGraphic miniEnamel;
        public CoastalSurfaceGraphic resultEnamel;
        public CoastalSurfaceGraphic timerEnamel;
        public Text miniTitle;
        public Text resultTitle;

        // Cached layout origins.
        LivingFishingView view;
        Vector2 miniHome;
        Vector2 miniDecorHome;
        Vector2 resultHome;
        Vector2 resultDecorHome;

        // Presentation timing and display cache.
        float miniAge = 10;
        float shownScore;
        float legendAge;
        float heldResultAge;
        bool wasMini;
        int displayScore = -1;
        int cachedTarget = -1;
        string scoreString = "0";
        string targetString = "0";

        void OnEnable()
        {
            view = presenter.view;
            miniHome = miniHeader.anchoredPosition;
            miniDecorHome = miniDecor.anchoredPosition;
            resultHome = resultHeader.anchoredPosition;
            resultDecorHome = resultDecor.anchoredPosition;

            wasMini = false;
            miniAge = 10;
            displayScore = -1;
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3 - 2 * t);
        }

        void Layer(
            RectTransform rt,
            CanvasGroup group,
            Vector2 home,
            float age,
            float delay,
            float duration
        )
        {
            float p = Ease((age - delay) / duration);
            group.alpha = p;
            rt.anchoredPosition = home + Vector2.up * (1 - p) * 10;
            rt.localScale = Vector3.one * Mathf.Lerp(.975f, 1, p);
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

            if (mini && !wasMini)
            {
                miniAge = legendAge = 0;
                shownScore = 0;
                displayScore = -1;
                cachedTarget = -1;
                observedCursor = observedGroup = 0;
                comboClock = 99;

                for (int i = 0; i < 4; i++)
                {
                    pressAges[i] = 99;
                }
            }

            if (mini)
            {
                miniAge += dt;
                legendAge += dt;
            }

            wasMini = mini;

            Layer(
                miniHeader,
                miniHeaderGroup,
                miniHome,
                miniAge,
                theme.headerDelay,
                theme.settleSeconds
            );
            Layer(
                miniDecor,
                miniDecorGroup,
                miniDecorHome,
                miniAge,
                theme.detailDelay,
                theme.settleSeconds
            );

            if (mini)
            {
                int nextGroup = Mathf.Min(4, session.Round.Cursor / 4);
                bool groupChanged = nextGroup != observedGroup;

                if (groupChanged)
                {
                    comboClock = 0;
                    observedGroup = nextGroup;

                    for (int i = 0; i < 4; i++)
                    {
                        pressAges[i] = 99;
                    }
                }

                if (session.Round.Cursor != observedCursor)
                {
                    if (!groupChanged)
                    {
                        pressAges[(session.Round.Cursor - 1) % 4] = 0;
                    }

                    observedCursor = session.Round.Cursor;
                }

                float combo = Ease(comboClock / view.theme.comboSeconds);
                view.ghostGroup.alpha = 1 - combo;
                view.rowGroup.alpha = .45f + .55f * combo;
                view.ghostRow.anchoredPosition = Vector2.left * combo * 15;
                view.arrowRow.anchoredPosition = Vector2.right * (1 - combo) * 15;

                if (ghostFaces != null)
                {
                    for (int i = 0; i < ghostFaces.Length; i++)
                    {
                        ghostFaces[i].anchoredPosition = Vector2.down * (i == 3 ? Mathf.Max(
                            0,
                            1 - comboClock / view.theme.pressSeconds
                        ) * theme.pressDepth
                            : 0);
                        view.ghostArrows[i].color = theme.cream;
                    }
                }

                comboClock += dt;

                if (timerEnamel != null)
                {
                    float fraction =
                        Mathf.Clamp01(
                            session.ArrowRemaining / session.configuration.arrowSeconds
                        );
                    timerEnamel.rectTransform.sizeDelta =
                        new Vector2(
                            Mathf.Max(
                                1,
                                1054 * fraction
                            ),
                            -6
                        );
                    timerEnamel.color = Color.Lerp(
                        new Color(.83f, .40f, .22f),
                        new Color(.02f, .65f, .59f),
                        Mathf.Clamp01(fraction * 3)
                    );
                }

                shownScore = Mathf.MoveTowards(
                    shownScore,
                    session.Round.Score,
                    dt * theme.scoreSpeed
                );
                int value = Mathf.RoundToInt(shownScore);

                if (value != displayScore)
                {
                    displayScore = value;
                    scoreString = value.ToString();
                }

                if (view.scoreText.text != scoreString)
                {
                    view.scoreText.text = scoreString;
                }

                if (cachedTarget != session.Selected.minimumScore)
                {
                    cachedTarget = session.Selected.minimumScore;
                    targetString = cachedTarget.ToString();
                }

                view.targetText.text = targetString;
                view.rarityText.color = new Color(1, .94f, .69f);
                miniEnamel.color = session.Selected.rarity == FishRarity.Legendary
                    ? new Color(.46f, .19f, .38f)
                    : theme.Rarity(session.Selected.rarity);

                for (int i = 0; i < view.tokens.Length; i++)
                {
                    var token = view.tokens[i];
                    float p = Mathf.Pow(
                        1 - Mathf.Clamp01(pressAges[i] / view.theme.pressSeconds),
                        1.3f
                    );
                    float intro = Ease((miniAge - i * .025f) / .15f);
                    float shake = token.outcome < 0
                        && !token.missed
                        && pressAges[i] < view.theme.feedbackSeconds ? Mathf.Cos(
                            pressAges[i] * 65
                        ) * 2.5f * (1 - pressAges[i] / view.theme.feedbackSeconds)
                        : 0;
                    token.visual.anchoredPosition =
                        new Vector2(
                            shake,
                            -p * theme.pressDepth - (1 - intro) * 7
                        );
                    token.face.sprite = token.highlight > .4f || token.outcome > 0
                        ? theme.activeKeycap
                        : theme.keycap;
                    token.face.color = token.outcome < 0 ? new Color(1, .80f, .72f) : Color.white;
                    token.arrow.color = token.outcome < 0 ? new Color(.65f, .25f, .13f)
                        : token.highlight > .4f
                        || token.outcome > 0 ? new Color(1, .95f, .79f)
                        : theme.ink;
                    arrowShadows[i].anchoredPosition = new Vector2(1, -7 + p * 4);
                    arrowShadows[i].localScale = new Vector3(1 - p * .04f, 1 - p * .10f, 1);
                    float glow = token.outcome > 0 && token.age < .22f
                        ? Mathf.Sin(Mathf.Clamp01(token.age / .22f) * Mathf.PI) * .27f
                        : token.highlight * .07f;
                    arrowGlows[i].color = new Color(1, .93f, .65f, glow);
                    pressAges[i] += dt;
                }

                for (int i = 0; i < legendSparkles.Length; i++)
                {
                    float alpha = session.Selected.rarity == FishRarity.Legendary ? Mathf.Max(
                        0,
                        1 - legendAge / .9f
                    ) * (.5f + .5f * Mathf.Sin(
                        legendAge * 7 + i
                    ))
                        : 0;
                    legendSparkles[i].color = new Color(1, .80f, .22f, alpha);
                }
            }

            bool result = presenter.ResultReveal > 0;

            if (presenter.ResultReveal > 0)
            {
                heldResultAge = presenter.ResultReveal;
            }
            else if (view.result.group.alpha <= .001f)
            {
                heldResultAge = 0;
            }

            float age = heldResultAge;
            Layer(resultHeader, resultHeaderGroup, resultHome, age, .035f, .23f);
            Layer(resultDecor, resultDecorGroup, resultDecorHome, age, .07f, .25f);
            previewGroup.alpha = Ease((age - .06f) / .25f);
            rarityGroup.alpha = Ease((age - .14f) / .17f);
            scoreGroup.alpha = Ease((age - .22f) / .17f);
            continueGroup.alpha = Ease((age - .28f) / .14f);

            if (result)
            {
                bool legend = session.LastResult.rarity == FishRarity.Legendary;
                resultEnamel.color = legend
                    ? new Color(1, .68f, .16f)
                    : theme.Rarity(session.LastResult.rarity);
                view.resultRarity.color = legend ? theme.ink : new Color(1, .95f, .80f);
                view.resultScore.color = theme.ink;
            }
        }

        void OnDisable()
        {
            if (miniHeaderGroup != null)
            {
                miniHeaderGroup.alpha = 0;
            }

            if (resultHeaderGroup != null)
            {
                resultHeaderGroup.alpha = 0;
            }
        }
    }
}
