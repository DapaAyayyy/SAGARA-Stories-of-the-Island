using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(1000)]
public sealed class DermagaCameraPresentation : MonoBehaviour
{
    [SerializeField] private PlayerCameraFollow follow;
    [SerializeField] private float minimumZoom = 4.2f;
    [SerializeField] private float maximumZoom = 8f;
    [SerializeField] private float zoomPerWheelStep = 0.35f;
    [SerializeField] private Vector2 worldMin = new Vector2(-58f, -36f);
    [SerializeField] private Vector2 worldMax = new Vector2(44f, 63f);
    [SerializeField] private float groundHeight = 0.4f;
    private Camera view;
    private FieldInfo zoomField;
    public bool BindingValid { get; private set; }
    public Vector3 LastBoundaryCorrection { get; private set; }

    private void Awake()
    {
        view = GetComponent<Camera>();
        if (follow == null) follow = GetComponent<PlayerCameraFollow>();
        // The shared component has no public zoom input API. Cache its existing
        // target field; only wheel events adjust that target. It still performs
        // all following and zoom smoothing in its own LateUpdate.
        zoomField = typeof(PlayerCameraFollow).GetField("zoom", BindingFlags.Instance | BindingFlags.NonPublic);
        BindingValid = follow != null && zoomField != null && zoomField.FieldType == typeof(float);
        if (!BindingValid) Debug.LogError("Dermaga camera: shared follow zoom binding is unavailable.", this);
    }

    private void Update()
    {
        if (!BindingValid || Mouse.current == null) return;
        float wheel = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) < 0.0001f) return;
        // Compensate only for the extra sensitivity. The shared follow script
        // subsequently contributes its existing 0.007f factor once.
        float current = (float)zoomField.GetValue(follow);
        float target = Mathf.Clamp(current - wheel * (zoomPerWheelStep - 0.007f), minimumZoom, maximumZoom);
        zoomField.SetValue(follow, target);
    }

    private void LateUpdate()
    {
        if (view == null || !view.orthographic) return;
        var plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
        Vector3 low = Vector3.zero, high = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            Ray ray = view.ViewportPointToRay(new Vector3(i % 2, i / 2, 0f));
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 point = ray.GetPoint(distance);
            if (i == 0) low = high = point;
            else { low = Vector3.Min(low, point); high = Vector3.Max(high, point); }
        }
        float x = low.x < worldMin.x ? worldMin.x - low.x : high.x > worldMax.x ? worldMax.x - high.x : 0f;
        float z = low.z < worldMin.y ? worldMin.y - low.z : high.z > worldMax.y ? worldMax.y - high.z : 0f;
        LastBoundaryCorrection = new Vector3(x, 0f, z);
        transform.position += LastBoundaryCorrection;
    }
}
