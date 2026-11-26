using UnityEngine;

namespace Sagara.Dermaga.Fishing
{
    /// <summary>
    /// Controlled dock landing; no rigidbody and no reward authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishLandingPresentation : MonoBehaviour
    {
        // Session and fish bindings.
        public FishingSession session;
        public SessionFishVisuals visuals;

        // Landing motion configuration.
        [Min(.3f)]
        public float flightSeconds = 1.15f;
        [Range(1, 2)]
        public float flopSeconds = 1.55f;
        public float arcHeight = 2.3f;

        // Observed landing state.
        public bool Active { get; private set; }

        public bool ReadyForConfirmation { get; private set; }

        public bool HoldsResult => Active && !acknowledged;

        public string Stage { get; private set; }

        public Vector3 LandingPoint { get; private set; }

        public bool UsedFallback { get; private set; }

        public string SurfaceName { get; private set; }

        public float Elapsed { get; private set; }

        public float LowestMeshY { get; private set; }

        // Landing animation state.
        bool acknowledged;
        Vector3 start;
        GameObject fish;
        Animator animator;

        // Geometry support and landing pose.
        Renderer[] renderers;
        Mesh supportMesh;
        readonly System.Collections.Generic.List<Vector3> vertices =
            new System.Collections.Generic.List<Vector3>();
        Quaternion side;

        public event System.Action Landed;

        void OnEnable()
        {
            if (session == null)
            {
                session = GetComponent<FishingSession>();
            }

            if (visuals == null)
            {
                visuals = GetComponent<SessionFishVisuals>();
            }

            if (session != null)
            {
                session.StateChanged += OnState;
                session.ResultPresentationPending = () => HoldsResult;
            }
        }

        void OnState(FishingState state)
        {
            if (state == FishingState.CatchSuccess)
            {
                Begin();
            }
            else if (state == FishingState.Cleanup || state == FishingState.Idle)
            {
                ResetPresentation();
            }
        }

        void Begin()
        {
            ResetPresentation();
            fish = visuals == null ? null : visuals.ActiveFish;

            if (fish == null)
            {
                Debug.LogWarning("Catch visual unavailable; confirmation uses result data.", this);
                Active = true;
                ReadyForConfirmation = true;
                Stage = "Result";
                return;
            }

            Active = true;
            start = fish.transform.position;
            animator = fish.GetComponentInChildren<Animator>();
            renderers = fish.GetComponentsInChildren<Renderer>();

            side = Quaternion.Euler(0, 25, 90);
            Stage = "Emerging";
            ResolveLanding();
        }

        bool IsDeck(Collider c)
        {
            if (c == null || c.transform.root.name != "Dock")
            {
                return false;
            }

            if (c.name.StartsWith("BO_Deck_") || c.name.StartsWith("Lantai"))
            {
                return true;
            }

            for (var t = c.transform.parent; t != null; t = t.parent)
            {
                if (t.name == "Decks")
                {
                    return true;
                }
            }

            return false;
        }

        bool SurfaceAt(Vector3 at, out RaycastHit result)
        {
            var hits = Physics.RaycastAll(
                at + Vector3.up * 4,
                Vector3.down,
                8,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            foreach (var hit in hits)
            {
                if (IsDeck(hit.collider) && hit.normal.y > .95f)
                {
                    result = hit;
                    return true;
                }
            }

            result = new RaycastHit();
            return false;
        }

        bool Safe(Vector3 candidate, out RaycastHit surface)
        {
            if (!SurfaceAt(candidate, out surface))
            {
                return false;
            }

            float radius = .48f;

            foreach (var offset in new[]
            {
                new Vector3(radius, 0, 0),
                new Vector3(-radius, 0, 0),
                new Vector3(0, 0, radius),
                new Vector3(0, 0, -radius)
            })
            {
                RaycastHit edge;

                if (!SurfaceAt(candidate + offset, out edge)
                    || Mathf.Abs(edge.point.y - surface.point.y) > .06f)
                {
                    return false;
                }
            }

            if (Vector2.Distance(
                new Vector2(candidate.x, candidate.z),
                new Vector2(
                    session.player.transform.position.x,
                    session.player.transform.position.z
                )
            ) < .8f)
            {
                return false;
            }

            foreach (var c in Physics.OverlapBox(
                surface.point + Vector3.up * .4f,
                new Vector3(.48f, .35f, .48f),
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore
            ))
            {
                if (!IsDeck(c) && !c.transform.IsChildOf(session.player.transform))
                {
                    return false;
                }
            }

            return true;
        }

        void ResolveLanding()
        {
            var p = session.player.transform.position;
            var inland = p - session.ActiveSpot.CastPosition;
            inland.y = 0;
            inland.Normalize();
            var along = Vector3.Cross(Vector3.up, inland);

            RaycastHit hit;

            foreach (var offset in new[]
            {
                inland * 1.05f,
                inland * .95f + along * .65f,
                inland * .95f - along * .65f,
                along * 1.05f,
                -along * 1.05f
            })
            {
                if (Safe(p + offset, out hit))
                {
                    LandingPoint = hit.point;
                    SurfaceName = hit.collider.name;
                    return;
                }
            }

            // Fallback probes the larger dock neighborhood and still requires actual supported, clear geometry.
            UsedFallback = true;

            for (float radius = .9f; radius < 2.2f; radius += .3f)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI / 6;
                    var at = p + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0,
                        Mathf.Sin(angle) * radius
                    );

                    if (Safe(at, out hit))
                    {
                        LandingPoint = hit.point;
                        SurfaceName = hit.collider.name;
                        return;
                    }
                }
            }

