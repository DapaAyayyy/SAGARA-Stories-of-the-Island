using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sagara.DermagaV2
{
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    public sealed class DermagaMangrovePlanting : MonoBehaviour
    {
        [Serializable]
        private sealed class PlantingSite
        {
            // Persisted identity. Keep this number with the same physical plot.
            public int slot;
            public Transform approach;
            public GameObject sapling;
        }

        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private PlayerNpcInteraction player;
        [SerializeField] private CharacterController character;
        [SerializeField] private DermagaQuestAnimation questAnimation;
        [SerializeField, Min(.1f)] private float interactionRadius = .78f;
        [SerializeField] private PlantingSite[] sites = Array.Empty<PlantingSite>();

        [Header("Grown Mangrove Visuals (Animated after Quest Complete)")]
        [SerializeField] private Mesh grownBarkMesh;
        [SerializeField] private Mesh grownCanopyMesh;
        [SerializeField] private Material grownBarkMaterial;
        [SerializeField] private Material grownCanopyMaterial;

        public string ActionPrompt { get; private set; } = string.Empty;
        private bool uiInteractQueued;
        private readonly List<Transform> grownTrees = new List<Transform>();
        private bool treesGrown;

        internal void RequestInteract() => uiInteractQueued = true;

        internal Vector3[] SitePositions
        {
            get
            {
                var pts = new Vector3[sites.Length];
                for (int i = 0; i < sites.Length; i++)
                {
                    pts[i] = sites[i].sapling ? sites[i].sapling.transform.position : sites[i].approach.position;
                }
                return pts;
            }
        }

        private void Awake()
        {
            bool alreadyGrown = quest && DermagaQuestSnapshot.Order((int)quest.Phase)
                >= DermagaQuestSnapshot.Order((int)DermagaQuestPhase.ReportMangroves);

            foreach (PlantingSite site in sites)
            {
                bool planted = quest.HasPlantedMangrove(site.slot);
                site.sapling.SetActive(planted && !alreadyGrown);
            }

            if (alreadyGrown)
            {
                SpawnGrownTrees(initialScale: 1f);
                treesGrown = true;
            }
        }

        private void Update()
        {
            ActionPrompt = string.Empty;
            if (quest.Phase != DermagaQuestPhase.PlantMangroves || !quest.CanShowInteractionHint
                || player.Target || !character.isGrounded)
                return;

            PlantingSite site = FindNearbySite(character.transform.position);
            if (site == null)
            {
                return;
            }

            bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) || uiInteractQueued;
            uiInteractQueued = false;
            ActionPrompt = "[E] Tanam Bibit Mangrove";
            if (pressed)
            {
                if (questAnimation.TryPlant(site.sapling, () => Plant(site)))
                {
                    ActionPrompt = string.Empty;
                }
            }
        }

        /// <summary>
        /// Smoothly grows all 4 newly planted mangrove saplings into lush young-mature SAGARA 3D mangroves
        /// during the celebratory post-planting drone flyover (stays permanently grown afterward!).
        /// </summary>
        internal IEnumerator AnimateGrowAllMangrovesRoutine(float duration, Action<Vector3> onTreePopSparkle)
        {
            if (treesGrown || !grownBarkMesh || !grownCanopyMesh)
            {
                yield break;
            }

            SpawnGrownTrees(initialScale: 0.01f);
            treesGrown = true;

            float[] targetScales = { 0.86f, 0.96f, 0.92f, 0.88f };
            bool[] popped = new bool[grownTrees.Count];

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < grownTrees.Count; i++)
                {
                    float startDelay = i * 0.42f;
                    float localT = Mathf.Clamp01((elapsed - startDelay) / Mathf.Max(0.4f, duration - 1.35f));
                    // Cozy elastic growth curve: smooth organic swell with gentle spring settle at full height!
                    float ease = localT * localT * (3f - 2f * localT);
                    float overshoot = localT > 0.65f
                        ? Mathf.Sin(((localT - 0.65f) / 0.35f) * Mathf.PI) * 0.11f
                        : 0f;
                    float target = targetScales[i % targetScales.Length];
                    float s = Mathf.Max(0.01f, (ease + overshoot) * target);
                    grownTrees[i].localScale = new Vector3(s, s * (0.94f + 0.06f * ease), s);

                    // Gently shrink starter sapling as the lush mangrove rises over it
                    if (i < sites.Length && sites[i].sapling)
                    {
                        if (localT > 0.25f)
                        {
                            sites[i].sapling.SetActive(false);
                        }
                    }

                    if (!popped[i] && localT >= 0.72f)
                    {
                        popped[i] = true;
                        onTreePopSparkle?.Invoke(grownTrees[i].position + Vector3.up * 1.4f);
                    }
                }
                yield return null;
            }

            // Lock cleanly at final lush grown scales
            for (int i = 0; i < grownTrees.Count; i++)
            {
                float target = targetScales[i % targetScales.Length];
                grownTrees[i].localScale = Vector3.one * target;
                if (i < sites.Length && sites[i].sapling)
                {
                    sites[i].sapling.SetActive(false);
                }
            }
        }

        private void SpawnGrownTrees(float initialScale)
        {
            if (grownTrees.Count > 0 || !grownBarkMesh || !grownCanopyMesh)
            {
                return;
            }

            float[] yaws = { 25f, 138f, 242f, 318f };
            float[] finalScales = { 0.86f, 0.96f, 0.92f, 0.88f };

            for (int i = 0; i < sites.Length; i++)
            {
                Vector3 pos = sites[i].sapling ? sites[i].sapling.transform.position : sites[i].approach.position;
                var treeRoot = new GameObject("PlantedGrownMangrove_" + i);
                treeRoot.transform.SetParent(transform, true);
                treeRoot.transform.position = pos + Vector3.down * 0.06f;
                treeRoot.transform.rotation = Quaternion.Euler(0f, yaws[i % yaws.Length], 0f);
                treeRoot.transform.localScale = Vector3.one * (initialScale > 0.5f ? finalScales[i % finalScales.Length] : initialScale);

                var barkGo = new GameObject("Bark");
                barkGo.transform.SetParent(treeRoot.transform, false);
                barkGo.AddComponent<MeshFilter>().sharedMesh = grownBarkMesh;
                var bMr = barkGo.AddComponent<MeshRenderer>();
                if (grownBarkMaterial) bMr.sharedMaterial = grownBarkMaterial;

                var canopyGo = new GameObject("Canopy");
                canopyGo.transform.SetParent(treeRoot.transform, false);
                canopyGo.AddComponent<MeshFilter>().sharedMesh = grownCanopyMesh;
                var cMr = canopyGo.AddComponent<MeshRenderer>();
                if (grownCanopyMaterial) cMr.sharedMaterial = grownCanopyMaterial;

                var cap = treeRoot.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 1.0f, 0f);
                cap.radius = 0.34f;
                cap.height = 2.2f;

                grownTrees.Add(treeRoot.transform);
            }
        }

        private void Plant(PlantingSite site)
        {
            if (quest.TryPlantMangrove(site.slot))
            {
                site.sapling.SetActive(true);
            }
        }

        private PlantingSite FindNearbySite(Vector3 position)
        {
            PlantingSite nearest = null;
            float best = interactionRadius * interactionRadius;
            foreach (PlantingSite site in sites)
            {
                if (quest.HasPlantedMangrove(site.slot))
                {
                    continue;
                }

                Vector3 delta = position - site.approach.position;
                float distance = new Vector2(delta.x, delta.z).sqrMagnitude;
                if (Mathf.Abs(delta.y) < .40f && distance < best)
                {
                    best = distance;
                    nearest = site;
                }
            }
            return nearest;
        }
    }
}
