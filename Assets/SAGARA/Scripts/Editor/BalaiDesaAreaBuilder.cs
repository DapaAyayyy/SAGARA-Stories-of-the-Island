using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using G = Sagara.EditorTools.BalaiDesaGreyboxBuilder;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Sagara.EditorTools
{
    /// <summary>
    /// Finishes the Balai Desa area in MainVillage.unity: village boundary (tree line + colliders so the
    /// Player cannot leave the terrain), scattered vegetation that keeps clear of paths and of the camera
    /// line of sight, alun-alun props saved as prefabs, street lamps and NPC slots.
    /// Run after "Greybox Balai Desa" (and optionally "Balai Desa Sementara"). Menu: SAGARA > Lengkapi Area Balai Desa.
    /// </summary>
    public sealed class BalaiDesaAreaBuilder : EditorWindow
    {
        private const string PropsFolder = "Assets/SAGARA/Prefabs/Village/Props";
        private const string AlunAlunPrefabPath = "Assets/SAGARA/Prefabs/Village/AlunAlun.prefab";
        private const string NatureFolder = "Assets/SAGARA/Prefabs/Nature/";

        // The camera sits 20 m south of the Player and 12 m up, so anything tall just south of a path hides the Player.
        private const float CameraShadowDepth = 10f;

        [SerializeField] private int seed = 2026;
        [SerializeField] private float decorRadius = 65f;
        [SerializeField] private int treeCount = 45;
        [SerializeField] private int bushCount = 45;
        [SerializeField] private int flowerCount = 30;
        [SerializeField] private int grassCount = 40;
        [SerializeField] private int rockCount = 10;
        [SerializeField] private float boundaryInset = 3f;
        [SerializeField] private bool roadLamps = true;

        private Vector2 scroll;

        [MenuItem("SAGARA/Lengkapi Area Balai Desa")]
        private static void Open()
        {
            GetWindow<BalaiDesaAreaBuilder>("Lengkapi Area Balai Desa").minSize = new Vector2(340, 380);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "Menambahkan batas desa, pohon & tanaman, props alun-alun (bangku, lampu, tiang bendera, pot, " +
                "papan informasi), lampu jalan, dan slot NPC. Jalankan setelah greybox / Balai Desa Sementara.",
                MessageType.None);

            EditorGUILayout.LabelField("Vegetasi", EditorStyles.boldLabel);
            seed = EditorGUILayout.IntField("Seed acak", seed);
            decorRadius = EditorGUILayout.Slider("Radius sebaran (m)", decorRadius, 30f, 100f);
            treeCount = EditorGUILayout.IntSlider("Pohon", treeCount, 0, 120);
            bushCount = EditorGUILayout.IntSlider("Semak", bushCount, 0, 120);
            flowerCount = EditorGUILayout.IntSlider("Bunga & pakis", flowerCount, 0, 120);
            grassCount = EditorGUILayout.IntSlider("Rumput", grassCount, 0, 150);
            rockCount = EditorGUILayout.IntSlider("Batu", rockCount, 0, 40);

            EditorGUILayout.LabelField("Batas dan lampu", EditorStyles.boldLabel);
            boundaryInset = EditorGUILayout.Slider("Jarak batas dari tepi terrain", boundaryInset, 0f, 20f);
            roadLamps = EditorGUILayout.Toggle("Lampu di tepi jalan", roadLamps);

            EditorGUILayout.HelpBox(
                "Pohon tidak ditanam di jalan/alun-alun, dan tidak di sisi selatan jalan (sisi kamera) " +
                "supaya Player tidak tertutup. Ganti seed untuk susunan lain.", MessageType.Info);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Lengkapi Area", GUILayout.Height(32))) Build();
            }

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            if (!G.OpenOrCreateScene()) return;
            Layout layout = Layout.Read();
            if (layout == null)
            {
                EditorUtility.DisplayDialog("Lengkapi Area Balai Desa",
                    "Terrain atau alun-alun belum ada. Jalankan dulu SAGARA > Greybox Balai Desa.", "OK");
                return;
            }

            RemoveReferenceCapsules();
            var mat = new G.Palette();
            var props = new PropPrefabs(mat);

            BuildAlunAlun(layout, mat, props);
            BuildBalaiDecor(layout, mat, props);
            if (roadLamps) BuildRoadLamps(layout, props);
            int trees = BuildVegetation(layout);
            int pieces = BuildBoundary(layout);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Lengkapi Area] Selesai: alun-alun Ø {layout.PlazaRadius * 2f:0.#} m, {trees} pohon/batu, " +
                      $"{pieces} potongan batas, area bisa dilalui {layout.WalkMin.x:0}..{layout.WalkMax.x:0} × " +
                      $"{layout.WalkMin.y:0}..{layout.WalkMax.y:0} m.");
        }

        private static void BuildAlunAlun(Layout layout, G.Palette mat, PropPrefabs props)
        {
            float r = layout.PlazaRadius;
            Transform roads = G.Root("_Roads");
            Transform greyPlaza = roads.Find("GB_AlunAlun");
            if (greyPlaza != null) greyPlaza.gameObject.SetActive(false);
            Transform greyBoard = G.Root("_Props").Find("GB_PapanInformasi");
            if (greyBoard != null) greyBoard.gameObject.SetActive(false);

            Transform root = G.Fresh(G.Root("_Props"), "AlunAlun");

            Transform floor = Group(root, "Lantai");
            G.Cylinder(floor, "Lantai_AlunAlun", new Vector3(0f, -0.06f, 0f), r, 0.12f, mat.Stone, false);
            G.Cylinder(floor, "Lingkar_Tengah", new Vector3(0f, -0.05f, 0f), 2.5f, 0.12f, mat.StoneDark, false);
            G.Cylinder(floor, "Lingkar_Luar", new Vector3(0f, -0.06f, 0f), r + 0.4f, 0.11f, mat.StoneDark, false);
            for (int i = 0; i < 8; i++)
            {
                // Spokes give the "batu melingkar" pattern from the map.
                float a = i * 45f;
                G.Box(floor, $"Garis_Batu_{i + 1}", new Vector3(0f, 0.005f, 0f) + Dir(a) * ((r + 2.5f) * 0.5f),
                    new Vector3(0.25f, 0.12f, r - 2.5f), mat.StoneDark, false, Quaternion.LookRotation(Dir(a)));
            }

            Transform items = Group(root, "Props");
            Spawn(props.Flagpole, items, Vector3.zero, Vector3.forward);

            Vector3 boardDir = Dir(135f);
            Vector3 boardPos = boardDir * (r - 2.5f);
            Spawn(props.Board, items, boardPos, -boardDir);

            foreach (float a in new[] { 50f, 215f, 250f, 290f })
            {
                Spawn(props.Bench, items, Dir(a) * (r - 2.2f), -Dir(a));
            }

            foreach (float a in new[] { 40f, 65f, 115f, 165f, 195f, 300f, 330f })
            {
                Spawn(props.Lamp, items, Dir(a) * (r - 0.8f), -Dir(a));
            }

            foreach (float side in new[] { -1f, 1f })
            {
                Spawn(props.Pot, items, new Vector3(side * (layout.StepsWidth * 0.5f + 0.9f), 0f, layout.StepsStartZ - 0.8f),
                    Vector3.back);
            }

            // Trash bins (organik / anorganik) next to two benches: a small nod to the clean-coast theme (SDG 14).
            foreach (float a in new[] { 227f, 302f })
            {
                Spawn(props.TrashBins, items, Dir(a) * (r - 2.2f), -Dir(a));
            }

            // Ring of flowers around the flagpole.
            Transform garden = Group(root, "Taman_Tengah");
            var flowers = AssetDatabase.LoadAssetAtPath<GameObject>(NatureFolder + "Flowers.prefab");
            if (flowers != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    var f = (GameObject)PrefabUtility.InstantiatePrefab(flowers, garden);
                    f.transform.localPosition = Dir(i * 45f + 22.5f) * 1.7f;
                    f.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f) * flowers.transform.localRotation;
                }
            }

            // Vendor cart with crates on the grass north-west of the plaza (north of the west road, so it never hides the Player).
            Transform cart = Group(root, "Gerobak_Pedagang");
            Vector3 cartPos = Dir(150f) * (r + 3.5f);
            PlaceKit(cart, "Prop_Wagon", cartPos, 240f, 1.5f);
            PlaceKit(cart, "Prop_Crate", Dir(140f) * (r + 5.5f), 20f, 1.5f);
            PlaceKit(cart, "Prop_Crate", Dir(144f) * (r + 6.9f), 65f, 1.5f);
            layout.Reserved.Add(new Vector4(cartPos.x, cartPos.z, 5f, 0f));

            Transform interactables = G.Root("_Interactables");
            G.Marker(interactables, "Interact_PapanInformasi", boardPos - boardDir * 1.3f, boardDir, 1);
            G.Marker(interactables, "NPC_Slot_AlunAlun", new Vector3(-3f, 0f, -3f), Vector3.forward, 4);
            G.Marker(interactables, "NPC_Slot_TerasBalai",
                new Vector3(-(layout.StepsWidth * 0.5f + 2f), layout.PorchTopY, layout.PorchFrontZ + 1.2f), Vector3.back, 4);

            G.MarkStatic(root);
            G.EnsureFolder("Assets/SAGARA/Prefabs/Village");
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, AlunAlunPrefabPath, InteractionMode.AutomatedAction);
        }

        /// <summary>Pots, hanging lanterns, Merah Putih bunting, name plate, flower beds and side bushes for the Balai.</summary>
        private static void BuildBalaiDecor(Layout layout, G.Palette mat, PropPrefabs props)
        {
            Transform root = G.Fresh(G.Root("_Props"), "Dekorasi_BalaiDesa");
            if (!layout.HasBalai) return;

            // Pots either side of the door, on the porch.
            foreach (float side in new[] { -1f, 1f })
            {
                Spawn(props.Pot, root, new Vector3(side * 2.4f, layout.PorchTopY, layout.BodyFrontZ - 0.9f), Vector3.back);
            }

            // Lanterns hanging from the beam, between the columns.
            Transform lanterns = Group(root, "Lentera_Teras");
            layout.ColumnXs.Sort();
            for (int i = 0; i + 1 < layout.ColumnXs.Count; i++)
            {
                float x = (layout.ColumnXs[i] + layout.ColumnXs[i + 1]) * 0.5f;
                Spawn(props.HangingLantern, lanterns, new Vector3(x, layout.BeamBottomY, layout.BeamZ), Vector3.back);
            }

            // Merah Putih bunting along the front of the beam (triangles pointing down).
            Transform bunting = Group(root, "Umbul_Umbul");
            float front = layout.BeamZ - layout.BeamDepth * 0.5f - 0.03f;
            int count = Mathf.FloorToInt((layout.BeamWidth - 0.6f) / 0.55f);
            for (int i = 0; i < count; i++)
            {
                float x = -(count - 1) * 0.55f * 0.5f + i * 0.55f;
                var flag = new GameObject(i % 2 == 0 ? "Umbul_Merah" : "Umbul_Putih");
                flag.transform.SetParent(bunting, false);
                flag.transform.localPosition = new Vector3(x, layout.BeamBottomY + 0.02f, front);
                flag.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                flag.transform.localScale = new Vector3(0.42f, 0.5f, 0.03f);
                flag.AddComponent<MeshFilter>().sharedMesh = G.PrismMesh();
                flag.AddComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? props.FlagRed : mat.Wall;
            }

            // Name plate above the beam (text to be added once the name Balai Desa / Balai Kota is decided).
            Transform plate = Group(root, "PapanNama_BalaiDesa");
            float plateY = layout.BeamBottomY + layout.BeamHeight + 0.55f;
            G.Box(plate, "Bingkai", new Vector3(0f, plateY, front + 0.02f), new Vector3(3.4f, 0.9f, 0.08f), mat.Wood, false);
            G.Box(plate, "Isi", new Vector3(0f, plateY, front - 0.03f), new Vector3(3.1f, 0.65f, 0.04f), props.Paper, false);

            // Flower beds along the front of the plinth, either side of the stairs.
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float offset in new[] { 1.6f, 4.3f })
                {
                    float x = side * (layout.StepsWidth * 0.5f + offset);
                    if (Mathf.Abs(x) + 1.2f > layout.PlinthHalfX + 0.6f) continue;
                    Spawn(props.FlowerBed, root, new Vector3(x, 0f, layout.PorchFrontZ - 0.75f), Vector3.back);
                }
            }

            // Bushes along both sides of the building.
            var bush = AssetDatabase.LoadAssetAtPath<GameObject>(NatureFolder + "Bush.prefab");
            if (bush != null)
            {
                Transform bushes = Group(root, "Semak_Samping");
                foreach (float side in new[] { -1f, 1f })
                {
                    for (float z = layout.PorchFrontZ + 1f; z < layout.PlinthBackZ - 0.5f; z += 2.4f)
                    {
                        var b = (GameObject)PrefabUtility.InstantiatePrefab(bush, bushes);
                        b.transform.localPosition = new Vector3(side * (layout.PlinthHalfX + 0.9f), 0f, z);
                        b.transform.localRotation = Quaternion.Euler(0f, z * 37f, 0f) * bush.transform.localRotation;
                    }
                }
            }

            G.MarkStatic(root);
        }

        private const string KitFolder = "Assets/SAGARA/Art/Medieval Village MegaKit[Standard]/FBX/";

        /// <summary>Places a MegaKit model standing on the ground, keeping its FBX root scale/rotation, with a box collider.</summary>
        private static void PlaceKit(Transform parent, string name, Vector3 position, float yaw, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + name + ".fbx");
            if (prefab == null) return;
            Bounds b = MeasurePrefab(prefab);

            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = position;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            go.transform.localRotation = prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale * scale;
            go.transform.localPosition = new Vector3(-b.center.x * scale, -b.min.y * scale, -b.center.z * scale);

            var c = holder.gameObject.AddComponent<BoxCollider>();
            c.center = new Vector3(0f, b.size.y * scale * 0.5f, 0f);
            c.size = b.size * scale;
        }

        /// <summary>Removes the old orange "Player height" capsules (scene objects and the Balai prefab).</summary>
        private static void RemoveReferenceCapsules()
        {
            const string capsule = "Patokan_Tinggi_Player_2.25m";
            const string balaiPrefab = "Assets/SAGARA/Prefabs/Village/BalaiDesa_Sementara.prefab";
            if (File.Exists(balaiPrefab))
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(balaiPrefab);
                bool changed = false;
                foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == capsule) { Object.DestroyImmediate(t.gameObject); changed = true; }
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, balaiPrefab);
                PrefabUtility.UnloadPrefabContents(contents);
            }

            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == capsule && !PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                        Object.DestroyImmediate(t.gameObject);
                }
            }
        }

        private static void BuildRoadLamps(Layout layout, PropPrefabs props)
        {
            Transform root = G.Fresh(G.Root("_Props"), "LampuJalan");
            foreach (OrientedRect road in layout.Roads)
            {
                if (road.Name == "Jalan_KeTangga") continue;
                bool alongX = road.Half.x > road.Half.y;
                Vector3 along = road.Rot * (alongX ? Vector3.right : Vector3.forward);
                Vector3 side = road.Rot * (alongX ? Vector3.forward : Vector3.right);
                if (side.z < -0.01f || (Mathf.Abs(side.z) <= 0.01f && side.x < 0f)) side = -side; // north/east side only
                float half = alongX ? road.Half.x : road.Half.y;
                float width = alongX ? road.Half.y : road.Half.x;
                for (float t = -half + 4f; t <= half - 2f; t += 14f)
                {
                    Vector3 p = road.Center + along * t + side * (width + 1.2f);
                    if (new Vector2(p.x, p.z).magnitude < layout.PlazaRadius + 3f) continue;
                    if (layout.InsideBalai(p, 2f)) continue;
                    Spawn(props.Lamp, root, p, -side);
                }
            }

            G.MarkStatic(root);
        }

        private int BuildVegetation(Layout layout)
        {
            Transform root = G.Fresh(G.Root("_Vegetation"), "Vegetasi_Desa");
            var rng = new Random(seed);
            var tallPoints = new List<Vector3>();
            var smallPoints = new List<Vector3>();
            int tall = 0;

            tall += Scatter(root, "Pohon", new[] { "TreeRound", "TreeBroad" }, treeCount, layout, rng, true, 4.5f, 4f, tallPoints, smallPoints);
            tall += Scatter(root, "Batu", new[] { "Rock", "RockCluster" }, rockCount, layout, rng, true, 3f, 3f, tallPoints, smallPoints);
            Scatter(root, "Semak", new[] { "Bush" }, bushCount, layout, rng, false, 2f, 1.8f, tallPoints, smallPoints);
            Scatter(root, "Bunga", new[] { "Flowers", "Fern" }, flowerCount, layout, rng, false, 1.5f, 1.2f, tallPoints, smallPoints);
            Scatter(root, "Rumput", new[] { "GrassTuft" }, grassCount, layout, rng, false, 1f, 0.8f, tallPoints, smallPoints);

            G.MarkStatic(root);
            return tall;
        }

        private int Scatter(Transform root, string groupName, string[] prefabNames, int count, Layout layout, Random rng,
            bool tall, float clearance, float spacing, List<Vector3> tallPoints, List<Vector3> smallPoints)
        {
            var prefabs = new List<GameObject>();
            foreach (string n in prefabNames)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(NatureFolder + n + ".prefab");
                if (p != null) prefabs.Add(p);
            }

            if (prefabs.Count == 0 || count <= 0) return 0;

            Transform group = Group(root, groupName);
            var centre = new Vector3(0f, 0f, layout.BalaiCenter.z * 0.5f);
            int placed = 0;
            for (int attempt = 0; attempt < count * 60 && placed < count; attempt++)
            {
                double angle = rng.NextDouble() * Math.PI * 2.0;
                double dist = Math.Sqrt(rng.NextDouble()) * decorRadius;
                var p = centre + new Vector3((float)(Math.Cos(angle) * dist), 0f, (float)(Math.Sin(angle) * dist));
                if (!layout.IsClear(p, clearance, tall)) continue;
                if (TooClose(p, tallPoints, tall ? spacing : 1.2f) || TooClose(p, smallPoints, tall ? 1.2f : spacing)) continue;

                GameObject prefab = prefabs[rng.Next(prefabs.Count)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                go.transform.localPosition = p;
                go.transform.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f) * prefab.transform.localRotation;
                go.transform.localScale = prefab.transform.localScale * (float)(0.85 + rng.NextDouble() * 0.4);
                (tall ? tallPoints : smallPoints).Add(p);
                placed++;
            }

            return placed;
        }

        private int BuildBoundary(Layout layout)
        {
            Transform root = G.Fresh(G.Root("_Vegetation"), "Batas_Desa");
            var piece = AssetDatabase.LoadAssetAtPath<GameObject>(NatureFolder + "Boundary_Center_Tropical.prefab");

            float depth = 4f;
            float length = 13f;
            bool longAlongX = true;
            Bounds b = new Bounds(Vector3.zero, Vector3.one);
            if (piece != null)
            {
                b = MeasurePrefab(piece);
                longAlongX = b.size.x >= b.size.z;
                length = Mathf.Max(Mathf.Max(b.size.x, b.size.z) * 0.95f, 2f);
                depth = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z), 1f, 8f);
            }

            Vector2 min = layout.TerrainMin + Vector2.one * (boundaryInset + depth * 0.5f);
            Vector2 max = layout.TerrainMax - Vector2.one * (boundaryInset + depth * 0.5f);
            layout.WalkMin = min + Vector2.one * depth * 0.5f;
            layout.WalkMax = max - Vector2.one * depth * 0.5f;

            var corners = new[]
            {
                new Vector3(min.x, 0f, min.y), new Vector3(max.x, 0f, min.y),
                new Vector3(max.x, 0f, max.y), new Vector3(min.x, 0f, max.y),
            };

            int pieces = 0;
            Transform trees = Group(root, "Pohon_Batas");
            Transform colliders = Group(root, "Collider_Batas");
            for (int e = 0; e < 4; e++)
            {
                Vector3 a = corners[e];
                Vector3 c = corners[(e + 1) % 4];
                Vector3 dir = (c - a).normalized;
                float edgeLength = Vector3.Distance(a, c);
                bool edgeAlongX = Mathf.Abs(dir.x) > 0.5f;

                var box = new GameObject($"Batas_{e + 1}").AddComponent<BoxCollider>();
                box.transform.SetParent(colliders, false);
                box.transform.localPosition = (a + c) * 0.5f + Vector3.up * 3f;
                box.size = edgeAlongX ? new Vector3(edgeLength + depth, 6f, depth) : new Vector3(depth, 6f, edgeLength + depth);

                if (piece == null) continue;
                int n = Mathf.Max(1, Mathf.CeilToInt(edgeLength / length));
                float yaw = edgeAlongX == longAlongX ? 0f : 90f;
                if (dir.x < -0.5f || dir.z < -0.5f) yaw += 180f;
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                for (int i = 0; i < n; i++)
                {
                    Vector3 slot = a + dir * ((i + 0.5f) * edgeLength / n);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(piece, trees);
                    go.transform.localRotation = rot * piece.transform.localRotation;
                    Vector3 fix = rot * new Vector3(-b.center.x, 0f, -b.center.z);
                    go.transform.localPosition = new Vector3(slot.x + fix.x, -0.3f - b.min.y, slot.z + fix.z);
                    pieces++;
                }
            }

            G.MarkStatic(root);
            return pieces;
        }

        // ------------------------------------------------------------------ helpers

        private static Vector3 Dir(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        }

        private static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static GameObject Spawn(GameObject prefab, Transform parent, Vector3 position, Vector3 facing)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.LookRotation(facing);
            return go;
        }

        private static bool TooClose(Vector3 p, List<Vector3> points, float minDistance)
        {
            float sq = minDistance * minDistance;
            foreach (Vector3 q in points)
            {
                if ((p - q).sqrMagnitude < sq) return true;
            }

            return false;
        }

        private static Bounds MeasurePrefab(GameObject prefab)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;
            bool any = false;
            var result = new Bounds(Vector3.zero, Vector3.zero);
            foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds mb = filter.sharedMesh.bounds;
                Matrix4x4 m = filter.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 p = m.MultiplyPoint3x4(new Vector3(
                        (c & 1) == 0 ? mb.min.x : mb.max.x,
                        (c & 2) == 0 ? mb.min.y : mb.max.y,
                        (c & 4) == 0 ? mb.min.z : mb.max.z));
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }

            Object.DestroyImmediate(go);
            return any ? result : new Bounds(Vector3.zero, Vector3.one);
        }

        // ------------------------------------------------------------------ layout

        private struct OrientedRect
        {
            public string Name;
            public Vector3 Center;
            public Quaternion Rot;
            public Vector2 Half;

            public float Distance(Vector3 p, out Vector3 nearest)
            {
                Vector3 local = Quaternion.Inverse(Rot) * (p - Center);
                float cx = Mathf.Clamp(local.x, -Half.x, Half.x);
                float cz = Mathf.Clamp(local.z, -Half.y, Half.y);
                nearest = Center + Rot * new Vector3(cx, 0f, cz);
                return new Vector2(local.x - cx, local.z - cz).magnitude;
            }
        }

        private sealed class Layout
        {
            public float PlazaRadius = 11f;
            public float StepsStartZ = 14f;
            public float StepsWidth = 6f;
            public float PorchFrontZ = 15.6f;
            public float PorchTopY = 0.8f;
            public Vector3 BalaiCenter = new Vector3(0f, 0f, 22f);
            public Vector2 BalaiHalf = new Vector2(8f, 7f);
            public Vector3 Spawn = new Vector3(-70f, 0f, 0f);
            public Vector2 TerrainMin, TerrainMax, WalkMin, WalkMax;
            public readonly List<OrientedRect> Roads = new List<OrientedRect>();

            // Balai details used by the decoration pass.
            public bool HasBalai;
            public float BodyFrontZ = 18.6f;
            public float BeamZ = 16.1f, BeamBottomY = 4.98f, BeamHeight = 0.5f, BeamDepth = 0.6f, BeamWidth = 15f;
            public float PlinthHalfX = 8f, PlinthBackZ = 28f;
            public readonly List<float> ColumnXs = new List<float>();

            /// <summary>Spots kept free of scattered vegetation: x, z, radius.</summary>
            public readonly List<Vector4> Reserved = new List<Vector4>();

            public static Layout Read()
            {
                Terrain terrain = G.Root("_Terrain").GetComponentInChildren<Terrain>();
                if (terrain == null || terrain.terrainData == null) return null;

                var layout = new Layout();
                Vector3 tp = terrain.transform.position;
                Vector3 ts = terrain.terrainData.size;
                layout.TerrainMin = new Vector2(tp.x, tp.z);
                layout.TerrainMax = new Vector2(tp.x + ts.x, tp.z + ts.z);
                layout.WalkMin = layout.TerrainMin;
                layout.WalkMax = layout.TerrainMax;

                Transform roads = G.Root("_Roads");
                Transform plazaFloor = roads.Find("GB_AlunAlun/Lantai_AlunAlun");
                if (plazaFloor != null) layout.PlazaRadius = plazaFloor.lossyScale.x * 0.5f;

                Transform jalan = roads.Find("GB_Jalan");
                if (jalan != null)
                {
                    foreach (Transform t in jalan)
                    {
                        layout.Roads.Add(new OrientedRect
                        {
                            Name = t.name,
                            Center = new Vector3(t.position.x, 0f, t.position.z),
                            Rot = Quaternion.Euler(0f, t.eulerAngles.y, 0f),
                            Half = new Vector2(t.lossyScale.x * 0.5f, t.lossyScale.z * 0.5f),
                        });
                    }
                }

                Transform buildings = G.Root("_Buildings");
                Transform balai = null;
                foreach (string n in new[] { "BalaiDesa_Sementara", "GB_BalaiDesa" })
                {
                    Transform t = buildings.Find(n);
                    if (t != null && t.gameObject.activeSelf) { balai = t; break; }
                }

                if (balai != null)
                {
                    Renderer[] renderers = balai.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        Bounds bb = renderers[0].bounds;
                        foreach (Renderer r in renderers) bb.Encapsulate(r.bounds);
                        layout.BalaiCenter = new Vector3(bb.center.x, 0f, bb.center.z);
                        layout.BalaiHalf = new Vector2(bb.extents.x, bb.extents.z);
                    }

                    Transform step = FindDeep(balai, "AnakTangga_1");
                    if (step != null)
                    {
                        layout.StepsWidth = step.lossyScale.x;
                        layout.StepsStartZ = step.position.z - step.lossyScale.z * 0.5f;
                        layout.PorchFrontZ = step.position.z + step.lossyScale.z * 0.5f;
                    }

                    Transform porch = FindDeep(balai, "Lantai_Teras");
                    if (porch != null)
                    {
                        layout.PorchTopY = porch.position.y + porch.lossyScale.y * 0.5f;
                        layout.PlinthHalfX = porch.lossyScale.x * 0.5f;
                        layout.PlinthBackZ = porch.position.z + porch.lossyScale.z * 0.5f;
                    }

                    layout.HasBalai = true;
                    Transform beam = FindDeep(balai, "Balok_Depan");
                    if (beam != null)
                    {
                        layout.BeamZ = beam.position.z;
                        layout.BeamHeight = beam.lossyScale.y;
                        layout.BeamBottomY = beam.position.y - beam.lossyScale.y * 0.5f;
                        layout.BeamDepth = beam.lossyScale.z;
                        layout.BeamWidth = beam.lossyScale.x;
                    }

                    Transform ceiling = FindDeep(balai, "Plafon_Teras");
                    if (ceiling != null) layout.BodyFrontZ = ceiling.position.z + ceiling.lossyScale.z * 0.5f;

                    foreach (Transform t in balai.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.StartsWith("Tiang_") && t.GetComponent<MeshFilter>() != null) layout.ColumnXs.Add(t.position.x);
                    }
                }

                Transform spawn = G.Root("_SpawnPoints").Find("Spawn_FromKebun");
                if (spawn != null) layout.Spawn = spawn.position;
                return layout;
            }

            public bool InsideBalai(Vector3 p, float margin)
            {
                return Mathf.Abs(p.x - BalaiCenter.x) < BalaiHalf.x + margin &&
                       Mathf.Abs(p.z - BalaiCenter.z) < BalaiHalf.y + margin;
            }

            /// <summary>True when p keeps clear of the plaza, roads, the Balai, the spawn and the boundary band.</summary>
            public bool IsClear(Vector3 p, float clearance, bool tall)
            {
                if (p.x < TerrainMin.x + 14f || p.x > TerrainMax.x - 14f) return false;
                if (p.z < TerrainMin.y + 14f || p.z > TerrainMax.y - 14f) return false;

                float plazaGap = new Vector2(p.x, p.z).magnitude - PlazaRadius;
                if (plazaGap < clearance) return false;
                if (tall && p.z < 0f && plazaGap < CameraShadowDepth) return false;

                foreach (OrientedRect road in Roads)
                {
                    float d = road.Distance(p, out Vector3 nearest);
                    if (d < clearance) return false;
                    if (tall && p.z < nearest.z && d < CameraShadowDepth) return false;
                }

                if (InsideBalai(p, clearance + 1f)) return false;
                foreach (Vector4 spot in Reserved)
                {
                    float rr = spot.z + clearance;
                    if ((p.x - spot.x) * (p.x - spot.x) + (p.z - spot.y) * (p.z - spot.y) < rr * rr) return false;
                }

                if (tall && p.z < BalaiCenter.z && InsideBalai(p, CameraShadowDepth) && Mathf.Abs(p.x - BalaiCenter.x) < BalaiHalf.x + 2f)
                    return false;
                return (p - Spawn).sqrMagnitude > 64f;
            }

            private static Transform FindDeep(Transform root, string name)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name) return t;
                }

                return null;
            }
        }

        // ------------------------------------------------------------------ prop prefabs

        /// <summary>Simple alun-alun props built from primitives in palette colours, saved as prefabs (B5/B6).</summary>
        private sealed class PropPrefabs
        {
            public readonly GameObject Bench, Lamp, Flagpole, Pot, Board, TrashBins, HangingLantern, FlowerBed;
            public readonly Material FlagRed, Paper;

            public PropPrefabs(G.Palette mat)
            {
                Material metal = G.Palette.Create("GB_BesiGelap", "4D4A48");
                Material terracotta = G.Palette.Create("GB_Terakota", "C46E4A");
                Material soil = G.Palette.Create("GB_TanahPot", "7A5238");
                Material flagRed = G.Palette.Create("GB_BenderaMerah", "E2554F");
                Material paper = G.Palette.Create("GB_KertasKrem", "EFE3C4");
                Material notice = G.Palette.Create("GB_KertasKuning", "F5CF5B");
                Material glow = G.Palette.Create("GB_LampuHangat", "FFD98A");
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.54f) * 1.5f);
                glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(glow);

                G.EnsureFolder(PropsFolder);

                Bench = Save("Bangku", t =>
                {
                    G.Box(t, "Dudukan", new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.1f, 0.5f), mat.Wood, false);
                    G.Box(t, "Sandaran", new Vector3(0f, 0.8f, -0.22f), new Vector3(1.6f, 0.45f, 0.08f), mat.Wood, false);
                    G.Box(t, "Kaki_Kiri", new Vector3(-0.65f, 0.2f, 0f), new Vector3(0.12f, 0.4f, 0.45f), metal, false);
                    G.Box(t, "Kaki_Kanan", new Vector3(0.65f, 0.2f, 0f), new Vector3(0.12f, 0.4f, 0.45f), metal, false);
                    var c = t.gameObject.AddComponent<BoxCollider>();
                    c.center = new Vector3(0f, 0.5f, -0.02f);
                    c.size = new Vector3(1.7f, 1f, 0.6f);
                });

                Lamp = Save("LampuTaman", t =>
                {
                    G.Cylinder(t, "Alas", Vector3.zero, 0.18f, 0.2f, metal, false);
                    G.Cylinder(t, "Tiang", Vector3.zero, 0.06f, 2.6f, metal, false);
                    G.Box(t, "Lentera", new Vector3(0f, 2.8f, 0f), new Vector3(0.32f, 0.4f, 0.32f), glow, false);
                    G.Box(t, "Atap_Lentera", new Vector3(0f, 3.05f, 0f), new Vector3(0.45f, 0.08f, 0.45f), metal, false);
                    var c = t.gameObject.AddComponent<CapsuleCollider>();
                    c.center = new Vector3(0f, 1.5f, 0f);
                    c.radius = 0.18f;
                    c.height = 3f;
                });

                Flagpole = Save("TiangBendera", t =>
                {
                    G.Cylinder(t, "Alas", Vector3.zero, 0.6f, 0.3f, mat.StoneDark, false);
                    G.Cylinder(t, "Tiang", new Vector3(0f, 0.3f, 0f), 0.06f, 7f, mat.Column, false);
                    G.Box(t, "Bendera_Merah", new Vector3(0.8f, 6.85f, 0f), new Vector3(1.5f, 0.5f, 0.03f), flagRed, false);
                    G.Box(t, "Bendera_Putih", new Vector3(0.8f, 6.35f, 0f), new Vector3(1.5f, 0.5f, 0.03f), mat.Wall, false);
                    var c = t.gameObject.AddComponent<CapsuleCollider>();
                    c.center = new Vector3(0f, 1.5f, 0f);
                    c.radius = 0.6f;
                    c.height = 3f;
                });

                var flowers = AssetDatabase.LoadAssetAtPath<GameObject>(NatureFolder + "Flowers.prefab");
                Pot = Save("PotBunga", t =>
                {
                    G.Cylinder(t, "Pot", Vector3.zero, 0.45f, 0.55f, terracotta, false);
                    G.Cylinder(t, "Tanah", new Vector3(0f, 0.5f, 0f), 0.4f, 0.06f, soil, false);
                    if (flowers != null)
                    {
                        var f = (GameObject)PrefabUtility.InstantiatePrefab(flowers, t);
                        f.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                        f.transform.localScale = flowers.transform.localScale * 0.8f;
                    }

                    var c = t.gameObject.AddComponent<CapsuleCollider>();
                    c.center = new Vector3(0f, 0.4f, 0f);
                    c.radius = 0.45f;
                    c.height = 1f;
                });

                Board = Save("PapanInformasi", t =>
                {
                    G.Cylinder(t, "Tiang_Kiri", new Vector3(-0.9f, 0f, 0f), 0.08f, 2.2f, mat.Wood, false);
                    G.Cylinder(t, "Tiang_Kanan", new Vector3(0.9f, 0f, 0f), 0.08f, 2.2f, mat.Wood, false);
                    G.Box(t, "Papan", new Vector3(0f, 1.6f, 0f), new Vector3(2f, 1.2f, 0.12f), mat.Wood, false);
                    G.Box(t, "Atap_Papan", new Vector3(0f, 2.3f, 0f), new Vector3(2.3f, 0.12f, 0.45f), mat.Roof, false);
                    G.Box(t, "Kertas_1", new Vector3(-0.45f, 1.7f, 0.07f), new Vector3(0.5f, 0.6f, 0.02f), paper, false);
                    G.Box(t, "Kertas_2", new Vector3(0.3f, 1.55f, 0.07f), new Vector3(0.6f, 0.45f, 0.02f), notice, false);
                    var c = t.gameObject.AddComponent<BoxCollider>();
                    c.center = new Vector3(0f, 1.1f, 0f);
                    c.size = new Vector3(2.2f, 2.2f, 0.4f);
                });

                Material binGreen = G.Palette.Create("GB_TongHijau", "3F9B4B");
                Material binYellow = G.Palette.Create("GB_TongKuning", "F4C430");
                FlagRed = flagRed;
                Paper = paper;

                TrashBins = Save("TempatSampah", t =>
                {
                    G.Cylinder(t, "Tong_Organik", new Vector3(-0.35f, 0f, 0f), 0.28f, 0.8f, binGreen, false);
                    G.Cylinder(t, "Tutup_Organik", new Vector3(-0.35f, 0.8f, 0f), 0.31f, 0.06f, metal, false);
                    G.Cylinder(t, "Tong_Anorganik", new Vector3(0.35f, 0f, 0f), 0.28f, 0.8f, binYellow, false);
                    G.Cylinder(t, "Tutup_Anorganik", new Vector3(0.35f, 0.8f, 0f), 0.31f, 0.06f, metal, false);
                    var c = t.gameObject.AddComponent<BoxCollider>();
                    c.center = new Vector3(0f, 0.45f, 0f);
                    c.size = new Vector3(1.3f, 0.9f, 0.6f);
                });

                // Pivot at the hook (top), so it can be placed directly under a beam. No collider: it hangs above head height.
                HangingLantern = Save("LenteraGantung", t =>
                {
                    G.Cylinder(t, "Rantai", new Vector3(0f, -0.4f, 0f), 0.02f, 0.4f, metal, false);
                    G.Box(t, "Atap_Lentera", new Vector3(0f, -0.42f, 0f), new Vector3(0.36f, 0.06f, 0.36f), metal, false);
                    G.Box(t, "Lentera", new Vector3(0f, -0.63f, 0f), new Vector3(0.28f, 0.36f, 0.28f), glow, false);
                });

                FlowerBed = Save("BakBunga", t =>
                {
                    G.Box(t, "Bak", new Vector3(0f, 0.225f, 0f), new Vector3(2.4f, 0.45f, 0.9f), mat.StoneDark, false);
                    G.Box(t, "Tanah", new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.06f, 0.7f), soil, false);
                    if (flowers != null)
                    {
                        foreach (float x in new[] { -0.7f, 0f, 0.7f })
                        {
                            var f = (GameObject)PrefabUtility.InstantiatePrefab(flowers, t);
                            f.transform.localPosition = new Vector3(x, 0.48f, 0f);
                        }
                    }

                    var c = t.gameObject.AddComponent<BoxCollider>();
                    c.center = new Vector3(0f, 0.25f, 0f);
                    c.size = new Vector3(2.4f, 0.5f, 0.9f);
                });
            }

            private static GameObject Save(string name, Action<Transform> build)
            {
                var root = new GameObject(name);
                build(root.transform);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PropsFolder}/{name}.prefab");
                Object.DestroyImmediate(root);
                return prefab;
            }
        }
    }
}
