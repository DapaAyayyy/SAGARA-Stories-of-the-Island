using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CropGrowth : MonoBehaviour
{
    public CropDefinition Definition { get; private set; }
    public int StageIndex { get; private set; }
    public bool IsWatered { get; private set; }
    public bool IsMature => Definition != null && StageIndex == Definition.StageCount - 1;
    public float SecondsRemaining => Definition == null ? 0f
        : Mathf.Max(0f, Definition.SecondsPerStage - growthElapsed);
    public event Action Changed;

    private float growthElapsed;
    private GameObject stageVisual;

    public void Initialize(CropDefinition definition)
    {
        if (definition == null || !definition.IsValid)
        {
            throw new ArgumentException("Crop needs at least two valid stage prefabs.", nameof(definition));
        }

        Definition = definition;
        StageIndex = 0;
        growthElapsed = 0f;
        IsWatered = false;
        ShowStage();
        Changed?.Invoke();
    }

    private void Update()
    {
        AdvanceGrowth(Time.deltaTime);
    }

    public bool TryWater()
    {
        if (Definition == null || IsMature || IsWatered)
        {
            return false;
        }

        IsWatered = true;
        Changed?.Invoke();
        return true;
    }

    public void AdvanceGrowth(float elapsedSeconds)
    {
        if (!IsWatered || IsMature || elapsedSeconds <= 0f)
        {
            return;
        }

        growthElapsed += elapsedSeconds;
        if (growthElapsed < Definition.SecondsPerStage)
        {
            return;
        }

        StageIndex++;
        growthElapsed = 0f;
        IsWatered = false;
        ShowStage();
        Changed?.Invoke();
    }

    private void ShowStage()
    {
        if (stageVisual != null)
        {
            stageVisual.SetActive(false);
            Destroy(stageVisual);
        }

        stageVisual = Instantiate(Definition.GetStagePrefab(StageIndex), transform);
        stageVisual.transform.localPosition = Vector3.zero;
        stageVisual.transform.localRotation = Quaternion.identity;
        stageVisual.transform.localScale = Vector3.one * Definition.VisualScale;
    }
}
