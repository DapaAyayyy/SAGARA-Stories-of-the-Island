using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sagara.DermagaV2
{
    /// <summary>Owns an isolated live-animated 3D specimen turntable for Jurnal Pesisir (supports discovered + darkened silhouette modes).</summary>
    internal sealed class DermagaJournalPreview
    {
        private const int Layer = 31;
        private static readonly Vector3 DefaultOrigin = new Vector3(1500f, 1500f, 1500f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int UseVertexColor = Shader.PropertyToID("_UseVertexColor");
        private static readonly int SwayAmount = Shader.PropertyToID("_SwayAmount");
        private static readonly int SilhouetteMode = Shader.PropertyToID("_SilhouetteMode");

        private readonly Shader shader;
        private readonly Vector3 origin;
        private readonly List<Material> materials = new List<Material>();
        private GameObject stage;
        private Transform turntable;
        private GameObject model;
        private Camera camera;
        private RenderTexture texture;
        private Mesh coralMesh;
        private Vector3 baseLocalPos;
        private float yaw;
        private float bobPhase;
        private bool swims;

        internal DermagaJournalPreview(Shader shader) : this(shader, DefaultOrigin) { }

        internal DermagaJournalPreview(Shader shader, Vector3 stageOrigin)
        {
            this.shader = shader;
            this.origin = stageOrigin;
        }

        internal RenderTexture Show(GameObject fish, string coralId, Color coralTint, bool discovered = true)
        {
            Clear();
            if (!shader || (!fish && string.IsNullOrEmpty(coralId)))
            {
                return null;
            }

            if (!camera)
            {
                CreateStage();
            }

            swims = fish;
            float silhouette = discovered ? 0f : 1f;

            if (swims)
            {
                model = Object.Instantiate(fish, turntable, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
                var animator = model.GetComponentInChildren<Animator>();
                if (animator)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                    animator.speed = 1.15f;
                    animator.Play("Swim", 0, 0f);
                    animator.Update(0f);
                }
            }
            else
            {
                model = new GameObject("Coral specimen");
                model.transform.SetParent(turntable, false);
                coralMesh = DermagaCoralSpecimen.Build(coralId, coralTint);
                model.AddComponent<MeshFilter>().sharedMesh = coralMesh;
                float sway = coralId == "seafan_coral_observation" || coralId == "anemone_coral_observation" ? 1.0f : 0.42f;
                model.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(Color.white, true, sway, silhouette);
            }

            foreach (var child in model.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = Layer;
            }

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Clear();
                return null;
            }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                if (swims)
                {
                    var recolored = renderer.sharedMaterials;
                    for (int i = 0; i < recolored.Length; i++)
                    {
                        Material material = recolored[i];
                        Color color = material && material.HasProperty(BaseColor)
                            ? BoostFishColor(material.GetColor(BaseColor)) : Color.white;
                        recolored[i] = MakeMaterial(color, false, 0f, silhouette);
                    }
                    renderer.sharedMaterials = recolored;
                }
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            float largest = Mathf.Max(bounds.size.y, Mathf.Max(bounds.size.x, bounds.size.z) * .80f);
            float scale = 1.84f / Mathf.Max(.05f, largest);
            model.transform.localScale *= scale;
            model.transform.position -= (bounds.center - origin) * scale;
            baseLocalPos = model.transform.localPosition;
            yaw = swims ? -16f : 18f;
            bobPhase = 0f;
            turntable.localRotation = Quaternion.Euler(0f, yaw, 0f);
            camera.enabled = true;
            return texture;
        }

        private static Color BoostFishColor(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s = Mathf.Clamp01(s * 1.24f);
            v = Mathf.Clamp01(v * 1.12f);
            return Color.HSVToRGB(h, s, v);
        }

        internal void Update()
        {
            if (!model)
            {
                return;
            }
            float dt = Time.unscaledDeltaTime;
            yaw += dt * (swims ? 18f : 22f);
            bobPhase += dt * (swims ? 2.6f : 1.7f);
            turntable.localRotation = Quaternion.Euler(swims ? Mathf.Sin(bobPhase * 0.7f) * 3.5f : 0f, yaw, 0f);
            if (swims)
            {
                model.transform.localPosition = baseLocalPos + new Vector3(0f, Mathf.Sin(bobPhase) * 0.065f, 0f);
            }
        }

        internal void Rotate(float degrees)
        {
            yaw += degrees;
        }

        internal void Clear()
        {
            if (model)
            {
                model.SetActive(false);
                Object.Destroy(model);
                model = null;
            }
            foreach (var material in materials)
            {
                Object.Destroy(material);
            }
            materials.Clear();
            if (coralMesh)
            {
                Object.Destroy(coralMesh);
                coralMesh = null;
            }
            if (turntable)
            {
                turntable.localRotation = Quaternion.identity;
            }
            if (camera)
            {
                camera.enabled = false;
            }
        }

        internal void Dispose()
        {
            Clear();
            if (stage)
            {
                Object.Destroy(stage);
            }
            if (texture)
            {
                texture.Release();
                Object.Destroy(texture);
            }
        }

        private Material MakeMaterial(Color color, bool useVertexColor, float sway, float silhouette)
        {
            var material = new Material(shader);
            material.SetColor(BaseColor, color);
            material.SetFloat(UseVertexColor, useVertexColor ? 1f : 0f);
            material.SetFloat(SwayAmount, sway);
            material.SetFloat(SilhouetteMode, silhouette);
            materials.Add(material);
            return material;
        }

        private void CreateStage()
        {
            stage = new GameObject("Dermaga Journal Preview") { hideFlags = HideFlags.DontSave };
            stage.transform.position = origin;
            turntable = new GameObject("Specimen turntable").transform;
            turntable.SetParent(stage.transform, false);
            var rig = new GameObject("Specimen camera");
            rig.transform.SetParent(stage.transform, false);
            rig.transform.localPosition = new Vector3(0f, .52f, -5f);
            rig.transform.LookAt(origin);
            camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.38f;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 10f;
            camera.cullingMask = 1 << Layer;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.volumeLayerMask = 0;
            data.requiresColorTexture = false;
            data.requiresDepthTexture = false;
            texture = new RenderTexture(768, 576, 24, RenderTextureFormat.ARGB32)
            {
                name = "Journal specimen",
                antiAliasing = 4,
                hideFlags = HideFlags.DontSave
            };
            texture.Create();
            camera.targetTexture = texture;
        }
    }

    /// <summary>Builds rich, multi-toned, vertex-colored 3D coral & reef anemone specimens that animate smoothly in ocean currents.</summary>
    internal static class DermagaCoralSpecimen
    {
        private static readonly Color ReefStone = new Color(0.54f, 0.51f, 0.45f, 1f);
        private static readonly Color ReefMoss = new Color(0.36f, 0.46f, 0.36f, 1f);

        internal static Mesh Build(string id, Color accent)
        {
            var positions = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();

            // Natural rocky reef pedestal anchored at y=0
            AddPedestal(positions, colors, indices);

            switch (id)
            {
                case "branching_coral_observation":
                    BuildStaghorn(positions, colors, indices, accent);
                    break;
                case "plate_coral_observation":
                    BuildTableCoral(positions, colors, indices, accent);
                    break;
                case "seafan_coral_observation":
                    BuildSeaFan(positions, colors, indices, accent);
                    break;
                case "anemone_coral_observation":
                    BuildAnemone(positions, colors, indices, accent);
                    break;
                default:
                    BuildBrainDome(positions, colors, indices, accent);
                    break;
            }

            var mesh = new Mesh { name = id };
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddPedestal(List<Vector3> p, List<Color> c, List<int> idx)
        {
            Tube(p, c, idx, new Vector3(0f, -0.04f, 0f), new Vector3(0f, 0.14f, 0f), 0.56f, 0.42f, ReefStone, ReefMoss, 14, 0.14f);
            Tube(p, c, idx, new Vector3(-0.24f, -0.02f, 0.16f), new Vector3(-0.20f, 0.11f, 0.14f), 0.24f, 0.16f, ReefStone, ReefStone * 1.08f, 10, 0.12f);
            Tube(p, c, idx, new Vector3(0.26f, -0.02f, -0.14f), new Vector3(0.22f, 0.10f, -0.12f), 0.22f, 0.14f, ReefStone, ReefMoss, 10, 0.12f);
        }

        private static void BuildStaghorn(List<Vector3> p, List<Color> c, List<int> idx, Color accent)
        {
            Color baseCol = Color.Lerp(accent, new Color(0.55f, 0.18f, 0.22f), 0.35f);
            Color midCol = Color.Lerp(accent, new Color(0.98f, 0.46f, 0.38f), 0.45f);
            Color tipCol = new Color(1.0f, 0.90f, 0.72f);

            var root = new Vector3(0f, 0.12f, 0f);
            Tube(p, c, idx, root, new Vector3(0f, 0.62f, 0f), 0.22f, 0.12f, baseCol, midCol, 10);
            Tube(p, c, idx, new Vector3(0f, 0.62f, 0f), new Vector3(0.03f, 0.96f, 0.02f), 0.12f, 0.045f, midCol, tipCol, 10);

            for (int i = 0; i < 8; i++)
            {
                float turn = i * 2.39996f;
                var side = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                var elbow = root + side * (0.26f + (i % 3) * 0.09f) + Vector3.up * (0.36f + (i % 2) * 0.16f);
                var top = elbow + side * 0.20f + Vector3.up * (0.38f + (i % 3) * 0.10f);
                Tube(p, c, idx, root + Vector3.up * 0.06f, elbow, 0.115f, 0.088f, baseCol, midCol, 9);
                Tube(p, c, idx, elbow, top, 0.088f, 0.052f, midCol, tipCol, 9);
                Tube(p, c, idx, top, top + Vector3.up * 0.08f, 0.052f, 0.020f, tipCol, Color.white, 8);

                Vector3 fork = elbow + Vector3.Cross(Vector3.up, side) * 0.22f + Vector3.up * 0.31f;
                Tube(p, c, idx, elbow + Vector3.up * 0.04f, fork, 0.068f, 0.038f, midCol, tipCol, 8);
                Tube(p, c, idx, fork, fork + Vector3.up * 0.09f, 0.038f, 0.016f, tipCol, Color.white, 7);
            }
        }

        private static void BuildTableCoral(List<Vector3> p, List<Color> c, List<int> idx, Color accent)
        {
            Color stalk = Color.Lerp(accent, new Color(0.18f, 0.42f, 0.44f), 0.45f);
            Color plate = Color.Lerp(accent, new Color(0.24f, 0.72f, 0.66f), 0.45f);
            Color rim = new Color(0.95f, 0.90f, 0.56f);

            Tube(p, c, idx, new Vector3(0f, 0.08f, 0f), new Vector3(0.03f, 0.78f, 0f), 0.22f, 0.10f, stalk, plate, 12);
            AddRuffledPlate(p, c, idx, new Vector3(-0.18f, 0.36f, 0.08f), 0.54f, 0.085f, stalk, plate, rim);
            AddRuffledPlate(p, c, idx, new Vector3(0.14f, 0.56f, -0.06f), 0.66f, 0.095f, stalk, plate, rim);
            AddRuffledPlate(p, c, idx, new Vector3(-0.04f, 0.78f, 0.04f), 0.48f, 0.080f, stalk, plate, rim);
        }

        private static void BuildBrainDome(List<Vector3> p, List<Color> c, List<int> idx, Color accent)
        {
            Color deep = Color.Lerp(accent, new Color(0.32f, 0.44f, 0.26f), 0.4f);
            Color dome = Color.Lerp(accent, new Color(0.92f, 0.74f, 0.30f), 0.5f);
            Color crown = new Color(1.0f, 0.94f, 0.66f);

            // Sculpted organic fluted coral dome
            Tube(p, c, idx, new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.32f, 0f), 0.64f, 0.72f, deep, dome, 22, 0.13f);
            Tube(p, c, idx, new Vector3(0f, 0.32f, 0f), new Vector3(0.02f, 0.56f, 0.01f), 0.72f, 0.56f, dome, dome * 1.06f, 22, 0.15f);
            Tube(p, c, idx, new Vector3(0.02f, 0.56f, 0.01f), new Vector3(0.03f, 0.74f, 0.02f), 0.56f, 0.28f, dome, crown, 20, 0.14f);
            Tube(p, c, idx, new Vector3(0.03f, 0.74f, 0.02f), new Vector3(0.03f, 0.82f, 0.02f), 0.28f, 0.05f, crown, crown, 14, 0.08f);

            // Side companion coral lobe
            Tube(p, c, idx, new Vector3(-0.36f, 0.10f, 0.22f), new Vector3(-0.38f, 0.34f, 0.24f), 0.28f, 0.22f, deep, dome, 14, 0.12f);
            Tube(p, c, idx, new Vector3(-0.38f, 0.34f, 0.24f), new Vector3(-0.38f, 0.44f, 0.24f), 0.22f, 0.04f, dome, crown, 12, 0.08f);
        }

        private static void BuildSeaFan(List<Vector3> p, List<Color> c, List<int> idx, Color accent)
        {
            Color baseCol = new Color(0.46f, 0.18f, 0.48f);
            Color midCol = Color.Lerp(accent, new Color(0.84f, 0.36f, 0.68f), 0.55f);
            Color tipCol = new Color(1.0f, 0.78f, 0.88f);

            Vector3 origin = new Vector3(0f, 0.12f, 0f);
            Tube(p, c, idx, origin, new Vector3(0f, 0.28f, 0f), 0.12f, 0.08f, baseCol, midCol, 8);

            // Fanning gorgonian ribs arranged in a sweeping sea-fan arc
            int ribs = 9;
            for (int i = 0; i < ribs; i++)
            {
                float t = (i / (float)(ribs - 1)) * 2f - 1f; // -1 .. +1
                float angle = t * 1.12f;
                float len = 0.86f - Mathf.Abs(t) * 0.24f;
                Vector3 dir = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle) * 0.96f, Mathf.Sin(t * 2.4f) * 0.12f).normalized;
                Vector3 mid = origin + Vector3.up * 0.14f + dir * (len * 0.52f);
                Vector3 end = origin + Vector3.up * 0.14f + dir * len;
                Tube(p, c, idx, origin + Vector3.up * 0.12f, mid, 0.046f, 0.032f, baseCol, midCol, 7);
                Tube(p, c, idx, mid, end, 0.032f, 0.014f, midCol, tipCol, 7);

                // Delicate branching lace twigs
                Vector3 side = new Vector3(Mathf.Cos(angle), -Mathf.Sin(angle) * 0.4f, 0.18f * (i % 2 == 0 ? 1f : -1f)).normalized;
                Tube(p, c, idx, mid, mid + (dir * 0.18f + side * 0.16f), 0.022f, 0.010f, midCol, tipCol, 6);
                Tube(p, c, idx, mid, mid + (dir * 0.22f - side * 0.14f), 0.022f, 0.010f, midCol, tipCol, 6);
            }
        }

        private static void BuildAnemone(List<Vector3> p, List<Color> c, List<int> idx, Color accent)
        {
            Color foot = new Color(0.68f, 0.34f, 0.36f);
            Color body = Color.Lerp(accent, new Color(0.96f, 0.56f, 0.44f), 0.5f);
            Color tip = new Color(0.66f, 0.98f, 0.84f);

            // Soft cushioned anemone foot
            Tube(p, c, idx, new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.28f, 0f), 0.38f, 0.32f, foot, body, 14, 0.08f);

            // Double crown of curving swaying tentacles with glowing mint tips
            for (int ring = 0; ring < 2; ring++)
            {
                int count = ring == 0 ? 8 : 13;
                float rBase = ring == 0 ? 0.12f : 0.25f;
                float spread = ring == 0 ? 0.24f : 0.52f;
                float height = ring == 0 ? 0.72f : 0.56f;
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2f / count + ring * 0.28f;
                    Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 b = new Vector3(0f, 0.24f, 0f) + radial * rBase;
                    Vector3 m = b + radial * (spread * 0.55f) + Vector3.up * (height * 0.58f);
                    Vector3 t = m + radial * (spread * 0.48f) + Vector3.up * (height * 0.42f);
                    Tube(p, c, idx, b, m, 0.052f, 0.036f, body, Color.Lerp(body, tip, 0.45f), 8);
                    Tube(p, c, idx, m, t, 0.036f, 0.022f, Color.Lerp(body, tip, 0.45f), tip, 8);
                }
            }
        }

        private static void AddRuffledPlate(List<Vector3> p, List<Color> c, List<int> idx,
            Vector3 center, float radius, float height, Color underCol, Color topCol, Color rimCol)
        {
            Tube(p, c, idx, center - Vector3.up * height, center, radius * 0.48f, radius, underCol, rimCol, 22, 0.22f);
            Tube(p, c, idx, center, center + Vector3.up * (height * 0.42f), radius, radius * 0.78f, rimCol, topCol, 22, 0.18f);
        }

        private static void Tube(List<Vector3> vertices, List<Color> colors, List<int> triangles,
            Vector3 start, Vector3 finish, float r0, float r1, Color col0, Color col1, int sides = 8, float wave = 0f)
        {
            Vector3 axis = (finish - start).normalized;
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.up;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .93f ? Vector3.right : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            int first = vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 circle = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                float radius = 1f + wave * (.61f * Mathf.Cos(angle * 5f) + .39f * Mathf.Sin(angle * 3f));
                vertices.Add(start + circle * (r0 * radius));
                colors.Add(col0);
                vertices.Add(finish + circle * (r1 * radius) + axis * (wave * .11f * Mathf.Sin(5f * angle)));
                colors.Add(col1);
            }
            int bottom = vertices.Count;
            vertices.Add(start);
            colors.Add(col0);
            int top = vertices.Count;
            vertices.Add(finish);
            colors.Add(col1);
            for (int i = 0; i < sides; i++)
            {
                int a = first + i * 2;
                int b = first + ((i + 1) % sides) * 2;
                triangles.Add(a); triangles.Add(b); triangles.Add(b + 1);
                triangles.Add(a); triangles.Add(b + 1); triangles.Add(a + 1);
                triangles.Add(bottom); triangles.Add(b); triangles.Add(a);
                triangles.Add(top); triangles.Add(a + 1); triangles.Add(b + 1);
            }
        }
    }
}
