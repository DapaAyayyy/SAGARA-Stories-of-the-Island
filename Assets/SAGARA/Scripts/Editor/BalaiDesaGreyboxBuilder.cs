using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sagara.EditorTools
{
    /// <summary>
    /// Builds the Balai Desa + alun-alun greybox in MainVillage.unity (checklist B3).
    /// Open with menu SAGARA > Greybox Balai Desa. Rebuilding only replaces the GB_* objects;
    /// interaction points, spawn points, the test player and the camera are kept and moved.
    /// Coordinates: (0, 0, 0) = centre of the alun-alun, +Z = north, camera looks north.
    /// </summary>
    public sealed class BalaiDesaGreyboxBuilder : EditorWindow
    {
        private const string ScenePath = "Assets/SAGARA/Scenes/MainVillage.unity";
        private const string MaterialFolder = "Assets/SAGARA/Art/Materials/Greybox";
        private const string MeshFolder = "Assets/SAGARA/Art/Meshes/Greybox";
        private const string TerrainFolder = "Assets/SAGARA/Art/Terrain";

        // Player values copied from RumahDanKebun.unity (CharacterController + PlayerCameraFollow).
        private const float PlayerHeight = 2.25f;
        private const float PlayerRadius = 0.375f;
        private const float PlayerStepOffset = 0.3f;

        // Dimensions in metres.
        [SerializeField] private float plazaDiameter = 22f;
        [SerializeField] private float gapPlazaToSteps = 3f;
        [SerializeField] private float balaiWidth = 16f;
        [SerializeField] private float balaiDepth = 12f;
        [SerializeField] private float porchDepth = 3f;
        [SerializeField] private int stepCount = 4;
        [SerializeField] private float stepHeight = 0.2f;
        [SerializeField] private float stepDepth = 0.4f;
        [SerializeField] private float wallHeight = 4.2f;
        [SerializeField] private float roofRise = 3.6f;
        [SerializeField] private float roofOverhang = 0.8f;
        [SerializeField] private float doorWidth = 2.4f;
        [SerializeField] private float doorHeight = 3.8f;
        [SerializeField] private float roadWidth = 4.5f;

        private Vector2 scroll;

        [MenuItem("SAGARA/Greybox Balai Desa")]
        private static void Open()
        {
            GetWindow<BalaiDesaGreyboxBuilder>("Greybox Balai Desa").minSize = new Vector2(340, 420);
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Disarankan (pas kamera)")) ApplyRecommendedPreset();
                if (GUILayout.Button("Dokumen (20 × 15 m)")) ApplyDocumentPreset();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Alun-alun dan jalan", EditorStyles.boldLabel);
            plazaDiameter = EditorGUILayout.FloatField("Diameter alun-alun", plazaDiameter);
            gapPlazaToSteps = EditorGUILayout.FloatField("Jarak alun-alun ke tangga", gapPlazaToSteps);
            roadWidth = EditorGUILayout.FloatField("Lebar jalan", roadWidth);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Balai Desa", EditorStyles.boldLabel);
            balaiWidth = EditorGUILayout.FloatField("Lebar (X)", balaiWidth);
            balaiDepth = EditorGUILayout.FloatField("Dalam total (Z, termasuk teras)", balaiDepth);
            porchDepth = EditorGUILayout.FloatField("Dalam teras", porchDepth);
            wallHeight = EditorGUILayout.FloatField("Tinggi dinding", wallHeight);
            roofRise = EditorGUILayout.FloatField("Tinggi atap", roofRise);
            roofOverhang = EditorGUILayout.FloatField("Atap menjorok", roofOverhang);
            doorWidth = EditorGUILayout.FloatField("Lebar pintu", doorWidth);
            doorHeight = EditorGUILayout.FloatField("Tinggi pintu", doorHeight);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tangga teras", EditorStyles.boldLabel);
            stepCount = EditorGUILayout.IntSlider("Jumlah anak tangga", stepCount, 1, 8);
            stepHeight = EditorGUILayout.FloatField("Tinggi anak tangga", stepHeight);
            stepDepth = EditorGUILayout.FloatField("Kedalaman anak tangga", stepDepth);

            EditorGUILayout.Space();
            DrawSummary();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Bangun / Perbarui Greybox", GUILayout.Height(32))) Build();
            }

            EditorGUILayout.HelpBox(
                "Membuka (atau membuat) " + ScenePath + ". Hanya objek GB_* yang dibangun ulang. " +
                "Titik interaksi, spawn, kamera, dan Player_Test dipertahankan.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void ApplyRecommendedPreset()
        {
            plazaDiameter = 22f; gapPlazaToSteps = 3f; roadWidth = 4.5f;
            balaiWidth = 16f; balaiDepth = 12f; porchDepth = 3f;
            wallHeight = 4.2f; roofRise = 3.6f; roofOverhang = 0.8f;
            doorWidth = 2.4f; doorHeight = 3.8f;
            stepCount = 4; stepHeight = 0.2f; stepDepth = 0.4f;
        }

        private void ApplyDocumentPreset()
        {
            plazaDiameter = 28f; gapPlazaToSteps = 4f; roadWidth = 4.5f;
            balaiWidth = 20f; balaiDepth = 15f; porchDepth = 3.5f;
            wallHeight = 4.5f; roofRise = 4.2f; roofOverhang = 1f;
            doorWidth = 2.4f; doorHeight = 3.8f;
            stepCount = 4; stepHeight = 0.2f; stepDepth = 0.4f;
        }

        private float FloorHeight => stepCount * stepHeight;
        private float TotalHeight => FloorHeight + wallHeight + 0.5f + roofRise + 0.3f;

        private void DrawSummary()
        {
            float rampAngle = Mathf.Atan2(FloorHeight, stepCount * stepDepth) * Mathf.Rad2Deg;
            EditorGUILayout.HelpBox(
                $"Tinggi lantai teras: {FloorHeight:0.00} m\n" +
                $"Tinggi total bangunan: {TotalHeight:0.0} m (≈ {TotalHeight / PlayerHeight:0.0}× Player)\n" +
                $"Pintu: {doorHeight / PlayerHeight:0.00}× tinggi Player ({PlayerHeight} m)\n" +
                $"Kemiringan ramp tangga: {rampAngle:0}° (batas Player 45°)\n" +
                "Layar kamera (zoom 4,2–9): lebar ±15–32 m", MessageType.Info);

            if (stepHeight > PlayerStepOffset)
                EditorGUILayout.HelpBox("Anak tangga lebih tinggi dari Step Offset Player (0,3 m).", MessageType.Warning);
            if (doorHeight > wallHeight - 0.2f)
                EditorGUILayout.HelpBox("Pintu lebih tinggi dari dinding.", MessageType.Warning);
            if (porchDepth > balaiDepth - 4f)
                EditorGUILayout.HelpBox("Teras terlalu dalam: ruang bangunan kurang dari 4 m.", MessageType.Warning);
            if (doorWidth + 2f > balaiWidth)
                EditorGUILayout.HelpBox("Pintu terlalu lebar untuk bangunan.", MessageType.Warning);
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            if (!OpenOrCreateScene()) return;

            var mat = new Palette();
            float r = plazaDiameter * 0.5f;
            float fh = FloorHeight;
            float stepsStartZ = r + gapPlazaToSteps;
            float porchFrontZ = stepsStartZ + stepCount * stepDepth;
            float bodyFrontZ = porchFrontZ + porchDepth;
            float bodyBackZ = porchFrontZ + balaiDepth;

            Terrain terrain = EnsureTerrain();
            EnsureLighting();

            // Buildings ---------------------------------------------------------
            Transform balai = Fresh(Root("_Buildings"), "GB_BalaiDesa");
            BuildBalai(balai, mat, fh, stepsStartZ, porchFrontZ, bodyFrontZ, bodyBackZ);

            // Plaza and roads ---------------------------------------------------
            Transform roads = Root("_Roads");
            Transform plaza = Fresh(roads, "GB_AlunAlun");
            Cylinder(plaza, "Lantai_AlunAlun", new Vector3(0f, -0.06f, 0f), r, 0.12f, mat.Stone, false);
            Cylinder(plaza, "Lingkar_Tengah", new Vector3(0f, -0.05f, 0f), 2.5f, 0.12f, mat.StoneDark, false);
            Cylinder(plaza, "Lingkar_Luar", new Vector3(0f, -0.06f, 0f), r + 0.4f, 0.11f, mat.StoneDark, false);

            Transform jalan = Fresh(roads, "GB_Jalan");
            float stepsWidth = StepsWidth();
            Box(jalan, "Jalan_KeTangga", new Vector3(0f, 0.02f, (r - 0.5f + stepsStartZ) * 0.5f),
                new Vector3(stepsWidth, 0.04f, stepsStartZ - (r - 0.5f)), mat.Stone, false);

            const float westEnd = 78f;
            Box(jalan, "Jalan_Barat_KeKebun", new Vector3(-(r - 1f + westEnd) * 0.5f, 0.02f, 0f),
                new Vector3(westEnd - (r - 1f), 0.04f, roadWidth), mat.Road, false);

            Vector3 southEast = new Vector3(1f, 0f, -1f).normalized;
            const float southEastEnd = 95f;
            Box(jalan, "Jalan_Tenggara_KeDermaga", southEast * ((r - 1f + southEastEnd) * 0.5f) + Vector3.up * 0.02f,
                new Vector3(roadWidth, 0.04f, southEastEnd - (r - 1f)), mat.Road, false,
                Quaternion.LookRotation(southEast));

            float northX = balaiWidth * 0.5f + 1.5f + roadWidth * 0.5f;
            float northEnd = bodyBackZ + 25f;
            Box(jalan, "Jalan_Utara_KeRumahWarga", new Vector3(northX, 0.02f, northEnd * 0.5f),
                new Vector3(roadWidth, 0.04f, northEnd), mat.Road, false);

            // Props -------------------------------------------------------------
            Transform props = Root("_Props");
            Transform board = Fresh(props, "GB_PapanInformasi");
            Vector3 boardDir = new Vector3(-1f, 0f, 1f).normalized;
            Vector3 boardPos = boardDir * Mathf.Max(r - 2.5f, 2f);
            board.localPosition = boardPos;
            board.localRotation = Quaternion.LookRotation(-boardDir);
            Cylinder(board, "Tiang_Kiri", new Vector3(-0.9f, 0f, 0f), 0.08f, 2.2f, mat.Wood, false);
            Cylinder(board, "Tiang_Kanan", new Vector3(0.9f, 0f, 0f), 0.08f, 2.2f, mat.Wood, false);
            Box(board, "Papan", new Vector3(0f, 1.6f, 0f), new Vector3(2f, 1.2f, 0.12f), mat.Wood, false);
            var boardCollider = board.gameObject.AddComponent<BoxCollider>();
            boardCollider.center = new Vector3(0f, 1.1f, 0f);
            boardCollider.size = new Vector3(2.2f, 2.2f, 0.4f);

            // Points (kept between rebuilds, only moved) ------------------------
            Transform interactables = Root("_Interactables");
            Marker(interactables, "Interact_PintuBalaiDesa", new Vector3(0f, fh, bodyFrontZ - 1.2f), Vector3.forward, 1);
            Marker(interactables, "Interact_PapanInformasi", boardPos - boardDir * 1.3f, boardDir, 1);

            Marker(Root("_SpawnPoints"), "Spawn_FromKebun", new Vector3(-(westEnd - 8f), 0f, 0f), Vector3.right, 3);

            Transform toKebun = Marker(Root("_Transitions"), "Trigger_ToKebun",
                new Vector3(-westEnd, 1.5f, 0f), Vector3.left, 6);
            var trigger = toKebun.GetComponent<BoxCollider>();
            if (trigger == null) trigger = toKebun.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(roadWidth + 1f, 3f, 2f);

            foreach (string group in new[] { "_Vegetation", "_LockedAreas" }) Root(group);

            MarkStatic(balai);
            MarkStatic(plaza);
            MarkStatic(jalan);
            MarkStatic(board);

            // Test player + camera ----------------------------------------------
            CharacterController player = EnsureTestPlayer(new Vector3(0f, 0f, -r * 0.5f), mat.PlayerColor);
            EnsureCamera(player, terrain);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = balai.gameObject;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

            Debug.Log($"[Greybox Balai Desa] Selesai. Balai {balaiWidth} × {balaiDepth} m, tinggi {TotalHeight:0.0} m, " +
                      $"alun-alun Ø {plazaDiameter} m. Tangga mulai z = {stepsStartZ:0.0}, pintu di z = {bodyFrontZ:0.0}.");
        }

        private float StepsWidth() => Mathf.Min(balaiWidth - 2f, doorWidth + 3.6f);

        private void BuildBalai(Transform root, Palette mat, float fh, float stepsStartZ,
            float porchFrontZ, float bodyFrontZ, float bodyBackZ)
        {
            float halfW = balaiWidth * 0.5f;
            float bodyWidth = balaiWidth - 1f;
            float bodyDepth = bodyBackZ - bodyFrontZ;
            float stepsWidth = StepsWidth();
            float stepsDepth = stepCount * stepDepth;

            // Plinth (teras + lantai bangunan). Front face is fh high, so it can only be climbed via the stairs.
            Box(root, "Lantai_Teras", new Vector3(0f, fh * 0.5f, (porchFrontZ + bodyBackZ) * 0.5f),
                new Vector3(balaiWidth, fh, balaiDepth), mat.StoneDark, true);

            // Stairs: visual steps without colliders + one sloped ramp collider.
            Transform stairs = new GameObject("Tangga").transform;
            stairs.SetParent(root, false);
            for (int i = 0; i < stepCount; i++)
            {
                float z0 = stepsStartZ + i * stepDepth;
                float h = (i + 1) * stepHeight;
                Box(stairs, $"AnakTangga_{i + 1}", new Vector3(0f, h * 0.5f, (z0 + porchFrontZ) * 0.5f),
                    new Vector3(stepsWidth, h, porchFrontZ - z0), mat.Stone, false);
            }

            float rampLength = Mathf.Sqrt(stepsDepth * stepsDepth + fh * fh);
            float rampAngle = Mathf.Atan2(fh, stepsDepth) * Mathf.Rad2Deg;
            var ramp = new GameObject("Collider_RampTangga");
            ramp.transform.SetParent(stairs, false);
            ramp.transform.localRotation = Quaternion.Euler(-rampAngle, 0f, 0f);
            ramp.transform.localPosition = new Vector3(0f, fh * 0.5f, stepsStartZ + stepsDepth * 0.5f)
                                           + ramp.transform.localRotation * (Vector3.down * 0.1f);
            var rampCollider = ramp.AddComponent<BoxCollider>();
            rampCollider.size = new Vector3(stepsWidth, 0.2f, rampLength);

            // Body.
            Box(root, "Dinding", new Vector3(0f, fh + wallHeight * 0.5f, (bodyFrontZ + bodyBackZ) * 0.5f),
                new Vector3(bodyWidth, wallHeight, bodyDepth), mat.Wall, true);
            Box(root, "Pintu", new Vector3(0f, fh + doorHeight * 0.5f, bodyFrontZ - 0.06f),
                new Vector3(doorWidth, doorHeight, 0.15f), mat.Wood, false);

            float windowInner = doorWidth * 0.5f + 1.9f;
            float windowOuter = bodyWidth * 0.5f - 1.4f;
            var windowXs = new List<float> { windowInner };
            if (windowOuter - windowInner >= 2f) windowXs.Add(windowOuter);
            foreach (float x in windowXs)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Box(root, side < 0 ? $"Jendela_Kiri_{x:0}" : $"Jendela_Kanan_{x:0}",
                        new Vector3(side * x, fh + 1.9f, bodyFrontZ - 0.06f), new Vector3(1.4f, 1.8f, 0.15f),
                        mat.Glass, false);
                }
            }

            // Front columns, beam, gable (white triangle) and roof.
            float columnZ = porchFrontZ + 0.5f;
            float outer = halfW - 0.5f;
            float inner = doorWidth * 0.5f + 0.9f;
            var columnXs = new List<float> { inner, outer };
            if (outer - inner > 4.5f) columnXs.Add((inner + outer) * 0.5f);
            foreach (float x in columnXs)
            {
                Cylinder(root, $"Tiang_Kiri_{x:0.0}", new Vector3(-x, fh, columnZ), 0.3f, wallHeight, mat.Column, true);
                Cylinder(root, $"Tiang_Kanan_{x:0.0}", new Vector3(x, fh, columnZ), 0.3f, wallHeight, mat.Column, true);
            }

            float eaveY = fh + wallHeight + 0.5f;
            Box(root, "Balok_Depan", new Vector3(0f, fh + wallHeight + 0.25f, columnZ),
                new Vector3(balaiWidth, 0.5f, 0.6f), mat.Wood, false);
            Box(root, "Balok_Samping_Kiri", new Vector3(-halfW + 0.3f, fh + wallHeight + 0.25f, (columnZ + bodyFrontZ) * 0.5f),
                new Vector3(0.6f, 0.5f, bodyFrontZ - columnZ), mat.Wood, false);
            Box(root, "Balok_Samping_Kanan", new Vector3(halfW - 0.3f, fh + wallHeight + 0.25f, (columnZ + bodyFrontZ) * 0.5f),
                new Vector3(0.6f, 0.5f, bodyFrontZ - columnZ), mat.Wood, false);

            float roofZ0 = columnZ - 0.3f;
            float roofZ1 = bodyBackZ;
            float roofCenterZ = (roofZ0 + roofZ1) * 0.5f;
            float roofLength = roofZ1 - roofZ0;
            Box(root, "Plafon_Teras", new Vector3(0f, fh + wallHeight + 0.45f, (roofZ0 + bodyFrontZ) * 0.5f),
                new Vector3(balaiWidth, 0.1f, bodyFrontZ - roofZ0), mat.Wall, false);
            Box(root, "Dinding_Atas", new Vector3(0f, fh + wallHeight + 0.25f, (bodyFrontZ + bodyBackZ) * 0.5f),
                new Vector3(bodyWidth, 0.5f, bodyDepth), mat.Wall, false);

            var gable = new GameObject("Segitiga_Atap");
            gable.transform.SetParent(root, false);
            gable.transform.localPosition = new Vector3(0f, eaveY, roofCenterZ);
            gable.transform.localScale = new Vector3(balaiWidth, roofRise, roofLength);
            gable.AddComponent<MeshFilter>().sharedMesh = PrismMesh();
            gable.AddComponent<MeshRenderer>().sharedMaterial = mat.Wall;

            float slope = Mathf.Atan2(roofRise, halfW);
            float span = halfW + roofOverhang;
            float slabLength = span / Mathf.Cos(slope);
            const float thickness = 0.3f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 normal = new Vector3(side * Mathf.Sin(slope), Mathf.Cos(slope), 0f);
                Vector3 center = new Vector3(side * span * 0.5f, eaveY + roofRise - span * 0.5f * Mathf.Tan(slope), roofCenterZ)
                                 + normal * (thickness * 0.5f);
                Box(root, side < 0 ? "Atap_Kiri" : "Atap_Kanan", center,
                    new Vector3(slabLength, thickness, roofLength + roofOverhang * 2f), mat.Roof, false,
                    Quaternion.Euler(0f, 0f, -side * slope * Mathf.Rad2Deg));
            }

            Box(root, "Bubungan", new Vector3(0f, eaveY + roofRise + thickness * 0.6f, roofCenterZ),
                new Vector3(0.5f, 0.35f, roofLength + roofOverhang * 2f + 0.1f), mat.RoofDark, false);

        }

        // ------------------------------------------------------------------ scene setup

        internal static bool OpenOrCreateScene()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            return true;
        }

        private static Terrain EnsureTerrain()
        {
            Transform group = Root("_Terrain");
            Terrain terrain = group.GetComponentInChildren<Terrain>();
            if (terrain != null) return terrain;

            EnsureFolder(TerrainFolder);
            string dataPath = TerrainFolder + "/MainVillage_TerrainData.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (data == null)
            {
                data = new TerrainData { heightmapResolution = 33 };
                data.size = new Vector3(250f, 30f, 220f);
                data.terrainLayers = new[] { GrassLayer() };
                AssetDatabase.CreateAsset(data, dataPath);
            }

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain_Desa";
            go.transform.SetParent(group, false);
            go.transform.position = new Vector3(-110f, 0f, -120f);
            return go.GetComponent<Terrain>();
        }

        private static TerrainLayer GrassLayer()
        {
            string texturePath = TerrainFolder + "/GB_Rumput.png";
            string layerPath = TerrainFolder + "/GB_Rumput.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer != null) return layer;

            if (!File.Exists(texturePath))
            {
                var texture = new Texture2D(4, 4);
                var pixels = new Color[16];
                ColorUtility.TryParseHtmlString("#8FC46E", out Color grass);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = grass;
                texture.SetPixels(pixels);
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
            }

            layer = new TerrainLayer
            {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath),
                tileSize = new Vector2(10f, 10f),
            };
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        private static void EnsureLighting()
        {
            Transform group = Root("_Lighting");
            if (group.GetComponentInChildren<Light>() != null) return;

            var sun = new GameObject("Matahari");
            sun.transform.SetParent(group, false);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
        }

        private static CharacterController EnsureTestPlayer(Vector3 startPosition, Material color)
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "Player_Test") return go.GetComponent<CharacterController>();
            }

            var player = new GameObject("Player_Test") { tag = "Player" };
            player.transform.SetPositionAndRotation(startPosition, Quaternion.identity);
            var controller = player.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = PlayerRadius;
            controller.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            controller.stepOffset = PlayerStepOffset;
            controller.slopeLimit = 45f;
            player.AddComponent<PlayerMovement>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Badan";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = controller.center;
            body.transform.localScale = new Vector3(PlayerRadius * 2f, PlayerHeight * 0.5f, PlayerRadius * 2f);
            body.GetComponent<Renderer>().sharedMaterial = color;
            DestroyImmediate(body.GetComponent<Collider>());

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Arah_Depan";
            nose.transform.SetParent(player.transform, false);
            nose.transform.localPosition = new Vector3(0f, PlayerHeight * 0.75f, PlayerRadius);
            nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.3f);
            DestroyImmediate(nose.GetComponent<Collider>());

            return controller;
        }

        private static void EnsureCamera(CharacterController player, Terrain terrain)
        {
            var cameraOffset = new Vector3(0f, 12f, -20f);
            Transform group = Root("_Camera");
            Camera view = group.GetComponentInChildren<Camera>();
            if (view == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                go.transform.SetParent(group, false);
                view = go.AddComponent<Camera>();
                view.orthographic = true;
                view.orthographicSize = 4.2f;
                view.nearClipPlane = 0.3f;
                view.farClipPlane = 1000f;
                go.AddComponent<AudioListener>();
                go.transform.SetPositionAndRotation(player.transform.position + cameraOffset,
                    Quaternion.LookRotation(-cameraOffset));
            }

            var follow = view.GetComponent<PlayerCameraFollow>();
            if (follow == null)
            {
                follow = view.gameObject.AddComponent<PlayerCameraFollow>();
                var fresh = new SerializedObject(follow);
                fresh.FindProperty("cameraOffset").vector3Value = cameraOffset;
                fresh.FindProperty("minZoom").floatValue = 4.2f;
                fresh.FindProperty("maxZoom").floatValue = 9f;
                fresh.ApplyModifiedPropertiesWithoutUndo();
            }

            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player;
            so.FindProperty("boundaryTerrain").objectReferenceValue = terrain;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ helpers

        internal static Transform Root(string name)
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == name) return go.transform;
            }

            return new GameObject(name).transform;
        }

        internal static Transform Fresh(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null) DestroyImmediate(old.gameObject);
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        internal static Transform Marker(Transform parent, string name, Vector3 position, Vector3 facing, int iconIndex)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
                var icon = EditorGUIUtility.IconContent($"sv_label_{iconIndex}").image as Texture2D;
                if (icon != null) EditorGUIUtility.SetIconForObject(t.gameObject, icon);
            }

            t.localPosition = position;
            t.localRotation = Quaternion.LookRotation(facing);
            return t;
        }

        internal static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size,
            Material material, bool collider, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        internal static GameObject Cylinder(Transform parent, string name, Vector3 baseCenter, float radius,
            float height, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = baseCenter + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        internal static void MarkStatic(Transform root)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                            StaticEditorFlags.OccluderStatic |
                                            StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        internal static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Unit triangular prism: base 1 m wide at y = 0, apex at y = 1, length 1 m along Z.</summary>
        internal static Mesh PrismMesh()
        {
            string path = MeshFolder + "/GB_PrismaSegitiga.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            var a0 = new Vector3(-0.5f, 0f, -0.5f);
            var b0 = new Vector3(0.5f, 0f, -0.5f);
            var c0 = new Vector3(0f, 1f, -0.5f);
            var a1 = new Vector3(-0.5f, 0f, 0.5f);
            var b1 = new Vector3(0.5f, 0f, 0.5f);
            var c1 = new Vector3(0f, 1f, 0.5f);
            var centroid = new Vector3(0f, 1f / 3f, 0f);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            void Tri(Vector3 p, Vector3 q, Vector3 s)
            {
                // Unity front faces are clockwise: Cross(q - p, s - p) must point away from the centre.
                if (Vector3.Dot(Vector3.Cross(q - p, s - p), (p + q + s) / 3f - centroid) < 0f) (q, s) = (s, q);
                int i = vertices.Count;
                vertices.Add(p); vertices.Add(q); vertices.Add(s);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            }

            void Quad(Vector3 p, Vector3 q, Vector3 s, Vector3 t)
            {
                Tri(p, q, s);
                Tri(p, s, t);
            }

            Tri(a0, b0, c0);
            Tri(a1, b1, c1);
            Quad(a0, b0, b1, a1);
            Quad(a0, c0, c1, a1);
            Quad(b0, c0, c1, b1);

            mesh = new Mesh { name = "GB_PrismaSegitiga" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            EnsureFolder(MeshFolder);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Greybox colours, taken from the shared palette so the greybox already reads like the island.</summary>
        internal sealed class Palette
        {
            public readonly Material Stone = Create("GB_BatuAlunAlun", "C9C1B3");
            public readonly Material StoneDark = Create("GB_BatuTua", "A9A6A0");
            public readonly Material Road = Create("GB_JalanTanah", "D2B285");
            public readonly Material Wall = Create("GB_DindingPutih", "F7F3EA");
            public readonly Material Column = Create("GB_TiangPutih", "FBF8F2");
            public readonly Material Roof = Create("GB_AtapMerah", "D9574A");
            public readonly Material RoofDark = Create("GB_AtapMerahGelap", "A93F38");
            public readonly Material Wood = Create("GB_Kayu", "8D5A34");
            public readonly Material Glass = Create("GB_KacaJendela", "8EC9DD");
            public readonly Material PlayerColor = Create("GB_PatokanPlayer", "F2994A");

            internal static Material Create(string name, string hex)
            {
                EnsureFolder(MaterialFolder);
                string path = $"{MaterialFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }

                ColorUtility.TryParseHtmlString("#" + hex, out Color color);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0.2f);
                EditorUtility.SetDirty(material);
                return material;
            }
        }
    }
}
