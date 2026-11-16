using UnityEngine;

[DisallowMultipleComponent]
public sealed class TrashInventory : MonoBehaviour
{
    public int TotalCollected { get; private set; }

    public void Add(int amount)
    {
        if (amount > 0)
        {
            TotalCollected += amount;
        }
    }
}
