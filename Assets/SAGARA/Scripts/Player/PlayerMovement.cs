using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private static readonly int IsWalkingParameter = Animator.StringToHash("isWalking");

    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private Animator animator;
    private float verticalSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        Vector3 direction = Vector3.zero;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) direction += Vector3.forward;
            if (keyboard.sKey.isPressed) direction += Vector3.back;
            if (keyboard.aKey.isPressed) direction += Vector3.left;
            if (keyboard.dKey.isPressed) direction += Vector3.right;
        }

        direction = Vector3.ClampMagnitude(direction, 1f);

        if (animator != null)
        {
            animator.SetBool(IsWalkingParameter, direction.sqrMagnitude > 0f);
        }

        if (direction.sqrMagnitude > 0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -2f;
        }

        verticalSpeed += gravity * Time.deltaTime;

        Vector3 velocity = direction * moveSpeed;
        velocity.y = verticalSpeed;
        controller.Move(velocity * Time.deltaTime);
    }
}
