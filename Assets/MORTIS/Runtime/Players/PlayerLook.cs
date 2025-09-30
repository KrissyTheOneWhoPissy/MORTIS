using Unity.Netcode;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MORTIS.Players
{
    public class PlayerLook : NetworkBehaviour
    {
        [SerializeField] Transform cameraTransform;
        [SerializeField] float sensitivity = 0.15f;
        [SerializeField] float minPitch = -75f, maxPitch = 75f;

        float pitch;
        bool cursorLocked;

        void OnEnable()  { if (IsOwner) SetCursorLocked(true); }
        void OnDisable() { if (IsOwner) SetCursorLocked(false); }

        void Update()
        {
            if (!IsOwner) return;

            // Esc toggles cursor
            #if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetCursorLocked(!cursorLocked);
            #else
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(!cursorLocked);
            #endif
            if (!cursorLocked) return; // don't look around when unlocked

            Vector2 delta = GetMouseDelta();
            float dx = delta.x * sensitivity;
            float dy = delta.y * sensitivity;

            transform.Rotate(0f, dx, 0f);
            pitch = Mathf.Clamp(pitch - dy, minPitch, maxPitch);
            if (cameraTransform) cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        void SetCursorLocked(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !locked;
        }

        Vector2 GetMouseDelta()
        {
            #if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m != null) return m.delta.ReadValue();
            #endif
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
        }
    }
}
