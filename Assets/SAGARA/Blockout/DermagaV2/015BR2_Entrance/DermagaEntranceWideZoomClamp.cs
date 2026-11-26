using UnityEngine;

namespace Sagara.DermagaV2
{
    [DefaultExecutionOrder(1010)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class DermagaEntranceWideZoomClamp : MonoBehaviour
    {
        private const float NormalSize = 5.3f;
        private const float FullyActiveDistance = 5f;
        private const float FadeOutDistance = 10f;
        private const float LeftViewportMargin = 0.028f;
        private const float HeroHalfWidth = 0.60f;
        private const float TopViewportLimit = 0.83f;
        private const float MaximumLeftShift = 2.85f;
        private const float MaximumUpShift = 0.90f;

        [SerializeField] private CharacterController player;
        [SerializeField] private Transform entranceBoundary;

        private Camera view;
        private Vector3 appliedOffset;

        private void Awake()
        {
            view = GetComponent<Camera>();
        }

        private void Update()
        {
            // Let the existing follow and bounds calculate from their own prior position.
            RemoveOffset();
        }

        private void LateUpdate()
        {
            if (!view.orthographic || view.orthographicSize <= NormalSize || !player || !entranceBoundary)
            {
                return;
            }

            Vector3 foot = player.transform.position;
            Vector3 offset = foot - entranceBoundary.position;
            float distance = new Vector2(offset.x, offset.z).magnitude;
            if (distance >= FadeOutDistance)
            {
                return;
            }

            float height = view.orthographicSize * 2f;
            float width = height * view.aspect;
            float left = view.WorldToViewportPoint(foot).x * width - HeroHalfWidth;
            float top = view.WorldToViewportPoint(foot + Vector3.up * player.height).y;
            float leftShift = Mathf.Clamp(LeftViewportMargin * width - left, 0f, MaximumLeftShift);
            float upShift = Mathf.Clamp((top - TopViewportLimit) * height, 0f, MaximumUpShift);

            float entranceWeight = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(FullyActiveDistance, FadeOutDistance, distance));
            float zoomWeight = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(NormalSize, NormalSize + 0.35f, view.orthographicSize));

            appliedOffset = (-transform.right * leftShift + transform.up * upShift) * (entranceWeight * zoomWeight);
            transform.position += appliedOffset;
        }

        private void OnDisable()
        {
            RemoveOffset();
        }

        private void RemoveOffset()
        {
            transform.position -= appliedOffset;
            appliedOffset = Vector3.zero;
        }
    }
}
