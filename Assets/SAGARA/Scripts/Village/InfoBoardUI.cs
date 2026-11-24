using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Tampilan papan informasi (UI Toolkit). Menampilkan petunjuk "[E] Baca Papan Informasi"
/// saat Player dekat papan, dan panel berisi judul serta daftar pengumuman saat papan dibuka.
/// Dipasang di objek yang punya komponen UIDocument (Panel Settings: FarmingPanelSettings).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class InfoBoardUI : MonoBehaviour
{
    private static readonly Color Wood = new Color(0.55f, 0.35f, 0.2f);
    private static readonly Color Paper = new Color(1f, 0.97f, 0.88f, 0.97f);
    private static readonly Color Ink = new Color(0.2f, 0.17f, 0.15f);

    // Kelas dari FarmingHud.uss milik tim. Kelas ini memberi font FarmingFont ke semua teks di dalamnya.
    private const string TeamFontClass = "farming-hud-root";

    [SerializeField] private PlayerInfoBoardInteraction player;

    private VisualElement container;
    private VisualElement promptRow;
    private Label prompt;
    private VisualElement panel;
    private Label titleLabel;
    private VisualElement list;
    private InfoBoard shownBoard;
    private PlayerTrashInteraction trash;
    private int lastTrashFrame = -1;
    private float messageUntil;

    private void OnEnable()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerInfoBoardInteraction>();
        }

        // Wadah selebar layar. Tidak menangkap klik, hanya tempat menaruh prompt dan panel.
        container = new VisualElement { pickingMode = PickingMode.Ignore };
        container.AddToClassList(TeamFontClass);
        container.style.position = Position.Absolute;
        container.style.left = 0;
        container.style.right = 0;
        container.style.top = 0;
        container.style.bottom = 0;
        SetPadding(container, 0, 0);

        // Baris bawah layar untuk tulisan petunjuk, isinya di tengah.
        promptRow = new VisualElement { pickingMode = PickingMode.Ignore };
        promptRow.style.position = Position.Absolute;
        promptRow.style.left = 0;
        promptRow.style.right = 0;
        promptRow.style.bottom = 40;
        promptRow.style.flexDirection = FlexDirection.Row;
        promptRow.style.justifyContent = Justify.Center;

        prompt = new Label();
        prompt.style.fontSize = 22;
        prompt.style.color = Color.white;
        prompt.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
        SetPadding(prompt, 10, 20);
        SetRadius(prompt, 10);
        promptRow.Add(prompt);

        panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = Length.Percent(20);
        panel.style.right = Length.Percent(20);
        panel.style.top = Length.Percent(12);
        panel.style.backgroundColor = Paper;
        SetPadding(panel, 18, 24);
        SetRadius(panel, 12);
        SetBorder(panel, 6, Wood);

        titleLabel = new Label();
        titleLabel.style.fontSize = 24;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.color = Wood;
        titleLabel.style.marginBottom = 12;

        list = new VisualElement();

        var hint = new Label("[E] / [Esc] Tutup");
        hint.style.fontSize = 14;
        hint.style.alignSelf = Align.FlexEnd;
        hint.style.marginTop = 12;
        hint.style.color = new Color(0.45f, 0.4f, 0.35f);

        panel.Add(titleLabel);
        panel.Add(list);
        panel.Add(hint);
        container.Add(promptRow);
        container.Add(panel);

        shownBoard = null;
        Refresh();
    }

    private void OnDisable()
    {
        container?.RemoveFromHierarchy();
        shownBoard = null;
    }

    private void Update()
    {
        Refresh();
    }

    /// <summary>
    /// UIDocument bisa membangun ulang root-nya setelah OnEnable (misalnya saat Play dimulai),
    /// sehingga elemen yang kita tambahkan ikut terhapus. Pasang ulang kalau itu terjadi.
    /// </summary>
    private void EnsureAttached()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            return;
        }

        // Tanpa Source Asset (UXML), root UIDocument bisa bertinggi 0 piksel sehingga isinya
        // tergambar di luar layar. Paksa root memenuhi seluruh layar.
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;

        if (container.parent != root)
        {
            container.RemoveFromHierarchy();
            root.Add(container);
        }
    }

    // Prompt "[E] Ambil Sampah" saat dekat sampah, lalu sebentar menampilkan progres quest setelah sampah diambil.
    private string TrashPromptText()
    {
        if (trash == null && player != null)
        {
            trash = player.GetComponent<PlayerTrashInteraction>();
        }

        if (trash == null || !trash.isActiveAndEnabled)
        {
            return string.Empty;
        }

        if (trash.LastActionFrame != lastTrashFrame)
        {
            lastTrashFrame = trash.LastActionFrame;
            messageUntil = Time.unscaledTime + 2.5f;
        }

        if (trash.Target != null)
        {
            return trash.ActionPrompt;
        }

        if (Time.unscaledTime < messageUntil)
        {
            QuestData quest = QuestLog.QuestBerjalan;
            return quest != null ? trash.LastMessage + "  " + quest.TeksSingkat : trash.LastMessage;
        }

        return string.Empty;
    }

    private void Refresh()
    {
        if (container == null)
        {
            return;
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerInfoBoardInteraction>();
        }

        EnsureAttached();

        bool reading = player != null && player.IsReading;
        string promptText = reading || player == null ? string.Empty : player.ActionPrompt;
        if (promptText.Length == 0 && !reading)
        {
            promptText = TrashPromptText();
        }

        panel.style.display = reading ? DisplayStyle.Flex : DisplayStyle.None;
        promptRow.style.display = promptText.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        prompt.text = promptText;

        // Isi panel hanya dibangun ulang saat papan yang dibuka berganti.
        InfoBoard board = reading ? player.OpenBoard : null;
        if (board == shownBoard)
        {
            return;
        }

        shownBoard = board;
        list.Clear();
        if (board == null)
        {
            return;
        }

        titleLabel.text = board.Title;
        for (int i = 0; i < board.EntryCount; i++)
        {
            var entry = new Label("•  " + board.GetEntry(i));
            entry.style.fontSize = 18;
            entry.style.whiteSpace = WhiteSpace.Normal;
            entry.style.color = Ink;
            entry.style.marginBottom = 8;
            list.Add(entry);
        }
    }

    private static void SetPadding(VisualElement e, float vertical, float horizontal)
    {
        e.style.paddingTop = vertical;
        e.style.paddingBottom = vertical;
        e.style.paddingLeft = horizontal;
        e.style.paddingRight = horizontal;
    }

    private static void SetRadius(VisualElement e, float radius)
    {
        e.style.borderTopLeftRadius = radius;
        e.style.borderTopRightRadius = radius;
        e.style.borderBottomLeftRadius = radius;
        e.style.borderBottomRightRadius = radius;
    }

    private static void SetBorder(VisualElement e, float width, Color color)
    {
        e.style.borderTopWidth = width;
        e.style.borderBottomWidth = width;
        e.style.borderLeftWidth = width;
        e.style.borderRightWidth = width;
        e.style.borderTopColor = color;
        e.style.borderBottomColor = color;
        e.style.borderLeftColor = color;
        e.style.borderRightColor = color;
    }
}
