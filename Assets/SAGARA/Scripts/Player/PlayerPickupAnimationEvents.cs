using UnityEngine;

public class PlayerPickupAnimationEvents : MonoBehaviour
{
    private PlayerTrashInteraction interaction;
    private PlayerWateringInteraction wateringInteraction;
    private PlayerFarmingInteraction farmingInteraction;

    private void Awake()
    {
        interaction = GetComponentInParent<PlayerTrashInteraction>();
        wateringInteraction = GetComponentInParent<PlayerWateringInteraction>();
        farmingInteraction = GetComponentInParent<PlayerFarmingInteraction>();
    }

    public void ReleasePickupMovement()
    {
        if (interaction != null)
        {
            interaction.ReleasePickupMovement();
        }
        if (wateringInteraction != null) wateringInteraction.FinishPickup();
        if (farmingInteraction != null) farmingInteraction.FinishHarvest();
    }

    public void CollectPendingTrash()
    {
        if (interaction != null)
        {
            interaction.CollectPendingTrash();
        }
        if (wateringInteraction != null) wateringInteraction.CollectWateringCan();
        if (farmingInteraction != null) farmingInteraction.CollectPendingHarvest();
    }

    public void PourWater()
    {
        if (wateringInteraction != null) wateringInteraction.PourWater();
    }

    public void FinishWatering()
    {
        if (wateringInteraction != null) wateringInteraction.FinishWatering();
    }
}
