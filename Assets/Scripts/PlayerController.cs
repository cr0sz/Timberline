using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour

{
    public bool IsMoving { get; private set; }
    public Animator animator;
    public float moveSpeed = 4f;
    public float rotationSpeed = 10f;
    public Transform cameraTransform;
    public Transform cameraFollowTarget;

    [Tooltip("Camera follow smoothing (seconds). 0 = hard lock.")]
    public float cameraSmoothTime = 0.12f;

    CharacterController controller;
    float baseMoveSpeed;   // captured before any speed upgrades / save-load
    Vector3 camVel;        // SmoothDamp velocity for the camera follow

    // Decaying knockback velocity (m/s), driven by PlayerHitFeedback on a hit.
    Vector3 knockback;
    const float knockbackTau = 0.1f;   // time constant; distance ~= speed * tau

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        baseMoveSpeed = moveSpeed;
    }

    // Shove the player. `velocity` is initial m/s; it decays to ~0 over ~0.3s.
    public void AddKnockback(Vector3 velocity) => knockback = velocity;

    void Update()
    {
        // Touch joystick / gamepad drives the virtual gamepad; read it first.
        Vector2 input = Gamepad.current != null
            ? Gamepad.current.leftStick.ReadValue()
            : Vector2.zero;

        // WASD fallback for testing in the editor on PC. When any key is held it
        // takes over, so you can drive the character without dragging the stick.
        if (Keyboard.current != null)
        {
            Vector2 kb = Vector2.zero;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)    kb.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)  kb.y -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) kb.x += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  kb.x -= 1f;
            if (kb != Vector2.zero) input = kb.normalized;
        }

        // Scale by the speed-upgrade factor so the walk cycle keeps up instead of
        // foot-sliding at high moveSpeed.
        float speedFactor = baseMoveSpeed > 0.01f ? moveSpeed / baseMoveSpeed : 1f;
        animator.SetFloat("Speed", input.magnitude * speedFactor);
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * input.y + camRight * input.x;
        IsMoving = moveDir.sqrMagnitude > 0.001f;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            controller.Move(moveDir * moveSpeed * Time.deltaTime);
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        if (knockback.sqrMagnitude > 0.0001f)
        {
            controller.Move(knockback * Time.deltaTime);
            knockback *= Mathf.Exp(-Time.deltaTime / knockbackTau);
            if (knockback.sqrMagnitude < 0.01f) knockback = Vector3.zero;
        }

        if (!controller.isGrounded)
        {
            controller.Move(Physics.gravity * Time.deltaTime);
        }
    }

    void LateUpdate()
    {
        // Smooth the follow instead of hard-locking — reads much better on phone.
        cameraFollowTarget.position = cameraSmoothTime > 0f
            ? Vector3.SmoothDamp(cameraFollowTarget.position, transform.position, ref camVel, cameraSmoothTime)
            : transform.position;
    }
}