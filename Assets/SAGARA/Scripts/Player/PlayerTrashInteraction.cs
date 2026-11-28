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
    public bool IsPickingUp { get; private set; }
    public bool HasInteractionPriority =>
        IsPickingUp || Target != null || LastActionFrame == Time.frameCount;
    public string ActionPrompt => Target == null ? string.Empty : "[E] Ambil " + Target.DisplayName;

    private Animator animator;
    private TrashPickup pendingPickup;
    private PlayerWateringInteraction wateringInteraction;
    private PlayerFarmingInteraction farmingInteraction;
    private bool InteractionBlocked => SceneTransitionFade.IsTransitioning
        || (wateringInteraction != null && wateringInteraction.IsBusy)
        || (farmingInteraction != null && farmingInteraction.IsHarvesting);

    private void Awake()
    {
        Inventory = GetComponent<TrashInventory>();
        animator = GetComponentInChildren<Animator>();
        wateringInteraction = GetComponent<PlayerWateringInteraction>();
        farmingInteraction = GetComponent<PlayerFarmingInteraction>();
    }

    private void Update()
    {
        if (InteractionBlocked)
        {
            Target = null;
            return;
        }

        Target = IsPickingUp ? null : FindTarget();
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
        if (IsPickingUp || animator == null || Inventory == null || InteractionBlocked)
            return false;

        Target = FindTarget();
        if (Target == null)
            return false;

        pendingPickup = Target;
        Target = null;

        IsPickingUp = true;
        LastActionFrame = Time.frameCount;

        animator.SetBool("isWalking", false);
        animator.SetTrigger("Pickup");

        return true;
    }

    public void CollectPendingTrash()
    {
        if (!IsPickingUp || pendingPickup == null)
            return;

        TrashPickup pickup = pendingPickup;
        pendingPickup = null;

        string displayName = pickup.DisplayName;

        if (pickup.TryCollect(Inventory))
        {
            LastMessage = displayName + " dikumpulkan.";
            LastActionFrame = Time.frameCount;
        }
    }

    public void ReleasePickupMovement()
    {
        IsPickingUp = false;
        pendingPickup = null;
    }

    private void OnDisable()
    {
        Target = null;
        IsPickingUp = false;
        pendingPickup = null;
    }
}
