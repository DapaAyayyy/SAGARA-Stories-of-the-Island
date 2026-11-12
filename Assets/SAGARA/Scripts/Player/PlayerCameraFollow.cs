using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] private CharacterController target;
    [SerializeField] private Terrain boundaryTerrain;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 12f, -10f);
    [SerializeField] private float focusHeight = 0.35f;
    [SerializeField, Min(0f)] private float forwardFraming = 0f;
    [SerializeField, Min(0f)] private float movementLookAhead = 0.15f;
    [SerializeField, Min(0.01f)] private float followSmoothTime = 0.14f;
    [SerializeField, Min(0.01f)] private float minZoom = 4.2f;
    [SerializeField, Min(0.01f)] private float maxZoom = 9f;

    private Camera view;
    private Vector3 cameraVelocity;
    private Vector3 cameraLead;
    private Vector3 cameraLeadVelocity;
    private float zoom;

    private void Awake()
    {
        view = GetComponent<Camera>();
        zoom = Mathf.Clamp(view.orthographicSize, minZoom, maxZoom);
    }

    private void Start()
    {
        if (target == null)
        {
            return;
        }

        if (cameraOffset.sqrMagnitude > 0f)
        {
            transform.rotation = Quaternion.LookRotation(-cameraOffset);
        }

        transform.position = ClampToTerrain(GetFocusPosition() + cameraOffset);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(target.velocity, Vector3.up);
        Vector3 desiredLead = Vector3.ClampMagnitude(horizontalVelocity, 1f) * movementLookAhead;
        cameraLead = Vector3.SmoothDamp(cameraLead, desiredLead, ref cameraLeadVelocity, 0.2f);

        if (cameraOffset.sqrMagnitude > 0f)
        {
            transform.rotation = Quaternion.LookRotation(-cameraOffset);
        }

        if (view.orthographic)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                zoom = Mathf.Clamp(zoom - mouse.scroll.ReadValue().y * 0.007f, minZoom, maxZoom);
            }

            view.orthographicSize = Mathf.Lerp(
                view.orthographicSize, zoom, 1f - Mathf.Exp(-5f * Time.deltaTime));
        }

        Vector3 desiredPosition = ClampToTerrain(GetFocusPosition() + cameraOffset + cameraLead);
        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref cameraVelocity, followSmoothTime);
        Vector3 boundedPosition = ClampToTerrain(nextPosition);

        if (boundedPosition.x != nextPosition.x) cameraVelocity.x = 0f;
        if (boundedPosition.z != nextPosition.z) cameraVelocity.z = 0f;
        transform.position = boundedPosition;
    }

    private Vector3 GetFocusPosition()
    {
        Vector3 forward = Vector3.ProjectOnPlane(-cameraOffset, Vector3.up).normalized;
        return target.transform.position + Vector3.up * focusHeight + forward * forwardFraming;
    }

    private Vector3 ClampToTerrain(Vector3 position)
    {
        if (boundaryTerrain == null || boundaryTerrain.terrainData == null)
        {
            return position;
        }

        if (!TryGetGroundFootprint(position, out Bounds footprint))
        {
            return position;
        }

        Vector3 terrainMin = boundaryTerrain.transform.position;
        Vector3 terrainSize = boundaryTerrain.terrainData.size;
        Vector3 terrainMax = terrainMin + terrainSize;

        if (view.orthographic)
        {
            float fitScale = Mathf.Min(
                terrainSize.x / Mathf.Max(footprint.size.x, 0.001f),
                terrainSize.z / Mathf.Max(footprint.size.z, 0.001f));
            if (fitScale < 1f)
            {
                view.orthographicSize *= fitScale;
                zoom = Mathf.Min(zoom, view.orthographicSize);
                if (!TryGetGroundFootprint(position, out footprint)) return position;
            }
        }

        float minX = position.x + terrainMin.x - footprint.min.x;
        float maxX = position.x + terrainMax.x - footprint.max.x;
        float minZ = position.z + terrainMin.z - footprint.min.z;
        float maxZ = position.z + terrainMax.z - footprint.max.z;
        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : (minX + maxX) * 0.5f;
        position.z = minZ <= maxZ ? Mathf.Clamp(position.z, minZ, maxZ) : (minZ + maxZ) * 0.5f;
        return position;
    }

    private bool TryGetGroundFootprint(Vector3 position, out Bounds footprint)
    {
        var ground = new Plane(Vector3.up, boundaryTerrain.transform.position);
        Vector3 translation = position - transform.position;
        footprint = new Bounds();

        for (int corner = 0; corner < 4; corner++)
        {
            Ray ray = view.ViewportPointToRay(new Vector3(corner % 2, corner / 2, 0f));
            ray.origin += translation;
            if (!ground.Raycast(ray, out float distance)) return false;

            Vector3 point = ray.GetPoint(distance);
            if (corner == 0) footprint = new Bounds(point, Vector3.zero);
            else footprint.Encapsulate(point);
        }

        return true;
    }

    private void OnValidate()
    {
        minZoom = Mathf.Max(0.01f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
    }
}
