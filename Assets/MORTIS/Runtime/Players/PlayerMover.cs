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

        [Header("Gravity")]
        [SerializeField] float gravity = -9.81f;
        [SerializeField] float groundedGravity = -2f;

        CharacterController cc;
        Vector3 velocity;  // y only used for gravity

        void Awake() => cc = GetComponent<CharacterController>();

        void Update()
        {
            if (!IsOwner) return;

            // --- INPUT (hybrid) ---
            Vector2 move = GetMoveInput();
            bool sprint = GetSprintInput();

            // world-space move based on player forward/right
            Vector3 wish = (transform.right * move.x + transform.forward * move.y).normalized;
            float finalSpeed = sprint ? speed * sprintMultiplier : speed;

            // --- GRAVITY ---
            if (cc.isGrounded && velocity.y < 0f)
                velocity.y = groundedGravity;
            else
                velocity.y += gravity * Time.deltaTime;

            // --- MOVE ---
            Vector3 delta = (wish * finalSpeed + new Vector3(0, velocity.y, 0)) * Time.deltaTime;
            cc.Move(delta);
        }

        static Vector2 NormalizeCardinal(Vector2 v)
        {
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }

        Vector2 GetMoveInput()
        {
            // New Input System
            #if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                float h = (kb.aKey.isPressed ? -1f : 0f) + (kb.dKey.isPressed ? 1f : 0f);
                float v = (kb.sKey.isPressed ? -1f : 0f) + (kb.wKey.isPressed ? 1f : 0f);
                return NormalizeCardinal(new Vector2(h, v));
            }
            #endif
            // Legacy Input Manager
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
    }
}
