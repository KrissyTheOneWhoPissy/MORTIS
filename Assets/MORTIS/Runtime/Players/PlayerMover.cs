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

        [Header("Crouch")]
        [SerializeField] float crouchHeight = 1.2f;             // CharacterController height while crouched
        [SerializeField] float crouchSpeedMultiplier = 0.55f;   // speed scale while crouched
        [SerializeField] float crouchLerpSpeed = 14f;           // how fast the controller resizes
        [SerializeField] LayerMask standCheckMask = ~0;         // ceiling check mask (everything by default)

        [Header("Camera Motion")]
        [SerializeField] CameraMotionController cameraMotion;

        [Header("Climbing")]
        [SerializeField] LedgeClimber ledgeClimber;

        [Header("Animation (Optional)")]
        [SerializeField] MortisAnimatorDriver animatorDriver; // <-- your bridge script

        [Header("UI Prompt")]
        [SerializeField] ActionPromptUI actionPromptUI; // <-- drag PlayerUI's ActionPromptUI here (or auto-find)
        [SerializeField] string ledgePromptText = "Press Space to Climb";

        CharacterController cc;

        float verticalVelocity;
        Vector3 horizontalVelocity;

        bool jumpHeld;
        bool wasGrounded;

        // --- Crouch state ---
        public bool IsCrouching { get; private set; }
        float standHeight;
        Vector3 standCenter;

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

            // NEW: find the prompt UI on this player (even if disabled)
            if (actionPromptUI == null)
                actionPromptUI = GetComponentInChildren<ActionPromptUI>(true);

            // Cache standing controller dimensions
            standHeight = cc.height;
            standCenter = cc.center;

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
            bool crouchHeld = GetCrouchInputHeld();

            IsSprinting = sprint;

            float prevYVel = verticalVelocity;

            // Grounded is most reliable AFTER Move, but we need an initial value too
            bool groundedBeforeMove = cc.isGrounded;

            // --- CROUCH (resize controller + animator bool) ---
            HandleCrouch(crouchHeld);
            animatorDriver?.SetCrouching(IsCrouching);

            // --- LEDGE PROMPT + LEDGE CLIMBING HANDOFF ---
            bool canClimbLedgeNow = false;

            if (ledgeClimber != null && !ledgeClimber.IsBusy)
            {
                // Non-destructive probe so UI can show the prompt
                canClimbLedgeNow = ledgeClimber.CanStartLedgeHangNow();

                Debug.Log($"[LedgeUI] canClimb={canClimbLedgeNow} viewForward={(ledgeClimber.viewForward ? ledgeClimber.viewForward.name : "NULL")} promptUI={(actionPromptUI != null)}");

                if (canClimbLedgeNow)
                {
                    Debug.Log("[LedgeUI] Calling Show()");
                    actionPromptUI?.Show(ledgePromptText);
                }       
                else
                {    
                    Debug.Log("[LedgeUI] Calling Hide()");
                    actionPromptUI?.Hide();
                }    
            }
            else
            {
                // If we're busy climbing, never show the prompt
                actionPromptUI?.Hide();
            }

            if (ledgeClimber != null)
            {
                // If we are already in a climb state, let the climber run and exit
                if (ledgeClimber.IsBusy)
                {
                    ledgeClimber.Tick(Time.deltaTime, moveInput, jumpPressed);
                    cameraMotion?.SetLocomotionState(moveInput, cc.isGrounded, sprint);
                    wasGrounded = cc.isGrounded;
                    return;
                }

                // Prefer ledge grab when a ledge is valid
                bool wantLedgeGrab = canClimbLedgeNow && (jumpPressed || (!groundedBeforeMove && jumpHeld));

                if (wantLedgeGrab && ledgeClimber.TryStartLedgeHang())
                {
                    // Hide prompt immediately once we commit to the hang
                    actionPromptUI?.Hide();

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

            // jump (normal jump only if we didn't start a ledge hang above)
            if (groundedBeforeMove && jumpPressed)
            {
                // prevent prompt lingering if you jump away
                actionPromptUI?.Hide();

                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                cameraMotion?.OnJump();
                animatorDriver?.TriggerJump(); // <-- hook animator here
            }

            // gravity
            verticalVelocity += gravity * Time.deltaTime;

            // --- HORIZONTAL (GROUND VS AIR) ---

            Vector3 wishDir = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
            float targetSpeed = sprint ? speed * sprintMultiplier : speed;

            // crouch slows movement
            if (IsCrouching) targetSpeed *= crouchSpeedMultiplier;

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
            }

            wasGrounded = groundedAfterMove;

            cameraMotion?.SetLocomotionState(moveInput, groundedAfterMove, sprint);
        }

        public void ApplyVerticalImpulse(float newUpwardVelocity)
        {
            if (newUpwardVelocity > verticalVelocity)
                verticalVelocity = newUpwardVelocity;
        }

        // -------------------- CROUCH --------------------

        void HandleCrouch(bool wantCrouch)
        {
            if (cc == null) return;

            // If trying to stand, only allow if there is room above.
            if (!wantCrouch && IsCrouching)
            {
                if (!CanStandUp())
                    wantCrouch = true;
            }

            IsCrouching = wantCrouch;

            float targetHeight = IsCrouching ? crouchHeight : standHeight;

            Vector3 targetCenter = IsCrouching
                ? new Vector3(standCenter.x, targetHeight * 0.5f, standCenter.z)
                : standCenter;

            cc.height = Mathf.Lerp(cc.height, targetHeight, Time.deltaTime * crouchLerpSpeed);
            cc.center = Vector3.Lerp(cc.center, targetCenter, Time.deltaTime * crouchLerpSpeed);
        }

        bool CanStandUp()
        {
            float r = cc.radius;
            Vector3 feet = transform.position;

            float currentTopY = feet.y + cc.height;
            float standTopY = feet.y + standHeight;

            if (standTopY <= currentTopY + 0.001f) return true;

            Vector3 start = new Vector3(feet.x, currentTopY - r * 0.25f, feet.z);
            float dist = (standTopY - currentTopY) + 0.02f;
            float headRadius = Mathf.Max(0.05f, r * 0.9f);

            bool hit = Physics.SphereCast(
                start,
                headRadius,
                Vector3.up,
                out _,
                dist,
                standCheckMask,
                QueryTriggerInteraction.Ignore
            );

            return !hit;
        }

        // -------------------- INPUT --------------------

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

        bool GetCrouchInputHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null) return kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed || kb.cKey.isPressed;
#endif
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.C);
        }
    }
}
