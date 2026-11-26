using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Sagara.DermagaV2
{
    /// <summary>
    /// Complete Aerial Drone Director for DermagaV2 (016_SteamPolish):
    /// 1. Opening Coastal Drone Flyover on fresh arrival without save (Phase == MeetPakDarma),
    ///    showcasing the damaged beach, broken dock, bare mangrove bay, and Pak Darma's cottage
    ///    before landing smoothly on the Player at the Main Village connection entrance.
    /// 2. Per-Quest Drone Beats when accepting or completing major restoration quests:
    ///    - CleanBeach start (shows debris) & completion (clean beach + 3D "cling-cling!" sparkles!)
    ///    - CollectWood start (shows forest clearing)
    ///    - RepairDock start (shows broken pier) & completion (restored dock + 3D "cling-cling!" sparkles!)
    ///    - PlantMangroves start (shows bare tidal shelf) & completion (time-lapse growth drone where all 4
    ///      planted saplings visibly grow into lush mature 3D mangroves with "cling-cling!" sparkles!)
    /// </summary>
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DermagaOpeningDroneCinematic : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerNpcInteraction playerDialogue;
        [SerializeField] private DermagaQuestAnimation questAnimation;
        [SerializeField] private DermagaMangrovePlanting mangrovePlanting;
        [SerializeField] private DermagaCleanupHud cleanupHud;

        [Header("Typography")]
        [SerializeField] private Font displayFont;
        [SerializeField] private Font bodyFont;
        [SerializeField] private FontAsset displayFontAsset;
        [SerializeField] private FontAsset bodyFontAsset;

        [Header("Cinematic Settings")]
        [SerializeField] private bool playOnFreshStart = true;
        [SerializeField] private bool playOnQuestMilestones = true;
        [SerializeField] private float droneFov = 44f;

        public static bool IsPlaying { get; private set; }
        private static bool s_introPlayedThisSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionState()
        {
            IsPlaying = false;
            s_introPlayedThisSession = false;
        }

        public bool ShouldPlayOpeningDrone =>
            playOnFreshStart && !s_introPlayedThisSession && quest && quest.Phase == DermagaQuestPhase.MeetPakDarma;

        private struct DroneShot
        {
            public Vector3 posStart;
            public Vector3 posEnd;
            public Vector3 lookStart;
            public Vector3 lookEnd;
            public float duration;
            public string kicker;
            public string caption;
            public Vector3[] sparklePoints;
            public Color sparkleColor;
            public bool growMangroves;
        }

        private sealed class SparkleInstance
        {
            public Transform tr;
            public Vector3 vel;
            public float age;
            public float lifetime;
            public float maxScale;
            public float spinSpeed;
            public Material mat;
        }

        private UIDocument document;
        private VisualElement root;
        private VisualElement fadeVeil;
        private VisualElement captionCard;
        private Label kickerLabel;
        private Label captionLabel;
        private Button skipButton;

        private Coroutine droneCoroutine;
        private bool movementWasEnabled;
        private bool dialogueWasEnabled;
        private bool hudWasEnabled;
        private bool camWasOrtho;
        private float camSavedOrthoSize;
        private float camSavedFov;
        private Vector3 camSavedPos;
        private Quaternion camSavedRot;
        private bool stateSaved;

        private DermagaQuestPhase observedPhase;
        private DermagaQuestPhase? queuedMilestone;
        private bool phaseInitialized;

        private readonly List<SparkleInstance> sparkles = new List<SparkleInstance>();
        private Mesh sparkleMesh;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (document) document.sortingOrder = 115;
            if (!quest) quest = FindFirstObjectByType<DermagaIntroProgress>();
            if (!playerMovement) playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (!playerDialogue) playerDialogue = FindFirstObjectByType<PlayerNpcInteraction>();
            if (!questAnimation) questAnimation = FindFirstObjectByType<DermagaQuestAnimation>();
            if (!mangrovePlanting) mangrovePlanting = FindFirstObjectByType<DermagaMangrovePlanting>();
            if (!cleanupHud) cleanupHud = FindFirstObjectByType<DermagaCleanupHud>();
            sparkleMesh = BuildSparkleStarMesh();
        }

        private void Start()
        {
            BuildOverlay();
            SetOverlayVisible(false);

            if (quest)
            {
                observedPhase = quest.Phase;
                phaseInitialized = true;
            }

            // Automatically play full damaged-coast arrival drone on first entry without save!
            // If arriving via CoastalSceneCrossing from Main Village, CoastalSceneCrossing hands off cleanly under the black cover.
            if (ShouldPlayOpeningDrone && !Sagara.SceneTransitions.CoastalSceneCrossing.IsArrivingScene(gameObject.scene.path))
            {
                PlayIntroDrone();
            }
        }

        private void OnDisable()
        {
            if (droneCoroutine != null)
            {
                StopCoroutine(droneCoroutine);
                droneCoroutine = null;
            }
            if (IsPlaying)
            {
                FinishImmediate();
            }
            ClearAllSparkles();
            if (sparkleMesh) Destroy(sparkleMesh);
            root?.RemoveFromHierarchy();
        }

        private void Update()
        {
            TickSparkles(Time.unscaledDeltaTime);

            if (IsPlaying)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                {
                    SkipDrone();
                }
                return;
            }

            if (!playOnQuestMilestones || !quest || !phaseInitialized)
            {
                return;
            }

            if (quest.Phase != observedPhase)
            {
                observedPhase = quest.Phase;
                if (HasMilestoneDrone(observedPhase))
                {
                    queuedMilestone = observedPhase;
                }
            }

            // Wait cleanly until dialogue & pickup/repair/planting animations finish before launching the drone!
            if (queuedMilestone.HasValue && CanStartMilestoneDrone())
            {
                DermagaQuestPhase phaseToPlay = queuedMilestone.Value;
                queuedMilestone = null;
                PlayQuestMilestoneDrone(phaseToPlay);
            }
        }

        private bool CanStartMilestoneDrone()
        {
            if (IsPlaying || DermagaCoral3DCutscene.IsPlaying || DermagaCoralVideoCutscene.IsPlaying) return false;
            if (playerDialogue && playerDialogue.IsTalking) return false;
            if (questAnimation && questAnimation.Busy) return false;
            if (quest && quest.OverlayControlsHeld) return false;
            return true;
        }

        private static bool HasMilestoneDrone(DermagaQuestPhase phase)
        {
            return phase == DermagaQuestPhase.CleanBeach
                || phase == DermagaQuestPhase.ReportToPakDarma
                || phase == DermagaQuestPhase.CollectWood
                || phase == DermagaQuestPhase.RepairDock
                || phase == DermagaQuestPhase.ReportDock
                || phase == DermagaQuestPhase.PlantMangroves
                || phase == DermagaQuestPhase.ReportMangroves
                || phase == DermagaQuestPhase.CollectCoralSupplies
                || phase == DermagaQuestPhase.ReportCoralSupplies;
        }

        /// <summary>Plays the opening aerial coastal drone flyover showcasing the damaged ecosystem.</summary>
        public void PlayIntroDrone()
        {
            if (IsPlaying) return;
            s_introPlayedThisSession = true;
            var shots = new DroneShot[]
            {
                new DroneShot
                {
                    posStart = new Vector3(-21.5f, 8.6f, -15.5f),
                    posEnd   = new Vector3(-9.5f,  6.8f, -12.8f),
                    lookStart = new Vector3(-14.8f, 0.6f, -8.2f),
                    lookEnd   = new Vector3(-12.2f, 0.7f, -6.4f),
                    duration = 3.0f,
                    kicker   = "PESISIR SAGARA",
                    caption  = "Sampah mulai menumpuk di sepanjang garis pantai..."
                },
                new DroneShot
                {
                    posStart = new Vector3(8.5f,  7.2f, -18.5f),
                    posEnd   = new Vector3(23.5f, 8.4f, -6.2f),
                    lookStart = new Vector3(14.5f, 0.7f, -7.2f),
                    lookEnd   = new Vector3(13.2f, 0.8f, -2.0f),
                    duration = 3.0f,
                    kicker   = "DERMAGA TUA",
                    caption  = "Papan dermaga lapuk dan pagarnya runtuh diterpa ombak..."
                },
                new DroneShot
                {
                    posStart = new Vector3(23.8f, 8.2f, 5.2f),
                    posEnd   = new Vector3(11.2f, 9.2f, 8.8f),
                    lookStart = new Vector3(15.2f, 0.3f, 11.4f),
                    lookEnd   = new Vector3(13.6f, 0.6f, 13.8f),
                    duration = 2.8f,
                    kicker   = "TELUK MANGROVE",
                    caption  = "Tepian berlumpur menanti bibit bakau untuk kembali hijau..."
                },
                new DroneShot
                {
                    posStart = new Vector3(-10.5f, 12.2f, 10.5f),
                    posEnd   = new Vector3(-47.5f, 15.8f, 25.0f),
                    lookStart = new Vector3(-5.8f,  2.6f,  8.6f),
                    lookEnd   = new Vector3(-55.6f, 4.8f, 35.0f),
                    duration = 3.1f,
                    kicker   = "MISI PEMULIHAN",
                    caption  = "Temui Pak Darma di gubuk pesisir untuk memulai pemulihan."
                }
            };
            StartDroneSequence(shots);
        }

        private void PlayQuestMilestoneDrone(DermagaQuestPhase phase)
        {
            DroneShot[] shots = null;
            switch (phase)
            {
                case DermagaQuestPhase.CleanBeach:
                    shots = new[]
                    {
                        new DroneShot
                        {
                            posStart = new Vector3(-21.2f, 7.6f, -14.8f),
                            posEnd   = new Vector3(-9.2f,  6.4f, -11.5f),
                            lookStart = new Vector3(-15.0f, 0.6f, -8.5f),
                            lookEnd   = new Vector3(-13.6f, 0.6f, -7.8f),
                            duration = 3.4f,
                            kicker   = "MISI · BERSIHKAN PANTAI",
                            caption  = "Pungut sampah yang berserakan di sepanjang pantai."
                        }
                    };
                    break;

                case DermagaQuestPhase.ReportToPakDarma:
                    shots = new[]
                    {
                        new DroneShot
                        {
                            posStart = new Vector3(-20.4f, 7.4f, -14.2f),
                            posEnd   = new Vector3(-9.6f,  6.5f, -11.2f),
                            lookStart = new Vector3(-15.0f, 0.7f, -8.5f),
                            lookEnd   = new Vector3(-13.4f, 0.8f, -7.5f),
                            duration = 3.6f,
                            kicker   = "PANTAI KEMBALI BERSIH!",
                            caption  = "Garis pantai kini bersih dan asri — lapor kembali ke Pak Darma.",
                            sparklePoints = new[]
                            {
                                new Vector3(-16.0f, 1.0f, -7.8f),
                                new Vector3(-14.5f, 1.1f, -9.8f),
                                new Vector3(-13.4f, 1.0f, -7.6f)
                            },
                            sparkleColor = new Color(1f, 0.90f, 0.42f, 1f)
                        }
                    };
                    break;

                case DermagaQuestPhase.CollectWood:
                    shots = new[]
                    {
                        new DroneShot
                        {
                            posStart = new Vector3(-24.8f, 10.6f, 19.8f),
                            posEnd   = new Vector3(-11.8f, 10.2f, 20.6f),
                            lookStart = new Vector3(-19.2f, 4.5f, 25.8f),
                            lookEnd   = new Vector3(-15.2f, 4.5f, 26.2f),
                            duration = 3.3f,
                            kicker   = "MISI · KUMPULKAN KAYU",
                            caption  = "Cari ranting kayu di area hutan pesisir untuk memperbaiki dermaga."
                        }
                    };
                    break;

                case DermagaQuestPhase.RepairDock:
                    shots = new[]
                    {
                        new DroneShot
                        {
                            posStart = new Vector3(8.0f,  6.8f, -17.5f),
                            posEnd   = new Vector3(22.5f, 7.6f, -3.5f),
                            lookStart = new Vector3(14.8f, 0.7f, -9.5f),
                            lookEnd   = new Vector3(13.5f, 0.7f, -1.8f),
                            duration = 3.5f,
                            kicker   = "MISI · PERBAIKI DERMAGA",
                            caption  = "Perbaiki papan dan pagar dermaga yang rusak di tiga titik."
                        }
                    };
                    break;

                case DermagaQuestPhase.ReportDock:
                    shots = new[]
                    {
                        new DroneShot
                        {
                            posStart = new Vector3(7.5f,  6.8f, -17.2f),
                            posEnd   = new Vector3(22.5f, 7.8f, -2.5f),
                            lookStart = new Vector3(14.8f, 0.8f, -9.5f),
                            lookEnd   = new Vector3(11.5f, 0.8f, -0.5f),
                            duration = 3.8f,
                            kicker   = "DERMAGA KEMBALI KOKOH!",
                            caption  = "Seluruh papan dan pagar dermaga kini rapi kembali!",
                            sparklePoints = new[]
                            {
                                new Vector3(2.8f,  1.2f,  0.0f),
                                new Vector3(15.2f, 1.3f, -4.2f),
                                new Vector3(15.2f, 1.3f, -10.8f),
                                new Vector3(10.5f, 1.2f,  0.0f)
                            },
                            sparkleColor = new Color(1f, 0.86f, 0.34f, 1f)
                        }
                    };
                    break;

                case DermagaQuestPhase.PlantMangroves:
                    shots = new[]
                    {
                        new DroneShot
                        {
                            posStart = new Vector3(21.5f, 6.8f, 4.8f),
                            posEnd   = new Vector3(10.5f, 6.8f, 5.2f),
                            lookStart = new Vector3(16.8f, 0.2f, 10.8f),
                            lookEnd   = new Vector3(12.2f, 0.2f, 10.8f),
                            duration = 3.4f,
                            kicker   = "MISI · TANAM MANGROVE",
                            caption  = "Tanam empat bibit mangrove di sepanjang tepian lumpur teluk."
                        }
                    };
                    break;

                case DermagaQuestPhase.ReportMangroves:
                    shots = new[]
                    {
                        new DroneShot
                        {
                            posStart = new Vector3(10.4f, 6.2f, 4.8f),
                            posEnd   = new Vector3(20.6f, 7.6f, 4.2f),
                            lookStart = new Vector3(12.6f, 0.9f, 10.8f),
                            lookEnd   = new Vector3(16.6f, 1.3f, 10.8f),
                            duration = 4.9f,
                            kicker   = "HUTAN MANGROVE TUMBUH!",
                            caption  = "Bibit bakau perlahan tumbuh rimbun menjaga pesisir Sagara!",
                            sparkleColor = new Color(0.54f, 1f, 0.72f, 1f),
                            growMangroves = true
                        }
                    };
                    break;

                case DermagaQuestPhase.CollectCoralSupplies:
                    shots = new[]
                    {
                        new DroneShot
                        {
                            posStart = new Vector3(-13.8f, 8.2f, 1.5f),
                            posEnd   = new Vector3(-1.5f,  6.2f, -4.8f),
                            lookStart = new Vector3(-9.5f, 2.5f, 7.3f),
                            lookEnd   = new Vector3(-5.0f, 0.8f, 1.0f),
                            duration = 3.2f,
                            kicker   = "MISI · PERLENGKAPAN TERUMBU",
                            caption  = "Ambil tali konservasi di dekat gubuk dan rangka karang di pantai."
                        },
                        new DroneShot
                        {
                            posStart = new Vector3(4.5f,  6.2f, -6.8f),
                            posEnd   = new Vector3(13.8f, 6.4f, -5.8f),
                            lookStart = new Vector3(9.4f, 0.8f, -0.8f),
                            lookEnd   = new Vector3(9.4f, 0.8f, -0.8f),
                            duration = 2.6f,
                            kicker   = "MISI · PERLENGKAPAN TERUMBU",
                            caption  = "Jangan lupa ambil pelampung penanda di atas dermaga!"
                        }
                    };
                    break;

                case DermagaQuestPhase.ReportCoralSupplies:
                    shots = new[]
                    {
                        new DroneShot
                        {
                            posStart = new Vector3(1.5f,  6.2f, -12.5f),
                            posEnd   = new Vector3(12.2f, 6.8f, -10.8f),
                            lookStart = new Vector3(6.3f, 0.5f, -5.6f),
                            lookEnd   = new Vector3(6.3f, 0.5f, -5.6f),
                            duration = 3.5f,
                            kicker   = "PERLENGKAPAN LENGKAP!",
                            caption  = "Semua peralatan siap — temui Pak Darma untuk berlayar ke terumbu karang!",
                            sparklePoints = new[]
                            {
                                new Vector3(6.2f, 1.2f, -5.5f),
                                new Vector3(8.2f, 1.4f, -5.0f),
                                new Vector3(5.6f, 1.3f, -1.0f)
                            },
                            sparkleColor = new Color(0.45f, 0.96f, 1f, 1f)
                        }
                    };
                    break;
            }

            if (shots != null)
            {
                StartDroneSequence(shots);
            }
        }

        private void StartDroneSequence(DroneShot[] shots)
        {
            if (IsPlaying || shots == null || shots.Length == 0) return;
            if (droneCoroutine != null) StopCoroutine(droneCoroutine);
            droneCoroutine = StartCoroutine(DroneSequenceRoutine(shots));
        }

        public void SkipDrone()
        {
            if (!IsPlaying) return;
            if (droneCoroutine != null)
            {
                StopCoroutine(droneCoroutine);
                droneCoroutine = null;
            }
            // Ensure mangroves still finish growing if player skips during growth drone!
            if (quest && DermagaQuestSnapshot.Order((int)quest.Phase) >= DermagaQuestSnapshot.Order((int)DermagaQuestPhase.ReportMangroves)
                && mangrovePlanting)
            {
                StartCoroutine(mangrovePlanting.AnimateGrowAllMangrovesRoutine(0.05f, null));
            }
            StartCoroutine(QuickFadeOutRoutine());
        }

        private IEnumerator QuickFadeOutRoutine()
        {
            float t = 0f;
            while (t < 0.24f)
            {
                t += Time.unscaledDeltaTime;
                fadeVeil.style.opacity = Mathf.Clamp01(t / 0.24f);
                yield return null;
            }
            RestoreGameplayState();
            t = 0f;
            while (t < 0.28f)
            {
                t += Time.unscaledDeltaTime;
                fadeVeil.style.opacity = 1f - Mathf.Clamp01(t / 0.28f);
                yield return null;
            }
            SetOverlayVisible(false);
            IsPlaying = false;
        }

        private IEnumerator DroneSequenceRoutine(DroneShot[] shots)
        {
            var cam = Camera.main;
            if (!cam) yield break;

            IsPlaying = true;
            LockForCinematic(cam);
            SetOverlayVisible(true);
            fadeVeil.style.opacity = 1f;

            cam.orthographic = false;
            cam.fieldOfView = droneFov;

            for (int s = 0; s < shots.Length; s++)
            {
                DroneShot shot = shots[s];
                kickerLabel.text = shot.kicker;
                captionLabel.text = shot.caption;

                bool[] firedSparkles = shot.sparklePoints != null ? new bool[shot.sparklePoints.Length] : null;
                Coroutine growRoutine = null;
                if (shot.growMangroves && mangrovePlanting)
                {
                    growRoutine = StartCoroutine(mangrovePlanting.AnimateGrowAllMangrovesRoutine(
                        shot.duration * 0.76f,
                        pos => SpawnClingClingBurst(pos, shot.sparkleColor, playChime: true)));
                }

                float elapsed = 0f;
                while (elapsed < shot.duration)
                {
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    float u = Mathf.Clamp01(elapsed / shot.duration);
                    float ease = u * u * (3f - 2f * u);
                    float blend = Mathf.Lerp(u, ease, 0.55f);

                    Vector3 p = Vector3.Lerp(shot.posStart, shot.posEnd, blend);
                    p.y += Mathf.Sin(elapsed * 1.4f + s) * 0.12f;
                    Vector3 look = Vector3.Lerp(shot.lookStart, shot.lookEnd, blend);

                    cam.transform.position = p;
                    cam.transform.rotation = Quaternion.LookRotation((look - p).normalized, Vector3.up);

                    // Trigger staggered 3D "Cling-Cling!" sparkle bursts on restored landmarks!
                    if (firedSparkles != null)
                    {
                        for (int k = 0; k < firedSparkles.Length; k++)
                        {
                            float triggerAt = 0.55f + k * 0.52f;
                            if (!firedSparkles[k] && elapsed >= triggerAt)
                            {
                                firedSparkles[k] = true;
                                SpawnClingClingBurst(shot.sparklePoints[k], shot.sparkleColor, playChime: true);
                            }
                        }
                    }

                    // Soft cross-fade veil at edges of each shot
                    float fadeIn = Mathf.Clamp01(elapsed / 0.38f);
                    float fadeOut = Mathf.Clamp01((shot.duration - elapsed) / 0.38f);
                    float shotVis = Mathf.Min(fadeIn, fadeOut);
                    fadeVeil.style.opacity = 1f - shotVis;

                    // Smooth floating subtitle glide
                    float txtIn = Mathf.Clamp01((elapsed - 0.16f) / 0.42f);
                    float txtVis = Mathf.Min(txtIn, fadeOut);
                    float te = txtVis * txtVis * (3f - 2f * txtVis);
                    captionCard.style.opacity = te;
                    captionCard.style.translate = new Translate(0f, (1f - te) * 14f, 0f);

                    yield return null;
                }

                if (growRoutine != null)
                {
                    yield return growRoutine;
                }
            }

            // Smooth restore to normal FoMT orthographic gameplay camera while veil is dark
            fadeVeil.style.opacity = 1f;
            RestoreGameplayState();
            yield return null;

            float outT = 0f;
            while (outT < 0.48f)
            {
                outT += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(outT / 0.48f);
                fadeVeil.style.opacity = a;
                captionCard.style.opacity = 0f;
                skipButton.style.opacity = a;
                yield return null;
            }

            SetOverlayVisible(false);
            IsPlaying = false;
            droneCoroutine = null;
        }

        // =====================================================================
        // 3D "CLING-CLING!" SPARKLE BURST SYSTEM
        // =====================================================================

        internal void SpawnClingClingBurst(Vector3 center, Color tint, bool playChime)
        {
            if (playChime)
            {
                DermagaUiAudio.PlayClingSparkle(UnityEngine.Random.Range(0.96f, 1.14f));
            }
            if (!sparkleMesh) return;

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            for (int i = 0; i < 9; i++)
            {
                var go = new GameObject("ClingSparkle");
                go.transform.position = center + new Vector3(
                    UnityEngine.Random.Range(-0.9f, 0.9f),
                    UnityEngine.Random.Range(-0.15f, 0.55f),
                    UnityEngine.Random.Range(-0.9f, 0.9f));
                go.transform.localScale = Vector3.one * 0.01f;
                go.AddComponent<MeshFilter>().sharedMesh = sparkleMesh;
                var mr = go.AddComponent<MeshRenderer>();
                var mat = new Material(unlit);
                Color c = Color.Lerp(tint, Color.white, (i % 3) * 0.35f);
                mat.SetColor("_BaseColor", c);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                float ang = (i / 9f) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
                sparkles.Add(new SparkleInstance
                {
                    tr = go.transform,
                    vel = new Vector3(Mathf.Cos(ang) * 0.85f, UnityEngine.Random.Range(1.2f, 2.3f), Mathf.Sin(ang) * 0.85f),
                    age = 0f,
                    lifetime = UnityEngine.Random.Range(0.85f, 1.25f),
                    maxScale = UnityEngine.Random.Range(0.28f, 0.48f),
                    spinSpeed = UnityEngine.Random.Range(180f, 340f),
                    mat = mat
                });
            }
        }

        private void TickSparkles(float dt)
        {
            for (int i = sparkles.Count - 1; i >= 0; i--)
            {
                var sp = sparkles[i];
                sp.age += dt;
                if (!sp.tr || sp.age >= sp.lifetime)
                {
                    if (sp.tr) Destroy(sp.tr.gameObject);
                    if (sp.mat) Destroy(sp.mat);
                    sparkles.RemoveAt(i);
                    continue;
                }
                float u = sp.age / sp.lifetime;
                sp.tr.position += sp.vel * (dt * (1f - u * 0.65f));
                sp.tr.Rotate(0f, sp.spinSpeed * dt, sp.spinSpeed * 0.35f * dt);
                float pop = Mathf.Sin(Mathf.Min(1f, u * 3.5f) * Mathf.PI * 0.5f) * (1f - u * u);
                float twinkle = 0.82f + 0.18f * Mathf.Sin(sp.age * 24f);
                sp.tr.localScale = Vector3.one * (sp.maxScale * pop * twinkle);
            }
        }

        private void ClearAllSparkles()
        {
            for (int i = 0; i < sparkles.Count; i++)
            {
                if (sparkles[i].tr) Destroy(sparkles[i].tr.gameObject);
                if (sparkles[i].mat) Destroy(sparkles[i].mat);
            }
            sparkles.Clear();
        }

        private static Mesh BuildSparkleStarMesh()
        {
            // Clean double-pyramid glowing crystal star diamond
            var octVerts = new Vector3[]
            {
                new Vector3(0f,  1.0f, 0f),
                new Vector3(0f, -1.0f, 0f),
                new Vector3( 0.58f, 0f,  0f),
                new Vector3(0f, 0f,  0.58f),
                new Vector3(-0.58f, 0f,  0f),
                new Vector3(0f, 0f, -0.58f)
            };
            var tris = new int[]
            {
                0, 3, 2,  0, 4, 3,  0, 5, 4,  0, 2, 5,
                1, 2, 3,  1, 3, 4,  1, 4, 5,  1, 5, 2
            };
            var m = new Mesh { name = "ClingSparkleDiamond" };
            m.SetVertices(octVerts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            return m;
        }

        private void LockForCinematic(Camera cam)
        {
            camWasOrtho = cam.orthographic;
            camSavedOrthoSize = cam.orthographicSize;
            camSavedFov = cam.fieldOfView;
            camSavedPos = cam.transform.position;
            camSavedRot = cam.transform.rotation;

            var follow = cam.GetComponent<PlayerCameraFollow>();
            var pres = cam.GetComponent<DermagaCameraPresentation>();
            var clamp = cam.GetComponent<DermagaEntranceWideZoomClamp>();
            if (follow) follow.enabled = false;
            if (pres) pres.enabled = false;
            if (clamp) clamp.enabled = false;

            if (quest) quest.OverlayControlsHeld = true;
            if (playerMovement)
            {
                movementWasEnabled = playerMovement.enabled;
                playerMovement.enabled = false;
            }
            if (playerDialogue)
            {
                dialogueWasEnabled = playerDialogue.enabled;
                playerDialogue.enabled = false;
            }
            if (cleanupHud)
            {
                var doc = cleanupHud.GetComponent<UIDocument>();
                if (doc && doc.rootVisualElement != null)
                {
                    hudWasEnabled = doc.rootVisualElement.style.display != DisplayStyle.None;
                    doc.rootVisualElement.style.display = DisplayStyle.None;
                }
            }
            stateSaved = true;
        }

        private void RestoreGameplayState()
        {
            if (!stateSaved) return;
            var cam = Camera.main;
            if (cam)
            {
                cam.orthographic = camWasOrtho;
                cam.orthographicSize = camSavedOrthoSize;
                cam.fieldOfView = camSavedFov;
                cam.transform.SetPositionAndRotation(camSavedPos, camSavedRot);

                var follow = cam.GetComponent<PlayerCameraFollow>();
                var pres = cam.GetComponent<DermagaCameraPresentation>();
                var clamp = cam.GetComponent<DermagaEntranceWideZoomClamp>();
                if (follow) follow.enabled = true;
                if (pres) pres.enabled = true;
                if (clamp) clamp.enabled = true;
            }
            if (playerMovement) playerMovement.enabled = movementWasEnabled;
            if (playerDialogue) playerDialogue.enabled = dialogueWasEnabled;
            if (quest) quest.OverlayControlsHeld = false;
            if (cleanupHud && hudWasEnabled)
            {
                var doc = cleanupHud.GetComponent<UIDocument>();
                if (doc && doc.rootVisualElement != null)
                {
                    doc.rootVisualElement.style.display = DisplayStyle.Flex;
                }
            }
            stateSaved = false;
        }

        private void FinishImmediate()
        {
            RestoreGameplayState();
            SetOverlayVisible(false);
            IsPlaying = false;
        }

        private void BuildOverlay()
        {
            if (!document || document.rootVisualElement == null) return;
            document.rootVisualElement.pickingMode = PickingMode.Ignore;
            document.rootVisualElement.StretchToParentSize();

            root = new VisualElement { name = "DermagaDroneCinematicRoot", pickingMode = PickingMode.Ignore };
            root.StretchToParentSize();
            document.rootVisualElement.Add(root);

            fadeVeil = new VisualElement { pickingMode = PickingMode.Ignore };
            fadeVeil.StretchToParentSize();
            fadeVeil.style.backgroundColor = new Color(0.04f, 0.13f, 0.18f, 1f);
            fadeVeil.style.opacity = 0f;
            root.Add(fadeVeil);

            // Sleek floating translucent coastal subtitle pill (zero clunky black bars — matches SAGARA UI DNA!)
            captionCard = new VisualElement { pickingMode = PickingMode.Ignore };
            captionCard.style.position = Position.Absolute;
            captionCard.style.bottom = 34;
            captionCard.style.alignSelf = Align.Center;
            captionCard.style.alignItems = Align.Center;
            captionCard.style.paddingLeft = 34;
            captionCard.style.paddingRight = 34;
            captionCard.style.paddingTop = 12;
            captionCard.style.paddingBottom = 14;
            captionCard.style.backgroundColor = new Color(0.05f, 0.16f, 0.21f, 0.84f);
            captionCard.style.borderTopLeftRadius = 20;
            captionCard.style.borderTopRightRadius = 20;
            captionCard.style.borderBottomLeftRadius = 20;
            captionCard.style.borderBottomRightRadius = 20;
            captionCard.style.borderTopWidth = 1.4f;
            captionCard.style.borderBottomWidth = 1.4f;
            captionCard.style.borderLeftWidth = 1.4f;
            captionCard.style.borderRightWidth = 1.4f;
            var cardRim = new Color(1f, 0.93f, 0.74f, 0.44f);
            captionCard.style.borderTopColor = cardRim;
            captionCard.style.borderBottomColor = cardRim;
            captionCard.style.borderLeftColor = cardRim;
            captionCard.style.borderRightColor = cardRim;
            root.Add(captionCard);

            kickerLabel = new Label("PESISIR SAGARA") { pickingMode = PickingMode.Ignore };
            ApplyFont(kickerLabel, display: true);
            kickerLabel.style.fontSize = 11;
            kickerLabel.style.letterSpacing = 2.4f;
            kickerLabel.style.color = new Color(1f, 0.86f, 0.44f, 1f);
            kickerLabel.style.marginBottom = 4;
            captionCard.Add(kickerLabel);

            captionLabel = new Label("") { pickingMode = PickingMode.Ignore };
            ApplyFont(captionLabel, display: false);
            captionLabel.style.fontSize = 18;
            captionLabel.style.color = new Color(0.99f, 0.97f, 0.91f, 1f);
            captionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            captionCard.Add(captionLabel);

            // Subtle floating Skip pill in bottom-right
            skipButton = new Button(SkipDrone) { text = "", focusable = false };
            skipButton.style.position = Position.Absolute;
            skipButton.style.right = 26;
            skipButton.style.bottom = 26;
            skipButton.style.flexDirection = FlexDirection.Row;
            skipButton.style.alignItems = Align.Center;
            skipButton.style.paddingLeft = 12;
            skipButton.style.paddingRight = 14;
            skipButton.style.paddingTop = 5;
            skipButton.style.paddingBottom = 5;
            skipButton.style.borderTopLeftRadius = 15;
            skipButton.style.borderTopRightRadius = 15;
            skipButton.style.borderBottomLeftRadius = 15;
            skipButton.style.borderBottomRightRadius = 15;
            skipButton.style.backgroundColor = new Color(0.06f, 0.19f, 0.25f, 0.80f);
            skipButton.style.borderTopWidth = 1.2f;
            skipButton.style.borderBottomWidth = 1.2f;
            skipButton.style.borderLeftWidth = 1.2f;
            skipButton.style.borderRightWidth = 1.2f;
            skipButton.style.borderTopColor = cardRim;
            skipButton.style.borderBottomColor = cardRim;
            skipButton.style.borderLeftColor = cardRim;
            skipButton.style.borderRightColor = cardRim;
            root.Add(skipButton);

            var keyLbl = new Label("Esc") { pickingMode = PickingMode.Ignore };
            ApplyFont(keyLbl, display: true);
            keyLbl.style.fontSize = 11;
            keyLbl.style.color = new Color(1f, 0.88f, 0.52f, 1f);
            keyLbl.style.marginRight = 7;
            skipButton.Add(keyLbl);

            var textLbl = new Label("Lewati") { pickingMode = PickingMode.Ignore };
            ApplyFont(textLbl, display: true);
            textLbl.style.fontSize = 12;
            textLbl.style.color = new Color(0.98f, 0.96f, 0.90f, 0.94f);
            skipButton.Add(textLbl);
        }

        private void ApplyFont(VisualElement el, bool display)
        {
            if (display)
            {
                if (displayFontAsset) el.style.unityFontDefinition = FontDefinition.FromSDFFont(displayFontAsset);
                else if (displayFont) el.style.unityFontDefinition = FontDefinition.FromFont(displayFont);
            }
            else
            {
                if (bodyFontAsset) el.style.unityFontDefinition = FontDefinition.FromSDFFont(bodyFontAsset);
                else if (bodyFont) el.style.unityFontDefinition = FontDefinition.FromFont(bodyFont);
            }
        }

        private void SetOverlayVisible(bool visible)
        {
            if (root != null)
            {
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible)
                {
                    skipButton.style.opacity = 1f;
                }
            }
        }
    }
}
