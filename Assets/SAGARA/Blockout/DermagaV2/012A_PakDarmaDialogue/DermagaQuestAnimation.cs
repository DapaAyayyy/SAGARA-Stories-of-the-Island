using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sagara.DermagaV2
{
    [DefaultExecutionOrder(-45)]
    [DisallowMultipleComponent]
    public sealed class DermagaQuestAnimation : MonoBehaviour
    {
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int Talking = Animator.StringToHash("Base Layer.Quest Talk");
        private static readonly int Pickup = Animator.StringToHash("Base Layer.Quest Pickup");
        private static readonly int Repair = Animator.StringToHash("Base Layer.Quest Repair");
        private static readonly int Planting = Animator.StringToHash("Base Layer.Quest Plant");
        private static readonly int Walking = Animator.StringToHash("isWalking");

        [SerializeField] private DermagaIntroProgress quest;
        [SerializeField] private PlayerNpcInteraction dialogue;
        [SerializeField] private PlayerTrashInteraction collector;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private CharacterController character;
        [SerializeField] private Animator playerAnimator;
        [SerializeField] private Animator pakDarmaAnimator;
        [SerializeField] private GameObject repairHammer;

        private Action contact;
        private float contactAt;
        private float releaseAt;
        private bool pakSpeaking;
        private Transform rightHand;
        private GameObject heldSeedling;
        private GameObject heldHammer;

        internal bool Busy { get; private set; }
        internal string Reply { get; private set; } = string.Empty;

        private void Awake()
        {
            rightHand = Array.Find(playerAnimator.GetComponentsInChildren<Transform>(true),
                bone => bone.name.EndsWith(":RightHand", StringComparison.Ordinal));
        }

        private void Update()
        {
            if (Busy)
            {
                TickAction();
                return;
            }

            if (!quest.ReadyForPickup || !collector.enabled || !collector.Target || !character.isGrounded
                || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            {
                return;
            }

            // Consume this pickup before the shared collector's Update. Recheck the target at contact.
            TrashPickup target = collector.Target;
            collector.enabled = false;
            Begin(Pickup, target.transform.position, .86f, 1.58f, () =>
            {
                if (target)
                {
                    target.TryCollect(collector.Inventory);
                }
            });
        }

        private void LateUpdate()
        {
            bool talking = dialogue.IsTalking && quest.IsPakDarma(dialogue.Speaker);
            bool speaking = talking && dialogue.CurrentLine != null && !dialogue.IsLineComplete;
            if (speaking != pakSpeaking)
            {
                pakDarmaAnimator.CrossFadeInFixedTime(speaking ? Talking : Idle, .14f);
                pakSpeaking = speaking;
            }
            // Post-dialogue "Siap, Pak" player reply & animation intentionally omitted for snappier pacing.
            Reply = string.Empty;
        }

        internal bool TryRepair(Vector3 workPoint, Action commit)
        {
            if (Busy || !character.isGrounded)
            {
                return false;
            }

            if (rightHand && repairHammer)
            {
                heldHammer = Instantiate(repairHammer, rightHand, false);
                Transform grip = heldHammer.transform;
                grip.localPosition = new Vector3(.01f, .04f, .03f);
                grip.localRotation = Quaternion.Euler(8f, 90f, 88f);
                foreach (var collider in heldHammer.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
                var renderers = heldHammer.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var mesh in renderers)
                    {
                        bounds.Encapsulate(mesh.bounds);
                    }
                    grip.localScale *= .43f / Mathf.Max(.01f, bounds.size.magnitude);
                }
            }
            Begin(Repair, workPoint, 1.28f, 2.06f, commit);
            return true;
        }

        internal bool TryPickupAt(Vector3 position, Action commit)
        {
            if (Busy || !character.isGrounded)
            {
                return false;
            }

            Begin(Pickup, position, .86f, 1.58f, commit);
            return true;
        }

        internal bool TryPlant(GameObject sapling, Action commit)
        {
            if (Busy || !character.isGrounded)
            {
                return false;
            }

            ShowSeedlingInHand(sapling);
            Begin(Planting, sapling.transform.position, 1.02f, 1.76f, () =>
            {
                ClearHeldSeedling();
                commit();
            });
            return true;
        }

        private void ShowSeedlingInHand(GameObject sapling)
        {
            if (!rightHand || !sapling.TryGetComponent(out MeshFilter source)
                || !sapling.TryGetComponent(out MeshRenderer appearance))
            {
                return;
            }

            heldSeedling = new GameObject("Bibit di tangan");
            var grip = heldSeedling.transform;
            grip.SetParent(rightHand, false);
            grip.localPosition = new Vector3(0f, .045f, .018f);
            grip.localRotation = Quaternion.Euler(72f, 0f, 18f);
            grip.localScale = Vector3.one * (.29f / Mathf.Max(.01f, rightHand.lossyScale.x));
            heldSeedling.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            heldSeedling.AddComponent<MeshRenderer>().sharedMaterials = appearance.sharedMaterials;
        }

        private void ClearHeldSeedling()
        {
            if (heldSeedling)
            {
                Destroy(heldSeedling);
                heldSeedling = null;
            }
        }

        private void Begin(int state, Vector3 target, float delay, float duration, Action commit)
        {
            Vector3 facing = target - character.transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > .012f)
            {
                character.transform.rotation = Quaternion.LookRotation(facing);
            }

            Busy = true;
            movement.enabled = false;
            dialogue.enabled = false;
            playerAnimator.SetBool(Walking, false);
            playerAnimator.CrossFadeInFixedTime(state, .12f);
            contact = commit;
            contactAt = Time.time + delay;
            releaseAt = Time.time + duration;
        }

        private void TickAction()
        {
            if (Time.time >= contactAt && contact != null)
            {
                Action commit = contact;
                contact = null;
                commit();
            }
            if (Time.time >= releaseAt)
            {
                Release();
            }
        }

        private void Release()
        {
            Busy = false;
            ClearHeldSeedling();
            if (heldHammer)
            {
                Destroy(heldHammer);
                heldHammer = null;
            }
            contact = null;
            Reply = string.Empty;
            playerAnimator.CrossFadeInFixedTime(Idle, .16f);
            movement.enabled = true;
            dialogue.enabled = true;
        }

        private void OnDisable()
        {
            if (Busy)
            {
                Release();
            }
        }
    }
}

