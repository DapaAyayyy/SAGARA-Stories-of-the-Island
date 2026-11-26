using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sagara.DermagaV2.Wildlife
{
    [DisallowMultipleComponent]
    public sealed class BoundedRabbitPatrol : MonoBehaviour
    {
        private const int DestinationAttempts = 16;
        private const float RouteSampleSpacing = 0.16f;
        private const float CrouchBlendSeconds = 0.28f;
        private const float IdleSampleSeconds = 3.2f;
        private const float HopSettleTime = 0.875f;

        [SerializeField] private Transform patrolOrigin;
        [SerializeField, Min(0.65f)] private float patrolRadius = 1.60f;
        [SerializeField, Min(0f)] private float visualPadding = 0.48f;
        [SerializeField] private Collider support;
        [SerializeField] private LayerMask obstacleLayers = ~0;
        [SerializeField, Min(0.08f)] private float bodyRadius = 0.32f;
        [SerializeField, Min(0.16f)] private float bodyHeight = 0.74f;
        [SerializeField, Range(0f, 45f)] private float maximumSlope = 16f;
        [SerializeField, Min(0.025f)] private float maximumGroundDifference = 0.13f;
        [SerializeField, Min(20f)] private float turnSpeed = 110f;
        [SerializeField] private Vector2 restSeconds = new Vector2(1.2f, 3.0f);
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip strideClip;
        [Tooltip("Cumulative forward distance in meters over the normalized native stride, calibrated from the supporting paws.")]
        [SerializeField] private float[] strideTravel;
        [Tooltip("Height of the native pose's lowest point at evenly spaced times in the first 3.2 seconds of the idle.")]
        [SerializeField] private float[] idleSoleHeight;
        [Tooltip("Height of the native pose's lowest point at evenly spaced times across the stride clip.")]
        [SerializeField] private float[] strideSoleHeight;

        private enum State { Resting, Turning, Striding }

        private readonly Collider[] overlaps = new Collider[12];
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable idlePlayable;
        private AnimationClipPlayable stridePlayable;
        private State state;
        private Vector3 visualRestPosition;
        private Vector3 direction;
        private Vector3 strideStart;
        private Vector3 destination;
        private float centerRadius;
        private float minimumGroundNormal;
        private float strideDistance;
        private float elapsed;
        private float idleTime;
        private float restDuration;
        private float strideDuration;
        private float liftScale;
        private int strideCount;

        private void OnEnable()
        {
            if (!patrolOrigin || !support || !animator || !idleClip || !strideClip
                || !HasSamples(strideTravel) || !HasSamples(idleSoleHeight) || !HasSamples(strideSoleHeight))
            {
                Debug.LogWarning("Rabbit patrol requires its forest support, patrol origin and calibrated CDmir motion.", this);
                enabled = false;
                return;
            }

            centerRadius = Mathf.Max(0f, patrolRadius - visualPadding);
            minimumGroundNormal = Mathf.Cos(maximumSlope * Mathf.Deg2Rad);
            strideDistance = strideTravel[strideTravel.Length - 1];
            if (strideDistance <= 0f || !TryGetFooting(transform.position, out Vector3 footing))
            {
                Debug.LogWarning("Rabbit patrol must start on the configured forest support with a valid stride.", this);
                enabled = false;
                return;
            }

            transform.position = footing;
            visualRestPosition = animator.transform.localPosition;
            animator.applyRootMotion = false;
            graph = PlayableGraph.Create("CDmir rabbit patrol");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
            stridePlayable = AnimationClipPlayable.Create(graph, strideClip);
            graph.Connect(idlePlayable, 0, mixer, 0);
            graph.Connect(stridePlayable, 0, mixer, 1);
            AnimationPlayableOutput.Create(graph, "Rabbit", animator).SetSourcePlayable(mixer);
            graph.Play();
            Rest();
            Pose(0f, 0f);
        }

        private void OnDisable()
        {
            if (!graph.IsValid())
                return;

            graph.Destroy();
            if (animator)
                animator.transform.localPosition = visualRestPosition;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.10f);
            elapsed += dt;
            switch (state)
            {
                case State.Resting:
                    idleTime = Mathf.Min(elapsed, IdleSampleSeconds);
                    Pose(0f, 0f);
                    if (elapsed >= restDuration)
                        ChooseDestination();
                    break;
                case State.Turning:
                    Turn(dt);
                    break;
                case State.Striding:
                    Stride();
                    break;
            }
        }

        private void Rest()
        {
            state = State.Resting;
            elapsed = 0f;
            idleTime = 0f;
            float earliest = Mathf.Max(0.5f, restSeconds.x);
            restDuration = Random.Range(earliest, Mathf.Max(earliest, restSeconds.y));
        }

        private void ChooseDestination()
        {
            float firstAngle = Random.Range(-32f, 32f);
            float searchSide = Random.value < 0.5f ? -1f : 1f;
            for (int attempt = 0; attempt < DestinationAttempts; attempt++)
            {
                // Look near the current heading first, then work outwards in both directions.
                float angle = firstAngle + ((attempt + 1) / 2) * 23f * searchSide * (attempt % 2 == 0 ? -1f : 1f);
                Vector3 forward = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
                forward.y = 0f;
                forward.Normalize();
                Vector3 point = transform.position + forward * strideDistance;
                if (!IsRouteClear(transform.position, point))
                    continue;

                destination = point;
                direction = forward;
                elapsed = 0f;
                state = State.Turning;
                return;
            }

            restDuration += 0.65f;
        }

        private void Turn(float dt)
        {
            Quaternion heading = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, heading, turnSpeed * dt);
            float crouch = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / CrouchBlendSeconds));
            Pose(0f, crouch);
            if (crouch < 1f || Quaternion.Angle(transform.rotation, heading) > 0.1f)
                return;

            if (!IsRouteClear(transform.position, destination))
            {
                Rest();
                return;
            }

            strideStart = transform.position;
            // A relaxed ground-level step and a full hop use the same calibrated stance timing.
            bool hop = strideCount++ % 3 == 1;
            liftScale = hop ? 1f : 0.24f;
            strideDuration = hop ? 1.04f : 1.40f;
            elapsed = 0f;
            state = State.Striding;
        }

        private void Stride()
        {
            float phase = Mathf.Clamp01(elapsed / strideDuration) * HopSettleTime;
            float travel = Sample(strideTravel, phase);
            Vector3 next = strideStart + direction * travel;
            if (!TryGetFooting(next, out Vector3 footing))
            {
                Rest();
                return;
            }

            transform.position = footing;
            Pose(phase, 1f);
            if (elapsed >= strideDuration)
                Rest();
        }

        private void Pose(float phase, float strideWeight)
        {
            idlePlayable.SetTime(idleTime);
            stridePlayable.SetTime(phase * strideClip.length);
            mixer.SetInputWeight(0, 1f - strideWeight);
            mixer.SetInputWeight(1, strideWeight);
            graph.Evaluate(0f);

            float strideSole = Sample(strideSoleHeight, phase);
            float idleSole = Sample(idleSoleHeight, idleTime / IdleSampleSeconds);
            // Keep planted paws on the support and retain the native arc when the paws are airborne.
            float lift = Mathf.Max(0f, strideSole - 0.007f) * liftScale;
            float offset = Mathf.Lerp(-idleSole, lift - strideSole, strideWeight);
            animator.transform.localPosition = visualRestPosition + Vector3.up * offset;
        }

        private static bool HasSamples(float[] samples)
        {
            return samples != null && samples.Length >= 2;
        }

        private static float Sample(float[] samples, float phase)
        {
            float sample = Mathf.Clamp01(phase) * (samples.Length - 1);
            int index = Mathf.Min((int)sample, samples.Length - 2);
            return Mathf.Lerp(samples[index], samples[index + 1], sample - index);
        }

        private bool IsRouteClear(Vector3 start, Vector3 end)
        {
            int samples = Mathf.CeilToInt(strideDistance / RouteSampleSpacing);
            float previousHeight = start.y;
            for (int i = 1; i <= samples; i++)
            {
                Vector3 sample = Vector3.Lerp(start, end, (float)i / samples);
                if (!TryGetFooting(sample, out Vector3 footing)
                    || Mathf.Abs(footing.y - previousHeight) > 0.07f
                    || !HasClearance(footing))
                    return false;
                previousHeight = footing.y;
            }

            return true;
        }

        private bool TryGetFooting(Vector3 point, out Vector3 footing)
        {
            footing = default;
            Vector3 origin = patrolOrigin.position;
            Vector2 offset = new Vector2(point.x - origin.x, point.z - origin.z);
            if (!support.enabled || !support.gameObject.activeInHierarchy || offset.sqrMagnitude > centerRadius * centerRadius)
                return false;

            var ray = new Ray(new Vector3(point.x, origin.y + 0.65f, point.z), Vector3.down);
            if (!support.Raycast(ray, out RaycastHit surface, 1.30f) || surface.normal.y < minimumGroundNormal
                || Mathf.Abs(surface.point.y - origin.y) > maximumGroundDifference)
                return false;

            footing = surface.point;
            return true;
        }

        private bool HasClearance(Vector3 footing)
        {
            Vector3 lower = footing + Vector3.up * (bodyRadius + 0.025f);
            Vector3 upper = footing + Vector3.up * Mathf.Max(bodyRadius + 0.025f, bodyHeight - bodyRadius);
            int count = Physics.OverlapCapsuleNonAlloc(lower, upper, bodyRadius, overlaps, obstacleLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (overlaps[i] && overlaps[i] != support && !overlaps[i].transform.IsChildOf(transform))
                    return false;
            }

            return true;
        }

#if UNITY_EDITOR
        [UnityEditor.DrawGizmo(UnityEditor.GizmoType.Selected | UnityEditor.GizmoType.NonSelected)]
        private static void DrawPatrolGizmo(BoundedRabbitPatrol rabbit, UnityEditor.GizmoType gizmoType)
        {
            if (Application.isPlaying || !rabbit.patrolOrigin)
                return;

            var depth = UnityEditor.Handles.zTest;
            using (new UnityEditor.Handles.DrawingScope(new Color(0.28f, 0.96f, 0.69f)))
            {
                try
                {
                    UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                    Vector3 center = rabbit.patrolOrigin.position + Vector3.up * 0.07f;
                    UnityEditor.Handles.DrawWireDisc(center, Vector3.up, rabbit.patrolRadius, 2f);
                }
                finally
                {
                    UnityEditor.Handles.zTest = depth;
                }
            }
        }
#endif
    }
}
