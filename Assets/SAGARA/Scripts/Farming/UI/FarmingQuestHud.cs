using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class FarmingQuestHud : MonoBehaviour
{
    private const string QuestId = "KebunUntukDesa";

    [SerializeField] private FontAsset displayFont;

    private VisualElement tracker;
    private Label title;
    private Label objective;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        tracker = root.Q("FarmingQuestTracker");
        title = root.Q<Label>("FarmingQuestTitle");
        objective = root.Q<Label>("FarmingQuestObjective");

        if (displayFont != null)
        {
            title.style.unityFontDefinition = FontDefinition.FromSDFFont(displayFont);
            objective.style.unityFontDefinition = FontDefinition.FromSDFFont(displayFont);
        }

        root.Q("FarmingQuestScrim").generateVisualContent += DrawScrim;
        root.Q("FarmingQuestRule").generateVisualContent += DrawRule;
        QuestLog.Berubah += RefreshQuest;
        RefreshQuest();
    }

    private void OnDisable()
    {
        QuestLog.Berubah -= RefreshQuest;
    }

    private void RefreshQuest()
    {
        QuestData quest = QuestLog.Cari(QuestId);
        bool readyToReport = quest?.Status == QuestStatus.SiapDilaporkan;
        bool visible = readyToReport || quest?.Status == QuestStatus.Aktif;
        tracker.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) return;

        title.text = quest.Judul;
        objective.text = readyToReport ? quest.PesanLapor : $"Panen wortel {quest.Progres}/{quest.Target}";
    }

    private static void DrawScrim(MeshGenerationContext context)
    {
        DrawFade(context, new Color32(10, 28, 34, 142));
    }

    private static void DrawRule(MeshGenerationContext context)
    {
        DrawFade(context, new Color32(252, 218, 128, 245));
    }

    private static void DrawFade(MeshGenerationContext context, Color32 color)
    {
        Rect rect = context.visualElement.contentRect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        var transparent = new Color32(color.r, color.g, color.b, 0);
        MeshWriteData mesh = context.Allocate(4, 6);
        mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = color });
        mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = transparent });
        mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = transparent });
        mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = color });
        mesh.SetNextIndex(0);
        mesh.SetNextIndex(1);
        mesh.SetNextIndex(2);
        mesh.SetNextIndex(0);
        mesh.SetNextIndex(2);
        mesh.SetNextIndex(3);
    }
}
