using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(FarmInventory), typeof(FarmTileHighlighter))]
public sealed class PlayerFarmingInteraction : MonoBehaviour
{
    private static readonly int PickupTrigger = Animator.StringToHash("Pickup");
    private static readonly int PickupState = Animator.StringToHash("Base Layer.Pickup");
    private static readonly int IsWalkingParameter = Animator.StringToHash("isWalking");
    private const float HarvestTimeout = 2.5f;

    [SerializeField, Min(0.1f)] private float interactionDistance = 0.9f;

    public FarmTile TargetTile { get; private set; }
    public FarmInventory Inventory { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;
    public int LastActionFrame { get; private set; } = -1;
    public bool IsHarvesting { get; private set; }

    private FarmTileHighlighter highlighter;
    private PlayerTrashInteraction trashInteraction;
    private PlayerWateringInteraction wateringInteraction;
    private Animator animator;
    private PlayerPickupAnimationEvents pickupEvents;
    private FarmTile pendingHarvest;
    private float harvestDeadline;
    private bool UsesWateringCan => wateringInteraction != null && wateringInteraction.isActiveAndEnabled;
    private bool InteractionBlocked => IsHarvesting || SceneTransitionFade.IsTransitioning
        || (UsesWateringCan && wateringInteraction.IsBusy)
        || (trashInteraction != null && trashInteraction.isActiveAndEnabled
            && trashInteraction.HasInteractionPriority);

    public string ActionPrompt
    {
        get
        {
            if (InteractionBlocked) return string.Empty;
            if (UsesWateringCan && wateringInteraction.CanPickUp) return "[E] Ambil gembor";

            if (TargetTile == null)
            {
                return "Dekati dan hadapkan karakter ke petak.";
            }

            if (Inventory == null || Inventory.Crop == null)
            {
                return "Data tanaman belum terpasang.";
            }

            CropGrowth crop = TargetTile.Crop;
            if (crop == null)
            {
                return Inventory.SeedCount > 0
                    ? "[E] Tanam " + Inventory.Crop.DisplayName
                    : "Benih " + Inventory.Crop.DisplayName + " habis.";
            }

            if (crop.IsMature)
            {
                return "[E] Panen " + crop.Definition.DisplayName;
            }

            if (crop.IsWatered)
            {
                return "Sedang tumbuh · " + Mathf.CeilToInt(crop.SecondsRemaining) + " dtk";
            }

            return UsesWateringCan && !wateringInteraction.HasWateringCan
                ? "Ambil gembor di dekat sumur." : "[E] Siram " + crop.Definition.DisplayName;
        }
    }

    private void Awake()
    {
        Inventory = GetComponent<FarmInventory>();
        highlighter = GetComponent<FarmTileHighlighter>();
        trashInteraction = GetComponent<PlayerTrashInteraction>();
        wateringInteraction = GetComponent<PlayerWateringInteraction>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null) pickupEvents = animator.GetComponent<PlayerPickupAnimationEvents>();
    }

    private void OnEnable()
    {
        if (wateringInteraction != null) wateringInteraction.Feedback += ReportAction;
    }

    private void LateUpdate()
    {
        if (IsHarvesting && Time.time >= harvestDeadline) FinishHarvest();

        if (InteractionBlocked)
        {
            TargetTile = null;
            highlighter.Show(null);
            return;
        }

        TargetTile = null;
        if (!UsesWateringCan || !wateringInteraction.CanPickUp)
        {
            Vector3 probeOrigin = transform.position
                + transform.forward * interactionDistance + Vector3.up;
            if (Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit,
                2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                TargetTile = hit.collider.GetComponent<FarmTile>();
            }
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteractTarget();
        }

        highlighter.Show(TargetTile);
    }

