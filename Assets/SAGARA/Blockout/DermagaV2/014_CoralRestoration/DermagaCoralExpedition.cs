using System;
using System.Collections;
using System.Collections.Generic;
using Sagara.Dermaga.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Sagara.DermagaV2
{
    [DefaultExecutionOrder(35)]
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class DermagaCoralExpedition : MonoBehaviour
    {
        [Serializable]
        private sealed class Supply
        {
            public int slot;
            public string displayName;
            public Transform approach;
            public GameObject prop;
        }

        [Serializable]
        internal sealed class CoralRecord
        {
            public string id;
            public string displayName;
            [TextArea] public string description;
            public Color accent;
            public Texture2D portrait;
        }

        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private DermagaQuestAnimation questAnimation;
        [SerializeField] private DermagaPakDarmaEscort escort;
        [SerializeField] private FishingSession fishing;
        [SerializeField] private PlayerNpcInteraction dialogue;
        [SerializeField] private FishingVisualCatalog fishCatalog;
        [SerializeField] private Supply[] supplies = Array.Empty<Supply>();
        [SerializeField] private Transform boardingPoint;
        [SerializeField] private Transform dockReturn;
        [SerializeField] private Transform restorationBoat;
        [SerializeField] private DermagaCoralVideoCutscene coralVideoCutscene;
        [SerializeField] private DermagaCoral3DCutscene coral3DCutscene;
        [SerializeField] private Texture2D[] illustrations = Array.Empty<Texture2D>();
        [SerializeField] private StyleSheet overlayStyle;
        [SerializeField] private Shader journalPreviewShader;
        [SerializeField] private Font displayFont;
        [SerializeField] private Font bodyFont;
        [SerializeField] private UnityEngine.TextCore.Text.FontAsset displayFontAsset;
        [SerializeField] private UnityEngine.TextCore.Text.FontAsset bodyFontAsset;
        [SerializeField] private CoralRecord[] coralRecords = Array.Empty<CoralRecord>();
        [SerializeField, Range(0f, 1f)] private float coralDiscoveryChance = .22f;

        [Header("Cozy Journal UI Icons")]
        [SerializeField] private Texture2D journalIcon;
        [SerializeField] private Texture2D fishIcon;
        [SerializeField] private Texture2D coralIcon;
        [SerializeField] private Texture2D lockIcon;
        [SerializeField] private Texture2D closeIcon;
        [SerializeField] private Texture2D rotateIcon;
        [SerializeField] private Texture2D bookSpreadTexture;

        private enum Modal
        {
            None,
            Journal,
            Expedition
        }

        private readonly System.Random discoveryRandom = new System.Random();
        private Modal modal;
        private DermagaCoralOverlay overlay;
        private LivingFishingView fishingView;
        private bool suppliesVisible;
        private bool movementWasEnabled;
        private bool dialogueWasEnabled;
        private bool[] spotsWereEnabled;
        private float restorationNoticeUntil;
        private bool uiInteractQueued;

        // 3D Boat Boarding & Sailing Cinematic State
        private bool sailingActive;
        private Coroutine sailingCoroutine;
        private Vector3 boatRestPos;
        private Quaternion boatRestRot;
        private bool boatSaved;
        private Vector3 pakDarmaDockPos;
        private Quaternion pakDarmaDockRot;
        private bool pakDarmaSaved;
        private readonly List<GameObject> activeWakes = new List<GameObject>();

        internal void RequestInteract() => uiInteractQueued = true;

        internal bool WaitingForPakDarma => quest.Phase == DermagaQuestPhase.BoardRestorationBoat
            && !escort.HasArrived;
        public bool OverlayOpen => modal != Modal.None;
        public bool ShowRestorationNotice => Time.unscaledTime < restorationNoticeUntil;
        public string ActionPrompt { get; private set; } = string.Empty;

        private void Awake()
        {
            fishingView = fishing.GetComponentInChildren<LivingFishingView>();
            UpdateSupplyVisibility();
        }

        private void OnEnable()
        {
            fishing.ResultRecorded += RecordCatch;
        }

        private void Start()
        {
            var icons = new DermagaCoralOverlay.Icons
            {
                journal = journalIcon,
                fish = fishIcon,
                coral = coralIcon,
                locked = lockIcon,
                close = closeIcon,
                rotate = rotateIcon,
                bookSpread = bookSpreadTexture
            };
            overlay = new DermagaCoralOverlay(GetComponent<UIDocument>().rootVisualElement, overlayStyle,
                displayFont, bodyFont, displayFontAsset, bodyFontAsset, illustrations, journalPreviewShader, icons, OpenJournal, CloseJournal,
                () => overlay.AdvanceExpedition(FinishExpedition), FinishExpedition);
        }

        internal void OpenJournalFromUi(bool openCoralTab)
        {
            if (modal != Modal.None || !quest.FishingUnlocked || !CanOpenOverlay())
            {
                return;
            }
            HoldControls(Modal.Journal);
            overlay.OpenJournal(quest, fishing.configuration, fishCatalog, coralRecords, openCoralTab);
        }

        private void OnDisable()
        {
            fishing.ResultRecorded -= RecordCatch;
            if (sailingCoroutine != null)
            {
                StopCoroutine(sailingCoroutine);
                sailingCoroutine = null;
            }
            if (sailingActive)
            {
                sailingActive = false;
                RestoreBoatAndCrewAfterExpedition();
            }
            overlay?.DisposePreview();
            if (OverlayOpen)
            {
                ReleaseControls();
            }
        }

        private void Update()
        {
            ActionPrompt = string.Empty;
            var keyboard = Keyboard.current;
            if (sailingActive || DermagaCoralVideoCutscene.IsPlaying || DermagaCoral3DCutscene.IsPlaying)
            {
                // Allow skipping boat sailing directly into the video cutscene with Esc
                if (sailingActive && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    SkipSailingIntoCutscene();
                }
                return;
            }
            if (modal == Modal.Expedition)
            {
                overlay.UpdateExpedition();
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    FinishExpedition();
                }
                else if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                {
                    overlay.AdvanceExpedition(FinishExpedition);
                }
                return;
            }

            if (modal == Modal.Journal)
            {
                overlay.UpdateJournal();
                if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame))
                {
                    CloseJournal();
                }
                return;
            }

            overlay.UpdateHudFeedback();

            if (suppliesVisible != (quest.Phase == DermagaQuestPhase.CollectCoralSupplies))
            {
                UpdateSupplyVisibility();
            }

            bool canInteract = CanOpenOverlay();
            overlay.SetJournalShortcut(canInteract && fishing.NearbySpot == null, quest.FishingUnlocked);
            if (!canInteract)
            {
                return;
            }

            if (keyboard != null && keyboard.jKey.wasPressedThisFrame)
            {
                if (quest.FishingUnlocked)
                {
                    overlay.PulseJournalShortcut();
                    OpenJournal();
                }
                else
                {
                    overlay.ShowShortcutLockedToast("Jurnal Pesisir terbuka setelah perbaikan dermaga selesai.");
                }
                return;
            }

            // Near an idle NPC, J can open the journal, but E still belongs to the NPC.
            if (dialogue.Target != null)
            {
                uiInteractQueued = false;
                return;
            }

            bool pressed = (keyboard != null && keyboard.eKey.wasPressedThisFrame) || uiInteractQueued;
            uiInteractQueued = false;
            if (suppliesVisible)
            {
                foreach (Supply supply in supplies)
                {
                    if (quest.HasCoralSupply(supply.slot) || !IsNear(supply.approach))
                    {
                        continue;
                    }

                    ActionPrompt = "[E] Ambil " + supply.displayName;
                    if (pressed && questAnimation.TryPickupAt(supply.prop.transform.position, () => Collect(supply)))
                    {
                        ActionPrompt = string.Empty;
                    }
                    return;
                }
            }

            if (quest.Phase == DermagaQuestPhase.BoardRestorationBoat && !WaitingForPakDarma && IsNear(boardingPoint))
            {
                ActionPrompt = ArtReady() ? "[E] Naik Perahu Restorasi" : "Ilustrasi ekspedisi belum tersedia.";
                if (pressed && ArtReady())
                {
                    BeginBoatBoardingAndSailing();
                }
            }
        }

        private void Collect(Supply supply)
        {
            if (quest.TryCollectCoralSupply(supply.slot))
            {
                supply.prop.SetActive(false);
            }
        }

        private bool CanOpenOverlay()
        {
            // The catch card keeps fading briefly after FishingSession returns to Idle.
            bool resultVisible = DermagaFishingHud.ResultOrFailureVisible
                || (fishingView && fishingView.isActiveAndEnabled
                    && (fishingView.result.group.alpha > .005f || fishingView.failure.group.alpha > .005f));
            return quest.CanShowInteractionHint && !resultVisible;
        }

        private bool IsNear(Transform target)
        {
            Vector3 offset = fishing.player.transform.position - target.position;
            return fishing.player.isGrounded && Mathf.Abs(offset.y) < .7f
                && new Vector2(offset.x, offset.z).sqrMagnitude <= 1.25f * 1.25f;
        }

        private void UpdateSupplyVisibility()
        {
            suppliesVisible = quest.Phase == DermagaQuestPhase.CollectCoralSupplies;
            foreach (Supply supply in supplies)
            {
                supply.prop.SetActive(suppliesVisible && !quest.HasCoralSupply(supply.slot));
            }
        }

        private bool ArtReady()
        {
            return illustrations.Length == 3 && illustrations[0] && illustrations[1] && illustrations[2];
        }

        private void OpenJournal()
        {
            if (OverlayOpen || !CanOpenOverlay())
            {
                return;
            }
            if (!quest.FishingUnlocked)
            {
                overlay.ShowShortcutLockedToast("Jurnal Pesisir terbuka setelah perbaikan dermaga selesai.");
                return;
            }

            HoldControls(Modal.Journal);
            overlay.OpenJournal(quest, fishing.configuration, fishCatalog, coralRecords);
        }

        private void CloseJournal()
        {
            if (modal == Modal.Journal)
            {
                overlay.CloseJournal(ReleaseControls);
            }
        }

        private void BeginBoatBoardingAndSailing()
        {
            HoldControls(Modal.Expedition);
            if (!restorationBoat)
            {
                var found = GameObject.Find("MuyayaFishingBoat_Primary");
                if (found) restorationBoat = found.transform;
            }
            if (!restorationBoat)
            {
                overlay.OpenExpedition();
                return;
            }

            sailingActive = true;
            sailingCoroutine = StartCoroutine(BoatBoardingAndSailingRoutine());
        }

        private void SkipSailingIntoCutscene()
        {
            if (!sailingActive) return;
            if (sailingCoroutine != null)
            {
                StopCoroutine(sailingCoroutine);
                sailingCoroutine = null;
            }
            sailingActive = false;
            DermagaAudioDirector.SetBoatEngine(false);
            CleanupWakeRings();
            LaunchCoralCutsceneOrFallback();
        }

        private IEnumerator BoatBoardingAndSailingRoutine()
        {
            var boat = restorationBoat;
            var playerCc = fishing.player;
            var playerTr = playerCc.transform;
            var pakTr = escort ? escort.transform : null;

            boatRestPos = boat.position;
            boatRestRot = boat.rotation;
            boatSaved = true;
            if (pakTr)
            {
                pakDarmaDockPos = pakTr.position;
                pakDarmaDockRot = pakTr.rotation;
                pakDarmaSaved = true;
            }

            // Exact recessed orange deck floor local coordinates (floor surface is at local Y = -0.50, X = 0.75..1.95, Z = -0.95..+0.95!)
            Vector3 pakLocalSeat = new Vector3(1.36f, -0.49f, 0.42f);
            Vector3 playerLocalSeat = new Vector3(1.36f, -0.49f, -0.42f);

            playerCc.enabled = false;
            if (escort) escort.enabled = false;

            // Pause camera follow so we can smoothly track the sailing boat
            var mainCam = Camera.main;
            var camFollow = mainCam ? mainCam.GetComponent<PlayerCameraFollow>() : null;
            var camPres = mainCam ? mainCam.GetComponent<DermagaCameraPresentation>() : null;
            var camClamp = mainCam ? mainCam.GetComponent<DermagaEntranceWideZoomClamp>() : null;
            Vector3 camOffset = mainCam ? (mainCam.transform.position - boat.position) : new Vector3(0f, 12f, -10f);
            if (camFollow) camFollow.enabled = false;
            if (camPres) camPres.enabled = false;
            if (camClamp) camClamp.enabled = false;

            // PHASE 1: Both Player & Pak Darma hop smoothly onto the boat deck (0.78s)
            Vector3 pStart = playerTr.position;
            Quaternion pStartRot = playerTr.rotation;
            Vector3 dStart = pakTr ? pakTr.position : Vector3.zero;
            Quaternion dStartRot = pakTr ? pakTr.rotation : Quaternion.identity;

            Vector3 pDeck = boat.TransformPoint(playerLocalSeat);
            Vector3 dDeck = boat.TransformPoint(pakLocalSeat);
            Quaternion crewFacing = Quaternion.LookRotation(-boat.right, Vector3.up);

            const float hopDur = 0.78f;
            float elapsed = 0f;
            while (elapsed < hopDur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / hopDur);
                float smooth = u * u * (3f - 2f * u);
                float arc = Mathf.Sin(u * Mathf.PI) * 1.15f;

                playerTr.position = Vector3.Lerp(pStart, pDeck, smooth) + Vector3.up * arc;
                playerTr.rotation = Quaternion.Slerp(pStartRot, crewFacing, smooth);
                if (pakTr)
                {
                    float uD = Mathf.Clamp01((elapsed - 0.08f) / (hopDur - 0.08f));
                    float smD = uD * uD * (3f - 2f * uD);
                    float arcD = Mathf.Sin(uD * Mathf.PI) * 1.05f;
                    pakTr.position = Vector3.Lerp(dStart, dDeck, smD) + Vector3.up * arcD;
                    pakTr.rotation = Quaternion.Slerp(dStartRot, crewFacing, smD);
                }
                if (mainCam)
                {
                    mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, boat.position + camOffset, Time.deltaTime * 4f);
                }
                yield return null;
            }

            // Landed on deck! Soft wooden thud + cozy harbor horn + boat engine starts
            DermagaAudioDirector.PlayBoatBoard();
            DermagaAudioDirector.PlayBoatHorn();
            DermagaAudioDirector.SetBoatEngine(true);

            // PHASE 2: Unmoor & sail smoothly out into the open turquoise ocean (2.85s)
            const float sailDur = 2.85f;
            float sailT = 0f;
            float wakeCooldown = 0f;
            float startYaw = boatRestRot.eulerAngles.y;
            float targetYaw = startYaw - 82f; // Curves bow (-boat.right) cleanly straight South down the open turquoise channel!

            Vector3 currentPos = boatRestPos;
            while (sailT < sailDur)
            {
                float dt = Time.deltaTime;
                sailT += dt;
                float u = Mathf.Clamp01(sailT / sailDur);

                // Gentle landing dip then ocean bob & playful wave roll
                float landDip = sailT < 0.35f ? -Mathf.Sin((sailT / 0.35f) * Mathf.PI) * 0.14f : 0f;
                float bob = Mathf.Sin(sailT * 4.2f) * 0.08f + landDip;
                float roll = Mathf.Sin(sailT * 3.1f) * 2.4f;
                float pitch = Mathf.Lerp(0f, -3.2f, Mathf.SmoothStep(0f, 1f, u)); // Bow lifts slightly as boat accelerates!

                float yaw = Mathf.Lerp(startYaw, targetYaw, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 1.15f)));
                Quaternion flatRot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 bowDir = -(flatRot * Vector3.right);
                float speed = Mathf.Lerp(0.4f, 5.4f, Mathf.SmoothStep(0f, 1f, u));
                currentPos += bowDir * (speed * dt);

                boat.position = new Vector3(currentPos.x, boatRestPos.y + bob, currentPos.z);
                boat.rotation = Quaternion.Euler(roll, yaw, pitch);

                // Keep Player & Pak Darma riding smoothly on deck
                Quaternion deckFace = Quaternion.LookRotation(bowDir, Vector3.up);
                playerTr.SetPositionAndRotation(boat.TransformPoint(playerLocalSeat), deckFace);
                if (pakTr)
                {
                    pakTr.SetPositionAndRotation(boat.TransformPoint(pakLocalSeat), deckFace);
                }

                // Smooth camera tracking
                if (mainCam)
                {
                    Vector3 desiredCam = boat.position + camOffset + bowDir * 1.4f;
                    mainCam.transform.position = Vector3.Lerp(mainCam.transform.position, desiredCam, dt * 3.6f);
                }

                // Stylized white seafoam wake rings behind the stern
                wakeCooldown -= dt;
                if (wakeCooldown <= 0f && u > 0.10f)
                {
                    wakeCooldown = 0.24f;
                    SpawnWakeRing(boat.TransformPoint(new Vector3(2.75f, -0.62f, 0f)));
                }
                UpdateWakeRings(dt);

                yield return null;
            }

            // PHASE 3: Gentle splash & seamless transition into the Real-Time Cozy 3D Underwater Cutscene!
            DermagaAudioDirector.SetBoatEngine(false);
            DermagaAudioDirector.PlayWaterSplash();
            CleanupWakeRings();
            sailingActive = false;
            sailingCoroutine = null;
            LaunchCoralCutsceneOrFallback();
        }

        private void LaunchCoralCutsceneOrFallback()
        {
            if (!coralVideoCutscene)
            {
                coralVideoCutscene = FindFirstObjectByType<DermagaCoralVideoCutscene>();
            }
            if (coralVideoCutscene && coralVideoCutscene.HasVideo)
            {
                coralVideoCutscene.PlayCutscene(CompleteExpeditionReturnToDock);
                return;
            }
            overlay.OpenExpedition();
        }

        private void CompleteExpeditionReturnToDock()
        {
            RestoreBoatAndCrewAfterExpedition();
            var player = fishing.player;
            player.enabled = false;
            player.transform.SetPositionAndRotation(dockReturn.position, dockReturn.rotation);
            player.enabled = true;
            ReleaseControls();
            quest.CompleteCoralExpedition();
            restorationNoticeUntil = Time.unscaledTime + 6.5f;
        }

        private void SpawnWakeRing(Vector3 worldPos)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "BoatWakeFoam";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = new Vector3(worldPos.x, -0.92f, worldPos.z);
            ring.transform.localScale = new Vector3(0.85f, 0.02f, 0.85f);
            var mr = ring.GetComponent<MeshRenderer>();
            if (mr)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.SetColor("_BaseColor", new Color(0.92f, 0.99f, 1f, 0.72f));
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            activeWakes.Add(ring);
        }

        private void UpdateWakeRings(float dt)
        {
            for (int i = activeWakes.Count - 1; i >= 0; i--)
            {
                var w = activeWakes[i];
                if (!w) { activeWakes.RemoveAt(i); continue; }
                Vector3 s = w.transform.localScale;
                s.x += dt * 1.9f;
                s.z += dt * 1.9f;
                s.y = Mathf.Max(0.001f, s.y - dt * 0.015f);
                w.transform.localScale = s;
                if (s.x > 3.2f)
                {
                    Destroy(w);
                    activeWakes.RemoveAt(i);
                }
            }
        }

        private void CleanupWakeRings()
        {
            for (int i = 0; i < activeWakes.Count; i++)
            {
                if (activeWakes[i]) Destroy(activeWakes[i]);
            }
            activeWakes.Clear();
        }

        private void RestoreBoatAndCrewAfterExpedition()
        {
            DermagaAudioDirector.SetBoatEngine(false);
            CleanupWakeRings();
            if (boatSaved && restorationBoat)
            {
                restorationBoat.SetPositionAndRotation(boatRestPos, boatRestRot);
            }
            if (pakDarmaSaved && escort)
            {
                escort.transform.SetPositionAndRotation(pakDarmaDockPos, pakDarmaDockRot);
                escort.enabled = true;
            }
            var mainCam = Camera.main;
            if (mainCam)
            {
                var camFollow = mainCam.GetComponent<PlayerCameraFollow>();
                var camPres = mainCam.GetComponent<DermagaCameraPresentation>();
                var camClamp = mainCam.GetComponent<DermagaEntranceWideZoomClamp>();
                if (camFollow) camFollow.enabled = true;
                if (camPres) camPres.enabled = true;
                if (camClamp) camClamp.enabled = true;
            }
        }

        private void FinishExpedition()
        {
            if (modal != Modal.Expedition || overlay.Exiting)
            {
                return;
            }

            overlay.CloseExpedition(() =>
            {
                RestoreBoatAndCrewAfterExpedition();
                var player = fishing.player;
                player.enabled = false;
                player.transform.SetPositionAndRotation(dockReturn.position, dockReturn.rotation);
                player.enabled = true;
                ReleaseControls();
                quest.CompleteCoralExpedition();
                restorationNoticeUntil = Time.unscaledTime + 6.5f;
            });
        }

        private void HoldControls(Modal target)
        {
            modal = target;
            quest.OverlayControlsHeld = true;
            overlay.SetJournalShortcut(false, quest.FishingUnlocked);
            movementWasEnabled = fishing.movement.enabled;
            dialogueWasEnabled = dialogue.enabled;
            fishing.movement.enabled = false;
            dialogue.enabled = false;
            // Both nearby input and direct TryBegin require an enabled spot.
            spotsWereEnabled = new bool[fishing.spots.Length];
            for (int i = 0; i < fishing.spots.Length; i++)
            {
                spotsWereEnabled[i] = fishing.spots[i].enabled;
                fishing.spots[i].enabled = false;
            }
        }

        private void ReleaseControls()
        {
            for (int i = 0; i < fishing.spots.Length; i++)
            {
                if (fishing.spots[i])
                {
                    fishing.spots[i].enabled = spotsWereEnabled[i];
                }
            }
            if (fishing.movement)
            {
                fishing.movement.enabled = movementWasEnabled;
            }
            if (dialogue)
            {
                dialogue.enabled = dialogueWasEnabled;
            }
            if (quest)
            {
                quest.OverlayControlsHeld = false;
            }
            modal = Modal.None;
        }

        private void RecordCatch(FishingResult result)
        {
            if (result == null || !result.success || !quest.FishingUnlocked
                || Array.Find(fishing.configuration.rarities, entry => entry.fishId == result.fishId) == null)
            {
                return;
            }

            string coralId = null;
            if (quest.CoralJournalUnlocked && discoveryRandom.NextDouble() < coralDiscoveryChance)
            {
                int unseen = 0;
                foreach (CoralRecord entry in coralRecords)
                {
                    if (!quest.HasDiscoveredCoral(entry.id) && discoveryRandom.Next(++unseen) == 0)
                    {
                        coralId = entry.id;
                    }
                }
            }
            quest.RecordFishingDiscovery(result.fishId, coralId);
        }
    }
}
