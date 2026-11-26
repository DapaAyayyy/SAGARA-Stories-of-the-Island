using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Sagara.DermagaV2
{
    /// <summary>
    /// VideoPlayer-based Coral Restoration Cutscene (016_SteamPolish).
    /// Plays Assets/SAGARA/Video/CoralExpedition_Cutscene.mp4 seamlessly right after the 3D boat sailing
    /// sequence, with timed cozy coastal Pak Darma subtitles, smooth fades, and [Esc] Lewati skip.
    /// Drop-in ready: replacing CoralExpedition_Cutscene.mp4 with any external 3D animated MP4 works automatically!
    /// </summary>
    [DefaultExecutionOrder(312)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class DermagaCoralVideoCutscene : MonoBehaviour
    {
        [Header("Video Asset")]
        [SerializeField] private VideoClip cutsceneClip;
        [SerializeField] private DermagaCleanupHud cleanupHud;

        [Header("Typography")]
        [SerializeField] private Font displayFont;
        [SerializeField] private Font bodyFont;
        [SerializeField] private FontAsset displayFontAsset;
        [SerializeField] private FontAsset bodyFontAsset;

        public static bool IsPlaying { get; private set; }

        private UIDocument document;
        private VideoPlayer videoPlayer;
        private RenderTexture renderTex;

        private VisualElement root;
        private Image videoImage;
        private VisualElement fadeVeil;
        private VisualElement subtitleCard;
        private Label speakerLabel;
        private Label lineLabel;
        private Button skipButton;

        private Coroutine routine;
        private Action onComplete;
        private bool skipRequested;
        private bool hudWasVisible;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (document) document.sortingOrder = 120;
            videoPlayer = GetComponent<VideoPlayer>();
            if (!cleanupHud) cleanupHud = FindFirstObjectByType<DermagaCleanupHud>();

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
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
            if (videoPlayer && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }
            if (IsPlaying)
            {
                RestoreHud();
                IsPlaying = false;
            }
            if (renderTex)
            {
                renderTex.Release();
                Destroy(renderTex);
                renderTex = null;
            }
            root?.RemoveFromHierarchy();
        }

        private void Update()
        {
            if (!IsPlaying) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                skipRequested = true;
            }
        }

        public bool HasVideo => cutsceneClip != null;

        public void PlayCutscene(Action onFinished)
        {
            if (IsPlaying) return;
            if (!cutsceneClip)
            {
                onFinished?.Invoke();
                return;
            }
            onComplete = onFinished;
            skipRequested = false;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            IsPlaying = true;
            HideHud();
            EnsureRenderTexture();
            SetUiVisible(true);
            fadeVeil.style.opacity = 1f;
            subtitleCard.style.opacity = 0f;

            videoPlayer.clip = cutsceneClip;
            videoPlayer.audioOutputMode = cutsceneClip.audioTrackCount > 0
                ? VideoAudioOutputMode.Direct
                : VideoAudioOutputMode.None;
            videoPlayer.targetTexture = renderTex;
            videoImage.image = renderTex;

            bool reachedEnd = false;
            VideoPlayer.EventHandler onLoopEnd = _ => reachedEnd = true;
            videoPlayer.loopPointReached += onLoopEnd;

            videoPlayer.Prepare();

            float prepTimeout = 5.0f;
            while (!videoPlayer.isPrepared && prepTimeout > 0f && !skipRequested)
            {
                prepTimeout -= Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                yield return null;
            }

            if (videoPlayer.isPrepared && !skipRequested)
            {
                videoPlayer.Play();
                double duration = videoPlayer.length > 0.5 ? videoPlayer.length : 16.5;
                float elapsed = 0f;

                while (!skipRequested && !reachedEnd && elapsed < (float)duration + 0.35f)
                {
                    if (!videoPlayer.isPaused)
                    {
                        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                        elapsed += dt;
                    }
                    float t = (videoPlayer.isPlaying && videoPlayer.time > 0.01) ? (float)videoPlayer.time : elapsed;

                    videoImage.MarkDirtyRepaint();

                    // Fade in cleanly over first 0.45s
                    float fadeIn = Mathf.Clamp01(elapsed / 0.45f);
                    float fadeOut = Mathf.Clamp01(((float)duration - t) / 0.45f);
                    fadeVeil.style.opacity = 1f - Mathf.Min(fadeIn, fadeOut);

                    // Sync timed Pak Darma subtitles across the 3 narrative chapters of the video
                    string caption;
                    if (t < duration * 0.31f)
                    {
                        caption = "Lihat ke sana, Nak. Sebagian terumbu karang di perairan ini mulai memutih dan rusak.";
                    }
                    else if (t < duration * 0.67f)
                    {
                        caption = "Pelan-pelan, Nak. Pastikan setiap fragmen terpasang kuat tanpa merusak karang di sekitarnya.";
                    }
                    else
                    {
                        caption = "Lihat! Terumbu karang mulai pulih dan ikan-ikan kembali meramaikan perairan Sagara!";
                    }

                    if (lineLabel.text != caption)
                    {
                        lineLabel.text = caption;
                    }

                    float subVis = Mathf.Clamp01((elapsed - 0.3f) / 0.4f) * fadeOut;
                    subtitleCard.style.opacity = subVis;
                    yield return null;
                }
            }

            videoPlayer.loopPointReached -= onLoopEnd;

            // Smooth ocean-blue fade out back to dock
            float ft = 0f;
            while (ft < 0.35f)
            {
                ft += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                fadeVeil.style.opacity = Mathf.Clamp01(ft / 0.35f);
                yield return null;
            }

            videoPlayer.Stop();
            SetUiVisible(false);
            RestoreHud();
            IsPlaying = false;
            routine = null;

            var cb = onComplete;
            onComplete = null;
            cb?.Invoke();
        }

        private void EnsureRenderTexture()
        {
            if (renderTex != null) return;
            renderTex = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32)
            {
                name = "CoralCutsceneVideoRT",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            renderTex.Create();
        }

        private void HideHud()
        {
            if (cleanupHud)
            {
                var doc = cleanupHud.GetComponent<UIDocument>();
                if (doc && doc.rootVisualElement != null)
                {
                    hudWasVisible = doc.rootVisualElement.style.display != DisplayStyle.None;
                    doc.rootVisualElement.style.display = DisplayStyle.None;
                }
            }
        }

        private void RestoreHud()
        {
            if (cleanupHud && hudWasVisible)
            {
                var doc = cleanupHud.GetComponent<UIDocument>();
                if (doc && doc.rootVisualElement != null)
                {
                    doc.rootVisualElement.style.display = DisplayStyle.Flex;
                }
            }
        }

        private void BuildUi()
        {
            if (!document || document.rootVisualElement == null) return;
            document.rootVisualElement.pickingMode = PickingMode.Ignore;
            document.rootVisualElement.StretchToParentSize();

            root = new VisualElement { name = "DermagaCoralVideoCutsceneUI", pickingMode = PickingMode.Position };
            root.StretchToParentSize();
            root.style.backgroundColor = new Color(0.03f, 0.13f, 0.18f, 1f);
            document.rootVisualElement.Add(root);

            videoImage = new Image { scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            videoImage.StretchToParentSize();
            root.Add(videoImage);

            fadeVeil = new VisualElement { pickingMode = PickingMode.Ignore };
            fadeVeil.StretchToParentSize();
            fadeVeil.style.backgroundColor = new Color(0.03f, 0.13f, 0.18f, 1f);
            fadeVeil.style.opacity = 0f;
            root.Add(fadeVeil);

            // Smooth Floating Coastal Subtitle Pill
            subtitleCard = new VisualElement { pickingMode = PickingMode.Ignore };
            subtitleCard.style.position = Position.Absolute;
            subtitleCard.style.bottom = 30;
            subtitleCard.style.alignSelf = Align.Center;
            subtitleCard.style.maxWidth = 760;
            subtitleCard.style.alignItems = Align.Center;
            subtitleCard.style.paddingLeft = 32;
            subtitleCard.style.paddingRight = 32;
            subtitleCard.style.paddingTop = 12;
            subtitleCard.style.paddingBottom = 14;
            subtitleCard.style.backgroundColor = new Color(0.04f, 0.15f, 0.21f, 0.84f);
            subtitleCard.style.borderTopLeftRadius = 20;
            subtitleCard.style.borderTopRightRadius = 20;
            subtitleCard.style.borderBottomLeftRadius = 20;
            subtitleCard.style.borderBottomRightRadius = 20;
            var rim = new Color(1f, 0.93f, 0.74f, 0.44f);
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

            // Bottom-right sleek floating Skip button
            skipButton = new Button(() => { DermagaUiAudio.PlayClick(); skipRequested = true; }) { text = "", focusable = false };
            skipButton.style.position = Position.Absolute;
            skipButton.style.right = 26;
            skipButton.style.bottom = 26;
            skipButton.style.flexDirection = FlexDirection.Row;
            skipButton.style.alignItems = Align.Center;
            skipButton.style.paddingLeft = 12;
            skipButton.style.paddingRight = 15;
            skipButton.style.paddingTop = 6;
            skipButton.style.paddingBottom = 6;
            skipButton.style.borderTopLeftRadius = 15;
            skipButton.style.borderTopRightRadius = 15;
            skipButton.style.borderBottomLeftRadius = 15;
            skipButton.style.borderBottomRightRadius = 15;
            skipButton.style.backgroundColor = new Color(0.06f, 0.19f, 0.25f, 0.84f);
            skipButton.style.borderTopWidth = 1.2f;
            skipButton.style.borderBottomWidth = 1.2f;
            skipButton.style.borderLeftWidth = 1.2f;
            skipButton.style.borderRightWidth = 1.2f;
            skipButton.style.borderTopColor = rim;
            skipButton.style.borderBottomColor = rim;
            skipButton.style.borderLeftColor = rim;
            skipButton.style.borderRightColor = rim;
            root.Add(skipButton);

            var k = new Label("Esc") { pickingMode = PickingMode.Ignore };
            ApplyFont(k, display: true);
            k.style.fontSize = 11;
            k.style.color = new Color(1f, 0.88f, 0.50f, 1f);
            k.style.marginRight = 7;
            skipButton.Add(k);

            var l = new Label("Lewati") { pickingMode = PickingMode.Ignore };
            ApplyFont(l, display: true);
            l.style.fontSize = 12;
            l.style.color = new Color(0.99f, 0.97f, 0.91f, 1f);
            skipButton.Add(l);
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
