using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class SceneTransitionTrigger : MonoBehaviour
{
    [SerializeField, Tooltip("Scene name or full Assets/.../.unity path, enabled in the Build Profiles scene list.")]
    private string destinationScene;
    [SerializeField, Tooltip("ID of the arrival point in the destination scene. Leave empty to keep its default player position.")]
    private string destinationSpawnId;
    [SerializeField] private PanelSettings transitionPanel;
    [SerializeField, Min(0f)] private float fadeDuration = 0.3f;

    private void Reset() => ConfigurePhysics();

    private void Awake() => ConfigurePhysics();

    private void ConfigurePhysics()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || SceneTransitionFade.IsTransitioning)
        {
            return;
        }

        CharacterController player = other.GetComponentInParent<CharacterController>();
        if (player == null || !player.CompareTag("Player"))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(destinationScene)
            || !Application.CanStreamedLevelBeLoaded(destinationScene))
        {
            Debug.LogWarning($"Scene transition '{name}': enable destination '{destinationScene}' in the Build Profiles scene list.", this);
            return;
        }

        if (transitionPanel == null)
        {
            Debug.LogWarning($"Scene transition '{name}': assign Transition Panel before using this exit.", this);
            return;
        }

        SceneTransitionFade.Load(destinationScene, transitionPanel, fadeDuration, destinationSpawnId);
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider area = GetComponent<BoxCollider>();
        if (area == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
