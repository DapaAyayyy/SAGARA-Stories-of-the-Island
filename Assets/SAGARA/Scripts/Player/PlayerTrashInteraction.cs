using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(TrashInventory))]
public sealed class PlayerTrashInteraction : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float pickupDistance = 1.5f;
    [SerializeField, Min(0f)] private float maximumHeightDifference = 1.25f;
    [SerializeField, Range(-1f, 1f)] private float minimumFacingDot = 0.25f;

    public TrashPickup Target { get; private set; }
    public TrashInventory Inventory { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;
    public int LastActionFrame { get; private set; } = -1;
    public bool HasInteractionPriority => Target != null || LastActionFrame == Time.frameCount;
    public string ActionPrompt => Target == null ? string.Empty : "[E] Ambil " + Target.DisplayName;

    private void Awake()
    {
        Inventory = GetComponent<TrashInventory>();
    }

    private void Update()
    {
        Target = FindTarget();
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryPickupTarget();
        }
    }

    private TrashPickup FindTarget()
    {
        TrashPickup nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (TrashPickup pickup in TrashPickup.ActivePickups)
        {
            if (pickup == null || !pickup.isActiveAndEnabled || pickup.IsCollected)
            {
                continue;
            }

            Vector3 offset = pickup.transform.position - transform.position;
            if (Mathf.Abs(offset.y) > maximumHeightDifference)
            {
                continue;
            }

            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (distance > pickupDistance * pickupDistance || distance >= nearestDistance
                || (distance > 0.01f
                    && Vector3.Dot(transform.forward, offset.normalized) < minimumFacingDot))
            {
                continue;
            }

            nearest = pickup;
            nearestDistance = distance;
        }

        return nearest;
    }

    public bool TryPickupTarget()
    {
        // Recheck the current position so teleports and repeated calls use a valid target.
        Target = FindTarget();
        if (Target == null || !Target.TryCollect(Inventory))
        {
            return false;
        }

        LastMessage = Target.DisplayName + " dikumpulkan.";
        LastActionFrame = Time.frameCount;
        Target = null;
        return true;
    }

    private void OnDisable()
    {
        Target = null;
    }
}
