using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FarmInventory : MonoBehaviour
{
    [SerializeField] private CropDefinition crop;
    [SerializeField, Min(0)] private int startingSeeds = 20;

    public CropDefinition Crop => crop;
    public int SeedCount => GetCount(seeds, crop);
    public int HarvestCount => GetCount(harvest, crop);

    private readonly Dictionary<CropDefinition, int> seeds = new Dictionary<CropDefinition, int>();
    private readonly Dictionary<CropDefinition, int> harvest = new Dictionary<CropDefinition, int>();

    private void Awake()
    {
        AddSeeds(crop, startingSeeds);
    }

    public bool TryConsumeSeed(CropDefinition definition)
    {
        int count = GetCount(seeds, definition);
        if (count == 0)
        {
            return false;
        }

        seeds[definition] = count - 1;
        return true;
    }

    public void AddSeeds(CropDefinition definition, int count)
    {
        if (definition != null && count > 0)
        {
            seeds[definition] = GetCount(seeds, definition) + count;
        }
    }

    public void AddHarvest(CropDefinition definition, int count)
    {
        if (definition != null && count > 0)
        {
            harvest[definition] = GetCount(harvest, definition) + count;
        }
    }

    private static int GetCount(Dictionary<CropDefinition, int> items, CropDefinition definition)
    {
        return definition != null && items.TryGetValue(definition, out int count) ? count : 0;
    }
}
