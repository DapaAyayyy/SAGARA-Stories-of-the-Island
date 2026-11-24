using System;
using UnityEngine;

/// <summary>Satu quest dalam rangkaian quest NPC, lengkap dengan dialognya.</summary>
[Serializable]
public sealed class NpcQuestStep
{
    public string id = "";
    public string judul = "";
    [TextArea(1, 3)] public string tujuan = "";
    public string pesanLapor = "";

    [Tooltip("Jenis kegiatan yang dihitung: \"Sampah\" atau \"Panen\" (lihat QuestLog).")]
    public string jenis = QuestLog.JenisSampah;
    [Min(1)] public int target = 1;

    [Tooltip("Opsional. Objek yang baru muncul saat quest ini dimulai, misalnya grup sampah di alun-alun.")]
    public GameObject munculSaatAktif;

    [Tooltip("Diucapkan NPC saat memberi quest.")]
    public NpcDialogueLine[] tawaranLines = Array.Empty<NpcDialogueLine>();
    [Tooltip("Diucapkan saat quest belum selesai. {0} = progres, {1} = target.")]
    public NpcDialogueLine[] pengingatLines = Array.Empty<NpcDialogueLine>();
    [Tooltip("Diucapkan saat pemain melapor bahwa quest selesai.")]
    public NpcDialogueLine[] selesaiLines = Array.Empty<NpcDialogueLine>();
}

/// <summary>
/// Dipasang di NPC pemberi quest (Bu Ratna), di samping <see cref="NpcDialogue"/>.
/// Berisi rangkaian quest yang dikerjakan berurutan:
/// 1. Bersihkan Alun-Alun: kumpulkan 5 sampah di alun-alun.
/// 2. Kebun untuk Desa: panen 3 wortel di kebun dekat rumah.
/// Setelah semuanya selesai, Bu Ratna menyuruh pemain menemui Pak Darma di dermaga.
///
/// Setiap kali diajak bicara, Bu Ratna memberi quest baru, mengingatkan progres,
/// atau menerima laporan lalu langsung memberi quest berikutnya.
/// Progres dicatat lewat QuestLog.Laporkan(...) dari gameplay (pickup sampah dan panen).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcDialogue))]
public sealed class NpcQuest : MonoBehaviour
{
    [SerializeField] private NpcQuestStep[] quests =
    {
        new NpcQuestStep
        {
            id = "BersihkanAlunAlun",
            judul = "Bersihkan Alun-Alun",
            tujuan = "Kumpulkan sampah yang berserakan di sekitar alun-alun.",
            pesanLapor = "Kembali ke Bu Ratna di Balai Desa.",
            jenis = QuestLog.JenisSampah,
            target = 5,
            tawaranLines = new[]
            {
                Line(NpcExpression.Khawatir, "Lihat alun-alun kita... Sejak badai, sampah berserakan di mana-mana."),
                Line(NpcExpression.Serius, "Boleh saya minta tolong? Kumpulkan 5 sampah di sekitar alun-alun, ya."),
                Line(NpcExpression.Ramah, "Kalau sudah terkumpul semua, kembali ke sini."),
            },
            pengingatLines = new[]
            {
                Line(NpcExpression.Ramah, "Sampah yang terkumpul baru {0} dari {1}. Coba cari lagi di sekitar alun-alun, ya."),
            },
            selesaiLines = new[]
            {
                Line(NpcExpression.Senang, "Wah, alun-alun jadi bersih sekali! Terima kasih banyak, ya."),
                Line(NpcExpression.Bangga, "Kamu memang bisa diandalkan. Desa ini butuh orang sepertimu."),
            },
        },
        new NpcQuestStep
        {
            id = "KebunUntukDesa",
            judul = "Kebun untuk Desa",
            tujuan = "Cangkul, tanam, siram, lalu panen wortel di kebun dekat rumah.",
            pesanLapor = "Kembali ke Bu Ratna di Balai Desa.",
            jenis = QuestLog.JenisPanen,
            target = 3,
            tawaranLines = new[]
            {
                Line(NpcExpression.Serius, "Satu lagi, ya. Sejak badai, warga desa kekurangan bahan makanan."),
                Line(NpcExpression.Ramah, "Kebun di dekat rumahmu masih bisa ditanami. Cangkul tanahnya, tanam benih, lalu siram setiap hari."),
                Line(NpcExpression.Ramah, "Panen 3 wortel, lalu kembali ke sini dan ceritakan hasilnya."),
            },
            pengingatLines = new[]
            {
                Line(NpcExpression.Ramah, "Hasil panenmu baru {0} dari {1}. Jangan lupa disiram, ya, supaya cepat tumbuh."),
            },
            selesaiLines = new[]
            {
                Line(NpcExpression.Senang, "Wortelnya segar sekali! Warga pasti senang mendapat hasil kebunmu."),
                Line(NpcExpression.Khawatir, "Tapi masih ada satu masalah besar. Dermaga kita rusak parah sejak badai."),
                Line(NpcExpression.Serius, "Pak Darma, nelayan desa, sedang kesulitan memperbaikinya sendirian."),
                Line(NpcExpression.Ramah, "Pergilah ke dermaga lewat jalan tenggara dan temui Pak Darma. Dia pasti senang dibantu."),
            },
        },
    };

