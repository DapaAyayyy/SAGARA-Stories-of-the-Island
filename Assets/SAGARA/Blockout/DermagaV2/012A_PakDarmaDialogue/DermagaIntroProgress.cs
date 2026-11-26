using System;
using System.Collections.Generic;
using Sagara.Dermaga.Fishing;
using UnityEngine;

namespace Sagara.DermagaV2
{
    public enum DermagaQuestPhase
    {
        // These values are part of the version 1 save format.
        MeetPakDarma = 0,
        CleanBeach = 1,
        ReportToPakDarma = 2,
        CollectWood = 3,
        ReportWood = 4,
        RepairDock = 5,
        ReportDock = 6,
        FishingUnlocked = 7,
        PlantMangroves = 8,
        ReportMangroves = 9,
        MangrovesComplete = 10,
        CollectCoralSupplies = 11,
        ReportCoralSupplies = 12,
        BoardRestorationBoat = 13,
        CoralRestored = 14
    }

    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NpcDialogue))]
    public sealed class DermagaIntroProgress : MonoBehaviour
    {
        public const int DebrisCount = 3;
        public const int BranchCount = 4;
        public const int DockRepairCount = 3;
        public const int MangroveCount = 4;
        public const int CoralSupplyCount = 3;
        private const int AllDebris = (1 << DebrisCount) - 1;
        private const int AllBranches = (1 << BranchCount) - 1;
        private const int AllDockRepairs = (1 << DockRepairCount) - 1;
        private const int AllMangroves = (1 << MangroveCount) - 1;
        private const int AllCoralSupplies = (1 << CoralSupplyCount) - 1;

        [SerializeField] private PlayerNpcInteraction player;
        [SerializeField] private PlayerTrashInteraction collector;
        [SerializeField] private FishingSession fishing;
        [SerializeField] private DermagaQuestAnimation questAnimation;
        [SerializeField] private TrashPickup[] beachDebris;
        [SerializeField] private GameObject beachBeforeCleanup;
        [SerializeField] private GameObject beachAfterCleanup;
        [SerializeField] private TrashPickup[] forestBranches;
        [SerializeField] private NpcDialogue cleanupDialogue;
        [SerializeField] private NpcDialogue reportDialogue;
        [SerializeField] private NpcDialogue woodDialogue;
        [SerializeField] private NpcDialogue reportWoodDialogue;
        [SerializeField] private NpcDialogue repairDockDialogue;
        [SerializeField] private NpcDialogue reportDockDialogue;
        [SerializeField] private NpcDialogue fishingUnlockedDialogue;
        [SerializeField] private NpcDialogue plantingDialogue;
        [SerializeField] private NpcDialogue reportMangrovesDialogue;
        [SerializeField] private NpcDialogue coralSuppliesDialogue;
        [SerializeField] private NpcDialogue reportCoralSuppliesDialogue;
        [SerializeField] private NpcDialogue boardRestorationBoatDialogue;
        [SerializeField] private NpcDialogue coralRestoredDialogue;

        private NpcDialogue introduction;
        private NpcDialogue activeDialogue;
        private NpcDialogueLine cleanupInstruction;
        private NpcDialogueLine mangroveInstruction;
        private NpcDialogueLine repairAcknowledgement;
        private int collectedDebris;
        private int collectedBranches;
        private int repairedDock;
        private int plantedMangroves;
        private int collectedCoralSupplies;
        private readonly List<string> discoveredFish = new List<string>();
        private readonly List<string> discoveredCorals = new List<string>();
        private bool closingLineRead;
        private bool expeditionEscort;
        private int resumeAfterMangroves;
        private DermagaQuestSave save;

        public DermagaQuestPhase Phase { get; private set; }
        public bool BeachCleanupAvailable => Phase != DermagaQuestPhase.MeetPakDarma;
        public bool ShowQuest => BeachCleanupAvailable && Phase != DermagaQuestPhase.FishingUnlocked
            && Phase != DermagaQuestPhase.CoralRestored;
        internal FishingSession Fishing => fishing;
        internal bool FishingBusy => fishing.Busy;
        internal bool OverlayControlsHeld { get; set; }
        internal bool CanShowInteractionHint => !OverlayControlsHeld && !player.IsTalking && !fishing.Busy
            && (!questAnimation || !questAnimation.Busy);
        internal bool ReadyForPickup => Collecting && CanShowInteractionHint
            && player.Target == null && fishing.NearbySpot == null;
        internal bool IsPakDarma(NpcDialogue npc) => npc && (npc == introduction || npc == activeDialogue);
        private bool Collecting => Phase == DermagaQuestPhase.CleanBeach || Phase == DermagaQuestPhase.CollectWood;
        public string PickupPrompt => Collecting && collector.isActiveAndEnabled ? collector.ActionPrompt : string.Empty;
        public int CollectedCount => CountCollected(collectedDebris);
        public int BranchesCollected => CountCollected(collectedBranches);
        public int DockRepairsCompleted => CountCollected(repairedDock);
        internal int NextDockRepairSlot
        {
            get
            {
                for (int slot = 0; slot < DockRepairCount; slot++)
                {
                    if (!HasRepairedDock(slot))
                    {
                        return slot;
                    }
                }

                return -1;
            }
        }

        public bool FishingUnlocked => DermagaQuestSnapshot.Order((int)Phase)
            >= DermagaQuestSnapshot.Order((int)DermagaQuestPhase.FishingUnlocked)
            || resumeAfterMangroves == (int)DermagaQuestPhase.FishingUnlocked;
        public bool MangroveQuestAccepted => DermagaQuestSnapshot.Order((int)Phase)
            >= DermagaQuestSnapshot.Order((int)DermagaQuestPhase.PlantMangroves);
        public int MangrovesPlanted => CountCollected(plantedMangroves);
        public int SeedlingsRemaining => MangroveQuestAccepted ? MangroveCount - MangrovesPlanted : 0;
        public int CoralSuppliesCollected => CountCollected(collectedCoralSupplies);
        public bool CoralJournalUnlocked => Phase == DermagaQuestPhase.CoralRestored;

        private void Awake()
        {
            introduction = GetComponent<NpcDialogue>();
            activeDialogue = introduction;
            cleanupInstruction = LastLine(introduction);
            mangroveInstruction = LastLine(reportDialogue);
            repairAcknowledgement = LastLine(reportWoodDialogue);

            save = new DermagaQuestSave();
            var progress = save.Load();
            if (progress != null)
            {
                Phase = (DermagaQuestPhase)progress.phase;
                resumeAfterMangroves = progress.resumeAfterMangroves;
                collectedDebris = progress.trashMask;
                collectedBranches = progress.branchMask;
                repairedDock = progress.repairMask;
                plantedMangroves = progress.mangroveMask;
                collectedCoralSupplies = progress.coralSupplyMask;
                discoveredFish.AddRange(progress.discoveredFish);
                discoveredCorals.AddRange(progress.discoveredCorals);
                UseReturningPlayerDialogue();
            }
        }

        private void UseReturningPlayerDialogue()
        {
            var line = LastLine(reportMangrovesDialogue);
            switch ((DermagaQuestPhase)resumeAfterMangroves)
            {
                case DermagaQuestPhase.CollectWood:
                    line.text = "Kita lanjutkan pekerjaan di dermaga. Cari sisa rantingnya di petak hutan.";
                    break;
                case DermagaQuestPhase.ReportWood:
                    line.text = "Ranting yang sudah kamu kumpulkan cukup. Kita siapkan perbaikan dermaga.";
                    break;
                case DermagaQuestPhase.RepairDock:
                    line.text = "Sekarang kita lanjutkan bagian dermaga yang belum diperbaiki.";
                    break;
                case DermagaQuestPhase.ReportDock:
                    line.text = "Kayunya sudah terpasang. Kita periksa dermaganya sebelum dibuka untuk mancing.";
                    break;
                case DermagaQuestPhase.FishingUnlocked:
                    line.text = "Kamu masih boleh mancing. Kalau ingin membantu lagi, kita bisa bicarakan terumbu karang.";
                    break;
            }
        }

        private void Start()
        {
            ApplyPickupState(beachDebris, collectedDebris, Phase == DermagaQuestPhase.CleanBeach);
            ApplyPickupState(forestBranches, collectedBranches, Phase == DermagaQuestPhase.CollectWood);
            UpdateBeachAppearance();
            SelectDialogue();
        }

        private void Update()
        {
            collector.enabled = ReadyForPickup;
        }

        private void LateUpdate()
        {
            var previousPhase = Phase;
            if (Phase == DermagaQuestPhase.MeetPakDarma && HasRead(introduction, cleanupInstruction))
            {
                Phase = DermagaQuestPhase.CleanBeach;
                ApplyPickupState(beachDebris, collectedDebris, true);
            }
            else if (Phase == DermagaQuestPhase.ReportToPakDarma && HasRead(reportDialogue, mangroveInstruction))
            {
                Phase = DermagaQuestPhase.PlantMangroves;
            }
            else if (Phase == DermagaQuestPhase.ReportWood && HasRead(reportWoodDialogue, repairAcknowledgement))
            {
                Phase = DermagaQuestPhase.RepairDock;
            }
            else if (Phase == DermagaQuestPhase.ReportDock && HasFinishedConversation(reportDockDialogue))
            {
                Phase = DermagaQuestPhase.FishingUnlocked;
            }
            else if (Phase == DermagaQuestPhase.ReportMangroves && HasFinishedConversation(reportMangrovesDialogue))
            {
                Phase = resumeAfterMangroves == 0
                    ? DermagaQuestPhase.CollectWood
                    : (DermagaQuestPhase)resumeAfterMangroves;
                resumeAfterMangroves = 0;
                ApplyPickupState(forestBranches, collectedBranches, Phase == DermagaQuestPhase.CollectWood);
            }
            else if (Phase == DermagaQuestPhase.FishingUnlocked && HasFinishedConversation(fishingUnlockedDialogue))
            {
                Phase = DermagaQuestPhase.CollectCoralSupplies;
            }
            else if (Phase == DermagaQuestPhase.ReportCoralSupplies && HasFinishedConversation(reportCoralSuppliesDialogue))
            {
                Phase = DermagaQuestPhase.BoardRestorationBoat;
            }

            if (Phase != previousPhase)
            {
                SaveProgress();
            }

            // Leave the current lines and speaker intact until the shared conversation closes.
            if (!player.IsTalking)
                SelectDialogue();

            if (!expeditionEscort && activeDialogue != introduction)
                introduction.transform.rotation = activeDialogue.transform.rotation;
        }

        internal void RecordDebris(int slot)
        {
            if (Phase != DermagaQuestPhase.CleanBeach
                || !TryMarkCompleted(ref collectedDebris, slot, DebrisCount))
            {
                return;
            }

            if (collectedDebris == AllDebris)
            {
                Phase = DermagaQuestPhase.ReportToPakDarma;
                UpdateBeachAppearance();
            }

            SaveProgress();
        }

        private void UpdateBeachAppearance()
        {
            bool cleaned = collectedDebris == AllDebris;
            if (beachBeforeCleanup)
            {
                beachBeforeCleanup.SetActive(!cleaned);
            }
            if (beachAfterCleanup)
            {
                beachAfterCleanup.SetActive(cleaned);
            }
        }

        internal void RecordBranch(int slot)
        {
            if (Phase != DermagaQuestPhase.CollectWood
                || !TryMarkCompleted(ref collectedBranches, slot, BranchCount))
            {
                return;
            }

            if (collectedBranches == AllBranches)
            {
                Phase = DermagaQuestPhase.ReportWood;
            }

            SaveProgress();
        }

        internal bool IsBranchTarget(TrashPickup pickup)
        {
            return Phase == DermagaQuestPhase.CollectWood && collector.isActiveAndEnabled
                && collector.Target == pickup;
        }

        internal bool TryRecordDockRepair(int slot)
        {
            if (Phase != DermagaQuestPhase.RepairDock
                || slot != NextDockRepairSlot
                || !TryMarkCompleted(ref repairedDock, slot, DockRepairCount))
            {
                return false;
            }

            if (repairedDock == AllDockRepairs)
            {
                Phase = DermagaQuestPhase.ReportDock;
            }

            SaveProgress();
            return true;
        }

        internal bool HasRepairedDock(int slot) => IsMarked(repairedDock, slot);
        internal bool HasCollectedDebris(int slot) => IsMarked(collectedDebris, slot);
        internal bool HasCollectedBranch(int slot) => IsMarked(collectedBranches, slot);
        internal bool HasPlantedMangrove(int slot) => IsMarked(plantedMangroves, slot);

        internal bool TryPlantMangrove(int slot)
        {
            if (Phase != DermagaQuestPhase.PlantMangroves
                || !TryMarkCompleted(ref plantedMangroves, slot, MangroveCount))
            {
                return false;
            }

            if (plantedMangroves == AllMangroves)
            {
                Phase = DermagaQuestPhase.ReportMangroves;
            }

            SaveProgress();
            return true;
        }

        internal bool HasCoralSupply(int slot) => IsMarked(collectedCoralSupplies, slot);

        internal bool TryCollectCoralSupply(int slot)
        {
            if (Phase != DermagaQuestPhase.CollectCoralSupplies
                || !TryMarkCompleted(ref collectedCoralSupplies, slot, CoralSupplyCount))
            {
                return false;
            }

            if (collectedCoralSupplies == AllCoralSupplies)
            {
                Phase = DermagaQuestPhase.ReportCoralSupplies;
            }

            SaveProgress();
            return true;
        }

        internal void CompleteCoralExpedition()
        {
            if (Phase != DermagaQuestPhase.BoardRestorationBoat)
            {
                return;
            }

            Phase = DermagaQuestPhase.CoralRestored;
            SaveProgress();
        }

        internal bool HasDiscoveredFish(string id) => discoveredFish.Contains(id);
        internal bool HasDiscoveredCoral(string id) => discoveredCorals.Contains(id);

        internal void RecordFishingDiscovery(string fishId, string coralId)
        {
            if (!FishingUnlocked)
            {
                return;
            }

            bool changed = AddDiscovery(discoveredFish, fishId);
            if (CoralJournalUnlocked)
            {
                changed |= AddDiscovery(discoveredCorals, coralId);
            }

            if (changed)
            {
                SaveProgress();
            }
        }

        private static bool AddDiscovery(List<string> discoveries, string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || discoveries.Count >= 32
                || discoveries.Contains(id))
            {
                return false;
            }

            discoveries.Add(id);
            return true;
        }

        private void SaveProgress()
        {
            save.Save(new DermagaQuestSnapshot
            {
                schemaVersion = DermagaQuestSnapshot.CurrentVersion,
                phase = (int)Phase,
                resumeAfterMangroves = resumeAfterMangroves,
                trashMask = collectedDebris,
                branchMask = collectedBranches,
                repairMask = repairedDock,
                mangroveMask = plantedMangroves,
                coralSupplyMask = collectedCoralSupplies,
                discoveredFish = discoveredFish.ToArray(),
                discoveredCorals = discoveredCorals.ToArray()
            });
        }

        private static void ApplyPickupState(TrashPickup[] pickups, int mask, bool available)
        {
            foreach (var pickup in pickups)
            {
                int slot = pickup.GetComponent<DermagaCleanupDebris>().Slot;
                bool collected = IsMarked(mask, slot);
                // Restoring visibility never invokes TryCollect or grants inventory rewards.
                pickup.enabled = available && !collected;
                pickup.gameObject.SetActive(!collected);
            }
        }

        private static bool TryMarkCompleted(ref int mask, int slot, int limit)
        {
            if (slot < 0 || slot >= limit || IsMarked(mask, slot))
            {
                return false;
            }

            mask |= 1 << slot;
            return true;
        }

        private static bool IsMarked(int mask, int slot) => (mask & (1 << slot)) != 0;

        private static int CountCollected(int mask)
        {
            int count = 0;
            for (; mask != 0; mask >>= 1)
                count += mask & 1;
            return count;
        }

        private bool HasRead(NpcDialogue speaker, NpcDialogueLine line)
        {
            return line != null && player.Speaker == speaker && player.CurrentLine == line && player.IsLineComplete;
        }

        private bool HasFinishedConversation(NpcDialogue speaker)
        {
            if (HasRead(speaker, LastLine(speaker)))
                closingLineRead = true;

            if (!closingLineRead || player.IsTalking)
                return false;

            closingLineRead = false;
            return true;
        }

        private static NpcDialogueLine LastLine(NpcDialogue dialogue)
        {
            var lines = dialogue.CurrentLines;
            return lines != null && lines.Length > 0 ? lines[lines.Length - 1] : null;
        }

        private void SelectDialogue()
        {
            NpcDialogue next;
            switch (Phase)
            {
                case DermagaQuestPhase.CleanBeach:
                    next = cleanupDialogue;
                    break;
                case DermagaQuestPhase.ReportToPakDarma:
                    next = reportDialogue;
                    break;
                case DermagaQuestPhase.CollectWood:
                    next = woodDialogue;
                    break;
                case DermagaQuestPhase.ReportWood:
                    next = reportWoodDialogue;
                    break;
                case DermagaQuestPhase.RepairDock:
                    next = repairDockDialogue;
                    break;
                case DermagaQuestPhase.ReportDock:
                    next = reportDockDialogue;
                    break;
                case DermagaQuestPhase.FishingUnlocked:
                    next = fishingUnlockedDialogue;
                    break;
                case DermagaQuestPhase.PlantMangroves:
                    next = plantingDialogue;
                    break;
                case DermagaQuestPhase.ReportMangroves:
                    next = reportMangrovesDialogue;
                    break;
                case DermagaQuestPhase.CollectCoralSupplies:
                    next = coralSuppliesDialogue;
                    break;
                case DermagaQuestPhase.ReportCoralSupplies:
                    next = reportCoralSuppliesDialogue;
                    break;
                case DermagaQuestPhase.BoardRestorationBoat:
                    next = boardRestorationBoatDialogue;
                    break;
                case DermagaQuestPhase.CoralRestored:
                    next = coralRestoredDialogue;
                    break;
                default:
                    next = introduction;
                    break;
            }

            if (next == activeDialogue)
                return;

            activeDialogue.enabled = false;
            next.enabled = !expeditionEscort;
            next.transform.rotation = introduction.transform.rotation;
            activeDialogue = next;
        }

        internal void SetExpeditionEscort(bool active)
        {
            expeditionEscort = active;
            activeDialogue.enabled = !active;
        }

        private void OnDisable()
        {
            if (collector)
                collector.enabled = false;
        }
    }
}
