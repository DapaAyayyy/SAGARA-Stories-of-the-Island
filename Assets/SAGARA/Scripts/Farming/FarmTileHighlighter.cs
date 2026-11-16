using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class FarmTileHighlighter : MonoBehaviour
{
    [SerializeField] private Material highlightMaterial;

    private LineRenderer outline;
    private readonly Vector3[] corners = new Vector3[4];

    private void Awake()
    {
        var indicator = new GameObject("FarmTileTarget");
        indicator.transform.SetParent(transform, false);
        outline = indicator.AddComponent<LineRenderer>();
        outline.sharedMaterial = highlightMaterial;
        outline.useWorldSpace = true;
        outline.loop = true;
        outline.positionCount = corners.Length;
        outline.widthMultiplier = 0.035f;
        outline.shadowCastingMode = ShadowCastingMode.Off;
        outline.receiveShadows = false;
        outline.enabled = false;
    }

    public void Show(FarmTile tile)
    {
        outline.enabled = tile != null;
        if (tile == null)
        {
            return;
        }

        MeshRenderer surface = tile.SoilRenderer;
        Bounds bounds = surface.localBounds;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        corners[0] = new Vector3(min.x, max.y, min.z);
        corners[1] = new Vector3(min.x, max.y, max.z);
        corners[2] = new Vector3(max.x, max.y, max.z);
        corners[3] = new Vector3(max.x, max.y, min.z);

        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = surface.transform.TransformPoint(corners[i])
                + surface.transform.up * 0.025f;
        }

        outline.SetPositions(corners);
    }

    private void OnDisable()
    {
        if (outline != null)
        {
            outline.enabled = false;
        }
    }
}
