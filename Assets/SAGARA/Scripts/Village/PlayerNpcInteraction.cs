using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dipasang di Player. Mengatur percakapan dengan NPC:
/// E untuk mulai bicara → NPC menyapa (teks diketik perlahan, E untuk lanjut) →
/// muncul pilihan pertanyaan (W/S atau panah untuk memilih, E/Enter untuk memilih, atau angka 1-9) →
/// NPC menjawab → kembali ke pilihan → pilih "Tidak ada, terima kasih" untuk pamit. Esc menutup kapan saja.
/// Selama percakapan Player tidak bisa berjalan.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerNpcInteraction : MonoBehaviour
{
    private static readonly int IsWalkingParameter = Animator.StringToHash("isWalking");

    private enum Phase
    {
        None,
        Lines,
        Choosing,
    }

    [SerializeField, Min(1f)] private float charactersPerSecond = 40f;

    private readonly List<string> choices = new List<string>();
    private readonly HashSet<int> askedTopics = new HashSet<int>();
    private PlayerMovement movement;
    private Animator animator;
    private PlayerInfoBoardInteraction infoBoard;
    private PlayerTrashInteraction trash;
    private Phase phase;
    private NpcDialogueLine[] activeLines;
    private bool endAfterLines;
    private float typedCharacters;

    public NpcDialogue Target { get; private set; }
    public NpcDialogue Speaker { get; private set; }
    public int LineIndex { get; private set; }
    public int ChoiceIndex { get; private set; }
    public bool IsTalking => Speaker != null;
    public bool IsChoosing => IsTalking && phase == Phase.Choosing;
    public IReadOnlyList<string> Choices => choices;
    public string ActionPrompt => Target == null ? string.Empty : "Tekan [E] untuk bicara dengan " + Target.DisplayName;

    /// <summary>Kalimat yang sedang tampil. Saat memilih, yang tampil adalah pertanyaan NPC.</summary>
    public NpcDialogueLine CurrentLine
    {
        get
        {
            if (!IsTalking)
            {
                return null;
            }

            if (phase == Phase.Choosing)
            {
                return Speaker.ChoicePrompt;
            }

            return activeLines != null && LineIndex < activeLines.Length ? activeLines[LineIndex] : null;
        }
    }

    /// <summary>Teks yang sudah "terketik" (efek mesin ketik seperti game Story of Seasons).</summary>
    public string VisibleText
    {
        get
        {
            NpcDialogueLine line = CurrentLine;
            if (line == null || string.IsNullOrEmpty(line.text))
            {
                return string.Empty;
            }

            if (phase == Phase.Choosing)
            {
                return line.text;
            }

            int count = Mathf.Clamp(Mathf.FloorToInt(typedCharacters), 0, line.text.Length);
            return line.text.Substring(0, count);
        }
    }

    public bool IsLineComplete
    {
        get
        {
            NpcDialogueLine line = CurrentLine;
            return phase == Phase.Choosing || line == null || typedCharacters >= line.text.Length;
        }
    }

    /// <summary>True kalau pilihan nomor <paramref name="index"/> sudah pernah ditanyakan.</summary>
    public bool WasAsked(int index) => askedTopics.Contains(index);

    /// <summary>Mulai percakapan dengan NPC terdekat (dipanggil dari klik tombol interaksi HUD).</summary>
    public bool TryStartTalkTarget()
    {
        if (IsTalking || Target == null)
        {
            return false;
        }

        StartTalk(Target);
        return true;
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        animator = GetComponentInChildren<Animator>();
        infoBoard = GetComponent<PlayerInfoBoardInteraction>();
        trash = GetComponent<PlayerTrashInteraction>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (IsTalking)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                EndTalk();
            }
            else if (phase == Phase.Choosing)
            {
                UpdateChoosing(keyboard);
            }
            else
            {
                typedCharacters += charactersPerSecond * Time.unscaledDeltaTime;
                if (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                {
                    Advance();
                }
            }

            return;
        }

        // Jangan berebut tombol E dengan papan informasi atau sampah yang bisa diambil.
        bool boardBusy = infoBoard != null && (infoBoard.IsReading || infoBoard.Target != null);
        bool trashBusy = trash != null && trash.isActiveAndEnabled && trash.HasInteractionPriority;
        Target = boardBusy || trashBusy ? null : FindTarget();
        if (Target != null && keyboard.eKey.wasPressedThisFrame)
        {
            StartTalk(Target);
        }
    }

    private void UpdateChoosing(Keyboard keyboard)
    {
        int count = choices.Count;
        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
        {
            ChoiceIndex = (ChoiceIndex - 1 + count) % count;
        }
        else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
        {
            ChoiceIndex = (ChoiceIndex + 1) % count;
        }

        // Angka 1-9 langsung memilih.
        Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        for (int i = 0; i < digits.Length && i < count; i++)
        {
            if (keyboard[digits[i]].wasPressedThisFrame)
            {
                Choose(i);
                return;
            }
        }

        if (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
        {
            Choose(ChoiceIndex);
        }
    }

    private NpcDialogue FindTarget()
    {
        NpcDialogue nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (NpcDialogue npc in NpcDialogue.ActiveNpcs)
        {
            if (npc == null || !npc.HasDialogue)
            {
                continue;
            }

            Vector3 offset = npc.transform.position - transform.position;
            if (Mathf.Abs(offset.y) > 1.5f)
            {
                continue;
            }

            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            float range = npc.TalkDistance;
            if (distance <= range * range && distance < nearestDistance)
            {
                nearest = npc;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void StartTalk(NpcDialogue npc)
    {
        Speaker = npc;
        Target = null;
        askedTopics.Clear();
        BuildChoices();
        npc.BeginTalk(transform);
        SetMovementLocked(true);

        // Sapaan dulu. Kalau NPC punya pilihan pertanyaan, setelah sapaan masuk ke menu pilihan.
        PlayLines(npc.CurrentLines, endAfter: !npc.HasTopics);
        if (activeLines == null || activeLines.Length == 0)
        {
            OpenChoices();
        }
    }

    private void BuildChoices()
    {
        choices.Clear();
        for (int i = 0; i < Speaker.TopicCount; i++)
        {
            choices.Add(Speaker.GetTopic(i).question);
        }

        choices.Add(Speaker.FarewellOption);
    }

    private void PlayLines(NpcDialogueLine[] lines, bool endAfter)
    {
        activeLines = lines;
        endAfterLines = endAfter;
        LineIndex = 0;
        typedCharacters = 0f;
        phase = Phase.Lines;
    }

    private void OpenChoices()
    {
        phase = Phase.Choosing;
        ChoiceIndex = Mathf.Clamp(ChoiceIndex, 0, choices.Count - 1);
    }

    private void Advance()
    {
        NpcDialogueLine line = CurrentLine;
        if (line != null && !IsLineComplete)
        {
            // Tekan E saat teks masih diketik: langsung tampilkan seluruh kalimat.
            typedCharacters = line.text.Length;
            return;
        }

        LineIndex++;
        typedCharacters = 0f;
        if (activeLines == null || LineIndex >= activeLines.Length)
        {
            if (endAfterLines)
            {
                EndTalk();
            }
            else
            {
                OpenChoices();
            }
        }
    }

    private void Choose(int index)
    {
        if (index < Speaker.TopicCount)
        {
            askedTopics.Add(index);
            ChoiceIndex = index;
            PlayLines(Speaker.GetTopic(index).answer, endAfter: false);
            return;
        }

        // Pilihan terakhir = pamit.
        NpcDialogueLine[] farewell = Speaker.FarewellLines;
        if (farewell.Length == 0)
        {
            EndTalk();
            return;
        }

        PlayLines(farewell, endAfter: true);
    }

    private void EndTalk()
    {
        if (Speaker != null)
        {
            Speaker.EndTalk();
        }

        Speaker = null;
        phase = Phase.None;
        activeLines = null;
        LineIndex = 0;
        ChoiceIndex = 0;
        typedCharacters = 0f;
        SetMovementLocked(false);
    }

    private void SetMovementLocked(bool locked)
    {
        if (movement != null)
        {
            movement.enabled = !locked;
        }

        // PlayerMovement berhenti di-update saat dimatikan, jadi animasi jalan dihentikan di sini.
        if (locked && animator != null)
        {
            animator.SetBool(IsWalkingParameter, false);
        }
    }

    private void OnDisable()
    {
        if (IsTalking)
        {
            EndTalk();
        }

        Target = null;
    }
}