    public bool TryInteractTarget()
    {
        if (InteractionBlocked) return false;

        if (UsesWateringCan && wateringInteraction.CanPickUp)
        {
            bool started = wateringInteraction.TryPickUp();
            ReportAction(started ? "Mengambil gembor..." : "Gembor belum bisa diambil.");
            return started;
        }

        if (TargetTile == null)
        {
            return false;
        }

        LastActionFrame = Time.frameCount;

        if (Inventory == null || Inventory.Crop == null)
        {
            LastMessage = "Data tanaman belum terpasang.";
            return false;
        }

        if (TargetTile.Crop == null)
        {
            return TryPlantTarget();
        }

        if (TargetTile.Crop.IsMature)
        {
            return BeginHarvest(TargetTile);
        }

        if (TargetTile.IsWatered)
        {
            LastMessage = "Tanaman sedang tumbuh.";
            return false;
        }

        if (UsesWateringCan)
        {
            if (!wateringInteraction.HasWateringCan)
            {
                LastMessage = "Ambil gembor di dekat sumur.";
                return false;
            }

            bool started = wateringInteraction.TryWater(TargetTile);
            LastMessage = started ? "Menyiram tanaman..." : "Tanaman belum bisa disiram.";
            return started;
        }

        if (TargetTile.TryWater())
        {
            LastMessage = "Tanaman disiram. Tunggu sampai tahap berikutnya.";
            return true;
        }

        LastMessage = "Tanaman sedang tumbuh.";
        return false;
    }

    private bool BeginHarvest(FarmTile tile)
    {
        if (pickupEvents == null || !pickupEvents.isActiveAndEnabled || !animator.isInitialized
            || !animator.HasState(0, PickupState))
        {
            return Harvest(tile);
        }

        pendingHarvest = tile;
        IsHarvesting = true;
        harvestDeadline = Time.time + HarvestTimeout;
        animator.SetBool(IsWalkingParameter, false);
        animator.SetTrigger(PickupTrigger);
        ReportAction("Memanen " + tile.Crop.Definition.DisplayName + "...");
        return true;
    }

    public void CollectPendingHarvest()
    {
        if (!IsHarvesting || pendingHarvest == null) return;

        FarmTile tile = pendingHarvest;
        pendingHarvest = null;
        if (!Harvest(tile)) ReportAction("Tanaman belum bisa dipanen.");
    }

    public void FinishHarvest()
    {
        if (!IsHarvesting) return;

        if (pendingHarvest != null) ReportAction("Panen terhenti. Tekan E untuk mencoba lagi.");
        pendingHarvest = null;
        IsHarvesting = false;
        if (animator != null && animator.isInitialized) animator.ResetTrigger(PickupTrigger);
    }

    private bool Harvest(FarmTile tile)
    {
        if (!tile.TryHarvest(out CropDefinition definition, out int amount)) return false;

        Inventory.AddHarvest(definition, amount);
        QuestLog.Laporkan(QuestLog.JenisPanen, amount);
        ReportAction("+" + amount + " " + definition.DisplayName + " masuk hasil panen.");
        return true;
    }

    private bool TryPlantTarget()
    {
        CropDefinition definition = Inventory.Crop;
        if (!Inventory.TryConsumeSeed(definition))
        {
            LastMessage = "Benih " + definition.DisplayName + " habis.";
            return false;
        }

        bool planted = false;
        try
        {
            planted = TargetTile.TryPlant(definition);
        }
        finally
        {
            if (!planted)
            {
                Inventory.AddSeeds(definition, 1);
            }
        }

        LastMessage = !planted ? "Tanaman belum bisa ditanam di petak ini."
            : UsesWateringCan && !wateringInteraction.HasWateringCan
                ? definition.DisplayName + " ditanam. Ambil gembor di dekat sumur."
                : definition.DisplayName + " ditanam. Siram untuk mulai tumbuh.";
        return planted;
    }

    private void OnDisable()
    {
        FinishHarvest();
        if (wateringInteraction != null) wateringInteraction.Feedback -= ReportAction;
        TargetTile = null;
        if (highlighter != null)
        {
            highlighter.Show(null);
        }
    }

    private void ReportAction(string message)
    {
        LastActionFrame = Time.frameCount;
        LastMessage = message;
    }
}