    private void Start()
    {
        foreach (NpcQuestStep step in quests)
        {
            if (step.munculSaatAktif == null)
            {
                continue;
            }

            QuestStatus status = QuestLog.StatusDari(step.id);
            bool berjalan = status == QuestStatus.Aktif || status == QuestStatus.SiapDilaporkan;
            step.munculSaatAktif.SetActive(berjalan);

            // Kalau pemain sempat pindah scene lalu kembali, sampah yang sudah diambil jangan muncul lagi.
            QuestData quest = QuestLog.Cari(step.id);
            int sudahDiambil = quest == null ? 0 : quest.Progres;
            TrashPickup[] sampah = step.munculSaatAktif.GetComponentsInChildren<TrashPickup>(true);
            for (int i = 0; i < sampah.Length && i < sudahDiambil; i++)
            {
                sampah[i].gameObject.SetActive(false);
                Destroy(sampah[i].gameObject);
            }
        }
    }

    /// <summary>
    /// Dipanggil <see cref="NpcDialogue"/> saat menentukan kalimat sapaan.
    /// Mengembalikan kalimat sesuai quest yang sedang berjalan.
    /// </summary>
    public NpcDialogueLine[] PilihBaris(NpcDialogueLine[] sapaanBiasa)
    {
        int index = IndexSekarang();
        if (index < 0)
        {
            return sapaanBiasa; // semua quest sudah selesai
        }

        NpcQuestStep step = quests[index];
        switch (QuestLog.StatusDari(step.id))
        {
            case QuestStatus.BelumDiambil:
                // Quest pertama didahului sapaan perkenalan.
                return index == 0 ? Gabung(sapaanBiasa, step.tawaranLines) : step.tawaranLines;
            case QuestStatus.Aktif:
                QuestData quest = QuestLog.Cari(step.id);
                return IsiAngka(step.pengingatLines, quest.Progres, quest.Target);
            default:
                // Siap dilaporkan: terima kasih, lalu langsung tawarkan quest berikutnya (kalau ada).
                NpcQuestStep next = index + 1 < quests.Length ? quests[index + 1] : null;
                return next == null ? step.selesaiLines : Gabung(step.selesaiLines, next.tawaranLines);
        }
    }

    /// <summary>Dipanggil <see cref="NpcDialogue"/> saat percakapan selesai.</summary>
    public void SetelahBicara()
    {
        int index = IndexSekarang();
        if (index < 0)
        {
            return;
        }

        NpcQuestStep step = quests[index];
        switch (QuestLog.StatusDari(step.id))
        {
            case QuestStatus.BelumDiambil:
                MulaiQuest(step);
                break;
            case QuestStatus.SiapDilaporkan:
                QuestLog.Selesaikan(step.id);
                if (index + 1 < quests.Length)
                {
                    MulaiQuest(quests[index + 1]);
                }

                break;
        }
    }

    // Quest pertama yang belum selesai, atau -1 kalau semuanya selesai.
    private int IndexSekarang()
    {
        for (int i = 0; i < quests.Length; i++)
        {
            if (QuestLog.StatusDari(quests[i].id) != QuestStatus.Selesai)
            {
                return i;
            }
        }

        return -1;
    }

    private static void MulaiQuest(NpcQuestStep step)
    {
        QuestLog.Mulai(step.id, step.judul, step.tujuan, step.pesanLapor, step.jenis, step.target);
        if (step.munculSaatAktif != null)
        {
            step.munculSaatAktif.SetActive(true);
        }
    }

    private static NpcDialogueLine Line(NpcExpression expression, string text) =>
        new NpcDialogueLine { expression = expression, text = text };

    private static NpcDialogueLine[] Gabung(NpcDialogueLine[] a, NpcDialogueLine[] b)
    {
        a = a ?? Array.Empty<NpcDialogueLine>();
        b = b ?? Array.Empty<NpcDialogueLine>();
        var hasil = new NpcDialogueLine[a.Length + b.Length];
        a.CopyTo(hasil, 0);
        b.CopyTo(hasil, a.Length);
        return hasil;
    }

    private static NpcDialogueLine[] IsiAngka(NpcDialogueLine[] lines, int progres, int target)
    {
        var hasil = new NpcDialogueLine[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            hasil[i] = Line(lines[i].expression, string.Format(lines[i].text, progres, target));
        }

        return hasil;
    }
}
