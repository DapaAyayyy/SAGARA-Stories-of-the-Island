using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sagara.DermagaV2
{
    [Serializable]
    internal sealed class DermagaQuestSnapshot
    {
        internal const int CurrentVersion = 3;

        // Negative defaults make omitted fields invalid, including masks at a fresh start.
        public int schemaVersion = -1;
        public int phase = -1;
        public int trashMask = -1;
        public int branchMask = -1;
        public int repairMask = -1;
        public int mangroveMask = -1;
        public int coralSupplyMask = -1;
        public int resumeAfterMangroves;
        public string[] discoveredFish;
        public string[] discoveredCorals;

        internal void Upgrade()
        {
            if (schemaVersion == 1 && phase <= (int)DermagaQuestPhase.MangrovesComplete)
            {
                schemaVersion = 2;
                coralSupplyMask = 0;
                discoveredFish = Array.Empty<string>();
                discoveredCorals = Array.Empty<string>();
            }

            if (schemaVersion != 2 || !IsValidLegacy())
            {
                return;
            }

            // Keeps partial wood/repair and earned fishing access while putting mangroves first.
            if (phase >= (int)DermagaQuestPhase.CollectWood && phase <= (int)DermagaQuestPhase.FishingUnlocked)
            {
                resumeAfterMangroves = phase;
                phase = (int)DermagaQuestPhase.PlantMangroves;
            }
            else if (phase == (int)DermagaQuestPhase.PlantMangroves
                || phase == (int)DermagaQuestPhase.ReportMangroves)
            {
                resumeAfterMangroves = (int)DermagaQuestPhase.FishingUnlocked;
            }
            else if (phase == (int)DermagaQuestPhase.MangrovesComplete)
            {
                phase = (int)DermagaQuestPhase.FishingUnlocked;
            }

            schemaVersion = CurrentVersion;
        }

        internal static int Order(int stage)
        {
            switch ((DermagaQuestPhase)stage)
            {
                case DermagaQuestPhase.PlantMangroves: return 3;
                case DermagaQuestPhase.ReportMangroves: return 4;
                case DermagaQuestPhase.CollectWood: return 5;
                case DermagaQuestPhase.ReportWood: return 6;
                case DermagaQuestPhase.RepairDock: return 7;
                case DermagaQuestPhase.ReportDock: return 8;
                case DermagaQuestPhase.FishingUnlocked: return 9;
                default: return stage;
            }
        }

        internal bool IsValid()
        {
            bool mangroveStage = phase == (int)DermagaQuestPhase.PlantMangroves
                || phase == (int)DermagaQuestPhase.ReportMangroves;
            if (phase < (int)DermagaQuestPhase.MeetPakDarma || phase > (int)DermagaQuestPhase.CoralRestored
                || phase == (int)DermagaQuestPhase.MangrovesComplete
                || (resumeAfterMangroves != 0 && (!mangroveStage
                    || resumeAfterMangroves < (int)DermagaQuestPhase.CollectWood
                    || resumeAfterMangroves > (int)DermagaQuestPhase.FishingUnlocked)))
            {
                return false;
            }

            int rank = Order(phase);
            int dockRank = resumeAfterMangroves == 0 ? rank : Order(resumeAfterMangroves);
            return MatchMasks(rank, dockRank, false)
                && ValidDiscoveries(discoveredFish, dockRank >= Order((int)DermagaQuestPhase.FishingUnlocked))
                && ValidDiscoveries(discoveredCorals, phase == (int)DermagaQuestPhase.CoralRestored);
        }

        private bool IsValidLegacy()
        {
            return phase >= 0 && phase <= (int)DermagaQuestPhase.CoralRestored
                && MatchMasks(phase, phase, true)
                && ValidDiscoveries(discoveredFish, phase >= (int)DermagaQuestPhase.FishingUnlocked)
                && ValidDiscoveries(discoveredCorals, phase == (int)DermagaQuestPhase.CoralRestored);
        }

        private bool MatchMasks(int rank, int dockRank, bool legacy)
        {
            return MatchesStage(trashMask, DermagaIntroProgress.DebrisCount, rank, 1)
                && MatchesStage(branchMask, DermagaIntroProgress.BranchCount, dockRank, legacy ? 3 : 5)
                && MatchesStage(repairMask, DermagaIntroProgress.DockRepairCount, dockRank, legacy ? 5 : 7)
                && MatchesStage(mangroveMask, DermagaIntroProgress.MangroveCount, rank, legacy ? 8 : 3)
                && MatchesStage(coralSupplyMask, DermagaIntroProgress.CoralSupplyCount, rank, 11);
        }

        private bool ValidDiscoveries(string[] ids, bool unlocked)
        {
            if (ids == null || ids.Length > 32 || (!unlocked && ids.Length != 0))
            {
                return false;
            }

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || Array.IndexOf(ids, id, 0, i) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesStage(int mask, int size, int rank, int collecting)
        {
            int full = (1 << size) - 1;
            if (mask < 0 || (mask & ~full) != 0)
            {
                return false;
            }

            if (rank < collecting)
            {
                return mask == 0;
            }

            return rank == collecting ? mask != full : mask == full;
        }

        internal bool Includes(DermagaQuestSnapshot other)
        {
            return other == null || (Order(phase) >= Order(other.phase)
                && Order(resumeAfterMangroves == 0 ? phase : resumeAfterMangroves)
                    >= Order(other.resumeAfterMangroves == 0 ? other.phase : other.resumeAfterMangroves)
                && (trashMask & other.trashMask) == other.trashMask
                && (branchMask & other.branchMask) == other.branchMask
                && (repairMask & other.repairMask) == other.repairMask
                && (mangroveMask & other.mangroveMask) == other.mangroveMask
                && (coralSupplyMask & other.coralSupplyMask) == other.coralSupplyMask
                && Array.TrueForAll(other.discoveredFish, id => Array.IndexOf(discoveredFish, id) >= 0)
                && Array.TrueForAll(other.discoveredCorals, id => Array.IndexOf(discoveredCorals, id) >= 0));
        }
    }

    internal sealed class DermagaQuestSave
    {
        private const string FileName = "quest-progress.json";
        private const int MaximumBytes = 16384;
        private readonly string path;
        private readonly string backupPath;
        private bool readOnly;

        private enum ReadStatus
        {
            Missing,
            Valid,
            Corrupt,
            Incompatible,
            Unavailable
        }

        internal DermagaQuestSave()
        {
            string folder = Path.Combine(Application.persistentDataPath, "DermagaV2");
#if UNITY_EDITOR
            string testSlot = DermagaQuestSaveMenu.TestSlot;
            if (!string.IsNullOrEmpty(testSlot))
            {
                folder = DermagaQuestSaveMenu.GetTestFolder(testSlot);
            }
#endif
            path = Path.Combine(folder, FileName);
            backupPath = path + ".bak";
        }

        internal DermagaQuestSnapshot Load()
        {
            ReadStatus primaryStatus = Read(path, out var primary);
            ReadStatus backupStatus = Read(backupPath, out var backup);
            readOnly = !CanWrite(primaryStatus, backupStatus);

            if (readOnly)
            {
                Debug.LogWarning($"DermagaV2 save is read-only (primary: {primaryStatus}, backup: {backupStatus}). Existing files were kept.");
            }

            if (primary != null && primary.Includes(backup))
            {
                return primary;
            }

            if (backup != null && backup.Includes(primary))
            {
                Debug.LogWarning("DermagaV2 restored progress from its backup.");
                return backup;
            }

            if (primary != null && backup != null)
            {
                readOnly = true;
                Debug.LogWarning("DermagaV2 save copies contain divergent progress. Both were kept.");
                return primary;
            }

            return null;
        }

        internal void Save(DermagaQuestSnapshot progress)
        {
            if (readOnly || !progress.IsValid())
            {
                return;
            }

            string pending = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Recheck the files under a writer lock so a stale game instance cannot roll progress back.
                using (var writerLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    ReadStatus primaryStatus = Read(path, out var primary);
                    ReadStatus backupStatus = Read(backupPath, out var backup);
                    if (!CanWrite(primaryStatus, backupStatus) || !progress.Includes(primary) || !progress.Includes(backup))
                    {
                        Debug.LogWarning("DermagaV2 kept the existing save because the new progress would replace newer, divergent, or unreadable data.");
                        return;
                    }

                    if (primary != null && primary.Includes(progress))
                    {
                        return;
                    }

                    pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    byte[] json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(progress, true));
                    using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(json, 0, json.Length);
                        stream.Flush(true);
                    }

                    if (primaryStatus == ReadStatus.Missing)
                    {
                        File.Move(pending, path);
                    }
                    else
                    {
                        // Preserve the last good backup when recovering a corrupt or older primary.
                        string rotateTo = primary != null && primary.Includes(backup) ? backupPath : null;
                        File.Replace(pending, path, rotateTo);
                    }
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning($"DermagaV2 could not write quest progress. Existing saves were kept. {error.Message}");
            }
            finally
            {
                RemovePendingFile(pending);
            }
        }

        private static bool CanWrite(ReadStatus primary, ReadStatus backup)
        {
            if (primary == ReadStatus.Incompatible || primary == ReadStatus.Unavailable
                || backup == ReadStatus.Incompatible || backup == ReadStatus.Unavailable)
            {
                return false;
            }

            return primary == ReadStatus.Valid || backup == ReadStatus.Valid
                || (primary == ReadStatus.Missing && backup == ReadStatus.Missing);
        }

        private static ReadStatus Read(string file, out DermagaQuestSnapshot progress)
        {
            progress = null;
            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                {
                    if (stream.Length == 0 || stream.Length > MaximumBytes)
                    {
                        return ReadStatus.Corrupt;
                    }

                    var candidate = new DermagaQuestSnapshot();
                    JsonUtility.FromJsonOverwrite(reader.ReadToEnd(), candidate);
                    if (candidate.schemaVersion > DermagaQuestSnapshot.CurrentVersion)
                    {
                        return ReadStatus.Incompatible;
                    }

                    candidate.Upgrade();
                    if (candidate.schemaVersion != DermagaQuestSnapshot.CurrentVersion || !candidate.IsValid())
                    {
                        return ReadStatus.Corrupt;
                    }

                    progress = candidate;
                    return ReadStatus.Valid;
                }
            }
            catch (FileNotFoundException)
            {
                return ReadStatus.Missing;
            }
            catch (DirectoryNotFoundException)
            {
                return ReadStatus.Missing;
            }
            catch (ArgumentException)
            {
                return ReadStatus.Corrupt;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return ReadStatus.Unavailable;
            }
        }

        private static void RemovePendingFile(string pending)
        {
            if (pending == null)
            {
                return;
            }

            try
            {
                File.Delete(pending);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning($"DermagaV2 could not remove its temporary save file. {error.Message}");
            }
        }
    }

