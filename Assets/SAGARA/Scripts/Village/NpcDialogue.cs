using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ekspresi wajah NPC yang dipakai potret dialog.</summary>
public enum NpcExpression
{
    Ramah,
    Senang,
    Bangga,
    Serius,
    Khawatir,
}

/// <summary>Satu kalimat dialog beserta ekspresi potret yang ditampilkan.</summary>
[Serializable]
public sealed class NpcDialogueLine
{
    public NpcExpression expression = NpcExpression.Ramah;
    [TextArea(2, 4)] public string text = "";
}

/// <summary>Satu pilihan pertanyaan yang bisa dipilih MC, beserta jawaban NPC.</summary>
[Serializable]
public sealed class NpcDialogueTopic
{
    public string question = "";
    public NpcDialogueLine[] answer = Array.Empty<NpcDialogueLine>();
}

/// <summary>
/// Dipasang di NPC (misalnya Bu Ratna, kepala desa). Menyimpan nama, jabatan, potret per ekspresi,
/// dan isi dialog. Alurnya: sapaan (pertama kali / kunjungan berikutnya) → NPC bertanya "ada yang ingin
/// ditanyakan?" → MC memilih pertanyaan → NPC menjawab → kembali ke pilihan → MC pamit → salam penutup.
/// NPC menoleh ke Player selama diajak bicara. Kalau NPC juga punya <see cref="NpcQuest"/>,
/// sapaannya diganti sesuai tahap quest (meminta bantuan, mengingatkan, atau berterima kasih).
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcDialogue : MonoBehaviour
{
    // Daftar semua NPC aktif, supaya Player bisa mencari NPC terdekat tanpa Physics.
    private static readonly HashSet<NpcDialogue> activeNpcs = new HashSet<NpcDialogue>();

    [SerializeField] private string displayName = "Bu Ratna";
    [SerializeField] private string role = "Kepala Desa";
    [SerializeField, Min(0.5f)] private float talkDistance = 2.2f;
    [SerializeField, Min(0f)] private float turnSpeed = 360f;

    [Header("Potret (urutan: Ramah, Senang, Bangga, Serius, Khawatir)")]
    [SerializeField] private Texture2D[] portraits = new Texture2D[5];

    [Header("Sapaan")]
    [SerializeField] private NpcDialogueLine[] firstLines = Array.Empty<NpcDialogueLine>();
    [SerializeField] private NpcDialogueLine[] repeatLines = Array.Empty<NpcDialogueLine>();

    [Header("Pilihan pertanyaan MC")]
    [SerializeField] private NpcDialogueLine choicePrompt = new NpcDialogueLine { text = "Ada yang ingin kamu tanyakan?" };
    [SerializeField] private NpcDialogueTopic[] topics = Array.Empty<NpcDialogueTopic>();
    [SerializeField] private string farewellOption = "Tidak ada, terima kasih.";
    [SerializeField] private NpcDialogueLine[] farewellLines = Array.Empty<NpcDialogueLine>();

    private Transform lookTarget;
    private Quaternion restRotation;
    private NpcQuest quest;

    public static IEnumerable<NpcDialogue> ActiveNpcs => activeNpcs;
    public string DisplayName => displayName;
    public string Role => role;
    public float TalkDistance => talkDistance;
    public int TimesTalked { get; private set; }

    // Sapaan biasa: dialog pertama, atau dialog ulang setelah pernah bicara.
    private NpcDialogueLine[] GreetingLines =>
        TimesTalked > 0 && repeatLines != null && repeatLines.Length > 0 ? repeatLines : firstLines;

    /// <summary>Dialog yang dipakai sekarang. Kalau NPC punya quest, kalimatnya mengikuti tahap quest.</summary>
    public NpcDialogueLine[] CurrentLines => quest != null ? quest.PilihBaris(GreetingLines) : GreetingLines;

    public bool HasDialogue => (GreetingLines != null && GreetingLines.Length > 0) || HasTopics || quest != null;
    public bool HasTopics => topics != null && topics.Length > 0;
    public NpcDialogueLine ChoicePrompt => choicePrompt;
    public int TopicCount => topics == null ? 0 : topics.Length;
    public NpcDialogueTopic GetTopic(int index) => topics[index];
    public string FarewellOption => farewellOption;
    public NpcDialogueLine[] FarewellLines => farewellLines ?? Array.Empty<NpcDialogueLine>();

    public Texture2D GetPortrait(NpcExpression expression)
    {
        int index = (int)expression;
        if (portraits != null && index >= 0 && index < portraits.Length && portraits[index] != null)
        {
            return portraits[index];
        }

        // Kalau potret ekspresi itu kosong, pakai potret Ramah.
        return portraits != null && portraits.Length > 0 ? portraits[0] : null;
    }

    private void Awake()
    {
        restRotation = transform.rotation;
        quest = GetComponent<NpcQuest>();
    }

    private void OnEnable()
    {
        activeNpcs.Add(this);
    }

    private void OnDisable()
    {
        activeNpcs.Remove(this);
        lookTarget = null;
    }

    public void BeginTalk(Transform player)
    {
        lookTarget = player;
    }

    public void EndTalk()
    {
        lookTarget = null;
        TimesTalked++;
        if (quest != null)
        {
            quest.SetelahBicara();
        }
    }

    private void Update()
    {
        // Saat bicara menoleh ke Player, setelah selesai kembali ke arah semula.
        Quaternion target = restRotation;
        if (lookTarget != null)
        {
            Vector3 toPlayer = lookTarget.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                target = Quaternion.LookRotation(toPlayer);
            }
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        // Lingkaran hijau di Scene = jarak Player bisa mengajak bicara.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, talkDistance);
    }
}
