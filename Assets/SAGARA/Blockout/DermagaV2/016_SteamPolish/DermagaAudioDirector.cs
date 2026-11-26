using System.Collections;
using Sagara.Dermaga.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sagara.DermagaV2
{
    /// <summary>
    /// Cozy Coastal Audio Director for DermagaV2 (016_SteamPolish).
    /// Drives seamless island BGM (with gentle ducking), spatial-aware ocean ambience,
    /// surface-aware footsteps (wood/sand/grass), and automatic SFX for fishing, quests, dialogue, and boat expedition.
    /// </summary>
    [DefaultExecutionOrder(280)]
    [DisallowMultipleComponent]
    public sealed class DermagaAudioDirector : MonoBehaviour
    {
        public static DermagaAudioDirector Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private CharacterController player;
        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private FishingSession fishing;
        [SerializeField] private PlayerNpcInteraction dialogue;
        [SerializeField] private DermagaCoralExpedition expedition;

        [Header("Music & Ambience")]
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private AudioClip oceanAmbienceClip;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.50f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0f;

        [Header("Footsteps")]
        [SerializeField] private AudioClip footstepWood;
        [SerializeField] private AudioClip footstepSand;
        [SerializeField] private AudioClip footstepGrass;

        [Header("Gameplay & World SFX")]
        [SerializeField] private AudioClip pickupTrash;
        [SerializeField] private AudioClip pickupWood;
        [SerializeField] private AudioClip hammerRepair;
        [SerializeField] private AudioClip mangrovePlant;
        [SerializeField] private AudioClip questComplete;
        [SerializeField] private AudioClip dialogVoice;
        [SerializeField] private AudioClip fishingCast;
        [SerializeField] private AudioClip fishingSplash;
        [SerializeField] private AudioClip boatBoard;
        [SerializeField] private AudioClip boatEngineLoop;
        [SerializeField] private AudioClip boatHorn;

        private AudioSource bgmSource;
        private AudioSource ambienceSource;
        private AudioSource sfxSource;
        private AudioSource boatEngineSource;

        private float stepTimer;
        private int lastTrash;
        private int lastWood;
        private int lastDock;
        private int lastMangrove;
        private int lastCoralSupply;
        private DermagaQuestPhase lastPhase;
        private FishingState lastFishState = FishingState.Idle;
        private bool wasTalking;
        private bool stateInitialized;

        private void Awake()
        {
            Instance = this;
            if (!player) player = FindFirstObjectByType<CharacterController>();
            if (!quest) quest = FindFirstObjectByType<DermagaIntroProgress>();
            if (!fishing) fishing = FindFirstObjectByType<FishingSession>();
            if (!dialogue) dialogue = FindFirstObjectByType<PlayerNpcInteraction>();
            if (!expedition) expedition = FindFirstObjectByType<DermagaCoralExpedition>();

            bgmSource = CreateSource("BGM", loop: true, volume: 0f);
            ambienceSource = CreateSource("OceanAmbience", loop: true, volume: 0f);
            sfxSource = CreateSource("SFX", loop: false, volume: 1f);
            boatEngineSource = CreateSource("BoatEngine", loop: true, volume: 0f);
        }

        private void Start()
        {
            if (bgmClip)
            {
                bgmSource.clip = bgmClip;
                bgmSource.Play();
            }
            if (oceanAmbienceClip && ambienceVolume > 0.001f)
            {
                ambienceSource.clip = oceanAmbienceClip;
                ambienceSource.Play();
            }
            if (boatEngineLoop)
            {
                boatEngineSource.clip = boatEngineLoop;
            }
            SyncObservedState();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioSource CreateSource(string label, bool loop, float volume)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            src.volume = volume;
            return src;
        }

        private void SyncObservedState()
        {
            if (quest)
            {
                lastTrash = quest.CollectedCount;
                lastWood = quest.BranchesCollected;
                lastDock = quest.DockRepairsCompleted;
                lastMangrove = quest.MangrovesPlanted;
                lastCoralSupply = quest.CoralSuppliesCollected;
                lastPhase = quest.Phase;
            }
            if (fishing) lastFishState = fishing.State;
            wasTalking = dialogue && dialogue.IsTalking;
            stateInitialized = true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateMix(dt);
            UpdateFootsteps(Time.deltaTime);
            UpdateGameplayObservers();
        }

        private void UpdateMix(float dt)
        {
            bool talking = dialogue && dialogue.IsTalking;
            bool minigame = fishing && fishing.State == FishingState.Minigame;
            bool overlayOpen = expedition && expedition.OverlayOpen;
            float duck = (talking || minigame || overlayOpen) ? 0.68f : 1f;
            float targetBgm = bgmVolume * duck;
            if (bgmSource && bgmSource.isPlaying)
            {
                bgmSource.volume = Mathf.MoveTowards(bgmSource.volume, targetBgm, dt * 0.35f);
            }

            // Ocean surf naturally swells slightly closer to the beach/dock/sea (z < 6) and softens up in the forest (z > 18)
            float coastalFactor = 1f;
            if (player)
            {
                float z = player.transform.position.z;
                coastalFactor = Mathf.Lerp(1.22f, 0.68f, Mathf.InverseLerp(-4f, 24f, z));
            }
            float targetAmb = ambienceVolume * coastalFactor * (overlayOpen ? 0.72f : 1f);
            if (ambienceSource && ambienceSource.isPlaying)
            {
                ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, targetAmb, dt * 0.35f);
            }
        }

        private void UpdateFootsteps(float dt)
        {
            if (!player || !player.enabled || !player.isGrounded)
            {
                stepTimer = 0.08f;
                return;
            }
            Vector3 horiz = new Vector3(player.velocity.x, 0f, player.velocity.z);
            float speed = horiz.magnitude;
            if (speed < 0.65f)
            {
                stepTimer = 0.06f;
                return;
            }

            stepTimer -= dt;
            if (stepTimer > 0f) return;

            float interval = speed > 4.2f ? 0.27f : 0.37f;
            stepTimer = interval;

            AudioClip clip = PickFootstepClip(player.transform.position);
            if (clip)
            {
                PlayOneShot(clip, speed > 4.2f ? 0.26f : 0.20f, Random.Range(0.92f, 1.08f));
            }
        }

        private AudioClip PickFootstepClip(Vector3 footPos)
        {
            if (Physics.Raycast(footPos + Vector3.up * 0.45f, Vector3.down, out RaycastHit hit, 1.6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                string n = hit.collider.name.ToLowerInvariant();
                string p = hit.collider.transform.parent ? hit.collider.transform.parent.name.ToLowerInvariant() : "";
                if (n.Contains("lantai") || n.Contains("dock") || n.Contains("board") || n.Contains("plank")
                    || n.Contains("ramp") || n.Contains("stair") || n.Contains("tangga") || n.Contains("wood")
                    || p.Contains("dock") || p.Contains("boardwalk") || p.Contains("repaired"))
                {
                    return footstepWood ? footstepWood : footstepGrass;
                }
                if (n.Contains("beach") || n.Contains("sand") || n.Contains("mud") || n.Contains("silt") || n.Contains("shore"))
                {
                    return footstepSand ? footstepSand : footstepGrass;
                }
            }
            return footstepGrass ? footstepGrass : footstepSand;
        }

        private void UpdateGameplayObservers()
        {
            if (!stateInitialized)
            {
                SyncObservedState();
                return;
            }

            if (quest)
            {
                if (quest.CollectedCount > lastTrash)
                {
                    lastTrash = quest.CollectedCount;
                    PlayOneShot(pickupTrash, 0.42f, Random.Range(0.96f, 1.06f));
                }
                if (quest.BranchesCollected > lastWood)
                {
                    lastWood = quest.BranchesCollected;
                    PlayOneShot(pickupWood, 0.42f, Random.Range(0.96f, 1.06f));
                }
                if (quest.DockRepairsCompleted > lastDock)
                {
                    lastDock = quest.DockRepairsCompleted;
                    PlayOneShot(hammerRepair, 0.46f, Random.Range(0.96f, 1.04f));
                }
                if (quest.MangrovesPlanted > lastMangrove)
                {
                    lastMangrove = quest.MangrovesPlanted;
                    PlayOneShot(mangrovePlant, 0.46f, Random.Range(0.97f, 1.05f));
                }
                if (quest.CoralSuppliesCollected > lastCoralSupply)
                {
                    lastCoralSupply = quest.CoralSuppliesCollected;
                    PlayOneShot(pickupWood, 0.42f, Random.Range(1.02f, 1.10f));
                }
                if (quest.Phase != lastPhase)
                {
                    var prev = lastPhase;
                    lastPhase = quest.Phase;
                    if (prev != DermagaQuestPhase.MeetPakDarma || quest.Phase != DermagaQuestPhase.MeetPakDarma)
                    {
                        PlayOneShot(questComplete, 0.42f, 1.0f);
                    }
                }
            }

            if (fishing && fishing.State != lastFishState)
            {
                FishingState next = fishing.State;
                if (next == FishingState.Casting)
                {
                    PlayOneShot(fishingCast, 0.36f, Random.Range(0.95f, 1.05f));
                    StartCoroutine(DelayedSplash(0.38f, 0.36f));
                }
                else if (next == FishingState.CatchSuccess)
                {
                    PlayOneShot(fishingSplash, 0.42f, Random.Range(1.02f, 1.10f));
                }
                lastFishState = next;
            }

            if (dialogue)
            {
                bool talking = dialogue.IsTalking;
                if (talking && !wasTalking)
                {
                    PlayOneShot(dialogVoice, 0.24f, Random.Range(0.94f, 1.08f));
                }
                else if (talking)
                {
                    var kb = Keyboard.current;
                    var ms = Mouse.current;
                    if ((kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                        || (ms != null && ms.leftButton.wasPressedThisFrame))
                    {
                        PlayOneShot(dialogVoice, 0.20f, Random.Range(0.92f, 1.10f));
                    }
                }
                wasTalking = talking;
            }
        }

        private IEnumerator DelayedSplash(float delay, float vol)
        {
            yield return new WaitForSeconds(delay);
            PlayOneShot(fishingSplash, vol, Random.Range(0.94f, 1.06f));
        }

        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (!sfxSource || !clip) return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volume);
        }

        // Public hooks for Restoration Boat Boarding & Sailing Sequence
        public static void PlayBoatBoard()
        {
            if (Instance) Instance.PlayOneShot(Instance.boatBoard, 0.48f, Random.Range(0.96f, 1.04f));
        }

        public static void PlayBoatHorn()
        {
            if (Instance) Instance.PlayOneShot(Instance.boatHorn, 0.36f, 1.0f);
        }

        public static void PlayWaterSplash()
        {
            if (Instance) Instance.PlayOneShot(Instance.fishingSplash, 0.44f, Random.Range(0.92f, 1.04f));
        }

        public static void SetBoatEngine(bool active)
        {
            if (!Instance || !Instance.boatEngineSource || !Instance.boatEngineLoop) return;
            if (active)
            {
                if (!Instance.boatEngineSource.isPlaying) Instance.boatEngineSource.Play();
                Instance.boatEngineSource.volume = 0.34f;
            }
            else
            {
                Instance.boatEngineSource.Stop();
                Instance.boatEngineSource.volume = 0f;
            }
        }
    }
}
