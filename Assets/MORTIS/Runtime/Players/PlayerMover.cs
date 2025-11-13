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
        [SerializeField] float airControl = 5f;     // how strongly you can steer in air
        [SerializeField] float airFriction = 0.5f;  // how fast momentum decays when no input

        [Header("Camera Motion")]
        [SerializeField] CameraMotionController cameraMotion;

        CharacterController cc;

        // y = vertical, xz = horizontal
        float verticalVelocity;            // vertical speed (jump / gravity)
        Vector3 horizontalVelocity;        // world-space horizontal velocity

        bool _jumpHeld;
        bool wasGrounded;

        // Public accessors for trampoline & others
        public bool IsGrounded      => cc.isGrounded;
        public float VerticalVelocity => verticalVelocity;
        public bool JumpHeld        => _jumpHeld;
        public float HorizontalSpeed => new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;

        void Awake() => cc = GetComponent<CharacterController>();

        void Update()
        {
            if (!IsOwner) return;

            // --- INPUT ---
            Vector2 moveInput   = GetMoveInput();
            bool   sprint       = GetSprintInput();
            bool   jumpPressed  = GetJumpInputPressed();
            _jumpHeld           = GetJumpInputHeld();

            // world-space desired direction from input
            Vector3 wishDir = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            float   targetSpeed = sprint ? speed * sprintMultiplier : speed;

            bool grounded = cc.isGrounded;
            float prevYVel = verticalVelocity;

            // --- VERTICAL (GROUND + JUMP + GRAVITY) ---

            // slight downward stick when grounded
            if (grounded && verticalVelocity < 0f)
                verticalVelocity = groundedGravity;

            // jump
            if (grounded && jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                cameraMotion?.OnJump();
            }

            // gravity (always)
            verticalVelocity += gravity * Time.deltaTime;

            // --- HORIZONTAL (GROUND VS AIR) ---

            if (grounded)
            {
                // On ground: direct control, very responsive
                horizontalVelocity = wishDir * targetSpeed;
            }
            else
            {
                // In air: keep momentum, steer toward wishDir
                Vector3 targetVel = wishDir * targetSpeed;

                // steer towards target (air control)
                float ac = airControl * Time.deltaTime;
                if (ac > 1f) ac = 1f;
                horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVel, ac);

                // if no input, apply gentle drag so you don't slide forever
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

            // --- LANDING DETECTION ---

            if (!wasGrounded && grounded)
            {
                float impact = Mathf.Abs(prevYVel);
                cameraMotion?.OnLand(impact);
            }

            wasGrounded = grounded;

            // feed state to camera motion
            cameraMotion?.SetLocomotionState(moveInput, grounded, sprint);
        }

        // Allow external forces (e.g. trampoline) to modify vertical velocity safely
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
