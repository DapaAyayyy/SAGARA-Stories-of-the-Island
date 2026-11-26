using System;
using UnityEngine;

namespace Sagara.DermagaV2
{
    [DefaultExecutionOrder(24)]
    [DisallowMultipleComponent]
    public sealed class DermagaPakDarmaEscort : MonoBehaviour
    {
        private static readonly int Walk = Animator.StringToHash("Base Layer.Expedition Walk");
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");

        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private Animator model;
        [SerializeField] private Transform player;
        [SerializeField] private Vector3[] waypoints = Array.Empty<Vector3>();

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private int nextPoint;
        private bool escortStarted;
        private bool walking;
        private bool leftBehind;

        internal bool HasArrived { get; private set; }

        private void Update()
        {
            if (quest.Phase != DermagaQuestPhase.BoardRestorationBoat)
            {
                if (escortStarted)
                {
                    StopEscort();
                }
                return;
            }

            if (HasArrived || waypoints.Length == 0 || !quest.CanShowInteractionHint)
            {
                SetWalking(false);
                return;
            }

            if (!escortStarted)
            {
                escortStarted = true;
                quest.SetExpeditionEscort(true);
            }

            Vector3 fromPlayer = player.position - transform.position;
            float distance = fromPlayer.magnitude;
            leftBehind = distance > (leftBehind ? 5f : 7.5f);
            if (leftBehind)
            {
                SetWalking(false);
                return;
            }

            Vector3 target = waypoints[nextPoint];
            Vector3 delta = target - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= .025f)
            {
                nextPoint++;
                if (nextPoint == waypoints.Length)
                {
                    SetWalking(false);
                    HasArrived = true;
                    return;
                }
                target = waypoints[nextPoint];
                delta = target - transform.position;
                delta.y = 0f;
            }

            Vector3 position = Vector3.MoveTowards(transform.position,
                new Vector3(target.x, transform.position.y, target.z), 1.8f * Time.deltaTime);
            if (!TryGround(position, out float floor))
            {
                SetWalking(false);
                return;
            }
            position.y = floor;
            if (delta.sqrMagnitude > .001f)
            {
                var facing = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta),
                    480f * Time.deltaTime);
                transform.SetPositionAndRotation(position, facing);
            }
            SetWalking(true);
        }

        private bool TryGround(Vector3 position, out float y)
        {
            y = position.y;
            float best = .54f;
            int count = Physics.RaycastNonAlloc(position + Vector3.up * .62f, Vector3.down,
                hits, 1.24f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                float error = Mathf.Abs(hit.point.y - position.y);
                if (hit.normal.y < .55f || error >= best || hit.collider.transform.IsChildOf(transform)
                    || hit.collider.transform.IsChildOf(player))
                {
                    continue;
                }
                y = hit.point.y;
                best = error;
            }
            return best < .54f;
        }

        private void SetWalking(bool value)
        {
            if (walking == value)
            {
                return;
            }
            walking = value;
            model.CrossFadeInFixedTime(walking ? Walk : Idle, .16f);
        }

        private void StopEscort()
        {
            SetWalking(false);
            quest.SetExpeditionEscort(false);
            escortStarted = false;
        }

        private void OnDisable()
        {
            if (escortStarted)
            {
                StopEscort();
            }
        }
    }
}

