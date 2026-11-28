using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerWateringInteraction : MonoBehaviour
{
    private static readonly int PickupTrigger = Animator.StringToHash("Pickup");
    private static readonly int WateringTrigger = Animator.StringToHash("Watering");
    private static readonly int IsWalkingParameter = Animator.StringToHash("isWalking");
    private const float ActionTimeout = 2.5f;

    [SerializeField] private GameObject wateringCanAtWell;
    [SerializeField] private GameObject wateringCanInHand;
    [SerializeField] private Transform spout;
    [SerializeField] private ParticleSystem waterDrops;
    [SerializeField, Min(0.1f)] private float pickupDistance = 1.15f;

    public bool HasWateringCan { get; private set; }
    public bool IsBusy => action != WateringAction.None;
    public bool CanPickUp => isActiveAndEnabled && !IsBusy && !HasWateringCan
        && wateringCanAtWell != null && wateringCanAtWell.activeInHierarchy
        && (wateringCanAtWell.transform.position - transform.position).sqrMagnitude
            <= pickupDistance * pickupDistance;
    public event Action<string> Feedback;

    private Animator animator;
    private FarmTile pendingTile;
    private WateringAction action;
    private float actionDeadline;

    private enum WateringAction
    {
        None,
        Pickup,
        Pour
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (wateringCanInHand != null) wateringCanInHand.SetActive(false);
    }

    private void Update()
    {
        // Release input even if the Animator is disabled or misses a clip event.
        if (IsBusy && Time.time >= actionDeadline) EndAction();
    }

    public bool TryPickUp()
    {
        if (!CanPickUp || !CanAnimate(PickupTrigger)) return false;

        Vector3 direction = wateringCanAtWell.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        BeginAction(WateringAction.Pickup, PickupTrigger);
        return true;
    }

    public bool TryWater(FarmTile tile)
    {
        if (IsBusy || !HasWateringCan || tile == null || tile.Crop == null
            || tile.Crop.IsMature || tile.IsWatered || !CanAnimate(WateringTrigger))
        {
            return false;
        }

        pendingTile = tile;
        wateringCanInHand.SetActive(true);
        BeginAction(WateringAction.Pour, WateringTrigger);
        return true;
    }

    public void CollectWateringCan()
    {
        if (action != WateringAction.Pickup || HasWateringCan || wateringCanAtWell == null) return;

        HasWateringCan = true;
        wateringCanAtWell.SetActive(false);
        wateringCanInHand.SetActive(true);
        Feedback?.Invoke("Gembor diambil. Hadap ke tanaman untuk menyiram.");
    }

    public void PourWater()
    {
        if (action != WateringAction.Pour) return;

        FarmTile tile = pendingTile;
        pendingTile = null;
        if (tile == null || !tile.TryWater()) return;

        EmitWater(tile);
        Feedback?.Invoke("Tanaman disiram. Tunggu sampai tahap berikutnya.");
    }

    public void FinishPickup()
    {
        if (action == WateringAction.Pickup) EndAction();
    }

    public void FinishWatering()
    {
        if (action == WateringAction.Pour) EndAction();
    }

    private bool CanAnimate(int trigger)
    {
        if (wateringCanInHand == null || animator == null || !animator.isActiveAndEnabled
            || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == trigger && parameter.type == AnimatorControllerParameterType.Trigger)
                return true;
        }

        return false;
    }

    private void BeginAction(WateringAction nextAction, int trigger)
    {
        action = nextAction;
        actionDeadline = Time.time + ActionTimeout;
        animator.SetBool(IsWalkingParameter, false);
        animator.SetTrigger(trigger);
    }

    private void EndAction()
    {
        if (animator != null && animator.isActiveAndEnabled && IsBusy)
        {
            animator.ResetTrigger(action == WateringAction.Pickup ? PickupTrigger : WateringTrigger);
        }

        action = WateringAction.None;
        pendingTile = null;
        if (wateringCanInHand != null) wateringCanInHand.SetActive(false);
    }

    private void EmitWater(FarmTile tile)
    {
        if (spout == null || waterDrops == null) return;

        Vector3 target = tile.SoilRenderer.bounds.center;
        target.y = tile.SoilRenderer.bounds.max.y;
        Vector3 gravity = Physics.gravity * waterDrops.main.gravityModifier.constant;
        waterDrops.Play();

        for (int i = 0; i < 28; i++)
        {
            Vector2 spread = UnityEngine.Random.insideUnitCircle * 0.32f;
            Vector3 landing = target + new Vector3(spread.x, 0f, spread.y);
            float duration = UnityEngine.Random.Range(0.20f, 0.42f);
            var drop = new ParticleSystem.EmitParams
            {
                position = spout.position,
                velocity = (landing - spout.position) / duration - 0.5f * gravity * duration,
                startLifetime = duration,
                startSize = UnityEngine.Random.Range(0.035f, 0.065f)
            };
            waterDrops.Emit(drop, 1);
        }
    }

    private void OnDisable() => EndAction();
}
