using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sagara.Dermaga.Fishing
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class FishingSession : MonoBehaviour
    {
        // Configuration and scene bindings.
        public FishingConfiguration configuration;
        public PlayerMovement movement;
        public CharacterController player;
        public Animator animator;
        public RuntimeAnimatorController fishingController;
        public FishingSpot[] spots;
        public Behaviour[] conflictingInteractions = new Behaviour[0];
        public FishingPresentation presentation;

        // Published session state.
        public FishingState State { get; private set; } = FishingState.Idle;

        public RaritySettings Selected { get; private set; }

        public FishingRound Round { get; private set; }

        public FishingSpot ActiveSpot { get; private set; }

        public FishingSpot NearbySpot { get; private set; }

        public FishingResult LastResult { get; private set; }

        public IReadOnlyList<FishingResult> Attempts => attempts;

        public IReadOnlyList<FishingResult> Catches => catches;

        // Notifications and presentation coordination.
        public event Action<FishingResult> ResultRecorded;
        public event Action<FishingState> StateChanged;
        // Presentation-only acknowledgement gate; scoring and result dispatch remain in Resolve.
        public Func<bool> ResultPresentationPending;

        public bool Busy => State != FishingState.Idle;

        public float PhaseProgress => Mathf.Clamp01(elapsed / Mathf.Max(.01f, phaseDuration));

        public float ArrowRemaining => Mathf.Max(0, configuration.arrowSeconds - arrowElapsed);

        // Ownership and result history.
        static readonly Dictionary<CharacterController, FishingSession> owners =
            new Dictionary<CharacterController, FishingSession>();
        readonly List<FishingResult> attempts = new List<FishingResult>();
        readonly List<FishingResult> catches = new List<FishingResult>();

        // Random selection and restoration state.
        System.Random random;
        RuntimeAnimatorController originalController;
        bool oldMovement;
        bool locked;
        bool[] oldInteractions;

        // Phase timing and input guards.
        float elapsed;
        float phaseDuration;
        float arrowElapsed;
        float nextInput;
        int lastInputFrame = -1;
        bool arrowsReleased = true;
        bool recorded;

        void OnEnable()
        {
            random = new System.Random(configuration != null && configuration.deterministicSeed
                ? configuration.seed
                : Environment.TickCount);
        }

        public void Reseed(int seed)
        {
            if (Busy)
            {
                throw new InvalidOperationException("Cannot reseed active fishing");
            }

            random = new System.Random(seed);
        }

        void Update()
        {
            if (configuration == null || player == null || movement == null)
            {
                return;
            }

            var k = Keyboard.current;
            var m = Mouse.current;

            if (!Busy)
            {
                NearbySpot = FindNearby();

                if (NearbySpot != null
                    && ((k != null
                    && k.eKey.wasPressedThisFrame)
                    || (m != null
                    && m.leftButton.wasPressedThisFrame
                    && !FishingHud.ContainsPointer(m.position.ReadValue()))))
                {
                    TryBegin(NearbySpot);
                }

                return;
            }

            if (k != null && k.escapeKey.wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            elapsed += Time.deltaTime;

            if (presentation != null)
            {
                presentation.Animate(this);
            }

            switch (State)
            {
                case FishingState.Casting:
                    if (elapsed >= phaseDuration)
                    {
                        presentation?.Splash(ActiveSpot.CastPosition, Color.white, 8);
                        Enter(
                            FishingState.WaitingForBite,
                            phaseDuration = (float)(
                                configuration.biteWaitSeconds.x
                                + random.NextDouble() * Mathf.Max(
                                    0,
                                    configuration.biteWaitSeconds.y
                                    - configuration.biteWaitSeconds.x
                                )
                            )
                        );
                    }

                    break;
                case FishingState.WaitingForBite:
                    if (elapsed >= phaseDuration)
                    {
                        presentation?.Bite(Selected);
                        Enter(FishingState.BiteDetected, configuration.biteNoticeSeconds);
                    }

                    break;
                case FishingState.BiteDetected:
                    if (elapsed >= phaseDuration)
                    {
                        arrowElapsed = 0;
                        arrowsReleased = false;
                        Enter(FishingState.Minigame, 0);
                    }

                    break;
                case FishingState.Minigame:
                    arrowElapsed += Time.deltaTime;
                    int count = 0;
                    FishingDirection direction = FishingDirection.Up;
                    bool held = false;
                    if (k != null)
                    {
                        held = k.upArrowKey.isPressed
                            || k.downArrowKey.isPressed
                            || k.leftArrowKey.isPressed
                            || k.rightArrowKey.isPressed;

                        if (k.upArrowKey.wasPressedThisFrame)
                        {
                            count++;
                            direction = FishingDirection.Up;
                        }

                        if (k.downArrowKey.wasPressedThisFrame)
                        {
                            count++;
                            direction = FishingDirection.Down;
                        }

                        if (k.leftArrowKey.wasPressedThisFrame)
                        {
                            count++;
                            direction = FishingDirection.Left;
                        }

                        if (k.rightArrowKey.wasPressedThisFrame)
                        {
                            count++;
                            direction = FishingDirection.Right;
                        }
                    }

                    if (!held)
                    {
                        arrowsReleased = true;
                    }

                    if (count > 0 && arrowsReleased)
                    {
                        arrowsReleased = false;
                        Submit(count == 1 ? (FishingDirection? )direction : null);
                    }

                    if (State == FishingState.Minigame
                        && arrowElapsed >= configuration.arrowSeconds)
                    {
                        Consume(null);
                    }

                    break;
                case FishingState.CatchSuccess:
                    if (elapsed >= phaseDuration
                        && (ResultPresentationPending == null
                        || !ResultPresentationPending()))
                    {
                        Finish();
                    }

                    break;
                case FishingState.CatchFail:
                    if (elapsed >= phaseDuration)
                    {
                        Finish();
                    }

                    break;
            }
        }

        FishingSpot FindNearby()
        {
            FishingSpot best = null;
            float distance = float.MaxValue;

            if (spots == null)
            {
                return null;
            }

            foreach (var s in spots)
            {
                if (s == null || !s.isActiveAndEnabled || !s.Contains(player.transform.position))
                {
                    continue;
                }

                float d = (s.standingPoint.position - player.transform.position).sqrMagnitude;

                if (d < distance)
                {
                    best = s;
                    distance = d;
                }
            }

            return best;
        }

        public bool TryBegin(FishingSpot spot)
        {
            if (!isActiveAndEnabled
                || Busy
                || configuration == null
                || !configuration.IsValid
                || player == null
                || movement == null
                || spot == null
                || !spot.isActiveAndEnabled
                || !spot.Contains(player.transform.position))
            {
                return false;
            }

            if (owners.TryGetValue(player, out var owner) && owner != null && owner != this)
            {
                return false;
            }

            owners[player] = this;
            oldMovement = movement.enabled;
            originalController = animator == null ? null : animator.runtimeAnimatorController;
            oldInteractions = new bool[conflictingInteractions.Length];

            for (int i = 0; i < conflictingInteractions.Length; i++)
            {
                var component = conflictingInteractions[i];

                if (component != null)
                {
                    oldInteractions[i] = component.enabled;
                    component.enabled = false;
                }
            }

            locked = true;
            movement.enabled = false;

            if (animator != null)
            {
                animator.SetBool("isWalking", false);

                if (fishingController != null)
                {
                    animator.runtimeAnimatorController = fishingController;
                }
            }

            ActiveSpot = spot;
            var definition =
                configuration.rarities[FishingRules.Roll(
                    random,
                    configuration.rarities
                )];
            Selected = new RaritySettings(
                definition.rarity,
                definition.weight,
                definition.minimumScore,
                definition.aura
            )
            {
                fishId = definition.fishId,
                intensity = definition.intensity
            };
            Round = new FishingRound(FishingRules.Sequence(random));
            recorded = false;
            lastInputFrame = -1;
            nextInput = 0;

            Vector3 facing = spot.CastPosition - player.transform.position;
            facing.y = 0;

            if (facing.sqrMagnitude > .01f)
            {
                player.transform.rotation = Quaternion.LookRotation(facing);
            }

            presentation?.Begin(this);
            Enter(FishingState.Casting, configuration.castSeconds);
            return true;
        }

        public bool Submit(FishingDirection? direction)
        {
            if (State != FishingState.Minigame
                || lastInputFrame == Time.frameCount
                || Time.time < nextInput)
            {
                return false;
            }

            lastInputFrame = Time.frameCount;
            nextInput = Time.time + configuration.inputInterval;
            Consume(direction);
            return true;
        }

        void Consume(FishingDirection? direction)
        {
            bool correct = Round.Consume(direction);
            arrowElapsed = 0;
            presentation?.InputFeedback(correct);

            if (Round.Complete)
            {
                Resolve();
            }
        }

        void Resolve()
        {
            if (recorded)
            {
                return;
            }

            recorded = true;
            LastResult = new FishingResult(
                Selected.fishId,
                Selected.rarity,
                Round.Score,
                Selected.minimumScore,
                ActiveSpot.spotId
            );

            attempts.Add(LastResult);

            if (LastResult.success)
            {
                catches.Add(LastResult);
            }

            Enter(
                LastResult.success ? FishingState.CatchSuccess : FishingState.CatchFail,
                configuration.resultSeconds
            );
            presentation?.Result(LastResult.success, Selected);

            if (ResultRecorded != null)
            {
                foreach (Action<FishingResult> callback in ResultRecorded.GetInvocationList())
                {
                    try
                    {
                        callback(LastResult);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex, this);
                    }
                }
            }
        }

        void Enter(FishingState state, float duration)
        {
            State = state;
            elapsed = 0;
            phaseDuration = duration;
            string hook = state == FishingState.Casting ? "FishingCast"
                : state == FishingState.WaitingForBite ? "FishingIdle"
                : state == FishingState.BiteDetected ? "FishingReaction"
                : state == FishingState.Minigame ? "FishingReel"
                : state == FishingState.CatchSuccess ? "CatchSuccess"
                : state == FishingState.CatchFail ? "CatchFail"
                : null;

            if (animator != null
                && hook != null
                && animator.HasState(0, Animator.StringToHash(hook)))
            {
                animator.CrossFadeInFixedTime(hook, .15f);
            }

            StateChanged?.Invoke(state);
        }

        void RestoreInput()
        {
            if (!locked)
            {
                return;
            }

            locked = false;

            if (movement != null)
            {
                movement.enabled = oldMovement;
            }

            for (int i = 0; i < conflictingInteractions.Length; i++)
            {
                if (conflictingInteractions[i] != null
                    && oldInteractions != null
                    && i < oldInteractions.Length)
                {
                    conflictingInteractions[i].enabled = oldInteractions[i];
                }
            }

            if (!ReferenceEquals(player, null)
                && owners.TryGetValue(player, out var owner)
                && owner == this)
            {
                owners.Remove(player);
            }
        }

        void RestoreAnimator()
        {
            if (animator != null && originalController != null)
            {
                animator.runtimeAnimatorController = originalController;
                animator.SetBool("isWalking", false);
            }

            originalController = null;
        }

        void Finish()
        {
            Enter(FishingState.Cleanup, 0);

            RestoreInput();
            RestoreAnimator();
            presentation?.Clear();

            ActiveSpot = null;
            Selected = null;
            Round = null;

            State = FishingState.Idle;
            elapsed = 0;
            StateChanged?.Invoke(State);
        }

        public void Cancel()
        {
            if (!Busy && !locked)
            {
                return;
            }

            Finish();
        }

        void OnDisable()
        {
            Cancel();
        }

        void OnDestroy()
        {
            RestoreInput();
            RestoreAnimator();
            presentation?.Clear();
        }
    }
}
