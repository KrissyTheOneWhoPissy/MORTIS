using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MORTIS.Players
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMover : NetworkBehaviour
    {
        [Header("Move")]
        [SerializeField] float speed = 4.5f;
        [SerializeField] float sprintMultiplier = 1.5f;

        [Header("Jump")]
        [SerializeField] float jumpHeight = 1.6f;

        [Header("Gravity")]
        [SerializeField] float gravity = -9.81f;
        [SerializeField] float groundedGravity = -2f;

        [Header("Air Control")]
        [SerializeField] float airControl = 5f;
        [SerializeField] float airFriction = 0.5f;

        [Header("Camera Motion")]
        [SerializeField] CameraMotionController cameraMotion;

        [Header("Climbing")]
        [SerializeField] LedgeClimber ledgeClimber;

        [Header("Animation (Optional)")]
        [SerializeField] MortisAnimatorDriver animatorDriver; // <-- your bridge script

        CharacterController cc;

        float verticalVelocity;
        Vector3 horizontalVelocity;

        bool jumpHeld;
        bool wasGrounded;

        // Public accessors
        public bool IsGrounded => cc != null && cc.isGrounded;
        public float VerticalVelocity => verticalVelocity;
        public bool JumpHeld => jumpHeld;
        public float HorizontalSpeed => new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;
        // Useful for animation normalization
        public float MaxGroundSpeed => speed * sprintMultiplier;
        public bool IsSprinting { get; private set; }

        void Awake()
        {
            cc = GetComponent<CharacterController>();

            if (ledgeClimber == null)
                ledgeClimber = GetComponent<LedgeClimber>();

            if (animatorDriver == null)
                animatorDriver = GetComponent<MortisAnimatorDriver>(); // optional component

            // Initialize
            wasGrounded = cc.isGrounded;
        }

        void Update()
        {
            if (!IsOwner) return;

            // --- INPUT ---
            Vector2 moveInput = GetMoveInput();
            bool sprint = GetSprintInput();
            bool jumpPressed = GetJumpInputPressed();
            jumpHeld = GetJumpInputHeld();

            IsSprinting = sprint;

            float prevYVel = verticalVelocity;

            // Grounded is most reliable AFTER Move, but we need an initial value too
            bool groundedBeforeMove = cc.isGrounded;

            // --- LEDGE CLIMBING HANDOFF ---
            if (ledgeClimber != null)
            {
                if (ledgeClimber.IsBusy)
                {
                    ledgeClimber.Tick(Time.deltaTime, moveInput, jumpPressed);
                    cameraMotion?.SetLocomotionState(moveInput, cc.isGrounded, sprint);
                    wasGrounded = cc.isGrounded;
                    return;
                }

                bool wantLedgeGrab = jumpPressed || (!groundedBeforeMove && jumpHeld);
                if (wantLedgeGrab && ledgeClimber.TryStartLedgeHang())
                {
                    verticalVelocity = groundedGravity;
                    cameraMotion?.OnJump();
                    wasGrounded = cc.isGrounded;
                    return;
                }
            }

            // --- VERTICAL (GROUND + JUMP + GRAVITY) ---

            // stick to ground
            if (groundedBeforeMove && verticalVelocity < 0f)
                verticalVelocity = groundedGravity;

            // jump
            if (groundedBeforeMove && jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                cameraMotion?.OnJump();
                animatorDriver?.TriggerJump(); // <-- hook animator here
            }

            // gravity
            verticalVelocity += gravity * Time.deltaTime;

            // --- HORIZONTAL (GROUND VS AIR) ---

            Vector3 wishDir = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            float targetSpeed = sprint ? speed * sprintMultiplier : speed;

            if (groundedBeforeMove)
            {
                horizontalVelocity = wishDir * targetSpeed;
            }
            else
            {
                Vector3 targetVel = wishDir * targetSpeed;

                float ac = airControl * Time.deltaTime;
                if (ac > 1f) ac = 1f;
                horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVel, ac);

                if (wishDir.sqrMagnitude < 0.001f)
                {
                    float drag = airFriction * Time.deltaTime;
                    if (drag > 1f) drag = 1f;
                    horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, drag);
                }
            }

            // --- MOVE CHARACTER ---
            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            cc.Move(velocity * Time.deltaTime);

            // grounded after move is the one you want for landing detection
            bool groundedAfterMove = cc.isGrounded;

            // --- LANDING DETECTION ---
            if (!wasGrounded && groundedAfterMove)
            {
                float impact = Mathf.Abs(prevYVel);
                cameraMotion?.OnLand(impact);
                // If you later want a "Land" trigger, call it here.
            }

            wasGrounded = groundedAfterMove;

            cameraMotion?.SetLocomotionState(moveInput, groundedAfterMove, sprint);
        }

        public void ApplyVerticalImpulse(float newUpwardVelocity)
        {
            if (newUpwardVelocity > verticalVelocity)
                verticalVelocity = newUpwardVelocity;
        }

        static Vector2 NormalizeCardinal(Vector2 v)
        {
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }

        Vector2 GetMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                float h = (kb.aKey.isPressed ? -1f : 0f) + (kb.dKey.isPressed ? 1f : 0f);
                float v = (kb.sKey.isPressed ? -1f : 0f) + (kb.wKey.isPressed ? 1f : 0f);
                return NormalizeCardinal(new Vector2(h, v));
            }
#endif
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            return NormalizeCardinal(new Vector2(x, y));
        }

        bool GetSprintInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null) return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
#endif
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        bool GetJumpInputPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null) return kb.spaceKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(KeyCode.Space);
        }

        bool GetJumpInputHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null) return kb.spaceKey.isPressed;
#endif
            return Input.GetKey(KeyCode.Space);
        }
    }
}
