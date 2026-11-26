using System;
using Sagara.Dermaga.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sagara.DermagaV2
{
    [DefaultExecutionOrder(-25)]
    [DisallowMultipleComponent]
    public sealed class DermagaDockRepair : MonoBehaviour
    {
        [Serializable]
        private sealed class RepairSite
        {
            // Persisted identity. Keep this number with the same physical repair point.
            public int slot;
            public Transform approach;
            public GameObject damaged;
            public GameObject repaired;
        }

        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private FishingSession fishing;
        [SerializeField] private PlayerNpcInteraction player;
        [SerializeField] private DermagaQuestAnimation questAnimation;
        [SerializeField, Min(.1f)] private float interactionRadius = 1.15f;
        [SerializeField] private RepairSite[] sites = Array.Empty<RepairSite>();

        private bool spotsUnlocked;
        private bool uiInteractQueued;
        public string ActionPrompt { get; private set; } = string.Empty;

        internal void RequestInteract() => uiInteractQueued = true;

        private void Awake()
        {
            spotsUnlocked = quest.FishingUnlocked;
            SetSpotAvailability(spotsUnlocked);
            foreach (RepairSite site in sites)
            {
                SetRepairAppearance(site, quest.HasRepairedDock(site.slot));
            }
        }

        private void Update()
        {
            ActionPrompt = string.Empty;
            if (quest.FishingUnlocked || !quest.CanShowInteractionHint || player.Target)
                return;

            Vector3 position = player.transform.position;
            if (quest.Phase == DermagaQuestPhase.RepairDock)
            {
                RepairSite site = FindNearbyRepair(position);
                if (site != null)
                {
                    bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) || uiInteractQueued;
                    uiInteractQueued = false;
                    ActionPrompt = "[E] Perbaiki Dermaga";
                    if (pressed
                        && questAnimation.TryRepair(WorkPoint(site, position), () => RepairSection(site)))
                    {
                        ActionPrompt = string.Empty;
                    }
                    return;
                }
            }
            uiInteractQueued = false;

            foreach (FishingSpot spot in fishing.spots)
            {
                if (spot && spot.gameObject.activeInHierarchy && spot.Contains(position))
                {
                    switch (quest.Phase)
                    {
                        case DermagaQuestPhase.RepairDock:
                            ActionPrompt = "Selesaikan perbaikan dermaga terlebih dahulu sebelum memancing.";
                            break;
                        case DermagaQuestPhase.ReportDock:
                            ActionPrompt = "Laporkan perbaikan ke Pak Darma.";
                            break;
                        default:
                            ActionPrompt = "Perbaiki dermaga untuk membuka akses memancing.";
                            break;
                    }
                    break;
                }
            }
        }

        private static Vector3 WorkPoint(RepairSite site, Vector3 playerPosition)
        {
            // The slot's work point can be beyond the reachable approach on a damaged section.
            Vector3 offset = site.damaged.transform.position - playerPosition;
            offset.y = 0f;
            return playerPosition + offset.normalized * .58f;
        }

        private void RepairSection(RepairSite site)
        {
            if (quest.TryRecordDockRepair(site.slot))
            {
                SetRepairAppearance(site, true);
            }
        }

        private void LateUpdate()
        {
            if (!spotsUnlocked && quest.FishingUnlocked)
            {
                SetSpotAvailability(true);
                spotsUnlocked = true;
                ActionPrompt = string.Empty;
            }
        }

        private RepairSite FindNearbyRepair(Vector3 position)
        {
            RepairSite nearest = null;
            float best = interactionRadius * interactionRadius;
            int reachableSlot = quest.NextDockRepairSlot;
            foreach (RepairSite site in sites)
            {
                if (site.slot != reachableSlot)
                {
                    continue;
                }

                Vector3 delta = position - site.approach.position;
                float distance = new Vector2(delta.x, delta.z).sqrMagnitude;
                if (Mathf.Abs(delta.y) < .75f && distance < best)
                {
                    best = distance;
                    nearest = site;
                }
            }
            return nearest;
        }

        private static void SetRepairAppearance(RepairSite site, bool repaired)
        {
            site.damaged.SetActive(!repaired);
            site.repaired.SetActive(repaired);
        }

        private void SetSpotAvailability(bool available)
        {
            // FishingSession checks isActiveAndEnabled for both proximity and direct TryBegin.
            foreach (FishingSpot spot in fishing.spots)
                if (spot)
                    spot.enabled = available;
        }
    }
}
