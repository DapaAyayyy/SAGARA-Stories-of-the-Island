using UnityEngine;

namespace Sagara.DermagaV2
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrashPickup))]
    public sealed class DermagaCleanupDebris : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color SelectionTint = new Color(1.34f, 1.22f, 1.10f, 1f);

        private enum Resource
        {
            BeachTrash,
            ForestBranch
        }

        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private Resource resource;
        [SerializeField, Range(0, DermagaIntroProgress.BranchCount - 1)] private int slot;

        internal int Slot => slot;

        private TrashPickup pickup;
        private Renderer[] branchRenderers;
        private MaterialPropertyBlock[] originalProperties;
        private MaterialPropertyBlock selectionProperties;
        private bool highlighted;

        private void Awake()
        {
            pickup = GetComponent<TrashPickup>();
            if (resource != Resource.ForestBranch)
                return;

            branchRenderers = GetComponentsInChildren<Renderer>();
            originalProperties = new MaterialPropertyBlock[branchRenderers.Length];
            for (int i = 0; i < originalProperties.Length; i++)
                originalProperties[i] = new MaterialPropertyBlock();
            selectionProperties = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (resource == Resource.ForestBranch)
                SetHighlighted(quest && quest.IsBranchTarget(pickup));
        }

        private void SetHighlighted(bool selected)
        {
            if (selected == highlighted)
                return;

            for (int i = 0; i < branchRenderers.Length; i++)
            {
                var branch = branchRenderers[i];
                if (selected)
                {
                    var material = branch.sharedMaterial;
                    if (!material || !material.HasProperty(BaseColor))
                        continue;

                    branch.GetPropertyBlock(originalProperties[i]);
                    branch.GetPropertyBlock(selectionProperties);
                    var color = selectionProperties.HasColor(BaseColor)
                        ? selectionProperties.GetColor(BaseColor) : material.GetColor(BaseColor);
                    selectionProperties.SetColor(BaseColor, color * SelectionTint);
                    branch.SetPropertyBlock(selectionProperties);
                }
                else
                {
                    var material = branch.sharedMaterial;
                    if (material && material.HasProperty(BaseColor))
                        branch.SetPropertyBlock(originalProperties[i].isEmpty ? null : originalProperties[i]);
                }
            }

            highlighted = selected;
        }

        private void OnDisable()
        {
            SetHighlighted(false);
            // TrashPickup marks itself collected before deactivating and destroying its GameObject.
            if (!pickup || !pickup.IsCollected || !quest)
                return;

            if (resource == Resource.ForestBranch)
                quest.RecordBranch(slot);
            else
                quest.RecordDebris(slot);
        }
    }
}
