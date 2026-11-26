using System;
using System.Collections;
using System.Collections.Generic;
using Sagara.Dermaga.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Sagara.DermagaV2
{
    /// <summary>
    /// Real-Time Cozy 3D Diorama Coral Restoration Cutscene (016_SteamPolish).
    /// Replaces static 2D expedition slides with a live 3-Act 3D underwater & boat-deck animation:
    ///   Act 1: Over-the-shoulder on the boat deck — Pak Darma points out the bleached 3D reef below.
    ///   Act 2: Underwater dive — live 3D Player swims beside the coral rack planting glowing fragments
    ///          with rising bubbles & sunlit god-rays while the boat floats overhead.
    ///   Act 3: Reef Bloom — all 3D corals burst into radiant colors with "cling-cling" sparkles as
    ///          live 3D SAGARA fish schools swirl joyfully around the restored reef!
    /// </summary>
    [DefaultExecutionOrder(310)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DermagaCoral3DCutscene : MonoBehaviour
    {
        private static readonly Vector3 StageOrigin = new Vector3(1450f, -35f, 1450f);
        private static readonly int AnimIdle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int AnimTalk = Animator.StringToHash("Base Layer.Quest Talk");
        private static readonly int AnimPlant = Animator.StringToHash("Base Layer.Quest Plant");

        [Header("Scene & Catalog References")]
        [SerializeField] private FishingVisualCatalog fishCatalog;
        [SerializeField] private DermagaCleanupHud cleanupHud;

        [Header("Typography")]
        [SerializeField] private Font displayFont;
        [SerializeField] private Font bodyFont;
        [SerializeField] private FontAsset displayFontAsset;
        [SerializeField] private FontAsset bodyFontAsset;

        public static bool IsPlaying { get; private set; }

        private sealed class CoralNode
        {
            public Transform tr;
            public Material mat;
            public Color bleachedCol;
            public Color healthyCol;
            public Vector3 baseScale;
            public bool isRackFragment;
        }

        private sealed class FishSwimmer
        {
            public Transform tr;
            public float orbitRad;
            public float speed;
            public float angle;
            public float baseY;
            public float bobPhase;
            public float dirSign;
        }

        private sealed class BubbleNode
        {
            public Transform tr;
            public float speed;
            public float baseX;
            public float baseZ;
            public float phase;
            public float minY;
            public float maxY;
        }

        private UIDocument document;
        private VisualElement root;
        private VisualElement fadeVeil;
        private VisualElement subtitleCard;
        private Label speakerLabel;
        private Label lineLabel;
        private Button nextButton;
        private Button skipButton;

        private GameObject stageRoot;
        private Transform stageBoat;
        private Transform stagePakDarma;
        private Transform stagePlayer;
        private Animator stagePlayerAnim;
        private Animator stagePakAnim;
        private readonly List<CoralNode> corals = new List<CoralNode>();
        private readonly List<FishSwimmer> fishes = new List<FishSwimmer>();
        private readonly List<BubbleNode> bubbles = new List<BubbleNode>();
        private readonly List<Transform> godRays = new List<Transform>();
        private readonly List<Material> runtimeMats = new List<Material>();

        private Coroutine routine;
        private Action onComplete;
        private int currentAct;
        private bool advanceRequested;
        private bool skipRequested;

        private bool camWasOrtho;
        private float camSavedOrtho;
        private float camSavedFov;
        private Vector3 camSavedPos;
        private Quaternion camSavedRot;
        private CameraClearFlags camSavedClear;
        private Color camSavedBg;
        private bool savedFog;
        private Color savedFogColor;
        private FogMode savedFogMode;
        private float savedFogStart;
        private float savedFogEnd;
        private bool hudWasVisible;
        private bool cameraSaved;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (!cleanupHud) cleanupHud = FindFirstObjectByType<DermagaCleanupHud>();
        }

        private void Start()
        {
            BuildUi();
            SetUiVisible(false);
        }

        private void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            CleanupStage();
            if (IsPlaying)
            {
                RestoreCamera();
                IsPlaying = false;
            }
            root?.RemoveFromHierarchy();
        }

        private void Update()
        {
            if (!IsPlaying) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    skipRequested = true;
                }
                else if (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    advanceRequested = true;
                }
            }
        }

        public void PlayCutscene(Action onFinished)
        {
            if (IsPlaying) return;
            onComplete = onFinished;
            advanceRequested = false;
            skipRequested = false;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(CutsceneRoutine());
        }

        private IEnumerator CutsceneRoutine()
        {
            var cam = Camera.main;
            if (!cam)
            {
                onComplete?.Invoke();
                yield break;
            }

            IsPlaying = true;
            SaveAndTakeCamera(cam);
            Build3DUnderwaterStage();
            SetUiVisible(true);

            // Fade cleanly from sailing boat into Act 1
            fadeVeil.style.opacity = 1f;
            cam.orthographic = false;
            cam.fieldOfView = 42f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.14f, 0.56f, 0.68f, 1f);

            string[] speakers = { "Pak Darma", "Pak Darma", "Pak Darma" };
            string[] lines =
            {
                "Lihat ke sana, Nak. Sebagian terumbu karang di perairan ini mulai memutih dan rusak.",
                "Pelan-pelan, Nak. Pastikan setiap fragmen terpasang kuat tanpa merusak karang di sekitarnya.",
                "Lihat! Terumbu karang mulai pulih dan ikan-ikan kembali meramaikan perairan Sagara!"
            };

            float stageTime = 0f;
            for (int act = 0; act < 3 && !skipRequested; act++)
            {
                currentAct = act;
                advanceRequested = false;
                speakerLabel.text = speakers[act];
                lineLabel.text = lines[act];

                ConfigureActActors(act);
                if (act == 1) DermagaAudioDirector.PlayWaterSplash();
                if (act == 2) DermagaUiAudio.PlayClingSparkle(1.05f);

                float actTime = 0f;
                int plantedCount = 0;
                bool chimePlayed = false;

                while (!skipRequested)
                {
                    float dt = Time.unscaledDeltaTime;
                    actTime += dt;
                    stageTime += dt;

                    AnimateStageAmbience(stageTime, dt);
                    AnimateAct(act, actTime, stageTime, cam, ref plantedCount, ref chimePlayed);

                    // Smooth fade-in at start of each act
                    float fadeIn = Mathf.Clamp01(actTime / 0.45f);
                    fadeVeil.style.opacity = 1f - fadeIn;
                    subtitleCard.style.opacity = Mathf.Clamp01((actTime - 0.15f) / 0.35f);
                    subtitleCard.style.translate = new Translate(0f, (1f - Mathf.Clamp01((actTime - 0.15f) / 0.35f)) * 12f, 0f);

                    // Allow advancing after 0.75s so accidental double-taps don't skip an act
                    if (advanceRequested && actTime > 0.75f)
                    {
                        DermagaUiAudio.PlayClick();
                        break;
                    }
                    yield return null;
                }

                // Soft cross-fade out between acts
                if (!skipRequested)
                {
                    float ft = 0f;
                    while (ft < 0.32f)
                    {
                        ft += Time.unscaledDeltaTime;
                        fadeVeil.style.opacity = Mathf.Clamp01(ft / 0.32f);
                        yield return null;
                    }
                }
            }

            // Fade out cleanly & tear down 3D diorama before returning to dock!
            float outT = 0f;
            while (outT < 0.36f)
            {
                outT += Time.unscaledDeltaTime;
                fadeVeil.style.opacity = Mathf.Clamp01(outT / 0.36f);
                yield return null;
            }

            CleanupStage();
            RestoreCamera();
            SetUiVisible(false);
            IsPlaying = false;
            routine = null;

            var cb = onComplete;
            onComplete = null;
            cb?.Invoke();
        }

        private void ConfigureActActors(int act)
        {
            if (act == 0)
            {
                // Act 1: Both Pak Darma & Player aboard the boat looking out over the clear reef!
                if (stageBoat && stagePakDarma)
                {
                    stagePakDarma.SetParent(stageBoat, false);
                    stagePakDarma.localPosition = new Vector3(1.25f, -0.49f, 0.38f);
                    stagePakDarma.localRotation = Quaternion.Euler(0f, -110f, 0f);
                    if (stagePakAnim) stagePakAnim.CrossFadeInFixedTime(AnimTalk, 0.1f);
                }
                if (stageBoat && stagePlayer)
                {
                    stagePlayer.SetParent(stageBoat, false);
                    stagePlayer.localPosition = new Vector3(1.32f, -0.49f, -0.42f);
                    stagePlayer.localRotation = Quaternion.Euler(0f, -125f, 0f);
                    if (stagePlayerAnim) stagePlayerAnim.CrossFadeInFixedTime(AnimIdle, 0.1f);
                }
                // Show pale bleached corals; rack fragments hidden until Act 2
                foreach (var c in corals)
                {
                    c.mat.SetColor("_BaseColor", c.bleachedCol);
                    c.tr.localScale = c.isRackFragment ? Vector3.one * 0.001f : c.baseScale * 0.86f;
                }
            }
            else if (act == 1)
            {
                // Act 2: Player dives smoothly down beside the PVC reef rack while Pak Darma watches from boat above!
                if (stageBoat && stagePakDarma)
                {
                    stagePakDarma.SetParent(stageBoat, false);
                    stagePakDarma.localPosition = new Vector3(1.25f, -0.49f, 0.10f);
                    stagePakDarma.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    if (stagePakAnim) stagePakAnim.CrossFadeInFixedTime(AnimIdle, 0.15f);
                }
                if (stagePlayer)
                {
                    stagePlayer.SetParent(stageRoot.transform, true);
                    stagePlayer.position = StageOrigin + new Vector3(-1.35f, 0.85f, -0.25f);
                    stagePlayer.rotation = Quaternion.Euler(36f, 82f, 0f); // Cozy swimming lean toward coral rack!
                    if (stagePlayerAnim) stagePlayerAnim.CrossFadeInFixedTime(AnimPlant, 0.15f);
                }
                foreach (var c in corals)
                {
                    if (c.isRackFragment) c.tr.localScale = Vector3.one * 0.001f;
                }
            }
            else
            {
                // Act 3: Reef blooms into vibrant tropical life as Player floats back admiring it!
                if (stagePlayer)
                {
                    stagePlayer.SetParent(stageRoot.transform, true);
                    stagePlayer.position = StageOrigin + new Vector3(-2.3f, 1.45f, -1.1f);
                    stagePlayer.rotation = Quaternion.Euler(22f, 58f, 0f);
                    if (stagePlayerAnim) stagePlayerAnim.CrossFadeInFixedTime(AnimIdle, 0.2f);
                }
            }
        }

        private void AnimateAct(int act, float actTime, float stageTime, Camera cam, ref int plantedCount, ref bool chimePlayed)
        {
            if (act == 0)
            {
                // Act 1: Cozy sunlit over-the-shoulder camera looking past Pak Darma & Player toward the bleached underwater reef
                cam.backgroundColor = new Color(0.32f, 0.72f, 0.86f, 1f);
                Vector3 boatPos = stageBoat ? stageBoat.position : StageOrigin + Vector3.up * 5.6f;
                Vector3 camPos = boatPos + new Vector3(3.4f, 2.35f, -2.5f) + new Vector3(Mathf.Sin(actTime * 0.5f) * 0.35f, Mathf.Sin(actTime * 0.8f) * 0.08f, 0f);
                Vector3 lookAt = StageOrigin + new Vector3(-0.6f, 3.2f, 0.4f);
                cam.transform.position = camPos;
                cam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            }
            else if (act == 1)
            {
                // Act 2: Cozy sunlit turquoise underwater camera framed wide enough for full 3D Player + PVC rack + boat above!
                cam.backgroundColor = new Color(0.05f, 0.38f, 0.50f, 1f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.06f, 0.42f, 0.54f, 1f);
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 6.5f;
                RenderSettings.fogEndDistance = 19.5f;

                float orbit = Mathf.Sin(actTime * 0.32f) * 0.34f;
                Vector3 camPos = StageOrigin + new Vector3(-0.35f + orbit * 2.0f, 2.25f + Mathf.Sin(actTime * 0.7f) * 0.12f, -6.45f);
                Vector3 lookAt = StageOrigin + new Vector3(-0.10f, 1.38f, 0.20f);
                cam.transform.position = camPos;
                cam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);

                // Gentle swimming hover & reach along the PVC rack
                if (stagePlayer)
                {
                    float swimBob = Mathf.Sin(stageTime * 2.6f) * 0.12f;
                    float swayZ = Mathf.Sin(actTime * 0.85f) * 0.45f;
                    stagePlayer.position = StageOrigin + new Vector3(-1.45f, 0.76f + swimBob, swayZ);
                    stagePlayer.rotation = Quaternion.Euler(34f + Mathf.Sin(stageTime * 2.2f) * 5f, 84f - swayZ * 16f, Mathf.Sin(stageTime * 1.9f) * 4f);
                }

                // Pop each of the 6 coral fragments onto the rack sequentially every 0.65s!
                int fragIdx = 0;
                foreach (var c in corals)
                {
                    if (!c.isRackFragment) continue;
                    float appearAt = 0.45f + fragIdx * 0.62f;
                    float t = Mathf.Clamp01((actTime - appearAt) / 0.45f);
                    float ease = t * t * (3f - 2f * t);
                    float pop = t > 0f && t < 1f ? Mathf.Sin(t * Mathf.PI) * 0.22f : 0f;
                    c.tr.localScale = Vector3.Max(Vector3.one * 0.001f, c.baseScale * (ease + pop));
                    c.mat.SetColor("_BaseColor", Color.Lerp(c.bleachedCol, c.healthyCol, ease));
                    if (t > 0.15f && fragIdx >= plantedCount)
                    {
                        plantedCount = fragIdx + 1;
                        DermagaUiAudio.PlayClingSparkle(0.95f + fragIdx * 0.06f);
                    }
                    fragIdx++;
                }
            }
            else
            {
                // Act 3: Reef blooms vibrantly & camera arcs smoothly upward toward sunlit ocean surface!
                cam.backgroundColor = new Color(0.07f, 0.46f, 0.58f, 1f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.08f, 0.48f, 0.60f, 1f);
                float u = Mathf.Clamp01(actTime / 4.2f);
                float ease = u * u * (3f - 2f * u);
                float ang = Mathf.Lerp(-0.32f, 0.50f, ease);
                Vector3 camPos = StageOrigin + new Vector3(Mathf.Sin(ang) * 6.4f, Mathf.Lerp(2.0f, 3.4f, ease), -Mathf.Cos(ang) * 6.4f);
                Vector3 lookAt = StageOrigin + new Vector3(0.0f, 1.25f, 0.2f);
                cam.transform.position = camPos;
                cam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);

                if (stagePlayer)
                {
                    stagePlayer.position = StageOrigin + new Vector3(-2.35f, 1.45f + Mathf.Sin(stageTime * 2.4f) * 0.14f, -1.1f);
                }

                float bloomT = Mathf.Clamp01(actTime / 2.2f);
                float bloomEase = bloomT * bloomT * (3f - 2f * bloomT);
                foreach (var c in corals)
                {
                    c.mat.SetColor("_BaseColor", Color.Lerp(c.bleachedCol, c.healthyCol, bloomEase));
                    float pulse = 1f + Mathf.Sin(stageTime * 2.5f + c.tr.position.x) * 0.04f;
                    c.tr.localScale = c.baseScale * Mathf.Lerp(0.86f, 1.12f, bloomEase) * pulse;
                }
                if (!chimePlayed && bloomT > 0.6f)
                {
                    chimePlayed = true;
                    DermagaUiAudio.PlayClingSparkle(1.22f);
                }
            }
        }

        private void AnimateStageAmbience(float t, float dt)
        {
            if (stageBoat)
            {
                stageBoat.position = StageOrigin + new Vector3(0.6f, 5.55f + Mathf.Sin(t * 2.1f) * 0.12f, 1.6f);
                stageBoat.rotation = Quaternion.Euler(Mathf.Sin(t * 1.7f) * 2.2f, 342f, Mathf.Cos(t * 1.5f) * 1.8f);
            }

            // Live swimming 3D fish circling gracefully around the reef!
            float speedBoost = currentAct == 2 ? 1.55f : 0.85f;
            for (int i = 0; i < fishes.Count; i++)
            {
                var f = fishes[i];
                if (!f.tr) continue;
                f.angle += dt * f.speed * speedBoost * f.dirSign;
                float x = Mathf.Cos(f.angle) * f.orbitRad;
                float z = Mathf.Sin(f.angle) * f.orbitRad;
                float y = f.baseY + Mathf.Sin(t * 2.4f + f.bobPhase) * 0.24f;
                Vector3 nextPos = StageOrigin + new Vector3(x, y, z);
                Vector3 fwd = (nextPos - f.tr.position);
                f.tr.position = nextPos;
                if (fwd.sqrMagnitude > 0.00001f)
                {
                    f.tr.rotation = Quaternion.Slerp(f.tr.rotation, Quaternion.LookRotation(fwd.normalized, Vector3.up), dt * 6f);
                }
            }

            // Continuous rising underwater 3D bubbles
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles[i];
                if (!b.tr) continue;
                Vector3 p = b.tr.position;
                p.y += b.speed * dt;
                if (p.y > b.maxY) p.y = b.minY;
                p.x = b.baseX + Mathf.Sin(t * 2.2f + b.phase) * 0.14f;
                p.z = b.baseZ + Mathf.Cos(t * 1.8f + b.phase) * 0.14f;
                b.tr.position = p;
            }

            // Gentle sunlit god-ray sway
            for (int i = 0; i < godRays.Count; i++)
            {
                if (godRays[i])
                {
                    godRays[i].localRotation = Quaternion.Euler(14f, i * 28f, -16f + Mathf.Sin(t * 0.6f + i) * 3.5f);
                }
            }
        }

        // =====================================================================
        // ISOLATED 3D UNDERWATER DIORAMA STAGE BUILDER (at StageOrigin)
        // =====================================================================

        private void Build3DUnderwaterStage()
        {
            CleanupStage();
            stageRoot = new GameObject("SAGARA_Coral3DCutsceneStage");
            stageRoot.transform.position = StageOrigin;

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");

            Material MakeLit(Color col, float smooth = 0.2f)
            {
                var m = new Material(lit);
                m.SetColor("_BaseColor", col);
                m.SetFloat("_Smoothness", smooth);
                runtimeMats.Add(m);
                return m;
            }

            Material MakeTransparent(Color col)
            {
                var m = new Material(unlit);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.SetColor("_BaseColor", col);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                runtimeMats.Add(m);
                return m;
            }

            // 1. Warm underwater directional sunlight + turquoise fill point light
            var lightGo = new GameObject("UnderwaterSunLight");
            lightGo.transform.SetParent(stageRoot.transform, false);
            lightGo.transform.position = new Vector3(0f, 8f, -2f);
            var pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 28f;
            pl.intensity = 42f;
            pl.color = new Color(0.74f, 0.96f, 1f);

            // 2. Sunlit underwater coastal teal-sand seabed floor
            var sandMat = MakeLit(new Color(0.44f, 0.72f, 0.70f), 0.14f);
            var seabed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seabed.name = "Seabed";
            Destroy(seabed.GetComponent<Collider>());
            seabed.transform.SetParent(stageRoot.transform, false);
            seabed.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            seabed.transform.localScale = new Vector3(28f, 1.0f, 28f);
            seabed.GetComponent<MeshRenderer>().sharedMaterial = sandMat;

            // Swaying coastal seagrass tufts framing the reef rocks
            var kelpMat = MakeLit(new Color(0.18f, 0.62f, 0.46f), 0.18f);
            for (int k = 0; k < 16; k++)
            {
                float ka = (k / 16f) * Mathf.PI * 2f + 0.15f;
                float kd = 2.1f + (k % 3) * 1.15f;
                var blade = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(blade.GetComponent<Collider>());
                blade.transform.SetParent(stageRoot.transform, false);
                blade.transform.localPosition = new Vector3(Mathf.Cos(ka) * kd, 0.36f, Mathf.Sin(ka) * kd);
                blade.transform.localScale = new Vector3(0.10f, 0.46f + (k % 3) * 0.16f, 0.06f);
                blade.transform.localEulerAngles = new Vector3(Mathf.Sin(ka) * 14f, k * 25f, Mathf.Cos(ka) * 12f);
                blade.GetComponent<MeshRenderer>().sharedMaterial = kelpMat;
            }

            // 3. Shimmering translucent water surface plane overhead
            var surfaceMat = MakeTransparent(new Color(0.24f, 0.82f, 0.90f, 0.44f));
            var waterTop = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterTop.name = "WaterSurface";
            Destroy(waterTop.GetComponent<Collider>());
            waterTop.transform.SetParent(stageRoot.transform, false);
            waterTop.transform.localPosition = new Vector3(0f, 5.25f, 0f);
            waterTop.transform.localScale = new Vector3(4.5f, 1f, 4.5f);
            waterTop.GetComponent<MeshRenderer>().sharedMaterial = surfaceMat;

            // 4. Sunlit underwater god-ray shafts slanting down from surface!
            var rayMat = MakeTransparent(new Color(0.76f, 0.98f, 1f, 0.11f));
            for (int r = 0; r < 5; r++)
            {
                var ray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ray.name = "GodRay_" + r;
                Destroy(ray.GetComponent<Collider>());
                ray.transform.SetParent(stageRoot.transform, false);
                ray.transform.localPosition = new Vector3(-3.2f + r * 1.65f, 3.1f, 1.2f + (r % 2) * 1.4f);
                ray.transform.localScale = new Vector3(0.75f + (r % 2) * 0.35f, 3.6f, 0.75f);
                ray.GetComponent<MeshRenderer>().sharedMaterial = rayMat;
                ray.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                godRays.Add(ray.transform);
            }

            // 5. Reef Boulders framing the seabed
            var rockMat = MakeLit(new Color(0.46f, 0.54f, 0.56f), 0.15f);
            Vector3[] rockPos =
            {
                new Vector3(-3.4f, 0.25f, 2.2f), new Vector3(3.2f, 0.30f, 1.8f),
                new Vector3(2.4f, 0.20f, -1.6f), new Vector3(-2.8f, 0.22f, -1.8f),
                new Vector3(0.4f, 0.35f, 3.4f)
            };
            for (int i = 0; i < rockPos.Length; i++)
            {
                var rk = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(rk.GetComponent<Collider>());
                rk.transform.SetParent(stageRoot.transform, false);
                rk.transform.localPosition = rockPos[i];
                rk.transform.localScale = new Vector3(1.6f + (i % 3) * 0.45f, 0.75f + (i % 2) * 0.35f, 1.4f);
                rk.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
            }

            // 6. Coral Restoration PVC Frame ("Rangka Restorasi Karang") center stage!
            var pvcMat = MakeLit(new Color(0.42f, 0.49f, 0.54f), 0.38f);
            var rack = new GameObject("PVC_CoralRack");
            rack.transform.SetParent(stageRoot.transform, false);
            rack.transform.localPosition = new Vector3(0.25f, 0.02f, 0f);
            void AddPipe(Vector3 p, Vector3 s, Vector3 rot)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(rack.transform, false);
                g.transform.localPosition = p;
                g.transform.localScale = s;
                g.transform.localEulerAngles = rot;
                g.GetComponent<MeshRenderer>().sharedMaterial = pvcMat;
            }
            // 4 legs + top rectangular frame + 2 crossbars
            AddPipe(new Vector3(-0.9f, 0.34f, -0.55f), new Vector3(0.08f, 0.34f, 0.08f), Vector3.zero);
            AddPipe(new Vector3( 0.9f, 0.34f, -0.55f), new Vector3(0.08f, 0.34f, 0.08f), Vector3.zero);
            AddPipe(new Vector3(-0.9f, 0.34f,  0.55f), new Vector3(0.08f, 0.34f, 0.08f), Vector3.zero);
            AddPipe(new Vector3( 0.9f, 0.34f,  0.55f), new Vector3(0.08f, 0.34f, 0.08f), Vector3.zero);
            AddPipe(new Vector3(0f, 0.66f, -0.55f), new Vector3(0.08f, 0.95f, 0.08f), new Vector3(0f, 0f, 90f));
            AddPipe(new Vector3(0f, 0.66f,  0.55f), new Vector3(0.08f, 0.95f, 0.08f), new Vector3(0f, 0f, 90f));
            AddPipe(new Vector3(-0.9f, 0.66f, 0f), new Vector3(0.08f, 0.58f, 0.08f), new Vector3(90f, 0f, 0f));
            AddPipe(new Vector3( 0.9f, 0.66f, 0f), new Vector3(0.08f, 0.58f, 0.08f), new Vector3(90f, 0f, 0f));
            AddPipe(new Vector3(0f, 0.66f, 0f),    new Vector3(0.07f, 0.92f, 0.07f), new Vector3(0f, 0f, 90f));

            // 7. Sculpted 3D Corals (6 transplantable rack fragments + 12 surrounding reef corals!)
            Color[] healthyPalette =
            {
                new Color(1.00f, 0.48f, 0.56f), // vibrant coral pink
                new Color(1.00f, 0.74f, 0.30f), // sunlit reef amber
                new Color(0.72f, 0.48f, 0.96f), // sea lavender
                new Color(0.24f, 0.88f, 0.78f), // glowing reef aqua
                new Color(1.00f, 0.56f, 0.34f)  // warm tangerine
            };

            void BuildCoralCluster(Transform parent, Vector3 localPos, float scale, Color healthy, bool rackFrag, int seed)
            {
                var holder = new GameObject(rackFrag ? "RackCoralFrag" : "ReefCoral");
                holder.transform.SetParent(parent, false);
                holder.transform.localPosition = localPos;
                var mat = MakeLit(new Color(0.84f, 0.88f, 0.89f), 0.25f);
                var rng = new System.Random(seed);
                int branches = rackFrag ? 5 : 6;
                for (int b = 0; b < branches; b++)
                {
                    var br = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    Destroy(br.GetComponent<Collider>());
                    br.transform.SetParent(holder.transform, false);
                    float ang = (b / (float)branches) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f;
                    float tilt = b == 0 ? 6f : 26f + (float)rng.NextDouble() * 18f;
                    float bh = 0.22f + (float)rng.NextDouble() * 0.16f;
                    br.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.08f, bh * 0.55f, Mathf.Sin(ang) * 0.08f);
                    br.transform.localRotation = Quaternion.Euler(Mathf.Sin(ang) * tilt, 0f, -Mathf.Cos(ang) * tilt);
                    br.transform.localScale = new Vector3(0.11f, bh, 0.11f);
                    br.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }
                var baseScale = Vector3.one * scale;
                holder.transform.localScale = baseScale;
                corals.Add(new CoralNode
                {
                    tr = holder.transform,
                    mat = mat,
                    bleachedCol = new Color(0.84f, 0.88f, 0.89f),
                    healthyCol = healthy,
                    baseScale = baseScale,
                    isRackFragment = rackFrag
                });
            }

            // 6 transplantable coral fragments neatly mounted across the PVC frame
            Vector3[] rackSpots =
            {
                new Vector3(-0.55f, 0.70f, -0.55f), new Vector3(0.0f, 0.70f, -0.55f), new Vector3(0.55f, 0.70f, -0.55f),
                new Vector3(-0.55f, 0.70f,  0.55f), new Vector3(0.0f, 0.70f,  0.55f), new Vector3(0.55f, 0.70f,  0.55f)
            };
            for (int i = 0; i < rackSpots.Length; i++)
            {
                BuildCoralCluster(rack.transform, rackSpots[i], 0.92f, healthyPalette[i % healthyPalette.Length], rackFrag: true, 400 + i * 19);
            }

            // 12 surrounding reef corals framed nicely around & behind the PVC rack
            for (int i = 0; i < 12; i++)
            {
                float ang = (i / 12f) * Mathf.PI * 2f + 0.22f;
                float dist = 1.95f + (i % 3) * 0.85f;
                Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, 0.12f, Mathf.Sin(ang) * dist + 0.35f);
                BuildCoralCluster(stageRoot.transform, pos, 1.25f + (i % 3) * 0.30f, healthyPalette[(i + 2) % healthyPalette.Length], rackFrag: false, 700 + i * 29);
            }

            // 8. Clone Real 3D Boat, Pak Darma, & Player visuals cleanly for the cutscene!
            var srcBoat = GameObject.Find("MuyayaFishingBoat_Primary");
            if (srcBoat)
            {
                var bClone = Instantiate(srcBoat, stageRoot.transform);
                bClone.name = "CutsceneBoat";
                foreach (var c in bClone.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                bClone.transform.localPosition = new Vector3(0.6f, 5.55f, 1.6f);
                stageBoat = bClone.transform;
            }

            var srcPak = GameObject.Find("PakDarma_Model");
            if (srcPak)
            {
                var dClone = Instantiate(srcPak, stageRoot.transform);
                dClone.name = "CutscenePakDarma";
                foreach (var c in dClone.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                dClone.transform.localScale = Vector3.one * 1.20f;
                stagePakDarma = dClone.transform;
                stagePakAnim = dClone.GetComponentInChildren<Animator>();
            }

            var srcPlayer = GameObject.Find("MC_CandidateVisual");
            if (srcPlayer)
            {
                var pClone = Instantiate(srcPlayer, stageRoot.transform);
                pClone.name = "CutscenePlayer";
                foreach (var c in pClone.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                pClone.transform.localScale = Vector3.one * 1.20f;
                stagePlayer = pClone.transform;
                stagePlayerAnim = pClone.GetComponentInChildren<Animator>();
            }

            // 9. Spawn Live Swimming Vibrant Opaque 3D Fish School from SAGARA's FishingVisualCatalog!
            if (fishCatalog && fishCatalog.entries != null && fishCatalog.entries.Length > 0)
            {
                for (int i = 0; i < 10; i++)
                {
                    var prefab = fishCatalog.entries[i % fishCatalog.entries.Length].prefab;
                    if (!prefab) continue;
                    var fGo = Instantiate(prefab, stageRoot.transform);
                    foreach (var c in fGo.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                    // Force crisp opaque URP/Lit materials so underwater fish render vibrant & solid (not ghost-faded!)
                    foreach (var mr in fGo.GetComponentsInChildren<Renderer>(true))
                    {
                        var srcMats = mr.sharedMaterials;
                        var opaqueMats = new Material[srcMats.Length];
                        for (int mi = 0; mi < srcMats.Length; mi++)
                        {
                            var sm = srcMats[mi];
                            var om = new Material(lit);
                            if (sm)
                            {
                                if (sm.HasProperty("_BaseMap") && sm.GetTexture("_BaseMap")) om.SetTexture("_BaseMap", sm.GetTexture("_BaseMap"));
                                else if (sm.HasProperty("_MainTex") && sm.GetTexture("_MainTex")) om.SetTexture("_BaseMap", sm.GetTexture("_MainTex"));
                                Color bc = sm.HasProperty("_BaseColor") ? sm.GetColor("_BaseColor")
                                    : (sm.HasProperty("_Color") ? sm.GetColor("_Color") : Color.white);
                                bc.a = 1f;
                                om.SetColor("_BaseColor", bc);
                            }
                            om.SetFloat("_Smoothness", 0.35f);
                            runtimeMats.Add(om);
                            opaqueMats[mi] = om;
                        }
                        mr.sharedMaterials = opaqueMats;
                    }
                    float rad = 2.0f + (i % 4) * 0.78f;
                    float ang = (i / 10f) * Mathf.PI * 2f;
                    float by = 0.85f + (i % 4) * 0.58f;
                    fGo.transform.localPosition = new Vector3(Mathf.Cos(ang) * rad, by, Mathf.Sin(ang) * rad);
                    fGo.transform.localScale = Vector3.one * (0.56f + (i % 3) * 0.14f);
                    fishes.Add(new FishSwimmer
                    {
                        tr = fGo.transform,
                        orbitRad = rad,
                        speed = 0.42f + (i % 3) * 0.18f,
                        angle = ang,
                        baseY = by,
                        bobPhase = i * 1.3f,
                        dirSign = i % 2 == 0 ? 1f : -1f
                    });
                }
            }

            // 10. Continuous Rising 3D Bubbles
            var bubMat = MakeTransparent(new Color(0.88f, 0.99f, 1f, 0.58f));
            for (int i = 0; i < 24; i++)
            {
                var bg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(bg.GetComponent<Collider>());
                bg.transform.SetParent(stageRoot.transform, false);
                float bx = StageOrigin.x + UnityEngine.Random.Range(-3.6f, 3.6f);
                float bz = StageOrigin.z + UnityEngine.Random.Range(-2.6f, 2.8f);
                float by = StageOrigin.y + UnityEngine.Random.Range(0.2f, 4.8f);
                bg.transform.position = new Vector3(bx, by, bz);
                float bs = UnityEngine.Random.Range(0.05f, 0.13f);
                bg.transform.localScale = Vector3.one * bs;
                bg.GetComponent<MeshRenderer>().sharedMaterial = bubMat;
                bg.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bubbles.Add(new BubbleNode
                {
                    tr = bg.transform,
                    speed = UnityEngine.Random.Range(0.55f, 1.25f),
                    baseX = bx,
                    baseZ = bz,
                    phase = i * 0.9f,
                    minY = StageOrigin.y + 0.15f,
                    maxY = StageOrigin.y + 5.15f
                });
            }
        }

        private void CleanupStage()
        {
            if (stageRoot)
            {
                Destroy(stageRoot);
                stageRoot = null;
            }
            stageBoat = stagePakDarma = stagePlayer = null;
            stagePlayerAnim = stagePakAnim = null;
            corals.Clear();
            fishes.Clear();
            bubbles.Clear();
            godRays.Clear();
            for (int i = 0; i < runtimeMats.Count; i++)
            {
                if (runtimeMats[i]) Destroy(runtimeMats[i]);
            }
            runtimeMats.Clear();
        }

        private void SaveAndTakeCamera(Camera cam)
        {
            camWasOrtho = cam.orthographic;
            camSavedOrtho = cam.orthographicSize;
            camSavedFov = cam.fieldOfView;
            camSavedPos = cam.transform.position;
            camSavedRot = cam.transform.rotation;
            camSavedClear = cam.clearFlags;
            camSavedBg = cam.backgroundColor;
            savedFog = RenderSettings.fog;
            savedFogColor = RenderSettings.fogColor;
            savedFogMode = RenderSettings.fogMode;
            savedFogStart = RenderSettings.fogStartDistance;
            savedFogEnd = RenderSettings.fogEndDistance;

            var follow = cam.GetComponent<PlayerCameraFollow>();
            var pres = cam.GetComponent<DermagaCameraPresentation>();
            var clamp = cam.GetComponent<DermagaEntranceWideZoomClamp>();
            if (follow) follow.enabled = false;
            if (pres) pres.enabled = false;
            if (clamp) clamp.enabled = false;

            if (cleanupHud)
            {
                var doc = cleanupHud.GetComponent<UIDocument>();
                if (doc && doc.rootVisualElement != null)
                {
                    hudWasVisible = doc.rootVisualElement.style.display != DisplayStyle.None;
                    doc.rootVisualElement.style.display = DisplayStyle.None;
                }
            }
            cameraSaved = true;
        }

        private void RestoreCamera()
        {
            if (!cameraSaved) return;
            var cam = Camera.main;
            if (cam)
            {
                cam.orthographic = camWasOrtho;
                cam.orthographicSize = camSavedOrtho;
                cam.fieldOfView = camSavedFov;
                cam.clearFlags = camSavedClear;
                cam.backgroundColor = camSavedBg;
                cam.transform.SetPositionAndRotation(camSavedPos, camSavedRot);
            }
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogStartDistance = savedFogStart;
            RenderSettings.fogEndDistance = savedFogEnd;
            if (cleanupHud && hudWasVisible)
            {
                var doc = cleanupHud.GetComponent<UIDocument>();
                if (doc && doc.rootVisualElement != null)
                {
                    doc.rootVisualElement.style.display = DisplayStyle.Flex;
                }
            }
            cameraSaved = false;
        }

        private void BuildUi()
        {
            if (!document || document.rootVisualElement == null) return;
            document.rootVisualElement.pickingMode = PickingMode.Ignore;
            document.rootVisualElement.StretchToParentSize();

            root = new VisualElement { name = "DermagaCoral3DCutsceneUI", pickingMode = PickingMode.Ignore };
            root.StretchToParentSize();
            document.rootVisualElement.Add(root);

            fadeVeil = new VisualElement { pickingMode = PickingMode.Ignore };
            fadeVeil.StretchToParentSize();
            fadeVeil.style.backgroundColor = new Color(0.03f, 0.14f, 0.20f, 1f);
            fadeVeil.style.opacity = 0f;
            root.Add(fadeVeil);

            // Smooth Floating Coastal Subtitle Card (zero AI-slop box!)
            subtitleCard = new VisualElement { pickingMode = PickingMode.Ignore };
            subtitleCard.style.position = Position.Absolute;
            subtitleCard.style.bottom = 32;
            subtitleCard.style.alignSelf = Align.Center;
            subtitleCard.style.maxWidth = 760;
            subtitleCard.style.alignItems = Align.Center;
            subtitleCard.style.paddingLeft = 34;
            subtitleCard.style.paddingRight = 34;
            subtitleCard.style.paddingTop = 14;
            subtitleCard.style.paddingBottom = 16;
            subtitleCard.style.backgroundColor = new Color(0.04f, 0.16f, 0.22f, 0.86f);
            subtitleCard.style.borderTopLeftRadius = 22;
            subtitleCard.style.borderTopRightRadius = 22;
            subtitleCard.style.borderBottomLeftRadius = 22;
            subtitleCard.style.borderBottomRightRadius = 22;
            var rim = new Color(1f, 0.93f, 0.74f, 0.46f);
            subtitleCard.style.borderTopWidth = 1.4f;
            subtitleCard.style.borderBottomWidth = 1.4f;
            subtitleCard.style.borderLeftWidth = 1.4f;
            subtitleCard.style.borderRightWidth = 1.4f;
            subtitleCard.style.borderTopColor = rim;
            subtitleCard.style.borderBottomColor = rim;
            subtitleCard.style.borderLeftColor = rim;
            subtitleCard.style.borderRightColor = rim;
            root.Add(subtitleCard);

            speakerLabel = new Label("Pak Darma") { pickingMode = PickingMode.Ignore };
            ApplyFont(speakerLabel, display: true);
            speakerLabel.style.fontSize = 12;
            speakerLabel.style.letterSpacing = 2.0f;
            speakerLabel.style.color = new Color(1f, 0.86f, 0.44f, 1f);
            speakerLabel.style.marginBottom = 4;
            subtitleCard.Add(speakerLabel);

            lineLabel = new Label("") { pickingMode = PickingMode.Ignore };
            ApplyFont(lineLabel, display: false);
            lineLabel.style.fontSize = 19;
            lineLabel.style.whiteSpace = WhiteSpace.Normal;
            lineLabel.style.color = new Color(0.99f, 0.97f, 0.91f, 1f);
            lineLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitleCard.Add(lineLabel);

            // Bottom-right sleek floating action pills ([E] Lanjut + [Esc] Lewati)
            var btnRow = new VisualElement { pickingMode = PickingMode.Ignore };
            btnRow.style.position = Position.Absolute;
            btnRow.style.right = 26;
            btnRow.style.bottom = 26;
            btnRow.style.flexDirection = FlexDirection.Row;
            root.Add(btnRow);

            nextButton = MakePillButton("E", "Lanjut", () => advanceRequested = true, isPrimary: true);
            nextButton.style.marginRight = 10;
            btnRow.Add(nextButton);

            skipButton = MakePillButton("Esc", "Lewati", () => skipRequested = true, isPrimary: false);
            btnRow.Add(skipButton);
        }

        private Button MakePillButton(string key, string label, Action onClick, bool isPrimary)
        {
            var btn = new Button(onClick) { text = "", focusable = false };
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.paddingLeft = 12;
            btn.style.paddingRight = 15;
            btn.style.paddingTop = 6;
            btn.style.paddingBottom = 6;
            btn.style.borderTopLeftRadius = 16;
            btn.style.borderTopRightRadius = 16;
            btn.style.borderBottomLeftRadius = 16;
            btn.style.borderBottomRightRadius = 16;
            btn.style.backgroundColor = isPrimary
                ? new Color(0.14f, 0.50f, 0.48f, 0.92f)
                : new Color(0.06f, 0.19f, 0.25f, 0.80f);
            var rim = new Color(1f, 0.93f, 0.74f, isPrimary ? 0.65f : 0.40f);
            btn.style.borderTopWidth = 1.2f;
            btn.style.borderBottomWidth = 1.2f;
            btn.style.borderLeftWidth = 1.2f;
            btn.style.borderRightWidth = 1.2f;
            btn.style.borderTopColor = rim;
            btn.style.borderBottomColor = rim;
            btn.style.borderLeftColor = rim;
            btn.style.borderRightColor = rim;

            var k = new Label(key) { pickingMode = PickingMode.Ignore };
            ApplyFont(k, display: true);
            k.style.fontSize = 11;
            k.style.color = new Color(1f, 0.88f, 0.50f, 1f);
            k.style.marginRight = 7;
            btn.Add(k);

            var l = new Label(label) { pickingMode = PickingMode.Ignore };
            ApplyFont(l, display: true);
            l.style.fontSize = 12;
            l.style.color = new Color(0.99f, 0.97f, 0.91f, 1f);
            btn.Add(l);
            return btn;
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

        private void SetUiVisible(bool visible)
        {
            if (root != null)
            {
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
