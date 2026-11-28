using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrashPickup : MonoBehaviour
{
    private static readonly HashSet<TrashPickup> activePickups = new HashSet<TrashPickup>();

    [SerializeField] private string displayName = "Sampah";
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private bool countsForVillageQuest;
    public static IEnumerable<TrashPickup> ActivePickups => activePickups;
    public string DisplayName => displayName;
    public bool IsCollected { get; private set; }

    private void OnEnable()
    {
        if (!IsCollected)
        {
            activePickups.Add(this);
        }
    }

    private void OnDisable()
    {
        activePickups.Remove(this);
    }

    public bool TryCollect(TrashInventory inventory)
    { 
        if (inventory == null || IsCollected || !isActiveAndEnabled)
        {
            return false;
        }

        IsCollected = true;
        inventory.Add(Mathf.Max(1, amount));
        if (countsForVillageQuest)
        {
            QuestLog.Laporkan(QuestLog.JenisSampah, 1);
        } 
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
