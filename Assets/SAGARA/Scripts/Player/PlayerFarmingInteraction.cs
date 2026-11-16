using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(FarmInventory), typeof(FarmTileHighlighter))]
public sealed class PlayerFarmingInteraction : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float interactionDistance = 0.9f;

    public FarmTile TargetTile { get; private set; }
    public FarmInventory Inventory { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;
    public int LastActionFrame { get; private set; } = -1;

    private FarmTileHighlighter highlighter;
    private PlayerTrashInteraction trashInteraction;

    public string ActionPrompt
    {
        get
        {
            if (TargetTile == null)
            {
                return "Dekati dan hadapkan karakter ke petak.";
            }

            if (!TargetTile.IsTilled)
            {
                return "[E] Cangkul tanah";
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

            return crop.IsWatered
                ? "Sedang tumbuh · " + Mathf.CeilToInt(crop.SecondsRemaining) + " dtk"
                : "[E] Siram " + crop.Definition.DisplayName;
        }
    }

    private void Awake()
    {
        Inventory = GetComponent<FarmInventory>();
        highlighter = GetComponent<FarmTileHighlighter>();
        trashInteraction = GetComponent<PlayerTrashInteraction>();
    }

    private void LateUpdate()
    {
        if (trashInteraction != null && trashInteraction.isActiveAndEnabled
            && trashInteraction.HasInteractionPriority)
        {
            TargetTile = null;
            highlighter.Show(null);
            return;
        }

        Vector3 probeOrigin = transform.position
            + transform.forward * interactionDistance + Vector3.up;
        TargetTile = Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit,
            2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.collider.GetComponent<FarmTile>()
            : null;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteractTarget();
        }

        highlighter.Show(TargetTile);
    }

    public bool TryInteractTarget()
    {
        if (TargetTile == null)
        {
            return false;
        }

        LastActionFrame = Time.frameCount;

        if (!TargetTile.IsTilled)
        {
            return TryTillTarget();
        }

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
            if (!TargetTile.TryHarvest(out CropDefinition definition, out int amount))
            {
                return false;
            }

            Inventory.AddHarvest(definition, amount);
            LastMessage = "+" + amount + " " + definition.DisplayName + " masuk hasil panen.";
            return true;
        }

        if (TargetTile.TryWater())
        {
            LastMessage = "Tanaman disiram. Tunggu sampai tahap berikutnya.";
            return true;
        }

        LastMessage = "Tanaman sedang tumbuh.";
        return false;
    }

    public bool TryTillTarget()
    {
        if (TargetTile == null || !TargetTile.TryTill())
        {
            return false;
        }

        LastMessage = "Tanah siap ditanami.";
        LastActionFrame = Time.frameCount;
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

        LastMessage = planted ? definition.DisplayName + " ditanam. Siram untuk mulai tumbuh."
            : "Tanaman belum bisa ditanam di petak ini.";
        return planted;
    }

    private void OnDisable()
    {
        TargetTile = null;
        if (highlighter != null)
        {
            highlighter.Show(null);
        }
    }
}
