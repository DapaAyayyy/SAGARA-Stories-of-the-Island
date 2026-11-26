using System;
using Sagara.Dermaga.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Sagara.DermagaV2
{
    /// <summary>
    /// Cozy 3D Steam-Ready Coastal Fishing UI (016_SteamPolish).
    /// Clean minimal-typography 3D minigame deck + living vertical water-tank gauge
    /// + centered cinematic 3D fish reward reveal with interactive 360° specimen turntable.
    /// Presentation-only: observes FishingSession without owning scoring or randomness.
    /// </summary>
    [DefaultExecutionOrder(260)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DermagaFishingHud : MonoBehaviour
    {
        private const float CompletionHoldSeconds = 0.38f;
        private const float ResultConfirmGuardSeconds = 0.34f;

        [Header("Fishing Scene Bindings")]
        [SerializeField] private FishingSession session;
        [SerializeField] private FishLandingPresentation landing;
        [SerializeField] private SessionFishVisuals fishVisuals;
        [SerializeField] private DermagaCleanupHud cleanupHud;

        [Header("016_SteamPolish Assets")]
        [SerializeField] private StyleSheet fishingStyle;
        [SerializeField] private Font displayFont;
        [SerializeField] private Font bodyFont;
        [SerializeField] private FontAsset displayFontAsset;
        [SerializeField] private FontAsset bodyFontAsset;
        [SerializeField] private Shader specimenPreviewShader;
        [SerializeField] private Texture2D rotateIcon;

        public static bool ResultOrFailureVisible { get; private set; }

        private UIDocument document;
        private VisualElement rootContainer;
        private DermagaJournalPreview fishPreview;

        // 1. Floating Status Pill (Casting / Waiting / Reeling / Flopping)
        private VisualElement statusWrap;
        private VisualElement statusBg;
        private VisualElement statusIcon;
        private Label statusLabel;
        private Label statusSub;
        private Button statusEscButton;
        private float statusVis;

        // 2. Bite Alert Banner + World-Space Beacon
        private VisualElement worldBite;
        private VisualElement worldBiteCanvas;
        private VisualElement biteBanner;
        private VisualElement biteBg;
        private Label biteTitle;
        private Label biteSub;
        private float biteVis;

        // 3. Catch Fail Notice
        private VisualElement failBanner;
        private VisualElement failBg;
        private float failVis;

        // 4. Box-Free Floating 3D Minigame HUD + Sleek Nautical Catch Gauge
        private VisualElement miniAssembly;
        private VisualElement miniScrim;
        private VisualElement headerDiamond;
        private Label rarityLabel;
        private readonly VisualElement[] stepDots = new VisualElement[5];

        private VisualElement keysLineBg;
        private VisualElement activeKeyRow;
        private VisualElement ghostKeyRow;
        private readonly KeySlot[] activeSlots = new KeySlot[4];
        private readonly GhostSlot[] ghostSlots = new GhostSlot[4];

        private VisualElement timerBarCanvas;
        private Button miniEscButton;

        private VisualElement gaugeWrap;
        private VisualElement gaugeTank;

        // 5. Centered Cinematic 3D Catch Reward Reveal (Opsi A)
        private VisualElement resultModal;
        private VisualElement resultAmbience;
        private VisualElement resultCenter;
        private VisualElement resultDiamondL;
        private VisualElement resultDiamondR;
        private Label resultKicker;
        private VisualElement resultStage;
        private VisualElement resultSpotlight;
        private Image resultSpecimenImage;
        private Label resultName;
        private Label resultSci;
        private Label resultRarityTag;
        private Label resultHabitatTag;
        private Button continueButton;
        private Label continueLabel;

        // Observed Fishing State & Animation Clocks
        private FishingState observedState = FishingState.Idle;
        private float stateAge;
        private float miniVis;
        private float miniAge;
        private float resolvedHoldAge;
        private float comboAge = 10f;
        private float deckLiftAge = 10f;
        private float deckShakeAge = 10f;

        private float resultVis;
        private float resultAge;
        private bool resultPopulated;
        private bool resultAcknowledged;
        private Color resultAccent = new Color(0.24f, 0.72f, 0.66f);
        private bool resultLegendary;

        // Minigame Round Tracking
        private FishingRound observedRound;
        private int observedCursor;
        private int observedGroup;
        private int streak;
        private int lastScore = -1;
        private int lastDots = -1;
        private float lastRemaining = 1.8f;
        private float gaugeFraction;
        private float gaugeStart;
        private float gaugeGoal;
        private float gaugeTweenAge = 1f;
        private bool thresholdReached;
        private float thresholdPulseAge = 10f;
        private float maxPulseAge = 10f;
        private readonly FishingDirection[] displayedDirs = new FishingDirection[4];

        private Func<Vector2, bool> pointerHook;

        private sealed class KeySlot
        {
            public VisualElement root;
            public VisualElement fx;
            public VisualElement cap;
            public VisualElement arrow;
            public Label pop;
            public FishingDirection direction;
            public bool active;
            public int outcome;
            public bool missed;
            public float pressAge = 10f;
            public float highlight;
        }

        private sealed class GhostSlot
        {
            public VisualElement cap;
            public VisualElement arrow;
            public FishingDirection direction;
            public int outcome;
        }

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();
            if (!session) session = GetComponentInParent<FishingSession>();
            if (!landing) landing = GetComponentInParent<FishLandingPresentation>();
            if (!fishVisuals) fishVisuals = GetComponentInParent<SessionFishVisuals>();
            if (!cleanupHud) cleanupHud = FindFirstObjectByType<DermagaCleanupHud>();

            if (specimenPreviewShader)
            {
                fishPreview = new DermagaJournalPreview(specimenPreviewShader, new Vector3(1820f, 1820f, 1820f));
            }

            BuildUi();
            pointerHook = ContainsPointer;
            FishingHud.PointerOverride = pointerHook;
            ResetRuntimeState();
        }

        private void OnDisable()
        {
            ResultOrFailureVisible = false;
            if (FishingHud.PointerOverride == pointerHook)
            {
                FishingHud.PointerOverride = null;
            }
            fishPreview?.Dispose();
            fishPreview = null;
            rootContainer?.RemoveFromHierarchy();
        }

        private void ResetRuntimeState()
        {
            observedState = session ? session.State : FishingState.Idle;
            stateAge = 0f;
            statusVis = biteVis = failVis = miniVis = resultVis = 0f;
            miniAge = resolvedHoldAge = resultAge = 0f;
            comboAge = deckLiftAge = deckShakeAge = 10f;
            resultPopulated = resultAcknowledged = false;
            observedRound = null;
            observedCursor = observedGroup = streak = 0;
            lastScore = lastDots = -1;
            gaugeFraction = gaugeStart = gaugeGoal = 0f;
            thresholdReached = false;
            thresholdPulseAge = maxPulseAge = 10f;

            for (int i = 0; i < 4; i++)
            {
                ResetKeySlot(activeSlots[i]);
            }

            SetVisible(statusWrap, false);
            SetVisible(worldBite, false);
            SetVisible(biteBanner, false);
            SetVisible(failBanner, false);
            SetVisible(miniAssembly, false);
            SetVisible(gaugeWrap, false);
            SetVisible(resultModal, false);
        }

        private void BuildUi()
        {
            rootContainer = new VisualElement { name = "DermagaFishingHud", pickingMode = PickingMode.Ignore };
            rootContainer.StretchToParentSize();
            rootContainer.AddToClassList("fish-root");
            if (fishingStyle)
            {
                rootContainer.styleSheets.Add(fishingStyle);
            }
            ApplyBodyFont(rootContainer);

            BuildStatusPill();
            BuildBiteAlert();
            BuildFailBanner();
            BuildMinigameDeck();
            BuildCenteredResultModal();
        }

        private void BuildStatusPill()
        {
            statusWrap = MakeEl(rootContainer, "fish-status-wrap", ignorePicking: true);
            statusBg = MakeEl(statusWrap, "fish-status-bg", ignorePicking: true);
            statusBg.generateVisualContent += DrawStatusRibbonBg;

            statusIcon = MakeEl(statusWrap, "fish-status-icon", ignorePicking: true);
            statusIcon.generateVisualContent += DrawBobberIcon;

            var textCol = MakeEl(statusWrap, "fish-status-text-col", ignorePicking: true);
            statusLabel = MakeLabel(textCol, "Melempar kail...", "fish-status-label", display: true);
            statusSub = MakeLabel(textCol, "Perhatikan riak air di dermaga", "fish-status-sub", display: false);

            statusEscButton = MakeEscButton(statusWrap);
        }

        private void BuildBiteAlert()
        {
            worldBite = MakeEl(rootContainer, "fish-world-bite", ignorePicking: true);
            worldBiteCanvas = MakeEl(worldBite, "fish-world-bite-canvas", ignorePicking: true);
            worldBiteCanvas.generateVisualContent += DrawWorldBiteBeacon;

            biteBanner = MakeEl(rootContainer, "fish-bite-banner", ignorePicking: true);
            biteBg = MakeEl(biteBanner, "fish-bite-bg", ignorePicking: true);
            biteBg.generateVisualContent += DrawBiteBannerBg;
            biteTitle = MakeLabel(biteBanner, "Ikan Menggigit!", "fish-bite-title", display: true);
            biteSub = MakeLabel(biteBanner, "Bersiap!", "fish-bite-sub", display: true);
        }

        private void BuildFailBanner()
        {
            failBanner = MakeEl(rootContainer, "fish-fail-banner", ignorePicking: true);
            failBg = MakeEl(failBanner, "fish-fail-bg", ignorePicking: true);
            failBg.generateVisualContent += DrawFailBannerBg;
            MakeLabel(failBanner, "Ikan Berhasil Lepas", "fish-fail-title", display: true);
            MakeLabel(failBanner, "Tenang, laut masih menyimpan cerita.", "fish-fail-sub", display: false);
        }

        private void BuildMinigameDeck()
        {
            miniAssembly = MakeEl(rootContainer, "fish-mini-assembly", ignorePicking: true);

            // Soft feathered ocean-mist backing (zero hard box edges — blends into the 3D world!)
            miniScrim = MakeEl(miniAssembly, "fish-mini-scrim", ignorePicking: true);
            miniScrim.generateVisualContent += DrawMiniScrim;

            var core = MakeEl(miniAssembly, "fish-deck-core", ignorePicking: true);

            // Whisper-clean floating header: Diamond + Rarity + 5 floating combination pips
            var header = MakeEl(core, "fish-deck-header", ignorePicking: true);
            headerDiamond = MakeEl(header, "fish-header-diamond", ignorePicking: true);
            headerDiamond.generateVisualContent += ctx => DrawGoldenDiamondIcon(ctx, 5.8f);
            rarityLabel = MakeLabel(header, "Common", "fish-rarity-label", display: true);

            var stepRow = MakeEl(header, "fish-step-row", ignorePicking: true);
            for (int i = 0; i < 5; i++)
            {
                stepDots[i] = MakeEl(stepRow, "fish-step-dot", ignorePicking: true);
            }

            // 4 Floating 3D Keycaps strung along a delicate golden fishing-line filigree
            var body = MakeEl(core, "fish-deck-body", ignorePicking: true);
            keysLineBg = MakeEl(body, "fish-keys-line-bg", ignorePicking: true);
            keysLineBg.generateVisualContent += DrawKeysLineBg;
            var keysStage = MakeEl(body, "fish-keys-stage", ignorePicking: true);

            ghostKeyRow = MakeEl(keysStage, "fish-keys-ghost", ignorePicking: true);
            ghostKeyRow.style.opacity = 0f;
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var slot = new GhostSlot();
                var wrap = MakeEl(ghostKeyRow, "fish-key-slot", ignorePicking: true);
                slot.cap = MakeEl(wrap, "fish-key-cap", ignorePicking: true);
                slot.arrow = MakeEl(slot.cap, "fish-key-arrow", ignorePicking: true);
                slot.arrow.generateVisualContent += ctx => DrawGhostArrow(ctx, idx);
                ghostSlots[i] = slot;
            }

            activeKeyRow = MakeEl(keysStage, "fish-keys-row", ignorePicking: true);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var slot = new KeySlot();
                slot.root = MakeEl(activeKeyRow, "fish-key-slot", ignorePicking: true);
                slot.fx = MakeEl(slot.root, "fish-key-fx", ignorePicking: true);
                slot.fx.generateVisualContent += ctx => DrawKeySlotFx(ctx, idx);
                slot.cap = MakeEl(slot.root, "fish-key-cap", ignorePicking: true);
                slot.arrow = MakeEl(slot.cap, "fish-key-arrow", ignorePicking: true);
                slot.arrow.generateVisualContent += ctx => DrawActiveArrow(ctx, idx);
                slot.pop = MakeLabel(slot.root, "", "fish-key-pop", display: true);
                activeSlots[i] = slot;
            }

            // Sleek Floating Glowing Countdown Ribbon directly under the 4 keys
            var track = MakeEl(core, "fish-timer-track", ignorePicking: true);
            timerBarCanvas = MakeEl(track, "fish-timer-wave", ignorePicking: true);
            timerBarCanvas.generateVisualContent += DrawCountdownTimerBar;

            // Subtle floating Esc micro-pill
            miniEscButton = MakeFloatingEscPill(core);

            // Independent Majestic Bottom-Right Nautical Catch Gauge (keeps 4 keys 100% dead-center!)
            gaugeWrap = MakeEl(rootContainer, "fish-gauge-wrap", ignorePicking: true);
            gaugeTank = MakeEl(gaugeWrap, "fish-gauge-tank", ignorePicking: true);
            gaugeTank.generateVisualContent += DrawWaterTankGauge;
        }

        private Button MakeFloatingEscPill(VisualElement parent)
        {
            var btn = new Button(() =>
            {
                DermagaUiAudio.PlayClick();
                if (session && session.Busy)
                {
                    session.Cancel();
                }
            }) { text = "", focusable = false };
            btn.AddToClassList("fish-esc-pill");
            AttachTactileFeedback(btn);
            parent.Add(btn);
            MakeLabel(btn, "Esc", "fish-esc-pill-cap", display: true);
            MakeLabel(btn, "Batal", "fish-esc-pill-label", display: true);
            return btn;
        }

        private void BuildCenteredResultModal()
        {
            resultModal = MakeEl(rootContainer, "fish-result-modal", ignorePicking: false);
            resultAmbience = MakeEl(resultModal, "fish-result-ambience", ignorePicking: true);
            resultAmbience.generateVisualContent += DrawCenteredRewardAmbience;

            resultCenter = MakeEl(resultModal, "fish-result-center", ignorePicking: true);

            // Minimal Golden Top Kicker: —— ◆ TANGKAPAN BERHASIL ◆ ——
            var kickerRow = MakeEl(resultCenter, "fish-result-kicker-row", ignorePicking: true);
            MakeEl(kickerRow, "fish-result-kicker-line", ignorePicking: true);
            resultDiamondL = MakeEl(kickerRow, "fish-result-diamond", ignorePicking: true);
            resultDiamondL.generateVisualContent += ctx => DrawGoldenDiamondIcon(ctx, 5.2f);
            resultKicker = MakeLabel(kickerRow, "TANGKAPAN BERHASIL", "fish-result-kicker", display: true);
            resultDiamondR = MakeEl(kickerRow, "fish-result-diamond", ignorePicking: true);
            resultDiamondR.generateVisualContent += ctx => DrawGoldenDiamondIcon(ctx, 5.2f);
            MakeEl(kickerRow, "fish-result-kicker-line", ignorePicking: true);

            // Hero Floating 3D Fish Turntable Stage
            resultStage = MakeEl(resultCenter, "fish-result-stage", ignorePicking: false);
            resultSpotlight = MakeEl(resultStage, "fish-result-spotlight", ignorePicking: true);
            resultSpotlight.generateVisualContent += DrawHeroFishSpotlight;

            resultSpecimenImage = new Image { scaleMode = ScaleMode.ScaleToFit };
            resultSpecimenImage.AddToClassList("fish-result-specimen");
            resultStage.Add(resultSpecimenImage);

            resultSpecimenImage.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0) resultSpecimenImage.CapturePointer(evt.pointerId);
            });
            resultSpecimenImage.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (resultSpecimenImage.HasPointerCapture(evt.pointerId) && fishPreview != null)
                {
                    fishPreview.Rotate(evt.deltaPosition.x * 0.9f);
                }
            });
            resultSpecimenImage.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (resultSpecimenImage.HasPointerCapture(evt.pointerId))
                {
                    resultSpecimenImage.ReleasePointer(evt.pointerId);
                }
            });

            var dragPill = MakeEl(resultStage, "fish-result-drag-pill", ignorePicking: true);
            if (rotateIcon)
            {
                var rotEl = MakeEl(dragPill, "fish-result-drag-icon", ignorePicking: true);
                rotEl.style.backgroundImage = new StyleBackground(rotateIcon);
            }
            MakeLabel(dragPill, "Seret 360°", "fish-result-drag-text", display: true);

            // Minimal Info Directly Under 3D Hero Fish
            resultName = MakeLabel(resultCenter, "Ikan Badut", "fish-result-name", display: true);
            resultSci = MakeLabel(resultCenter, "Amphiprion ocellaris", "fish-result-sci", display: false);

            var tags = MakeEl(resultCenter, "fish-result-tags", ignorePicking: true);
            resultRarityTag = MakeLabel(tags, "Umum", "fish-result-tag", display: true);
            resultHabitatTag = MakeLabel(tags, "Dermaga Pesisir", "fish-result-tag fish-result-tag-habitat", display: true);

            // Tactile 3D Continue Button
            continueButton = new Button(AcknowledgeResult) { text = "", focusable = false };
            continueButton.AddToClassList("fish-continue-btn");
            AttachTactileFeedback(continueButton);
            resultCenter.Add(continueButton);
            continueLabel = MakeLabel(continueButton, "Lanjutkan", "fish-continue-label", display: true);
            MakeLabel(continueButton, "Enter", "fish-continue-key", display: true);
        }

        private Button MakeEscButton(VisualElement parent)
        {
            var btn = new Button(() =>
            {
                DermagaUiAudio.PlayClick();
                if (session && session.Busy)
                {
                    session.Cancel();
                }
            }) { text = "", focusable = false };
            btn.AddToClassList("fish-mini-esc");
            AttachTactileFeedback(btn);
            parent.Add(btn);
            MakeLabel(btn, "Esc", "fish-mini-esc-cap", display: true);
            MakeLabel(btn, "Batal", "fish-mini-esc-label", display: true);
            return btn;
        }

        private bool ContainsPointer(Vector2 screenPoint)
        {
            if (cleanupHud && cleanupHud.IsPointerOverAction(screenPoint))
            {
                return true;
            }
            if (rootContainer == null || rootContainer.panel == null)
            {
                return false;
            }
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                rootContainer.panel,
                new Vector2(screenPoint.x, Screen.height - screenPoint.y)
            );
            if (resultVis > 0.02f) return true;
            if (miniVis > 0.02f && (miniAssembly.worldBound.Contains(panelPos) || gaugeWrap.worldBound.Contains(panelPos))) return true;
            if (statusVis > 0.02f && statusWrap.worldBound.Contains(panelPos)) return true;
            if (biteVis > 0.02f && biteBanner.worldBound.Contains(panelPos)) return true;
            if (failVis > 0.02f && failBanner.worldBound.Contains(panelPos)) return true;
            return false;
        }

        private void AcknowledgeResult()
        {
            if (resultAcknowledged || landing == null || !landing.ReadyForConfirmation || resultAge < ResultConfirmGuardSeconds)
            {
                return;
            }
            resultAcknowledged = true;
            DermagaUiAudio.PlayClick();
            landing.Acknowledge();
        }

        private void LateUpdate()
        {
            if (document == null || document.rootVisualElement == null || session == null)
            {
                return;
            }
            if (rootContainer.parent != document.rootVisualElement)
            {
                document.rootVisualElement.pickingMode = PickingMode.Ignore;
                document.rootVisualElement.StretchToParentSize();
                document.rootVisualElement.Add(rootContainer);
            }

            float dt = Time.unscaledDeltaTime;
            stateAge += dt;
            comboAge += dt;
            deckLiftAge += dt;
            deckShakeAge += dt;
            thresholdPulseAge += dt;
            maxPulseAge += dt;

            FishingState state = session.State;
            if (state != observedState)
            {
                OnStateTransition(observedState, state);
                observedState = state;
            }

            bool readyResult = state == FishingState.CatchSuccess && landing != null && landing.ReadyForConfirmation;
            bool isResolved = state == FishingState.CatchSuccess || state == FishingState.CatchFail;

            if (isResolved && observedRound != null)
            {
                resolvedHoldAge += dt;
            }
            else if (state != FishingState.Minigame)
            {
                resolvedHoldAge = 0f;
            }

            bool wantMini = state == FishingState.Minigame
                || (isResolved && observedRound != null && resolvedHoldAge < CompletionHoldSeconds);
            bool wantStatus = !wantMini && (state == FishingState.Casting
                || state == FishingState.WaitingForBite
                || (state == FishingState.CatchSuccess && !readyResult));
            bool wantBite = state == FishingState.BiteDetected;
            bool wantFail = state == FishingState.CatchFail && !wantMini && stateAge > 0.14f;

            UpdateStatusPill(wantStatus, state, dt);
            UpdateBiteAlert(wantBite, dt);
            UpdateFailBanner(wantFail, dt);
            UpdateMinigame(wantMini, state == FishingState.Minigame, dt);
            UpdateResultModal(readyResult, dt);

            ResultOrFailureVisible = resultVis > 0.01f || failVis > 0.01f;
        }

        private void OnStateTransition(FishingState from, FishingState to)
        {
            stateAge = 0f;
            lastDots = -1;

            if (to == FishingState.Casting)
            {
                resultPopulated = false;
                resultAcknowledged = false;
                resultAge = 0f;
                observedRound = null;
                observedCursor = 0;
                observedGroup = 0;
                streak = 0;
                lastScore = -1;
                gaugeFraction = gaugeStart = gaugeGoal = 0f;
                thresholdReached = false;
                thresholdPulseAge = maxPulseAge = 10f;
                for (int i = 0; i < 4; i++) ResetKeySlot(activeSlots[i]);
            }
            else if (to == FishingState.BiteDetected)
            {
                DermagaUiAudio.PlayFishBite();
            }
            else if (to == FishingState.Minigame)
            {
                InitMinigameRound();
            }
            else if (to == FishingState.Idle)
            {
                observedRound = null;
                resolvedHoldAge = 0f;
            }
        }

        private void InitMinigameRound()
        {
            observedRound = session.Round;
            observedCursor = 0;
            observedGroup = 0;
            streak = 0;
            miniAge = 0f;
            comboAge = 10f;
            resolvedHoldAge = 0f;
            lastScore = 0;
            gaugeFraction = gaugeStart = gaugeGoal = 0f;
            gaugeTweenAge = 1f;
            thresholdReached = false;
            thresholdPulseAge = maxPulseAge = 10f;

            ghostKeyRow.style.opacity = 0f;
            for (int i = 0; i < 4; i++) ResetKeySlot(activeSlots[i]);

            rarityLabel.text = RarityIndo(session.Selected.rarity).ToUpperInvariant();
            rarityLabel.style.color = RarityBright(session.Selected.rarity);
            UpdateStepDots(0);
        }

        private void UpdateStatusPill(bool want, FishingState state, float dt)
        {
            statusVis = Mathf.MoveTowards(statusVis, want ? 1f : 0f, dt * (want ? 7.2f : 9.5f));
            bool show = statusVis > 0.01f;
            SetVisible(statusWrap, show);
            if (!show) return;

            float e = Smooth(statusVis);
            statusWrap.style.opacity = e;
            statusWrap.style.translate = new Translate(0f, (1f - e) * 14f, 0f);
            float sc = Mathf.Lerp(0.95f, 1f, e);
            statusWrap.style.scale = new Scale(new Vector3(sc, sc, 1f));

            string baseCaption = state == FishingState.Casting ? "Melempar kail"
                : state == FishingState.WaitingForBite ? "Menunggu gigitan"
                : (landing != null && landing.Stage == "Flopping") ? "Tangkapan di dermaga!"
                : "Menarik tangkapan";

            string subCaption = state == FishingState.Casting ? "Arahkan pandangan ke pelampung di air"
                : state == FishingState.WaitingForBite ? "Tetap tenang hingga ikan menyambar"
                : "Ikan sedang ditarik ke tepi dermaga";

            int dot = (int)(Time.unscaledTime * 2.4f) % 4;
            if (dot != lastDots || !statusLabel.text.StartsWith(baseCaption, StringComparison.Ordinal))
            {
                lastDots = dot;
                statusLabel.text = baseCaption.EndsWith("!", StringComparison.Ordinal)
                    ? baseCaption
                    : baseCaption + new string('.', dot);
                statusSub.text = subCaption;
            }

            SetVisible(statusEscButton, state == FishingState.Casting || state == FishingState.WaitingForBite);
            statusBg.MarkDirtyRepaint();
            statusIcon.MarkDirtyRepaint();
        }

        private void UpdateBiteAlert(bool want, float dt)
        {
            biteVis = Mathf.MoveTowards(biteVis, want ? 1f : 0f, dt * (want ? 9.5f : 8.5f));
            bool show = biteVis > 0.01f;
            SetVisible(biteBanner, show);
            SetVisible(worldBite, show && session.ActiveSpot != null && Camera.main != null);
            if (!show) return;

            float e = Smooth(biteVis);
            float pop = want ? Mathf.Sin(Mathf.Clamp01(stateAge / 0.24f) * Mathf.PI) * 0.06f : 0f;
            float sc = Mathf.Lerp(0.90f, 1f, e) + pop;
            biteBanner.style.opacity = e;
            biteBanner.style.scale = new Scale(new Vector3(sc, sc, 1f));
            biteBanner.style.translate = new Translate(0f, (1f - e) * 14f, 0f);

            if (session.Selected != null)
            {
                biteTitle.text = "Ikan Menggigit!";
                biteSub.text = $"{RarityIndo(session.Selected.rarity)}  ·  Bersiap!";
                biteSub.style.color = RarityBright(session.Selected.rarity);
            }
            biteBg.MarkDirtyRepaint();

            if (worldBite.style.display == DisplayStyle.Flex && Camera.main != null && session.ActiveSpot != null)
            {
                Vector3 vp = Camera.main.WorldToViewportPoint(session.ActiveSpot.CastPosition + Vector3.up * 0.92f);
                float w = rootContainer.layout.width;
                float h = rootContainer.layout.height;
                if (vp.z > 0f && w > 1f && h > 1f)
                {
                    float bounce = Mathf.Sin(Mathf.Clamp01(stateAge / 0.26f) * Mathf.PI) * 0.18f;
                    float wScale = Mathf.Lerp(0.75f, 1f, e) + bounce;
                    worldBite.style.left = vp.x * w - 42f;
                    worldBite.style.top = (1f - vp.y) * h - 42f;
                    worldBite.style.opacity = e;
                    worldBite.style.scale = new Scale(new Vector3(wScale, wScale, 1f));
                    worldBiteCanvas.MarkDirtyRepaint();
                }
                else
                {
                    SetVisible(worldBite, false);
                }
            }
        }

        private void UpdateFailBanner(bool want, float dt)
        {
            failVis = Mathf.MoveTowards(failVis, want ? 1f : 0f, dt * (want ? 6.5f : 7.5f));
            bool show = failVis > 0.01f;
            SetVisible(failBanner, show);
            if (!show) return;

            float e = Smooth(failVis);
            failBanner.style.opacity = e;
            failBanner.style.translate = new Translate(0f, (1f - e) * 12f, 0f);
            failBg.MarkDirtyRepaint();
        }

        private void UpdateMinigame(bool want, bool activeInput, float dt)
        {
            miniVis = Mathf.MoveTowards(miniVis, want ? 1f : 0f, dt * (want ? 7.0f : 8.5f));
            bool show = miniVis > 0.01f;
            SetVisible(miniAssembly, show);
            SetVisible(gaugeWrap, show);
            if (!show) return;

            miniAge += dt;
            float e = Smooth(miniVis);
            float introLift = (1f - e) * 20f;

            // Cozy living motion: subtle ocean sway + micro-lift on hit + damped recoil on miss
            float breatheY = Mathf.Sin(Time.unscaledTime * 2.2f) * 1.3f;
            float hitLift = deckLiftAge < 0.22f ? -Mathf.Sin((deckLiftAge / 0.22f) * Mathf.PI) * 3.4f : 0f;
            float missShakeX = deckShakeAge < 0.24f
                ? Mathf.Sin(deckShakeAge * 68f) * 4.0f * (1f - deckShakeAge / 0.24f)
                : 0f;

            miniAssembly.style.opacity = e;
            miniAssembly.style.translate = new Translate(missShakeX, introLift + breatheY + hitLift, 0f);
            float sc = Mathf.Lerp(0.94f, 1f, e);
            miniAssembly.style.scale = new Scale(new Vector3(sc, sc, 1f));

            // Independent bottom-right gauge glides smoothly in from the right edge & pulses softly on hit/threshold
            float gaugeSlideX = (1f - e) * 34f;
            float gaugeBreatheY = Mathf.Sin(Time.unscaledTime * 2.0f + 1.1f) * 1.6f;
            float gaugeHitPop = deckLiftAge < 0.22f ? Mathf.Sin((deckLiftAge / 0.22f) * Mathf.PI) * 0.035f : 0f;
            float gaugeThreshPop = thresholdPulseAge < 0.45f ? Mathf.Sin((thresholdPulseAge / 0.45f) * Mathf.PI) * 0.06f : 0f;
            float gSc = Mathf.Lerp(0.92f, 1f, e) + gaugeHitPop + gaugeThreshPop;
            gaugeWrap.style.opacity = e;
            gaugeWrap.style.translate = new Translate(gaugeSlideX, gaugeBreatheY, 0f);
            gaugeWrap.style.scale = new Scale(new Vector3(gSc, gSc, 1f));

            var round = session.Round ?? observedRound;
            if (round == null || session.Selected == null) return;

            if (round.Cursor != observedCursor)
            {
                int prevIdx = Mathf.Clamp(round.Cursor - 1, 0, 19);
                int slotIdx = prevIdx % 4;
                bool success = round.outcomes[prevIdx] > 0;
                bool missed = !success && lastRemaining < Mathf.Max(0.06f, Time.deltaTime * 2.2f);

                var slot = activeSlots[slotIdx];
                slot.outcome = success ? 1 : -1;
                slot.missed = missed;
                slot.pressAge = 0f;

                if (success)
                {
                    streak++;
                    deckLiftAge = 0f;
                    DermagaUiAudio.PlayFishHit(streak);
                }
                else
                {
                    streak = 0;
                    deckShakeAge = 0f;
                    DermagaUiAudio.PlayFishMiss();
                }

                observedCursor = round.Cursor;
            }

            int currentGroup = Mathf.Min(4, round.Cursor / 4);
            if (currentGroup != observedGroup && activeInput)
            {
                for (int i = 0; i < 4; i++)
                {
                    ghostSlots[i].direction = displayedDirs[i];
                    ghostSlots[i].outcome = activeSlots[i].outcome;
                    ghostSlots[i].cap.EnableInClassList("fish-key-cap-hit", activeSlots[i].outcome > 0);
                    ghostSlots[i].cap.EnableInClassList("fish-key-cap-miss", activeSlots[i].outcome < 0);
                    ghostSlots[i].arrow.MarkDirtyRepaint();
                    ResetKeySlot(activeSlots[i]);
                }
                observedGroup = currentGroup;
                comboAge = 0f;
                UpdateStepDots(currentGroup);
            }

            // Smooth combo slide-out / pop-in
            float comboT = Smooth(Mathf.Clamp01(comboAge / 0.22f));
            ghostKeyRow.style.opacity = comboAge < 0.22f ? (1f - comboT) * 0.85f : 0f;
            ghostKeyRow.style.translate = new Translate(-comboT * 26f, 0f, 0f);
            activeKeyRow.style.opacity = Mathf.Lerp(0.45f, 1f, comboT);
            activeKeyRow.style.translate = new Translate((1f - comboT) * 24f, 0f, 0f);

            // Animate each tactile 3D keycap
            for (int i = 0; i < 4; i++)
            {
                int globalSlot = observedGroup * 4 + i;
                var slot = activeSlots[i];
                slot.direction = round.sequence[globalSlot];
                displayedDirs[i] = slot.direction;
                if (round.outcomes[globalSlot] != 0 && slot.outcome == 0)
                {
                    slot.outcome = round.outcomes[globalSlot];
                }

                slot.active = activeInput && globalSlot == round.Cursor;
                slot.pressAge += dt;
                slot.highlight = Mathf.MoveTowards(slot.highlight, slot.active ? 1f : 0f, dt * 11f);

                slot.cap.EnableInClassList("fish-key-cap-active", slot.active && slot.outcome == 0);
                slot.cap.EnableInClassList("fish-key-cap-hit", slot.outcome > 0);
                slot.cap.EnableInClassList("fish-key-cap-miss", slot.outcome < 0);

                float stagger = Smooth(Mathf.Clamp01((Mathf.Min(miniAge, comboAge) - i * 0.032f) / 0.16f));
                float activeLift = -slot.highlight * 6.5f;
                float pressDrop = slot.pressAge < 0.18f
                    ? Mathf.Sin((slot.pressAge / 0.18f) * Mathf.PI) * 5.5f
                    : (slot.outcome != 0 ? 2.5f : 0f);
                float shakeX = slot.outcome < 0 && !slot.missed && slot.pressAge < 0.26f
                    ? Mathf.Sin(slot.pressAge * 72f) * 5.0f * (1f - slot.pressAge / 0.26f)
                    : 0f;
                // Distinct visual hierarchy: active keycap pops boldly to 1.13x while waiting keys sit calmly at 0.93x
                float baseScale = slot.outcome != 0 ? 0.96f : Mathf.Lerp(0.93f, 1.13f, slot.highlight);
                float pressSquash = slot.pressAge < 0.16f
                    ? Mathf.Sin((slot.pressAge / 0.16f) * Mathf.PI) * 0.09f
                    : 0f;
                float s = Mathf.Lerp(0.84f, 1f, stagger) * (baseScale - pressSquash);

                slot.cap.style.translate = new Translate(shakeX, activeLift + pressDrop + (1f - stagger) * 8f, 0f);
                slot.cap.style.scale = new Scale(new Vector3(s, s, 1f));

                // Minimal crisp floating feedback badge below keycap
                if (slot.outcome != 0 && slot.pressAge < 0.90f)
                {
                    float popT = Mathf.Clamp01(slot.pressAge / 0.28f);
                    float fadeOut = 1f - Mathf.Clamp01((slot.pressAge - 0.55f) / 0.35f);
                    slot.pop.text = slot.outcome > 0 ? "+5" : "×";
                    slot.pop.style.color = slot.outcome > 0
                        ? new Color(0.56f, 1.00f, 0.84f)
                        : new Color(1.00f, 0.62f, 0.54f);
                    slot.pop.style.opacity = Smooth(popT) * fadeOut;
                    slot.pop.style.translate = new Translate(0f, Mathf.Lerp(5f, 0f, Smooth(popT)), 0f);
                }
                else
                {
                    slot.pop.text = "";
                    slot.pop.style.opacity = 0f;
                }

                slot.fx.MarkDirtyRepaint();
                slot.arrow.MarkDirtyRepaint();
            }

            // Animate step dot pulse
            for (int i = 0; i < 5; i++)
            {
                float dotScale = i == observedGroup
                    ? 1.24f + Mathf.Sin(Time.unscaledTime * 5f) * 0.10f
                    : 1f;
                stepDots[i].style.scale = new Scale(new Vector3(dotScale, dotScale, 1f));
            }

            // Bottom Live Countdown Ribbon
            lastRemaining = activeInput ? session.ArrowRemaining : 0f;

            // Smooth Living Floating Nautical Gauge
            int authScore = Mathf.Clamp(round.Score, 0, 100);
            if (authScore != lastScore)
            {
                gaugeStart = gaugeFraction;
                gaugeGoal = authScore / 100f;
                gaugeTweenAge = 0f;
                lastScore = authScore;
            }

            gaugeTweenAge += dt;
            float gt = Smooth(Mathf.Clamp01(gaugeTweenAge / 0.22f));
            gaugeFraction = Mathf.Lerp(gaugeStart, gaugeGoal, gt);

            int targetScore = session.Selected.minimumScore;
            if (!thresholdReached && authScore >= targetScore && gaugeFraction + 0.001f >= targetScore / 100f)
            {
                thresholdReached = true;
                thresholdPulseAge = 0f;
                DermagaUiAudio.PlayFishThreshold();
            }
            if (authScore == 100 && maxPulseAge > 5f)
            {
                maxPulseAge = 0f;
            }

            miniScrim.MarkDirtyRepaint();
            headerDiamond.MarkDirtyRepaint();
            keysLineBg.MarkDirtyRepaint();
            timerBarCanvas.MarkDirtyRepaint();
            gaugeTank.MarkDirtyRepaint();
        }

        private void UpdateResultModal(bool ready, float dt)
        {
            if (ready && !resultPopulated)
            {
                PopulateResultModal();
                resultPopulated = true;
                resultAge = 0f;
                resultAcknowledged = false;
                DermagaUiAudio.PlayFishCatch(resultLegendary);
            }
            else if (!ready)
            {
                resultPopulated = false;
            }

            if (ready)
            {
                resultAge += dt;
            }

            resultVis = Mathf.MoveTowards(resultVis, ready ? 1f : 0f, dt * (ready ? 4.8f : 7.5f));
            bool show = resultVis > 0.01f;
            SetVisible(resultModal, show);
            if (!show)
            {
                fishPreview?.Clear();
                return;
            }

            float e = Smooth(resultVis);
            resultModal.style.opacity = e;

            // Cinematic staggered reveal: fish floats in first, then crisp name/tags/button settle gently
            float fishEase = Smooth(Mathf.Clamp01(resultAge / 0.34f));
            float fishBounce = ready ? Mathf.Sin(Mathf.Clamp01(resultAge / 0.36f) * Mathf.PI) * 0.045f : 0f;
            float fishScale = Mathf.Lerp(0.84f, 1.0f, fishEase) + fishBounce;
            resultStage.style.scale = new Scale(new Vector3(fishScale, fishScale, 1f));
            resultStage.style.translate = new Translate(0f, (1f - fishEase) * 14f, 0f);

            float infoEase = Smooth(Mathf.Clamp01((resultAge - 0.12f) / 0.26f));
            resultName.style.opacity = infoEase;
            resultName.style.translate = new Translate(0f, (1f - infoEase) * 10f, 0f);
            resultSci.style.opacity = infoEase;

            bool canAck = ready && !resultAcknowledged && resultAge >= ResultConfirmGuardSeconds;
            continueButton.SetEnabled(canAck);
            float btnEase = Smooth(Mathf.Clamp01((resultAge - 0.22f) / 0.22f));
            continueButton.style.opacity = btnEase;
            continueButton.style.translate = new Translate(0f, (1f - btnEase) * 8f, 0f);

            fishPreview?.Update();
            resultAmbience.MarkDirtyRepaint();
            resultSpotlight.MarkDirtyRepaint();
            resultDiamondL.MarkDirtyRepaint();
            resultDiamondR.MarkDirtyRepaint();

            if (canAck && Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.eKey.wasPressedThisFrame
                    || Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                AcknowledgeResult();
            }
        }

        private void PopulateResultModal()
        {
            var res = session.LastResult;
            var entry = fishVisuals && fishVisuals.catalog ? fishVisuals.catalog.Find(session.Selected) : null;
            FishRarity rarity = res != null ? res.rarity : FishRarity.Common;
            string fishId = res != null ? res.fishId : string.Empty;
            string fallback = entry != null ? entry.displayName : "Ikan Pesisir";

            GetFishMeta(fishId, fallback, out string indoName, out string sci, out string habitat);
            resultLegendary = rarity == FishRarity.Legendary;
            resultAccent = RarityBright(rarity);

            resultKicker.text = resultLegendary ? "TANGKAPAN LEGENDARIS" : "TANGKAPAN BERHASIL";
            resultName.text = indoName;
            resultSci.text = sci;
            resultHabitatTag.text = habitat;
            ApplyRarityStyleDark(resultRarityTag, rarity);

            continueButton.EnableInClassList("fish-continue-btn-legendary", resultLegendary);
            continueLabel.text = "Lanjutkan";

            if (fishPreview != null && entry != null && entry.prefab != null)
            {
                RenderTexture rt = fishPreview.Show(entry.prefab, null, resultAccent, discovered: true);
                resultSpecimenImage.image = rt;
            }
            else if (entry != null && entry.portrait != null)
            {
                resultSpecimenImage.image = entry.portrait;
            }
        }

        // =====================================================================
        // PAINTER2D VISUALS (COZY 3D STEAM POLISH)
        // =====================================================================

        private void DrawStatusRibbonBg(MeshGenerationContext ctx)
        {
            DrawFeatheredRibbon(ctx, new Color32(14, 38, 46, 224), new Color(0.98f, 0.84f, 0.46f, 0.72f));
        }

        private void DrawFailBannerBg(MeshGenerationContext ctx)
        {
            DrawFeatheredRibbon(ctx, new Color32(22, 38, 46, 228), new Color(0.88f, 0.64f, 0.50f, 0.72f));
        }

        private void DrawBiteBannerBg(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 40f || r.height < 20f) return;

            Color accent = session && session.Selected != null
                ? RarityBright(session.Selected.rarity)
                : new Color(1f, 0.84f, 0.42f);

            DrawFeatheredRibbon(ctx, new Color32(12, 36, 46, 236), new Color(accent.r, accent.g, accent.b, 0.90f));

            var p = ctx.painter2D;
            float cx = r.width * 0.5f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);

            p.fillColor = new Color(accent.r, accent.g, accent.b, 0.28f + 0.20f * pulse);
            DrawDiamond(p, new Vector2(cx, 1f), 12f + pulse * 2.5f);
            p.Fill();

            p.fillColor = accent;
            p.strokeColor = new Color(0.22f, 0.14f, 0.05f, 0.92f);
            p.lineWidth = 1.4f;
            DrawDiamond(p, new Vector2(cx, 1f), 7.8f);
            p.Fill();
            p.Stroke();

            p.fillColor = new Color(1f, 0.99f, 0.92f, 1f);
            DrawDiamond(p, new Vector2(cx, 1f), 3.4f);
            p.Fill();
        }

        private static void DrawFeatheredRibbon(MeshGenerationContext ctx, Color32 solid, Color borderCol)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 44f || r.height < 16f) return;
            float fade = Mathf.Min(68f, r.width * 0.18f);
            var mesh = ctx.Allocate(8, 18);
            var clear = new Color32(solid.r, solid.g, solid.b, 0);
            float[] xs = { 0f, fade, r.width - fade, r.width };
            Color32[] cols = { clear, solid, solid, clear };
            for (int i = 0; i < 4; i++)
            {
                mesh.SetNextVertex(new Vertex { position = new Vector3(xs[i], 0f, Vertex.nearZ), tint = cols[i] });
                mesh.SetNextVertex(new Vertex { position = new Vector3(xs[i], r.height, Vertex.nearZ), tint = cols[i] });
            }
            for (int seg = 0; seg < 3; seg++)
            {
                ushort a = (ushort)(seg * 2);
                ushort b = (ushort)(a + 1);
                ushort c = (ushort)(a + 2);
                ushort d = (ushort)(a + 3);
                mesh.SetNextIndex(a); mesh.SetNextIndex(c); mesh.SetNextIndex(d);
                mesh.SetNextIndex(a); mesh.SetNextIndex(d); mesh.SetNextIndex(b);
            }

            var p = ctx.painter2D;
            p.strokeColor = borderCol;
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(fade * 0.65f, 1f));
            p.LineTo(new Vector2(r.width - fade * 0.65f, 1f));
            p.MoveTo(new Vector2(fade * 0.65f, r.height - 1f));
            p.LineTo(new Vector2(r.width - fade * 0.65f, r.height - 1f));
            p.Stroke();
        }

        private void DrawBobberIcon(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            Vector2 c = r.center;
            float t = Time.unscaledTime;
            var p = ctx.painter2D;

            float ripple = Mathf.Repeat(t * 0.65f, 1f);
            p.strokeColor = new Color(0.62f, 0.94f, 0.92f, (1f - ripple) * 0.65f);
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.Arc(new Vector2(c.x, c.y + 4f), 6f + ripple * 10f, 0f, 360f);
            p.Stroke();

            float bob = Mathf.Sin(t * 3.8f) * 2.4f;
            Vector2 bc = new Vector2(c.x, c.y - 1f + bob);
            p.fillColor = new Color(0.95f, 0.44f, 0.34f, 1f);
            p.BeginPath();
            p.Arc(bc, 6.5f, 180f, 360f);
            p.ClosePath();
            p.Fill();

            p.fillColor = new Color(1f, 0.97f, 0.90f, 1f);
            p.BeginPath();
            p.Arc(bc, 6.5f, 0f, 180f);
            p.ClosePath();
            p.Fill();

            p.strokeColor = new Color(1f, 0.88f, 0.52f, 0.95f);
            p.lineWidth = 1.8f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(bc.x, bc.y - 6.5f));
            p.LineTo(new Vector2(bc.x, bc.y - 11.5f));
            p.Stroke();

            p.strokeColor = new Color(0.70f, 0.96f, 0.94f, 0.85f);
            p.lineWidth = 1.6f;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x - 11f, c.y + 8f));
            p.QuadraticCurveTo(new Vector2(c.x - 4f, c.y + 5f), new Vector2(c.x, c.y + 8f));
            p.QuadraticCurveTo(new Vector2(c.x + 4f, c.y + 11f), new Vector2(c.x + 11f, c.y + 8f));
            p.Stroke();
        }

        private void DrawWorldBiteBeacon(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            Vector2 c = r.center;
            Color accent = session && session.Selected != null
                ? RarityBright(session.Selected.rarity)
                : new Color(1f, 0.82f, 0.28f);

            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;

            for (int i = 0; i < 2; i++)
            {
                float rp = Mathf.Repeat(stateAge * 1.9f + i * 0.5f, 1f);
                p.strokeColor = new Color(accent.r, accent.g, accent.b, (1f - rp) * 0.85f);
                p.lineWidth = Mathf.Lerp(3f, 0.8f, rp);
                p.BeginPath();
                p.Arc(c, 16f + rp * 22f, 0f, 360f);
                p.Stroke();
            }

            p.fillColor = new Color(0.06f, 0.14f, 0.16f, 0.55f);
            DrawDiamond(p, c + new Vector2(0f, 2.5f), 20f);
            p.Fill();

            p.fillColor = accent;
            p.strokeColor = new Color(0.24f, 0.15f, 0.05f, 0.95f);
            p.lineWidth = 2.0f;
            DrawDiamond(p, c, 19f);
            p.Fill();
            p.Stroke();

            p.strokeColor = new Color(1f, 0.99f, 0.94f, 1f);
            p.lineWidth = 3.8f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x, c.y - 8.5f));
            p.LineTo(new Vector2(c.x, c.y + 1.5f));
            p.Stroke();

            p.fillColor = new Color(1f, 0.99f, 0.94f, 1f);
            p.BeginPath();
            p.Arc(new Vector2(c.x, c.y + 7.5f), 2.3f, 0f, 360f);
            p.Fill();
        }

        private void DrawMiniScrim(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 60f || r.height < 40f) return;

            // Soft feathered elliptical deep-coastal mist behind the floating keys (ZERO hard box edges!)
            const int segs = 40;
            var mesh = ctx.Allocate(1 + segs * 2, segs * 9);
            Vector2 c = r.center;
            float rx = r.width * 0.50f;
            float ry = r.height * 0.52f;
            var c0 = new Color32(8, 28, 38, 148);
            var c1 = new Color32(10, 32, 42, 92);
            var c2 = new Color32(10, 32, 42, 0);
            mesh.SetNextVertex(new Vertex { position = new Vector3(c.x, c.y, Vertex.nearZ), tint = c0 });
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                mesh.SetNextVertex(new Vertex { position = new Vector3(c.x + Mathf.Cos(a) * rx * 0.62f, c.y + Mathf.Sin(a) * ry * 0.62f, Vertex.nearZ), tint = c1 });
            }
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                mesh.SetNextVertex(new Vertex { position = new Vector3(c.x + Mathf.Cos(a) * rx, c.y + Mathf.Sin(a) * ry, Vertex.nearZ), tint = c2 });
            }
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                ushort mA = (ushort)(1 + i);
                ushort mB = (ushort)(1 + next);
                ushort oA = (ushort)(1 + segs + i);
                ushort oB = (ushort)(1 + segs + next);
                mesh.SetNextIndex(0); mesh.SetNextIndex(mA); mesh.SetNextIndex(mB);
                mesh.SetNextIndex(mA); mesh.SetNextIndex(oA); mesh.SetNextIndex(oB);
                mesh.SetNextIndex(mA); mesh.SetNextIndex(oB); mesh.SetNextIndex(mB);
            }
        }

        private void DrawKeysLineBg(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 40f || r.height < 20f) return;

            var p = ctx.painter2D;
            float cy = r.height * 0.50f;
            float t = Time.unscaledTime;

            // Delicate sunlit golden fishing-line filigree threading behind the 4 floating 3D keycaps
            p.strokeColor = new Color(1f, 0.90f, 0.58f, 0.38f);
            p.lineWidth = 1.8f;
            p.BeginPath();
            p.MoveTo(new Vector2(8f, cy));
            p.LineTo(new Vector2(r.width - 8f, cy));
            p.Stroke();

            // Subtle sweeping glint traveling along the line
            float gx = Mathf.Lerp(16f, r.width - 16f, Mathf.Repeat(t * 0.42f, 1f));
            p.strokeColor = new Color(1f, 0.98f, 0.86f, 0.78f);
            p.lineWidth = 2.6f;
            p.BeginPath();
            p.MoveTo(new Vector2(Mathf.Max(8f, gx - 24f), cy));
            p.LineTo(new Vector2(Mathf.Min(r.width - 8f, gx + 24f), cy));
            p.Stroke();

            // Delicate golden nautical diamond finials at left & right ends
            p.fillColor = new Color(1f, 0.88f, 0.52f, 0.85f);
            DrawDiamond(p, new Vector2(6f, cy), 4.2f);
            p.Fill();
            DrawDiamond(p, new Vector2(r.width - 6f, cy), 4.2f);
            p.Fill();
        }

        private static void DrawGoldenDiamondIcon(MeshGenerationContext ctx, float size)
        {
            var p = ctx.painter2D;
            Vector2 c = ctx.visualElement.contentRect.center;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.4f);
            p.lineJoin = LineJoin.Round;

            p.fillColor = new Color(1f, 0.84f, 0.38f, 0.22f + 0.16f * pulse);
            DrawDiamond(p, c, size + 2.2f);
            p.Fill();

            p.fillColor = new Color(0.96f, 0.76f, 0.26f, 1f);
            p.strokeColor = new Color(0.36f, 0.22f, 0.06f, 0.90f);
            p.lineWidth = 1.2f;
            DrawDiamond(p, c, size);
            p.Fill();
            p.Stroke();

            p.fillColor = new Color(1f, 0.98f, 0.88f, 1f);
            DrawDiamond(p, c, size * 0.44f);
            p.Fill();
        }

        private void DrawKeySlotFx(MeshGenerationContext ctx, int idx)
        {
            var slot = activeSlots[idx];
            Rect r = ctx.visualElement.contentRect;
            Vector2 c = r.center;
            var p = ctx.painter2D;

            // Signature active spotlight: soft volumetric coastal-teal/gold aura + floating golden crest tucked right above active keycap
            if (slot.active && slot.outcome == 0 && slot.highlight > 0.02f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7.0f);
                DrawRadialGlowMesh(ctx, c, 64f + pulse * 6f,
                    new Color(0.28f, 0.96f, 0.88f, slot.highlight * (0.56f + pulse * 0.18f)),
                    new Color(0.20f, 0.84f, 0.78f, slot.highlight * 0.22f));

                float bobY = 6.5f + Mathf.Sin(Time.unscaledTime * 8.5f) * 2.0f;
                p.fillColor = new Color(1f, 0.88f, 0.40f, slot.highlight);
                p.strokeColor = new Color(0.24f, 0.15f, 0.04f, slot.highlight * 0.95f);
                p.lineWidth = 1.4f;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x - 6.8f, bobY));
                p.LineTo(new Vector2(c.x + 6.8f, bobY));
                p.LineTo(new Vector2(c.x, bobY + 6.8f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }

            // Expanding water-ripple feedback ring on hit / miss
            if (slot.pressAge < 0.34f && slot.outcome != 0)
            {
                float t = Smooth(slot.pressAge / 0.34f);
                float alpha = (1f - t) * 0.92f;
                float half = Mathf.Lerp(42f, 58f, t);
                p.strokeColor = slot.outcome > 0
                    ? new Color(0.42f, 0.96f, 0.78f, alpha)
                    : new Color(1.00f, 0.52f, 0.42f, alpha);
                p.lineWidth = Mathf.Lerp(3.2f, 0.8f, t);
                DrawRoundedRect(p, new Rect(c.x - half, c.y - half, half * 2f, half * 2f), Mathf.Lerp(18f, 24f, t));
                p.Stroke();
            }
        }

        private void DrawActiveArrow(MeshGenerationContext ctx, int idx)
        {
            var slot = activeSlots[idx];
            bool isTealSurface = slot.active && slot.outcome == 0;
            Color col = slot.outcome > 0 ? new Color(1f, 0.99f, 0.94f, 1f)
                : slot.outcome < 0 ? new Color(1f, 0.96f, 0.94f, 0.96f)
                : isTealSurface ? new Color(1f, 0.99f, 0.92f, 1f)
                : new Color(0.27f, 0.20f, 0.13f, 0.96f);

            float nudge = isTealSurface ? Mathf.Sin(Time.unscaledTime * 8.5f) * 2.4f : 0f;
            DrawBoldBlockArrow(ctx, slot.direction, col, nudge, isLightOnDark: isTealSurface || slot.outcome != 0);
        }

        private void DrawGhostArrow(MeshGenerationContext ctx, int idx)
        {
            var slot = ghostSlots[idx];
            Color col = slot.outcome != 0 ? new Color(1f, 0.98f, 0.94f, 0.85f) : new Color(0.32f, 0.24f, 0.16f, 0.7f);
            DrawBoldBlockArrow(ctx, slot.direction, col, 0f, isLightOnDark: slot.outcome != 0);
        }

        private static void DrawBoldBlockArrow(MeshGenerationContext ctx, FishingDirection dir, Color color, float nudge, bool isLightOnDark)
        {
            Rect r = ctx.visualElement.contentRect;
            Vector2 c = r.center;
            Vector2 fwd = dir == FishingDirection.Up ? new Vector2(0f, -1f)
                : dir == FishingDirection.Down ? new Vector2(0f, 1f)
                : dir == FishingDirection.Left ? new Vector2(-1f, 0f)
                : new Vector2(1f, 0f);
            Vector2 right = new Vector2(-fwd.y, fwd.x);
            c += fwd * nudge;

            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            // Sculpted, hefty filled block arrow polygon (solid, dense, high-craft signature readability!)
            void TraceArrow(Vector2 center, float scale)
            {
                float tipLen = 17.5f * scale;
                float headBase = 2.0f * scale;
                float headHalfW = 16.0f * scale;
                float shaftHalfW = 6.8f * scale;
                float tailLen = -15.5f * scale;

                p.BeginPath();
                p.MoveTo(center + fwd * tipLen);
                p.LineTo(center + fwd * headBase + right * headHalfW);
                p.LineTo(center + fwd * headBase + right * shaftHalfW);
                p.LineTo(center + fwd * tailLen + right * shaftHalfW);
                p.LineTo(center + fwd * tailLen - right * shaftHalfW);
                p.LineTo(center + fwd * headBase - right * shaftHalfW);
                p.LineTo(center + fwd * headBase - right * headHalfW);
                p.ClosePath();
            }

            // 1. Soft 3D recessed/elevated drop shadow underneath the solid arrow
            Vector2 shOff = new Vector2(0f, isLightOnDark ? 2.6f : 1.8f);
            p.fillColor = isLightOnDark
                ? new Color(0.03f, 0.16f, 0.18f, 0.42f)
                : new Color(0.45f, 0.36f, 0.22f, 0.22f);
            TraceArrow(c + shOff, 1.0f);
            p.Fill();

            // 2. Solid bold filled arrow body with rounded stroke shoulder for plush cozy feel
            p.fillColor = color;
            p.strokeColor = color;
            p.lineWidth = 3.2f;
            TraceArrow(c, 1.0f);
            p.Fill();
            p.Stroke();

            // 3. Subtle sculpted inner highlight along arrow core for 3D ceramic depth
            p.fillColor = isLightOnDark
                ? new Color(1f, 1f, 1f, 0.26f)
                : new Color(1f, 0.96f, 0.88f, 0.16f);
            TraceArrow(c + new Vector2(0f, -1.2f), 0.56f);
            p.Fill();
        }

        private void DrawCountdownTimerBar(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 10f || r.height < 4f) return;

            float maxSec = session && session.configuration ? Mathf.Max(0.1f, session.configuration.arrowSeconds) : 1.8f;
            float frac = Mathf.Clamp01(lastRemaining / maxSec);
            float fillW = r.width * frac;
            if (fillW < 2f) return;

            float barTop = 5f;
            float barH = Mathf.Max(5f, r.height - 10f);
            float cy = barTop + barH * 0.5f;

            var p = ctx.painter2D;
            // High-contrast coastal aqua -> sunlit amber-gold -> vivid coral urgency curve (reads crisply over warm dock & blue ocean!)
            Color barCol = frac > 0.45f
                ? Color.Lerp(new Color(1.00f, 0.80f, 0.28f), new Color(0.17f, 0.88f, 0.82f), Mathf.Clamp01((frac - 0.45f) / 0.32f))
                : Color.Lerp(new Color(1.00f, 0.36f, 0.28f), new Color(1.00f, 0.80f, 0.28f), Mathf.Clamp01((frac - 0.18f) / 0.27f));

            // Luminous bar fill
            p.fillColor = barCol;
            DrawRoundedRect(p, new Rect(0f, barTop, fillW, barH), barH * 0.5f);
            p.Fill();

            // Crisp specular top sheen
            p.fillColor = new Color(1f, 1f, 0.96f, 0.48f);
            DrawRoundedRect(p, new Rect(2f, barTop + 1f, Mathf.Max(0f, fillW - 4f), barH * 0.36f), 2f);
            p.Fill();

            // Glowing traveling diamond spark right at the moving timer tip
            if (fillW > 6f)
            {
                p.fillColor = new Color(barCol.r, barCol.g, barCol.b, 0.48f);
                DrawDiamond(p, new Vector2(fillW, cy), 7.6f);
                p.Fill();

                p.fillColor = new Color(1f, 0.99f, 0.92f, 1f);
                p.strokeColor = new Color(0.18f, 0.12f, 0.04f, 0.48f);
                p.lineWidth = 1.0f;
                DrawDiamond(p, new Vector2(fillW, cy), 4.6f);
                p.Fill();
                p.Stroke();
            }
        }

        private void DrawWaterTankGauge(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 40f || r.height < 120f) return;

            // Majestic Bottom-Right Nautical Glass Gauge (matches DermagaMinimap warm ivory-gold bezel!)
            // Layout inside 96x304:
            //   - Left (inward toward player gaze): Expressive animated pearl-ivory fish pointer facing RIGHT into the water level
            //   - Center-right (x=34..72): Tall 38px-wide translucent coastal glass chamber with ornate compass finials
            //   - Right (outer edge): Golden target threshold diamond crest (zero collision with fish!)
            float t = Time.unscaledTime;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            float tubeW = 40f;
            float tubeX = 44f;
            float tubeTop = 20f;
            float tubeBot = r.height - 20f;
            float tubeH = tubeBot - tubeTop;
            float rad = tubeW * 0.5f;
            float tubeCx = tubeX + rad;
            Rect tubeRect = new Rect(tubeX, tubeTop, tubeW, tubeH);

            // 1. Soft atmospheric radial aura behind gauge when target threshold is reached!
            if (thresholdReached)
            {
                float glowPulse = 0.5f + 0.5f * Mathf.Sin(t * 4.2f);
                DrawRadialGlowMesh(ctx, new Vector2(tubeCx, r.height * 0.5f), 58f + glowPulse * 8f,
                    new Color(0.24f, 0.94f, 0.78f, 0.24f + glowPulse * 0.12f),
                    new Color(0.16f, 0.78f, 0.72f, 0.08f));
            }

            // 2. Soft grounding shadow behind floating glass tube
            p.fillColor = new Color(0.02f, 0.10f, 0.15f, 0.52f);
            DrawRoundedRect(p, new Rect(tubeX + 2.5f, tubeTop + 4f, tubeW, tubeH), rad);
            p.Fill();

            // 3. Translucent deep-ocean glass chamber backing
            p.fillColor = new Color(0.05f, 0.18f, 0.25f, 0.80f);
            DrawRoundedRect(p, tubeRect, rad);
            p.Fill();

            // Inner subtle depth bevel ring
            p.strokeColor = new Color(0.02f, 0.10f, 0.15f, 0.55f);
            p.lineWidth = 2.0f;
            DrawRoundedRect(p, new Rect(tubeX + 2f, tubeTop + 2f, tubeW - 4f, tubeH - 4f), rad - 2f);
            p.Stroke();

            // Nautical measurement scale ticks on the right interior wall
            p.strokeColor = new Color(0.84f, 0.97f, 0.97f, 0.30f);
            p.lineWidth = 1.3f;
            p.BeginPath();
            for (int k = 1; k <= 9; k++)
            {
                float ty = tubeBot - 4f - (tubeH - 8f) * (k * 0.10f);
                float tickLen = (k == 5) ? 11f : (k % 2 == 0 ? 8f : 5f);
                p.MoveTo(new Vector2(tubeX + tubeW - 4f, ty));
                p.LineTo(new Vector2(tubeX + tubeW - 4f - tickLen, ty));
            }
            p.Stroke();

            // 4. Living Dual-Layer Animated Ocean Water Fill
            float fillFrac = Mathf.Clamp01(gaugeFraction);
            float usableH = tubeH - 6f;
            float fillH = fillFrac * usableH;
            float surfaceY = (tubeBot - 3f) - fillH;

            if (fillH > 3f)
            {
                Color waterDeep = thresholdReached
                    ? new Color(0.14f, 0.78f, 0.62f, 0.95f)
                    : new Color(0.13f, 0.66f, 0.74f, 0.94f);
                Color waterBright = thresholdReached
                    ? new Color(0.28f, 0.92f, 0.76f, 0.96f)
                    : new Color(0.24f, 0.82f, 0.86f, 0.95f);

                if (maxPulseAge < 0.65f)
                {
                    float mp = Mathf.Sin((maxPulseAge / 0.65f) * Mathf.PI) * 0.45f;
                    waterDeep = Color.Lerp(waterDeep, new Color(0.86f, 1.00f, 0.72f, 1f), mp);
                    waterBright = Color.Lerp(waterBright, new Color(1f, 0.98f, 0.78f, 1f), mp);
                }

                float innerX = tubeX + 3f;
                float innerW = tubeW - 6f;
                float innerRad = innerW * 0.5f;
                float fillTopY = Mathf.Clamp(surfaceY, tubeTop + 3f, tubeBot - 6f);
                float clampedH = (tubeBot - 3f) - fillTopY;
                float cornerR = Mathf.Min(innerRad, clampedH * 0.5f);

                // Base rich turquoise body
                p.fillColor = waterDeep;
                DrawRoundedRect(p, new Rect(innerX, fillTopY, innerW, clampedH), cornerR);
                p.Fill();

                // Sunlit inner vertical core sheen inside the water column
                p.fillColor = new Color(waterBright.r, waterBright.g, waterBright.b, 0.58f);
                DrawRoundedRect(p, new Rect(innerX + 4f, fillTopY + 2f, innerW - 10f, Mathf.Max(2f, clampedH - 4f)), Mathf.Max(2f, cornerR - 3f));
                p.Fill();

                // Dual overlapping animated meniscus waves at the water surface
                if (fillH > 12f && fillH < usableH - 6f)
                {
                    const int ws = 12;
                    // Back wave (soft translucent seafoam)
                    p.strokeColor = new Color(0.62f, 0.98f, 0.96f, 0.55f);
                    p.lineWidth = 2.4f;
                    p.BeginPath();
                    for (int i = 0; i <= ws; i++)
                    {
                        float u = i / (float)ws;
                        float x = innerX + 2.5f + u * (innerW - 5f);
                        float y = fillTopY + 3.0f + Mathf.Sin(u * Mathf.PI * 2.2f - t * 4.2f + 1.4f) * 2.0f;
                        if (i == 0) p.MoveTo(new Vector2(x, y));
                        else p.LineTo(new Vector2(x, y));
                    }
                    p.Stroke();

                    // Front sunlit white-aqua foam crest
                    p.strokeColor = new Color(0.92f, 1f, 0.99f, 0.96f);
                    p.lineWidth = 2.6f;
                    p.BeginPath();
                    for (int i = 0; i <= ws; i++)
                    {
                        float u = i / (float)ws;
                        float x = innerX + 2.0f + u * (innerW - 4f);
                        float y = fillTopY + 1.8f + Mathf.Sin(u * Mathf.PI * 2.0f + t * 5.6f) * 2.2f;
                        if (i == 0) p.MoveTo(new Vector2(x, y));
                        else p.LineTo(new Vector2(x, y));
                    }
                    p.Stroke();
                }

                // Animated rising translucent ocean bubbles inside the tall chamber
                for (int b = 0; b < 7; b++)
                {
                    float prog = Mathf.Repeat(t * (0.26f + (b % 3) * 0.08f) + b * 0.17f, 1f);
                    float by = Mathf.Lerp(tubeBot - 12f, fillTopY + 8f, prog);
                    if (by > fillTopY + 5f && by < tubeBot - 10f)
                    {
                        float bx = innerX + innerW * (0.24f + ((b * 37) % 52) / 100f) + Mathf.Sin(t * 3.0f + b * 1.9f) * 2.8f;
                        float br = 1.6f + (b % 3) * 0.85f;
                        float ba = Mathf.Sin(prog * Mathf.PI) * 0.68f;
                        p.fillColor = new Color(0.92f, 1f, 1f, ba * 0.45f);
                        p.strokeColor = new Color(0.96f, 1f, 1f, ba);
                        p.lineWidth = 1.0f;
                        p.BeginPath();
                        p.Arc(new Vector2(bx, by), br, 0f, 360f);
                        p.Fill();
                        p.Stroke();
                    }
                }
            }

            // 5. Curved 3D Glass Specular Reflection + Warm Ivory-Gold Nautical Bezel (matches DermagaMinimap!)
            p.fillColor = new Color(1f, 1f, 1f, 0.22f);
            DrawRoundedRect(p, new Rect(tubeX + 5.5f, tubeTop + 14f, 5.2f, tubeH - 28f), 2.6f);
            p.Fill();
            p.fillColor = new Color(1f, 1f, 1f, 0.12f);
            DrawRoundedRect(p, new Rect(tubeX + tubeW - 8.5f, tubeTop + 20f, 2.4f, tubeH - 40f), 1.2f);
            p.Fill();

            // Outer warm ivory-gold bezel rim
            p.strokeColor = new Color(1f, 0.96f, 0.84f, 0.90f);
            p.lineWidth = 2.4f;
            DrawRoundedRect(p, tubeRect, rad);
            p.Stroke();

            // Delicate inner hairline golden filigree ring
            p.strokeColor = new Color(0.96f, 0.82f, 0.48f, 0.46f);
            p.lineWidth = 1.0f;
            DrawRoundedRect(p, new Rect(tubeX + 3.2f, tubeTop + 3.2f, tubeW - 6.4f, tubeH - 6.4f), rad - 3.2f);
            p.Stroke();

            // Sculpted nautical compass finials at top & bottom of capsule (matches DermagaMinimap!)
            void DrawCompassFinial(float cy)
            {
                p.fillColor = new Color(0.08f, 0.18f, 0.22f, 0.92f);
                p.strokeColor = new Color(1f, 0.92f, 0.64f, 0.96f);
                p.lineWidth = 1.8f;
                p.BeginPath(); p.Arc(new Vector2(tubeCx, cy), 5.6f, 0f, 360f); p.Fill(); p.Stroke();

                p.fillColor = new Color(1f, 0.90f, 0.54f, 1f);
                DrawDiamond(p, new Vector2(tubeCx, cy), 3.0f);
                p.Fill();
            }
            DrawCompassFinial(tubeTop);
            DrawCompassFinial(tubeBot);

            // 6. Golden Target Threshold Line + Faceted Diamond Crest on RIGHT (outer) bezel
            int targetScore = session && session.Selected != null ? session.Selected.minimumScore : 50;
            float targetY = (tubeBot - 3f) - Mathf.Clamp01(targetScore / 100f) * usableH;
            float tPulse = thresholdPulseAge < 0.65f ? Mathf.Sin((thresholdPulseAge / 0.65f) * Mathf.PI) : 0f;
            Color targetCol = thresholdReached
                ? Color.Lerp(new Color(1f, 0.90f, 0.40f), Color.white, tPulse * 0.55f)
                : new Color(1f, 0.85f, 0.38f, 0.98f);

            // Soft golden glow behind threshold bar
            p.strokeColor = new Color(1f, 0.86f, 0.36f, 0.40f + tPulse * 0.42f);
            p.lineWidth = 6.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(tubeX - 3f, targetY));
            p.LineTo(new Vector2(tubeX + tubeW + 6f, targetY));
            p.Stroke();

            // Crisp golden threshold bar
            p.strokeColor = targetCol;
            p.lineWidth = 2.4f;
            p.BeginPath();
            p.MoveTo(new Vector2(tubeX - 3f, targetY));
            p.LineTo(new Vector2(tubeX + tubeW + 7f, targetY));
            p.Stroke();

            // Faceted golden target diamond on right bezel
            Vector2 dPos = new Vector2(tubeX + tubeW + 10f, targetY);
            p.fillColor = targetCol;
            p.strokeColor = new Color(0.28f, 0.17f, 0.04f, 0.96f);
            p.lineWidth = 1.6f;
            DrawDiamond(p, dPos, 6.8f + tPulse * 2.8f);
            p.Fill();
            p.Stroke();

            p.fillColor = new Color(1f, 0.99f, 0.90f, 0.96f);
            DrawDiamond(p, dPos, 3.0f);
            p.Fill();

            // 7. Expressive Gliding Fish Pointer riding cleanly on the LEFT (inward) side, facing RIGHT toward the water level!
            float pointerY = Mathf.Clamp(surfaceY, tubeTop + 18f, tubeBot - 18f);
            float wag = Mathf.Sin(t * 11.5f) * 2.2f;
            float fx = tubeX - 18f; // body center at x=26, nose at x=35 pointing toward tubeX=44, tail at x=10!

            // Soft dark-coastal drop shadow for guaranteed contrast over ocean, dock, or sand
            p.fillColor = new Color(0.03f, 0.12f, 0.17f, 0.56f);
            DrawEllipse(p, new Vector2(fx, pointerY + 2.4f), 9.4f, 6.0f);
            p.Fill();

            Color fishFill = thresholdReached
                ? new Color(1f, 0.94f, 0.66f, 1f)
                : new Color(1f, 0.98f, 0.92f, 1f);
            Color fishRim = new Color(0.08f, 0.22f, 0.27f, 0.92f);

            // Wagging tail fin (on the LEFT of the fish body — clean straight polygon, zero bezier miter!)
            p.fillColor = fishFill;
            p.strokeColor = fishRim;
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(fx - 7.0f, pointerY));
            p.LineTo(new Vector2(fx - 15.6f, pointerY - 6.2f + wag));
            p.LineTo(new Vector2(fx - 13.4f, pointerY + wag * 0.4f));
            p.LineTo(new Vector2(fx - 15.6f, pointerY + 6.2f + wag));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Cute top dorsal fin (clean rounded polygon — avoids Unity Painter2D QuadraticCurveTo+ClosePath spike!)
            p.BeginPath();
            p.MoveTo(new Vector2(fx - 4.0f, pointerY - 4.5f));
            p.LineTo(new Vector2(fx - 1.2f, pointerY - 9.0f));
            p.LineTo(new Vector2(fx + 2.6f, pointerY - 7.6f));
            p.LineTo(new Vector2(fx + 4.4f, pointerY - 4.2f));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            // Plush fish body facing RIGHT toward the glass tube
            DrawEllipse(p, new Vector2(fx, pointerY), 9.0f, 5.7f);
            p.Fill();
            p.Stroke();

            // Subtle warm cheek/gill highlight
            p.fillColor = thresholdReached
                ? new Color(1f, 0.80f, 0.34f, 0.65f)
                : new Color(0.32f, 0.88f, 0.84f, 0.45f);
            DrawEllipse(p, new Vector2(fx + 1.2f, pointerY + 0.9f), 3.2f, 2.2f);
            p.Fill();

            // Crisp eye near the right nose
            p.fillColor = new Color(0.06f, 0.20f, 0.25f, 1f);
            p.BeginPath();
            p.Arc(new Vector2(fx + 4.6f, pointerY - 0.9f), 1.65f, 0f, 360f);
            p.Fill();
            p.fillColor = Color.white;
            p.BeginPath();
            p.Arc(new Vector2(fx + 5.1f, pointerY - 1.4f), 0.62f, 0f, 360f);
            p.Fill();

            // Delicate horizontal pointer tick bridging the fish nose to the glass bezel
            p.strokeColor = new Color(1f, 0.96f, 0.84f, 0.82f);
            p.lineWidth = 1.8f;
            p.BeginPath();
            p.MoveTo(new Vector2(fx + 10.6f, pointerY));
            p.LineTo(new Vector2(tubeX - 1.8f, pointerY));
            p.Stroke();

            // 8. Golden celebratory starburst when crossing target threshold
            if (thresholdPulseAge < 0.70f)
            {
                float bt = thresholdPulseAge / 0.70f;
                float ba = (1f - bt) * 0.96f;
                for (int s = 0; s < 8; s++)
                {
                    float ang = (s / 8f) * Mathf.PI * 2f + bt * 1.1f;
                    float dist = 10f + bt * 30f;
                    Vector2 sp = new Vector2(tubeCx + Mathf.Cos(ang) * dist, targetY + Mathf.Sin(ang) * dist);
                    p.fillColor = new Color(1f, 0.92f, 0.52f, ba);
                    DrawDiamond(p, sp, 4.0f * (1f - bt * 0.45f));
                    p.Fill();
                }
            }
        }

        private void DrawCenteredRewardAmbience(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 40f || r.height < 40f) return;

            // Deep coastal translucent ocean veil (allows world glow to breathe softly behind)
            var mesh = ctx.Allocate(4, 6);
            var topCol = new Color32(12, 42, 54, 212);
            var botCol = new Color32(8, 28, 38, 232);
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, 0f, Vertex.nearZ), tint = topCol });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, 0f, Vertex.nearZ), tint = topCol });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, r.height, Vertex.nearZ), tint = botCol });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, r.height, Vertex.nearZ), tint = botCol });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);

            var p = ctx.painter2D;
            float t = Time.unscaledTime;

            // Gentle sunlit ocean god-rays drifting diagonally across the backdrop
            for (int i = 0; i < 4; i++)
            {
                float drift = Mathf.Sin(t * (0.35f + i * 0.10f) + i * 1.8f) * 24f;
                float x0 = r.width * (0.20f + i * 0.18f) + drift;
                float wTop = 44f + (i % 2) * 26f;
                float wBot = wTop * 1.9f;
                p.fillColor = new Color(0.84f, 0.98f, 0.98f, 0.045f + 0.018f * Mathf.Sin(t * 0.9f + i));
                p.BeginPath();
                p.MoveTo(new Vector2(x0, 0f));
                p.LineTo(new Vector2(x0 + wTop, 0f));
                p.LineTo(new Vector2(x0 + wBot + 90f, r.height));
                p.LineTo(new Vector2(x0 + 90f, r.height));
                p.ClosePath();
                p.Fill();
            }

            // Drifting sunlit motes & gentle rising bubbles
            for (int i = 0; i < 14; i++)
            {
                float seed = i * 2.07f + 0.3f;
                float px = r.width * (0.08f + 0.84f * Mathf.Repeat(seed * 0.37f + t * 0.006f, 1f));
                float py = r.height * (0.06f + 0.88f * Mathf.Repeat(seed * 0.61f - t * 0.014f, 1f));
                float alpha = 0.08f + 0.10f * Mathf.Sin(t * 1.6f + seed * 3f);
                if (i % 3 == 0)
                {
                    p.strokeColor = new Color(0.82f, 0.98f, 1f, alpha * 1.4f);
                    p.lineWidth = 1.2f;
                    p.BeginPath();
                    p.Arc(new Vector2(px, py), 3.0f + (i % 3) * 1.5f, 0f, 360f);
                    p.Stroke();
                }
                else
                {
                    p.fillColor = new Color(1f, 0.94f, 0.74f, alpha);
                    p.BeginPath();
                    p.Arc(new Vector2(px, py), 2.2f + (i % 3) * 1.0f, 0f, 360f);
                    p.Fill();
                }
            }
        }

        private void DrawHeroFishSpotlight(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 60f || r.height < 60f) return;

            Vector2 c = r.center;
            float t = Time.unscaledTime;
            var p = ctx.painter2D;

            // 1. True smooth volumetric radial spotlight mesh (feathers seamlessly to 0 alpha — ZERO circle outline!)
            float breathe = Mathf.Sin(t * 1.8f) * 6f;
            float baseRad = Mathf.Min(r.width, r.height) * 0.44f;
            DrawRadialGlowMesh(ctx, c, baseRad + 42f + breathe,
                new Color(resultAccent.r, resultAccent.g, resultAccent.b, resultLegendary ? 0.34f : 0.24f),
                new Color(0.36f, 0.82f, 0.86f, 0.10f));
            DrawRadialGlowMesh(ctx, c, baseRad * 0.72f + breathe * 0.5f,
                new Color(0.86f, 0.99f, 0.97f, 0.22f),
                new Color(0.55f, 0.90f, 0.90f, 0.06f));

            // 2. Soft grounded elliptical 3D depth shadow floating beneath the swimming fish
            Vector2 shCenter = new Vector2(c.x, r.height - 42f);
            float shRx = 96f + Mathf.Sin(t * 2.6f) * 4f;
            float shRy = 13f;
            p.fillColor = new Color(0.02f, 0.10f, 0.14f, 0.34f);
            DrawEllipse(p, shCenter, shRx, shRy);
            p.Fill();

            // 3. Twinkling coastal/rarity diamond sparkles orbiting softly around the 3D fish
            int sparkCount = resultLegendary ? 10 : 6;
            for (int i = 0; i < sparkCount; i++)
            {
                float ang = (i / (float)sparkCount) * Mathf.PI * 2f + t * 0.18f;
                float dist = baseRad * (0.80f + (i % 3) * 0.14f);
                float pulse = Mathf.Max(0f, Mathf.Sin(t * (1.8f + (i % 3) * 0.4f) + i * 1.4f));
                if (pulse < 0.08f) continue;

                Vector2 sp = c + new Vector2(Mathf.Cos(ang) * dist * 1.25f, Mathf.Sin(ang) * dist * 0.85f);
                Color col = resultLegendary
                    ? new Color(1f, 0.90f, 0.42f, pulse * 0.92f)
                    : new Color(resultAccent.r, resultAccent.g, resultAccent.b, pulse * 0.78f);
                p.fillColor = col;
                DrawDiamond(p, sp, 2.8f + pulse * 3.8f);
                p.Fill();
            }
        }

        // =====================================================================
        // HELPERS & METADATA
        // =====================================================================

        private void UpdateStepDots(int currentGroup)
        {
            for (int i = 0; i < 5; i++)
            {
                stepDots[i].EnableInClassList("fish-step-dot-done", i < currentGroup);
                stepDots[i].EnableInClassList("fish-step-dot-active", i == currentGroup);
            }
        }

        private static void ResetKeySlot(KeySlot slot)
        {
            if (slot == null) return;
            slot.active = false;
            slot.outcome = 0;
            slot.missed = false;
            slot.pressAge = 10f;
            slot.highlight = 0f;
            slot.cap.RemoveFromClassList("fish-key-cap-active");
            slot.cap.RemoveFromClassList("fish-key-cap-hit");
            slot.cap.RemoveFromClassList("fish-key-cap-miss");
            slot.cap.style.translate = new Translate(0f, 0f, 0f);
            slot.cap.style.scale = new Scale(Vector3.one);
            slot.pop.text = "";
            slot.pop.style.opacity = 0f;
        }

        private static void ApplyRarityStyleDark(Label badge, FishRarity rarity)
        {
            Color bright = RarityBright(rarity);
            Color deep = RarityAccent(rarity);
            badge.text = RarityIndo(rarity);
            badge.style.backgroundColor = new Color(deep.r, deep.g, deep.b, 0.34f);
            badge.style.borderTopColor = new Color(bright.r, bright.g, bright.b, 0.78f);
            badge.style.borderBottomColor = new Color(bright.r, bright.g, bright.b, 0.78f);
            badge.style.borderLeftColor = new Color(bright.r, bright.g, bright.b, 0.78f);
            badge.style.borderRightColor = new Color(bright.r, bright.g, bright.b, 0.78f);
            badge.style.color = new Color(1f, 0.99f, 0.94f, 1f);
        }

        private static string RarityIndo(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Uncommon: return "Uncommon";
                case FishRarity.Rare: return "Rare";
                case FishRarity.Epic: return "Epic";
                case FishRarity.Legendary: return "Legendary";
                default: return "Common";
            }
        }

        private static Color RarityAccent(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Uncommon: return new Color(0.20f, 0.56f, 0.40f);
                case FishRarity.Rare: return new Color(0.24f, 0.44f, 0.70f);
                case FishRarity.Epic: return new Color(0.58f, 0.34f, 0.64f);
                case FishRarity.Legendary: return new Color(0.80f, 0.54f, 0.18f);
                default: return new Color(0.22f, 0.54f, 0.52f);
            }
        }

        private static Color RarityBright(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Uncommon: return new Color(0.46f, 0.90f, 0.64f);
                case FishRarity.Rare: return new Color(0.46f, 0.78f, 1.00f);
                case FishRarity.Epic: return new Color(0.86f, 0.62f, 0.98f);
                case FishRarity.Legendary: return new Color(1.00f, 0.84f, 0.34f);
                default: return new Color(0.56f, 0.92f, 0.86f);
            }
        }

        private static void GetFishMeta(string fishId, string fallbackName,
            out string indoName, out string sci, out string habitat)
        {
            switch (fishId)
            {
                case "prototype_fish_common":
                    indoName = "Ikan Badut";
                    sci = "Amphiprion ocellaris";
                    habitat = "Dermaga Pesisir";
                    return;
                case "prototype_fish_uncommon":
                    indoName = "Kakap Merah";
                    sci = "Lutjanus malabaricus";
                    habitat = "Tepi Dermaga";
                    return;
                case "prototype_fish_rare":
                    indoName = "Kerapu Karang";
                    sci = "Cephalopholis miniata";
                    habitat = "Celah Terumbu";
                    return;
                case "prototype_fish_epic":
                    indoName = "Gramma Ungu";
                    sci = "Gramma loreto";
                    habitat = "Lereng Terumbu";
                    return;
                case "prototype_fish_legendary":
                    indoName = "Ikan Lepu Ayam";
                    sci = "Pterois volitans";
                    habitat = "Terumbu Luar";
                    return;
                default:
                    indoName = fallbackName;
                    sci = "Species marina";
                    habitat = "Dermaga Pesisir";
                    return;
            }
        }

        private static void DrawRadialGlowMesh(MeshGenerationContext ctx, Vector2 center, float radius, Color innerCol, Color midCol)
        {
            const int segs = 36;
            // Center (0) + Mid ring (1..segs) + Outer zero-alpha ring (segs+1..2*segs)
            var mesh = ctx.Allocate(1 + segs * 2, segs * 9);
            Color32 c0 = innerCol;
            Color32 c1 = midCol;
            Color32 c2 = new Color32(c1.r, c1.g, c1.b, 0);
            mesh.SetNextVertex(new Vertex { position = new Vector3(center.x, center.y, Vertex.nearZ), tint = c0 });
            float rMid = radius * 0.56f;
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                mesh.SetNextVertex(new Vertex { position = new Vector3(center.x + Mathf.Cos(a) * rMid, center.y + Mathf.Sin(a) * rMid, Vertex.nearZ), tint = c1 });
            }
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                mesh.SetNextVertex(new Vertex { position = new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, Vertex.nearZ), tint = c2 });
            }
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                ushort mA = (ushort)(1 + i);
                ushort mB = (ushort)(1 + next);
                ushort oA = (ushort)(1 + segs + i);
                ushort oB = (ushort)(1 + segs + next);
                mesh.SetNextIndex(0); mesh.SetNextIndex(mA); mesh.SetNextIndex(mB);
                mesh.SetNextIndex(mA); mesh.SetNextIndex(oA); mesh.SetNextIndex(oB);
                mesh.SetNextIndex(mA); mesh.SetNextIndex(oB); mesh.SetNextIndex(mB);
            }
        }

        private static void DrawDiamond(Painter2D painter, Vector2 at, float size)
        {
            painter.BeginPath();
            painter.MoveTo(at + Vector2.up * size);
            painter.LineTo(at + Vector2.right * size);
            painter.LineTo(at - Vector2.up * size);
            painter.LineTo(at - Vector2.right * size);
            painter.ClosePath();
        }

        private static void DrawEllipse(Painter2D p, Vector2 center, float rx, float ry)
        {
            const int segs = 28;
            p.BeginPath();
            for (int i = 0; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                Vector2 pt = new Vector2(center.x + Mathf.Cos(a) * rx, center.y + Mathf.Sin(a) * ry);
                if (i == 0) p.MoveTo(pt);
                else p.LineTo(pt);
            }
            p.ClosePath();
        }

        private static void DrawRoundedRect(Painter2D p, Rect rect, float radius)
        {
            float r = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            float x0 = rect.xMin;
            float y0 = rect.yMin;
            float x1 = rect.xMax;
            float y1 = rect.yMax;
            p.BeginPath();
            p.MoveTo(new Vector2(x0 + r, y0));
            p.LineTo(new Vector2(x1 - r, y0));
            p.Arc(new Vector2(x1 - r, y0 + r), r, 270f, 360f, ArcDirection.Clockwise);
            p.LineTo(new Vector2(x1, y1 - r));
            p.Arc(new Vector2(x1 - r, y1 - r), r, 0f, 90f, ArcDirection.Clockwise);
            p.LineTo(new Vector2(x0 + r, y1));
            p.Arc(new Vector2(x0 + r, y1 - r), r, 90f, 180f, ArcDirection.Clockwise);
            p.LineTo(new Vector2(x0, y0 + r));
            p.Arc(new Vector2(x0 + r, y0 + r), r, 180f, 270f, ArcDirection.Clockwise);
            p.ClosePath();
        }

        private static void AttachTactileFeedback(Button btn)
        {
            btn.RegisterCallback<PointerEnterEvent>(_ => DermagaUiAudio.PlayHover());
            btn.RegisterCallback<PointerDownEvent>(_ => btn.AddToClassList("sagara-ui-pressed"));
            btn.RegisterCallback<PointerUpEvent>(_ => btn.RemoveFromClassList("sagara-ui-pressed"));
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.RemoveFromClassList("sagara-ui-pressed"));
        }

        private VisualElement MakeEl(VisualElement parent, string classes, bool ignorePicking)
        {
            var el = new VisualElement { pickingMode = ignorePicking ? PickingMode.Ignore : PickingMode.Position };
            if (!string.IsNullOrEmpty(classes))
            {
                foreach (string c in classes.Split(' '))
                {
                    if (!string.IsNullOrEmpty(c)) el.AddToClassList(c);
                }
            }
            parent.Add(el);
            return el;
        }

        private Label MakeLabel(VisualElement parent, string text, string classes, bool display)
        {
            var lbl = new Label(text) { pickingMode = PickingMode.Ignore };
            if (!string.IsNullOrEmpty(classes))
            {
                foreach (string c in classes.Split(' '))
                {
                    if (!string.IsNullOrEmpty(c)) lbl.AddToClassList(c);
                }
            }
            if (display) ApplyDisplayFont(lbl);
            else ApplyBodyFont(lbl);
            parent.Add(lbl);
            return lbl;
        }

        private void ApplyDisplayFont(VisualElement el)
        {
            if (displayFontAsset) el.style.unityFontDefinition = FontDefinition.FromSDFFont(displayFontAsset);
            else if (displayFont) el.style.unityFontDefinition = FontDefinition.FromFont(displayFont);
        }

        private void ApplyBodyFont(VisualElement el)
        {
            if (bodyFontAsset) el.style.unityFontDefinition = FontDefinition.FromSDFFont(bodyFontAsset);
            else if (bodyFont) el.style.unityFontDefinition = FontDefinition.FromFont(bodyFont);
        }

        private static void SetVisible(VisualElement el, bool visible)
        {
            if (el != null) el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
