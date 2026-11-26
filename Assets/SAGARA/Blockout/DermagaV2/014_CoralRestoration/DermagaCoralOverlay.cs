using System;
using System.Collections.Generic;
using Sagara.Dermaga.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Sagara.DermagaV2
{
    internal sealed class DermagaCoralOverlay
    {
        internal struct Icons
        {
            public Texture2D journal;
            public Texture2D fish;
            public Texture2D coral;
            public Texture2D locked;
            public Texture2D close;
            public Texture2D rotate;
            public Texture2D bookSpread;
        }

        private static readonly string[] Narration =
        {
            "Lihat ke sana, Nak. Sebagian terumbu karang di perairan ini mulai rusak.",
            "Padahal, karang menjadi tempat berlindung bagi banyak ikan dan makhluk laut. Kita harus membantu memulihkannya.",
            "Kita gunakan fragmen karang dari pembibitan, lalu memasangnya pada rangka yang sudah ditambatkan.",
            "Pelan-pelan, Nak. Pastikan setiap fragmen terpasang kuat tanpa merusak karang di sekitarnya.",
            "Lihat betapa luasnya laut kita. Rangka yang kita pasang memang kecil, tetapi ini langkah penting.",
            "Karang-karang ini masih membutuhkan waktu untuk tumbuh. Kita akan terus menjaga dan memantau perkembangannya."
        };

        private const float FadeSeconds = .48f;
        private const float BookOpenDuration = .22f;
        private const float BookCloseDuration = .18f;

        // Top-right HUD Journal button (always visible from start; strictly locked until FishingUnlocked!)
        private readonly Button journalShortcut;
        private readonly VisualElement shortcutIcon;
        private bool shortcutUnlocked;
        private float shortcutShakeTimer;

        // Consistent non-AI-slop frosted coastal toast for locked feedback
        private readonly VisualElement lockedToast;
        private readonly VisualElement lockedToastBg;
        private readonly Label lockedToastLabel;
        private float lockedToastUntil;
        private float lockedToastVis;

        // Cinematic coral expedition overlay
        private readonly VisualElement cinematic;
        private readonly VisualElement frame;
        private readonly VisualElement[] stills = new VisualElement[2];
        private readonly VisualElement cinemaScrim;
        private readonly VisualElement dialoguePanel;
        private readonly Label narration;
        private readonly VisualElement cinemaContinueArrow;
        private readonly VisualElement cinematicKeys;

        // Beloved 2-Page Living 3D Field Journal (Left: Header + Tabs + 5 Tactile 3D Cards | Right: Spacious Live 3D Diorama + Crisp Notes)
        private readonly VisualElement journal;
        private readonly VisualElement journalDeskBg;
        private readonly VisualElement bookSpread;
        private readonly Label journalCount;
        private readonly VisualElement progressFill;
        private readonly Button fishTab;
        private readonly Button coralTab;
        private readonly VisualElement coralTabIcon;
        private readonly VisualElement grid;
        private readonly VisualElement detail;
        private VisualElement activeAquariumBg;
        private VisualElement activeDetailWrap;
        private Button selectedCard;

        private readonly List<Button> activeCards = new List<Button>();
        private readonly List<Action> cardSelectActions = new List<Action>();

        private readonly Texture2D[] illustrations;
        private readonly Font displayFont;
        private readonly Font bodyFont;
        private readonly FontAsset displayFontAsset;
        private readonly FontAsset bodyFontAsset;
        private readonly Icons icons;
        private readonly DermagaJournalPreview preview;

        private DermagaIntroProgress progress;
        private FishingConfiguration fishing;
        private FishingVisualCatalog fishCatalog;
        private DermagaCoralExpedition.CoralRecord[] corals;
        private bool currentIsCoral;
        private int currentIndex;

        // Clean, snappy living UI animation state (NO drawn paper-flip overlay!)
        private float bookOpenAnim = 1f;
        private bool bookClosing;
        private float bookCloseAnim;
        private Action onJournalClosed;

        private float cardsAppearTime;
        private float detailPopAnim = 1f;
        private float coralTabShakeTimer;

        // Expedition state
        private int line;
        private int front;
        private int imageIndex;
        private float fadeStarted;
        private float sceneStarted;
        private float nextPress;
        private float exitStarted;
        private float exitOpacity;
        private Action onExit;
        public bool Exiting => onExit != null;

        internal DermagaCoralOverlay(VisualElement root, StyleSheet style, Font displayFont, Font bodyFont,
            FontAsset displayFontAsset, FontAsset bodyFontAsset,
            Texture2D[] illustrations, Shader previewShader, Icons icons, Action showJournal, Action closeJournal, Action advance, Action finish)
        {
            this.illustrations = illustrations;
            this.icons = icons;
            this.displayFont = displayFont;
            this.bodyFont = bodyFont;
            this.displayFontAsset = displayFontAsset;
            this.bodyFontAsset = bodyFontAsset;
            preview = new DermagaJournalPreview(previewShader);

            root.pickingMode = PickingMode.Ignore;
            root.StretchToParentSize();
            ApplyBodyFont(root);
            if (style)
            {
                root.styleSheets.Add(style);
            }

            // --- TOP-RIGHT "JURNAL" BUTTON (VISIBLE FROM START; LOCKED UNTIL QUEST DONE!) ---
            journalShortcut = new Button(() =>
            {
                if (shortcutUnlocked)
                {
                    showJournal();
                }
                else
                {
                    ShowShortcutLockedToast("Jurnal Pesisir terbuka setelah perbaikan dermaga selesai.");
                }
            }) { text = "", focusable = false };
            journalShortcut.AddToClassList("sagara-journal-shortcut");
            root.Add(journalShortcut);
            shortcutIcon = MakeElement(journalShortcut, "sagara-shortcut-icon", ignorePicking: true);
            if (icons.journal)
            {
                shortcutIcon.style.backgroundImage = new StyleBackground(icons.journal);
            }
            MakeLabel(journalShortcut, "Jurnal", "sagara-shortcut-text", title: true);
            AttachTactileFeedback(journalShortcut);

            // --- CINEMATIC CORAL EXPEDITION OVERLAY ---
            cinematic = MakeElement(root, "sagara-cinema");
            frame = MakeElement(cinematic, "sagara-still-frame");
            cinematic.RegisterCallback<GeometryChangedEvent>(ResizeFrame);
            stills[0] = MakeElement(frame, "sagara-still");
            stills[1] = MakeElement(frame, "sagara-still");
            foreach (var still in stills)
            {
                still.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            }

            cinemaScrim = MakeElement(frame, "sagara-cinema-scrim", ignorePicking: true);
            cinemaScrim.generateVisualContent += DrawCinemaScrim;

            dialoguePanel = MakeElement(frame, "sagara-cinema-dialogue", ignorePicking: true);
            var speakerRow = MakeElement(dialoguePanel, "sagara-cinema-speaker-row", ignorePicking: true);
            MakeElement(speakerRow, "sagara-cinema-speaker-line", ignorePicking: true);
            MakeLabel(speakerRow, "Pak Darma", "sagara-cinema-speaker", title: true);
            MakeElement(speakerRow, "sagara-cinema-speaker-line", ignorePicking: true);

            narration = MakeLabel(dialoguePanel, "", "sagara-narration", title: true);
            cinemaContinueArrow = MakeElement(dialoguePanel, "sagara-cinema-continue-arrow", ignorePicking: true);
            cinemaContinueArrow.generateVisualContent += DrawContinueChevron;

            cinematicKeys = MakeElement(frame, "sagara-cinema-keys");
            var nextButton = new Button(() => { DermagaUiAudio.PlayClick(); advance(); }) { text = "", focusable = false };
            nextButton.AddToClassList("sagara-cinema-key");
            nextButton.AddToClassList("sagara-cinema-key-primary");
            AttachTactileFeedback(nextButton);
            cinematicKeys.Add(nextButton);
            var nextCap = MakeElement(nextButton, "sagara-cinema-keycap", ignorePicking: true);
            MakeLabel(nextCap, "E", "sagara-cinema-keycap-text", title: true);
            MakeLabel(nextButton, "Lanjut", "sagara-cinema-key-label", title: true);

            var skipButton = new Button(() => { DermagaUiAudio.PlayClick(); finish(); }) { text = "", focusable = false };
            skipButton.AddToClassList("sagara-cinema-key");
            AttachTactileFeedback(skipButton);
            cinematicKeys.Add(skipButton);
            var skipCap = MakeElement(skipButton, "sagara-cinema-keycap", ignorePicking: true);
            MakeLabel(skipCap, "Esc", "sagara-cinema-keycap-text", title: true);
            MakeLabel(skipButton, "Lewati", "sagara-cinema-key-label", title: true);

            // --- LIVING 3D OPEN FIELD BOOK ("JURNAL PESISIR") ---
            journal = MakeElement(root, "sagara-journal");

            // Warm sunlit coastal ambience + drifting sunlight motes behind the book
            journalDeskBg = MakeElement(journal, "sagara-desk-ambience", ignorePicking: true);
            journalDeskBg.generateVisualContent += DrawDeskAmbience;

            // Physical Open Leather Hardcover Book
            bookSpread = MakeElement(journal, "sagara-book-spread");
            if (icons.bookSpread)
            {
                bookSpread.style.backgroundImage = new StyleBackground(icons.bookSpread);
            }

            // Top Header Row across the two pages (Left: Book Icon + Title + Progress | Right: Clean Tutup ×)
            var topBar = MakeElement(bookSpread, "sagara-book-topbar");
            var titleGroup = MakeElement(topBar, "sagara-title-group");
            var titleIcon = MakeElement(titleGroup, "sagara-title-icon", ignorePicking: true);
            if (icons.journal)
            {
                titleIcon.style.backgroundImage = new StyleBackground(icons.journal);
            }
            var titleStack = MakeElement(titleGroup, "sagara-title-stack");
            MakeLabel(titleStack, "Jurnal Pesisir", "sagara-journal-title", title: true);
            var progressRow = MakeElement(titleStack, "sagara-progress-row");
            var progressTrack = MakeElement(progressRow, "sagara-progress-track", ignorePicking: true);
            progressFill = MakeElement(progressTrack, "sagara-progress-fill", ignorePicking: true);
            journalCount = MakeLabel(progressRow, "", "sagara-journal-count", title: true);

            // Clean tactile 3D Tutup × button (zero clunky Esc badge!)
            var closeButton = new Button(() => { DermagaUiAudio.PlayClick(); closeJournal(); }) { text = "", focusable = false };
            closeButton.AddToClassList("sagara-close");
            AttachTactileFeedback(closeButton);
            topBar.Add(closeButton);
            MakeLabel(closeButton, "Tutup", "sagara-close-text", title: true);
            if (icons.close)
            {
                var cx = MakeElement(closeButton, "sagara-close-icon", ignorePicking: true);
                cx.style.backgroundImage = new StyleBackground(icons.close);
            }

            // Two-Page Open Spread Columns (Left: Category Tabs + 5 Tactile 3D Cards | Right: Live 3D Diorama + Notes)
            var columns = MakeElement(bookSpread, "sagara-spread-columns");

            // === LEFT PAGE ===
            var leftPage = MakeElement(columns, "sagara-left-page");
            var tabs = MakeElement(leftPage, "sagara-tabs");
            fishTab = BuildTabButton(tabs, "Koleksi Ikan", icons.fish, () => RequestCategorySwitch(false), out _);
            coralTab = BuildTabButton(tabs, "Koleksi Karang", icons.coral, () => RequestCategorySwitch(true), out coralTabIcon);
            grid = MakeElement(leftPage, "sagara-grid");

            // === RIGHT PAGE ===
            var rightPage = MakeElement(columns, "sagara-right-page");
            detail = MakeElement(rightPage, "sagara-detail");

            // --- CONSISTENT NON-AI-SLOP FROSTED COASTAL LOCKED TOAST ---
            lockedToast = MakeElement(root, "sagara-locked-toast", ignorePicking: true);
            lockedToastBg = MakeElement(lockedToast, "sagara-locked-toast-bg", ignorePicking: true);
            lockedToastBg.generateVisualContent += DrawLockedToastBg;
            if (icons.locked)
            {
                var li = MakeElement(lockedToast, "sagara-locked-toast-icon", ignorePicking: true);
                li.style.backgroundImage = new StyleBackground(icons.locked);
            }
            lockedToastLabel = MakeLabel(lockedToast, "", "sagara-locked-toast-text", title: true);
            lockedToast.style.display = DisplayStyle.None;

            cinematic.style.display = DisplayStyle.None;
            journal.style.display = DisplayStyle.None;
            journalShortcut.style.display = DisplayStyle.None;
        }

        private Button BuildTabButton(VisualElement parent, string label, Texture2D iconTex, Action onClick, out VisualElement iconEl)
        {
            var btn = new Button(onClick) { text = "", focusable = false };
            btn.AddToClassList("sagara-tab");
            AttachTactileFeedback(btn);
            parent.Add(btn);
            iconEl = MakeElement(btn, "sagara-tab-icon", ignorePicking: true);
            if (iconTex)
            {
                iconEl.style.backgroundImage = new StyleBackground(iconTex);
            }
            MakeLabel(btn, label, "sagara-tab-label", title: true);
            return btn;
        }

        internal void SetJournalShortcut(bool visible, bool unlocked = true)
        {
            shortcutUnlocked = unlocked;
            journalShortcut.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            journalShortcut.EnableInClassList("sagara-shortcut-locked", !unlocked);
            if (shortcutIcon != null)
            {
                Texture2D tex = unlocked ? icons.journal : (icons.locked ? icons.locked : icons.journal);
                if (tex)
                {
                    shortcutIcon.style.backgroundImage = new StyleBackground(tex);
                }
            }
        }

        internal void PulseJournalShortcut()
        {
            journalShortcut.AddToClassList("sagara-ui-pressed");
            journalShortcut.schedule.Execute(() => journalShortcut.RemoveFromClassList("sagara-ui-pressed")).StartingIn(140);
        }

        internal void ShowShortcutLockedToast(string message)
        {
            DermagaUiAudio.PlayLocked();
            shortcutShakeTimer = 0.34f;
            ShowToast(message);
        }

        private void ShowToast(string message)
        {
            lockedToastLabel.text = message;
            lockedToastUntil = Time.unscaledTime + 3.0f;
            lockedToast.BringToFront();
        }

        internal void UpdateHudFeedback()
        {
            float dt = Time.unscaledDeltaTime;
            if (shortcutShakeTimer > 0f)
            {
                shortcutShakeTimer = Mathf.Max(0f, shortcutShakeTimer - dt);
                float t = shortcutShakeTimer / 0.34f;
                float shakeX = Mathf.Sin(t * Mathf.PI * 5f) * 5.5f * t;
                journalShortcut.style.translate = new Translate(shakeX, 0f, 0f);
            }
            else
            {
                journalShortcut.style.translate = new Translate(0f, 0f, 0f);
            }

            UpdateLockedToast(dt);
        }

        private void UpdateLockedToast(float dt)
        {
            bool want = Time.unscaledTime < lockedToastUntil;
            lockedToastVis = Mathf.MoveTowards(lockedToastVis, want ? 1f : 0f, dt * (want ? 7.5f : 5.5f));
            bool show = lockedToastVis > 0.01f;
            lockedToast.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                float e = Smooth(lockedToastVis);
                lockedToast.style.opacity = e;
                lockedToast.style.translate = new Translate(0f, (1f - e) * -10f, 0f);
                lockedToastBg.MarkDirtyRepaint();
            }
        }

        // --- EXPEDITION CINEMATIC ---

        internal void OpenExpedition()
        {
            onExit = null;
            line = 0;
            front = 0;
            imageIndex = 0;
            fadeStarted = Time.unscaledTime;
            sceneStarted = fadeStarted;
            nextPress = fadeStarted + FadeSeconds;
            cinematic.style.display = DisplayStyle.Flex;
            cinematic.style.opacity = 0;
            dialoguePanel.style.display = DisplayStyle.Flex;
            stills[front].style.backgroundImage = new StyleBackground(illustrations[0]);
            stills[front].style.opacity = 1;
            stills[1 - front].style.opacity = 0;
            ResizeFrame(null);
            UpdateNarration();
        }

        internal void AdvanceExpedition(Action returnToDock)
        {
            if (Exiting || Time.unscaledTime < nextPress)
            {
                return;
            }

            line++;
            nextPress = Time.unscaledTime + .16f;
            if (line >= Narration.Length)
            {
                returnToDock();
                return;
            }

            int nextImage = line / 2;
            if (nextImage != imageIndex)
            {
                front = 1 - front;
                imageIndex = nextImage;
                stills[front].style.opacity = 0f;
                stills[front].style.backgroundImage = new StyleBackground(illustrations[imageIndex]);
                stills[front].BringToFront();
                sceneStarted = Time.unscaledTime;
                nextPress = sceneStarted + FadeSeconds;
                cinemaScrim.BringToFront();
                dialoguePanel.BringToFront();
                cinematicKeys.BringToFront();
            }
            UpdateNarration();
        }

        internal void UpdateExpedition()
        {
            float now = Time.unscaledTime;
            if (Exiting)
            {
                float fadeOut = Mathf.Clamp01((now - exitStarted) / FadeSeconds);
                cinematic.style.opacity = exitOpacity * (1f - Smooth(fadeOut));
                if (fadeOut >= 1f)
                {
                    cinematic.style.display = DisplayStyle.None;
                    Action callback = onExit;
                    onExit = null;
                    callback();
                }
                return;
            }

            cinematic.style.opacity = Smooth(Mathf.Clamp01((now - fadeStarted) / FadeSeconds));
            float cross = Smooth(Mathf.Clamp01((now - sceneStarted) / FadeSeconds));
            stills[front].style.opacity = cross;
            stills[1 - front].style.opacity = 1f - cross;
            float drift = Mathf.Sin((now - sceneStarted) * .28f) * .018f;
            stills[front].style.scale = new Scale(new Vector3(1.03f + drift, 1.03f + drift, 1f));
            cinemaContinueArrow.style.translate = new Translate(0f, Mathf.Sin(now * 4.2f) * 3f, 0f);
        }

        internal void CloseExpedition(Action onClosed)
        {
            if (Exiting)
            {
                return;
            }
            exitStarted = Time.unscaledTime;
            exitOpacity = cinematic.resolvedStyle.opacity;
            dialoguePanel.style.display = DisplayStyle.None;
            onExit = onClosed;
        }

        // --- LIVING 3D OPEN FIELD BOOK ---

        internal void OpenJournal(DermagaIntroProgress progress, FishingConfiguration fishing,
            FishingVisualCatalog fishCatalog, DermagaCoralExpedition.CoralRecord[] corals, bool openCoralTab = false)
        {
            this.progress = progress;
            this.fishing = fishing;
            this.fishCatalog = fishCatalog;
            this.corals = corals;

            bool wantCoral = openCoralTab && progress.CoralJournalUnlocked;
            bookClosing = false;
            onJournalClosed = null;
            bookOpenAnim = 0f;

            journal.style.display = DisplayStyle.Flex;
            journal.style.opacity = 0f;
            DermagaUiAudio.PlayOpenBook();
            ShowCategory(wantCoral);
        }

        internal void CloseJournal(Action onFinished = null)
        {
            if (bookClosing)
            {
                return;
            }
            if (onFinished == null)
            {
                FinishJournalClose();
                return;
            }
            bookClosing = true;
            bookCloseAnim = 0f;
            onJournalClosed = onFinished;
            DermagaUiAudio.PlayClick();
        }

        private void FinishJournalClose()
        {
            bookClosing = false;
            preview.Clear();
            journal.style.display = DisplayStyle.None;
            Action cb = onJournalClosed;
            onJournalClosed = null;
            cb?.Invoke();
        }

        internal void UpdateJournal()
        {
            float dt = Time.unscaledDeltaTime;

            // Keyboard left/right card browsing!
            var kb = Keyboard.current;
            if (!bookClosing && bookOpenAnim >= 0.85f && kb != null && cardSelectActions.Count > 1)
            {
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                {
                    int next = (currentIndex + 1) % cardSelectActions.Count;
                    DermagaUiAudio.PlayClick();
                    cardSelectActions[next]();
                }
                else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                {
                    int prev = (currentIndex - 1 + cardSelectActions.Count) % cardSelectActions.Count;
                    DermagaUiAudio.PlayClick();
                    cardSelectActions[prev]();
                }
            }

            // Locked Koleksi Karang tab shake
            if (coralTabShakeTimer > 0f)
            {
                coralTabShakeTimer = Mathf.Max(0f, coralTabShakeTimer - dt);
                float t = coralTabShakeTimer / 0.32f;
                float sx = Mathf.Sin(t * Mathf.PI * 5f) * 5.5f * t;
                coralTab.style.translate = new Translate(sx, 0f, 0f);
            }
            else
            {
                coralTab.style.translate = StyleKeyword.Null;
            }

            // 1. Clean Snappy Book Closing Spring
            if (bookClosing)
            {
                bookCloseAnim = Mathf.Min(1f, bookCloseAnim + dt / BookCloseDuration);
                float e = Smooth(bookCloseAnim);
                journal.style.opacity = 1f - e;
                float s = Mathf.Lerp(1f, 0.93f, e);
                bookSpread.style.scale = new Scale(new Vector3(s, s, 1f));
                bookSpread.style.translate = new Translate(0f, e * 14f, 0f);
                journalDeskBg.MarkDirtyRepaint();
                UpdateLockedToast(dt);
                if (bookCloseAnim >= 1f)
                {
                    FinishJournalClose();
                }
                return;
            }

            // 2. Clean Snappy Book Opening Spring
            if (bookOpenAnim < 1f)
            {
                bookOpenAnim = Mathf.Min(1f, bookOpenAnim + dt / BookOpenDuration);
                float e = Smooth(bookOpenAnim);
                journal.style.opacity = e;
                float s = Mathf.Lerp(0.93f, 1f, e);
                bookSpread.style.scale = new Scale(new Vector3(s, s, 1f));
                bookSpread.style.translate = new Translate(0f, (1f - e) * 14f, 0f);
            }
            else
            {
                journal.style.opacity = 1f;
                bookSpread.style.scale = new Scale(Vector3.one);
                bookSpread.style.translate = new Translate(0f, 0f, 0f);
            }

            // 3. Staggered Living Card Pop-In Animation!
            float sinceCards = Time.unscaledTime - cardsAppearTime;
            for (int i = 0; i < activeCards.Count; i++)
            {
                var card = activeCards[i];
                float delay = 0.02f + i * 0.04f;
                float t = Mathf.Clamp01((sinceCards - delay) / 0.20f);
                if (t < 1f)
                {
                    float e = Smooth(t);
                    float bounce = Mathf.Sin(t * Mathf.PI) * 0.045f;
                    card.style.opacity = e;
                    float sc = Mathf.Lerp(0.86f, 1f, e) + bounce;
                    card.style.scale = new Scale(new Vector3(sc, sc, 1f));
                }
                else if (card.style.opacity != StyleKeyword.Null)
                {
                    card.style.opacity = StyleKeyword.Null;
                    card.style.scale = StyleKeyword.Null;
                }
            }

            // 4. Smooth Detail Panel Pop on Specimen Switch
            if (detailPopAnim < 1f && activeDetailWrap != null)
            {
                detailPopAnim = Mathf.Min(1f, detailPopAnim + dt * 5.8f);
                float e = Smooth(detailPopAnim);
                activeDetailWrap.style.opacity = e;
                float ds = Mathf.Lerp(0.96f, 1f, e);
                activeDetailWrap.style.scale = new Scale(new Vector3(ds, ds, 1f));
            }

            preview.Update();
            activeAquariumBg?.MarkDirtyRepaint();
            journalDeskBg.MarkDirtyRepaint();
            UpdateLockedToast(dt);
        }

        internal void DisposePreview()
        {
            preview.Dispose();
        }

        private void RequestCategorySwitch(bool wantCoral)
        {
            if (bookClosing)
            {
                return;
            }
            // STRICT LOCKING: Koleksi Karang CANNOT be opened while locked!
            if (wantCoral && !progress.CoralJournalUnlocked)
            {
                DermagaUiAudio.PlayLocked();
                coralTabShakeTimer = 0.32f;
                ShowToast("Catatan Karang terbuka setelah ekspedisi restorasi bersama Pak Darma.");
                return;
            }
            if (wantCoral == currentIsCoral)
            {
                return;
            }
            DermagaUiAudio.PlayClick();
            ShowCategory(wantCoral);
        }

        private void ShowCategory(bool coral)
        {
            currentIsCoral = coral;
            bool coralUnlocked = progress.CoralJournalUnlocked;

            fishTab.EnableInClassList("sagara-tab-active", !coral);
            coralTab.EnableInClassList("sagara-tab-active", coral && coralUnlocked);
            coralTab.EnableInClassList("sagara-tab-locked", !coralUnlocked);
            if (coralTabIcon != null)
            {
                Texture2D tex = coralUnlocked ? icons.coral : (icons.locked ? icons.locked : icons.coral);
                if (tex)
                {
                    coralTabIcon.style.backgroundImage = new StyleBackground(tex);
                }
            }

            grid.Clear();
            detail.Clear();
            activeCards.Clear();
            cardSelectActions.Clear();
            activeAquariumBg = null;
            activeDetailWrap = null;
            selectedCard = null;
            currentIndex = 0;
            cardsAppearTime = Time.unscaledTime;

            int discovered = 0;
            if (coral)
            {
                for (int i = 0; i < corals.Length; i++)
                {
                    var item = corals[i];
                    bool found = progress.HasDiscoveredCoral(item.id);
                    GetCoralMeta(item.id, out string sci, out string habitat, out string fallbackDesc);
                    string desc = !string.IsNullOrEmpty(item.description) ? item.description : fallbackDesc;
                    AddCard(i, item.displayName, sci, desc, habitat, "Spesies Karang",
                        item.portrait, item.accent, found, true, null, item.id);
                    discovered += found ? 1 : 0;
                }
                int total = Mathf.Max(1, corals.Length);
                journalCount.text = $"{discovered} / {corals.Length} karang tercatat";
                progressFill.style.width = Length.Percent(100f * discovered / total);
            }
            else
            {
                for (int i = 0; i < fishing.rarities.Length; i++)
                {
                    var rarity = fishing.rarities[i];
                    FishVisualEntry entry = fishCatalog.Find(rarity);
                    bool found = progress.HasDiscoveredFish(rarity.fishId);
                    GetFishMeta(rarity.fishId, entry.displayName, rarity.rarity,
                        out string indoName, out string sci, out string habitat, out string desc);
                    string rarityText = RarityName(rarity.rarity.ToString());
                    Color badgeCol = RarityAccent(rarity.rarity);
                    AddCard(i, indoName, sci, desc, habitat, rarityText,
                        entry.portrait, badgeCol, found, false, entry.prefab, null);
                    discovered += found ? 1 : 0;
                }
                int total = Mathf.Max(1, fishing.rarities.Length);
                journalCount.text = $"{discovered} / {fishing.rarities.Length} ikan tercatat";
                progressFill.style.width = Length.Percent(100f * discovered / total);
            }

            if (cardSelectActions.Count > 0)
            {
                cardSelectActions[0]();
            }
        }

        private void AddCard(int idx, string name, string scientificName, string description, string habitat, string badgeLabel,
            Texture2D portrait, Color accent, bool found, bool coral, GameObject fish, string coralId)
        {
            var card = new Button { text = "", focusable = false };
            card.AddToClassList("sagara-card");
            card.EnableInClassList("sagara-card-unseen", !found);
            AttachTactileFeedback(card);
            grid.Add(card);
            activeCards.Add(card);

            var cardTop = MakeElement(card, "sagara-card-top", ignorePicking: true);
            MakeLabel(cardTop, $"#{idx + 1:00}", "sagara-card-num", title: true);
            var pip = MakeElement(cardTop, "sagara-card-pip", ignorePicking: true);
            if (found)
            {
                pip.style.backgroundColor = accent;
            }
            else
            {
                pip.AddToClassList("sagara-card-pip-unseen");
            }

            // Species 3D portrait (vibrant when discovered, darkened silhouette when not yet discovered!)
            var thumb = MakeElement(card, "sagara-card-thumb", ignorePicking: true);
            if (portrait)
            {
                thumb.style.backgroundImage = new StyleBackground(portrait);
                thumb.style.unityBackgroundImageTintColor = found
                    ? Color.white
                    : new Color(0.14f, 0.21f, 0.24f, 0.90f);
            }
            else
            {
                Texture2D fb = coral ? icons.coral : icons.fish;
                if (fb)
                {
                    thumb.style.backgroundImage = new StyleBackground(fb);
                    thumb.style.unityBackgroundImageTintColor = found ? accent : new Color(0.16f, 0.22f, 0.25f, 0.85f);
                }
            }

            MakeLabel(card, found ? name : "???", found ? "sagara-card-name" : "sagara-card-name sagara-card-name-dim", title: true);

            void Select()
            {
                currentIndex = idx;
                selectedCard?.RemoveFromClassList("sagara-card-selected");
                selectedCard = card;
                card.AddToClassList("sagara-card-selected");

                string hint = coral
                    ? "Temukan fragmen karang ini saat memancing setelah restorasi."
                    : "Tangkap ikan ini di dermaga Sagara untuk mencatatnya.";

                RenderDetail(found ? name : "Belum Tercatat",
                    found ? description : hint,
                    habitat, found ? badgeLabel : "Terkunci",
                    accent, found, coral, fish, coralId);
            }

            cardSelectActions.Add(Select);
            card.clicked += () => { DermagaUiAudio.PlayClick(); Select(); };
        }

        private void RenderDetail(string name, string description, string habitat, string badgeLabel,
            Color accent, bool found, bool coral, GameObject fish, string coralId)
        {
            detail.Clear();
            preview.Clear();
            detailPopAnim = 0f;

            activeDetailWrap = MakeElement(detail, "sagara-detail-wrap");

            // 1. Spacious Living 3D Underwater Diorama Stage (NO tight corner brackets — clean open ocean room!)
            var plate = MakeElement(activeDetailWrap, "sagara-detail-viewport");
            var aquariumBg = MakeElement(plate, "sagara-aquarium-bg", ignorePicking: true);
            aquariumBg.generateVisualContent += DrawAquariumBackdrop;
            activeAquariumBg = aquariumBg;

            RenderTexture rendered = preview.Show(fish, coralId, accent, discovered: found);
            if (rendered)
            {
                var image = new Image { image = rendered, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("sagara-live-specimen");
                plate.Add(image);
                image.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0)
                    {
                        image.CapturePointer(evt.pointerId);
                    }
                });
                image.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (image.HasPointerCapture(evt.pointerId))
                    {
                        preview.Rotate(evt.deltaPosition.x * .85f);
                    }
                });
                image.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (image.HasPointerCapture(evt.pointerId))
                    {
                        image.ReleasePointer(evt.pointerId);
                    }
                });

                var dragPill = MakeElement(plate, "sagara-drag-pill", ignorePicking: true);
                if (icons.rotate)
                {
                    var rotIcon = MakeElement(dragPill, "sagara-drag-icon", ignorePicking: true);
                    rotIcon.style.backgroundImage = new StyleBackground(icons.rotate);
                }
                MakeLabel(dragPill, "Seret 360°", "sagara-drag-text", true);
            }

            // 2. Concise, Clean Species Info (NO bloated text walls!)
            var tags = MakeElement(activeDetailWrap, "sagara-detail-tags");
            var badge = MakeLabel(tags, badgeLabel, "sagara-tag-pill", true);
            if (found)
            {
                badge.style.backgroundColor = new Color(accent.r, accent.g, accent.b, .16f);
                badge.style.borderTopColor = accent;
                badge.style.borderBottomColor = accent;
                badge.style.borderLeftColor = accent;
                badge.style.borderRightColor = accent;
                badge.style.color = new Color(accent.r * .62f, accent.g * .62f, accent.b * .62f, 1f);
            }
            MakeLabel(tags, habitat, "sagara-tag-pill sagara-tag-habitat", true);

            MakeLabel(activeDetailWrap, name, "sagara-detail-name", true);
            MakeLabel(activeDetailWrap, description, "sagara-record-copy");
        }

        // --- LIVING AQUARIUM PAINTER ---

        private static void DrawAquariumBackdrop(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 20f || r.height < 20f)
            {
                return;
            }
            // Sunlit tropical ocean depth gradient
            var mesh = ctx.Allocate(4, 6);
            var topCol = new Color32(138, 212, 218, 255);
            var botCol = new Color32(52, 124, 140, 255);
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, 0f, Vertex.nearZ), tint = topCol });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, 0f, Vertex.nearZ), tint = topCol });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, r.height, Vertex.nearZ), tint = botCol });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, r.height, Vertex.nearZ), tint = botCol });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);

            var p = ctx.painter2D;
            float t = Time.unscaledTime;

            // Gentle sunlit ocean caustics / diagonal rays drifting smoothly
            for (int i = 0; i < 4; i++)
            {
                float drift = Mathf.Sin(t * (0.45f + i * 0.12f) + i * 1.9f) * 14f;
                float x0 = r.width * (0.14f + i * 0.24f) + drift;
                float wTop = 28f + (i % 2) * 16f;
                float wBot = wTop * 1.6f;
                p.fillColor = new Color(1f, 0.99f, 0.92f, 0.11f + 0.03f * Mathf.Sin(t * 1.1f + i));
                p.BeginPath();
                p.MoveTo(new Vector2(x0, 0f));
                p.LineTo(new Vector2(x0 + wTop, 0f));
                p.LineTo(new Vector2(x0 + wBot + 36f, r.height));
                p.LineTo(new Vector2(x0 + 36f, r.height));
                p.ClosePath();
                p.Fill();
            }

            // Soft swaying seabed kelp silhouettes along bottom edge
            for (int k = 0; k < 5; k++)
            {
                float bx = r.width * (0.10f + k * 0.20f);
                float kh = 32f + (k % 3) * 16f;
                float sway = Mathf.Sin(t * (1.3f + k * 0.18f) + k * 1.7f) * 8f;
                p.strokeColor = new Color(0.11f, 0.36f, 0.42f, 0.30f);
                p.lineWidth = 4.2f;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.MoveTo(new Vector2(bx, r.height + 4f));
                p.QuadraticCurveTo(new Vector2(bx + sway * 0.5f, r.height - kh * 0.5f), new Vector2(bx + sway, r.height - kh));
                p.Stroke();
            }

            // Gently rising translucent bubbles
            for (int b = 0; b < 7; b++)
            {
                float speed = 0.10f + (b % 3) * 0.045f;
                float prog = Mathf.Repeat(t * speed + b * 0.27f, 1f);
                float bx = r.width * (0.12f + (b * 0.13f) % 0.78f) + Mathf.Sin(t * 1.8f + b * 2.1f) * 7f;
                float by = Mathf.Lerp(r.height + 8f, -10f, prog);
                float rad = 2.6f + (b % 3) * 1.8f;
                float alpha = Mathf.Sin(prog * Mathf.PI) * 0.48f;
                p.strokeColor = new Color(0.95f, 1f, 1f, alpha);
                p.lineWidth = 1.2f;
                p.BeginPath();
                p.Arc(new Vector2(bx, by), rad, 0f, 360f);
                p.Stroke();
            }
        }

        private static void DrawSpecimenPlateFrame(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 40f || r.height < 40f)
            {
                return;
            }
            var p = ctx.painter2D;
            p.strokeColor = new Color(1f, 0.99f, 0.94f, 0.42f);
            p.lineWidth = 1.5f;
            float m = 12f;
            float len = 14f;
            p.BeginPath();
            p.MoveTo(new Vector2(m, m + len)); p.LineTo(new Vector2(m, m)); p.LineTo(new Vector2(m + len, m));
            p.MoveTo(new Vector2(r.width - m - len, m)); p.LineTo(new Vector2(r.width - m, m)); p.LineTo(new Vector2(r.width - m, m + len));
            p.MoveTo(new Vector2(m, r.height - m - len)); p.LineTo(new Vector2(m, r.height - m)); p.LineTo(new Vector2(m + len, r.height - m));
            p.MoveTo(new Vector2(r.width - m - len, r.height - m)); p.LineTo(new Vector2(r.width - m, r.height - m)); p.LineTo(new Vector2(r.width - m, r.height - m - len));
            p.Stroke();
        }

        private static void DrawDeskAmbience(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 40f || r.height < 40f)
            {
                return;
            }
            var mesh = ctx.Allocate(4, 6);
            var topCol = new Color32(18, 42, 46, 168);
            var botCol = new Color32(30, 38, 34, 198);
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, 0f, Vertex.nearZ), tint = topCol });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, 0f, Vertex.nearZ), tint = topCol });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, r.height, Vertex.nearZ), tint = botCol });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, r.height, Vertex.nearZ), tint = botCol });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);

            var p = ctx.painter2D;
            float t = Time.unscaledTime;
            for (int i = 0; i < 12; i++)
            {
                float seed = i * 2.13f + 0.5f;
                float px = r.width * (0.04f + 0.92f * Mathf.Repeat(seed * 0.37f + t * 0.008f, 1f));
                float py = r.height * (0.06f + 0.88f * Mathf.Repeat(seed * 0.61f - t * 0.011f, 1f));
                float alpha = 0.09f + 0.09f * Mathf.Sin(t * 1.4f + seed * 3f);
                p.fillColor = new Color(1f, 0.95f, 0.78f, alpha);
                p.BeginPath();
                p.Arc(new Vector2(px, py), 2.4f + (i % 3) * 1.1f, 0f, 360f);
                p.Fill();
            }
        }

        private static void DrawLockedToastBg(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 44f || r.height < 16f)
            {
                return;
            }
            float fade = Mathf.Min(64f, r.width * 0.16f);
            var mesh = ctx.Allocate(8, 18);
            var clear = new Color32(18, 40, 48, 0);
            var solid = new Color32(18, 40, 48, 224);
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
            p.strokeColor = new Color(.98f, .84f, .48f, .76f);
            p.lineWidth = 1.35f;
            p.BeginPath();
            p.MoveTo(new Vector2(fade * 0.72f, 1f));
            p.LineTo(new Vector2(r.width - fade * 0.72f, 1f));
            p.MoveTo(new Vector2(fade * 0.72f, r.height - 1f));
            p.LineTo(new Vector2(r.width - fade * 0.72f, r.height - 1f));
            p.Stroke();
        }

        private static void DrawCinemaScrim(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            if (r.width < 10f || r.height < 10f)
            {
                return;
            }
            var mesh = ctx.Allocate(6, 12);
            var top = new Color32(10, 22, 26, 0);
            var mid = new Color32(10, 22, 26, 184);
            var bot = new Color32(8, 18, 22, 240);
            float my = r.height * 0.48f;
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, 0f, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, 0f, Vertex.nearZ), tint = top });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, my, Vertex.nearZ), tint = mid });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, my, Vertex.nearZ), tint = mid });
            mesh.SetNextVertex(new Vertex { position = new Vector3(0f, r.height, Vertex.nearZ), tint = bot });
            mesh.SetNextVertex(new Vertex { position = new Vector3(r.width, r.height, Vertex.nearZ), tint = bot });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(3);
            mesh.SetNextIndex(0); mesh.SetNextIndex(3); mesh.SetNextIndex(2);
            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(5);
            mesh.SetNextIndex(2); mesh.SetNextIndex(5); mesh.SetNextIndex(4);
        }

        private static void DrawContinueChevron(MeshGenerationContext ctx)
        {
            Rect r = ctx.visualElement.contentRect;
            float cx = r.width * 0.5f;
            float cy = r.height * 0.5f;
            var p = ctx.painter2D;
            p.strokeColor = new Color(1f, .86f, .46f, .96f);
            p.lineWidth = 2.4f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - 7f, cy - 3f));
            p.LineTo(new Vector2(cx, cy + 4f));
            p.LineTo(new Vector2(cx + 7f, cy - 3f));
            p.Stroke();
        }

        private static void AttachTactileFeedback(Button btn)
        {
            btn.RegisterCallback<PointerEnterEvent>(_ => DermagaUiAudio.PlayHover());
            btn.RegisterCallback<PointerDownEvent>(_ => btn.AddToClassList("sagara-ui-pressed"));
            btn.RegisterCallback<PointerUpEvent>(_ => btn.RemoveFromClassList("sagara-ui-pressed"));
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.RemoveFromClassList("sagara-ui-pressed"));
        }

        private static void GetFishMeta(string fishId, string fallbackName, FishRarity rarity,
            out string indoName, out string sci, out string habitat, out string desc)
        {
            switch (fishId)
            {
                case "prototype_fish_common":
                    indoName = "Ikan Badut";
                    sci = "Amphiprion ocellaris";
                    habitat = "Dermaga Pesisir";
                    desc = "Ikan kecil berbelang cerah yang hidup bersimbiosis di antara anemon dan perairan jernih dekat dermaga Sagara.";
                    return;
                case "prototype_fish_uncommon":
                    indoName = "Kakap Merah";
                    sci = "Lutjanus malabaricus";
                    habitat = "Tepi Dermaga";
                    desc = "Berenang berkelompok dekat tiang kayu dermaga saat arus tenang. Menandakan sehatnya rantai makanan pesisir.";
                    return;
                case "prototype_fish_rare":
                    indoName = "Kerapu Karang";
                    sci = "Cephalopholis miniata";
                    habitat = "Celah Terumbu";
                    desc = "Penjaga celah karang yang tenang dan waspada, muncul kembali di perairan jernih ketika terumbu mulai pulih.";
                    return;
                case "prototype_fish_epic":
                    indoName = "Gramma Ungu";
                    sci = "Gramma loreto";
                    habitat = "Lereng Terumbu";
                    desc = "Dikenal lewat gradasi warna ungu-emasnya yang mencolok. Sering berenang menyusuri dinding karang teduh.";
                    return;
                case "prototype_fish_legendary":
                    indoName = "Ikan Lepu Ayam";
                    sci = "Pterois volitans";
                    habitat = "Terumbu Luar";
                    desc = "Meluncur anggun dengan sirip kipas panjang berpola belang. Kemunculannya menandakan ekosistem karang yang matang.";
                    return;
                default:
                    indoName = fallbackName;
                    sci = "Species marina";
                    habitat = "Dermaga Pesisir";
                    desc = $"Ikan pesisir kategori {RarityName(rarity.ToString()).ToLowerInvariant()} yang hidup di perairan jernih sekitar Sagara.";
                    return;
            }
        }

        private static void GetCoralMeta(string coralId, out string sci, out string habitat, out string desc)
        {
            switch (coralId)
            {
                case "branching_coral_observation":
                    sci = "Acropora cervicornis";
                    habitat = "Terumbu Sagara";
                    desc = "Terumbu bercabang berwarna koral kemerahan yang menjadi rumah berlindung bagi ikan-ikan karang kecil di perairan dangkal.";
                    return;
                case "plate_coral_observation":
                    sci = "Acropora hyacinthus";
                    habitat = "Terumbu Sagara";
                    desc = "Membentuk kanopi melebar untuk menangkap sinar matahari sekaligus menciptakan ruang teduh di bawahnya.";
                    return;
                case "massive_coral_observation":
                    sci = "Diploria labyrinthiformis";
                    habitat = "Laguna Sagara";
                    desc = "Struktur kubah padatnya tumbuh perlahan namun sangat tangguh meredam energi gelombang pesisir.";
                    return;
                case "seafan_coral_observation":
                    sci = "Gorgonia ventalina";
                    habitat = "Dinding Karang";
                    desc = "Jaring polip lenturnya bergoyang mengikuti arus laut lembut sembari menyaring plankton halus.";
                    return;
                case "anemone_coral_observation":
                    sci = "Heteractis magnifica";
                    habitat = "Terumbu Dangkal";
                    desc = "Tentakel lembutnya menjadi tempat tinggal aman bagi ikan badut dalam hubungan simbiosis alami.";
                    return;
                default:
                    sci = "Anthozoa sp.";
                    habitat = "Terumbu Sagara";
                    desc = "Bagian penting dari pemulihan ekosistem terumbu pesisir pulau Sagara.";
                    return;
            }
        }

        private void ResizeFrame(GeometryChangedEvent _)
        {
            float width = cinematic.resolvedStyle.width;
            float height = cinematic.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height) || width < 1f || height < 1f)
            {
                return;
            }
            const float aspect = 16f / 9f;
            bool wider = width / height > aspect;
            frame.style.width = wider ? height * aspect : width;
            frame.style.height = wider ? height : width / aspect;
        }

        private void UpdateNarration()
        {
            narration.text = Narration[line];
        }

        private VisualElement MakeElement(VisualElement parent, string classes, bool ignorePicking = false)
        {
            var element = new VisualElement();
            if (ignorePicking)
            {
                element.pickingMode = PickingMode.Ignore;
            }
            foreach (string name in classes.Split(' '))
            {
                if (!string.IsNullOrEmpty(name))
                {
                    element.AddToClassList(name);
                }
            }
            parent.Add(element);
            return element;
        }

        private Label MakeLabel(VisualElement parent, string text, string classes, bool title = false)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (string name in classes.Split(' '))
            {
                if (!string.IsNullOrEmpty(name))
                {
                    label.AddToClassList(name);
                }
            }
            if (title)
            {
                ApplyDisplayFont(label);
            }
            else
            {
                ApplyBodyFont(label);
            }
            parent.Add(label);
            return label;
        }

        private void ApplyDisplayFont(VisualElement el)
        {
            if (displayFontAsset)
            {
                el.style.unityFontDefinition = FontDefinition.FromSDFFont(displayFontAsset);
            }
            else if (displayFont)
            {
                el.style.unityFontDefinition = FontDefinition.FromFont(displayFont);
            }
        }

        private void ApplyBodyFont(VisualElement el)
        {
            if (bodyFontAsset)
            {
                el.style.unityFontDefinition = FontDefinition.FromSDFFont(bodyFontAsset);
            }
            else if (bodyFont)
            {
                el.style.unityFontDefinition = FontDefinition.FromFont(bodyFont);
            }
        }

        private static string RarityName(string value)
        {
            switch (value)
            {
                case "Common": return "Common";
                case "Uncommon": return "Uncommon";
                case "Rare": return "Rare";
                case "Epic": return "Epic";
                case "Legendary": return "Legendary";
                default: return value;
            }
        }

        private static Color RarityAccent(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Common: return new Color(.22f, .54f, .52f);
                case FishRarity.Uncommon: return new Color(.20f, .56f, .40f);
                case FishRarity.Rare: return new Color(.24f, .44f, .70f);
                case FishRarity.Epic: return new Color(.58f, .34f, .64f);
                default: return new Color(.80f, .54f, .18f);
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