#if UNITY_EDITOR
    internal static class DermagaQuestSaveMenu
    {
        private const string TestSlotKey = "Sagara.DermagaV2.QuestTestSlot";
        private const string UseTestMenu = "SAGARA/Dermaga V2/Use Isolated Test Save";
        private const string ResetTestMenu = "SAGARA/Dermaga V2/Archive and Reset Isolated Test Save";
        private const string ResetAllMenu = "SAGARA/Dermaga V2/Reset ALL Progress (Fresh Start + Opening Drone)";

        internal static string TestSlot
        {
            get
            {
                string val = UnityEditor.SessionState.GetString(TestSlotKey, string.Empty);
                return System.Text.RegularExpressions.Regex.IsMatch(val, @"\A[a-zA-Z0-9_-]{1,48}\z")
                    ? val : string.Empty;
            }
        }

        internal static string GetTestFolder(string slot)
        {
            string project = Hash128.Compute(Application.dataPath).ToString();
            return Path.Combine(Path.GetTempPath(), "Sagara", "DermagaV2", "EditorTest", project, slot);
        }

        [UnityEditor.MenuItem(UseTestMenu)]
        private static void ToggleTestSlot()
        {
            UnityEditor.SessionState.SetString(TestSlotKey, string.IsNullOrEmpty(TestSlot) ? "default" : string.Empty);
        }

        [UnityEditor.MenuItem(UseTestMenu, true)]
        private static bool CanToggleTestSlot()
        {
            UnityEditor.Menu.SetChecked(UseTestMenu, !string.IsNullOrEmpty(TestSlot));
            return !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [UnityEditor.MenuItem(ResetTestMenu, true)]
        private static bool CanResetTestSlot()
        {
            return !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !string.IsNullOrEmpty(TestSlot);
        }

        [UnityEditor.MenuItem(ResetTestMenu)]
        private static void ArchiveTestSlot()
        {
            string folder = GetTestFolder(TestSlot);
            if (!CanResetTestSlot() || !Directory.Exists(folder)
                || !UnityEditor.EditorUtility.DisplayDialog("Reset Dermaga V2 test save?",
                    "Archive the selected test slot and start it fresh on the next Play. The normal save is unaffected.",
                    "Archive test slot", "Cancel"))
            {
                return;
            }

            try
            {
                Directory.Move(folder, folder + ".archived-" + Guid.NewGuid().ToString("N"));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning($"DermagaV2 could not archive the test save. {error.Message}");
            }
        }

        [UnityEditor.MenuItem(ResetAllMenu, true)]
        private static bool CanResetAll()
        {
            return !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [UnityEditor.MenuItem(ResetAllMenu)]
        private static void ResetAllSavesForFreshStart()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

            int archived = 0;
            string suffix = ".archived-" + Guid.NewGuid().ToString("N");

            string normalFolder = Path.Combine(Application.persistentDataPath, "DermagaV2");
            if (Directory.Exists(normalFolder) && Directory.GetFiles(normalFolder).Length > 0)
            {
                try
                {
                    Directory.Move(normalFolder, normalFolder + suffix);
                    archived++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"DermagaV2 could not archive normal save: {e.Message}");
                }
            }

            string slot = string.IsNullOrEmpty(TestSlot) ? "default" : TestSlot;
            string testFolder = GetTestFolder(slot);
            if (Directory.Exists(testFolder) && Directory.GetFiles(testFolder).Length > 0)
            {
                try
                {
                    Directory.Move(testFolder, testFolder + suffix);
                    archived++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"DermagaV2 could not archive test save: {e.Message}");
                }
            }

            Debug.Log($"[DermagaV2] Reset ALL saves complete ({archived} folder archived). Next Play starts 100% fresh with Opening Drone View!");
        }
    }
#endif
}
