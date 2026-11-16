using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public sealed class FarmTile : MonoBehaviour
{
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

    [SerializeField] private CropGrowth cropPrefab;
    [SerializeField] private Color wateredTint = new Color(0.55f, 0.55f, 0.6f, 1f);

    public bool IsTilled { get; private set; }
    public MeshRenderer SoilRenderer { get; private set; }
    public CropGrowth Crop { get; private set; }
    public bool IsWatered => Crop != null && Crop.IsWatered;

    private MaterialPropertyBlock soilProperties;
    private Color dryColor;

    private void Awake()
    {
        SoilRenderer = GetComponent<MeshRenderer>();
        soilProperties = new MaterialPropertyBlock();
        dryColor = SoilRenderer.sharedMaterial.GetColor(BaseColorProperty);
        RefreshSoil();
    }

    public bool TryTill()
    {
        if (IsTilled)
        {
            return false;
        }

        IsTilled = true;
        RefreshSoil();
        return true;
    }

    public bool TryPlant(CropDefinition definition)
    {
        if (!IsTilled || Crop != null || cropPrefab == null
            || definition == null || !definition.IsValid)
        {
            return false;
        }

        Vector3 surfaceCenter = SoilRenderer.localBounds.center;
        surfaceCenter.y = SoilRenderer.localBounds.max.y;
        CropGrowth instance = Instantiate(cropPrefab,
            SoilRenderer.transform.TransformPoint(surfaceCenter), transform.rotation);
        instance.transform.SetParent(transform, true);
        instance.Initialize(definition);
        Crop = instance;
        Crop.Changed += RefreshSoil;
        RefreshSoil();
        return true;
    }

    public bool TryWater()
    {
        return Crop != null && Crop.TryWater();
    }

    public bool TryHarvest(out CropDefinition definition, out int amount)
    {
        definition = null;
        amount = 0;
        if (Crop == null || !Crop.IsMature)
        {
            return false;
        }

        definition = Crop.Definition;
        amount = definition.HarvestYield;
        Crop.Changed -= RefreshSoil;
        Crop.gameObject.SetActive(false);
        Destroy(Crop.gameObject);
        Crop = null;
        RefreshSoil();
        return true;
    }

    private void RefreshSoil()
    {
        SoilRenderer.enabled = IsTilled;
        Color color = IsWatered ? dryColor * wateredTint : dryColor;
        color.a = dryColor.a;
        SoilRenderer.GetPropertyBlock(soilProperties);
        soilProperties.SetColor(BaseColorProperty, color);
        SoilRenderer.SetPropertyBlock(soilProperties);
    }

    private void OnDestroy()
    {
        if (Crop != null)
        {
            Crop.Changed -= RefreshSoil;
        }
    }
}
