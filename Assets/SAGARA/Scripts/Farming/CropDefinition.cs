using UnityEngine;

[CreateAssetMenu(menuName = "SAGARA/Farming/Crop Definition")]
public sealed class CropDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Wortel";
    [SerializeField] private GameObject[] stagePrefabs;
    [SerializeField, Min(0.1f)] private float secondsPerStage = 15f;
    [SerializeField, Min(0.1f)] private float visualScale = 1.6f;
    [SerializeField, Min(1)] private int harvestYield = 1;

    public string DisplayName => displayName;
    public int StageCount => stagePrefabs == null ? 0 : stagePrefabs.Length;
    public float SecondsPerStage => Mathf.Max(0.1f, secondsPerStage);
    public float VisualScale => Mathf.Max(0.1f, visualScale);
    public int HarvestYield => Mathf.Max(1, harvestYield);

    public bool IsValid
    {
        get
        {
            if (StageCount < 2)
            {
                return false;
            }

            foreach (GameObject stage in stagePrefabs)
            {
                if (stage == null)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public GameObject GetStagePrefab(int stageIndex)
    {
        return stagePrefabs[stageIndex];
    }
}
