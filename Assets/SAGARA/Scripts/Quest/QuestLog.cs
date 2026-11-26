using System;
using System.Collections.Generic;

/// <summary>Tahap sebuah quest.</summary>
public enum QuestStatus
{
    BelumDiambil,
    Aktif,
    SiapDilaporkan, // target sudah tercapai, tinggal lapor ke NPC pemberi quest
    Selesai,
}

/// <summary>Data satu quest, misalnya "Bersihkan Alun-Alun: 2/5".</summary>
public sealed class QuestData
{
    public string Id { get; }
    public string Judul { get; }
    public string Tujuan { get; }
    public string PesanLapor { get; }
    public string Jenis { get; }
    public int Target { get; }
    public int Progres { get; internal set; }
    public QuestStatus Status { get; internal set; }

    public QuestData(string id, string judul, string tujuan, string pesanLapor, string jenis, int target)
    {
        Id = id;
        Judul = judul;
        Tujuan = tujuan;
        PesanLapor = pesanLapor;
        Jenis = jenis;
        Target = Math.Max(1, target);
        Status = QuestStatus.Aktif;
    }

    /// <summary>Teks singkat untuk UI, misalnya "Bersihkan Alun-Alun: 2/5".</summary>
    public string TeksSingkat => Status == QuestStatus.SiapDilaporkan
        ? Judul + " beres! " + PesanLapor
        : Judul + ": " + Progres + "/" + Target;
}

/// <summary>
/// Catatan semua quest pemain. Disimpan selama game berjalan (tidak hilang saat pindah scene).
///
/// Cara mencatat progres dari gameplay (bagian Dapa: pickup sampah dan panen):
///     QuestLog.Laporkan(QuestLog.JenisSampah, 1);
///     QuestLog.Laporkan(QuestLog.JenisPanen, jumlah);
/// Hanya quest yang sedang aktif dengan jenis yang sama yang bertambah progresnya.
///
/// UI quest (bagian Fathan) cukup membaca <see cref="QuestBerjalan"/> / <see cref="Semua"/>
/// dan mendengarkan event <see cref="Berubah"/>.
/// </summary>
public static class QuestLog
{
    public const string JenisSampah = "Sampah";
    public const string JenisPanen = "Panen";

    private static readonly List<QuestData> daftar = new List<QuestData>();

    /// <summary>Dipanggil setiap ada quest baru, progres bertambah, atau quest selesai.</summary>
    public static event Action Berubah;

    public static IReadOnlyList<QuestData> Semua => daftar;

    public static QuestData Cari(string id) => daftar.Find(q => q.Id == id);

    public static QuestStatus StatusDari(string id)
    {
        QuestData quest = Cari(id);
        return quest == null ? QuestStatus.BelumDiambil : quest.Status;
    }

    /// <summary>Quest yang sedang dikerjakan (paling baru diambil), atau null.</summary>
    public static QuestData QuestBerjalan =>
        daftar.FindLast(q => q.Status == QuestStatus.Aktif || q.Status == QuestStatus.SiapDilaporkan);

    public static QuestData Mulai(string id, string judul, string tujuan, string pesanLapor, string jenis, int target)
    {
        QuestData quest = Cari(id);
        if (quest != null)
        {
            return quest;
        }

        quest = new QuestData(id, judul, tujuan, pesanLapor, jenis, target);
        daftar.Add(quest);
        Berubah?.Invoke();
        return quest;
    }

    /// <summary>
    /// Dipanggil gameplay saat pemain melakukan sesuatu, misalnya memungut 1 sampah atau memanen 1 wortel.
    /// Kalau target quest tercapai, quest menjadi "siap dilaporkan".
    /// </summary>
    public static void Laporkan(string jenis, int jumlah)
    {
        if (jumlah <= 0)
        {
            return;
        }

        bool berubah = false;
        foreach (QuestData quest in daftar)
        {
            if (quest.Status != QuestStatus.Aktif || quest.Jenis != jenis)
            {
                continue;
            }

            quest.Progres = Math.Min(quest.Progres + jumlah, quest.Target);
            if (quest.Progres >= quest.Target)
            {
                quest.Status = QuestStatus.SiapDilaporkan;
            }

            berubah = true;
        }

        if (berubah)
        {
            Berubah?.Invoke();
        }
    }

    public static void Selesaikan(string id)
    {
        QuestData quest = Cari(id);
        if (quest == null || quest.Status == QuestStatus.Selesai)
        {
            return;
        }

        quest.Progres = quest.Target;
        quest.Status = QuestStatus.Selesai;
        Berubah?.Invoke();
    }

    // Kosongkan catatan setiap kali Play dimulai.
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSaatPlay()
    {
        daftar.Clear();
        Berubah = null;
    }
}
