using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId;
    [SerializeField] private CharacterController player;

    public static bool TryPlacePlayer(Scene scene, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        SceneSpawnPoint match = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SceneSpawnPoint point in root.GetComponentsInChildren<SceneSpawnPoint>())
            {
                if (!point.isActiveAndEnabled || point.spawnId != id) continue;
                if (match != null)
                {
                    Debug.LogWarning($"Scene '{scene.name}' has duplicate spawn ID '{id}'. Keeping the default player position.");
                    return false;
                }
                match = point;
            }
        }

        if (match == null || match.player == null || match.player.gameObject.scene != scene)
        {
            Debug.LogWarning($"Scene '{scene.name}': arrival point '{id}' is missing or has no local Player assigned. Keeping the default player position.");
            return false;
        }

        bool wasEnabled = match.player.enabled;
        match.player.enabled = false;
        match.player.transform.SetPositionAndRotation(match.transform.position, match.transform.rotation);
        match.player.enabled = wasEnabled;
        Physics.SyncTransforms();
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.3f);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.15f, transform.forward * 1.5f);
    }
}
