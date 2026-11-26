using System;
using System.Collections.Generic;
using Sagara.Dermaga.Fishing;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sagara.DermagaV2
{
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class DermagaQuestGuidance : MonoBehaviour
    {
        [Serializable]
        private sealed class Destination
        {
            public DermagaQuestPhase phase;
            public int slot;
            public int approach;
            public Transform target;
        }

        [Serializable]
        private struct Link
        {
            public int from;
            public int to;
        }

        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private FishingSession fishing;
        [SerializeField] private DermagaCoralExpedition expedition;
        [SerializeField] private Shader trailShader;
        [SerializeField] private bool showGroundTrail = false;
        [SerializeField] private Transform[] route = Array.Empty<Transform>();
        [SerializeField] private Link[] links = Array.Empty<Link>();
        [SerializeField] private Destination[] destinations = Array.Empty<Destination>();

        private const float RefreshSeconds = .12f;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private List<int>[] neighbours;
        private float[] cost;
        private bool[] visited;
        private Material material;
        private const int TrailCount = 14;
        private readonly Transform[] dots = new Transform[TrailCount];
        private readonly List<int> forwardRoute = new List<int>();
        private Mesh dotMesh;
        private Mesh glintMesh;
        private Mesh chevronMesh;
        private int[] predecessor;
        private Camera gameplayCamera;
        private float nextRefresh;
        private Vector3 lastRouteOrigin;
        private Vector3 routeEntry;
        private DermagaQuestPhase lastPhase;

        public Transform CurrentTarget { get; private set; }
        public Transform CurrentWaypoint { get; private set; }
        public bool ArrowVisible => dots[0] && dots[0].gameObject.activeSelf;
        public bool MarkerVisible => CurrentTarget;
        internal bool TargetIsPakDarma => CurrentTarget == quest.transform;
        internal Vector3 PakDarmaViewport => gameplayCamera
            ? gameplayCamera.WorldToViewportPoint(quest.transform.position + Vector3.up * 2.45f)
            : Vector3.zero;
        internal float Distance { get; private set; }
        internal Transform Player => fishing.player.transform;
        internal Vector3 DestinationViewport => gameplayCamera && CurrentTarget
            ? gameplayCamera.WorldToViewportPoint(CurrentTarget.position + Vector3.up * 1.5f)
            : Vector3.zero;

        private void Awake()
        {
            neighbours = new List<int>[route.Length];
            cost = new float[route.Length];
            predecessor = new int[route.Length];
            visited = new bool[route.Length];
            for (int i = 0; i < route.Length; i++)
            {
                neighbours[i] = new List<int>();
            }
            foreach (var link in links)
            {
                neighbours[link.from].Add(link.to);
                neighbours[link.to].Add(link.from);
            }

            material = new Material(trailShader);
            dotMesh = BuildTrailMesh(false);
            glintMesh = BuildTrailMesh(true);
            chevronMesh = BuildChevronMesh();
            for (int i = 0; i < dots.Length; i++)
            {
                Mesh shape = i % 3 == 0 ? chevronMesh : i % 3 == 1 ? glintMesh : dotMesh;
                dots[i] = CreateTrail($"Quest trail light {i + 1}", shape);
            }
        }

        private void LateUpdate()
        {
            if (!gameplayCamera)
            {
                gameplayCamera = Camera.main;
            }
            if (!quest.CanShowInteractionHint || fishing.NearbySpot != null || expedition.WaitingForPakDarma)
            {
                ClearPresentation();
                nextRefresh = 0f;
                return;
            }

            var pos = fishing.player.transform.position;
            if (Time.unscaledTime >= nextRefresh || lastPhase != quest.Phase
                || (pos - lastRouteOrigin).sqrMagnitude > .49f)
            {
                Refresh(pos);
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                lastPhase = quest.Phase;
                lastRouteOrigin = pos;
            }

            if (!showGroundTrail)
            {
                HideUnusedDots(0);
                return;
            }

            // Genshin-style forward-flowing golden pulse wave along the trail
            float beat = Time.unscaledTime * 5.2f;
            for (int i = 0; i < dots.Length && dots[i].gameObject.activeSelf; i++)
            {
                float wave = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(beat - i * .58f)), 2f);
                float baseScale = i == 0 ? 1.18f : (i % 3 == 0 ? 1.04f : (i % 3 == 1 ? 1.06f : .94f));
                float pulse = baseScale * (.88f + .26f * wave);
                dots[i].localScale = Vector3.one * pulse;
            }
        }

        private void OnDisable()
        {
            ClearPresentation();
        }

        private void OnDestroy()
        {
            Destroy(dotMesh);
            Destroy(glintMesh);
            Destroy(chevronMesh);
            Destroy(material);
        }

        private void ClearPresentation()
        {
            CurrentTarget = null;
            CurrentWaypoint = null;
            HideUnusedDots(0);
        }

        private void HideUnusedDots(int start)
        {
            for (int i = start; i < dots.Length; i++)
            {
                if (dots[i] && dots[i].gameObject.activeSelf)
                {
                    dots[i].gameObject.SetActive(false);
                }
            }
        }

        private void Refresh(Vector3 pos)
        {
            if (!Array.Exists(destinations, Available))
            {
                ClearPresentation();
                return;
            }

            if (!TryFindRouteEntry(pos, out var entry))
            {
                ClearPresentation();
                return;
            }
            SolveRoutes(entry);
            Destination chosen = null;
            float shortest = float.PositiveInfinity;
            foreach (var target in destinations)
            {
                if (Available(target) && cost[target.approach] < shortest)
                {
                    chosen = target;
                    shortest = cost[target.approach];
                }
            }
            if (chosen == null)
            {
                ClearPresentation();
                return;
            }

            CurrentTarget = chosen.target;
            Distance = shortest + Vector3.Distance(pos, routeEntry);
            forwardRoute.Clear();
            for (int node = chosen.approach; node >= 0; node = predecessor[node])
            {
                forwardRoute.Add(node);
            }
            int first = forwardRoute.Count - 1;
            if (first > 0 && Flat(route[forwardRoute[first]].position - routeEntry).sqrMagnitude < .09f)
            {
                first--;
            }
            CurrentWaypoint = route[forwardRoute[first]];
            if (showGroundTrail)
            {
                DrawConnectedTrail(routeEntry);
            }
            else
            {
                HideUnusedDots(0);
            }
        }

        private void DrawConnectedTrail(Vector3 from)
        {
            int count = 0;
            float next = .66f;
            for (int i = forwardRoute.Count - 1; i >= 0 && count < dots.Length; i--)
            {
                Vector3 to = route[forwardRoute[i]].position;
                float length = Vector3.Distance(from, to);
                if (length < .02f)
                {
                    continue;
                }
                while (next < length - .06f && count < dots.Length)
                {
                    if (TryFloor(Vector3.Lerp(from, to, next / length), out var floor))
                    {
                        Transform dot = dots[count];
                        Vector3 direction = Flat(to - from);
                        if (direction.sqrMagnitude > .001f)
                        {
                            dot.rotation = Quaternion.LookRotation(direction);
                        }
                        count++;
                        dot.position = floor + Vector3.up * .155f;
                        dot.gameObject.SetActive(true);
                    }
                    next += .62f;
                }
                next = Mathf.Max(.02f, next - length);
                from = to;
            }
            HideUnusedDots(count);
        }

        private bool Available(Destination target)
        {
            if (target.phase != quest.Phase || !target.target)
            {
                return false;
            }

            switch (target.phase)
            {
                case DermagaQuestPhase.CleanBeach:
                    return !quest.HasCollectedDebris(target.slot);
                case DermagaQuestPhase.CollectWood:
                    return !quest.HasCollectedBranch(target.slot);
                case DermagaQuestPhase.RepairDock:
                    return target.slot == quest.NextDockRepairSlot;
                case DermagaQuestPhase.PlantMangroves:
                    return !quest.HasPlantedMangrove(target.slot);
                case DermagaQuestPhase.CollectCoralSupplies:
                    return !quest.HasCoralSupply(target.slot);
                default:
                    return true;
            }
        }

        private bool TryFindRouteEntry(Vector3 playerPosition, out Link entry)
        {
            entry = default;
            float best = 8f * 8f;
            bool found = false;
            foreach (var link in links)
            {
                Vector3 start = route[link.from].position;
                Vector3 span = route[link.to].position - start;
                Vector3 horizontal = Flat(span);
                if (horizontal.sqrMagnitude < .0025f)
                {
                    continue;
                }

                float t = Mathf.Clamp01(Vector3.Dot(Flat(playerPosition - start), horizontal) / horizontal.sqrMagnitude);
                Vector3 projected = start + t * span;
                Vector3 offset = projected - playerPosition;
                float score = offset.sqrMagnitude + 7f * offset.y * offset.y;
                if (score >= best || !TryFloor(projected, out var floor)
                    || Mathf.Abs(floor.y - playerPosition.y) > .48f
                    || !ClearSegment(playerPosition, floor))
                {
                    continue;
                }

                best = score;
                entry = link;
                routeEntry = floor;
                found = true;
            }
            return found;
        }

        private void SolveRoutes(Link entry)
        {
            for (int i = 0; i < route.Length; i++)
            {
                cost[i] = float.PositiveInfinity;
                predecessor[i] = -1;
                visited[i] = false;
            }

            cost[entry.from] = Vector3.Distance(routeEntry, route[entry.from].position);
            cost[entry.to] = Vector3.Distance(routeEntry, route[entry.to].position);

            for (int i = 0; i < route.Length; i++)
            {
                int current = -1;
                float shortest = float.PositiveInfinity;
                for (int j = 0; j < route.Length; j++)
                {
                    if (!visited[j] && cost[j] < shortest)
                    {
                        current = j;
                        shortest = cost[j];
                    }
                }
                if (current < 0)
                {
                    break;
                }
                visited[current] = true;

                foreach (int next in neighbours[current])
                {
                    float distance = shortest + Vector3.Distance(route[current].position, route[next].position);
                    if (distance < cost[next])
                    {
                        cost[next] = distance;
                        predecessor[next] = current;
                    }
                }
            }
        }

        private bool ClearSegment(Vector3 from, Vector3 to)
        {
            Vector3 direction = Flat(to - from);
            float length = direction.magnitude;
            if (length < .20f)
            {
                return Mathf.Abs(to.y - from.y) < .37f;
            }

            int steps = Mathf.CeilToInt(length / .56f);
            Vector3 last = from;
            for (int i = 1; i <= steps; i++)
            {
                if (!TryFloor(Vector3.Lerp(from, to, (float)i / steps), out var floor)
                    || Mathf.Abs(last.y - floor.y) > .42f)
                {
                    return false;
                }
                var delta = Flat(floor - last);
                float distance = delta.magnitude;
                Vector3 start = last + Vector3.up * .49f;
                int count = Physics.CapsuleCastNonAlloc(start, start + Vector3.up * .71f, .29f,
                    delta.normalized, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < count; j++)
                {
                    if (hits[j].collider != fishing.player && hits[j].distance > .017f
                        && hits[j].normal.y < .59f)
                    {
                        return false;
                    }
                }
                last = floor;
            }
            return true;
        }

        private bool TryFloor(Vector3 expected, out Vector3 point)
        {
            point = default;
            float best = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(expected + Vector3.up * .76f, Vector3.down,
                hits, 1.48f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                float difference = Mathf.Abs(expected.y - hit.point.y);
                if (hit.collider != fishing.player && hit.normal.y > .65f && difference < .53f && difference < best)
                {
                    best = difference;
                    point = hit.point;
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction;
        }

        private Transform CreateTrail(string label, Mesh mesh)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        private static readonly Color TrailShadow = new Color(.08f, .10f, .09f, .44f);
        private static readonly Color TrailShadowClear = new Color(.08f, .10f, .09f, 0f);
        private static readonly Color TrailAura = new Color(1f, .76f, .24f, .52f);
        private static readonly Color TrailAuraClear = new Color(1f, .76f, .24f, 0f);
        private static readonly Color TrailGold = new Color(1f, .80f, .26f, 1f);
        private static readonly Color TrailCore = new Color(1f, .98f, .84f, 1f);

        private static Mesh BuildTrailMesh(bool glint)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            int sides = glint ? 8 : 16;
            var liftGlow = new Vector3(0f, .005f, 0f);
            var liftCore = new Vector3(0f, .012f, 0f);

            for (int i = 0; i < sides; i++)
            {
                float angle = i * 2f * Mathf.PI / sides;
                float next = (i + 1) * 2f * Mathf.PI / sides;
                float radius = glint ? (i % 2 == 0 ? .28f : .10f) : .135f;
                float nextRadius = glint ? (i % 2 == 0 ? .10f : .28f) : .135f;
                var a = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                var b = new Vector3(Mathf.Cos(next), 0f, Mathf.Sin(next)) * nextRadius;

                // Soft dark-warm ground cushion so golden trail pops cleanly over bright sand AND wood
                AddQuad(vertices, colors, indices, a * 1.58f, b * 1.58f, b * .78f, a * .78f,
                    TrailShadowClear, TrailShadow);
                // Warm luminous golden halo
                AddQuad(vertices, colors, indices, a * 1.48f + liftGlow, b * 1.48f + liftGlow,
                    b * .88f + liftGlow, a * .88f + liftGlow, TrailAuraClear, TrailAura);
                // Crisp radiant golden-white core
                AddQuad(vertices, colors, indices, a * .92f + liftCore, b * .92f + liftCore,
                    liftCore, liftCore, TrailGold, TrailCore);
            }
            return CreateTrailMesh(glint ? "Luminous quest star" : "Luminous quest bead", vertices, colors, indices);
        }

        private static Mesh BuildChevronMesh()
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();

            // Sleek Genshin-inspired forward chevron with soft grounding halo + luminous golden-white core
            var tip = new Vector3(0f, 0f, .38f);
            var right = new Vector3(.30f, 0f, -.08f);
            var rightEnd = new Vector3(.19f, 0f, -.22f);
            var notch = new Vector3(0f, 0f, .05f);
            var left = new Vector3(-right.x, 0f, right.z);
            var leftEnd = new Vector3(-rightEnd.x, 0f, rightEnd.z);

            // Soft dark-warm contrast halo underneath (no hard black outline)
            float sh = 1.24f;
            AddQuad(vertices, colors, indices, tip * sh, right * sh, rightEnd * sh, notch * sh,
                TrailShadow, TrailShadowClear);
            AddQuad(vertices, colors, indices, tip * sh, notch * sh, leftEnd * sh, left * sh,
                TrailShadow, TrailShadowClear);

            // Golden aura layer
            var liftAura = new Vector3(0f, .006f, 0f);
            float au = 1.14f;
            AddQuad(vertices, colors, indices, tip * au + liftAura, right * au + liftAura,
                rightEnd * au + liftAura, notch * au + liftAura, TrailAura, TrailAuraClear);
            AddQuad(vertices, colors, indices, tip * au + liftAura, notch * au + liftAura,
                leftEnd * au + liftAura, left * au + liftAura, TrailAura, TrailAuraClear);

            // Crisp radiant gold-to-ivory chevron body
            var liftCore = new Vector3(0f, .014f, 0f);
            AddQuad(vertices, colors, indices, tip + liftCore, right + liftCore, rightEnd + liftCore,
                notch + liftCore, TrailCore, TrailGold);
            AddQuad(vertices, colors, indices, tip + liftCore, notch + liftCore, leftEnd + liftCore,
                left + liftCore, TrailCore, TrailGold);
            return CreateTrailMesh("Genshin-style quest direction chevron", vertices, colors, indices);
        }

        private static Mesh CreateTrailMesh(string label, List<Vector3> vertices, List<Color> colors, List<int> indices)
        {
            var mesh = new Mesh { name = label };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(List<Vector3> vertices, List<Color> colors, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color first, Color second)
        {
            int index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            colors.Add(first);
            colors.Add(first);
            colors.Add(second);
            colors.Add(second);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }
    }
}
