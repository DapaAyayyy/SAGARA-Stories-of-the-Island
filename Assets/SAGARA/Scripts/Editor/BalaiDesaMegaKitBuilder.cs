using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using G = Sagara.EditorTools.BalaiDesaGreyboxBuilder;

namespace Sagara.EditorTools
{
    /// <summary>
    /// Assembles a temporary Balai Desa from the Quaternius Medieval Village MegaKit that is already in
    /// the project (white plaster walls, round-tile roof), on the same spot as the greybox.
    /// Conventions taken from Prefabs/Buildings/House_Coral.prefab: wall pieces are 2 m wide, their
    /// exterior faces local +Z, doors/windows sit 0.04 m in front of the wall, and the roof pivot sits
    /// on top of the walls with its ridge along local Z.
    /// Open with menu SAGARA > Balai Desa Sementara (MegaKit).
    /// </summary>
    public sealed class BalaiDesaMegaKitBuilder : EditorWindow
    {
        private const string KitFolder = "Assets/SAGARA/Art/Medieval Village MegaKit[Standard]/FBX/";
        private const string PrefabPath = "Assets/SAGARA/Prefabs/Village/BalaiDesa_Sementara.prefab";
        private const string RootName = "BalaiDesa_Sementara";

        [SerializeField] private float pieceScale = 1.5f;
        [SerializeField] private int widthPieces = 5;
        [SerializeField] private int depthPieces = 3;
        [SerializeField] private float porchDepth = 3f;
        [SerializeField] private float plazaDiameter = 22f;
        [SerializeField] private float gapPlazaToSteps = 3f;
        [SerializeField] private int stepCount = 4;
        [SerializeField] private float stepHeight = 0.2f;
        [SerializeField] private float stepDepth = 0.4f;
        [SerializeField] private bool usePaletteColours = true;
        [SerializeField] private bool rotateRoof;
        [SerializeField] private float roofHeightFactor = 1f;
        [SerializeField] private bool savePrefab = true;

        private Vector2 scroll;

        [MenuItem("SAGARA/Balai Desa Sementara (MegaKit)")]
        private static void Open()
        {
            GetWindow<BalaiDesaMegaKitBuilder>("Balai Desa Sementara").minSize = new Vector2(340, 400);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "Merakit Balai Desa dari Medieval Village MegaKit (sudah ada di project). " +
                "Greybox GB_BalaiDesa disembunyikan, alun-alun dan jalan tetap dipakai.", MessageType.None);

            EditorGUILayout.LabelField("Bangunan", EditorStyles.boldLabel);
            pieceScale = EditorGUILayout.Slider("Skala potongan MegaKit", pieceScale, 1f, 2f);
            widthPieces = EditorGUILayout.IntSlider("Jumlah dinding depan", widthPieces, 3, 9);
            if (widthPieces % 2 == 0) widthPieces++;
            depthPieces = EditorGUILayout.IntSlider("Jumlah dinding samping", depthPieces, 2, 6);
            porchDepth = EditorGUILayout.FloatField("Dalam teras", porchDepth);

            EditorGUILayout.LabelField("Posisi dan tangga", EditorStyles.boldLabel);
            plazaDiameter = EditorGUILayout.FloatField("Diameter alun-alun", plazaDiameter);
            gapPlazaToSteps = EditorGUILayout.FloatField("Jarak alun-alun ke tangga", gapPlazaToSteps);
            stepCount = EditorGUILayout.IntSlider("Jumlah anak tangga", stepCount, 1, 8);
            stepHeight = EditorGUILayout.FloatField("Tinggi anak tangga", stepHeight);
            stepDepth = EditorGUILayout.FloatField("Kedalaman anak tangga", stepDepth);

            EditorGUILayout.LabelField("Tampilan", EditorStyles.boldLabel);
            usePaletteColours = EditorGUILayout.Toggle("Warna palet (putih + merah)", usePaletteColours);
            rotateRoof = EditorGUILayout.Toggle("Putar atap 90°", rotateRoof);
            roofHeightFactor = EditorGUILayout.Slider(new GUIContent("Tinggi atap",
                "1 = curam seperti rumah adat, 0,5 = landai"), roofHeightFactor, 0.3f, 1f);
            savePrefab = EditorGUILayout.Toggle("Simpan sebagai prefab", savePrefab);