            // Never place a visual in geometry if no supported landing exists.
            Debug.LogWarning(
                "No safe dock landing resolved; using result-card-only fallback near Player.",
                this
            );
            fish.SetActive(false);
            ReadyForConfirmation = true;
            Stage = "Result";
        }

        void LateUpdate()
        {
            if (!Active || ReadyForConfirmation || fish == null || !fish.activeSelf)
            {
                return;
            }

            Elapsed += Time.deltaTime;

            if (Elapsed < flightSeconds)
            {
                Stage = Elapsed < flightSeconds * .45f ? "Emerging" : "Flying";
                float p = Mathf.Clamp01(Elapsed / flightSeconds);
                fish.transform.rotation = Quaternion.Slerp(
                    Quaternion.LookRotation((LandingPoint - start).normalized),
                    side,
                    p * p
                );
                fish.transform.position =
                    Vector3.Lerp(
                        start,
                        LandingPoint + Vector3.up * .18f,
                        p
                    ) + Vector3.up * (Mathf.Sin(
                        p * Mathf.PI
                    ) * arcHeight);
                // Support height is measured using the current animated bounds on the final approach.
                if (p > .9f)
                {
                    Support(true);
                }

                session.presentation?.FollowCaughtFish(fish.transform.position);
            }
            else
            {
                if (Stage != "Flopping")
                {
                    Stage = "Flopping";
                    if (animator != null && animator.isActiveAndEnabled)
                    {
                        animator.Play("OutOfWater", 0, 0);
                    }
                    session.presentation?.FinishPull(true);
                    session.presentation?.Splash(start, session.Selected.aura, 8);
                    visuals.Celebrate(LandingPoint + Vector3.up * .2f);
                    Landed?.Invoke();
                }

                float t = Mathf.Clamp01((Elapsed - flightSeconds) / flopSeconds);
                float pulse = Mathf.Sin(t * Mathf.PI * 6);
                float envelope = (1 - t) * (1 - t);
                fish.transform.rotation = side * Quaternion.Euler(
                    pulse * 17 * envelope,
                    0,
                    Mathf.Sin(t * 25) * 9 * envelope
                );
                fish.transform.position = LandingPoint + new Vector3(
                    Mathf.Sin(t * 18) * .055f * envelope,
                    0,
                    Mathf.Sin(t * 13) * .045f * envelope
                );
                // Three damped twists, supported by deck every frame; source skeletal Out_Of_Water adds body/tail motion.
                Support();

                if (t >= 1)
                {
                    ReadyForConfirmation = true;
                    Stage = "Result";

                    if (animator != null)
                    {
                        animator.speed = 0;
                    }

                    Support();
                }
            }
        }

        void Support(bool onlyPreventClipping = false)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            float minimum = float.PositiveInfinity;

            foreach (var r in renderers)
            {
                var skin = r as SkinnedMeshRenderer;

                if (skin != null)
                {
                    // Renderer bounds may be conservative; evaluate the actual deformed mesh.
                    if (supportMesh == null)
                    {
                        supportMesh = new Mesh();
                    }

                    skin.BakeMesh(supportMesh, true);
                    supportMesh.GetVertices(vertices);

                    foreach (var vertex in vertices)
                    {
                        minimum = Mathf.Min(minimum, skin.transform.TransformPoint(vertex).y);
                    }
                }
                else
                {
                    minimum = Mathf.Min(minimum, r.bounds.min.y);
                }
            }

            if (!float.IsInfinity(minimum))
            {
                float correction = LandingPoint.y + .008f - minimum;

                if (!onlyPreventClipping || correction > 0)
                {
                    fish.transform.position += Vector3.up * correction;
                }

                LowestMeshY = minimum + ((!onlyPreventClipping || correction > 0) ? correction : 0);
            }
        }

        public void Acknowledge()
        {
            if (ReadyForConfirmation)
            {
                acknowledged = true;
            }
        }

        public void ResetPresentation()
        {
            Active = false;
            ReadyForConfirmation = false;
            acknowledged = false;
            Elapsed = 0;
            UsedFallback = false;
            SurfaceName = null;
            Stage = null;
            fish = null;
            animator = null;
            renderers = null;
        }

        void OnDestroy()
        {
            if (supportMesh != null)
            {
                Destroy(supportMesh);
            }
        }

        void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= OnState;
                session.ResultPresentationPending = null;

                if (session.State == FishingState.CatchSuccess)
                {
                    session.Cancel();
                }
            }

            ResetPresentation();
        }
    }
}
