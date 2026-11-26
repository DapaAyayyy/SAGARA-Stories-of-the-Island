using Sagara.Dermaga.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Sagara.DermagaV2
{
    [DefaultExecutionOrder(1120)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DermagaCleanupHud : MonoBehaviour
    {
        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private DermagaDockRepair dockRepair;
        [SerializeField] private DermagaMangrovePlanting mangroves;
        [SerializeField] private DermagaCoralExpedition coral;

        [SerializeField] private StyleSheet presentationStyle;
        [SerializeField] private Font textFont;
        [SerializeField] private Font displayFont;
        [SerializeField] private FontAsset bodyFontAsset;
        [SerializeField] private FontAsset displayFontAsset;
        [SerializeField] private DermagaQuestGuidance guidance;
        [SerializeField] private DermagaQuestAnimation questAnimation;
        [SerializeField] private PlayerNpcInteraction npcInteraction;
        [SerializeField] private NpcDialogueUI dialogueView;
        [SerializeField] private Texture2D minimapArtwork;
        [SerializeField] private Texture2D minimapRepairedArtwork;

        [Header("Tactile 3D UI Assets")]
        [SerializeField] private Texture2D infoIcon;
        [SerializeField] private Texture2D celebrationCrest;
        [SerializeField] private Texture2D pickupHandIcon;
        [SerializeField] private Texture2D questTalkIcon;
        [SerializeField] private Texture2D talkBubbleIcon;
        [SerializeField] private Texture2D plantIcon;
        [SerializeField] private Texture2D repairIcon;
        [SerializeField] private Texture2D boatIcon;
        [SerializeField] private Texture2D fishingIcon;

        private UIDocument document;
        private VisualElement container;
        private VisualElement tracker;
        private VisualElement trackerScrim;
        private VisualElement questDiamond;
        private VisualElement questRule;
        private DermagaMinimap minimap;
        private Label title;
        private Label objective;
        private Label trackerDistance;

        // Authentic Genshin-Style PC [E] ▶ (Icon Label) Interaction Prompt
        private Button actionButton;
        private VisualElement actionKeyCap;
        private VisualElement actionKeyHalo;
        private Label actionKey;
        private VisualElement actionArrow;
        private VisualElement actionPill;
        private VisualElement actionStripBg;
        private VisualElement actionIcon;
        private Label prompt;
        private bool actionIsQuestStyle;
        private float actionHoverBlend;
        private float actionSweepPhase = 1f;

        // Sleek top advisory toast
        private VisualElement noticeToast;
        private VisualElement noticeBg;
        private VisualElement noticeIcon;
        private Label noticeLabel;

        // Genshin world / edge waypoint beacon
        private VisualElement marker;
        private VisualElement markerCanvas;
        private Label markerDistance;
        private bool markerOffscreen;
        private Vector2 markerBearing = Vector2.right;

        // Pak Darma overhead quest emblem
        private VisualElement pakDarmaMarker;
        private VisualElement pakDarmaCanvas;

        // Clean, symmetrical, perfectly aligned Quest Completion Banner
        private VisualElement completion;
        private VisualElement completionBg;
        private Button completionCta;

        // Floating MC spoken response
        private VisualElement response;
        private Label responseText;

        // Smooth tactile spring animation state
        private float actionVis;
        private float actionPressTimer;
        private float actionRipplePhase = 1f;
        private float noticeVis;
        private float completionVis;
        private bool lastHasNpcPrompt;
        private bool lastWasFishingPrompt;
        private bool actionHovered;
        private PlayerTrashInteraction trashInteraction;

        internal bool IsPointerOverAction(Vector2 screenPoint)
        {
            if (actionButton == null || actionVis <= 0.02f || actionButton.panel == null)
            {
                return false;
            }
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
                actionButton.panel,
                new Vector2(screenPoint.x, Screen.height - screenPoint.y)
            );
            return actionButton.worldBound.Contains(panelPos);
        }

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();
            if (npcInteraction)
            {
                trashInteraction = npcInteraction.GetComponent<PlayerTrashInteraction>();
            }

            container = new VisualElement { name = "DermagaCleanupHud", pickingMode = PickingMode.Ignore };
            container.StretchToParentSize();
            if (presentationStyle)
            {
                container.styleSheets.Add(presentationStyle);
            }
            ApplyBodyFont(container);

            if (dialogueView)
            {
                dialogueView.enabled = false;
            }

            // --- TOP-LEFT SIDEBAR: GENSHIN MINIMAP + ULTRA-SHARP HD SDF QUEST TRACKER ---
            var sidebar = Element(container, "coast-sidebar");
            container.RegisterCallback<GeometryChangedEvent>(change => PlaceSidebar(sidebar, change.newRect));
            minimap = new DermagaMinimap(sidebar, minimapArtwork, minimapRepairedArtwork);

            tracker = Element(sidebar, "coast-tracker");
            // Soft feathered left-to-right shadow scrim so pure bright text pops cleanly over bright grass/sand
            trackerScrim = Element(tracker, "coast-tracker-scrim");
            trackerScrim.generateVisualContent += DrawTrackerScrim;

            var heading = Element(tracker, "coast-quest-heading");
            questDiamond = Element(heading, "coast-quest-diamond");
            questDiamond.generateVisualContent += DrawQuestDiamond;
            title = Text(heading, "coast-quest-title", "Pesisir yang Pulih", isDisplay: true);
            questRule = Element(tracker, "coast-quest-rule");
            questRule.generateVisualContent += DrawGoldenRule;
            // Use thicker readable Display SDF (Fredoka) at clean crisp sizes with zero inward-eating outline!
            objective = Text(tracker, "coast-quest-objective", "", isDisplay: true);
            trackerDistance = Text(tracker, "coast-quest-distance", "", isDisplay: true);

            // --- AUTHENTIC GENSHIN PC [E] INTERACT PROMPT (3D KEYCAP + GAP + ROUNDED CONTEXT-ICON PILL) ---
            actionButton = new Button(OnActionClicked) { text = "", focusable = false };
            actionButton.AddToClassList("coast-action");
            actionButton.RegisterCallback<PointerEnterEvent>(_ => { actionHovered = true; DermagaUiAudio.PlayHover(); });
            actionButton.RegisterCallback<PointerLeaveEvent>(_ => { actionHovered = false; });
            actionButton.RegisterCallback<PointerDownEvent>(_ => TriggerActionPressAnim());
            container.Add(actionButton);

            actionKeyCap = Element(actionButton, "coast-keycap-wrap");
            actionKeyHalo = Element(actionKeyCap, "coast-keycap-halo");
            actionKeyHalo.generateVisualContent += DrawKeycapHalo;
            actionKey = Text(actionKeyCap, "coast-keycap", "E", isDisplay: true);

            actionArrow = Element(actionButton, "coast-action-arrow");
            actionArrow.generateVisualContent += DrawActionArrow;

            actionPill = Element(actionButton, "coast-action-pill");
            actionStripBg = Element(actionPill, "coast-action-strip-bg");
            actionStripBg.generateVisualContent += DrawActionStripBg;
            actionIcon = Element(actionPill, "coast-action-icon");
            prompt = Text(actionPill, "coast-action-label", "", isDisplay: true);

            // --- SLEEK TOP ADVISORY TOAST ---
            noticeToast = Element(container, "coast-notice");
            noticeBg = Element(noticeToast, "coast-notice-bg");
            noticeBg.generateVisualContent += DrawNoticeBg;
            noticeIcon = Element(noticeToast, "coast-notice-icon");
            if (infoIcon)
            {
                noticeIcon.style.backgroundImage = new StyleBackground(infoIcon);
            }
            noticeLabel = Text(noticeToast, "coast-notice-label", "", isDisplay: true);

            // --- GENSHIN WORLD / OFFSCREEN WAYPOINT BEACON ---
            marker = Element(container, "coast-marker");
            markerCanvas = Element(marker, "coast-marker-canvas");
            markerCanvas.generateVisualContent += DrawWaypointBeacon;
            markerDistance = Text(marker, "coast-marker-distance", isDisplay: true);

            // --- PAK DARMA OVERHEAD QUEST EMBLEM ---
            pakDarmaMarker = Element(container, "coast-npc-quest-marker");
            pakDarmaCanvas = Element(pakDarmaMarker, "coast-npc-quest-canvas");
            pakDarmaCanvas.generateVisualContent += DrawNpcEmblem;

            // --- CLEAN, ARCHITECTURALLY ALIGNED QUEST COMPLETION BANNER ---
            completion = Element(container, "coast-completion");
            completionBg = Element(completion, "coast-completion-bg");
            completionBg.generateVisualContent += DrawCelebrationBackdrop;

            Text(completion, "coast-completion-kicker", "MISI PESISIR SELESAI", isDisplay: true);
            Text(completion, "coast-completion-title", "Restorasi Karang Selesai", isDisplay: true);
            Text(completion, "coast-completion-note", "Koleksi Karang kini terbuka di Jurnal Pesisir", isDisplay: true);

            // Single perfectly centered tactile 3D CTA button (zero misaligned dual chips!)
            completionCta = new Button(OnOpenJournalFromBanner) { text = "", focusable = false };
            completionCta.AddToClassList("coast-completion-cta");
            Text(completionCta, "coast-cta-label", "Buka Jurnal Pesisir", isDisplay: true);
            completionCta.RegisterCallback<PointerEnterEvent>(_ => DermagaUiAudio.PlayHover());
            completionCta.RegisterCallback<PointerDownEvent>(_ => { completionCta.AddToClassList("sagara-ui-pressed"); DermagaUiAudio.PlayClick(); });
            completionCta.RegisterCallback<PointerUpEvent>(_ => completionCta.RemoveFromClassList("sagara-ui-pressed"));
            completionCta.RegisterCallback<PointerLeaveEvent>(_ => completionCta.RemoveFromClassList("sagara-ui-pressed"));
            completion.Add(completionCta);

            // --- SUBTLE FLOATING MC RESPONSE ---
            response = Element(container, "coast-response");
            Text(response, "coast-response-name", "MC");
            responseText = Text(response, "coast-response-text", "", isDisplay: true);

            actionVis = 0f;
            noticeVis = 0f;
            completionVis = 0f;
            Visible(actionButton, false);
            Visible(noticeToast, false);
            Visible(completion, false);
            Visible(marker, false);
            Visible(pakDarmaMarker, false);
            Visible(response, false);
        }

        private void ApplyBodyFont(VisualElement el)
        {
            if (bodyFontAsset)
            {
                el.style.unityFontDefinition = FontDefinition.FromSDFFont(bodyFontAsset);
            }
            else if (textFont)
            {
                el.style.unityFont = textFont;
            }
        }

        private void ApplyDisplayFont(VisualElement el)
        {
            if (displayFontAsset)
            {
                el.style.unityFontDefinition = FontDefinition.FromSDFFont(displayFontAsset);
            }
            else if (displayFont)
            {
                el.style.unityFont = displayFont;
            }
        }

        private void TriggerActionPressAnim()
        {
            actionPressTimer = 0.24f;
            actionRipplePhase = 0f;
            actionSweepPhase = 0f;
            DermagaUiAudio.PlayClick();
        }

        private void OnOpenJournalFromBanner()
        {
            if (coral)
            {
                coral.OpenJournalFromUi(openCoralTab: true);
            }
        }

        private void OnActionClicked()
        {
            TriggerActionPressAnim();
            if (lastHasNpcPrompt && npcInteraction)
            {
                npcInteraction.TryStartTalkTarget();
                return;
            }

            if (coral && !string.IsNullOrEmpty(coral.ActionPrompt) && coral.ActionPrompt.StartsWith("[E] ", System.StringComparison.Ordinal))
            {
                coral.RequestInteract();
                return;
            }
            if (mangroves && !string.IsNullOrEmpty(mangroves.ActionPrompt) && mangroves.ActionPrompt.StartsWith("[E] ", System.StringComparison.Ordinal))
            {
                mangroves.RequestInteract();
                return;
            }
            if (dockRepair && !string.IsNullOrEmpty(dockRepair.ActionPrompt) && dockRepair.ActionPrompt.StartsWith("[E] ", System.StringComparison.Ordinal))
            {
                dockRepair.RequestInteract();
                return;
            }
            if (trashInteraction && trashInteraction.Target != null)
            {
                trashInteraction.TryPickupTarget();
                return;
            }
            if (lastWasFishingPrompt && quest && quest.Fishing && quest.Fishing.NearbySpot != null)
            {
                quest.Fishing.TryBegin(quest.Fishing.NearbySpot);
            }
        }

        private static void PlaceSidebar(VisualElement sidebar, Rect bounds)
        {
            Rect safeArea = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0 || bounds.width <= 0f || bounds.height <= 0f)
            {
                return;
            }

            sidebar.style.left = 26f + bounds.width * safeArea.xMin / Screen.width;
            sidebar.style.top = 22f + bounds.height * (1f - safeArea.yMax / Screen.height);
        }

        private void LateUpdate()
        {
            var root = document.rootVisualElement;
            if (root == null)
            {
                return;
            }
            if (container.parent != root)
            {
                root.Add(container);
            }

            float dt = Time.unscaledDeltaTime;

            bool talking = npcInteraction && npcInteraction.IsTalking;
            if (dialogueView && dialogueView.enabled != talking)
            {
                dialogueView.enabled = talking;
            }

            bool overlayOpen = coral && coral.OverlayOpen;
            bool canInteract = !overlayOpen && quest.CanShowInteractionHint;
            var phase = quest.Phase;
            bool showQuest = !overlayOpen && !talking && !quest.FishingBusy && (quest.ShowQuest || phase == DermagaQuestPhase.MeetPakDarma
                || phase == DermagaQuestPhase.FishingUnlocked);
            bool escorting = coral && coral.WaitingForPakDarma;

            string action = mangroves && !string.IsNullOrEmpty(mangroves.ActionPrompt)
                ? mangroves.ActionPrompt : (dockRepair ? dockRepair.ActionPrompt : string.Empty);
            if (coral && !string.IsNullOrEmpty(coral.ActionPrompt))
            {
                action = coral.ActionPrompt;
            }
            if (string.IsNullOrEmpty(action))
            {
                action = quest.PickupPrompt;
            }

            NpcDialogue npc = npcInteraction ? npcInteraction.Target : null;
            bool hasNpcPrompt = npc && quest.IsPakDarma(npc);
            FishingSession fishing = quest ? quest.Fishing : null;
            bool isFishingPrompt = false;
            if (!hasNpcPrompt && string.IsNullOrEmpty(action) && canInteract
                && fishing && fishing.isActiveAndEnabled && !fishing.Busy && fishing.NearbySpot != null)
            {
                action = "[E] Memancing di Dermaga";
                isFishingPrompt = true;
            }

            bool hasKey = hasNpcPrompt || (!string.IsNullOrEmpty(action) && action.StartsWith("[E] ", System.StringComparison.Ordinal));
            string cleanLabel = hasNpcPrompt ? $"Bicara dengan {npc.DisplayName}"
                : hasKey ? action.Substring(4) : action;

            bool wantAction = canInteract && hasKey && !string.IsNullOrEmpty(cleanLabel);
            bool wantNotice = canInteract && !hasKey && !string.IsNullOrEmpty(cleanLabel);

            bool fishingJustTriggered = lastWasFishingPrompt && fishing && fishing.Busy;
            lastHasNpcPrompt = hasNpcPrompt;
            lastWasFishingPrompt = isFishingPrompt && wantAction;

            if ((wantAction || fishingJustTriggered) && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                TriggerActionPressAnim();
            }
            if (actionPressTimer > 0f)
            {
                actionPressTimer = Mathf.Max(0f, actionPressTimer - dt);
            }
            if (actionRipplePhase < 1f)
            {
                actionRipplePhase = Mathf.Min(1f, actionRipplePhase + dt * 3.0f);
            }
            if (actionSweepPhase < 1f)
            {
                actionSweepPhase = Mathf.Min(1f, actionSweepPhase + dt * 2.8f);
            }
            actionHoverBlend = Mathf.MoveTowards(actionHoverBlend, actionHovered ? 1f : 0f, dt * 10f);
            bool pressedDown = actionPressTimer > 0.08f;
            actionButton.EnableInClassList("coast-action-pressed", pressedDown);

            // Buttery-smooth Genshin-style spring slide-in + breathing glow + physical keycap press & pill sweep
            actionVis = Mathf.MoveTowards(actionVis, wantAction ? 1f : 0f, dt * (wantAction ? 7.2f : 10f));
            bool showAction = actionVis > .01f;
            Visible(actionButton, showAction);
            if (wantAction)
            {
                prompt.text = cleanLabel;
                UpdateActionContextIcon(hasNpcPrompt, cleanLabel, phase);
                PositionAction();
            }
            if (showAction)
            {
                // Cubic ease-out for silky Genshin slide-in from right (+14px -> 0px)
                float inv = 1f - actionVis;
                float ease = 1f - inv * inv * inv;
                float hoverEase = Smooth(actionHoverBlend);

                float slideX = (1f - ease) * 14f;
                float baseScale = Mathf.Lerp(0.93f, 1f, ease) * Mathf.Lerp(1f, 1.03f, hoverEase);
                float keyScale = 1f;
                float keyDrop = Mathf.Lerp(-1.5f, -2.2f, hoverEase);
                float pillScale = 1f;

                if (actionPressTimer > 0f)
                {
                    // Smooth spring press & rebound curve
                    float t = 1f - (actionPressTimer / 0.24f);
                    if (t < 0.38f)
                    {
                        float down = Smooth(t / 0.38f);
                        keyScale = Mathf.Lerp(1f, 0.88f, down);
                        keyDrop = Mathf.Lerp(-1.5f, 3.2f, down);
                        pillScale = Mathf.Lerp(1f, 0.965f, down);
                    }
                    else
                    {
                        float up = Smooth((t - 0.38f) / 0.62f);
                        float bounce = Mathf.Sin(up * Mathf.PI) * 0.04f;
                        keyScale = Mathf.Lerp(0.88f, 1f, up) + bounce;
                        keyDrop = Mathf.Lerp(3.2f, -1.5f, up);
                        pillScale = Mathf.Lerp(0.965f, 1f, up) + bounce * 0.5f;
                    }
                }

                actionButton.style.opacity = ease;
                actionButton.style.translate = new Translate(slideX, 0f, 0f);
                actionButton.style.scale = new Scale(new Vector3(baseScale, baseScale, 1f));
                actionKeyCap.style.scale = new Scale(new Vector3(keyScale, keyScale, 1f));
                actionKey.style.translate = new Translate(0f, keyDrop, 0f);
                float arrowNudge = hoverEase * 1.5f + (pressedDown ? 2.2f : 0f);
                actionArrow.style.translate = new Translate(arrowNudge, 0f, 0f);
                actionPill.style.scale = new Scale(new Vector3(pillScale, pillScale, 1f));
                actionKeyHalo.MarkDirtyRepaint();
                actionArrow.MarkDirtyRepaint();
                actionStripBg.MarkDirtyRepaint();
            }

            // Animate sleek top advisory toast
            noticeVis = Mathf.MoveTowards(noticeVis, wantNotice ? 1f : 0f, dt * 6.5f);
            bool showNotice = noticeVis > .01f;
            Visible(noticeToast, showNotice);
            if (wantNotice)
            {
                noticeLabel.text = cleanLabel;
            }
            if (showNotice)
            {
                float e = Smooth(noticeVis);
                noticeToast.style.opacity = e;
                noticeToast.style.translate = new Translate(0f, (1f - e) * -10f, 0f);
                noticeBg.MarkDirtyRepaint();
            }

            // Animate clean symmetrical Quest Complete Banner
            bool wantCompletion = canInteract && coral && coral.ShowRestorationNotice;
            completionVis = Mathf.MoveTowards(completionVis, wantCompletion ? 1f : 0f, dt * 3.4f);
            bool showCompletion = completionVis > .01f;
            Visible(completion, showCompletion);
            if (showCompletion)
            {
                float e = Smooth(completionVis);
                completion.style.opacity = e;
                float s = Mathf.Lerp(.93f, 1f, e);
                completion.style.scale = new Scale(new Vector3(s, s, 1f));
                completion.style.translate = new Translate(0f, (1f - e) * -12f, 0f);
                completionBg.MarkDirtyRepaint();
            }

            // Floating MC reply
            responseText.text = questAnimation ? questAnimation.Reply : string.Empty;
            Visible(response, !overlayOpen && responseText.text.Length > 0);

            // Quest tracker & markers
            Visible(tracker, showQuest);
            bool overhead = UpdatePakDarmaMarker(showQuest && (escorting || guidance.TargetIsPakDarma || (hasNpcPrompt && ShouldReport(phase))));
            UpdateDestination(showQuest && !wantAction && !overhead);

            bool dockRepaired = quest.FishingUnlocked || quest.DockRepairsCompleted >= DermagaIntroProgress.DockRepairCount;
            minimap.SetDockRepaired(dockRepaired);
            minimap.Update(!talking && !overlayOpen, guidance.Player, escorting ? quest.transform : guidance.CurrentTarget);
            if (!showQuest)
            {
                return;
            }

            questDiamond.MarkDirtyRepaint();
            title.text = phase >= DermagaQuestPhase.CollectCoralSupplies ? "Terumbu yang Tumbuh"
                : phase == DermagaQuestPhase.PlantMangroves || phase == DermagaQuestPhase.ReportMangroves
                    ? "Akar Kehidupan" : "Pesisir yang Pulih";
            objective.text = ObjectiveText(phase);

            bool hasDist = guidance && guidance.MarkerVisible && guidance.Distance >= 1.5f;
            Visible(trackerDistance, hasDist);
            if (hasDist)
            {
                trackerDistance.text = $"{Mathf.CeilToInt(guidance.Distance)}m";
            }
        }

        private void PositionAction()
        {
            var camera = Camera.main;
            if (!camera || !guidance || !guidance.Player)
            {
                return;
            }

            Vector3 point = camera.WorldToViewportPoint(guidance.Player.position + Vector3.up * .82f);
            float width = container.layout.width;
            float height = container.layout.height;
            if (width <= 0f || height <= 0f)
            {
                return;
            }

            bool left = point.x >= .66f;
            float slide = (1f - Smooth(actionVis)) * (left ? 14f : -14f);
            actionButton.style.left = Mathf.Clamp(point.x * width + (left ? -56f : 56f) + slide, 54f, width - 54f);
            actionButton.style.top = Mathf.Clamp((1f - point.y) * height + 8f, 108f, height - 68f);
            actionButton.style.translate = new Translate(left ? Length.Percent(-100f) : 0f, Length.Percent(-50f));
        }

        private string ObjectiveText(DermagaQuestPhase phase)
        {
            switch (phase)
            {
                case DermagaQuestPhase.MeetPakDarma:
                    return "Temui Pak Darma di depan rumahnya";
                case DermagaQuestPhase.CleanBeach:
                    return $"Bersihkan pantai ({quest.CollectedCount}/{DermagaIntroProgress.DebrisCount})";
                case DermagaQuestPhase.CollectWood:
                    return $"Kumpulkan ranting di hutan ({quest.BranchesCollected}/{DermagaIntroProgress.BranchCount})";
                case DermagaQuestPhase.RepairDock:
                    return $"Perbaiki bagian dermaga ({quest.DockRepairsCompleted}/{DermagaIntroProgress.DockRepairCount})";
                case DermagaQuestPhase.PlantMangroves:
                    return $"Tanam bibit mangrove di teluk ({quest.MangrovesPlanted}/{DermagaIntroProgress.MangroveCount})";
                case DermagaQuestPhase.CollectCoralSupplies:
                    return $"Siapkan perlengkapan ({quest.CoralSuppliesCollected}/{DermagaIntroProgress.CoralSupplyCount})";
                case DermagaQuestPhase.FishingUnlocked:
                    return "Bicara dengan Pak Darma untuk ekspedisi";
                case DermagaQuestPhase.BoardRestorationBoat:
                    return coral.WaitingForPakDarma ? "Ikuti Pak Darma ke perahu" : "Naik ke perahu restorasi";
                default:
                    return "Laporkan perkembangan ke Pak Darma";
            }
        }

        private static bool ShouldReport(DermagaQuestPhase phase)
        {
            switch (phase)
            {
                case DermagaQuestPhase.MeetPakDarma:
                case DermagaQuestPhase.ReportToPakDarma:
                case DermagaQuestPhase.ReportWood:
                case DermagaQuestPhase.ReportDock:
                case DermagaQuestPhase.FishingUnlocked:
                case DermagaQuestPhase.ReportMangroves:
                case DermagaQuestPhase.ReportCoralSupplies:
                    return true;
                default:
                    return false;
            }
        }

        private bool UpdatePakDarmaMarker(bool wanted)
        {
            Vector3 point = guidance.PakDarmaViewport;
            bool visible = wanted && point.z > 0f && point.x > .06f && point.x < .94f
                && point.y > .14f && point.y < .9f
                && container.layout.width > 1f && container.layout.height > 1f;
            Visible(pakDarmaMarker, visible);
            if (visible)
            {
                float bob = Mathf.Sin(Time.unscaledTime * 3.4f) * 3.5f;
                pakDarmaMarker.style.left = container.layout.width * point.x - 18f;
                pakDarmaMarker.style.top = container.layout.height * (1f - point.y) - 22f + bob;
                pakDarmaCanvas.MarkDirtyRepaint();
            }
            return visible;
        }

        private void UpdateDestination(bool visible)
        {
            if (!visible || !guidance.MarkerVisible || guidance.Distance < 1.35f
                || container.layout.width < 1f || container.layout.height < 1f)
            {
                Visible(marker, false);
                return;
            }

            Vector3 point = guidance.DestinationViewport;
            Vector2 direction = new Vector2(point.x - .5f, .5f - point.y);

            float width = container.layout.width;
            float height = container.layout.height;
            markerOffscreen = point.z <= 0.05f || Mathf.Abs(direction.x) > .43f || Mathf.Abs(direction.y) > .41f;
            if (markerOffscreen)
            {
                // Compute true camera-space screen direction directly (immune to perspective flip when target is south/behind camera!)
                Camera cam = Camera.main;
                if (cam && guidance.CurrentTarget)
                {
                    Vector3 local = cam.transform.InverseTransformPoint(guidance.CurrentTarget.position + Vector3.up * 0.9f);
                    Vector2 camDir = new Vector2(local.x, -local.y);
                    if (camDir.sqrMagnitude > 0.0001f)
                    {
                        direction = camDir.normalized;
                    }
                    else
                    {
                        direction = Vector2.up;
                    }
                }
                else if (point.z < 0f)
                {
                    direction = -direction;
                }

                float fit = Mathf.Max(Mathf.Abs(direction.x) / .43f, Mathf.Abs(direction.y) / .41f);
                direction /= Mathf.Max(.01f, fit);
            }

            marker.style.left = width * (.5f + direction.x) - 44f;
            marker.style.top = height * (.5f + direction.y) - 34f;
            Vector2 rawBearing = new Vector2(direction.x * width, direction.y * height);
            markerBearing = rawBearing.sqrMagnitude > .001f ? rawBearing.normalized : Vector2.right;
            markerCanvas.MarkDirtyRepaint();
            markerDistance.text = $"{Mathf.CeilToInt(guidance.Distance)} m";
            Visible(marker, true);
        }

        // --- CRISP AAA PAINTER2D VISUALS ---

        private static void DrawTrackerScrim(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 10f || r.height < 10f)
            {
                return;
            }
            // Feathered horizontal soft dark gradient behind quest text (100% HD readability, zero hard box!)
            var mesh = ctx.Allocate(6, 12);
            var left = new Color32(10, 28, 34, 128);
            var mid  = new Color32(10, 28, 34, 72);
            var right = new Color32(10, 28, 34, 0);
            float mx = r.width * 0.65f;
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, 0f, Vertex.nearZ), tint = left });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, r.height, Vertex.nearZ), tint = left });
            mesh.SetNextVertex(new Vertex { position = new Vector3(mx, 0f, Vertex.nearZ), tint = mid });
            mesh.SetNextVertex(new Vertex { position = new Vector3(mx, r.height, Vertex.nearZ), tint = mid });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, 0f, Vertex.nearZ), tint = right });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, r.height, Vertex.nearZ), tint = right });
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
            mesh.SetNextIndex(0); mesh.SetNextIndex(3); mesh.SetNextIndex(1);
            mesh.SetNextIndex(2); mesh.SetNextIndex(4); mesh.SetNextIndex(5);
            mesh.SetNextIndex(2); mesh.SetNextIndex(5); mesh.SetNextIndex(3);
        }

        private void UpdateActionContextIcon(bool hasNpcPrompt, string cleanLabel, DermagaQuestPhase phase)
        {
            Texture2D chosen = pickupHandIcon;
            actionIsQuestStyle = false;

            if (hasNpcPrompt)
            {
                bool isQuestTurnIn = (guidance && guidance.TargetIsPakDarma)
                    || ShouldReport(phase)
                    || phase == DermagaQuestPhase.MeetPakDarma;
                actionIsQuestStyle = isQuestTurnIn;
                chosen = isQuestTurnIn && questTalkIcon ? questTalkIcon : talkBubbleIcon;
            }
            else if (!string.IsNullOrEmpty(cleanLabel))
            {
                string low = cleanLabel.ToLowerInvariant();
                if (low.Contains("pancing") || low.Contains("mancing"))
                {
                    chosen = fishingIcon ? fishingIcon : (boatIcon ? boatIcon : pickupHandIcon);
                }
                else if (low.Contains("tanam") || low.Contains("mangrove") || low.Contains("bibit"))
                {
                    chosen = plantIcon ? plantIcon : pickupHandIcon;
                }
                else if (low.Contains("perbaiki") || low.Contains("dermaga") || low.Contains("pasang"))
                {
                    chosen = repairIcon ? repairIcon : pickupHandIcon;
                }
                else if (low.Contains("perahu") || low.Contains("naik") || low.Contains("berangkat") || low.Contains("ekspedisi"))
                {
                    chosen = boatIcon ? boatIcon : pickupHandIcon;
                }
                else if (low.Contains("bicara") || low.Contains("ngobrol") || low.Contains("sapa"))
                {
                    chosen = talkBubbleIcon ? talkBubbleIcon : pickupHandIcon;
                }
                else
                {
                    chosen = pickupHandIcon;
                }
            }

            if (chosen)
            {
                actionIcon.style.backgroundImage = new StyleBackground(chosen);
                actionIcon.style.display = DisplayStyle.Flex;
            }
            else
            {
                actionIcon.style.display = DisplayStyle.None;
            }
        }

        private void DrawActionArrow(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            float cx = r.width * 0.5f;
            float cy = r.height * 0.5f;
            float w = 4.2f;
            float h = 6.2f;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;

            // Soft drop shadow so the crisp white arrow pops even over bright sand
            p.fillColor = new Color(0.05f, 0.10f, 0.14f, 0.58f);
            p.BeginPath();
            p.MoveTo(new Vector2(cx - w + 0.6f, cy - h + 1.2f));
            p.LineTo(new Vector2(cx + w + 0.6f, cy + 1.2f));
            p.LineTo(new Vector2(cx - w + 0.6f, cy + h + 1.2f));
            p.ClosePath();
            p.Fill();

            // Crisp bright Genshin right-pointing triangle ▶
            p.fillColor = actionIsQuestStyle
                ? new Color(1f, 0.90f, 0.56f, 0.98f)
                : new Color(1f, 0.99f, 0.96f, 0.96f);
            p.BeginPath();
            p.MoveTo(new Vector2(cx - w, cy - h));
            p.LineTo(new Vector2(cx + w, cy));
            p.LineTo(new Vector2(cx - w, cy + h));
            p.ClosePath();
            p.Fill();
        }

        private void DrawActionStripBg(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 60f || r.height < 20f)
            {
                return;
            }
            // Authentic Genshin Impact PC Pill built as ONE unified vertex mesh (ZERO Painter2D/Allocate color-space seam!):
            // Semicircle left cap + 100% solid dark frosted backing behind icon & text + ultra-smooth 10-stop dissolve tail!
            float yTop = 0.5f;
            float yBot = r.height - 0.5f;
            float h = yBot - yTop;
            float rad = h * 0.5f;
            float cy = yTop + rad;

            // Fade starts cleanly AFTER the text (generous 96px smooth dissolve tail)
            float fadeW = Mathf.Min(104f, r.width * 0.36f);
            float xSolidEnd = Mathf.Max(rad + 20f, r.width - fadeW);

            float hover = Smooth(actionHoverBlend);
            float mainAlpha = Mathf.Lerp(0.80f, 0.90f, hover);
            byte bgR = 20, bgG = 36, bgB = 46;
            byte mainB = (byte)Mathf.RoundToInt(mainAlpha * 255f);

            // 1. Unified Pill Fill Mesh: 14-segment left semicircle + 10-stop smooth fade tail
            const int arcSegs = 14;
            const int fadeStops = 10;
            int totalVerts = (arcSegs + 2) + (fadeStops * 2);
            int totalIndices = (arcSegs * 3) + 6 + ((fadeStops - 1) * 6);
            var fillMesh = ctx.Allocate(totalVerts, totalIndices);

            var cSolid = new Color32(bgR, bgG, bgB, mainB);
            // Center of left semicircle (index 0)
            fillMesh.SetNextVertex(new Vertex { position = new Vector3(rad, cy, Vertex.nearZ), tint = cSolid });
            // Arc perimeter from top (270 deg) counterclockwise to bottom (90 deg) -> indices 1 .. arcSegs+1
            for (int i = 0; i <= arcSegs; i++)
            {
                float ang = Mathf.Lerp(Mathf.PI * 1.5f, Mathf.PI * 0.5f, i / (float)arcSegs);
                float px = rad + Mathf.Cos(ang) * rad;
                float py = cy + Mathf.Sin(ang) * rad;
                fillMesh.SetNextVertex(new Vertex { position = new Vector3(px, py, Vertex.nearZ), tint = cSolid });
            }
            // Horizontal fade strips starting from xSolidEnd -> r.width (indices arcSegs+2 .. end)
            int fadeBase = arcSegs + 2;
            for (int i = 0; i < fadeStops; i++)
            {
                float t = i / (float)(fadeStops - 1);
                float x = Mathf.Lerp(xSolidEnd, r.width, t);
                // Smooth cosine-squared falloff: zero kink at start (derivative=0 at t=0 and t=1!)
                float c = Mathf.Cos(t * Mathf.PI * 0.5f);
                float falloff = c * c;
                byte a = (byte)Mathf.RoundToInt(mainAlpha * falloff * 255f);
                var col = new Color32(bgR, bgG, bgB, a);
                fillMesh.SetNextVertex(new Vertex { position = new Vector3(x, yTop, Vertex.nearZ), tint = col });
                fillMesh.SetNextVertex(new Vertex { position = new Vector3(x, yBot, Vertex.nearZ), tint = col });
            }
            // Fan triangles for left semicircle
            for (int i = 0; i < arcSegs; i++)
            {
                fillMesh.SetNextIndex(0);
                fillMesh.SetNextIndex((ushort)(1 + i + 1));
                fillMesh.SetNextIndex((ushort)(1 + i));
            }
            // Solid rectangle connecting semicircle right edge (rad) to fade start (xSolidEnd)
            ushort arcTop = 1;
            ushort arcBot = (ushort)(1 + arcSegs);
            ushort f0Top = (ushort)fadeBase;
            ushort f0Bot = (ushort)(fadeBase + 1);
            fillMesh.SetNextIndex(arcTop); fillMesh.SetNextIndex(f0Top); fillMesh.SetNextIndex(f0Bot);
            fillMesh.SetNextIndex(arcTop); fillMesh.SetNextIndex(f0Bot); fillMesh.SetNextIndex(arcBot);
            // Smooth fade quads
            for (int seg = 0; seg < fadeStops - 1; seg++)
            {
                ushort a = (ushort)(fadeBase + seg * 2);
                ushort b = (ushort)(a + 1);
                ushort cIdx = (ushort)(a + 2);
                ushort d = (ushort)(a + 3);
                fillMesh.SetNextIndex(a); fillMesh.SetNextIndex(cIdx); fillMesh.SetNextIndex(d);
                fillMesh.SetNextIndex(a); fillMesh.SetNextIndex(d); fillMesh.SetNextIndex(b);
            }

            // 2. Press confirmation smooth luminous sheen sweep gliding left -> right inside pill
            if (actionSweepPhase < 0.99f)
            {
                float swEase = Smooth(actionSweepPhase);
                float cx = Mathf.Lerp(rad, r.width - 30f, swEase);
                float swW = 54f;
                float swA = Mathf.Sin(actionSweepPhase * Mathf.PI) * 0.34f;
                var sweep = ctx.Allocate(6, 12);
                byte sB = (byte)Mathf.RoundToInt(swA * 255f);
                var sZero = new Color32(255, 246, 206, 0);
                var sMid = new Color32(255, 246, 206, sB);
                float x0 = Mathf.Max(rad * 0.45f, cx - swW);
                float x1 = Mathf.Min(r.width - 10f, cx + swW);
                sweep.SetNextVertex(new Vertex { position = new Vector3(x0, yTop + 1.5f, Vertex.nearZ), tint = sZero });
                sweep.SetNextVertex(new Vertex { position = new Vector3(x0, yBot - 1.5f, Vertex.nearZ), tint = sZero });
                sweep.SetNextVertex(new Vertex { position = new Vector3(cx, yTop + 1.5f, Vertex.nearZ), tint = sMid });
                sweep.SetNextVertex(new Vertex { position = new Vector3(cx, yBot - 1.5f, Vertex.nearZ), tint = sMid });
                sweep.SetNextVertex(new Vertex { position = new Vector3(x1, yTop + 1.5f, Vertex.nearZ), tint = sZero });
                sweep.SetNextVertex(new Vertex { position = new Vector3(x1, yBot - 1.5f, Vertex.nearZ), tint = sZero });
                sweep.SetNextIndex(0); sweep.SetNextIndex(2); sweep.SetNextIndex(3);
                sweep.SetNextIndex(0); sweep.SetNextIndex(3); sweep.SetNextIndex(1);
                sweep.SetNextIndex(2); sweep.SetNextIndex(4); sweep.SetNextIndex(5);
                sweep.SetNextIndex(2); sweep.SetNextIndex(5); sweep.SetNextIndex(3);
            }

            // 3. Unified Crisp Genshin Semicircle Rim + Smooth Cosine Fade (ONE single ribbon mesh -> ZERO seam!)
            float rimAlpha = actionIsQuestStyle
                ? Mathf.Lerp(0.66f, 0.92f, hover)
                : Mathf.Lerp(0.48f, 0.78f, hover);
            byte rimR = (byte)(actionIsQuestStyle ? 255 : 246);
            byte rimG = (byte)(actionIsQuestStyle ? 216 : 244);
            byte rimB = (byte)(actionIsQuestStyle ? 112 : 236);
            float thick = 1.6f;
            float rOut = rad;
            float rIn = rad - thick;

            // Ribbon points: top right -> top solid -> left semicircle (top to bottom) -> bottom solid -> bottom right
            const int rFade = 8;
            int arcPts = arcSegs + 1;
            int rimCols = rFade + arcPts + rFade;
            var rimMesh = ctx.Allocate(rimCols * 2, (rimCols - 1) * 6);
            float rimStart = Mathf.Max(rad + 10f, xSolidEnd - 14f);
            float rimEnd = Mathf.Min(r.width - 4f, xSolidEnd + fadeW * 0.85f);

            // Top fade: right (t=1, alpha=0) -> left (t=0, alpha=rimAlpha)
            for (int i = 0; i < rFade; i++)
            {
                float t = 1f - i / (float)(rFade - 1);
                float x = Mathf.Lerp(rimStart, rimEnd, t);
                float c = Mathf.Cos(t * Mathf.PI * 0.5f);
                byte a = (byte)Mathf.RoundToInt(rimAlpha * c * c * 255f);
                var col = new Color32(rimR, rimG, rimB, a);
                rimMesh.SetNextVertex(new Vertex { position = new Vector3(x, yTop, Vertex.nearZ), tint = col });
                rimMesh.SetNextVertex(new Vertex { position = new Vector3(x, yTop + thick, Vertex.nearZ), tint = col });
            }
            // Left semicircle rim (full rimAlpha)
            var cRimFull = new Color32(rimR, rimG, rimB, (byte)Mathf.RoundToInt(rimAlpha * 255f));
            for (int i = 0; i < arcPts; i++)
            {
                float ang = Mathf.Lerp(Mathf.PI * 1.5f, Mathf.PI * 0.5f, i / (float)arcSegs);
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                rimMesh.SetNextVertex(new Vertex { position = new Vector3(rad + ca * rOut, cy + sa * rOut, Vertex.nearZ), tint = cRimFull });
                rimMesh.SetNextVertex(new Vertex { position = new Vector3(rad + ca * rIn, cy + sa * rIn, Vertex.nearZ), tint = cRimFull });
            }
            // Bottom fade: left (t=0, alpha=rimAlpha) -> right (t=1, alpha=0)
            for (int i = 0; i < rFade; i++)
            {
                float t = i / (float)(rFade - 1);
                float x = Mathf.Lerp(rimStart, rimEnd, t);
                float c = Mathf.Cos(t * Mathf.PI * 0.5f);
                byte a = (byte)Mathf.RoundToInt(rimAlpha * c * c * 255f);
                var col = new Color32(rimR, rimG, rimB, a);
                rimMesh.SetNextVertex(new Vertex { position = new Vector3(x, yBot, Vertex.nearZ), tint = col });
                rimMesh.SetNextVertex(new Vertex { position = new Vector3(x, yBot - thick, Vertex.nearZ), tint = col });
            }
            for (int seg = 0; seg < rimCols - 1; seg++)
            {
                ushort a = (ushort)(seg * 2);
                ushort b = (ushort)(a + 1);
                ushort cIdx = (ushort)(a + 2);
                ushort d = (ushort)(a + 3);
                rimMesh.SetNextIndex(a); rimMesh.SetNextIndex(cIdx); rimMesh.SetNextIndex(d);
                rimMesh.SetNextIndex(a); rimMesh.SetNextIndex(d); rimMesh.SetNextIndex(b);
            }
        }

        private void DrawKeycapHalo(MeshGenerationContext ctx)
        {
            // Clean keycap at rest (ZERO outline clutter around [E]!); only fire subtle golden wave when pressed!
            if (actionRipplePhase >= 0.99f)
            {
                return;
            }
            var p = ctx.painter2D;
            Vector2 c = ctx.visualElement.contentRect.center;
            float ease = Smooth(actionRipplePhase);
            float alpha = (1f - actionRipplePhase) * 0.88f;
            float expand = Mathf.Lerp(21f, 34f, ease);
            p.strokeColor = new Color(1f, .90f, .46f, alpha);
            p.lineWidth = Mathf.Lerp(2.6f, 0.7f, actionRipplePhase);
            DrawRoundedRect(p, new Rect(c.x - expand, c.y - expand, expand * 2f, expand * 2f), Mathf.Lerp(10f, 15f, ease));
            p.Stroke();
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

        private static void DrawNoticeBg(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 40f || r.height < 12f)
            {
                return;
            }
            float fade = Mathf.Min(64f, r.width * 0.16f);
            var mesh = ctx.Allocate(8, 18);
            var clear = new Color32(16, 40, 48, 0);
            var solid = new Color32(16, 40, 48, 224);
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
            p.strokeColor = new Color(.98f, .84f, .46f, .78f);
            p.lineWidth = 1.4f;
            p.BeginPath();
            p.MoveTo(new Vector2(fade * 0.7f, 1f));
            p.LineTo(new Vector2(r.width - fade * 0.7f, 1f));
            p.MoveTo(new Vector2(fade * 0.7f, r.height - 1f));
            p.LineTo(new Vector2(r.width - fade * 0.7f, r.height - 1f));
            p.Stroke();
        }

        private void DrawQuestDiamond(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            Vector2 c = ctx.visualElement.contentRect.center;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            p.lineJoin = LineJoin.Round;

            p.fillColor = new Color(1f, .84f, .38f, .22f + .18f * pulse);
            DrawDiamond(p, c, 9.8f);
            p.Fill();

            p.fillColor = new Color(.98f, .78f, .28f, 1f);
            p.strokeColor = new Color(.36f, .22f, .06f, .92f);
            p.lineWidth = 1.4f;
            DrawDiamond(p, c, 7.2f);
            p.Fill();
            p.Stroke();

            p.fillColor = new Color(1f, .98f, .88f, 1f);
            DrawDiamond(p, c, 3.3f);
            p.Fill();
        }

        private static void DrawGoldenRule(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width <= 4f)
            {
                return;
            }
            var mesh = ctx.Allocate(4, 6);
            var left = new Color32(252, 218, 128, 245);
            var right = new Color32(252, 218, 128, 0);
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, 0f, Vertex.nearZ), tint = left });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, 0f, Vertex.nearZ), tint = right });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, r.height, Vertex.nearZ), tint = right });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, r.height, Vertex.nearZ), tint = left });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
        }

        private void DrawWaypointBeacon(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            Vector2 c = ctx.visualElement.contentRect.center;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 4.0f);
            p.lineJoin = LineJoin.Round;

            p.strokeColor = new Color(1f, .88f, .46f, .52f * (1f - pulse));
            p.lineWidth = 1.8f;
            p.BeginPath();
            p.Arc(c, 10.5f + pulse * 6.5f, 0f, 360f);
            p.Stroke();

            p.fillColor = new Color(.06f, .12f, .14f, .55f);
            DrawDiamond(p, c + new Vector2(0f, 1.8f), 11.4f);
            p.Fill();

            p.fillColor = new Color(.98f, .78f, .28f, 1f);
            p.strokeColor = new Color(.36f, .22f, .06f, .95f);
            p.lineWidth = 1.8f;
            DrawDiamond(p, c, 10.4f);
            p.Fill();
            p.Stroke();

            p.fillColor = new Color(1f, .99f, .90f, 1f);
            DrawDiamond(p, c, 4.9f);
            p.Fill();

            if (markerOffscreen)
            {
                Vector2 dir = markerBearing;
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 basePt = c + dir * 13.5f;
                Vector2 tipPt = c + dir * 21.5f;

                p.fillColor = new Color(1f, .92f, .58f, 1f);
                p.strokeColor = new Color(.30f, .19f, .06f, .95f);
                p.lineWidth = 1.4f;
                p.BeginPath();
                p.MoveTo(tipPt);
                p.LineTo(basePt + side * 5.2f);
                p.LineTo(c + dir * 15.2f);
                p.LineTo(basePt - side * 5.2f);
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }
        }

        private void DrawNpcEmblem(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            Vector2 c = ctx.visualElement.contentRect.center;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 3.8f);
            p.lineJoin = LineJoin.Round;

            p.fillColor = new Color(1f, .86f, .42f, .24f + .16f * pulse);
            p.BeginPath();
            p.Arc(c, 13.5f + pulse * 2.2f, 0f, 360f);
            p.Fill();

            p.fillColor = new Color(.08f, .12f, .12f, .42f);
            DrawDiamond(p, c + new Vector2(0f, 2f), 11.2f);
            p.Fill();

            p.fillColor = new Color(.99f, .80f, .30f, 1f);
            p.strokeColor = new Color(.38f, .23f, .07f, .95f);
            p.lineWidth = 1.8f;
            DrawDiamond(p, c, 10.6f);
            p.Fill();
            p.Stroke();

            p.fillColor = new Color(1f, .98f, .88f, 1f);
            DrawDiamond(p, c, 4.8f);
            p.Fill();
        }

        private void DrawCelebrationBackdrop(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width <= 20f || r.height <= 20f)
            {
                return;
            }

            // Clean, architecturally symmetrical Genshin-style feathered ribbon (ZERO overlapping clutter stars!)
            float fade = Mathf.Min(130f, r.width * .24f);
            float topY = 12f;
            float botY = r.height - 6f;
            var mesh = ctx.Allocate(8, 18);
            var clear = new Color32(12, 38, 46, 0);
            var solid = new Color32(12, 38, 46, 232);

            float[] xs = { 0f, fade, r.width - fade, r.width };
            Color32[] cols = { clear, solid, solid, clear };
            for (int i = 0; i < 4; i++)
            {
                mesh.SetNextVertex(new Vertex { position = new Vector3(xs[i], topY, Vertex.nearZ), tint = cols[i] });
                mesh.SetNextVertex(new Vertex { position = new Vector3(xs[i], botY, Vertex.nearZ), tint = cols[i] });
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
            float cx = r.width * .5f;
            float span = r.width * .38f;
            p.lineJoin = LineJoin.Round;

            // Symmetrical top golden border interrupted cleanly by centered diamond emblem
            p.strokeColor = new Color(.98f, .84f, .46f, .92f);
            p.lineWidth = 1.8f;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - span, topY));
            p.LineTo(new Vector2(cx - 16f, topY));
            p.MoveTo(new Vector2(cx + 16f, topY));
            p.LineTo(new Vector2(cx + span, topY));
            // Symmetrical bottom golden border
            p.MoveTo(new Vector2(cx - span * .86f, botY));
            p.LineTo(new Vector2(cx + span * .86f, botY));
            p.Stroke();

            // Subtle sweeping glint strictly along the top border (never over text!)
            float sweep = Mathf.PingPong(Time.unscaledTime * 0.45f, 1f);
            float gx = Mathf.Lerp(cx - span + 24f, cx + span - 24f, sweep);
            if (Mathf.Abs(gx - cx) > 20f)
            {
                p.strokeColor = new Color(1f, .98f, .86f, .78f);
                p.lineWidth = 2.6f;
                p.BeginPath();
                p.MoveTo(new Vector2(gx - 18f, topY));
                p.LineTo(new Vector2(gx + 18f, topY));
                p.Stroke();
            }

            // Crisp centered golden double-diamond crest sitting cleanly ON the top border
            p.fillColor = new Color(.98f, .78f, .28f, 1f);
            p.strokeColor = new Color(.34f, .22f, .06f, .95f);
            p.lineWidth = 1.5f;
            DrawDiamond(p, new Vector2(cx, topY), 9.5f);
            p.Fill();
            p.Stroke();
            p.fillColor = new Color(1f, .98f, .88f, 1f);
            DrawDiamond(p, new Vector2(cx, topY), 4.4f);
            p.Fill();
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

        private static float Smooth(float value) => value * value * (3f - 2f * value);

        private static VisualElement Element(VisualElement parent, string styleClass)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(styleClass);
            parent.Add(element);
            return element;
        }

        private Label Text(VisualElement parent, string styleClass, string text = "", bool isDisplay = false)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(styleClass);
            if (isDisplay)
            {
                ApplyDisplayFont(label);
            }
            parent.Add(label);
            return label;
        }

        private static void Visible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnDisable()
        {
            container?.RemoveFromHierarchy();
            if (dialogueView && dialogueView.gameObject.activeInHierarchy && gameObject.scene.isLoaded)
            {
                dialogueView.enabled = true;
            }
        }
    }
}
