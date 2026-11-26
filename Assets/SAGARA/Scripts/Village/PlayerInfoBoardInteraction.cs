using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dipasang di Player. Saat Player berdiri dekat papan informasi, tombol E membuka papan.
/// Selama papan terbuka Player tidak bisa berjalan; E atau Esc menutup papan.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInfoBoardInteraction : MonoBehaviour
{
    private static readonly int IsWalkingParameter = Animator.StringToHash("isWalking");

    private PlayerMovement movement;
    private Animator animator;
    private PlayerNpcInteraction npc;
    private PlayerTrashInteraction trash;

    public InfoBoard Target { get; private set; }
    public InfoBoard OpenBoard { get; private set; }
    public bool IsReading => OpenBoard != null;
    public string ActionPrompt => Target == null ? string.Empty : Target.Prompt;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        animator = GetComponentInChildren<Animator>();
        npc = GetComponent<PlayerNpcInteraction>();
        trash = GetComponent<PlayerTrashInteraction>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (IsReading)
        {
            if (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }

            return;
        }

        // Jangan membuka papan saat Player sedang bicara dengan NPC.
        if (npc != null && npc.IsTalking)
        {
            Target = null;
            return;
        }

        // Sampah di dekat Player didahulukan, supaya satu tombol E tidak membuka papan sekaligus.
        bool trashBusy = trash != null && trash.isActiveAndEnabled && trash.HasInteractionPriority;
        Target = trashBusy ? null : FindTarget();
        if (Target != null && keyboard.eKey.wasPressedThisFrame)
        {
            Open(Target);
        }
    }

    private InfoBoard FindTarget()
    {
        InfoBoard nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (InfoBoard board in InfoBoard.ActiveBoards)
        {
            if (board == null || board.EntryCount == 0)
            {
                continue;
            }

            Vector3 offset = board.transform.position - transform.position;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            float range = board.ReadDistance;
            if (distance <= range * range && distance < nearestDistance)
            {
                nearest = board;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void Open(InfoBoard board)
    {
        OpenBoard = board;
        Target = null;
        SetMovementLocked(true);
    }

    private void Close()
    {
        OpenBoard = null;
        SetMovementLocked(false);
    }

    private void SetMovementLocked(bool locked)
    {
        if (movement != null)
        {
            movement.enabled = !locked;
        }

        // PlayerMovement berhenti di-update saat dimatikan, jadi animasi jalan dihentikan di sini.
        if (locked && animator != null)
        {
            animator.SetBool(IsWalkingParameter, false);
        }
    }

    private void OnDisable()
    {
        if (IsReading)
        {
            Close();
        }

        Target = null;
    }
}
