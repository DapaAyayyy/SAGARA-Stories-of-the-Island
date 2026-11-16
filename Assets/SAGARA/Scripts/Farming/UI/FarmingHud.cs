using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class FarmingHud : MonoBehaviour
{
    [SerializeField] private PlayerFarmingInteraction player;
    [SerializeField] private PlayerTrashInteraction trashPlayer;

    private Label cropName;
    private Label stock;
    private Label prompt;
    private Label feedback;
    private Label trashStock;
    private IVisualElementScheduledItem refresh;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        root.AddToClassList("farming-document");
        cropName = root.Q<Label>("CropName");
        stock = root.Q<Label>("Stock");
        prompt = root.Q<Label>("Prompt");
        feedback = root.Q<Label>("Feedback");
        trashStock = root.Q<Label>("TrashStock");
        refresh = root.schedule.Execute(Refresh).Every(150);
    }

    private void Refresh()
    {
        if (player == null || player.Inventory == null || cropName == null
            || stock == null || prompt == null || feedback == null)
        {
            return;
        }

        FarmInventory inventory = player.Inventory;
        stock.text = "Benih " + inventory.SeedCount + "   ·   Panen " + inventory.HarvestCount;
        bool trashAvailable = trashPlayer != null && trashPlayer.isActiveAndEnabled;
        cropName.text = trashAvailable && trashPlayer.Target != null ? "Bersih-bersih"
            : inventory.Crop == null ? "Kebun" : inventory.Crop.DisplayName;
        if (trashStock != null)
        {
            int total = trashAvailable && trashPlayer.Inventory != null
                ? trashPlayer.Inventory.TotalCollected : 0;
            trashStock.text = "Sampah terkumpul " + total;
        }

        prompt.text = trashAvailable && trashPlayer.Target != null
            ? trashPlayer.ActionPrompt : player.ActionPrompt;
        feedback.text = trashAvailable && trashPlayer.LastActionFrame > player.LastActionFrame
            ? trashPlayer.LastMessage : player.LastMessage;
    }

    private void OnDisable()
    {
        refresh?.Pause();
    }
}