            float unit = 2f * pieceScale;
            EditorGUILayout.HelpBox(
                $"Perkiraan ukuran: {widthPieces * unit:0.#} × {depthPieces * unit + porchDepth:0.#} m " +
                $"(1 potongan = {unit:0.#} m), dinding ±{3f * pieceScale:0.#} m.\n" +
                "Kalau atap terlihat miring/salah arah, centang \"Putar atap 90°\" lalu rakit ulang.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Rakit Balai Desa Sementara", GUILayout.Height(32))) Build();
                if (GUILayout.Button("Tampilkan greybox lagi (sembunyikan versi MegaKit)")) ShowGreybox(true);
            }

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            if (!G.OpenOrCreateScene()) return;

            var kit = new Kit();
            if (!kit.Load()) return;

            var mat = new G.Palette();
            float s = pieceScale;
            float unit = kit.WallSize.x * s;
            float wallH = kit.WallSize.y * s;
            float bodyW = widthPieces * unit;
            float bodyD = depthPieces * unit;
            float r = plazaDiameter * 0.5f;
            float fh = stepCount * stepHeight;
            float stepsStartZ = r + gapPlazaToSteps;
            float porchFrontZ = stepsStartZ + stepCount * stepDepth;
            float bodyFrontZ = porchFrontZ + porchDepth;
            float bodyBackZ = bodyFrontZ + bodyD;
            float bodyCenterZ = (bodyFrontZ + bodyBackZ) * 0.5f;
            float wallTop = fh + wallH;
            float columnZ = porchFrontZ + 0.5f;

            Transform buildings = G.Root("_Buildings");
            ShowGreybox(false);
            Transform root = G.Fresh(buildings, RootName);
            root.gameObject.SetActive(true);

            // Plinth, porch floor and stairs.
            Transform baseGroup = Group(root, "Lantai_dan_Tangga");
            G.Box(baseGroup, "Lantai_Teras", new Vector3(0f, fh * 0.5f, (porchFrontZ + bodyBackZ + 0.5f) * 0.5f),
                new Vector3(bodyW + 1f, fh, bodyBackZ + 0.5f - porchFrontZ), mat.StoneDark, true);
            TileFloor(baseGroup, kit, mat, s, bodyW + 1f, porchFrontZ, bodyFrontZ, fh);
            BuildStairs(baseGroup, mat, fh, stepsStartZ, porchFrontZ, Mathf.Min(bodyW - 2f, unit + 3f));

            // Walls (4 sides). The middle front piece is the door.
            Transform walls = Group(root, "Dinding");
            BuildWallSide(walls, kit, mat, "Depan", new Vector3(0f, fh, bodyFrontZ), Vector3.back, Vector3.right,
                widthPieces, unit, s, frontSide: true);
            BuildWallSide(walls, kit, mat, "Belakang", new Vector3(0f, fh, bodyBackZ), Vector3.forward, Vector3.left,
                widthPieces, unit, s, frontSide: false);
            BuildWallSide(walls, kit, mat, "Kiri", new Vector3(-bodyW * 0.5f, fh, bodyCenterZ), Vector3.left, Vector3.back,
                depthPieces, unit, s, frontSide: false);
            BuildWallSide(walls, kit, mat, "Kanan", new Vector3(bodyW * 0.5f, fh, bodyCenterZ), Vector3.right, Vector3.forward,
                depthPieces, unit, s, frontSide: false);

            // Corner pilasters hide the seams where the wall pieces meet.
            foreach (float x in new[] { -bodyW * 0.5f, bodyW * 0.5f })
            foreach (float z in new[] { bodyFrontZ, bodyBackZ })
            {
                G.Box(walls, "Pilar_Sudut", new Vector3(x, fh + wallH * 0.5f, z), new Vector3(0.6f, wallH, 0.6f),
                    mat.Column, false);
            }

            var bodyCollider = new GameObject("Collider_Bangunan").AddComponent<BoxCollider>();
            bodyCollider.transform.SetParent(walls, false);
            bodyCollider.center = new Vector3(0f, fh + wallH * 0.5f, bodyCenterZ);
            bodyCollider.size = new Vector3(bodyW, wallH, bodyD);

            // Front columns + beam.
            Transform porch = Group(root, "Tiang_Teras");
            float columnH = wallH - 0.5f;
            float outer = bodyW * 0.5f - 0.4f;
            float inner = unit * 0.5f + 0.6f;
            var columnXs = new List<float> { inner, outer };
            if (outer - inner > 4.5f) columnXs.Add((inner + outer) * 0.5f);
            foreach (float x in columnXs)
            foreach (float side in new[] { -1f, 1f })
            {
                G.Cylinder(porch, side < 0 ? "Tiang_Kiri" : "Tiang_Kanan", new Vector3(side * x, fh, columnZ),
                    0.3f, columnH, mat.Column, true);
            }

            G.Box(porch, "Balok_Depan", new Vector3(0f, wallTop - 0.25f, columnZ), new Vector3(bodyW + 0.4f, 0.5f, 0.6f),
                mat.Wood, false);
            G.Box(porch, "Plafon_Teras", new Vector3(0f, wallTop - 0.05f, (columnZ + bodyFrontZ) * 0.5f),
                new Vector3(bodyW, 0.1f, bodyFrontZ - columnZ), mat.Wall, false);

            // Roof + white gable triangle underneath it.
            Transform roofGroup = Group(root, "Atap");
            float roofZ0 = columnZ - 0.3f;
            float roofLength = bodyBackZ - roofZ0;
            float roofCenterZ = (roofZ0 + bodyBackZ) * 0.5f;
            float roofHeight = BuildRoof(roofGroup, kit, mat, new Vector3(0f, wallTop, roofCenterZ), bodyW, roofLength);

            var gable = new GameObject("Segitiga_Atap");
            gable.transform.SetParent(roofGroup, false);
            gable.transform.localPosition = new Vector3(0f, wallTop, roofCenterZ);
            gable.transform.localScale = new Vector3(bodyW, Mathf.Max(roofHeight * 0.75f, 0.5f), roofLength - 0.1f);
            gable.AddComponent<MeshFilter>().sharedMesh = G.PrismMesh();
            gable.AddComponent<MeshRenderer>().sharedMaterial = mat.Wall;

            G.MarkStatic(root);

            // Move the door interaction point to the new door.
            Transform door = G.Root("_Interactables").Find("Interact_PintuBalaiDesa");
            if (door != null) door.localPosition = new Vector3(0f, fh, bodyFrontZ - 1.2f);

            if (savePrefab)
            {
                G.EnsureFolder("Assets/SAGARA/Prefabs/Village");
                PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);
            }

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = root.gameObject;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log($"[Balai Desa Sementara] Selesai: {bodyW:0.#} × {bodyD + porchDepth:0.#} m, dinding {wallH:0.##} m, " +
                      $"atap {kit.RoofLog}. Material FBX yang ditemukan: {string.Join(", ", kit.SeenMaterials)}");
        }

        private static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private void BuildWallSide(Transform parent, Kit kit, G.Palette mat, string label, Vector3 center,
            Vector3 outward, Vector3 tangent, int count, float unit, float s, bool frontSide)
        {
            Transform side = Group(parent, "Dinding_" + label);
            Quaternion rot = Quaternion.LookRotation(outward);
            int doorIndex = count / 2;
            for (int i = 0; i < count; i++)
            {
                Vector3 slot = center + tangent * ((i - (count - 1) * 0.5f) * unit);
                bool isDoor = frontSide && i == doorIndex;
                bool hasWindow = !isDoor && (frontSide || label == "Kiri" || label == "Kanan") && i % 2 == (frontSide ? 1 - doorIndex % 2 : 1);

                GameObject wallPrefab = isDoor ? kit.WallDoor : hasWindow ? kit.WallWindow : kit.WallPlain;
                Place(side, kit, mat, wallPrefab, slot, rot, s, 0f);
                if (isDoor) Place(side, kit, mat, kit.Door, slot, rot, s, 0.04f);
                else if (hasWindow) Place(side, kit, mat, kit.Window, slot, rot, s, 0.04f);
            }
        }

        /// <summary>Places a kit piece so its visual centre (X) and bottom (Y) match the slot, pivot depth kept.</summary>
        private void Place(Transform parent, Kit kit, G.Palette mat, GameObject prefab, Vector3 slot, Quaternion rot,
            float s, float outwardOffset)
        {
            Bounds b = kit.BoundsOf(prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localRotation = rot * prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale * s;
            Vector3 pivotFix = rot * new Vector3(-b.center.x * s, -b.min.y * s, 0f);
            go.transform.localPosition = slot + pivotFix + rot * Vector3.forward * (outwardOffset * s);
            Recolour(go, kit, mat);
        }

        private void TileFloor(Transform parent, Kit kit, G.Palette mat, float s, float width, float z0, float z1, float topY)
        {
            if (kit.Floor == null) return;
            Bounds b = kit.BoundsOf(kit.Floor);
            float tileX = Mathf.Max(b.size.x * s, 0.5f);
            float tileZ = Mathf.Max(b.size.z * s, 0.5f);
            int nx = Mathf.CeilToInt(width / tileX);
            int nz = Mathf.CeilToInt((z1 - z0) / tileZ);
            float sx = width / (nx * tileX);
            float sz = (z1 - z0) / (nz * tileZ);
            Transform group = Group(parent, "Ubin_Teras");
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(kit.Floor, group);
                go.transform.localScale = ScaleInPrefabFrame(kit.Floor, new Vector3(s * sx, s, s * sz));
                Vector3 center = new Vector3(-width * 0.5f + (ix + 0.5f) * tileX * sx, topY + 0.01f,
                    z0 + (iz + 0.5f) * tileZ * sz);
                go.transform.localPosition = center - new Vector3(b.center.x * s * sx, b.max.y * s, b.center.z * s * sz);
                if (usePaletteColours) SetAll(go, mat.Stone);
            }
        }

        private static void BuildStairs(Transform parent, G.Palette mat, float fh, float z0, float porchFrontZ, float width)
        {
            Transform stairs = Group(parent, "Tangga");
            int count = Mathf.Max(1, Mathf.RoundToInt((porchFrontZ - z0) / 0.4f));
            float depth = (porchFrontZ - z0) / count;
            for (int i = 0; i < count; i++)
            {
                float zStart = z0 + i * depth;
                float h = fh * (i + 1) / count;
                G.Box(stairs, $"AnakTangga_{i + 1}", new Vector3(0f, h * 0.5f, (zStart + porchFrontZ) * 0.5f),
                    new Vector3(width, h, porchFrontZ - zStart), mat.Stone, false);
            }

            float run = porchFrontZ - z0;
            float angle = Mathf.Atan2(fh, run) * Mathf.Rad2Deg;
            var ramp = new GameObject("Collider_RampTangga");
            ramp.transform.SetParent(stairs, false);
            ramp.transform.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            ramp.transform.localPosition = new Vector3(0f, fh * 0.5f, z0 + run * 0.5f)
                                           + ramp.transform.localRotation * (Vector3.down * 0.1f);
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.2f, Mathf.Sqrt(run * run + fh * fh));
        }

        /// <summary>Picks the round-tile roof whose proportions fit best and stretches it over the footprint.</summary>
        private float BuildRoof(Transform parent, Kit kit, G.Palette mat, Vector3 center, float spanX, float lengthZ)
        {
            // Ridge runs along local Z (as in House_Coral). With "rotate" the ridge runs along world X instead.
            float wantSpan = rotateRoof ? lengthZ : spanX;
            float wantLength = rotateRoof ? spanX : lengthZ;
            float want = wantSpan / wantLength;

            GameObject best = null;
            Vector2 bestNamed = Vector2.one;
            float bestScore = float.MaxValue;
            foreach (KeyValuePair<GameObject, Vector2> roof in kit.Roofs)
            {
                float score = Mathf.Abs(Mathf.Log(want / (roof.Value.x / roof.Value.y)));
                if (score < bestScore) { bestScore = score; best = roof.Key; bestNamed = roof.Value; }
            }

            if (best == null)
            {
                Debug.LogWarning("[Balai Desa Sementara] Tidak ada Roof_RoundTiles_* di MegaKit. Atap dibuat dari kotak.");
                G.Box(parent, "Atap_Pengganti", center + Vector3.up * 1.5f, new Vector3(spanX + 1f, 3f, lengthZ + 1f), mat.Roof, false);
                return 3f;
            }

            Bounds b = kit.BoundsOf(best);
            float sx = wantSpan / bestNamed.x;
            float sz = wantLength / bestNamed.y;
            var scale = new Vector3(sx, sx * roofHeightFactor, sz);
            Quaternion rot = Quaternion.Euler(0f, rotateRoof ? 90f : 0f, 0f);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(best, parent);
            go.name = "Atap_" + best.name;
            go.transform.localRotation = rot * best.transform.localRotation;
            go.transform.localScale = ScaleInPrefabFrame(best, scale);
            Vector3 centreFix = rot * Vector3.Scale(new Vector3(-b.center.x, 0f, -b.center.z), scale);
            go.transform.localPosition = center + centreFix;
            Recolour(go, kit, mat);

            kit.RoofLog = $"{best.name} (skala {sx:0.##} × {sz:0.##}{(rotateRoof ? ", diputar 90°" : "")})";
            return b.max.y * sx * roofHeightFactor;
        }

        /// <summary>
        /// FBX files from Blender often import with a root scale (e.g. 100) and rotation (e.g. -90° X).
        /// Converts a scale measured in the prefab's own frame into a localScale that keeps that root scale.
        /// </summary>
        private static Vector3 ScaleInPrefabFrame(GameObject prefab, Vector3 frameScale)
        {
            Quaternion q = prefab.transform.localRotation;
            var perAxis = new Vector3();
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = Vector3.zero;
                axis[i] = 1f;
                Vector3 d = q * axis;
                perAxis[i] = Mathf.Abs(d.x) * frameScale.x + Mathf.Abs(d.y) * frameScale.y + Mathf.Abs(d.z) * frameScale.z;
            }

            return Vector3.Scale(prefab.transform.localScale, perAxis);
        }

        // ------------------------------------------------------------------ materials

        private void Recolour(GameObject go, Kit kit, G.Palette mat)
        {
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    string name = slots[i].name.ToLowerInvariant();
                    kit.SeenMaterials.Add(slots[i].name);
                    if (!usePaletteColours) continue;

                    Material replacement =
                        name.Contains("plaster") || name.Contains("white") ? mat.Wall :
                        name.Contains("tile") || name.Contains("roof") ? mat.Roof :
                        name.Contains("glass") ? mat.Glass :
                        name.Contains("wood") || name.Contains("door") || name.Contains("window") ? mat.Wood :
                        name.Contains("brick") || name.Contains("stone") || name.Contains("rock") ? mat.StoneDark :
                        name.Contains("metal") || name.Contains("iron") ? mat.RoofDark :
                        null;
                    if (replacement != null) slots[i] = replacement;
                }

                renderer.sharedMaterials = slots;
            }
        }

        private static void SetAll(GameObject go, Material material)
        {
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                var slots = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
        }

        private static void ShowGreybox(bool showGreybox)
        {
            Transform buildings = G.Root("_Buildings");
            Transform greybox = buildings.Find("GB_BalaiDesa");
            Transform kitVersion = buildings.Find(RootName);
            if (greybox != null) greybox.gameObject.SetActive(showGreybox);
            if (kitVersion != null) kitVersion.gameObject.SetActive(!showGreybox);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // ------------------------------------------------------------------ kit loading

        private sealed class Kit
        {
            public GameObject WallPlain, WallWindow, WallDoor, Window, Door, Floor;
            public readonly Dictionary<GameObject, Vector2> Roofs = new Dictionary<GameObject, Vector2>();
            public readonly SortedSet<string> SeenMaterials = new SortedSet<string>();
            public string RoofLog = "-";
            public Vector3 WallSize;
            private readonly Dictionary<GameObject, Bounds> bounds = new Dictionary<GameObject, Bounds>();

            public bool Load()
            {
                WallPlain = Get("Wall_Plaster_Straight");
                WallWindow = Get("Wall_Plaster_Window_Wide_Flat");
                WallDoor = Get("Wall_Plaster_Door_Flat");
                Window = Get("Window_Wide_Flat1");
                Door = Get("Door_1_Flat");
                Floor = Get("Floor_Brick");

                if (WallPlain == null || WallWindow == null || WallDoor == null)
                {
                    EditorUtility.DisplayDialog("Balai Desa Sementara",
                        "Potongan dinding MegaKit tidak ditemukan di\n" + KitFolder, "OK");
                    return false;
                }

                foreach (string guid in AssetDatabase.FindAssets("Roof_RoundTiles_ t:GameObject", new[] { KitFolder.TrimEnd('/') }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Match m = Regex.Match(path, @"Roof_RoundTiles_(\d+)x(\d+)\.fbx$");
                    if (!m.Success) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Bounds b = BoundsOf(prefab);
                    float a = float.Parse(m.Groups[1].Value);
                    float c = float.Parse(m.Groups[2].Value);
                    // Match the two numbers in the name to the measured X / Z size.
                    bool aIsX = Mathf.Abs(b.size.x - a) + Mathf.Abs(b.size.z - c) <= Mathf.Abs(b.size.x - c) + Mathf.Abs(b.size.z - a);
                    Roofs[prefab] = aIsX ? new Vector2(a, c) : new Vector2(c, a);
                }

                Bounds wall = BoundsOf(WallPlain);
                Debug.Log($"[Balai Desa Sementara] Ukuran 1 dinding MegaKit: {wall.size.x:0.##} × {wall.size.y:0.##} m, " +
                          $"skala bawaan FBX {WallPlain.transform.localScale}, rotasi {WallPlain.transform.localEulerAngles}");
                if (wall.size.x < 0.5f || wall.size.y < 0.5f)
                {
                    EditorUtility.DisplayDialog("Balai Desa Sementara",
                        $"Ukuran dinding MegaKit terbaca {wall.size.x:0.###} × {wall.size.y:0.###} m (terlalu kecil).\n" +
                        "Kirim pesan Console ke Claude supaya script-nya disesuaikan.", "OK");
                    return false;
                }

                WallSize = wall.size;
                return true;
            }

            private static GameObject Get(string name)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + name + ".fbx");
            }

            /// <summary>Bounds of the prefab at the origin with identity rotation and unit scale.</summary>
            public Bounds BoundsOf(GameObject prefab)
            {
                if (bounds.TryGetValue(prefab, out Bounds cached)) return cached;
                // Keep the prefab's own root rotation/scale: that is the frame every placement starts from.
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = Vector3.zero;
                // Built from the mesh data itself so it does not depend on renderer bounds being refreshed.
                bool any = false;
                var result = new Bounds(Vector3.zero, Vector3.zero);
                foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    Bounds mb = filter.sharedMesh.bounds;
                    Matrix4x4 m = filter.transform.localToWorldMatrix;
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = new Vector3(
                            (c & 1) == 0 ? mb.min.x : mb.max.x,
                            (c & 2) == 0 ? mb.min.y : mb.max.y,
                            (c & 4) == 0 ? mb.min.z : mb.max.z);
                        Vector3 p = m.MultiplyPoint3x4(corner);
                        if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                        else result.Encapsulate(p);
                    }
                }

                Object.DestroyImmediate(go);
                if (!any) result = new Bounds(Vector3.zero, Vector3.one);
                bounds[prefab] = result;
                return result;
            }
        }
    }
}
