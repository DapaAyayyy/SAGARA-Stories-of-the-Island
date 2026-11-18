using System.Collections.Generic;
using UnityEngine;
using UIImage = UnityEngine.UIElements.Image;
using UnityEngine.UIElements;

/// <summary>
/// Tampilan dialog NPC bergaya Story of Seasons (UI Toolkit):
/// kotak krem bergradasi selebar layar dengan ornamen sulur di kedua sudut, potret ekspresi NPC besar
/// di kanan, papan nama merah muda berbingkai putih, teks miring cokelat yang diketik
/// perlahan, dan segitiga hijau yang memantul saat kalimat selesai.
/// Juga menampilkan petunjuk "Tekan [E] untuk bicara ..." dan panel pilihan pertanyaan MC.
/// Dipasang di objek yang punya komponen UIDocument (Panel Settings: FarmingPanelSettings).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class NpcDialogueUI : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.33f, 0.22f, 0.13f);
    private static readonly Color SoftInk = new Color(0.55f, 0.45f, 0.36f);
    private static readonly Color Pink = new Color(0.7f, 0.15f, 0.41f);
    private static readonly Color Cream = new Color(1f, 0.97f, 0.9f, 0.94f);

    // Kelas dari FarmingHud.uss milik tim: memberi font tim ke tulisan petunjuk "Tekan [E]".
    private const string TeamFontClass = "farming-hud-root";

    [SerializeField] private PlayerNpcInteraction player;

    [Header("Font kotak dialog")]
    [Tooltip("Font teks dialog (Nunito).")]
    [SerializeField] private Font bodyFont;
    [Tooltip("Font papan nama (Fredoka).")]
    [SerializeField] private Font titleFont;

    [Header("Gambar kotak dialog (folder UI/Dialog)")]
    [SerializeField] private Texture2D boxTexture;
    [SerializeField] private Texture2D ornamentTexture;
    [SerializeField] private Texture2D nameTagTexture;
    [SerializeField] private Texture2D nextTexture;

    private readonly List<Label> choiceLabels = new List<Label>();
    private VisualElement container;
    private VisualElement promptRow;
    private Label prompt;
    private VisualElement dialog;
    private UIImage portrait;
    private Label nameLabel;
    private Label roleLabel;
    private Label textLabel;
    private VisualElement nextMark;
    private VisualElement choicePanel;

    private void OnEnable()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerNpcInteraction>();
        }

        container = Full(new VisualElement { pickingMode = PickingMode.Ignore });
        container.AddToClassList(TeamFontClass);
        SetPadding(container, 0, 0);

        BuildPrompt();
        BuildDialog();

        container.Add(promptRow);
        container.Add(dialog);
        Refresh();
    }

    // ------------------------------------------------------------------ petunjuk "Tekan [E]" (gaya awal)

    private void BuildPrompt()
    {
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
    }

    // ------------------------------------------------------------------ kotak dialog

    private void BuildDialog()
    {
        dialog = Full(new VisualElement { pickingMode = PickingMode.Ignore });
        ApplyFont(dialog, bodyFont);

        // Potret besar di kanan; bagian bawahnya berada di belakang kotak dialog.
        portrait = new UIImage { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
        portrait.style.position = Position.Absolute;
        portrait.style.right = Length.Percent(5);
        portrait.style.bottom = Length.Percent(5);
        portrait.style.width = Length.Percent(32);
        portrait.style.height = Length.Percent(80);

        // Kotak selebar layar: gambar gradasi krem dengan pita cokelat di tepi atas.
        var box = new UIImage { image = boxTexture, scaleMode = ScaleMode.StretchToFill };
        box.style.position = Position.Absolute;
        box.style.left = 0;
        box.style.right = 0;
        box.style.bottom = 0;
        box.style.height = Length.Percent(28);
        if (boxTexture == null)
        {
            box.style.backgroundColor = Cream;
        }

        // Ornamen sulur di sudut kiri dan kanan (kanan = dicerminkan).
        box.Add(Ornament(left: true));
        box.Add(Ornament(left: false));

        // Teks dialog.
        textLabel = new Label();
        textLabel.style.position = Position.Absolute;
        textLabel.style.left = Length.Percent(12);
        textLabel.style.right = Length.Percent(14);
        textLabel.style.top = 46;
        textLabel.style.fontSize = 31;
        textLabel.style.whiteSpace = WhiteSpace.Normal;
        textLabel.style.color = Ink;
        textLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        box.Add(textLabel);

        // Segitiga hijau di kanan bawah.
        nextMark = new UIImage { image = nextTexture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
        nextMark.style.position = Position.Absolute;
        nextMark.style.right = Length.Percent(9);
        nextMark.style.bottom = 16;
        nextMark.style.width = 40;
        nextMark.style.height = 40;
        box.Add(nextMark);

        var hint = new Label("Esc: tutup");
        hint.style.position = Position.Absolute;
        hint.style.right = Length.Percent(13);
        hint.style.bottom = 24;
        hint.style.fontSize = 14;
        hint.style.color = SoftInk;
        box.Add(hint);

        dialog.Add(portrait);
        dialog.Add(box);
        dialog.Add(BuildNameRow());
        dialog.Add(BuildChoicePanel());
    }

    private UIImage Ornament(bool left)
    {
        var image = new UIImage { image = ornamentTexture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
        image.style.position = Position.Absolute;
        image.style.top = 18;
        image.style.width = 230;
        image.style.height = 128;
        if (left)
        {
            image.style.left = 14;
        }
        else
        {
            image.style.right = 14;
            image.style.scale = new Scale(new Vector3(-1f, 1f, 1f));
        }

        image.style.display = ornamentTexture != null ? DisplayStyle.Flex : DisplayStyle.None;
        return image;
    }

    /// <summary>Papan nama merah muda, duduk di garis atas kotak dialog dekat potret.</summary>
    private VisualElement BuildNameRow()
    {
        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.style.position = Position.Absolute;
        row.style.right = Length.Percent(21);
        row.style.bottom = Length.Percent(28);
        row.style.translate = new Translate(0, Length.Percent(50));
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        var tag = new VisualElement();
        tag.style.flexDirection = FlexDirection.Row;
        tag.style.alignItems = Align.Center;
        tag.style.height = 58;
        tag.style.paddingLeft = 26;
        tag.style.paddingRight = 26;
        tag.style.paddingBottom = 4;
        if (nameTagTexture != null)
        {
            // 9-slice: ujung bulat kiri-kanan tidak ikut melar saat nama panjang.
            tag.style.backgroundImage = new StyleBackground(nameTagTexture);
            tag.style.unitySliceLeft = 40;
            tag.style.unitySliceRight = 40;
            tag.style.unitySliceTop = 0;
            tag.style.unitySliceBottom = 0;
        }
        else
        {
            tag.style.backgroundColor = Pink;
            SetRadius(tag, 26);
            SetBorder(tag, 4, Color.white);
        }

        nameLabel = new Label();
        ApplyFont(nameLabel, titleFont);
        nameLabel.style.fontSize = 34;
        nameLabel.style.color = Color.white;
        nameLabel.style.unityTextOutlineWidth = 1.5f;
        nameLabel.style.unityTextOutlineColor = new Color(0.43f, 0.05f, 0.24f);

        roleLabel = new Label();
        ApplyFont(roleLabel, titleFont);
        roleLabel.style.fontSize = 17;
        roleLabel.style.marginLeft = 10;
        roleLabel.style.marginTop = 8;
        roleLabel.style.color = new Color(1f, 0.86f, 0.93f);

        tag.Add(nameLabel);
        tag.Add(roleLabel);
        row.Add(tag);
        return row;
    }

    /// <summary>Panel pilihan pertanyaan MC, di atas kotak dialog sebelah kiri (potret NPC di kanan).</summary>
    private VisualElement BuildChoicePanel()
    {
        choicePanel = new VisualElement();
        choicePanel.style.position = Position.Absolute;
        choicePanel.style.left = Length.Percent(10);
        choicePanel.style.bottom = Length.Percent(31);
        choicePanel.style.width = Length.Percent(46);
        choicePanel.style.backgroundColor = Cream;
        SetPadding(choicePanel, 12, 14);
        SetRadius(choicePanel, 20);
        SetBorder(choicePanel, 4, Color.white);
        return choicePanel;
    }

    // ------------------------------------------------------------------ update

    private void OnDisable()
    {
        container?.RemoveFromHierarchy();
        choiceLabels.Clear();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (container == null)
        {
            return;
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerNpcInteraction>();
        }

        EnsureAttached();

        bool talking = player != null && player.IsTalking;
        bool showPrompt = player != null && !talking && player.Target != null;
        dialog.style.display = talking ? DisplayStyle.Flex : DisplayStyle.None;
        promptRow.style.display = showPrompt ? DisplayStyle.Flex : DisplayStyle.None;

        if (showPrompt)
        {
            prompt.text = player.ActionPrompt;
        }

        if (!talking)
        {
            return;
        }

        NpcDialogue npc = player.Speaker;
        NpcDialogueLine line = player.CurrentLine;
        nameLabel.text = npc.DisplayName;
        roleLabel.text = npc.Role;
        roleLabel.style.display = string.IsNullOrEmpty(npc.Role) ? DisplayStyle.None : DisplayStyle.Flex;
        textLabel.text = player.VisibleText;

        Texture2D face = line != null ? npc.GetPortrait(line.expression) : null;
        if (portrait.image != face)
        {
            portrait.image = face;
        }

        portrait.style.display = face != null ? DisplayStyle.Flex : DisplayStyle.None;

        // Segitiga hijau memantul pelan saat kalimat sudah selesai diketik (tidak saat memilih).
        bool showNext = !player.IsChoosing && player.IsLineComplete;
        nextMark.style.opacity = showNext ? 1f : 0f;
        nextMark.style.translate = new Translate(0, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * -6f);

        RefreshChoices();
    }

    private void RefreshChoices()
    {
        bool choosing = player.IsChoosing;
        choicePanel.style.display = choosing ? DisplayStyle.Flex : DisplayStyle.None;
        if (!choosing)
        {
            return;
        }

        IReadOnlyList<string> choices = player.Choices;
        if (choiceLabels.Count != choices.Count)
        {
            choicePanel.Clear();
            choiceLabels.Clear();
            for (int i = 0; i < choices.Count; i++)
            {
                var item = new Label();
                item.style.fontSize = 24;
                item.style.whiteSpace = WhiteSpace.Normal;
                SetPadding(item, 8, 18);
                SetRadius(item, 16);
                item.style.marginBottom = 4;
                choicePanel.Add(item);
                choiceLabels.Add(item);
            }

            var hint = new Label("W/S: pindah    E: pilih");
            hint.style.fontSize = 14;
            hint.style.alignSelf = Align.FlexEnd;
            hint.style.marginTop = 4;
            hint.style.color = SoftInk;
            choicePanel.Add(hint);
        }

        for (int i = 0; i < choiceLabels.Count; i++)
        {
            bool selected = i == player.ChoiceIndex;
            bool asked = player.WasAsked(i);
            Label item = choiceLabels[i];
            item.text = choices[i];
            // Pilihan aktif: pita merah muda dengan teks putih, senada dengan papan nama.
            item.style.backgroundColor = selected ? Pink : Color.clear;
            item.style.color = selected ? Color.white : asked ? SoftInk : Ink;
            item.style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Italic;
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Tanpa Source Asset (UXML), root UIDocument bisa bertinggi 0 piksel dan bisa dibangun ulang saat Play
    /// dimulai. Paksa root memenuhi layar dan pasang ulang tampilan kalau terlepas.
    /// </summary>
    private void EnsureAttached()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            return;
        }

        root.pickingMode = PickingMode.Ignore;
        Full(root);
        if (container.parent != root)
        {
            container.RemoveFromHierarchy();
            root.Add(container);
        }
    }

    private static void ApplyFont(VisualElement e, Font font)
    {
        if (font != null)
        {
            e.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
        }
    }

    private static VisualElement Full(VisualElement e)
    {
        e.style.position = Position.Absolute;
        e.style.left = 0;
        e.style.right = 0;
        e.style.top = 0;
        e.style.bottom = 0;
        return e;
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
